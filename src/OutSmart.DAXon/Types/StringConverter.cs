////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Types
{
    public abstract class StringConverter : Converter
    {
        protected StringConverter()
        {
        }

        protected StringConverter(ConversionRules rules) : base(rules)
        {
        }

        public abstract IConversionResult ConvertString(UnicodeString input);
        public virtual ValidationFailure Validate(UnicodeString input)
        {
            IConversionResult result = ConvertString(input);
            return result is ValidationFailure ? (ValidationFailure)result : null;
        }

        public virtual IConversionResult Convert(AtomicValue input)

        {

            return ConvertString(input.UnicodeStringValue);

        }

        // Bridge the compat base's hollow Convert(object) (returns null) to the real validating path.
        // Call sites typed as the base Converter (CastableExpression.IsCastable etc.) bind Convert(object);
        // without this override every string-source cast "succeeded" and castable-as was always true.
        public override IConversionResult Convert(object value)
        {
            return Convert((AtomicValue)value);
        }

        internal sealed class StringToNonStringDerivedType : StringConverter
        {
            private readonly StringConverter phaseOne;
            private readonly UnfailingConverter.DownCastingConverter phaseTwo;
            public StringToNonStringDerivedType(StringConverter phaseOne, UnfailingConverter.DownCastingConverter phaseTwo)
            {
                this.phaseOne = phaseOne;
                this.phaseTwo = phaseTwo;
            }

            public StringToNonStringDerivedType SetNamespaceResolver(INamespaceResolver resolver)
            {
                return new StringToNonStringDerivedType((StringConverter)phaseOne.SetNamespaceResolver(resolver), (UnfailingConverter.DownCastingConverter)phaseTwo.SetNamespaceResolver(resolver));
            }

            public override Converter SetNamespaceResolver(object resolver) => SetNamespaceResolver((INamespaceResolver)resolver);

            public override IConversionResult ConvertString(UnicodeString input)
            {
                try
                {
                    input = phaseTwo.TargetType.Preprocess(input);
                }
                catch (ValidationException err)
                {
                    return err.GetValidationFailure();
                }

                IConversionResult temp = phaseOne.ConvertString(input);
                if (temp is ValidationFailure)
                {
                    return temp;
                }

                return phaseTwo.Convert((AtomicValue)temp, input);
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                try
                {
                    input = phaseTwo.TargetType.Preprocess(input);
                }
                catch (ValidationException err)
                {
                    return err.GetValidationFailure();
                }

                IConversionResult temp = phaseOne.ConvertString(input);
                if (temp is ValidationFailure)
                {
                    return (ValidationFailure)temp;
                }

                return phaseTwo.Validate((AtomicValue)temp, input);
            }
        }

        /// <summary>
        /// Converts from xs:string or xs:untypedAtomic to xs:String
        /// </summary>
        internal sealed class StringToString : StringConverter
        {
            public static readonly StringToString INSTANCE = new StringToString();
            public override IConversionResult Convert(AtomicValue input)
            {
                return new StringValue(input.UnicodeStringValue.Tidy());
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                return new StringValue(input.Tidy());
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                return null;
            }

            public override bool IsAlwaysSuccessful()
            {
                return true;
            }
        }

        /// <summary>
        /// Converts from xs:string or xs:untypedAtomic to xs:untypedAtomic
        /// </summary>
        internal sealed class StringToUntypedAtomic : StringConverter
        {
            public static readonly StringToUntypedAtomic INSTANCE = new StringToUntypedAtomic();
            public override IConversionResult Convert(AtomicValue input)
            {
                return StringValue.MakeUntypedAtomic(input.UnicodeStringValue);
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                return StringValue.MakeUntypedAtomic(input);
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                return null;
            }

            public override bool IsAlwaysSuccessful()
            {
                return true;
            }
        }

        /// <summary>
        /// Converts from xs:string to xs:normalizedString
        /// </summary>
        internal sealed class StringToNormalizedString : StringConverter
        {
            public static readonly StringToNormalizedString INSTANCE = new StringToNormalizedString();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                return new StringValue(Whitespace.NormalizeWhitespace(input).Tidy(), BuiltInAtomicType.NORMALIZED_STRING);
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                return null;
            }

            public override bool IsAlwaysSuccessful()
            {
                return true;
            }
        }

        /// <summary>
        /// Converts from xs:string to xs:token
        /// </summary>
        internal sealed class StringToToken : StringConverter
        {
            public static readonly StringToToken INSTANCE = new StringToToken();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                return new StringValue(Whitespace.CollapseWhitespace(input).Tidy(), BuiltInAtomicType.TOKEN);
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                return null;
            }

            public override bool IsAlwaysSuccessful()
            {
                return true;
            }
        }

        /// <summary>
        /// Converts from xs:string to xs:language
        /// </summary>
        internal sealed class StringToLanguage : StringConverter
        {
            private static ARegularExpression _regexLazy;
            public static readonly StringToLanguage INSTANCE = new StringToLanguage();
            private static ARegularExpression regex => _regexLazy ??= ARegularExpression.Compile("[a-zA-Z]{1,8}(-[a-zA-Z0-9]{1,8})*", "");
            public override IConversionResult ConvertString(UnicodeString input)
            {
                UnicodeString trimmed = Whitespace.Trim(input);
                if (!regex.Matches(trimmed))
                {
                    return new ValidationFailure("The value '" + input + "' is not a valid xs:language");
                }

                return new StringValue(trimmed, BuiltInAtomicType.LANGUAGE);
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                if (regex.Matches(Whitespace.Trim(input)))
                {
                    return null;
                }
                else
                {
                    return new ValidationFailure("The value '" + input + "' is not a valid xs:language");
                }
            }
        }

        /// <summary>
        /// Converts from xs:string to xs:NCName, xs:ID, xs:IDREF, or xs:ENTITY
        /// </summary>
        internal class StringToNCName : StringConverter
        {
            // Lazy: eager initializers here can run DURING BuiltInAtomicType's own static init (the two
            // classes are mutually referencing), capturing a still-null ID/ENTITY/IDREF and later NRE-ing
            // in casts like xs:NCName(...) cast as xs:ENTITY. First USE is always after full type init.
            private static StringToNCName _toId, _toEntity, _toNCName, _toIdref;
            IAtomicType targetType;
            public static StringToNCName TO_ID => _toId ?? (_toId = new StringToNCName(BuiltInAtomicType.ID));
            public static StringToNCName TO_ENTITY => _toEntity ?? (_toEntity = new StringToNCName(BuiltInAtomicType.ENTITY));
            public static StringToNCName TO_NCNAME => _toNCName ?? (_toNCName = new StringToNCName(BuiltInAtomicType.NCNAME));
            public static StringToNCName TO_IDREF => _toIdref ?? (_toIdref = new StringToNCName(BuiltInAtomicType.IDREF));
            public StringToNCName(IAtomicType targetType)
            {
                this.targetType = targetType;
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                UnicodeString trimmed = Whitespace.Trim(input);
                if (NameChecker.IsValidNCName(trimmed.CodePoints()))
                {
                    return new StringValue(trimmed, targetType);
                }
                else
                {
                    return new ValidationFailure("The value '" + input + "' is not a valid " + targetType.DisplayName);
                }
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                if (NameChecker.IsValidNCName(Whitespace.Trim(input).CodePoints()))
                {
                    return null;
                }
                else
                {
                    return new ValidationFailure("The value '" + input + "' is not a valid " + targetType.DisplayName);
                }
            }
        }

        /// <summary>
        /// Converts from xs:string to xs:NMTOKEN
        /// </summary>
        internal sealed class StringToNMTOKEN : StringConverter
        {
            public static readonly StringToNMTOKEN INSTANCE = new StringToNMTOKEN();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                UnicodeString trimmed = Whitespace.Trim(input);
                if (NameChecker.IsValidNmtoken(trimmed))
                {
                    return new StringValue(trimmed, BuiltInAtomicType.NMTOKEN);
                }
                else
                {
                    return new ValidationFailure("The value '" + input + "' is not a valid xs:NMTOKEN");
                }
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                if (NameChecker.IsValidNmtoken(Whitespace.Trim(input)))
                {
                    return null;
                }
                else
                {
                    return new ValidationFailure("The value '" + input + "' is not a valid xs:NMTOKEN");
                }
            }
        }

        /// <summary>
        /// Converts from xs:string to xs:Name
        /// </summary>
        internal sealed class StringToName : StringToNCName
        {
            public static readonly StringToName INSTANCE = new StringToName();
            public StringToName() : base(BuiltInAtomicType.NAME)
            {
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                ValidationFailure vf = Validate(input);
                if (vf == null)
                {
                    return new StringValue(Whitespace.Trim(input), BuiltInAtomicType.NAME);
                }
                else
                {
                    return vf;
                }
            }

            public override ValidationFailure Validate(UnicodeString input)
            {

                // if it's valid as an NCName then it's OK
                UnicodeString trimmed = Whitespace.Trim(input);
                if (NameChecker.IsValidNCName(trimmed.CodePoints()))
                {
                    return null;
                }


                // if not, replace any colons by underscores and then test if it's a valid NCName
                if (NameChecker.IsValidNCName(trimmed.ToString().Replace(':', '_')))
                {
                    return null;
                }
                else
                {
                    return new ValidationFailure("The value '" + trimmed + "' is not a valid xs:Name");
                }
            }
        }

        /// <summary>
        /// Converts from xs:string to a user-defined type derived directly from xs:string
        /// </summary>
        internal sealed class StringToStringSubtype : StringConverter
        {
            IAtomicType targetType;
            int whitespaceAction;
            public StringToStringSubtype(ConversionRules rules, IAtomicType targetType) : base(rules)
            {
                this.targetType = targetType;
                this.whitespaceAction = targetType.WhitespaceAction;
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                UnicodeString cs = Whitespace.ApplyWhitespaceNormalization(whitespaceAction, input);
                try
                {
                    cs = targetType.Preprocess(cs);
                }
                catch (ValidationException err)
                {
                    return err.GetValidationFailure();
                }

                ValidationFailure f = targetType.Validate(new StringValue(cs), cs, GetConversionRules());
                if (f == null)
                {
                    return new StringValue(cs, targetType);
                }
                else
                {
                    return f;
                }
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                UnicodeString cs = Whitespace.ApplyWhitespaceNormalization(whitespaceAction, input);
                try
                {
                    cs = targetType.Preprocess(cs);
                }
                catch (ValidationException err)
                {
                    return err.GetValidationFailure();
                }

                return targetType.Validate(new StringValue(cs), cs, GetConversionRules());
            }
        }

        /// <summary>
        /// Converts from xs;string to a user-defined type derived from a built-in subtype of xs:string
        /// </summary>
        internal sealed class StringToDerivedStringSubtype : StringConverter
        {
            IAtomicType targetType;
            StringConverter builtInValidator;
            int whitespaceAction;
            public StringToDerivedStringSubtype(ConversionRules rules, IAtomicType targetType) : base(rules)
            {
                this.targetType = targetType;
                this.whitespaceAction = targetType.WhitespaceAction;
                builtInValidator = ((IAtomicType)targetType.BuiltInBaseType).GetStringConverter(rules);
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                UnicodeString cs = Whitespace.ApplyWhitespaceNormalization(whitespaceAction, input);
                ValidationFailure f = builtInValidator.Validate(cs);
                if (f != null)
                {
                    return f;
                }

                try
                {
                    cs = targetType.Preprocess(cs);
                }
                catch (ValidationException err)
                {
                    return err.GetValidationFailure();
                }

                f = targetType.Validate(new StringValue(cs), cs, GetConversionRules());
                if (f == null)
                {
                    return new StringValue(cs, targetType);
                }
                else
                {
                    return f;
                }
            }
        }

        /// <summary>
        /// Converts a string to xs:float
        /// </summary>
        internal sealed class StringToFloat : StringConverter
        {
            public StringToFloat(ConversionRules rules) : base(rules ?? throw new NullReferenceException())
            {
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                if (GetConversionRules().StringToDoubleConverter.TryStringToNumber(input, out double d))
                {
                    return new FloatValue((float)d);
                }

                ValidationFailure ve = new ValidationFailure("Cannot convert string to float: " + input);
                ve.SetErrorCode("FORG0001");
                return ve;
            }
        }

        /// <summary>
        /// Converts a string to an xs:decimal
        /// </summary>
        internal sealed class StringToDecimal : StringConverter
        {
            public static readonly StringToDecimal INSTANCE = new StringToDecimal();

            // Bounded pure-function result cache: xs:decimal casts over a document column
            // typically see a small set of distinct strings, each re-parsed per row. Entries are
            // immutable and reference writes are atomic, so racing threads at worst lose a write
            // and recompute. Keys are re-materialized (BMPString over a fresh string), never the
            // incoming slice, so the cache cannot retain a document's text buffer.
            private sealed class DecEntry
            {
                internal readonly UnicodeString key;
                internal readonly BigDecimalValue value;
                internal DecEntry(UnicodeString key, BigDecimalValue value) { this.key = key; this.value = value; }
            }

            private readonly DecEntry[] cache = new DecEntry[1024];

            public override IConversionResult ConvertString(UnicodeString input)
            {
                int idx = (int)((uint)(input.GetHashCode() * -1640531527) >> 22) & 1023;
                DecEntry e = cache[idx];
                if (e != null && input.Equals(e.key))
                {
                    return e.value;
                }

                string s = input.ToString();
                IConversionResult result = BigDecimalValue.MakeDecimalValue(s, true);
                if (result is BigDecimalValue dv && s.Length <= 64)
                {
                    cache[idx] = new DecEntry(BMPString.Of(s), dv);
                }

                return result;
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                if (BigDecimalValue.CastableAsDecimal(input.ToString()))
                {
                    return null;
                }
                else
                {
                    return new ValidationFailure("Cannot convert string to decimal: " + input);
                }
            }
        }

        /// <summary>
        /// Converts a string to an integer
        /// </summary>
        internal sealed class StringToInteger : StringConverter
        {
            public static readonly StringToInteger INSTANCE = new StringToInteger();

            public override IConversionResult ConvertString(UnicodeString input)
            {
                return IntegerValue.StringToInteger(input);
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                return IntegerValue.CastableAsInteger(input);
            }
        }

        /// <summary>
        /// Converts a string to a duration
        /// </summary>
        internal sealed class StringToDuration : StringConverter
        {
            public static readonly StringToDuration INSTANCE = new StringToDuration();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                return DurationValue.MakeDuration(input);
            }
        }

        /// <summary>
        /// Converts a string to a dayTimeDuration
        /// </summary>
        internal sealed class StringToDayTimeDuration : StringConverter
        {
            public static readonly StringToDayTimeDuration INSTANCE = new StringToDayTimeDuration();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                return DayTimeDurationValue.MakeDayTimeDurationValue(input);
            }
        }

        /// <summary>
        /// Converts a string to a yearMonthDuration
        /// </summary>
        internal sealed class StringToYearMonthDuration : StringConverter
        {
            public static readonly StringToYearMonthDuration INSTANCE = new StringToYearMonthDuration();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                return YearMonthDurationValue.MakeYearMonthDurationValue(input);
            }
        }

        /// <summary>
        /// Converts a string to a dateTime
        /// </summary>
        internal sealed class StringToDateTime : StringConverter
        {
            public StringToDateTime(ConversionRules rules) : base(rules)
            {
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                return DateTimeValue.MakeDateTimeValue(input, GetConversionRules());
            }
        }

        /// <summary>
        /// Converts a string to a dateTimeStamp
        /// </summary>
        internal sealed class StringToDateTimeStamp : StringConverter
        {
            public StringToDateTimeStamp(ConversionRules rules) : base(rules)
            {
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                IConversionResult val = DateTimeValue.MakeDateTimeValue(input, GetConversionRules());
                if (val is DateTimeValue)
                {
                    if (!((DateTimeValue)val).HasTimezone())
                    {
                        return new ValidationFailure("Supplied DateTimeStamp value " + input + " has no time zone");
                    }
                    else
                    {
                        val = ((DateTimeValue)val).CopyAsSubType(BuiltInAtomicType.DATE_TIME_STAMP);
                    }
                }

                return val;
            }
        }

        /// <summary>
        /// Converts a string to a date
        /// </summary>
        internal sealed class StringToDate : StringConverter
        {
            public StringToDate(ConversionRules rules) : base(rules)
            {
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                return DateValue.MakeDateValue(input, GetConversionRules());
            }
        }

        /// <summary>
        /// Converts a string to a gMonth
        /// </summary>
        internal sealed class StringToGMonth : StringConverter
        {
            public static readonly StringToGMonth INSTANCE = new StringToGMonth();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                return GMonthValue.MakeGMonthValue(input);
            }
        }

        /// <summary>
        /// Converts a string to a gYearMonth
        /// </summary>
        internal sealed class StringToGYearMonth : StringConverter
        {
            public StringToGYearMonth(ConversionRules rules) : base(rules)
            {
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                return GYearMonthValue.MakeGYearMonthValue(input, GetConversionRules());
            }
        }

        /// <summary>
        /// Converts a string to a gYear
        /// </summary>
        internal sealed class StringToGYear : StringConverter
        {
            public StringToGYear(ConversionRules rules) : base(rules)
            {
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                return GYearValue.MakeGYearValue(input, GetConversionRules());
            }
        }

        /// <summary>
        /// Converts a string to a gMonthDay
        /// </summary>
        internal sealed class StringToGMonthDay : StringConverter
        {
            public static readonly StringToGMonthDay INSTANCE = new StringToGMonthDay();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                return GMonthDayValue.MakeGMonthDayValue(input);
            }
        }

        /// <summary>
        /// Converts a string to a gDay
        /// </summary>
        internal sealed class StringToGDay : StringConverter
        {
            public static readonly StringToGDay INSTANCE = new StringToGDay();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                return GDayValue.MakeGDayValue(input);
            }
        }

        /// <summary>
        /// Converts a string to a time
        /// </summary>
        internal sealed class StringToTime : StringConverter
        {
            public static readonly StringToTime INSTANCE = new StringToTime();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                return TimeValue.MakeTimeValue(input);
            }
        }

        /// <summary>
        /// Converts a string to a boolean
        /// </summary>
        internal sealed class StringToBoolean : StringConverter
        {
            public static readonly StringToBoolean INSTANCE = new StringToBoolean();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                return BooleanValue.FromString(input);
            }
        }

        /// <summary>
        /// Converts a string to hexBinary
        /// </summary>
        internal sealed class StringToHexBinary : StringConverter
        {
            public static readonly StringToHexBinary INSTANCE = new StringToHexBinary();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                try
                {
                    return new HexBinaryValue(input);
                }
                catch (XPathException e)
                {
                    return ValidationFailure.FromException(e);
                }
            }
        }

        /// <summary>
        /// Converts string to base64
        /// </summary>
        internal sealed class StringToBase64Binary : StringConverter
        {
            public static readonly StringToBase64Binary INSTANCE = new StringToBase64Binary();
            public override IConversionResult ConvertString(UnicodeString input)
            {
                try
                {
                    return new Base64BinaryValue(input);
                }
                catch (XPathException e)
                {
                    return ValidationFailure.FromException(e);
                }
            }
        }

        /// <summary>
        /// Converts String to QName
        /// </summary>
        internal sealed class StringToQName : StringConverter
        {
            private INamespaceResolver nsResolver;
            public StringToQName(ConversionRules rules) : base(rules)
            {
            }

            public StringToQName SetNamespaceResolver(INamespaceResolver resolver)
            {
                StringToQName c = new StringToQName(GetConversionRules());
                c.nsResolver = resolver;
                return c;
            }

            // The compat base declares SetNamespaceResolver(object) (hollow => this); the typed overload
            // above is a separate signature, so a base-typed call (CastExpression wires the resolver via
            // Converter.SetNamespaceResolver(GetRetainedStaticContext())) hit the hollow one and dropped
            // the resolver -> "Cannot validate a QName without a namespace resolver". Forward it.
            public override Converter SetNamespaceResolver(object resolver) => SetNamespaceResolver((INamespaceResolver)resolver);

            public override IConversionResult ConvertString(UnicodeString input)
            {
                if (nsResolver == null)
                {
                    throw new NotSupportedException("Cannot validate a QName without a namespace resolver");
                }

                try
                {
                    string[] parts = NameChecker.TryGetQNameParts(Whitespace.Trim(input.ToString()));
                    if (parts == null)
                    {
                        return new ValidationFailure("Invalid lexical QName " + Err.Wrap(input));
                    }

                    NamespaceUri uri = nsResolver.GetURIForPrefix(parts[0], true);
                    if (uri == null)
                    {
                        ValidationFailure failure = new ValidationFailure("Namespace prefix " + Err.Wrap(parts[0]) + " has not been declared");
                        failure.SetErrorCode("FONS0004");
                        return failure;
                    }

                    return new QNameValue(parts[0], uri, parts[1], BuiltInAtomicType.QNAME, false);
                }
                catch (XPathException err)
                {
                    return new ValidationFailure(err.Message);
                }
            }
        }

        /// <summary>
        /// Converts String to NOTATION
        /// </summary>
        internal sealed class StringToNotation : StringConverter
        {
            private INamespaceResolver nsResolver;
            public StringToNotation(ConversionRules rules) : base(rules)
            {
            }

            public StringToNotation SetNamespaceResolver(INamespaceResolver resolver)
            {
                StringToNotation c = new StringToNotation(GetConversionRules());
                c.nsResolver = resolver;
                return c;
            }

            public override Converter SetNamespaceResolver(object resolver) => SetNamespaceResolver((INamespaceResolver)resolver);

            public INamespaceResolver GetNamespaceResolver()
            {
                return nsResolver;
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                if (GetNamespaceResolver() == null)
                {
                    throw new NotSupportedException("Cannot validate a NOTATION without a namespace resolver");
                }

                try
                {
                    string[] parts = NameChecker.TryGetQNameParts(Whitespace.Trim(input.ToString()));
                    if (parts == null)
                    {
                        return new ValidationFailure("Invalid lexical QName " + Err.Wrap(input));
                    }

                    NamespaceUri uri = GetNamespaceResolver().GetURIForPrefix(parts[0], true);
                    if (uri == null)
                    {
                        return new ValidationFailure("Namespace prefix " + Err.Wrap(parts[0]) + " has not been declared");
                    }


                    // This check added in 9.3. The XSLT spec says that this check should not be performed during
                    // validation. However, this appears to be based on an incorrect assumption: see spec bug 6952
                    if (!GetConversionRules().IsDeclaredNotation(uri, parts[1]))
                    {

                        return new ValidationFailure("Notation {" + uri + "}" + parts[1] + " is not declared in the schema");
                    }

                    return new NotationValue(parts[0], uri, parts[1], false);
                }
                catch (XPathException err)
                {
                    return new ValidationFailure(err.Message);
                }
            }
        }

        /// <summary>
        /// Converts string to anyURI
        /// </summary>
        internal sealed class StringToAnyURI : StringConverter
        {
            public StringToAnyURI(ConversionRules rules) : base(rules)
            {
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                if (GetConversionRules().IsValidURI(input.ToString()))
                {
                    return new AnyURIValue(input);
                }
                else
                {
                    return new ValidationFailure("Invalid URI: " + input);
                }
            }

            public override ValidationFailure Validate(UnicodeString input)
            {
                if (GetConversionRules().IsValidURI(input.ToString()))
                {
                    return null;
                }
                else
                {
                    return new ValidationFailure("Invalid URI: " + input);
                }
            }
        }

        /// <summary>
        /// Converter from string to plain union types
        /// </summary>
        internal sealed class StringToUnionConverter : StringConverter
        {
            IPlainType targetType;
            ConversionRules rules;
            public StringToUnionConverter(IPlainType targetType, ConversionRules rules)
            {
                if (!targetType.IsPlainType())
                {
                    throw new ArgumentException();
                }

                if (targetType.IsNamespaceSensitive())
                {
                    throw new ArgumentException();
                }

                this.targetType = targetType;
                this.rules = rules;
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                try
                {

                    return (AtomicValue)((IUnionType)targetType).GetTypedValue(input, null, rules).Head();
                }
                catch (ValidationException err)
                {
                    return err.GetValidationFailure();
                }
            }
        }
    }
}
