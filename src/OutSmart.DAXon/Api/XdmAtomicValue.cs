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
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Net;
using System;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
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
                    catch (XPathException)
                    {
                        throw new DAXonApiException("Cannot cast item to an integer");
                    }
                }
                else if (av is StringValue)
                {
                    try
                    {
                        StringToDouble converter = StringToDouble.GetInstance();
                        return JavaMath.ToLong(converter.StringToNumber(av.UnicodeStringValue));
                    }
                    catch (FormatException e)
                    {
                        throw new DAXonApiException(e.Message);
                    }
                }
                else
                {
                    throw new DAXonApiException("Cannot cast item to an integer");
                }
            }
        }
        public XdmAtomicValue(AtomicValue value) : base(ArgumentCheck.Checked(value, nameof(value)))
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

        public XdmAtomicValue(BigDecimal value) : this(new BigDecimalValue(ArgumentCheck.Checked(value, nameof(value))))
        {
        }

        public XdmAtomicValue(double value) : this(new DoubleValue(value))
        {
        }

        public XdmAtomicValue(float value) : this(new FloatValue(value))
        {
        }

        public XdmAtomicValue(string value) : this(new StringValue(ArgumentCheck.Checked(value, nameof(value))))
        {
        }

        public XdmAtomicValue(URI value) : this(new AnyURIValue(ArgumentCheck.Checked(value, nameof(value)).ToString()))
        {
        }

        public XdmAtomicValue(QName value) : this(new QNameValue(ArgumentCheck.Checked(value, nameof(value)).GetStructuredQName(), BuiltInAtomicType.QNAME))
        {
        }


        /// <summary>An xs:unsignedLong of this value. Without it a ulong would fit float and decimal alike: no constructor to pick.</summary>
        public XdmAtomicValue(ulong value) : base(FromLexicalForm(value.ToString(System.Globalization.CultureInfo.InvariantCulture), ItemType.UNSIGNED_LONG))
        {
        }

        /// <summary>An xs:decimal of this value.</summary>
        public XdmAtomicValue(decimal value) : base(FromLexicalForm(value.ToString(System.Globalization.CultureInfo.InvariantCulture), ItemType.DECIMAL))
        {
        }

        /// <summary>
        /// An xs:dateTime. A value of kind Utc has the time zone Z, one of kind Local the offset of the machine at that
        /// time, one of kind Unspecified none.
        /// </summary>
        public XdmAtomicValue(DateTime value) : base(FromLexicalForm(value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", System.Globalization.CultureInfo.InvariantCulture), ItemType.DATE_TIME))
        {
        }

        /// <summary>An xs:dateTime with the offset of this value.</summary>
        public XdmAtomicValue(DateTimeOffset value) : base(FromLexicalForm(value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", System.Globalization.CultureInfo.InvariantCulture), ItemType.DATE_TIME))
        {
        }

        /// <summary>An xs:dayTimeDuration of this length.</summary>
        public XdmAtomicValue(TimeSpan value) : base(FromLexicalForm(System.Xml.XmlConvert.ToString(value), ItemType.DAY_TIME_DURATION))
        {
        }

        /// <summary>An xs:anyURI of this text.</summary>
        public XdmAtomicValue(Uri value) : this(new AnyURIValue(ArgumentCheck.Checked(value, nameof(value)).OriginalString))
        {
        }

        public XdmAtomicValue(string lexicalForm, ItemType type) : base(FromLexicalForm(lexicalForm, type))
        {
        }

        private static AtomicValue FromLexicalForm(string lexicalForm, ItemType type)
        {
            ArgumentCheck.NotNull(lexicalForm, nameof(lexicalForm));
            ArgumentCheck.NotNull(type, nameof(type));
            Types.ItemType it = type.UnderlyingItemType;
            // Plain but not atomic: the union xs:numeric (ItemType.NUMERIC).
            if (!it.IsPlainType() || !(it is IAtomicType))
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
                return converter.ConvertString(StringView.Of(StringTool.WithoutHalfPairs(lexicalForm))).AsAtomic();
            }
            catch (ValidationException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public static XdmAtomicValue MakeAtomicValue(object value)
        {
            ArgumentCheck.NotNull(value, nameof(value));
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
            else if (value is decimal)
            {
                return new XdmAtomicValue((decimal)value);
            }
            else if (value is sbyte)
            {
                return new XdmAtomicValue(Int64Value.MakeDerived((sbyte)value, BuiltInAtomicType.BYTE));
            }
            else if (value is ushort)
            {
                return new XdmAtomicValue(Int64Value.MakeDerived((ushort)value, BuiltInAtomicType.UNSIGNED_SHORT));
            }
            else if (value is uint)
            {
                return new XdmAtomicValue(Int64Value.MakeDerived((uint)value, BuiltInAtomicType.UNSIGNED_INT));
            }
            else if (value is ulong)
            {
                return new XdmAtomicValue(((ulong)value).ToString(System.Globalization.CultureInfo.InvariantCulture), ItemType.UNSIGNED_LONG);
            }
            else if (value is DateTime)
            {
                return new XdmAtomicValue((DateTime)value);
            }
            else if (value is DateTimeOffset)
            {
                return new XdmAtomicValue((DateTimeOffset)value);
            }
            else if (value is TimeSpan)
            {
                return new XdmAtomicValue((TimeSpan)value);
            }
            else if (value is Uri)
            {
                return new XdmAtomicValue((Uri)value);
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

        /// <summary>
        /// The fields of an xs:dateTime or xs:date as they stand, of kind Unspecified: a time zone it has is left out. A year
        /// DateTime does not reach is a DAXonApiException.
        /// </summary>
        public virtual DateTime GetDateTime()
        {
            AtomicValue av = UnderlyingValue;
            CalendarValue value = av is DateTimeValue || av is DateValue
                ? (CalendarValue)av
                : throw new DAXonApiException("Value is not an xs:dateTime or an xs:date");
            string local = value.RemoveTimezone().GetStringValue();
            return DateTime.Parse(ReachedByDateTime(local), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        /// <summary>An xs:dateTime with a time zone, as an instant with its offset. Any other value, and a year DateTimeOffset does not reach, is a DAXonApiException.</summary>
        public virtual DateTimeOffset GetDateTimeOffset()
        {
            if (!(UnderlyingValue is DateTimeValue value) || !value.HasTimezone())
            {
                throw new DAXonApiException("Value is not an xs:dateTime with a time zone");
            }

            try
            {
                return DateTimeOffset.Parse(ReachedByDateTime(value.GetStringValue()), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            }
            catch (Exception e) when (e is FormatException || e is ArgumentOutOfRangeException)
            {
                // 0001-01-01T00:00:00+05:00: a year DateTime has, an instant before its first one.
                throw new DAXonApiException(e.Message);
            }
        }

        /// <summary>An xs:dayTimeDuration as a TimeSpan, to the tick. Any other value, and one a TimeSpan does not reach, is a DAXonApiException.</summary>
        public virtual TimeSpan GetTimeSpan()
        {
            if (!(UnderlyingValue is DayTimeDurationValue))
            {
                throw new DAXonApiException("Value is not an xs:dayTimeDuration");
            }

            try
            {
                return System.Xml.XmlConvert.ToTimeSpan(GetStringValue());
            }
            catch (Exception e) when (e is FormatException || e is OverflowException)
            {
                throw new DAXonApiException(e.Message);
            }
        }

        // The lexical form, or the refusal of a year .NET has no calendar for: a sign, a fifth digit.
        private static string ReachedByDateTime(string lexical)
        {
            if (lexical.StartsWith("-", StringComparison.Ordinal) || lexical.IndexOf('-') != 4 || lexical.StartsWith("0000", StringComparison.Ordinal))
            {
                throw new DAXonApiException("The year of " + lexical + " is outside the range of DateTime");
            }

            return lexical;
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
                string s = Whitespace.Trim(av.UnicodeStringValue).ToString();
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
                    return converter.StringToNumber(av.UnicodeStringValue);
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
                catch (XPathException)
                {
                    throw new DAXonApiException("Cannot cast item to a decimal");
                }
            }
            else if (av is StringValue)
            {
                return ParseDecimal(av.GetStringValue());
            }
            else
            {
                throw new DAXonApiException("Cannot cast item to a decimal");
            }
        }

        // A string as Java's BigDecimal reads it, exponent included. An exponent beyond 1000 is refused: the shim scales
        // by an exact power of ten, and "1e2000000000" would compute one of two billion digits on the host's thread.
        private static BigDecimal ParseDecimal(string text)
        {
            // BigDecimal reads the exponent as an int: one outside that range is its OverflowException, below.
            int e = text.IndexOfAny(new[] { 'e', 'E' });
            if (e >= 0 && int.TryParse(text.Substring(e + 1), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out int exponent)
                && (exponent > 1000 || exponent < -1000))
            {
                throw new DAXonApiException("Cannot cast item to a decimal: exponent out of range");
            }

            try
            {
                return new BigDecimal(text);
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                throw new DAXonApiException("Cannot cast item to a decimal: " + ex.Message);
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