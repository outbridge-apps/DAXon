////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
namespace OutSmart.DAXon.Text
{
    public interface IUnicodeWriter : IDisposable
    {
        void Write(UnicodeString chars);
        void WriteAscii(byte[] content);
        void WriteCodePoint(int codepoint);



        void WriteRepeatedAscii(byte asciiChar, int count);





        void Write(string chars);
        void Flush();

    }
}
