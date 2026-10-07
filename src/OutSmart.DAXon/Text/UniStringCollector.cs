////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Text;

namespace OutSmart.DAXon.Text
{
    /// <summary>
    /// A UnicodeString consumer that assembles its input in Latin1 byte storage.
    /// 8-bit tokens (Slice8/Twine8 - tree-text slices, rendered numbers) append as a raw
    /// byte copy with no intermediate string; string-backed tokens (BMPString/StringView)
    /// narrow in one pass. The first codepoint above 0xFF switches the collector to a
    /// char-based StringBuilder for the rest of its life.
    ///
    /// Storage is chunked: the current chunk doubles from 256 bytes up to 64KB (sub-LOH);
    /// once full it is sealed as a Slice8 segment and a fresh chunk starts. Small results
    /// therefore behave exactly like a single growable buffer, while large results avoid
    /// the doubling ladder entirely: no copy-on-grow past 64KB and no 2x retention of a
    /// half-used final buffer. The result is width-honest: a single-chunk all-Latin1
    /// result wraps its buffer as a Slice8 (Width=8, zero-copy); a multi-chunk result is
    /// assembled ONCE at exact size into a flat Twine8 (a rope was tried and rejected --
    /// downstream per-codepoint paths pay more on composite strings than one exact copy
    /// costs); wide content converts via StringTool.FromCharSequence.
    /// </summary>
    internal sealed class UniStringCollector : AbstractUniStringConsumer, IUnicodeWriter
    {
        // IDisposable via IUnicodeWriter; in-memory collector, nothing to release.
        // 64KB chunk cap: below the LOH threshold, and each sealed chunk becomes one rope segment.
        private const int CHUNK = 1 << 16;

        private byte[] bytes = new byte[256];   // current (unsealed) chunk; doubles up to CHUNK
        private int used;                       // bytes used in the current chunk
        private List<UnicodeString> sealedChunks;  // full chunks sealed as Slice8 segments (null until first seal)
        private int sealedLength;               // total codepoints across sealed chunks
        private StringBuilder wide;             // non-null once a char > 0xFF has been seen

        public override IUniStringConsumer Accept(UnicodeString chars)
        {
            if (wide != null)
            {
                wide.Append(chars.ToString());
                WideGrown();
                return this;
            }

            if (chars is Slice8 s8)
            {
                AppendBytes(s8.ByteArray, s8.Start, s8.End);
                return this;
            }

            if (chars is Twine8 t8)
            {
                byte[] b = t8.ByteArray;
                AppendBytes(b, 0, b.Length);
                return this;
            }

            if (chars is BMPSlice sl)
            {
                // zero-copy token view: narrow the char window directly, no substring materialization
                AppendChars(sl.Backing, sl.Start, sl.End);
                return this;
            }

            // BMPString/StringView return their backing string without copying; other
            // (wide or composite) representations materialize here and then take the
            // wide switch below on their first >0xFF char.
            AppendString(chars.ToString());
            return this;
        }

        private void AppendBytes(byte[] src, int start, int end)
        {
            int n = end - start;
            while (n > 0)
            {
                if (used == bytes.Length)
                {
                    GrowOrSeal();
                }

                int k = Math.Min(n, bytes.Length - used);
                Buffer.BlockCopy(src, start, bytes, used, k);
                used += k;
                start += k;
                n -= k;
            }
        }

        // IUnicodeWriter face: lets this collector serve as the in-memory sink of a serializer
        // chain (fn:serialize), keeping Latin1 output on the byte path end to end.
        public void Write(UnicodeString chars)
        {
            Accept(chars);
        }

        public void Write(string chars)
        {
            if (wide != null)
            {
                wide.Append(chars);
                WideGrown();
                return;
            }

            AppendString(chars);
        }

        public void WriteAscii(byte[] content)
        {
            if (wide != null)
            {
                foreach (byte b in content)
                {
                    wide.Append((char)b);
                }

                WideGrown();
                return;
            }

            AppendBytes(content, 0, content.Length);
        }

