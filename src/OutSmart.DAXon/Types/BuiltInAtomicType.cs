////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.schema.UserSimpleType;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal.Collections;
using static OutSmart.DAXon.Types.SchemaValidationStatus;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Types
{
    public class BuiltInAtomicType : IAtomicType, IItemTypeWithSequenceTypeCache
    {
        private static readonly Dictionary<string, BuiltInAtomicType> byAlphaCode = new Dictionary<string, BuiltInAtomicType>(60);
        public static readonly BuiltInAtomicType ANY_ATOMIC = MakeAtomicType(StandardNames.XS_ANY_ATOMIC_TYPE, AnySimpleType.INSTANCE, "A", true);
        public static readonly BuiltInAtomicType STRING = MakeAtomicType(StandardNames.XS_STRING, ANY_ATOMIC, "AS", true);
        public static readonly BuiltInAtomicType BOOLEAN = MakeAtomicType(StandardNames.XS_BOOLEAN, ANY_ATOMIC, "AB", true);
        public static readonly BuiltInAtomicType DURATION = MakeAtomicType(StandardNames.XS_DURATION, ANY_ATOMIC, "AR", false);
        public static readonly BuiltInAtomicType DATE_TIME = MakeAtomicType(StandardNames.XS_DATE_TIME, ANY_ATOMIC, "AM", true);
        public static readonly BuiltInAtomicType DATE = MakeAtomicType(StandardNames.XS_DATE, ANY_ATOMIC, "AA", true);
        public static readonly BuiltInAtomicType TIME = MakeAtomicType(StandardNames.XS_TIME, ANY_ATOMIC, "AT", true);
        public static readonly BuiltInAtomicType G_YEAR_MONTH = MakeAtomicType(StandardNames.XS_G_YEAR_MONTH, ANY_ATOMIC, "AH", false);
        public static readonly BuiltInAtomicType G_MONTH = MakeAtomicType(StandardNames.XS_G_MONTH, ANY_ATOMIC, "AI", false);
        public static readonly BuiltInAtomicType G_MONTH_DAY = MakeAtomicType(StandardNames.XS_G_MONTH_DAY, ANY_ATOMIC, "AJ", false);
        public static readonly BuiltInAtomicType G_YEAR = MakeAtomicType(StandardNames.XS_G_YEAR, ANY_ATOMIC, "AG", false);
        public static readonly BuiltInAtomicType G_DAY = MakeAtomicType(StandardNames.XS_G_DAY, ANY_ATOMIC, "AK", false);
        public static readonly BuiltInAtomicType HEX_BINARY = MakeAtomicType(StandardNames.XS_HEX_BINARY, ANY_ATOMIC, "AX", true);
        public static readonly BuiltInAtomicType BASE64_BINARY = MakeAtomicType(StandardNames.XS_BASE64_BINARY, ANY_ATOMIC, "A2", true);
        public static readonly BuiltInAtomicType ANY_URI = MakeAtomicType(StandardNames.XS_ANY_URI, ANY_ATOMIC, "AU", true);
        public static readonly BuiltInAtomicType QNAME = MakeAtomicType(StandardNames.XS_QNAME, ANY_ATOMIC, "AQ", false);
        public static readonly BuiltInAtomicType NOTATION = MakeAtomicType(StandardNames.XS_NOTATION, ANY_ATOMIC, "AN", false);
        public static readonly BuiltInAtomicType UNTYPED_ATOMIC = MakeAtomicType(StandardNames.XS_UNTYPED_ATOMIC, ANY_ATOMIC, "AZ", true);
        public static readonly BuiltInAtomicType DECIMAL = MakeAtomicType(StandardNames.XS_DECIMAL, ANY_ATOMIC, "AD", true);
        public static readonly BuiltInAtomicType FLOAT = MakeAtomicType(StandardNames.XS_FLOAT, ANY_ATOMIC, "AF", true);
        public static readonly BuiltInAtomicType DOUBLE = MakeAtomicType(StandardNames.XS_DOUBLE, ANY_ATOMIC, "AO", true);
        public static readonly BuiltInAtomicType INTEGER = MakeAtomicType(StandardNames.XS_INTEGER, DECIMAL, "ADI", true);
        public static readonly BuiltInAtomicType NON_POSITIVE_INTEGER = MakeAtomicType(StandardNames.XS_NON_POSITIVE_INTEGER, INTEGER, "ADIN", true);
        public static readonly BuiltInAtomicType NEGATIVE_INTEGER = MakeAtomicType(StandardNames.XS_NEGATIVE_INTEGER, NON_POSITIVE_INTEGER, "ADINN", true);
        public static readonly BuiltInAtomicType LONG = MakeAtomicType(StandardNames.XS_LONG, INTEGER, "ADIL", true);
        public static readonly BuiltInAtomicType INT = MakeAtomicType(StandardNames.XS_INT, LONG, "ADILI", true);
        public static readonly BuiltInAtomicType SHORT = MakeAtomicType(StandardNames.XS_SHORT, INT, "ADILIS", true);
        public static readonly BuiltInAtomicType BYTE = MakeAtomicType(StandardNames.XS_BYTE, SHORT, "ADILISB", true);
        public static readonly BuiltInAtomicType NON_NEGATIVE_INTEGER = MakeAtomicType(StandardNames.XS_NON_NEGATIVE_INTEGER, INTEGER, "ADIP", true);
        public static readonly BuiltInAtomicType POSITIVE_INTEGER = MakeAtomicType(StandardNames.XS_POSITIVE_INTEGER, NON_NEGATIVE_INTEGER, "ADIPP", true);
        public static readonly BuiltInAtomicType UNSIGNED_LONG = MakeAtomicType(StandardNames.XS_UNSIGNED_LONG, NON_NEGATIVE_INTEGER, "ADIPL", true);
        public static readonly BuiltInAtomicType UNSIGNED_INT = MakeAtomicType(StandardNames.XS_UNSIGNED_INT, UNSIGNED_LONG, "ADIPLI", true);
        public static readonly BuiltInAtomicType UNSIGNED_SHORT = MakeAtomicType(StandardNames.XS_UNSIGNED_SHORT, UNSIGNED_INT, "ADIPLIS", true);
        public static readonly BuiltInAtomicType UNSIGNED_BYTE = MakeAtomicType(StandardNames.XS_UNSIGNED_BYTE, UNSIGNED_SHORT, "ADIPLISB", true);
        public static readonly BuiltInAtomicType YEAR_MONTH_DURATION = MakeAtomicType(StandardNames.XS_YEAR_MONTH_DURATION, DURATION, "ARY", true);
        public static readonly BuiltInAtomicType DAY_TIME_DURATION = MakeAtomicType(StandardNames.XS_DAY_TIME_DURATION, DURATION, "ARD", true);
        public static readonly BuiltInAtomicType NORMALIZED_STRING = MakeAtomicType(StandardNames.XS_NORMALIZED_STRING, STRING, "ASN", true);
        public static readonly BuiltInAtomicType TOKEN = MakeAtomicType(StandardNames.XS_TOKEN, NORMALIZED_STRING, "ASNT", true);
        public static readonly BuiltInAtomicType LANGUAGE = MakeAtomicType(StandardNames.XS_LANGUAGE, TOKEN, "ASNTL", true);
        // Internal, alone among these singletons: it collides by case with the Name property
        // below, which implements ISchemaType.Name and so cannot move. A case-insensitive
        // binder rejects the whole type over the pair, and the property is the one hosts read.
        internal static readonly BuiltInAtomicType NAME = MakeAtomicType(StandardNames.XS_NAME, TOKEN, "ASNTN", true);
        public static readonly BuiltInAtomicType NMTOKEN = MakeAtomicType(StandardNames.XS_NMTOKEN, TOKEN, "ASNTK", true);
        public static readonly BuiltInAtomicType NCNAME = MakeAtomicType(StandardNames.XS_NCNAME, NAME, "ASNTNC", true);
        public static readonly BuiltInAtomicType ID = MakeAtomicType(StandardNames.XS_ID, NCNAME, "ASNTNCI", true);
        public static readonly BuiltInAtomicType IDREF = MakeAtomicType(StandardNames.XS_IDREF, NCNAME, "ASNTNCR", true);
        public static readonly BuiltInAtomicType ENTITY = MakeAtomicType(StandardNames.XS_ENTITY, NCNAME, "ASNTNCE", true);
        public static readonly BuiltInAtomicType DATE_TIME_STAMP = MakeAtomicType(StandardNames.XS_DATE_TIME_STAMP, DATE_TIME, "AMP", true);
        private readonly int fingerprint;
        private int baseFingerprint;
        private int primitiveFingerprint;
        private UType uType;
        private string alphaCode;
        private bool ordered = false;
        public StringConverter stringConverter; // may be null for types where conversion rules can vary
        private SequenceType _one;
        private SequenceType _oneOrMore;
        private SequenceType _zeroOrOne;
        private SequenceType _zeroOrMore;

        public virtual string Name => StandardNames.GetLocalName(fingerprint);

        public virtual NamespaceUri TargetNamespace => NamespaceUri.SCHEMA;

        public virtual string EQName => "Q{" + NamespaceUri.SCHEMA + "}" + Name;

        public virtual StructuredQName TypeName => new StructuredQName(StandardNames.GetPrefix(fingerprint), StandardNames.GetURI(fingerprint), StandardNames.GetLocalName(fingerprint));

        public virtual string BasicAlphaCode => alphaCode;

        public virtual int RedefinitionLevel => 0;

        public SchemaValidationStatus ValidationStatus => VALIDATED;

        public int DerivationMethod => Derivation.DERIVATION_RESTRICTION;

        public virtual int FinalProhibitions => 0;

        public int Fingerprint => fingerprint;

        public virtual string DisplayName => StandardNames.GetDisplayName(fingerprint);

        public ISchemaType BaseType
        {
            get
            {
                if (baseFingerprint == -1)
                {
                    return null;
                }
                else
                {
                    return (ISchemaType)BuiltInType.GetSchemaType(baseFingerprint);
                }
            }
        }

        public virtual int PrimitiveType => primitiveFingerprint;

        public virtual ISchemaType KnownBaseType => BaseType;

        public virtual string Description => DisplayName;

        // OK
        // OK
        public virtual int WhitespaceAction
        {
            get
            {
                switch (Fingerprint)
                {
                    case StandardNames.XS_STRING:
                        return Whitespace.PRESERVE;
                    case StandardNames.XS_NORMALIZED_STRING:
                        return Whitespace.REPLACE;
                    default:
                        return Whitespace.COLLAPSE;
                }
            }
        }

        // OK
        // OK
        public virtual ISchemaType BuiltInBaseType
        {
            get
            {
                BuiltInAtomicType @base = this;
                while ((@base != null) && (@base.Fingerprint > 1023))
                {
                    @base = (BuiltInAtomicType)@base.BaseType;
                }

                return @base;
            }
        }

        // OK
        // OK
        public virtual IList<IPlainType> PlainMemberTypes => new List<IPlainType>(1) { this };
        public virtual double DefaultPriority => 0; // upstream BuiltInAtomicType.getDefaultPriority (NumericType overrides with 0.125)
        static BuiltInAtomicType()
        {

            // See bug 2524
            ANY_ATOMIC.stringConverter = StringConverter.StringToString.INSTANCE;
            STRING.stringConverter = StringConverter.StringToString.INSTANCE;
            LANGUAGE.stringConverter = StringConverter.StringToLanguage.INSTANCE;
            NORMALIZED_STRING.stringConverter = StringConverter.StringToNormalizedString.INSTANCE;
            TOKEN.stringConverter = StringConverter.StringToToken.INSTANCE;
            // .NET static-init-order fix: the StringConverter.StringToNCName.TO_* static fields may still be
            // null here — on the CLR (unlike the JVM) StringToNCName can be initialized *before*
            // BuiltInAtomicType (it is `beforefieldinit`), and its own TO_* initializers re-enter
            // BuiltInAtomicType, so this assignment runs while StringToNCName is mid-init and reads null.
            // Construct the converters directly from the (already-assigned) type objects instead of reading
            // TO_*, so NCName/ID/IDREF/ENTITY always get a converter with a non-null target type.
            NCNAME.stringConverter = new StringConverter.StringToNCName(NCNAME);
            NAME.stringConverter = StringConverter.StringToName.INSTANCE;
            NMTOKEN.stringConverter = StringConverter.StringToNMTOKEN.INSTANCE;
            ID.stringConverter = new StringConverter.StringToNCName(ID);
            IDREF.stringConverter = new StringConverter.StringToNCName(IDREF);
            ENTITY.stringConverter = new StringConverter.StringToNCName(ENTITY);
            DECIMAL.stringConverter = StringConverter.StringToDecimal.INSTANCE;
            INTEGER.stringConverter = StringConverter.StringToInteger.INSTANCE;
            DURATION.stringConverter = StringConverter.StringToDuration.INSTANCE;
            G_MONTH.stringConverter = StringConverter.StringToGMonth.INSTANCE;
            G_MONTH_DAY.stringConverter = StringConverter.StringToGMonthDay.INSTANCE;
            G_DAY.stringConverter = StringConverter.StringToGDay.INSTANCE;
            DAY_TIME_DURATION.stringConverter = StringConverter.StringToDayTimeDuration.INSTANCE;
            YEAR_MONTH_DURATION.stringConverter = StringConverter.StringToYearMonthDuration.INSTANCE;
            TIME.stringConverter = StringConverter.StringToTime.INSTANCE;
            BOOLEAN.stringConverter = StringConverter.StringToBoolean.INSTANCE;
            HEX_BINARY.stringConverter = StringConverter.StringToHexBinary.INSTANCE;
            BASE64_BINARY.stringConverter = StringConverter.StringToBase64Binary.INSTANCE;
            UNTYPED_ATOMIC.stringConverter = StringConverter.StringToUntypedAtomic.INSTANCE;
            NON_POSITIVE_INTEGER.stringConverter = new StringToIntegerSubtype(NON_POSITIVE_INTEGER);
            NEGATIVE_INTEGER.stringConverter = new StringToIntegerSubtype(NEGATIVE_INTEGER);
            LONG.stringConverter = new StringToIntegerSubtype(LONG);
            INT.stringConverter = new StringToIntegerSubtype(INT);
            SHORT.stringConverter = new StringToIntegerSubtype(SHORT);
            BYTE.stringConverter = new StringToIntegerSubtype(BYTE);
            NON_NEGATIVE_INTEGER.stringConverter = new StringToIntegerSubtype(NON_NEGATIVE_INTEGER);
            POSITIVE_INTEGER.stringConverter = new StringToIntegerSubtype(POSITIVE_INTEGER);
            UNSIGNED_LONG.stringConverter = new StringToIntegerSubtype(UNSIGNED_LONG);
            UNSIGNED_INT.stringConverter = new StringToIntegerSubtype(UNSIGNED_INT);
            UNSIGNED_SHORT.stringConverter = new StringToIntegerSubtype(UNSIGNED_SHORT);
            UNSIGNED_BYTE.stringConverter = new StringToIntegerSubtype(UNSIGNED_BYTE); // We were getting an IntelliJ warning here about potential class loading deadlock. See bug #2524. Have moved the
            // static initializers here, and removed the dependency on static initialization in StringConverter, which hopefully
            // solves the problem.
        }

        private BuiltInAtomicType(int fingerprint)
        {
            this.fingerprint = fingerprint;
        }

        public static BuiltInAtomicType FromAlphaCode(string code)
        {
            return byAlphaCode.GetOrDefault(code);
        }

        public static bool IsStringLike(ItemType type)
        {
            int fp = type.PrimitiveType;
            return fp == StandardNames.XS_STRING || fp == StandardNames.XS_ANY_URI || fp == StandardNames.XS_UNTYPED_ATOMIC;
        }

        public virtual UType GetUType()
        {
            return uType;
        }

        public virtual bool IsAbstract()
        {
            switch (fingerprint)
            {
                case StandardNames.XS_NOTATION:
                case StandardNames.XS_ANY_ATOMIC_TYPE:
                case StandardNames.XS_NUMERIC:
                case StandardNames.XS_ANY_SIMPLE_TYPE:
                    return true;
                default:
                    return false;
            }
        }

        public virtual bool IsBuiltInType()
        {
            return true;
        }

        public virtual SequenceType One()
        {
            if (_one == null)
            {
                _one = new SequenceType(this, StaticProperty.EXACTLY_ONE);
            }

            return _one;
        }

        public virtual SequenceType ZeroOrOne()
        {
            if (_zeroOrOne == null)
            {
                _zeroOrOne = new SequenceType(this, StaticProperty.ALLOWS_ZERO_OR_ONE);
            }

            return _zeroOrOne;
        }

        public virtual SequenceType OneOrMore()
        {
            if (_oneOrMore == null)
            {
                _oneOrMore = new SequenceType(this, StaticProperty.ALLOWS_ONE_OR_MORE);
            }

            return _oneOrMore;
        }

        public virtual SequenceType ZeroOrMore()
        {
            if (_zeroOrMore == null)
            {
                _zeroOrMore = new SequenceType(this, StaticProperty.ALLOWS_ZERO_OR_MORE);
            }

            return _zeroOrMore;
        }

        public virtual bool IsOrdered(bool optimistic)
        {
            return ordered || (optimistic && (this == DURATION || this == ANY_ATOMIC));
        }

        public virtual string GetSystemId()
        {
            return null;
        }

        public virtual bool IsPrimitiveNumeric()
        {
            switch (Fingerprint)
            {
                case StandardNames.XS_INTEGER:
                case StandardNames.XS_DECIMAL:
                case StandardNames.XS_DOUBLE:
                case StandardNames.XS_FLOAT:
                    return true;
                default:
                    return false;
            }
        }

        public int GetBlock()
        {
            return 0;
        }

        public bool AllowsDerivation(int derivation)
        {
            return true;
        }

        public void SetBaseTypeFingerprint(int baseFingerprint)
        {
            this.baseFingerprint = baseFingerprint;
        }

        public StructuredQName GetStructuredQName()
        {
            return new StructuredQName("xs", NamespaceUri.SCHEMA, StandardNames.GetLocalName(fingerprint));
        }

        public bool IsPrimitiveType()
        {
            return Types.Type.IsPrimitiveAtomicType(fingerprint);
        }

        public bool IsComplexType()
        {
            return false;
        }

        public bool IsAnonymousType()
        {
            return false;
        }

        public virtual bool IsPlainType()
        {
            return true;
        }

        public virtual bool Matches(IItem item, TypeHierarchy th)
        {
            return item is AtomicValue && Types.Type.IsSubType(((AtomicValue)item).GetItemType(), this);
        }

        public virtual BuiltInAtomicType GetPrimitiveItemType()
        {
            if (IsPrimitiveType())
            {
                return this;
            }
            else
            {
                ItemType s = (ItemType)BaseType;
                if (s.IsPlainType())
                {
                    return (BuiltInAtomicType)s.GetPrimitiveItemType();
                }
                else
                {
                    return this;
                }
            }
        }

        public virtual bool IsAllowedInXSD10()
        {
            return Fingerprint != StandardNames.XS_DATE_TIME_STAMP;
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public virtual IAtomicType GetAtomizedItemType()
        {
            return this;
        }

        IPlainType IItemTypeWithSequenceTypeCache.GetAtomizedItemType() => GetAtomizedItemType();

        public virtual bool IsAtomizable(TypeHierarchy th)
        {
            return true;
        }

        public virtual bool IsSameType(ISchemaType other)
        {
            return other.Fingerprint == Fingerprint;
        }

        public virtual void CheckTypeDerivationIsOK(ISchemaType type, int block)
        {
            if (type == AnySimpleType.INSTANCE)
            {
            }
            else if (IsSameType(type))
            {
            }
            else
            {
                ISchemaType @base = BaseType;
                if (@base == null)
                {
                    throw new SchemaException("The type " + Description + " is not validly derived from the type " + type.Description);
                }

                try
                {
                    @base.CheckTypeDerivationIsOK(type, block);
                }
                catch (SchemaException)
                {
                    throw new SchemaException("The type " + Description + " is not validly derived from the type " + type.Description);
                }
            }
        }

        // OK
        // OK
        public bool IsSimpleType()
        {
            return true;
        }

        // OK
        // OK
        public virtual bool IsAtomicType()
        {
            return true;
        }

        // OK
        // OK
        public virtual bool IsIdType()
        {
            return fingerprint == StandardNames.XS_ID;
        }

        // OK
        // OK
        public virtual bool IsIdRefType()
        {
            return fingerprint == StandardNames.XS_IDREF;
        }

        // OK
        // OK
        public virtual bool IsListType()
        {
            return false;
        }

        // OK
        // OK
        public virtual bool IsUnionType()
        {
            return false;
        }

        // OK
        // OK
        public virtual bool IsNamespaceSensitive()
        {
            BuiltInAtomicType @base = this;
            int fp = @base.Fingerprint;
            while (fp > 1023)
            {
                @base = (BuiltInAtomicType)@base.BaseType;
                fp = @base.Fingerprint;
            }

            return fp == StandardNames.XS_QNAME || fp == StandardNames.XS_NOTATION;
        }

        // OK
        // OK
        public virtual ValidationFailure ValidateContent(UnicodeString value, INamespaceResolver nsResolver, ConversionRules rules)
        {
            int f = Fingerprint;
            if (f == StandardNames.XS_STRING || f == StandardNames.XS_ANY_SIMPLE_TYPE || f == StandardNames.XS_UNTYPED_ATOMIC || f == StandardNames.XS_ANY_ATOMIC_TYPE)
            {
                return null;
            }

            StringConverter converter = stringConverter;
            if (converter == null)
            {
                converter = GetStringConverter(rules);
                if (IsNamespaceSensitive())
                {
                    if (nsResolver == null)
                    {
                        throw new NotSupportedException("Cannot validate a QName without a namespace resolver");
                    }

                    converter = (StringConverter)converter.SetNamespaceResolver(nsResolver);
                    IConversionResult result = converter.ConvertString(value);
                    if (result is ValidationFailure)
                    {
                        return (ValidationFailure)result;
                    }

                    if (fingerprint == StandardNames.XS_NOTATION)
                    {
                        NotationValue nv = (NotationValue)result;

                        // This check added in 9.3. The XSLT spec says that this check should not be performed during
                        // validation. However, this appears to be based on an incorrect assumption: see spec bug 6952
                        if (!rules.IsDeclaredNotation(nv.GetNamespaceURI(), nv.LocalName))
                        {
                            return new ValidationFailure("Notation {" + nv.GetNamespaceURI() + "}" + nv.LocalName + " is not declared in the schema");
                        }
                    }

                    return null;
                }
            }

            return converter.Validate(value);
        }

        // OK
        // OK
        public virtual StringConverter GetStringConverter(ConversionRules rules)
        {
            if (stringConverter != null)
            {
                return stringConverter;
            }

            switch (fingerprint)
            {
                case StandardNames.XS_DOUBLE:
                case StandardNames.XS_NUMERIC:
                    return rules.StringToDoubleConverter;
                case StandardNames.XS_FLOAT:
                    return new StringConverter.StringToFloat(rules);
                case StandardNames.XS_DATE_TIME:
                    return new StringConverter.StringToDateTime(rules);
                case StandardNames.XS_DATE_TIME_STAMP:
                    return new StringConverter.StringToDateTimeStamp(rules);
                case StandardNames.XS_DATE:
                    return new StringConverter.StringToDate(rules);
                case StandardNames.XS_G_YEAR:
                    return new StringConverter.StringToGYear(rules);
                case StandardNames.XS_G_YEAR_MONTH:
                    return new StringConverter.StringToGYearMonth(rules);
                case StandardNames.XS_ANY_URI:
                    return new StringConverter.StringToAnyURI(rules);
                case StandardNames.XS_QNAME:
                    return new StringConverter.StringToQName(rules);
                case StandardNames.XS_NOTATION:
                    return new StringConverter.StringToNotation(rules);
                default:
                    throw new InvalidOperationException("No string converter available for " + this);
            }
        }

        // OK
        // OK
        public virtual IAtomicSequence Atomize(NodeInfo node)
        {

            // Fast path for common cases
            UnicodeString stringValue = node.UnicodeStringValue;
            if (stringValue.IsEmpty() && node.IsNilled())
            {
                return AtomicArray.EMPTY_ATOMIC_ARRAY;
            }

            if (fingerprint == StandardNames.XS_STRING)
            {
                return new StringValue(stringValue.Tidy());
            }
            else if (fingerprint == StandardNames.XS_UNTYPED_ATOMIC)
            {
                return StringValue.MakeUntypedAtomic(stringValue);
            }

            StringConverter converter = stringConverter;
            if (converter == null)
            {
                converter = GetStringConverter(node.GetConfiguration().GetConversionRules());
                if (IsNamespaceSensitive())
                {
                    NodeInfo container = node.GetNodeKind() == Types.Type.ELEMENT ? node : node.GetParent();
                    converter = (StringConverter)converter.SetNamespaceResolver(container.AllNamespaces);
                }
            }

            return converter.ConvertString(stringValue).AsAtomic();
        }

        // OK
        // OK
        public virtual IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules)
        {

            // Fast path for common cases
            if (fingerprint == StandardNames.XS_STRING)
            {
                return new StringValue(value.Tidy());
            }
            else if (fingerprint == StandardNames.XS_UNTYPED_ATOMIC)
            {
                return StringValue.MakeUntypedAtomic(value);
            }

            StringConverter converter = GetStringConverter(rules);
            if (IsNamespaceSensitive())
            {
                converter = (StringConverter)converter.SetNamespaceResolver(resolver);
            }

            return converter.ConvertString(value).AsAtomic();
        }

        // OK
        // OK
        public override bool Equals(object obj)
        {
            return obj is BuiltInAtomicType && Fingerprint == ((BuiltInAtomicType)obj).Fingerprint;
        }

        // OK
        // OK
        public override int GetHashCode()
        {
            return Fingerprint;
        }

        // OK
        // OK
        public virtual ValidationFailure Validate(AtomicValue primValue, UnicodeString lexicalValue, ConversionRules rules)
        {
            switch (fingerprint)
            {
                case StandardNames.XS_NUMERIC:
                case StandardNames.XS_STRING:
                case StandardNames.XS_BOOLEAN:
                case StandardNames.XS_DURATION:
                case StandardNames.XS_DATE_TIME:
                case StandardNames.XS_DATE:
                case StandardNames.XS_TIME:
                case StandardNames.XS_G_YEAR_MONTH:
                case StandardNames.XS_G_MONTH:
                case StandardNames.XS_G_MONTH_DAY:
                case StandardNames.XS_G_YEAR:
                case StandardNames.XS_G_DAY:
                case StandardNames.XS_HEX_BINARY:
                case StandardNames.XS_BASE64_BINARY:
                case StandardNames.XS_ANY_URI:
                case StandardNames.XS_QNAME:
                case StandardNames.XS_NOTATION:
                case StandardNames.XS_UNTYPED_ATOMIC:
                case StandardNames.XS_DECIMAL:
                case StandardNames.XS_FLOAT:
                case StandardNames.XS_DOUBLE:
                    return null;
                case StandardNames.XS_INTEGER:
                    if (primValue.GetItemType() == BuiltInAtomicType.DECIMAL)
                    {
                        if (((DecimalValue)primValue).IsWholeNumber())
                        {
                            return null;
                        }
                        else
                        {
                            return new ValidationFailure("xs:decimal value " + primValue.ToShortString() + " cannot be used where xs:integer is required");
                        }
                    }
                    else
                    {
                        return null;
                    }

                case StandardNames.XS_NON_POSITIVE_INTEGER:
                case StandardNames.XS_NEGATIVE_INTEGER:
                case StandardNames.XS_LONG:
                case StandardNames.XS_INT:
                case StandardNames.XS_SHORT:
                case StandardNames.XS_BYTE:
                case StandardNames.XS_NON_NEGATIVE_INTEGER:
                case StandardNames.XS_POSITIVE_INTEGER:
                case StandardNames.XS_UNSIGNED_LONG:
                case StandardNames.XS_UNSIGNED_INT:
                case StandardNames.XS_UNSIGNED_SHORT:
                case StandardNames.XS_UNSIGNED_BYTE:
                    return ((IntegerValue)primValue).ValidateAgainstSubType(this);
                case StandardNames.XS_YEAR_MONTH_DURATION:
                case StandardNames.XS_DAY_TIME_DURATION:
                    return null; // treated as primitive
                case StandardNames.XS_DATE_TIME_STAMP:
                    return ((CalendarValue)primValue).TimezoneInMinutes == CalendarValue.NO_TIMEZONE ? new ValidationFailure("xs:dateTimeStamp value must have a timezone") : null;
                case StandardNames.XS_NORMALIZED_STRING:
                case StandardNames.XS_TOKEN:
                case StandardNames.XS_LANGUAGE:
                case StandardNames.XS_NAME:
                case StandardNames.XS_NMTOKEN:
                case StandardNames.XS_NCNAME:
                case StandardNames.XS_ID:
                case StandardNames.XS_IDREF:
                case StandardNames.XS_ENTITY:
                    return stringConverter.Validate(primValue.UnicodeStringValue);
                default:
                    throw new ArgumentException();
            }
        }

        // OK
        // OK
        public virtual void AnalyzeContentExpression(Expression expression, int kind)
        {
            AnalyzeContentExpression(this, expression, kind);
        }

        // OK
        // OK
        public static void AnalyzeContentExpression(ISimpleType simpleType, Expression expression, int kind)
        {
            if (kind == Types.Type.ELEMENT)
            {
                expression.CheckPermittedContents(simpleType, true); //            // if we are building the content of an element or document, no atomization will take
                //            // place, and therefore the presence of any element or attribute nodes in the content will
                //            // cause a validity error, since only simple content is allowed
                //                throw new XPathException("The content of an element with a simple type must not include any element nodes");
                //            }
                //                throw new XPathException("The content of an element with a simple type must not include any attribute nodes");
                //            }
            }
            else if (kind == Types.Type.ATTRIBUTE)
            {

                // for attributes, do a check only for text nodes and atomic values: anything else gets atomized
                if (expression is ValueOf || expression is Literal)
                {
                    expression.CheckPermittedContents(simpleType, true);
                }
            }
        }

        // OK
        // OK
        private static BuiltInAtomicType MakeAtomicType(int fingerprint, ISimpleType baseType, string code, bool ordered)
        {
            BuiltInAtomicType t = new BuiltInAtomicType(fingerprint);
            t.SetBaseTypeFingerprint(baseType.Fingerprint);
            if (t.IsPrimitiveType())
            {
                t.primitiveFingerprint = fingerprint;
            }
            else
            {
                t.primitiveFingerprint = ((IAtomicType)baseType).PrimitiveType;
            }

            t.uType = UType.FromTypeCode(t.primitiveFingerprint);
            t.ordered = ordered;
            t.alphaCode = code;
            BuiltInType.Register(fingerprint, t);
            byAlphaCode[code] = t;
            return t;
        }

        // OK
        // OK
        public virtual UnicodeString Preprocess(UnicodeString input)
        {
            return input;
        }

        // OK
        // OK
        public virtual UnicodeString Postprocess(UnicodeString input)
        {
            return input;
        }

        // OK
        // OK
        public virtual bool IsNumericType()
        {
            ItemType p = GetPrimitiveItemType();
            return p == NumericType.GetInstance() || p == DECIMAL || p == DOUBLE || p == FLOAT || p == INTEGER;
        }

        // OK
        // OK
        public virtual bool IsDurationType()
        {
            return this == DURATION || this == DAY_TIME_DURATION || this == YEAR_MONTH_DURATION;
        }
        IAtomicType IPlainType.GetPrimitiveItemType() => GetPrimitiveItemType();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual Genre GetGenre() => Genre.ATOMIC; // upstream AtomicType.getGenre() default; was a throwing stub (broke axis type-checks on atomic context)
        public virtual string ExplainMismatch(IItem item, TypeHierarchy th) => null; // upstream default: no extra explanation (diagnostics must not throw)
    }
}