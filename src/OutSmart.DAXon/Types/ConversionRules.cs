////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using System;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Caching;
namespace OutSmart.DAXon.Lib
{
    public class ConversionRules
    {
        private StringToDouble stringToDouble = StringToDouble11.GetInstance();
        private INotationSet notationSet; // may be null
        private IURIChecker uriChecker;
        private bool allowYearZero = true;
        private TypeHierarchy typeHierarchy; // may be null
        // Process-shared across threads via a single Configuration; the cache must be genuinely
        // thread-safe. (Was Expr.Sort.LFUCache(concurrent:true) -- but the port silently backed that
        // with a plain Dictionary, so it was not thread-safe; Internal.Caching.ClockCache is.)
        private readonly ClockCache<int, Converter> converterCache = new ClockCache<int, Converter>(100);
        public static readonly ConversionRules DEFAULT = new ConversionRules();
        public ConversionRules()
        {
        }

        public virtual ConversionRules Copy()
        {
            ConversionRules cr = new ConversionRules();
            CopyTo(cr);
            return cr;
        }

        public virtual void CopyTo(ConversionRules cr)
        {
            cr.stringToDouble = stringToDouble;
            cr.notationSet = notationSet;
            cr.uriChecker = uriChecker;
            cr.allowYearZero = allowYearZero;
            cr.typeHierarchy = typeHierarchy;
            cr.converterCache.Clear();
        }

        public virtual void SetTypeHierarchy(TypeHierarchy typeHierarchy)
        {
            this.typeHierarchy = typeHierarchy;
        }

        public virtual StringToDouble StringToDoubleConverter
        {
            get => stringToDouble; set
            {
                this.stringToDouble = value;
            }
        }

        public virtual void SetNotationSet(INotationSet notations)
        {
            this.notationSet = notations;
        }

        public virtual bool IsDeclaredNotation(NamespaceUri uri, string local)
        {

            if (notationSet == null)
            {
                return true; // in the absence of a known configuration, treat all notations as valid
            }
            else
            {
                return notationSet.IsDeclaredNotation(uri, local);
            }
        }

        public virtual void SetURIChecker(IURIChecker checker)
        {
            this.uriChecker = checker;
        }

        public virtual bool IsValidURI(string str)
        {
            return uriChecker == null || uriChecker.IsValidURI(str);
        }

        public virtual void SetAllowYearZero(bool allowed)
        {
            allowYearZero = allowed;
        }

        public virtual bool IsAllowYearZero()
        {
            return allowYearZero;
        }

        public virtual Converter GetConverter(IAtomicType source, IAtomicType target)
        {

            // Handle some common cases before looking in the cache
            if (source == target)
            {
                return StringConverter.IdentityConverter.INSTANCE;
            }
            else if (source == BuiltInAtomicType.STRING || source == BuiltInAtomicType.UNTYPED_ATOMIC)
            {
                return target.GetStringConverter(this);
            }
            else if (target == BuiltInAtomicType.STRING)
            {
                return Converter.ToStringConverter.INSTANCE;
            }
            else if (target == BuiltInAtomicType.UNTYPED_ATOMIC)
            {
                return Converter.ToUntypedAtomicConverter.INSTANCE;
            }


            // For a lookup key, use the primitive type of the source type (always 10 bits) and the
            // fingerprint of the target type (20 bits)
            int key = (source.PrimitiveType << 20) | target.Fingerprint;
            if (converterCache.TryGet(key, out Converter cached))
            {
                return cached;
            }
            Converter converter = MakeConverter(source, target);
            if (converter == null)
            {
                // No converter for this pair: not cached, recomputed on each call (as before).
                return null;
            }
            return converterCache.GetOrAdd(key, _ => converter);
        }

