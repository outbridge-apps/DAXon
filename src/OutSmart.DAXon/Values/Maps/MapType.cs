////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using System;
using System.Text;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Values.Maps
{
    internal sealed class MapType : AnyFunctionType
    {
        public static readonly MapType ANY_MAP_TYPE = new MapType(BuiltInAtomicType.ANY_ATOMIC, SequenceType.ANY_SEQUENCE);
        public static readonly MapType EMPTY_MAP_TYPE = new MapType(BuiltInAtomicType.ANY_ATOMIC, SequenceType.ANY_SEQUENCE, true);
        public static readonly SequenceType OPTIONAL_MAP_ITEM = SequenceType.MakeSequenceType(ANY_MAP_TYPE, StaticProperty.ALLOWS_ZERO_OR_ONE);
        private readonly IPlainType keyType;
        private readonly SequenceType valueType;
        private readonly bool mustBeEmpty;

        public IPlainType KeyType => keyType;

        public SequenceType ValueType => valueType;

        public override string BasicAlphaCode => "FM";

        public override double DefaultPriority => keyType.GetNormalizedDefaultPriority() * valueType.PrimaryType.GetNormalizedDefaultPriority();

        public override SequenceType[] ArgumentTypes => new SequenceType[]
            {
                SequenceType.MakeSequenceType(BuiltInAtomicType.ANY_ATOMIC, StaticProperty.EXACTLY_ONE)
            };

        public override SequenceType ResultType
        {
            get
            {

                // a function call on this map can always return ()
                if (Cardinality.AllowsZero(valueType.GetCardinality()))
                {
                    return valueType;
                }
                else
                {
                    return SequenceType.MakeSequenceType(valueType.PrimaryType, Cardinality.Union(valueType.GetCardinality(), StaticProperty.ALLOWS_ZERO));
                }
            }
        }
        public MapType(IPlainType keyType, SequenceType valueType)
        {
            this.keyType = keyType;
            this.valueType = valueType;
            this.mustBeEmpty = false;
        }

        public MapType(IAtomicType keyType, SequenceType valueType, bool mustBeEmpty)
        {
            this.keyType = keyType;
            this.valueType = valueType;
            this.mustBeEmpty = mustBeEmpty;
        }

        public override Genre GetGenre()
        {
            return Genre.MAP;
        }

        public override bool IsMapType()
        {
            return true;
        }

        public override bool IsArrayType()
        {
            return false;
        }

        public override bool IsAtomizable(TypeHierarchy th)
        {
            return false;
        }

        public override bool Matches(IItem item, TypeHierarchy th)
        {
            if (!(item is MapItem))
            {
                return false;
            }

            if (((MapItem)item).IsEmpty())
            {
                return true;
            }
            else if (mustBeEmpty)
            {
                return false;
            }

            if (this == ANY_MAP_TYPE)
            {
                return true;
            }
            else
            {
                return ((MapItem)item).Conforms(keyType, valueType, th);
            }
        }

        public override string ToString()
        {
            if (this == ANY_MAP_TYPE)
            {
                return "map(*)";
            }
            else if (this == EMPTY_MAP_TYPE)
            {
                return "map{}";
            }
            else
            {
                StringBuilder sb = new StringBuilder(100);
                sb.Append("map(");
                sb.Append(keyType.ToString());
                sb.Append(", ");
                sb.Append(valueType.ToString());
                sb.Append(')');
                return sb.ToString();
            }
        }

        /// <summary>
        /// Test whether this function type equals another function type
        /// </summary>
        public override bool Equals(object other)
        {
            if (this == other)
            {
                return true;
            }

            if (other is MapType)
            {
                MapType f2 = (MapType)other;
                return keyType.Equals(f2.keyType) && valueType.Equals(f2.valueType) && mustBeEmpty == f2.mustBeEmpty;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return keyType.GetHashCode() ^ valueType.GetHashCode();
        }

        public override Affinity Relationship(IFunctionItemType other, TypeHierarchy th)
        {
            if (other == AnyFunctionType.GetInstance())
            {
                return Affinity.SUBSUMED_BY;
            }
            else if (Equals(other))
            {
                return Affinity.SAME_TYPE;
            }
            else if (other == MapType.ANY_MAP_TYPE)
            {
                return Affinity.SUBSUMED_BY;
            }
            else if (other.IsArrayType())
            {
                return Affinity.DISJOINT;
            }
            else if (other is RecordTest)
            {
                return TypeHierarchy.InverseRelationship(other.Relationship(this, th));
            }
            else if (other is MapType)
            {

                // See bug 3720. Two map types can never be disjoint because the empty
                // map is an instance of every map type
                MapType f2 = (MapType)other;
                Affinity keyRel = th.Relationship(keyType, f2.keyType);
                if (keyRel == Affinity.DISJOINT)
                {
                    return Affinity.OVERLAPS;
                }

                Affinity valueRel = th.SequenceTypeRelationship(valueType, f2.valueType);
                if (valueRel == Affinity.DISJOINT)
                {
                    return Affinity.OVERLAPS;
                }

                if (keyRel == valueRel)
                {
                    return keyRel;
                }

                if ((keyRel == Affinity.SAME_TYPE || keyRel == Affinity.SUBSUMES) && (valueRel == Affinity.SAME_TYPE || valueRel == Affinity.SUBSUMES))
                {
                    return Affinity.SUBSUMES;
                }

                if ((keyRel == Affinity.SAME_TYPE || keyRel == Affinity.SUBSUMED_BY) && (valueRel == Affinity.SAME_TYPE || valueRel == Affinity.SUBSUMED_BY))
                {
                    return Affinity.SUBSUMED_BY;
                }

                return Affinity.OVERLAPS;
            }
            else
            {

                // see Bug #4692
                SequenceType st = ResultType;
                if (!Cardinality.AllowsZero(st.GetCardinality()))
                {
                    st = SequenceType.MakeSequenceType(st.PrimaryType, Cardinality.Union(st.GetCardinality(), StaticProperty.ALLOWS_ZERO));
                }

                return new SpecificFunctionType(new SequenceType[] { SequenceType.ATOMIC_SEQUENCE }, st).Relationship(other, th);
            }
        }

        public override Expression MakeFunctionSequenceCoercer(Expression exp, Func<RoleDiagnostic> role, bool allow40)
        {
            return new SpecificFunctionType(ArgumentTypes, ResultType).MakeFunctionSequenceCoercer(exp, role, false);
        }
    }
}
