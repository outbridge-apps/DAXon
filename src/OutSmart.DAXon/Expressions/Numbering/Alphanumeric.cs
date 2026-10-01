////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Numbering
{
    internal sealed class Alphanumeric
    {
        private static readonly int[] zeroDigits = new[]
        {
            0x0030,
            0x0660,
            0x06f0,
            0x0966,
            0x09e6,
            0x0a66,
            0x0ae6,
            0x0b66,
            0x0be6,
            0x0c66,
            0x0ce6,
            0x0d66,
            0x0e50,
            0x0ed0,
            0x0f20,
            0x1040,
            0x17e0,
            0x1810,
            0x1946,
            0x19d0,
            0xff10,
            0x104a0,
            0x107ce,
            0x107d8,
            0x107e2,
            0x107ec,
            0x107f6
        };

        public static int GetDigitValue(int @in)
        {
            foreach (int zeroDigit in zeroDigits)
            {
                if (@in <= zeroDigit + 9)
                {
                    if (@in >= zeroDigit)
                    {
                        return @in - zeroDigit;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }

            return -1;
        }

        public static int GetDigitFamily(int @in)
        {
            foreach (int zeroDigit in zeroDigits)
            {
                if (@in <= zeroDigit + 9)
                {
                    if (@in >= zeroDigit)
                    {
                        return zeroDigit;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }

            return -1;
        }
    }
}