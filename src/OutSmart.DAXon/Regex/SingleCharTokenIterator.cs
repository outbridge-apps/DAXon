////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// Tokenizer for the (very common) case where the compiled regex matches exactly one literal
    /// codepoint: splitting is a plain IndexOf scan, bypassing the regex engine entirely. The
    /// token sequence is identical to ATokenIterator's, including leading/trailing/adjacent
    /// separators producing zero-length tokens.
    /// </summary>
    internal sealed class SingleCharTokenIterator : IAtomicIterator
    {
        private readonly UnicodeString input;
        private readonly int separator;
        private int prevEnd;   // -1 after the last token has been delivered
        private int tokens;    // every 1024 tokens it looks at the limits, which count() pulling millions never does

        public SingleCharTokenIterator(UnicodeString input, int separator)
        {
            this.input = input;
            this.separator = separator;
            prevEnd = 0;
        }

        public StringValue Next()
        {
            if (prevEnd < 0)
            {
                return null;
            }

            Tick();
            long sep = input.IndexOf(separator, prevEnd);
            StringValue current;
            if (sep >= 0)
            {
                current = new StringValue(Token(prevEnd, sep));
                prevEnd = (int)sep + 1;
            }
            else
            {
                current = new StringValue(Token(prevEnd, input.Length()));
                prevEnd = -1;
            }

            return current;
        }

        // BMP input (surrogate-free by classification): a token is a zero-copy view of the line,
        // not a copied substring -- the copies dominate GC when millions of tokens are retained.
        private UnicodeString Token(int from, long to)
        {
            if (input is BMPString b)
            {
                string s = b.ToString();
                return from == 0 && to == s.Length ? input : new BMPSlice(s, from, (int)to);
            }

            return input.Substring(from, to);
        }

        private void Tick()
        {
            if ((++tokens & 1023) == 0)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();
            }
        }

        AtomicValue IAtomicIterator.Next() => Next();
        IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
        public void Dispose() { }

        /// <summary>
        /// Bulk-materialize the remaining tokens (used by array{tokenize(...)}): separators are
        /// pre-counted so the list is allocated at exact size, and tokens are sliced in a tight
        /// loop -- no growth ladder and no per-token iterator dispatch. Token-for-token identical
        /// to draining via Next().
        /// </summary>
        internal System.Collections.Generic.List<IGroundedValue> DrainRemaining()
        {
            var list = new System.Collections.Generic.List<IGroundedValue>();
            if (prevEnd < 0)
            {
                return list;
            }

            int count = 1;
            for (long p = input.IndexOf(separator, prevEnd); p >= 0; p = input.IndexOf(separator, (int)p + 1))
            {
                count++;
                Tick();
            }

            list.Capacity = count;
            while (prevEnd >= 0)
            {
                Tick();
                long sep = input.IndexOf(separator, prevEnd);
                if (sep >= 0)
                {
                    list.Add(new StringValue(Token(prevEnd, sep)));
                    prevEnd = (int)sep + 1;
                }
                else
                {
                    list.Add(new StringValue(Token(prevEnd, input.Length())));
                    prevEnd = -1;
                }
            }

            return list;
        }
    }
}
