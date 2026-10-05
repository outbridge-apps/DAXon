////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.Numerics;
namespace OutSmart.DAXon.Api
{
    public class XdmAtomicValue : XdmItem
    {

        public AtomicValue UnderlyingValue => (AtomicValue)base.UnderlyingValue;

        public virtual QName PrimitiveTypeName
        {
            get
            {
                AtomicValue value = UnderlyingValue;
                BuiltInAtomicType type = value.PrimitiveType;
                return new QName(type.GetStructuredQName());
            }
        }

        public virtual QName TypeName
        {
            get
            {
                AtomicValue value = UnderlyingValue;
                IAtomicType type = value.GetItemType();
                return new QName(type.GetStructuredQName());
            }
        }

        public virtual object Value
        {
            get
            {
                AtomicValue av = UnderlyingValue;
                if (av is StringValue)
                {
                    return av.UnicodeStringValue;
                }
                else if (av is IntegerValue)
                {
                    return ((IntegerValue)av).AsBigInteger();
                }
                else if (av is DoubleValue)
                {
                    return ((DoubleValue)av).GetDoubleValue();
                }
                else if (av is FloatValue)
                {
                    return ((FloatValue)av).GetFloatValue();
                }
                else if (av is BooleanValue)
                {
                    return ((BooleanValue)av).GetBooleanValue();
                }
                else if (av is DecimalValue)
                {
                    return ((DecimalValue)av).GetDecimalValue();
                }
                else if (av is QNameValue)
                {
                    return new QName(((QNameValue)av).GetStructuredQName());
                }
                else
                {
                    return av.UnicodeStringValue;
                }
            }
        }

        public virtual long LongValue
        {
            get
            {
                AtomicValue av = UnderlyingValue;
                if (av is BooleanValue)
                {
                    // true is 1 here and in the double and decimal forms, as in a cast; s9api has the two swapped
                    return ((BooleanValue)av).GetBooleanValue() ? 1 : 0;
                }
                else if (av is NumericValue)
                {
                    try
                    {
                        return ((NumericValue)av).LongValue();
                    }
                    catch (XPathException e)
                    {
                        throw new DAXonApiException("Cannot cast item to an integer");
                    }
                }
                else if (av is StringValue)
                {
                    StringToDouble converter = StringToDouble.GetInstance();
                    return JavaMath.ToLong(converter.StringToNumber(av.UnicodeStringValue.Tidy()));
                }
                else
                {
                    throw new DAXonApiException("Cannot cast item to an integer");
                }
            }
        }
        public XdmAtomicValue(AtomicValue value) : base(value)
        {
        }

        public XdmAtomicValue(bool value) : this(BooleanValue.Get(value))
        {
        }

        public XdmAtomicValue(long value) : this(Int64Value.MakeDerived(value, BuiltInAtomicType.LONG))
        {
        }

        public XdmAtomicValue(int value) : this(Int64Value.MakeDerived(value, BuiltInAtomicType.INT))
        {
        }

        public XdmAtomicValue(short value) : this(Int64Value.MakeDerived(value, BuiltInAtomicType.SHORT))
        {
        }

        public XdmAtomicValue(byte value) : this(Int64Value.MakeDerived(value, BuiltInAtomicType.BYTE))
        {
        }

        public XdmAtomicValue(BigDecimal value) : this(new BigDecimalValue(value))
        {
        }

        public XdmAtomicValue(double value) : this(new DoubleValue(value))
        {
        }

        public XdmAtomicValue(float value) : this(new FloatValue(value))
        {
        }

        public XdmAtomicValue(string value) : this(new StringValue(value))
        {
        }

        public XdmAtomicValue(URI value) : this(new AnyURIValue((value.ToString())))
        {
        }

        public XdmAtomicValue(QName value) : this(new QNameValue(value.GetStructuredQName(), BuiltInAtomicType.QNAME))
        {
        }


        public XdmAtomicValue(string lexicalForm, ItemType type) : base(FromLexicalForm(lexicalForm, type))
        {
        }

