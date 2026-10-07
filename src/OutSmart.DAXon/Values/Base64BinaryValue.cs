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
    /// A value of type xs:base64Binary
    /// </summary>
    public class Base64BinaryValue : AtomicValue, IAtomicMatchKey, IXPathComparable, IContextFreeAtomicValue
    {

        private static readonly string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        private static readonly int[] encoding = new int[64];
        private static readonly int[] decoding = new int[128];
        private readonly byte[] binaryValue;

        public virtual byte[] BinaryValue => binaryValue;

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.BASE64_BINARY;

        public override UnicodeString PrimitiveStringValue => Encode(binaryValue);

        public virtual int LengthInOctets => binaryValue.Length;

        public IXPathComparable XPathComparable => this;
        static Base64BinaryValue()
        {
            ArrayTools.Fill(decoding, -1);
            for (int i = 0; i < alphabet.Length; i++)
            {
                char c = alphabet[i];
                encoding[i] = c;
                decoding[c] = i;
            }
        }
        public Base64BinaryValue(UnicodeString s) : base(BuiltInAtomicType.BASE64_BINARY)
        {
            binaryValue = Decode(s);
        }

        public Base64BinaryValue(byte[] value) : base(BuiltInAtomicType.BASE64_BINARY)
        {
            binaryValue = value;
        }

        public Base64BinaryValue(byte[] value, IAtomicType typeLabel) : base(typeLabel)
        {
            binaryValue = value;
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new Base64BinaryValue(binaryValue, typeLabel);
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        public override bool Equals(object other)
        {
            return other is Base64BinaryValue && ArrayTools.Equals(binaryValue, ((Base64BinaryValue)other).binaryValue);
        }

        public override int GetHashCode()
        {
            return ByteArrayHashCode(binaryValue);
        }

        public static int ByteArrayHashCode(byte[] value)
        {
            long h = 0;
            for (int i = 0; i < Math.Min(value.Length, 64); i++)
            {
                h = (h << 1) ^ value[i];
            }

            return (int)((h >> 32) ^ h);
        }

        public static UnicodeString Encode(byte[] value)
        {
            UnicodeBuilder buff = new UnicodeBuilder(value.Length * 2);
            int whole = value.Length - value.Length % 3;

            // process bytes 3 at a time: 3 bytes => 4 characters
            for (int i = 0; i < whole; i += 3)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);   // millions of bytes, one call

                // 3 bytes = 24 bits = 4 characters
                int val = ((((int)value[i]) & 0xff) << 16) + ((((int)value[i + 1]) & 0xff) << 8) + ((((int)value[i + 2]) & 0xff));
                buff.Append((char)encoding[(val >> 18) & 0x3f]);
                buff.Append((char)encoding[(val >> 12) & 0x3f]);
                buff.Append((char)encoding[(val >> 6) & 0x3f]);
                buff.Append((char)encoding[val & 0x3f]);
            }

            int remainder = (value.Length % 3);
            switch (remainder)
            {
                case 0:
                default:

                    // no action
                    break;
                case 1:
                    {

                        // pad the final 8 bits to 12 (2 groups of 6)
                        int val = ((((int)value[whole]) & 0xff) << 4);
                        buff.Append((char)encoding[(val >> 6) & 0x3f]);
                        buff.Append((char)encoding[val & 0x3f]);
                        buff.AppendLatin("==");
                        break;
                    }

                case 2:
                    {

                        // pad the final 16 bits to 18 (3 groups of 6)
                        int val = ((((int)value[whole]) & 0xff) << 10) + ((((int)value[whole + 1]) & 0xff) << 2);
                        buff.Append((char)encoding[(val >> 12) & 0x3f]);
                        buff.Append((char)encoding[(val >> 6) & 0x3f]);
                        buff.Append((char)encoding[val & 0x3f]);
                        buff.Append('=');
                        break;
                    }
            }

            return buff.ToUnicodeString();
        }

        public static byte[] Decode(UnicodeString @in)
        {
            @in = @in.Tidy();
            int[] unit = new int[4];
            byte[] result = new byte[@in.Length32()];
            int bytesUsed = 0;
            int i = 0;
            int u = 0;
            int pad = 0;
            int chars = 0;
            char last = (char)0;

            // process characters 4 at a time: 4 characters => 3 bytes
            while (i < @in.Length())
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                int c = @in.CodePointAt(i++);
                if (!Whitespace.IsWhite(c))
                {
                    chars++;
                    if (c == '=')
                    {

                        // all following chars must be '=' or whitespace
                        pad = 1;
                        for (int k = i; k < @in.Length(); k++)
                        {
                            int ch = @in.CodePointAt(k);
                            if (ch == '=')
                            {
                                pad++;
                                chars++;
                            }
                            else if (Whitespace.IsWhite(ch))
                            {
                            }
                            else
                            {
                                throw new XPathException("Base64 padding character '=' is followed by non-padding characters", "FORG0001");
                            }
                        }

                        if (pad == 1 && "AEIMQUYcgkosw048".IndexOf(last) < 0)
                        {
                            throw new XPathException("In base64, if the value ends with a single '=' character, then the preceding character must be" + " one of [AEIMQUYcgkosw048]", "FORG0001");
                        }
                        else if (pad == 2 && "AQgw".IndexOf(last) < 0)
                        {
                            throw new XPathException("In base64, if the value ends with '==', then the preceding character must be" + " one of [AQgw]", "FORG0001");
                        }


                        // number of padding characters must be the number required
                        if (pad > 2)
                        {
                            throw new XPathException("Found " + pad + " '=' characters at end of base64 value; max is 2", "FORG0001");
                        }

                        if (pad != ((4 - u) % 4))
                        {
                            throw new XPathException("Required " + ((4 - u) % 4) + " '=' characters at end of base64 value; found " + pad, "FORG0001");
                        }


                        // append 0 sextets corresponding to number of padding characters
                        for (int p = 0; p < pad; p++)
                        {
                            unit[u++] = 'A';
                        }

                        i = @in.Length32();
                    }
                    else
                    {
                        last = (char)c;
                        unit[u++] = c;
                    }

                    if (u == 4)
                    {
                        int t = (DecodeChar(unit[0]) << 18) + (DecodeChar(unit[1]) << 12) + (DecodeChar(unit[2]) << 6) + (DecodeChar(unit[3]));
                        if (bytesUsed + 3 > result.Length)
                        {
                            byte[] r2 = new byte[bytesUsed * 2];
                            Array.Copy(result, 0, r2, 0, bytesUsed);
                            result = r2;
                        }

                        result[bytesUsed++] = (byte)((t >> 16) & 0xff);
                        result[bytesUsed++] = (byte)((t >> 8) & 0xff);
                        result[bytesUsed++] = (byte)(t & 0xff);
                        u = 0;
                    }
                }

                if (i >= @in.Length())
                {
                    bytesUsed -= pad;
                    break;
                }
            }

            if (chars % 4 != 0)
            {
                throw new XPathException("Length of base64 value must be a multiple of four", "FORG0001");
            }

            byte[] r3 = new byte[bytesUsed];
            Array.Copy(result, 0, r3, 0, bytesUsed);
            return r3;
        }

        private static int DecodeChar(int c)
        {
            int d = c < 128 ? decoding[c] : -1;
            if (d == -1)
            {
                throw new XPathException("Invalid character '" + c + "' in base64 value", "FORG0001");
            }

            return d;
        }

        public int CompareTo(IXPathComparable o)
        {
            if (o is HexBinaryValue)
            {
                o = new Base64BinaryValue(((HexBinaryValue)o).BinaryValue);
            }

            if (o is Base64BinaryValue)
            {
                byte[] other = ((Base64BinaryValue)o).binaryValue;
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
                throw new InvalidCastException("Cannot compare xs:base64Binary to " + o.GetType());
            }
        }
    }
}