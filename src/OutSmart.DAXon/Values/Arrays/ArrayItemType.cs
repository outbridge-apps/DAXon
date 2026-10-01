////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values.Arrays
{
    internal sealed class ArrayItemType : AnyFunctionType
    {
        public static readonly ArrayItemType ANY_ARRAY_TYPE = new ArrayItemType(SequenceType.ANY_SEQUENCE);
        public static readonly SequenceType SINGLE_ARRAY = SequenceType.MakeSequenceType(ArrayItemType.ANY_ARRAY_TYPE, StaticProperty.EXACTLY_ONE);
        private readonly SequenceType memberType;

        public SequenceType MemberType => memberType;

        public override string BasicAlphaCode => "FA";

        public override SequenceType[] ArgumentTypes => new SequenceType[]
            {
                BuiltInAtomicType.INTEGER.One()
            };

        public override double DefaultPriority => memberType.PrimaryType.GetNormalizedDefaultPriority();

        public override SequenceType ResultType => memberType;
        public ArrayItemType(SequenceType memberType)
        {
            this.memberType = memberType;
        }

        public override Genre GetGenre()
        {
            return Genre.ARRAY;
        }

        public override bool IsMapType()
        {
            return false;
        }

        public override bool IsArrayType()
        {
            return true;
        }

        public override bool IsAtomizable(TypeHierarchy th)
        {
            return true;
        }

        public override IPlainType GetAtomizedItemType()
        {
            // For a mixed/nested member type (e.g. integer | array), the atomized member type may
            // not resolve to a single plain type; fall back to xs:anyAtomicType (deep-flattening is
            // resolved at run time) rather than crashing or yielding ErrorType.
            return memberType.PrimaryType.GetAtomizedItemType() as IPlainType ?? BuiltInAtomicType.ANY_ATOMIC;
        }

        public override bool Matches(IItem item, TypeHierarchy th)
        {
            if (!(item is ArrayItem))
            {
                return false;
            }

            if (this == ANY_ARRAY_TYPE)
            {
                return true;
            }
            else
            {
                foreach (IGroundedValue s in ((ArrayItem)item).Members())
                {
                    if (!memberType.Matches(s, th))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public override string ToString()
        {
            return MakeString(st => st.ToString());
        }

        private string MakeString(Func<SequenceType, string> show)
        {
            if (this.Equals(ANY_ARRAY_TYPE))
            {
                return "array(*)";
            }
            else
            {
                return "array(" + show(memberType) + ")";
            }
        }

        /// <summary>
        /// Test whether this array type equals another array type
        /// </summary>
        public override bool Equals(object other)
        {
            if (this == other)
            {
                return true;
            }

            if (other is ArrayItemType)
            {
                ArrayItemType f2 = (ArrayItemType)other;
                return memberType.Equals(f2.memberType);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return memberType.GetHashCode();
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
            else if (other == ArrayItemType.ANY_ARRAY_TYPE)
            {
                return Affinity.SUBSUMED_BY;
            }
            else if (other.IsMapType())
            {
                return Affinity.DISJOINT;
            }
            else if (other is ArrayItemType)
            {

                // See bug 3720. Array types are never disjoint, because the empty array
                // is an instance of every array type
                ArrayItemType f2 = (ArrayItemType)other;
                Affinity rel = th.SequenceTypeRelationship(memberType, f2.memberType);
                return rel == Affinity.DISJOINT ? Affinity.OVERLAPS : rel;
            }
            else
            {
                Affinity rel = new SpecificFunctionType(ArgumentTypes, ResultType).Relationship(other, th);
                if (rel == Affinity.SUBSUMES || rel == Affinity.SAME_TYPE)
                {
                    rel = Affinity.OVERLAPS;
                }

                return rel;
            }
        }

        public override Expression MakeFunctionSequenceCoercer(Expression exp, Func<RoleDiagnostic> role, bool allow40)
        {
            return new SpecificFunctionType(ArgumentTypes, ResultType).MakeFunctionSequenceCoercer(exp, role, false);
        }
    }
}
