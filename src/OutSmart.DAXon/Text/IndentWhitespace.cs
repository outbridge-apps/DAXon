////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Text
{
    internal sealed class IndentWhitespace : WhitespaceString
    {
        private readonly int newlines;
        private readonly int spaces;
        private IndentWhitespace(int newlines, int spaces)
        {
            this.newlines = newlines;
            this.spaces = spaces;
        }

        public static IndentWhitespace Of(int newlines, int spaces)
        {

            // Uses a factory method to permit pooling, not currently implemented
            return new IndentWhitespace(newlines, spaces);
        }

        public override UnicodeString Uncompress()
        {
            byte[] bytes = new byte[newlines + spaces];
            ArrayTools.Fill(bytes, 0, newlines, (byte)0x0a);
            ArrayTools.Fill(bytes, newlines, newlines + spaces, (byte)0x20);
            return new Twine8(bytes);
        }

        public override long Length()
        {
            return newlines + spaces;
        }

        public override int Length32()
        {
            return newlines + spaces;
        }

        public override int CodePointAt(long index)
        {
            if (index >= 0 && index < newlines)
            {
                return 0x0A;
            }
            else if (index <= newlines + spaces)
            {
                return 0x20;
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        public override IIntIterator CodePoints()
        {
            return new ConcatenatingIntIterator(new IntRepeatIterator(10, newlines), () => new IntRepeatIterator(32, spaces));
        }

        public override string ToString()
        {
            char[] chars = new char[newlines + spaces];
            ArrayTools.Fill(chars, 0, newlines, '\n');
            ArrayTools.Fill(chars, newlines, newlines + spaces, ' ');
            return new string(chars);
        }

        public override void Write(IUnicodeWriter writer)
        {
            if (newlines > 0)
            {
                writer.WriteRepeatedAscii((byte)0x0A, newlines);
            }

            if (spaces > 0)
            {
                writer.WriteRepeatedAscii((byte)0x20, spaces);
            }
        }

        public override void WriteEscape(bool[] specialChars, IUnicodeWriter writer)
        {
            if (specialChars[0x0A])
            {
                for (int i = 0; i < newlines; i++)
                {
                    writer.WriteAscii(StringConstants.ESCAPE_NL);
                }
            }
            else
            {
                writer.WriteRepeatedAscii((byte)0x0A, newlines);
            }

            writer.WriteRepeatedAscii((byte)0x20, spaces);
        }
    }
}