////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using System;
using OutSmart.DAXon.Text;
namespace OutSmart.DAXon.Values
{
    public class StringValue : AtomicValue
    {
        public static readonly StringValue EMPTY_STRING = new StringValue(EmptyUnicodeString.GetInstance());
        public static readonly StringValue SINGLE_SPACE = new StringValue(StringConstants.SINGLE_SPACE);
        public static readonly StringValue TRUE = new StringValue(StringConstants.TRUE);
        public static readonly StringValue FALSE = new StringValue(StringConstants.FALSE);
        public static readonly StringValue ZERO_LENGTH_UNTYPED = StringValue.MakeUntypedAtomic(EmptyUnicodeString.GetInstance());
        protected readonly UnicodeString content;

        public override BuiltInAtomicType PrimitiveType => typeLabel == BuiltInAtomicType.UNTYPED_ATOMIC ? BuiltInAtomicType.UNTYPED_ATOMIC : BuiltInAtomicType.STRING;

        public override UnicodeString PrimitiveStringValue => content;

        public virtual UnicodeString Content => content;

        public virtual Base64BinaryValue CodepointCollationKey
        {
            get
            {
                int len = Content.Length32();
                byte[] result = new byte[len * 3];
                for (int i = 0, j = 0; i < len; i++)
                {
                    int c = Content.CodePointAt(i);
                    result[j++] = (byte)(c >> 16);
                    result[j++] = (byte)(c >> 8);
                    result[j++] = (byte)c;
                }

                return new Base64BinaryValue(result);
            }
        }

        public override UnicodeString UnicodeStringValue => content;
        protected StringValue() : base(BuiltInAtomicType.STRING)
        {
            content = EmptyUnicodeString.GetInstance();
        }

        protected StringValue(IAtomicType typeLabel) : base(typeLabel)
        {
            content = EmptyUnicodeString.GetInstance();
        }

        public StringValue(UnicodeString content) : this(content, BuiltInAtomicType.STRING)
        {
        }

        public StringValue(UnicodeString content, IAtomicType type) : base(type)
        {
            this.content = content;
        }

        public StringValue(string value) : this(value, BuiltInAtomicType.STRING)
        {
        }

        public StringValue(string value, IAtomicType typeLabel) : base(typeLabel)
        {
            this.content = StringTool.FromCharSequence(value);
        }

        public static StringValue MakeUntypedAtomic(UnicodeString value)
        {
            return new StringValue(value, BuiltInAtomicType.UNTYPED_ATOMIC);
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            if (typeLabel == this.typeLabel)
            {
                return this;
            }
            else
            {
                return new StringValue(this.content, typeLabel);
            }
        }

        public static StringValue Bmp(string content)
        {

            //TODO: most if not all calls supply a literal String. Use a static constant pool.
            return new StringValue(BMPString.Of(content));
        }

        public static StringValue MakeStringValue(string value)
        {
            if (value == null || value.Length == 0)
            {
                return StringValue.EMPTY_STRING;
            }
            else
            {
                return new StringValue(value.ToString());
            }
        }

        public virtual StringValue Economize()
        {
            UnicodeString c2 = content.Economize();
            if (c2 == content)
            {
                return this;
            }

            return new StringValue(c2, typeLabel);
        }

        public static StringValue MakeUStringValue(UnicodeString value)
        {
            if (value == null || value.IsEmpty())
            {
                return StringValue.EMPTY_STRING;
            }
            else
            {
                return new StringValue(value);
            }
        }

        public virtual long Length()
        {
            return content.Length();
        }

        public virtual int Length32()
        {
            return content.Length32();
        }

        public virtual bool IsEmpty()
        {
            return content.IsEmpty();
        }

        public virtual IAtomicIterator IterateCharacters()
        {
            lock (this)
            {
                return new CodepointIterator(CodePoints());
            }
        }

        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            return collator.GetCollationKey(this.UnicodeStringValue);
        }

        public virtual IIntIterator CodePoints()
        {
            return content.CodePoints();
        }

        public override int GetHashCode()
        {

            // Same algorithm as String#hashCode(), but not cached; and truncated after 100 characters
            int h = 0;
            int count = 0;
            IIntIterator iter = CodePoints();
            while (iter.MoveNext())
            {
                h = 31 * h + iter.Current;
                if (++count >= 100)
                {
                    break;
                }
            }

            return h;
        }

        public override bool Equals(object o)
        {
            if (o is StringValue)
            {
                return content.Equals(((StringValue)o).content);
            }
            else
            {
                return false;
            }
        }

        public override bool EffectiveBooleanValue()
        {
            return !IsEmpty();
        }

        public override string ToString()
        {
            return Content.ToString();
        }

        public override string ToShortString()
        {
            string s = content.ToString();
            if (s.Length > 40)
            {
                s = s.Substring(0, 20) + " ... " + s.Substring(s.Length - 20);
            }

            s = "\"" + s + '"';
            if (typeLabel == BuiltInAtomicType.UNTYPED_ATOMIC)
            {
                s = "u" + s;
            }

            return s;
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return new AnonymousXPathComparable(this, collator);
        }

        public override bool IsIdentical(AtomicValue v)
        {
            return v is StringValue && (this is AnyURIValue == v is AnyURIValue) && (this.IsUntypedAtomic() == v.IsUntypedAtomic()) && Equals(v);
        }

        private sealed class AnonymousXPathComparable : IXPathComparable
        {

            private readonly StringValue parent;
            private readonly IStringCollator collator;
            public AnonymousXPathComparable(StringValue parent, IStringCollator collator)
            {
                this.parent = parent; this.collator = collator;
            }
            public int CompareTo(IXPathComparable o)
            {
                if (o is StringValue)
                {
                    return collator.CompareStrings(parent.Content, ((StringValue)o).content);
                }
                else
                {
                    throw new InvalidCastException("Cannot compare xs:string to " + o.ToString());
                }
            }
        }
    }
}

