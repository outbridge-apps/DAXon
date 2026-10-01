////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// A value of type xs:hexBinary
    /// </summary>
    internal sealed class HexBinaryValue : AtomicValue, IAtomicMatchKey, IXPathComparable, IContextFreeAtomicValue
    {
        private readonly byte[] binaryValue;

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.HEX_BINARY;

        public byte[] BinaryValue => binaryValue;

        public override UnicodeString PrimitiveStringValue
        {
            get
            {
                string digits = "0123456789ABCDEF";
                UnicodeBuilder sb = new UnicodeBuilder(binaryValue.Length * 2);
                foreach (byte aBinaryValue in binaryValue)
                {
                    sb.Append(digits[(aBinaryValue >> 4) & 0xf]);
                    sb.Append(digits[aBinaryValue & 0xf]);
                }

                return sb.ToUnicodeString();
            }
        }

        public IXPathComparable XPathComparable => this;
        public HexBinaryValue(UnicodeString @in) : base(BuiltInAtomicType.HEX_BINARY)
        {
            UnicodeString s = Whitespace.Trim(@in);
            int len32 = s.Length32();
            if ((len32 & 1) != 0)
            {
                throw new XPathException("A hexBinary value must contain an even number of characters", "FORG0001");
            }

            binaryValue = new byte[len32 / 2];
            for (int i = 0; i < binaryValue.Length; i++)
            {
                binaryValue[i] = (byte)((FromHex(s.CodePointAt(2 * i)) << 4) + FromHex(s.CodePointAt(2 * i + 1)));
            }
        }

        public HexBinaryValue(byte[] value) : base(BuiltInAtomicType.HEX_BINARY)
        {
            binaryValue = value;
        }

        public HexBinaryValue(byte[] value, IAtomicType typeLabel) : base(typeLabel)
        {
            binaryValue = value;
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new HexBinaryValue(binaryValue, typeLabel);
        }

        private int FromHex(int c)
        {
            int d = c < 255 ? "0123456789ABCDEFabcdef".IndexOf((char)c) : -1;
            if (d > 15)
            {
                d = d - 6;
            }

            if (d < 0)
            {
                throw new XPathException("Invalid hexadecimal digit '" + c + "'", "FORG0001");
            }

            return d;
        }

        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        public override bool Equals(object other)
        {
            return other is HexBinaryValue && ArrayTools.Equals(binaryValue, ((HexBinaryValue)other).binaryValue);
        }

        public override int GetHashCode()
        {
            return Base64BinaryValue.ByteArrayHashCode(binaryValue);
        }

        public int CompareTo(IXPathComparable o)
        {
            if (o is Base64BinaryValue)
            {
                o = new HexBinaryValue(((Base64BinaryValue)o).BinaryValue);
            }

            if (o is HexBinaryValue)
            {
                byte[] other = ((HexBinaryValue)o).binaryValue;
                int len0 = binaryValue.Length;
                int len1 = other.Length;
                int shorter = Math.Min(len0, len1);
                for (int i = 0; i < shorter; i++)
                {
                    int a = (int)binaryValue[i] & 0xff;
                    int b = (int)other[i] & 0xff;
                    if (a != b)
                    {
                        return a < b ? -1 : +1;
                    }
                }

                return Math.Sign(len0 - len1);
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:hexBinary to " + o.GetType());
            }
        }
    }
}