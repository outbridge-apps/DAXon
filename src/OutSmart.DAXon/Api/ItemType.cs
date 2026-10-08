////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Api
{
    public abstract class ItemType
    {

        private static readonly ConversionRules defaultConversionRules = new ConversionRules();

        /// <summary>
        /// ItemType representing the type item(), that @is, any item at all
        /// </summary>
        public static ItemType ANY_ITEM = new AnonymousItemType(AnyItemType.GetInstance());

        /// <summary>
        /// ItemType representing the type function(*), that @is, any function
        /// </summary>
        public static ItemType ANY_FUNCTION = new AnonymousItemType1(AnyFunctionType.GetInstance());

        /// <summary>
        /// ItemType representing the type node(), that @is, any node
        /// </summary>
        public static readonly ItemType ANY_NODE = new AnonymousItemType2(AnyNodeTest.GetInstance());

        /// <summary>
        /// ItemType representing the ATTRIBUTE node() type
        /// </summary>
        public static readonly ItemType ATTRIBUTE_NODE = new AnonymousItemType3(NodeKindTest.ATTRIBUTE);

        /// <summary>
        /// ItemType representing the COMMENT node() type
        /// </summary>
        public static readonly ItemType COMMENT_NODE = new AnonymousItemType4(NodeKindTest.COMMENT);

        /// <summary>
        /// ItemType representing the TEXT node() type
        /// </summary>
        public static readonly ItemType TEXT_NODE = new AnonymousItemType5(NodeKindTest.TEXT);

        /// <summary>
        /// ItemType representing the ELEMENT node() type
        /// </summary>
        public static readonly ItemType ELEMENT_NODE = new AnonymousItemType6(NodeKindTest.ELEMENT);

        /// <summary>
        /// ItemType representing the DOCUMENT node() type
        /// </summary>
        public static readonly ItemType DOCUMENT_NODE = new AnonymousItemType7(NodeKindTest.DOCUMENT);

        /// <summary>
        /// ItemType representing the NAMESPACE node() type
        /// </summary>
        public static readonly ItemType NAMESPACE_NODE = new AnonymousItemType8(NodeKindTest.NAMESPACE);

        /// <summary>
        /// ItemType representing the PROCESSING_INSTRUCTION node() type
        /// </summary>
        public static readonly ItemType PROCESSING_INSTRUCTION_NODE = new AnonymousItemType9(NodeKindTest.PROCESSING_INSTRUCTION);

        /// <summary>
        /// ItemType representing the type map(*), that @is, any map
        /// </summary>
        public static readonly ItemType ANY_MAP = new AnonymousItemType10(MapType.ANY_MAP_TYPE);

        /// <summary>
        /// ItemType representing the type array(*), that @is, any array
        /// </summary>
        public static readonly ItemType ANY_ARRAY = new AnonymousItemType11(ArrayItemType.ANY_ARRAY_TYPE);

        /// <summary>
        /// ItemType representing the type xs:anyAtomicType, that @is, any atomic value
        /// </summary>
        public static readonly ItemType ANY_ATOMIC_VALUE = Atomic(BuiltInAtomicType.ANY_ATOMIC, defaultConversionRules);
        /// <summary>
        /// ItemType representing the type xs:error: a type with no instances
        /// </summary>
        public static readonly ItemType ERROR = new AnonymousItemType12(ErrorType.GetInstance());

        /// <summary>
        /// ItemType representing the primitive type xs:string
        /// </summary>
        public static readonly ItemType STRING = Atomic(BuiltInAtomicType.STRING, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:boolean
        /// </summary>
        public static readonly ItemType BOOLEAN = Atomic(BuiltInAtomicType.BOOLEAN, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:duration
        /// </summary>
        public static readonly ItemType DURATION = Atomic(BuiltInAtomicType.DURATION, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:dateTime
        /// </summary>
        public static readonly ItemType DATE_TIME = Atomic(BuiltInAtomicType.DATE_TIME, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:date
        /// </summary>
        public static readonly ItemType DATE = Atomic(BuiltInAtomicType.DATE, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:time
        /// </summary>
        public static readonly ItemType TIME = Atomic(BuiltInAtomicType.TIME, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:gYearMonth
        /// </summary>
        public static readonly ItemType G_YEAR_MONTH = Atomic(BuiltInAtomicType.G_YEAR_MONTH, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:gMonth
        /// </summary>
        public static readonly ItemType G_MONTH = Atomic(BuiltInAtomicType.G_MONTH, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:gMonthDay
        /// </summary>
        public static readonly ItemType G_MONTH_DAY = Atomic(BuiltInAtomicType.G_MONTH_DAY, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:gYear
        /// </summary>
        public static readonly ItemType G_YEAR = Atomic(BuiltInAtomicType.G_YEAR, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:gDay
        /// </summary>
        public static readonly ItemType G_DAY = Atomic(BuiltInAtomicType.G_DAY, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:hexBinary
        /// </summary>
        public static readonly ItemType HEX_BINARY = Atomic(BuiltInAtomicType.HEX_BINARY, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:base64Binary
        /// </summary>
        public static readonly ItemType BASE64_BINARY = Atomic(BuiltInAtomicType.BASE64_BINARY, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:anyURI
        /// </summary>
        public static readonly ItemType ANY_URI = Atomic(BuiltInAtomicType.ANY_URI, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:QName
        /// </summary>
        public static readonly ItemType QNAME = Atomic(BuiltInAtomicType.QNAME, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:NOTATION
        /// </summary>
        public static readonly ItemType NOTATION = Atomic(BuiltInAtomicType.NOTATION, defaultConversionRules);
        /// <summary>
        /// ItemType representing the XPath-defined type xs:untypedAtomic
        /// </summary>
        public static readonly ItemType UNTYPED_ATOMIC = Atomic(BuiltInAtomicType.UNTYPED_ATOMIC, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:decimal
        /// </summary>
        public static readonly ItemType DECIMAL = Atomic(BuiltInAtomicType.DECIMAL, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:float
        /// </summary>
        public static readonly ItemType FLOAT = Atomic(BuiltInAtomicType.FLOAT, defaultConversionRules);
        /// <summary>
        /// ItemType representing the primitive type xs:double
        /// </summary>
        public static readonly ItemType DOUBLE = Atomic(BuiltInAtomicType.DOUBLE, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:integer
        /// </summary>
        public static readonly ItemType INTEGER = Atomic(BuiltInAtomicType.INTEGER, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:nonPositiveInteger
        /// </summary>
        public static readonly ItemType NON_POSITIVE_INTEGER = Atomic(BuiltInAtomicType.NON_POSITIVE_INTEGER, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:negativeInteger
        /// </summary>
        public static readonly ItemType NEGATIVE_INTEGER = Atomic(BuiltInAtomicType.NEGATIVE_INTEGER, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:long
        /// </summary>
        public static readonly ItemType LONG = Atomic(BuiltInAtomicType.LONG, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:int
        /// </summary>
        public static readonly ItemType INT = Atomic(BuiltInAtomicType.INT, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:short
        /// </summary>
        public static readonly ItemType SHORT = Atomic(BuiltInAtomicType.SHORT, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:byte
        /// </summary>
        public static readonly ItemType BYTE = Atomic(BuiltInAtomicType.BYTE, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:nonNegativeInteger
        /// </summary>
        public static readonly ItemType NON_NEGATIVE_INTEGER = Atomic(BuiltInAtomicType.NON_NEGATIVE_INTEGER, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:positiveInteger
        /// </summary>
        public static readonly ItemType POSITIVE_INTEGER = Atomic(BuiltInAtomicType.POSITIVE_INTEGER, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:unsignedLong
        /// </summary>
        public static readonly ItemType UNSIGNED_LONG = Atomic(BuiltInAtomicType.UNSIGNED_LONG, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:unsignedInt
        /// </summary>
        public static readonly ItemType UNSIGNED_INT = Atomic(BuiltInAtomicType.UNSIGNED_INT, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:unsignedShort
        /// </summary>
        public static readonly ItemType UNSIGNED_SHORT = Atomic(BuiltInAtomicType.UNSIGNED_SHORT, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:unsignedByte
        /// </summary>
        public static readonly ItemType UNSIGNED_BYTE = Atomic(BuiltInAtomicType.UNSIGNED_BYTE, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:yearMonthDuration
        /// </summary>
        public static readonly ItemType YEAR_MONTH_DURATION = Atomic(BuiltInAtomicType.YEAR_MONTH_DURATION, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:dayTimeDuration
        /// </summary>
        public static readonly ItemType DAY_TIME_DURATION = Atomic(BuiltInAtomicType.DAY_TIME_DURATION, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:normalizedString
        /// </summary>
        public static readonly ItemType NORMALIZED_STRING = Atomic(BuiltInAtomicType.NORMALIZED_STRING, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:token
        /// </summary>
        public static readonly ItemType TOKEN = Atomic(BuiltInAtomicType.TOKEN, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:language
        /// </summary>
        public static readonly ItemType LANGUAGE = Atomic(BuiltInAtomicType.LANGUAGE, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:Name
        /// </summary>
        public static readonly ItemType NAME = Atomic(BuiltInAtomicType.NAME, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:NMTOKEN
        /// </summary>
        public static readonly ItemType NMTOKEN = Atomic(BuiltInAtomicType.NMTOKEN, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:NCName
        /// </summary>
        public static readonly ItemType NCNAME = Atomic(BuiltInAtomicType.NCNAME, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:ID
        /// </summary>
        public static readonly ItemType ID = Atomic(BuiltInAtomicType.ID, defaultConversionRules);
        /// <summary>
        /// ItemType representing the built-in (but non-primitive) type xs:IDREF
        /// </summary>
        public static readonly ItemType IDREF = Atomic(BuiltInAtomicType.IDREF, defaultConversionRules);
        public static readonly ItemType ENTITY = Atomic(BuiltInAtomicType.ENTITY, defaultConversionRules);
        public static readonly ItemType DATE_TIME_STAMP = Atomic(BuiltInAtomicType.DATE_TIME_STAMP, defaultConversionRules);

        public static readonly ItemType NUMERIC = new AnonymousItemType13(NumericType.GetInstance());
        protected readonly Types.ItemType underlyingType;
        public virtual Types.ItemType UnderlyingItemType => underlyingType;

        public virtual QName TypeName
        {
            get
            {
                Types.ItemType type = UnderlyingItemType;
                if (type is ISchemaType)
                {
                    StructuredQName name = ((ISchemaType)type).GetStructuredQName();
                    return name == null ? null : new QName(name);
                }
                else
                {
                    return null;
                }
            }
        }
        static ItemType()
        {
            defaultConversionRules.StringToDoubleConverter = StringToDouble.GetInstance();
            defaultConversionRules.SetNotationSet(null);
            defaultConversionRules.SetURIChecker(StandardURIChecker.GetInstance());
        }
        public ItemType(Types.ItemType underlyingType)
        {
            this.underlyingType = underlyingType;
        }

        public virtual SequenceType With(OccurrenceIndicator occurrenceIndicator)
        {
            return SequenceType.MakeSequenceType(this, occurrenceIndicator);
        }

        public virtual SequenceType One()
        {
            return SequenceType.MakeSequenceType(this, OccurrenceIndicator.ONE);
        }

        public virtual SequenceType OneOrMore()
        {
            return SequenceType.MakeSequenceType(this, OccurrenceIndicator.ONE_OR_MORE);
        }

        public virtual SequenceType ZeroOrMore()
        {
            return SequenceType.MakeSequenceType(this, OccurrenceIndicator.ZERO_OR_MORE);
        }

        public virtual SequenceType ZeroOrOne()
        {
            return SequenceType.MakeSequenceType(this, OccurrenceIndicator.ZERO_OR_ONE);
        }
        private static ItemType Atomic(BuiltInAtomicType underlyingType, ConversionRules conversionRules)
        {
            return new BuiltInAtomicItemType(underlyingType, conversionRules);
        }

        public virtual ConversionRules GetConversionRules()
        {
            return defaultConversionRules;
        }

        public virtual bool Test(XdmItem item)
        {
            return Matches(item);
        }

        public abstract bool Matches(XdmItem item);
        public abstract bool Subsumes(ItemType other);

        public override bool Equals(object other)
        {
            return other is ItemType && UnderlyingItemType.Equals(((ItemType)other).UnderlyingItemType);
        }

        public override int GetHashCode()
        {
            return UnderlyingItemType.GetHashCode();
        }

        public override string ToString()
        {
            Types.ItemType type = UnderlyingItemType;
            if (type is ISchemaType)
            {
                string marker = "";
                ISchemaType st = (ISchemaType)type;
                StructuredQName name;
                while (true)
                {
                    name = st.GetStructuredQName();
                    if (name != null)
                    {
                        return marker + name.EQName;
                    }
                    else
                    {
                        marker = "<";
                        st = st.BaseType;
                        if (st == null)
                        {
                            return "Q{" + NamespaceConstant.SCHEMA + "}anyType";
                        }
                    }
                }
            }
            else
            {
                return type.ToString();
            }
        }
        private sealed class AnonymousItemType : ItemType
        {

            public AnonymousItemType(Types.ItemType parent) : base(parent)
            {
            }
            public override ConversionRules GetConversionRules()
            {
                return defaultConversionRules;
            }

            public override bool Matches(XdmItem item)
            {
                return true;
            }

            public override bool Subsumes(ItemType other)
            {
                return true;
            }
        }
        private sealed class AnonymousItemType1 : ItemType
        {

            public AnonymousItemType1(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                return item.UnderlyingValue is IFunctionItem;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType is IFunctionItemType;
            }
        }
        private sealed class AnonymousItemType2 : ItemType
        {

            public AnonymousItemType2(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                return item.UnderlyingValue is NodeInfo;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType is NodeTest;
            }
        }
        private sealed class AnonymousItemType3 : ItemType
        {

            public AnonymousItemType3(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                IItem it = item.UnderlyingValue;
                return it is NodeInfo && ((NodeInfo)it).GetNodeKind() == Types.Type.ATTRIBUTE;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType.GetUType() == UType.ATTRIBUTE;
            }
        }
        private sealed class AnonymousItemType4 : ItemType
        {

            public AnonymousItemType4(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                IItem it = item.UnderlyingValue;
                return it is NodeInfo && ((NodeInfo)it).GetNodeKind() == Types.Type.COMMENT;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType.GetUType() == UType.COMMENT;
            }
        }
        private sealed class AnonymousItemType5 : ItemType
        {

            public AnonymousItemType5(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                IItem it = item.UnderlyingValue;
                return it is NodeInfo && ((NodeInfo)it).GetNodeKind() == Types.Type.TEXT;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType.GetUType() == UType.TEXT;
            }
        }
        private sealed class AnonymousItemType6 : ItemType
        {

            public AnonymousItemType6(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                IItem it = item.UnderlyingValue;
                return it is NodeInfo && ((NodeInfo)it).GetNodeKind() == Types.Type.ELEMENT;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType.GetUType() == UType.ELEMENT;
            }
        }
        private sealed class AnonymousItemType7 : ItemType
        {

            public AnonymousItemType7(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                IItem it = item.UnderlyingValue;
                return it is NodeInfo && ((NodeInfo)it).GetNodeKind() == Types.Type.DOCUMENT;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType.GetUType() == UType.DOCUMENT;
            }
        }
        private sealed class AnonymousItemType8 : ItemType
        {

            public AnonymousItemType8(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                IItem it = item.UnderlyingValue;
                return it is NodeInfo && ((NodeInfo)it).GetNodeKind() == Types.Type.NAMESPACE;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType.GetUType() == UType.NAMESPACE;
            }
        }
        private sealed class AnonymousItemType9 : ItemType
        {

            public AnonymousItemType9(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                IItem it = item.UnderlyingValue;
                return it is NodeInfo && ((NodeInfo)it).GetNodeKind() == Types.Type.PROCESSING_INSTRUCTION;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType.GetUType() == UType.PI;
            }
        }
        private sealed class AnonymousItemType10 : ItemType
        {

            public AnonymousItemType10(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                return item.UnderlyingValue is MapItem;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType is MapType;
            }
        }
        private sealed class AnonymousItemType11 : ItemType
        {

            public AnonymousItemType11(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                return item.UnderlyingValue is ArrayItem;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType is ArrayItemType;
            }
        }
        private sealed class AnonymousItemType12 : ItemType
        {

            public AnonymousItemType12(Types.ItemType parent) : base(parent)
            {
            }
            public override bool Matches(XdmItem item)
            {
                return false;
            }

            public override bool Subsumes(ItemType other)
            {
                return other.UnderlyingItemType is ErrorType;
            }
        }

        /// <summary>
        /// ItemType representing a built-in atomic type
        /// </summary>
        protected class BuiltInAtomicItemType : ItemType
        {
            private readonly ConversionRules conversionRules;

            public override Types.ItemType UnderlyingItemType => underlyingType;
            public BuiltInAtomicItemType(BuiltInAtomicType underlyingType, ConversionRules conversionRules) : base(underlyingType)
            {
                this.conversionRules = conversionRules;
            }

            public static BuiltInAtomicItemType MakeVariant(BuiltInAtomicItemType type, ConversionRules conversionRules)
            {
                return new BuiltInAtomicItemType((BuiltInAtomicType)type.underlyingType, conversionRules);
            }

            public override ConversionRules GetConversionRules()
            {
                return conversionRules;
            }

            public override bool Matches(XdmItem item)
            {
                IItem value = item.UnderlyingValue;
                if (!(value is AtomicValue))
                {
                    return false;
                }

                IAtomicType type = ((AtomicValue)value).GetItemType();
                return SubsumesUnderlyingType(type);
            }

            public override bool Subsumes(ItemType other)
            {
                Types.ItemType otherType = other.UnderlyingItemType;
                if (!otherType.IsPlainType())
                {
                    return false;
                }

                IAtomicType type = (IAtomicType)otherType;
                return SubsumesUnderlyingType(type);
            }

            private bool SubsumesUnderlyingType(IAtomicType type)
            {
                BuiltInAtomicType builtIn = type is BuiltInAtomicType ? (BuiltInAtomicType)type : (BuiltInAtomicType)type.BuiltInBaseType;
                while (true)
                {
                    if (builtIn.IsSameType((IAtomicType)underlyingType))
                    {
                        return true;
                    }

                    ISchemaType @base = builtIn.BaseType;
                    if (!(@base is BuiltInAtomicType))
                    {
                        return false;
                    }

                    builtIn = (BuiltInAtomicType)@base;
                }
            }

            public override string ToString()
            {
                return "xs:" + ((BuiltInAtomicType)underlyingType).GetStructuredQName().GetLocalPart();
            }
        }
        private sealed class AnonymousItemType13 : ItemType
        {

            public AnonymousItemType13(Types.ItemType parent) : base(parent)
            {
            }
            public override ConversionRules GetConversionRules()
            {
                return defaultConversionRules;
            }

            public override bool Matches(XdmItem item)
            {
                return item.UnderlyingValue is NumericValue;
            }

            public override bool Subsumes(ItemType other)
            {
                return DECIMAL.Subsumes(other) || DOUBLE.Subsumes(other) || FLOAT.Subsumes(other);
            }
        }
    }
}