        public void WriteCodePoint(int codepoint)
        {
            if (wide == null && codepoint <= 0xFF)
            {
                if (used == bytes.Length)
                {
                    GrowOrSeal();
                }

                bytes[used++] = (byte)codepoint;
                return;
            }

            if (wide == null)
            {
                SwitchToWide();
            }

            if (codepoint <= 0xFFFF)
            {
                wide.Append((char)codepoint);
            }
            else
            {
                wide.Append(char.ConvertFromUtf32(codepoint));
            }

            WideGrown();
        }

        public void WriteRepeatedAscii(byte asciiChar, int count)
        {
            for (int i = 0; i < count; i++)
            {
                WriteCodePoint(asciiChar);
            }
        }

        public void Flush()
        {
        }

        private void AppendString(string s)
        {
            AppendChars(s, 0, s.Length);
        }

        private void AppendChars(string s, int i, int n)
        {
            while (i < n)
            {
                if (used == bytes.Length)
                {
                    GrowOrSeal();
                }

                byte[] b = bytes;
                int u = used;
                int lim = b.Length;
                while (i < n && u < lim)
                {
                    char c = s[i];
                    if (c > 0xFF)
                    {
                        used = u;
                        SwitchToWide();
                        wide.Append(s, i, n - i);
                        WideGrown();
                        return;
                    }

                    b[u++] = (byte)c;
                    i++;
                }

                used = u;
            }
        }

        // The current chunk is full: double it while below the 64KB cap (cheap early growth
        // for small results), otherwise seal it as a rope segment and start a fresh chunk.
        private void GrowOrSeal()
        {
            if (bytes.Length < CHUNK)
            {
                byte[] grown = new byte[Math.Min(bytes.Length * 2, CHUNK)];
                Buffer.BlockCopy(bytes, 0, grown, 0, used);
                bytes = grown;
                return;
            }

            if (sealedChunks == null)
            {
                sealedChunks = new List<UnicodeString>();
            }

            sealedChunks.Add(new Slice8(bytes, 0, bytes.Length));
            sealedLength += bytes.Length;
            bytes = new byte[CHUNK];
            used = 0;
            Enter();
            OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();   // one append of a long string seals thousands of chunks
        }

        // A result grown large is entered in the ledger of the call building it, which then sums it as it grows.
        private bool inLedger;
        private static readonly Func<object, long> Sizer = o => ((UniStringCollector)o).HeldBytes();

        private long HeldBytes()
        {
            return bytes.Length + (wide != null ? 2L * wide.Capacity : sealedLength);
        }

        private void Enter()
        {
            if (!inLedger)
            {
                inLedger = true;
                OutSmart.DAXon.Core.MemoryLedger.Hold(this, Sizer);
            }
        }

        private void WideGrown()
        {
            if (!inLedger && wide.Length >= OutSmart.DAXon.Core.MemoryLedger.BigChars)
            {
                Enter();
            }
        }

        private void SwitchToWide()
        {
            wide = new StringBuilder(sealedLength + used + 64);
            if (sealedChunks != null)
            {
                foreach (UnicodeString chunk in sealedChunks)
                {
                    wide.Append(chunk.ToString());
                }

                sealedChunks = null;
            }

            byte[] b = bytes;
            for (int i = 0; i < used; i++)
            {
                wide.Append((char)b[i]);
            }
        }

        public UnicodeString ToUnicodeString()
        {
            if (wide != null)
            {
                return wide.Length == 0 ? (UnicodeString)EmptyUnicodeString.GetInstance() : StringTool.FromCharSequence(wide.ToString());
            }

            if (sealedChunks == null)
            {
                return used == 0 ? (UnicodeString)EmptyUnicodeString.GetInstance() : new Slice8(bytes, 0, used);
            }

            // Multi-chunk result: one exact-size assembly copy. Unlike the old doubling buffer
            // this allocates the final array once at its true size (no 2x retention, no ladder
            // of discarded half-size buffers), and downstream still sees a flat Width=8 string.
            byte[] all = new byte[sealedLength + used];
            int pos = 0;
            foreach (UnicodeString chunk in sealedChunks)
            {
                Slice8 s = (Slice8)chunk;
                int n = s.End - s.Start;
                Buffer.BlockCopy(s.ByteArray, s.Start, all, pos, n);
                pos += n;
            }

            Buffer.BlockCopy(bytes, 0, all, pos, used);
            return new Twine8(all);
        }
    }
}
