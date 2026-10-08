////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2013-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Types
{
    public class UType
    {
        // Enum.GetValues reflects and allocates per call; compile-time node tests decompose constantly
        private static readonly PrimitiveUType[] AllPrimitives = (PrimitiveUType[])Enum.GetValues(typeof(PrimitiveUType));
        public static readonly UType VOID = new UType(0);
        public static readonly UType DOCUMENT = PrimitiveUType.DOCUMENT.ToUType();
        public static readonly UType ELEMENT = PrimitiveUType.ELEMENT.ToUType();
        public static readonly UType ATTRIBUTE = PrimitiveUType.ATTRIBUTE.ToUType();
        public static readonly UType TEXT = PrimitiveUType.TEXT.ToUType();
        public static readonly UType COMMENT = PrimitiveUType.COMMENT.ToUType();
        public static readonly UType PI = PrimitiveUType.PI.ToUType();
        public static readonly UType NAMESPACE = PrimitiveUType.NAMESPACE.ToUType();
        public static readonly UType FUNCTION = PrimitiveUType.FUNCTION.ToUType();
        public static readonly UType STRING = PrimitiveUType.STRING.ToUType();
        public static readonly UType BOOLEAN = PrimitiveUType.BOOLEAN.ToUType();
        public static readonly UType DECIMAL = PrimitiveUType.DECIMAL.ToUType();
        public static readonly UType FLOAT = PrimitiveUType.FLOAT.ToUType();
        public static readonly UType DOUBLE = PrimitiveUType.DOUBLE.ToUType();
        public static readonly UType DURATION = PrimitiveUType.DURATION.ToUType();
        public static readonly UType DATE_TIME = PrimitiveUType.DATE_TIME.ToUType();
        public static readonly UType TIME = PrimitiveUType.TIME.ToUType();
        public static readonly UType DATE = PrimitiveUType.DATE.ToUType();
        public static readonly UType G_YEAR_MONTH = PrimitiveUType.G_YEAR_MONTH.ToUType();
        public static readonly UType G_YEAR = PrimitiveUType.G_YEAR.ToUType();
        public static readonly UType G_MONTH_DAY = PrimitiveUType.G_MONTH_DAY.ToUType();
        public static readonly UType G_DAY = PrimitiveUType.G_DAY.ToUType();
        public static readonly UType G_MONTH = PrimitiveUType.G_MONTH.ToUType();
        public static readonly UType HEX_BINARY = PrimitiveUType.HEX_BINARY.ToUType();
        public static readonly UType BASE64_BINARY = PrimitiveUType.BASE64_BINARY.ToUType();
        public static readonly UType ANY_URI = PrimitiveUType.ANY_URI.ToUType();
        public static readonly UType QNAME = PrimitiveUType.QNAME.ToUType();
        public static readonly UType NOTATION = PrimitiveUType.NOTATION.ToUType();
        public static readonly UType UNTYPED_ATOMIC = PrimitiveUType.UNTYPED_ATOMIC.ToUType();
        public static readonly UType EXTENSION = PrimitiveUType.EXTENSION.ToUType();
        public static readonly UType NUMERIC = DOUBLE.Union(FLOAT).Union(DECIMAL);
        public static readonly UType STRING_LIKE = STRING.Union(ANY_URI).Union(UNTYPED_ATOMIC);
        public static readonly UType CHILD_NODE_KINDS = ELEMENT.Union(TEXT).Union(COMMENT).Union(PI);
        public static readonly UType PARENT_NODE_KINDS = DOCUMENT.Union(ELEMENT);
        public static readonly UType ELEMENT_OR_ATTRIBUTE = ELEMENT.Union(ATTRIBUTE);
        public static readonly UType ANY_NODE = CHILD_NODE_KINDS.Union(DOCUMENT).Union(ATTRIBUTE).Union(NAMESPACE);
        public static readonly UType ANY_ATOMIC = new UType(0x0FFFFF00);
        public static readonly UType ANY = ANY_NODE.Union(ANY_ATOMIC).Union(FUNCTION).Union(EXTENSION);
        private readonly int bits;
        public UType(int bits)
        {
            this.bits = bits;
        }

        public override int GetHashCode()
        {
            return bits;
        }

        public override bool Equals(object obj)
        {
            return obj is UType && bits == ((UType)obj).bits;
        }

        public virtual UType Union(UType other)
        {
            if (other == null)
            {
                new NullReferenceException().ToString();
            }

            // Steady-state unions (per-entry map type bookkeeping) are almost always subsets:
            // return an existing instance instead of allocating an equal one.
            int merged = bits | other.bits;
            if (merged == bits)
            {
                return this;
            }

            if (merged == other.bits)
            {
                return other;
            }

            return new UType(merged);
        }

        public virtual UType Intersection(UType other)
        {
            return new UType(bits & other.bits);
        }

        public virtual UType Except(UType other)
        {
            return new UType(bits & ~other.bits);
        }

        public static UType FromTypeCode(int code)
        {
            switch (code)
            {
                case Types.Type.NODE:
                    return ANY_NODE;
                case Types.Type.ELEMENT:
                    return ELEMENT;
                case Types.Type.ATTRIBUTE:
                    return ATTRIBUTE;
                case Types.Type.TEXT:
                case Types.Type.WHITESPACE_TEXT:
                    return TEXT;
                case Types.Type.DOCUMENT:
                    return DOCUMENT;
                case Types.Type.COMMENT:
                    return COMMENT;
                case Types.Type.PROCESSING_INSTRUCTION:
                    return PI;
                case Types.Type.NAMESPACE:
                    return NAMESPACE;
                case Types.Type.FUNCTION:
                    return FUNCTION;
                case Types.Type.ITEM:
                    return ANY;
                case StandardNames.XS_ANY_ATOMIC_TYPE:
                    return ANY_ATOMIC;
                case StandardNames.XS_NUMERIC:
                    return NUMERIC;
                case StandardNames.XS_STRING:
                    return STRING;
                case StandardNames.XS_BOOLEAN:
                    return BOOLEAN;
                case StandardNames.XS_DURATION:
                    return DURATION;
                case StandardNames.XS_DATE_TIME:
                    return DATE_TIME;
                case StandardNames.XS_DATE:
                    return DATE;
                case StandardNames.XS_TIME:
                    return TIME;
                case StandardNames.XS_G_YEAR_MONTH:
                    return G_YEAR_MONTH;
                case StandardNames.XS_G_MONTH:
                    return G_MONTH;
                case StandardNames.XS_G_MONTH_DAY:
                    return G_MONTH_DAY;
                case StandardNames.XS_G_YEAR:
                    return G_YEAR;
                case StandardNames.XS_G_DAY:
                    return G_DAY;
                case StandardNames.XS_HEX_BINARY:
                    return HEX_BINARY;
                case StandardNames.XS_BASE64_BINARY:
                    return BASE64_BINARY;
                case StandardNames.XS_ANY_URI:
                    return ANY_URI;
                case StandardNames.XS_QNAME:
                    return QNAME;
                case StandardNames.XS_NOTATION:
                    return NOTATION;
                case StandardNames.XS_UNTYPED_ATOMIC:
                    return UNTYPED_ATOMIC;
                case StandardNames.XS_DECIMAL:
                    return DECIMAL;
                case StandardNames.XS_FLOAT:
                    return FLOAT;
                case StandardNames.XS_DOUBLE:
                    return DOUBLE;
                case StandardNames.XS_INTEGER:
                    return DECIMAL;
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
                    return DECIMAL;
                case StandardNames.XS_YEAR_MONTH_DURATION:
                case StandardNames.XS_DAY_TIME_DURATION:
                    return DURATION;
                case StandardNames.XS_DATE_TIME_STAMP:
                    return DATE_TIME;
                case StandardNames.XS_NORMALIZED_STRING:
                case StandardNames.XS_TOKEN:
                case StandardNames.XS_LANGUAGE:
                case StandardNames.XS_NAME:
                case StandardNames.XS_NMTOKEN:
                case StandardNames.XS_NCNAME:
                case StandardNames.XS_ID:
                case StandardNames.XS_IDREF:
                case StandardNames.XS_ENTITY:
                    return STRING;
                default:
                    throw new ArgumentException("" + code);
            }
        }

        public virtual HashSet<PrimitiveUType> Decompose()
        {
            HashSet<PrimitiveUType> result = new HashSet<PrimitiveUType>();
            foreach (PrimitiveUType p in AllPrimitives)
            {
                if ((bits & (1 << p.GetBit())) != 0)
                {
                    result.Add(p);
                }
            }

            return result;
        }

        public override string ToString()
        {
            HashSet<PrimitiveUType> components = Decompose();
            if (components.Count == 0)
            {
                return "U{}";
            }

            StringBuilder sb = new StringBuilder(256);
            bool started = false;
            foreach (PrimitiveUType component in components)
            {
                if (started)
                {
                    sb.Append('|');
                }

                started = true;
                sb.Append(component.ToString());
            }

            return sb.ToString();
        }

        public virtual string ToStringWithIndefiniteArticle()
        {
            return Err.IndefiniteArticleFor(ToString(), false) + " " + this + " node";
        }

        public virtual bool Overlaps(UType other)
        {
            return (bits & other.bits) != 0;
        }

        public virtual bool Subsumes(UType other)
        {
            return (bits & other.bits) == other.bits;
        }

        public virtual ItemType ToItemType()
        {
            HashSet<PrimitiveUType> p = Decompose();
            if (p.Count == 0)
            {
                return ErrorType.GetInstance();
            }
            else if (p.Count == 1)
            {
                return p.First().ToItemType();
            }
            else if (ANY_NODE.Subsumes(this))
            {
                return AnyNodeTest.GetInstance();
            }
            else if (Equals(NUMERIC))
            {
                return NumericType.GetInstance();
            }
            else if (ANY_ATOMIC.Subsumes(this))
            {
                return BuiltInAtomicType.ANY_ATOMIC;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        public virtual bool Matches(IItem item)
        {
            return Subsumes(GetUType(item));
        }

        public static UType GetUType(IItem item)
        {
            if (item is NodeInfo)
            {
                return FromTypeCode(((NodeInfo)item).GetNodeKind());
            }
            else if (item is AtomicValue)
            {
                return ((AtomicValue)item).GetUType();
            }
            else if (item is IFunctionItem)
            {
                return UType.FUNCTION;
            }
            else if (item.GetGenre() == Genre.EXTERNAL)
            {
                return UType.EXTENSION;
            }
            else
            {
                return UType.VOID;
            }
        }

        public static UType GetUType(IGroundedValue sequence)
        {
            ISequenceIterator iter = sequence.Iterate();
            UType u = UType.VOID;
            for (IItem item; (item = iter.Next()) != null;)
            {
                u = u.Union(GetUType(item));
            }

            return u;
        }

        public static bool IsPossiblyComparable(UType t1, UType t2, bool ordered)
        {
            if (t1 == t2)
            {
                return true; // short cut
            }

            if (t1 == UType.ANY_ATOMIC || t2 == UType.ANY_ATOMIC)
            {
                return true; // meaning we don't actually know at this stage
            }

            if (t1 == UType.UNTYPED_ATOMIC || t1 == UType.ANY_URI)
            {
                t1 = UType.STRING;
            }

            if (t2 == UType.UNTYPED_ATOMIC || t2 == UType.ANY_URI)
            {
                t2 = UType.STRING;
            }

            if (NUMERIC.Subsumes(t1))
            {
                t1 = NUMERIC;
            }

            if (NUMERIC.Subsumes(t2))
            {
                t2 = NUMERIC;
            }

            return t1 == t2;
        }

        public static bool IsGuaranteedComparable(UType t1, UType t2)
        {
            if (t1 == t2)
            {
                return true; // short cut
            }

            if (t1 == UType.UNTYPED_ATOMIC || t1 == UType.ANY_URI)
            {
                t1 = UType.STRING;
            }

            if (t2 == UType.UNTYPED_ATOMIC || t2 == UType.ANY_URI)
            {
                t2 = UType.STRING;
            }

            if (NUMERIC.Subsumes(t1))
            {
                t1 = NUMERIC;
            }

            if (NUMERIC.Subsumes(t2))
            {
                t2 = NUMERIC;
            }

            return t1.Equals(t2);
        }

        public static bool IsGenerallyComparable(UType t1, UType t2)
        {
            return t1 == UType.UNTYPED_ATOMIC || t2 == UType.UNTYPED_ATOMIC || IsGuaranteedComparable(t1, t2);
        }
    }
}