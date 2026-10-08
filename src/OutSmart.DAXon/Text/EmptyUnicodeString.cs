////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Collections;

namespace OutSmart.DAXon.Text
{

    internal sealed class EmptyUnicodeString : UnicodeString
    {
        private static readonly EmptyUnicodeString _instance = new EmptyUnicodeString();
        public override int Width => 8;
        public static EmptyUnicodeString GetInstance() => _instance;
        public override long Length() => 0;
        public override bool IsEmpty() => true;
        public override int CodePointAt(long index) => -1;
        public override UnicodeString Substring(long start, long end) => _instance;
        public override UnicodeString Concat(UnicodeString other) => other ?? (UnicodeString)_instance;
        public override int CompareTo(UnicodeString other) => other == null || other.IsEmpty() ? 0 : -1;
        public override long IndexOf(int codePoint) => -1;
        public override long IndexOf(int codePoint, long from) => -1;
        public override IIntIterator CodePoints() => new StrCodePointIterator(ToString());
        public override long IndexWhere(Func<int, bool> predicate, long from) => -1;
        public override string ToString() => "";
        public override void Copy8bit(byte[] target, int offset) { }
        public override void Copy16bit(char[] target, int offset) { }
        public override void Copy24bit(byte[] target, int offset) { }
        public override void Copy32bit(int[] target, int offset) { }
    }
}
