////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Core;
namespace OutSmart.DAXon.Values.Arrays
{
    /// <summary>
    /// A simple implementation of XDM array items, in which the array is backed by a Java List.
    /// </summary>
    internal sealed class SimpleArrayItem : AbstractArrayItem
    {
        public static readonly SimpleArrayItem EMPTY_ARRAY = new SimpleArrayItem(new List<IGroundedValue>());
        private readonly IList<IGroundedValue> _members;
        private IPingable conversionPingable;

        public override OperandRole[] OperandRoles => new OperandRole[]
            {
                OperandRole.SINGLE_ATOMIC
            };
        public SimpleArrayItem(IList<IGroundedValue> members)
        {
            this._members = members;
            if (members.Count >= MemoryLedger.BigCount)
            {
                HoldMembers(members);
            }
            else
            {
                MemoryLedger.Active?.Sample(this, 64 + 40L * members.Count);   // a chain of small arrays holds as much as a large one
            }
        }

        // A large member list, entered once in the ledger of the call making it; also for one still being filled.
        internal static void HoldMembers(IList<IGroundedValue> members)
        {
            MemoryLedger.HoldOnce(members, -1, MembersSizer);
        }

        private static readonly Func<object, long> MembersSizer = o => MemoryLedger.ListBytes((IList<IGroundedValue>)o);

        // The chunks a large array is collected in, summed as they grow.
        private static readonly Func<object, long> ChunksSizer = o =>
        {
            var chunks = (List<IGroundedValue[]>)o;
            long n = (long)chunks.Count * COLLECT_CHUNK;
            return n == 0 ? 0 : 24 + 8 * n + MemoryLedger.MembersBytes(chunks[0], COLLECT_CHUNK) / COLLECT_CHUNK * n;
        };

        public static SimpleArrayItem MakeSimpleArrayItem(ISequenceIterator input)
        {
            IList<IGroundedValue> members = input is Regex.SingleCharTokenIterator tok
                ? tok.DrainRemaining()               // exact-size fast path for array{tokenize(...)}
                : Collect(input);
            return new SimpleArrayItem(members);
        }

        // 64KB of refs per chunk: sub-LOH, so a large member sequence avoids the List doubling
        // ladder (repeated large-object allocations + copies, each a full-GC trigger candidate).
        private const int COLLECT_CHUNK = 8192;

        private static IList<IGroundedValue> Collect(ISequenceIterator input)
        {
            List<IGroundedValue> head = new List<IGroundedValue>();
            IItem item;
            while (head.Count < COLLECT_CHUNK && (item = input.Next()) != null)
            {
                head.Add(item);
            }

            if (head.Count < COLLECT_CHUNK)
            {
                return head;
            }

            // Large: accumulate fixed chunks, then assemble ONCE at exact size.
            List<IGroundedValue[]> chunks = new List<IGroundedValue[]>();
            IGroundedValue[] cur = new IGroundedValue[COLLECT_CHUNK];
            int used = 0;
            while ((item = input.Next()) != null)
            {
                if (used == COLLECT_CHUNK)
                {
                    chunks.Add(cur);
                    if (chunks.Count == 1)
                    {
                        MemoryLedger.Hold(chunks, ChunksSizer);
                    }

                    cur = new IGroundedValue[COLLECT_CHUNK];
                    used = 0;
                }

                cur[used++] = item;
            }

            List<IGroundedValue> all = new List<IGroundedValue>(head.Count + chunks.Count * COLLECT_CHUNK + used);
            all.AddRange(head);
            foreach (IGroundedValue[] c in chunks)
            {
                all.AddRange(c);
            }

            IGroundedValue[] tail = new IGroundedValue[used];
            Array.Copy(cur, tail, used);
            all.AddRange(tail);
            return all;
        }

        public void RequestNotification(IPingable informee)
        {
            this.conversionPingable = informee;
        }

        public void NotifyConversion()
        {
            if (conversionPingable != null)
            {
                conversionPingable.Ping();
            }
        }

        public override AnnotationList GetAnnotations()
        {
            return AnnotationList.EMPTY;
        }

        public override IGroundedValue Get(int index)
        {
            return _members[index];
        }

        public override ArrayItem Put(int index, IGroundedValue newValue)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.Put(index, newValue);
        }

        public override int ArrayLength()
        {
            return _members.Count;
        }

        public override bool IsEmpty()
        {
            return _members.Count == 0;
        }

        public override IEnumerable<IGroundedValue> Members()
        {
            return _members;
        }

        public override ISequenceIterator Parcels()
        {
            return (ISequenceIterator)(new SequenceIteratorOverJavaIterator<IGroundedValue>(_members.GetEnumerator(), (member) => new Parcel(member)));
        }

        public override ArrayItem RemoveSeveral(IntSet positions)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.RemoveSeveral(positions);
        }

        public override ArrayItem Remove(int pos)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.Remove(pos);
        }

        public override ArrayItem SubArray(int start, int end)
        {
            return new SimpleArrayItem(_members.GetRange(start, (end) - (start)));
        }

        public override ArrayItem Insert(int position, IGroundedValue member)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.Insert(position, member);
        }

        public override ArrayItem Append(IGroundedValue newMember)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.Append(newMember);
        }

        public override ArrayItem Concat(ArrayItem other)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.Concat(other);
        }

        public IList<IGroundedValue> GetMembers()
        {
            return _members;
        }

        public override string ToShortString()
        {
            int size = GetMembers().Count;
            if (size == 0)
            {
                return "[]";
            }
            else if (size > 5)
            {
                return "[(:size " + size + ":)]";
            }
            else
            {
                StringBuilder buff = new StringBuilder(256);
                buff.Append('[');
                foreach (IGroundedValue entry in Members())
                {
                    buff.Append(Err.DepictSequence(entry).ToString().Trim());
                    buff.Append(", ");
                }

                if (size == 1)
                {
                    buff.Append(']');
                }
                else
                {
                    buff[buff.Length - 2] = ']';
                }

                return buff.ToString().Trim();
            }
        }
    }
}