        private static AtomicValue FromLexicalForm(string lexicalForm, ItemType type)
        {
            Types.ItemType it = type.UnderlyingItemType;
            if (!it.IsPlainType())
            {
                throw new DAXonApiException("Requested type is not atomic");
            }

            if (((IAtomicType)it).IsAbstract())
            {
                throw new DAXonApiException("Requested type is an abstract type");
            }

            if (((IAtomicType)it).IsNamespaceSensitive())
            {
                throw new DAXonApiException("Requested type is namespace-sensitive");
            }

            try
            {
                StringConverter converter = ((IAtomicType)it).GetStringConverter(type.GetConversionRules());
                return converter.ConvertString(StringView.Of(StringTool.WithoutHalfPairs(lexicalForm)).Tidy()).AsAtomic();
            }
            catch (ValidationException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public static XdmAtomicValue MakeAtomicValue(object value)
        {
            if (value is AtomicValue)
            {
                return new XdmAtomicValue((AtomicValue)value);
            }
            else if (value is bool)
            {
                return new XdmAtomicValue((bool)value);
            }
            else if (value is int)
            {
                return new XdmAtomicValue((int)value);
            }
            else if (value is long)
            {
                return new XdmAtomicValue((long)value);
            }
            else if (value is short)
            {
                return new XdmAtomicValue((short)value);
            }
            else if (value is char)
            {
                return new XdmAtomicValue(((char)value).ToString());
            }
            else if (value is byte)
            {
                return new XdmAtomicValue((byte)value);
            }
            else if (value is string)
            {
                return new XdmAtomicValue((string)value);
            }
            else if (value is double)
            {
                return new XdmAtomicValue((double)value);
            }
            else if (value is float)
            {
                return new XdmAtomicValue((float)value);
            }
            else if (value is BigDecimal)
            {
                return new XdmAtomicValue((BigDecimal)value);
            }
            else if (value is BigInteger)
            {
                return new XdmAtomicValue(IntegerValue.MakeIntegerValue((BigInteger)value));
            }
            else if (value is URI)
            {
                return new XdmAtomicValue((URI)value);
            }
            else if (value is QName)
            {
                return new XdmAtomicValue((QName)value);
            }
            else if (value is XdmAtomicValue)
            {
                return (XdmAtomicValue)value;
            }
            else
            {
                throw new ArgumentException(value.ToString());
            }
        }

        public override string ToString()
        {
            return GetStringValue();
        }

        public virtual bool GetBooleanValue()
        {
            AtomicValue av = UnderlyingValue;
            if (av is BooleanValue)
            {
                return ((BooleanValue)av).GetBooleanValue();
            }
            else if (av is NumericValue)
            {
                return !av.IsNaN() && ((NumericValue)av).Signum() != 0;
            }
            else if (av is StringValue)
            {
                string s = Whitespace.Trim(av.UnicodeStringValue.Tidy()).ToString();
                return s == "1" || s == "true";
            }
            else
            {
                throw new DAXonApiException("Cannot cast item to a boolean");
            }
        }

        public virtual double GetDoubleValue()
        {
            AtomicValue av = UnderlyingValue;
            if (av is BooleanValue)
            {
                return ((BooleanValue)av).GetBooleanValue() ? 1 : 0;
            }
            else if (av is NumericValue)
            {
                return ((NumericValue)av).GetDoubleValue();
            }
            else if (av is StringValue)
            {
                try
                {
                    StringToDouble converter = StringToDouble11.GetInstance();
                    return converter.StringToNumber(av.UnicodeStringValue.Tidy());
                }
                catch (FormatException e)
                {
                    throw new DAXonApiException(e.Message);
                }
            }
            else
            {
                throw new DAXonApiException("Cannot cast item to a double");
            }
        }

        public virtual BigDecimal GetDecimalValue()
        {
            AtomicValue av = UnderlyingValue;
            if (av is BooleanValue)
            {
                return ((BooleanValue)av).GetBooleanValue() ? BigDecimal.One : BigDecimal.Zero;
            }
            else if (av is NumericValue)
            {
                try
                {
                    return ((NumericValue)av).GetDecimalValue();
                }
                catch (XPathException e)
                {
                    throw new DAXonApiException("Cannot cast item to a decimal");
                }
            }
            else if (av is StringValue)
            {
                return new BigDecimal(av.GetStringValue());
            }
            else
            {
                throw new DAXonApiException("Cannot cast item to a decimal");
            }
        }

        public virtual QName GetQNameValue()
        {
            AtomicValue av = UnderlyingValue;
            if (av is QualifiedNameValue)
            {
                return new QName(((QualifiedNameValue)av).GetStructuredQName());
            }
            else
            {
                return null;
            }
        }


        public override bool Equals(object other)
        {
            if (other is XdmAtomicValue)
            {
                IAtomicMatchKey a = UnderlyingValue.AsMapKey();
                IAtomicMatchKey b = ((XdmAtomicValue)other).UnderlyingValue.AsMapKey();
                return a.Equals(b);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return UnderlyingValue.AsMapKey().GetHashCode();
        }
    }
}