        private Converter MakeConverter(IAtomicType sourceType, IAtomicType targetType)
        {
            if (sourceType == targetType)
            {
                return StringConverter.IdentityConverter.INSTANCE;
            }

            int tt = targetType.Fingerprint;
            int tp = targetType.PrimitiveType;
            int st = sourceType.PrimitiveType;
            if ((st == StandardNames.XS_STRING || st == StandardNames.XS_UNTYPED_ATOMIC) && (tp == StandardNames.XS_STRING || tp == StandardNames.XS_UNTYPED_ATOMIC))
            {
                return MakeStringConverter(targetType);
            }

            if (!targetType.IsPrimitiveType())
            {
                IAtomicType primTarget = (IAtomicType)targetType.GetPrimitiveItemType();
                if (sourceType == primTarget)
                {
                    return new UnfailingConverter.DownCastingConverter(targetType, this);
                }
                else if (st == StandardNames.XS_STRING || st == StandardNames.XS_UNTYPED_ATOMIC)
                {
                    return MakeStringConverter(targetType);
                }
                else
                {
                    Converter stageOne = MakeConverter(sourceType, primTarget);
                    if (stageOne == null)
                    {
                        return null;
                    }

                    Converter stageTwo = new UnfailingConverter.DownCastingConverter(targetType, this);
                    return new TwoPhaseConverter(stageOne, stageTwo);
                }
            }

            if (st == tt)
            {

                // we are casting between subtypes of the same primitive type.
                if (typeHierarchy != null && typeHierarchy.IsSubType(sourceType, targetType))
                {
                    return new UpCastingConverter(targetType);
                }

                Converter upcast = new UpCastingConverter((IAtomicType)sourceType.GetPrimitiveItemType());
                Converter downcast = new UnfailingConverter.DownCastingConverter(targetType, this);
                return new TwoPhaseConverter(upcast, downcast);
            }

            switch (tt)
            {
                case StandardNames.XS_UNTYPED_ATOMIC:
                    return Converter.ToUntypedAtomicConverter.INSTANCE;
                case StandardNames.XS_STRING:
                    return Converter.ToStringConverter.INSTANCE;
                case StandardNames.XS_FLOAT:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return new StringToFloat(this);
                        case StandardNames.XS_DOUBLE:
                        case StandardNames.XS_DECIMAL:
                        case StandardNames.XS_INTEGER:
                        case StandardNames.XS_NUMERIC:
                            return Converter.NumericToFloat.INSTANCE;
                        case StandardNames.XS_BOOLEAN:
                            return Converter.BooleanToFloat.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_DOUBLE:
                case StandardNames.XS_NUMERIC:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return stringToDouble;
                        case StandardNames.XS_FLOAT:
                        case StandardNames.XS_DECIMAL:
                        case StandardNames.XS_INTEGER:
                        case StandardNames.XS_NUMERIC:
                            return Converter.NumericToDouble.INSTANCE;
                        case StandardNames.XS_BOOLEAN:
                            return Converter.BooleanToDouble.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_DECIMAL:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToDecimal.INSTANCE;
                        case StandardNames.XS_FLOAT:
                            return Converter.FloatToDecimal.INSTANCE;
                        case StandardNames.XS_DOUBLE:
                            return Converter.DoubleToDecimal.INSTANCE;
                        case StandardNames.XS_INTEGER:
                            return Converter.IntegerToDecimal.INSTANCE;
                        case StandardNames.XS_NUMERIC:
                            return Converter.NumericToDecimal.INSTANCE;
                        case StandardNames.XS_BOOLEAN:
                            return Converter.BooleanToDecimal.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_INTEGER:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToInteger.INSTANCE;
                        case StandardNames.XS_FLOAT:
                            return Converter.FloatToInteger.INSTANCE;
                        case StandardNames.XS_DOUBLE:
                            return Converter.DoubleToInteger.INSTANCE;
                        case StandardNames.XS_DECIMAL:
                            return Converter.DecimalToInteger.INSTANCE;
                        case StandardNames.XS_NUMERIC:
                            return Converter.NumericToInteger.INSTANCE;
                        case StandardNames.XS_BOOLEAN:
                            return Converter.BooleanToInteger.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_DURATION:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToDuration.INSTANCE;
                        case StandardNames.XS_DAY_TIME_DURATION:
                        case StandardNames.XS_YEAR_MONTH_DURATION:
                            return new UpCastingConverter(BuiltInAtomicType.DURATION);
                        default:
                            return null;
                    }

                case StandardNames.XS_YEAR_MONTH_DURATION:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToYearMonthDuration.INSTANCE;
                        case StandardNames.XS_DURATION:
                        case StandardNames.XS_DAY_TIME_DURATION:
                            return Converter.DurationToYearMonthDuration.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_DAY_TIME_DURATION:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToDayTimeDuration.INSTANCE;
                        case StandardNames.XS_DURATION:
                        case StandardNames.XS_YEAR_MONTH_DURATION:
                            return Converter.DurationToDayTimeDuration.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_DATE_TIME:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return new StringToDateTime(this);
                        case StandardNames.XS_DATE:
                            return Converter.DateToDateTime.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_TIME:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToTime.INSTANCE;
                        case StandardNames.XS_DATE_TIME:
                            return Converter.DateTimeToTime.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_DATE:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return new StringToDate(this);
                        case StandardNames.XS_DATE_TIME:
                            return Converter.DateTimeToDate.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_G_YEAR_MONTH:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return new StringToGYearMonth(this);
                        case StandardNames.XS_DATE:
                            return Converter.TwoPhaseConverter.MakeTwoPhaseConverter(BuiltInAtomicType.DATE, BuiltInAtomicType.DATE_TIME, BuiltInAtomicType.G_YEAR_MONTH, this);
                        case StandardNames.XS_DATE_TIME:
                            return Converter.DateTimeToGYearMonth.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_G_YEAR:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return new StringToGYear(this);
                        case StandardNames.XS_DATE:
                            return Converter.TwoPhaseConverter.MakeTwoPhaseConverter(BuiltInAtomicType.DATE, BuiltInAtomicType.DATE_TIME, BuiltInAtomicType.G_YEAR, this);
                        case StandardNames.XS_DATE_TIME:
                            return Converter.DateTimeToGYear.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_G_MONTH_DAY:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToGMonthDay.INSTANCE;
                        case StandardNames.XS_DATE:
                            return Converter.TwoPhaseConverter.MakeTwoPhaseConverter(BuiltInAtomicType.DATE, BuiltInAtomicType.DATE_TIME, BuiltInAtomicType.G_MONTH_DAY, this);
                        case StandardNames.XS_DATE_TIME:
                            return Converter.DateTimeToGMonthDay.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_G_DAY:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToGDay.INSTANCE;
                        case StandardNames.XS_DATE:
                            return Converter.TwoPhaseConverter.MakeTwoPhaseConverter(BuiltInAtomicType.DATE, BuiltInAtomicType.DATE_TIME, BuiltInAtomicType.G_DAY, this);
                        case StandardNames.XS_DATE_TIME:
                            return Converter.DateTimeToGDay.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_G_MONTH:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToGMonth.INSTANCE;
                        case StandardNames.XS_DATE:
                            return Converter.TwoPhaseConverter.MakeTwoPhaseConverter(BuiltInAtomicType.DATE, BuiltInAtomicType.DATE_TIME, BuiltInAtomicType.G_MONTH, this);
                        case StandardNames.XS_DATE_TIME:
                            return Converter.DateTimeToGMonth.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_BOOLEAN:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToBoolean.INSTANCE;
                        case StandardNames.XS_FLOAT:
                        case StandardNames.XS_DOUBLE:
                        case StandardNames.XS_DECIMAL:
                        case StandardNames.XS_INTEGER:
                        case StandardNames.XS_NUMERIC:
                            return Converter.NumericToBoolean.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_BASE64_BINARY:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToBase64Binary.INSTANCE;
                        case StandardNames.XS_HEX_BINARY:
                            return Converter.HexBinaryToBase64Binary.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_HEX_BINARY:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToHexBinary.INSTANCE;
                        case StandardNames.XS_BASE64_BINARY:
                            return Converter.Base64BinaryToHexBinary.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_ANY_URI:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return new StringToAnyURI(this);
                        default:
                            return null;
                    }

