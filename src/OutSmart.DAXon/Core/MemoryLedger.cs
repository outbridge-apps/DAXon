////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Core
{
    /// <summary>
    /// What a call holds besides the trees it was handed, for its ProcessorOptions.MaxMemoryBytes: the large structures it
    /// made - trees, strings, sequences, maps, arrays and the buffers that build them. Each is entered once and weakly:
    /// the ledger keeps nothing alive, and what the call dropped leaves the sum with the collection that frees it. Small
    /// trees are entered by sampling, about one per 64 KB (less under a small limit), each standing for the bytes around it.
    /// </summary>
    internal sealed class MemoryLedger
    {
        internal const int Big = 64 * 1024;       // bytes: a structure this large is entered on its own
        internal const int BigChars = Big / 2;
        internal const int BigCount = Big / 8;    // members: a sequence, map or array this long is entered on its own
        private readonly long interval;           // mean bytes between two sampled small structures

        internal readonly long Limit;

        private WeakReference[] holders;
        private long[] sizes;                     // -1: the sizer reads it from the holder
        private Func<object, long>[] sizers;
        private int count;
        private ConditionalWeakTable<object, object> shared;   // contents entered once, however many hold them
        private long untilSample;
        private uint random = 2463534242;
        private long collectedAt = long.MinValue / 2;   // the allocation counter at the last collection a sum made
        private int falseAlarms;                        // collections that found the call under its limit

        internal MemoryLedger(long limit)
        {
            Limit = limit;
            interval = Math.Max(1024, Math.Min(Big, limit / 256));   // a small limit is summed as finely
            untilSample = interval;
        }

        /// <summary>Bytes a call allocates between two sums: a sixteenth of its limit, from 256 KB to 32 MB.</summary>
        internal static long StepOf(long limit)
        {
            return Math.Max(256L * 1024, Math.Min(32L << 20, limit / 16));
        }

        // The ledger of the call running on this thread, or null when it has no memory limit.
        internal static MemoryLedger Active => Controller.ActiveLedger;

        /// <summary>A structure of a known size the active call made.</summary>
        internal static void Hold(object holder, long bytes)
        {
            Active?.Add(holder, bytes, null);
        }

        /// <summary>A structure that may still grow: the sizer reads its size when the ledger is summed.</summary>
        internal static void Hold(object holder, Func<object, long> sizer)
        {
            Active?.Add(holder, -1, sizer);
        }

        /// <summary>Contents that several values may hold - a string's storage, a list - entered once.</summary>
        internal static void HoldOnce(object holder, long bytes, Func<object, long> sizer)
        {
            MemoryLedger ledger = Active;
            if (ledger != null)
            {
                lock (ledger)
                {
                    ledger.shared ??= new ConditionalWeakTable<object, object>();
                    if (ledger.shared.TryGetValue(holder, out _))
                    {
                        return;
                    }

                    ledger.shared.Add(holder, Marker);
                    ledger.AddLocked(holder, bytes, sizer);
                }
            }
        }

        private static readonly object Marker = new object();

        /// <summary>
        /// A large string's storage (BigChars or more), entered once however many strings share it. Called from the
        /// constructors of every string, which must stay small enough to inline.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void HoldText(string storage)
        {
            HoldOnce(storage, 22 + 2L * storage.Length, null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void HoldText(byte[] storage)
        {
            HoldOnce(storage, 24 + storage.Length, null);
        }

        /// <summary>
        /// A small map or array, by its own bytes: sampled as small trees are, since a chain of them (each holding the
        /// last) holds as much as one large structure. A container holding one counts only its reference.
        /// </summary>
        internal static void SampleSmall(object holder, long bytes)
        {
            Active?.Sample(holder, bytes);
        }

        /// <summary>A record of values - a JSON object, a map of fixed shape: its object, its slots and its values.</summary>
        internal static long RecordBytes(IGroundedValue[] values)
        {
            long bytes = 56 + 8L * values.Length;
            foreach (IGroundedValue value in values)
            {
                bytes += MemberBytes(value);
            }

            return bytes;
        }

        /// <summary>A small tree: one in about Interval bytes is entered, weighted for the bytes it stands for.</summary>
        internal void Sample(object holder, long bytes)
        {
            untilSample -= bytes;
            if (untilSample > 0 || bytes <= 0)
            {
                return;
            }

            untilSample = NextInterval();
            double weight = bytes / (1 - Math.Exp(-(double)bytes / interval));
            Add(holder, (long)weight, null);
        }

        private long NextInterval()
        {
            random ^= random << 13;
            random ^= random >> 17;
            random ^= random << 5;
            double u = (random + 1.0) / 4294967297.0;
            return (long)(-Math.Log(u) * interval) + 1;
        }

        private void Add(object holder, long bytes, Func<object, long> sizer)
        {
            lock (this)
            {
                AddLocked(holder, bytes, sizer);
            }
        }

        private void AddLocked(object holder, long bytes, Func<object, long> sizer)
        {
            if (holders == null)
            {
                holders = new WeakReference[16];
                sizes = new long[16];
                sizers = new Func<object, long>[16];
            }
            else if (count == holders.Length && Prune() > holders.Length / 2)
            {
                Array.Resize(ref holders, holders.Length * 2);
                Array.Resize(ref sizes, sizes.Length * 2);
                Array.Resize(ref sizers, sizers.Length * 2);
            }

            holders[count] = new WeakReference(holder);
            sizes[count] = bytes;
            sizers[count] = sizer;
            count++;
        }

        // Drops the entries whose structure is gone; returns how many remain.
        private int Prune()
        {
            int live = 0;
            for (int i = 0; i < count; i++)
            {
                if (holders[i].IsAlive)
                {
                    holders[live] = holders[i];
                    sizes[live] = sizes[i];
                    sizers[live] = sizers[i];
                    live++;
                }
            }

            Array.Clear(holders, live, count - live);
            Array.Clear(sizers, live, count - live);
            count = live;
            return live;
        }

        /// <summary>What the structures the call made and still holds take; the trees it was handed are its token's.</summary>
        internal long Sum()
        {
            lock (this)
            {
                long total = 0;
                int live = 0;
                for (int i = 0; i < count; i++)
                {
                    object target = holders[i].Target;
                    if (target == null)
                    {
                        continue;
                    }

                    total += sizes[i] >= 0 ? sizes[i] : sizers[i](target);
                    holders[live] = holders[i];
                    sizes[live] = sizes[i];
                    sizers[live] = sizers[i];
                    live++;
                }

                if (holders != null)
                {
                    Array.Clear(holders, live, count - live);
                    Array.Clear(sizers, live, count - live);
                }

                count = live;
                return total;
            }
        }

        /// <summary>
        /// Whether the call holds more than its limit. A sum over it may still count what the call dropped and no
        /// collection has freed yet, so it is taken again after one; a collection that finds the call under its limit
        /// doubles the allocations to the next one.
        /// </summary>
        internal bool Over(long allocated, long inputs, out long held)
        {
            held = inputs + Sum();
            if (held <= Limit || allocated - collectedAt < Math.Min(Limit / 2 << Math.Min(falseAlarms, 3), 4 * Limit))
            {
                return false;
            }

            collectedAt = allocated;
            GC.Collect();
            held = inputs + Sum();
            if (held <= Limit)
            {
                falseAlarms++;
                return false;
            }

            return true;
        }

        /// <summary>Bytes a list of values holds: its references, and its members as a few spread over it tell.</summary>
        internal static long ListBytes<T>(IList<T> list)
        {
            int n = list.Count;
            return n == 0 ? 0 : 24 + 8L * n + MembersBytes(list, n);
        }

        internal static readonly Func<object, long> ItemListSizer = o => ListBytes((IList<IItem>)o);

        internal const int Probes = 8;

        // n members as up to eight of them, evenly spread, tell.
        internal static long MembersBytes<T>(IList<T> list, int n)
        {
            object[] probes = new object[Probes];
            int step = Math.Max(1, n / Probes);
            int taken = 0;
            for (int i = 0; i < n && taken < Probes; i += step)
            {
                probes[taken++] = list[i];
            }

            return ProbedBytes(probes, taken, n);
        }

        // n members as the probes tell; a probe repeating an earlier one counts nothing, for members many times held.
        internal static long ProbedBytes(object[] probes, int taken, int n)
        {
            long sampled = 0;
            for (int i = 0; i < taken; i++)
            {
                bool repeat = false;
                for (int j = 0; j < i && !repeat; j++)
                {
                    repeat = ReferenceEquals(probes[i], probes[j]);
                }

                if (!repeat)
                {
                    sampled += MemberBytes(probes[i]);
                }
            }

            return taken == 0 ? 0 : (long)((double)sampled / taken * n);
        }

        /// <summary>A map entry's bytes: the pair with its trie node, its key and its value.</summary>
        internal static long EntryBytes(object key, object value)
        {
            return 60 + MemberBytes(key) + (ReferenceEquals(key, value) ? 0 : MemberBytes(value));
        }

        /// <summary>
        /// A map holds its entries, which its versions share: about one entry in EntryInterval made is entered, standing for
        /// the entries around it, so the entries the call's maps keep count once however many versions share them.
        /// </summary>
        internal static void SampleEntry(object entry, object key, object value)
        {
            MemoryLedger ledger = Active;
            if (ledger != null && --ledger.untilEntry <= 0)
            {
                ledger.untilEntry = ledger.NextInterval() * EntryInterval / ledger.interval;
                ledger.Add(entry, EntryInterval * EntryBytes(key, value), null);
            }
        }

        private const int EntryInterval = 1024;
        private long untilEntry = EntryInterval;

        /// <summary>One member's own bytes; what a ledger enters on its own (a large string, a tree) adds nothing here.</summary>
        internal static long MemberBytes(object o)
        {
            switch (o)
            {
                case null:
                    return 0;
                case StringValue s:
                    UnicodeString u = s.UnicodeStringValue;
                    long length = u.Length();
                    return length >= BigChars ? 32 : 80 + length * (u.Width >> 3);
                case NodeInfo _:
                    return 40;
                case AtomicValue _:
                    return 32;
                case SequenceExtent extent:
                    return 32 + 40L * extent.GetLength();
                case OutSmart.DAXon.Values.Maps.MapItem _:
                case OutSmart.DAXon.Values.Arrays.ArrayItem _:
                    return 0;   // in the ledger on their own: a large one entered, a small one sampled
                default:
                    return 48;
            }
        }
    }
}
