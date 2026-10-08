////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Patterns
{
    public abstract class NodeTest : INodePredicate, IItemTypeWithSequenceTypeCache
    {
        private SequenceType _one;
        private SequenceType _oneOrMore;
        private SequenceType _zeroOrOne;
        private SequenceType _zeroOrMore;

        public abstract double DefaultPriority { get; }

        public virtual int PrimitiveType => Types.Type.NODE;

        public virtual int Fingerprint => -1;

        public virtual StructuredQName MatchingNodeName => null;

        public virtual string BasicAlphaCode
        {
            get
            {
                switch (PrimitiveType)
                {
                    case Types.Type.NODE:
                        return "N";
                    case Types.Type.ELEMENT:
                        return "NE";
                    case Types.Type.ATTRIBUTE:
                        return "NA";
                    case Types.Type.TEXT:
                        return "NT";
                    case Types.Type.COMMENT:
                        return "NC";
                    case Types.Type.PROCESSING_INSTRUCTION:
                        return "NP";
                    case Types.Type.DOCUMENT:
                        return "ND";
                    case Types.Type.NAMESPACE:
                        return "NN";
                    default:
                        return "*";
                }
            }
        }

        public virtual ISchemaType ContentType
        {
            get
            {
                HashSet<PrimitiveUType> m = this.GetUType().Decompose();
                if (m.Count == 1)
                {
                    PrimitiveUType p = m.First();
                    switch (p)
                    {
                        case PrimitiveUType.DOCUMENT:
                            return AnyType.INSTANCE;
                        case PrimitiveUType.ELEMENT:
                            return AnyType.INSTANCE;
                        case PrimitiveUType.ATTRIBUTE:
                            return AnySimpleType.INSTANCE;
                        case PrimitiveUType.COMMENT:
                            return BuiltInAtomicType.STRING;
                        case PrimitiveUType.TEXT:
                            return BuiltInAtomicType.UNTYPED_ATOMIC;
                        case (PrimitiveUType)(Types.Type.PROCESSING_INSTRUCTION):
                            return BuiltInAtomicType.STRING;
                        case PrimitiveUType.NAMESPACE:
                            return BuiltInAtomicType.STRING;
                    }
                }

                return AnyType.INSTANCE;
            }
        }

        public virtual IntSet RequiredNodeNames => IntUniversalSet.GetInstance();
        public virtual Genre GetGenre()
        {
            return Genre.NODE;
        }

        // runtime: satisfy IItemTypeWithSequenceTypeCache.GetUType(); generic default -> non-overriders land in the always-searched genericRuleChain; precise tests override.
        public virtual UType GetUType()
        {
            return UType.ANY_NODE;
        }
        public virtual bool Matches(IItem item, TypeHierarchy th)
        {
            return item is NodeInfo && Test((NodeInfo)item);
        }

        public virtual ItemType GetPrimitiveItemType()
        {
            int p = PrimitiveType;
            if (p == Types.Type.NODE)
            {
                return AnyNodeTest.GetInstance();
            }
            else
            {
                return NodeKindTest.MakeNodeKindTest(p);
            }
        }

        public virtual bool IsAtomicType()
        {
            return false;
        }

        public virtual bool IsPlainType()
        {
            return false;
        }

        public virtual IAtomicType GetAtomizedItemType()
        {

            // This is overridden for a ContentTypeTest
            return BuiltInAtomicType.ANY_ATOMIC;
        }

        IPlainType IItemTypeWithSequenceTypeCache.GetAtomizedItemType() => GetAtomizedItemType();

        public virtual bool IsAtomizable(TypeHierarchy th)
        {

            // This is overridden for a ContentTypeTest
            return true;
        }

        public virtual IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            return IntPredicateLambda.Of((nodeNr) => tree.GetNodeKind(nodeNr) != Types.Type.PARENT_POINTER && Test(tree.GetNode(nodeNr)));
        }

        public abstract bool Matches(int nodeKind, INodeName name, ISchemaType annotation);
        public virtual bool Test(NodeInfo node)
        {
            return Matches(node.GetNodeKind(), NameOfNode.MakeName(node), node.GetSchemaType());
        }

        public virtual bool IsNillable()
        {
            return true;
        }

        public virtual NodeTest Copy()
        {
            return this;
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

        public virtual string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            if (item is NodeInfo)
            {
                UType actualKind = UType.GetUType(item);
                if (!this.GetUType().Overlaps(actualKind))
                {
                    return ("The supplied value is " + actualKind.ToStringWithIndefiniteArticle());
                }

                return null;
            }
            else
            {
                return ("The supplied value is " + Err.DescribeGenre(item.GetGenre()));
            }
        }

        public virtual string ToShortString()
        {
            return ToString();
        }
    }
}
