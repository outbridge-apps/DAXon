////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
namespace OutSmart.DAXon.Text
{
    internal abstract class WhitespaceString : UnicodeString
    {
        public override int Width => 7;
        public abstract UnicodeString Uncompress();

        public override UnicodeString Substring(long start, long end)
        {
            return Uncompress().Substring(start, end);
        }

        public override long IndexOf(int codePoint, long from)
        {

            // Faster implementations are possible, but not needed
            return Uncompress().IndexOf(codePoint, from);
        }

        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            return Uncompress().IndexWhere(predicate, from);
        }

        public override string ToString()
        {
            return Uncompress().ToString();
        }

        public abstract void Write(IUnicodeWriter writer);
        public override void Copy8bit(byte[] target, int offset)
        {
            Uncompress().Copy8bit(target, offset);
        }

        public override void Copy16bit(char[] target, int offset)
        {
            Uncompress().Copy16bit(target, offset);
        }

        public override void Copy24bit(byte[] target, int offset)
        {
            Uncompress().Copy24bit(target, offset);
        }

        public override void Copy32bit(int[] target, int offset)
        {
            Uncompress().Copy32bit(target, offset);
        }

        public abstract void WriteEscape(bool[] specialChars, IUnicodeWriter writer);
    }
}
