////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using System.IO;

namespace OutSmart.DAXon.Serialization
{
    // 2026-06-03: functional stub (the real OutSmart.DAXon.Text.UnicodeWriterToWriter stays excluded to avoid a
    // Phase-7/transpile re-include cascade). Was a hollow stub NOT implementing IUnicodeWriter -> serialize callers
    // (ExpandedStreamResult.ObtainUnicodeWriter et al., which `using OutSmart.DAXon.Text` but resolve this same-namespace
    // class) cast it to IUnicodeWriter -> InvalidCast. Now it really implements IUnicodeWriter, wrapping the Writer.
    internal sealed class UnicodeWriterToWriter : IUnicodeWriter
    {
        // IO-removal W2: compat Writer base eliminated -> wraps a BCL System.IO.TextWriter directly.
        private readonly TextWriter _w;
        // Bounded-chunk write path: 32K codepoints per slice keeps the reused char[] and any
        // fallback strings sub-LOH. Without this, one large text value (e.g. a whole xml-to-json
        // result) would materialize as a single multi-hundred-MB string via ToString().
        private const int WRITE_CHUNK = 1 << 15;
        private char[] writeBuf;
        public UnicodeWriterToWriter(TextWriter writer) { _w = writer; }

        // A writer whose bytes begin with a byte order mark of their own when they are to have one - the engine's over
        // UTF-16 or UTF-32, a StreamWriter of the host's whose encoding has a preamble: the U+FEFF that
        // byte-order-mark="yes" asks an emitter for would be a second mark, and the document would not parse.
        public UnicodeWriterToWriter(TextWriter writer, bool marksItself)
        {
            _w = writer;
            MarksItself = marksItself;
        }

        internal bool MarksItself { get; }
        public void Write(UnicodeString chars)
        {
            // Latin1 byte reps: widen straight into the reused buffer - the ToString() below
            // allocated a string (plus its 8->16 copy) per text-node write.
            byte[] b8 = null;
            int off8 = 0;
            if (chars is Slice8 sl8)
            {
                b8 = sl8.ByteArray;
                off8 = sl8.Start;
            }
            else if (chars is Twine8 tw8)
            {
                b8 = tw8.ByteArray;
            }

            if (b8 != null)
            {
                int len8 = chars.Length32();
                char[] buf8 = writeBuf ?? (writeBuf = new char[WRITE_CHUNK]);
                for (int i = 0; i < len8; i += WRITE_CHUNK)
                {
                    int n = Math.Min(WRITE_CHUNK, len8 - i);
                    for (int k = 0; k < n; k++)
                    {
                        buf8[k] = (char)(b8[off8 + i + k] & 0xff);
                    }

                    _w.Write(buf8, 0, n);
                }

                return;
            }

            long len = chars.Length();
            if (len <= WRITE_CHUNK)
            {
                _w.Write(chars.ToString());
                return;
            }

            if (chars.Width <= 16)
            {
                // No astral codepoints: codepoints map 1:1 to chars; copy each slice into a
                // reused buffer and hand it to the writer's char[] overload -- zero big strings.
                char[] buf = writeBuf ?? (writeBuf = new char[WRITE_CHUNK]);
                for (long i = 0; i < len; i += WRITE_CHUNK)
                {
                    long end = Math.Min(i + WRITE_CHUNK, len);
                    chars.Substring(i, end).Copy16bit(buf, 0);
                    _w.Write(buf, 0, (int)(end - i));
                }
            }
            else
            {
                // Astral content: surrogate pairs make chars != codepoints -- write bounded
                // string slices instead. Half-step: a full 32K-codepoint slice can reach 64K
                // UTF-16 units (~128KB string, just over the LOH threshold); 16K stays under.
                const int astralChunk = WRITE_CHUNK / 2;
                for (long i = 0; i < len; i += astralChunk)
                {
                    _w.Write(chars.Substring(i, Math.Min(i + astralChunk, len)).ToString());
                }
            }
        }
        public void Write(string chars) { _w.Write(chars); }
        // Hot path (element/attribute names, XML punctuation): widen bytes -> char[] once and hand it to the
        // Writer's char[] overload, instead of allocating BOTH a char[] (ConvertAll) AND a string (new string)
        // per call only to have Writer.Write(string) re-copy it. Byte-identical, ~2 fewer allocations per write.
        public void WriteAscii(byte[] content) { var chars = new char[content.Length]; for (int i = 0; i < content.Length; i++) chars[i] = (char)content[i]; _w.Write(chars, 0, content.Length); }
        public void WriteRepeatedAscii(byte asciiChar, int count) { _w.Write(new string((char)asciiChar, count)); }
        // BMP fast path: write the single char with no per-codepoint string allocation (ConvertFromUtf32 only for astral).
        // IO-removal W2: write the BMP char via Write(char) — compat Writer.Write(int) had char semantics,
        // but BCL TextWriter.Write(int) writes the integer as text. UTF8Writer overrides Write(char) to keep
        // char semantics; for the non-UTF8 StreamWriter sink, Write(char) is the correct codepoint write.
        public void WriteCodePoint(int codepoint) { if (codepoint <= 0xFFFF) _w.Write((char)codepoint); else _w.Write(char.ConvertFromUtf32(codepoint)); }
        public void Dispose() { _w.Dispose(); }
        public void Flush() { _w.Flush(); }
    }
}