                case StandardNames.XS_QNAME:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return new StringToQName(this);
                        case StandardNames.XS_NOTATION:
                            return Converter.NotationToQName.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_NOTATION:
                    switch (st)
                    {
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_STRING:
                            return new StringToNotation(this);
                        case StandardNames.XS_QNAME:
                            return Converter.QNameToNotation.INSTANCE;
                        default:
                            return null;
                    }

                case StandardNames.XS_ANY_ATOMIC_TYPE:
                    return StringConverter.IdentityConverter.INSTANCE;
                default:
                    throw new ArgumentException("Unknown primitive type " + tt);
            }
        }

        public virtual StringConverter MakeStringConverter(IAtomicType targetType)
        {
            int tt = targetType.PrimitiveType;
            if (targetType.IsBuiltInType())
            {
                if (tt == StandardNames.XS_STRING)
                {
                    switch (targetType.Fingerprint)
                    {
                        case StandardNames.XS_STRING:
                            return StringConverter.StringToString.INSTANCE;
                        case StandardNames.XS_NORMALIZED_STRING:
                            return StringConverter.StringToNormalizedString.INSTANCE;
                        case StandardNames.XS_TOKEN:
                            return StringConverter.StringToToken.INSTANCE;
                        case StandardNames.XS_LANGUAGE:
                            return StringConverter.StringToLanguage.INSTANCE;
                        case StandardNames.XS_NAME:
                            return StringConverter.StringToName.INSTANCE;
                        case StandardNames.XS_NCNAME:
                            return StringConverter.StringToNCName.TO_NCNAME;
                        case StandardNames.XS_ID:
                            return StringConverter.StringToNCName.TO_ID;
                        case StandardNames.XS_IDREF:
                            return StringConverter.StringToNCName.TO_IDREF;
                        case StandardNames.XS_ENTITY:
                            return StringConverter.StringToNCName.TO_ENTITY;
                        case StandardNames.XS_NMTOKEN:
                            return StringConverter.StringToNMTOKEN.INSTANCE;
                        default:
                            throw new InvalidOperationException("Unknown built-in subtype of xs:string");
                    }
                }
                else if (tt == StandardNames.XS_UNTYPED_ATOMIC)
                {
                    return StringConverter.StringToUntypedAtomic.INSTANCE;
                }
                else if (targetType.IsPrimitiveType())
                {

                    // converter to built-in types unrelated to xs:string
                    Converter converter = GetConverter(BuiltInAtomicType.STRING, targetType);
                    return (StringConverter)converter;
                }
                else if (tt == StandardNames.XS_INTEGER)
                {
                    return new Types.StringToIntegerSubtype((BuiltInAtomicType)targetType);
                }
                else
                {
                    switch (targetType.Fingerprint)
                    {
                        case StandardNames.XS_DAY_TIME_DURATION:
                            return StringConverter.StringToDayTimeDuration.INSTANCE;
                        case StandardNames.XS_YEAR_MONTH_DURATION:
                            return StringConverter.StringToYearMonthDuration.INSTANCE;
                        case StandardNames.XS_DATE_TIME_STAMP:
                            StringConverter first = new StringToDateTime(this);
                            UnfailingConverter.DownCastingConverter second = new UnfailingConverter.DownCastingConverter(targetType, this);
                            return new StringConverter.StringToNonStringDerivedType(first, second);
                        default:
                            throw new InvalidOperationException("Unknown built in type " + targetType);
                    }
                }
            }
            else
            {
                if (tt == StandardNames.XS_STRING)
                {
                    if (targetType.BuiltInBaseType == BuiltInAtomicType.STRING)
                    {

                        // converter to user-defined subtypes of xs:string
                        return new StringConverter.StringToStringSubtype(this, targetType);
                    }
                    else
                    {

                        // converter to user-defined subtypes of built-in subtypes of xs:string
                        return new StringConverter.StringToDerivedStringSubtype(this, targetType);
                    }
                }
                else
                {

                    // converter to user-defined types derived from types other than xs:string
                    StringConverter first = ((IAtomicType)targetType.GetPrimitiveItemType()).GetStringConverter(this);
                    UnfailingConverter.DownCastingConverter second = new UnfailingConverter.DownCastingConverter(targetType, this);
                    return new StringConverter.StringToNonStringDerivedType(first, second);
                }
            }
        }
    }
}
