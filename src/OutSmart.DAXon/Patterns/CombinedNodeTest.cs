////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Tiny;
using System;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Patterns
{
    internal sealed class CombinedNodeTest : NodeTest
    {
        private readonly NodeTest nodetest1;
        private readonly NodeTest nodetest2;
        private readonly int @operator;

        public override int PrimitiveType
        {
            get
            {
                UType mask = GetUType();
                if (mask.Equals(UType.ELEMENT))
                {
                    return Types.Type.ELEMENT;
                }

                if (mask.Equals(UType.ATTRIBUTE))
                {
                    return Types.Type.ATTRIBUTE;
                }

                if (mask.Equals(UType.DOCUMENT))
                {
                    return Types.Type.DOCUMENT;
                }

                return Types.Type.NODE;
            }
        }

        public override IntSet RequiredNodeNames
        {
            get
            {
                IntSet os1 = nodetest1.RequiredNodeNames;
                IntSet os2 = nodetest2.RequiredNodeNames;
                if (os1 != null && os2 != null)
                {
                    IntSet s1 = os1;
                    IntSet s2 = os2;
                    switch (@operator)
                    {
                        case Token.UNION:
                            {
                                return (s1.Union(s2));
                            }

                        case Token.INTERSECT:
                            {
                                return (s1.Intersect(s2));
                            }

                        case Token.EXCEPT:
                            {
                                return (s1.Except(s2));
                            }

                        default:
                            throw new InvalidOperationException();
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        public override ISchemaType ContentType
        {
            get
            {
                ISchemaType type1 = nodetest1.ContentType;
                ISchemaType type2 = nodetest2.ContentType;
                if (type1.IsSameType(type2))
                {
                    return type1;
                }

                if (@operator == Token.INTERSECT)
                {
                    if (type2 is AnyType || (type2 is AnySimpleType && type1.IsSimpleType()))
                    {
                        return type1;
                    }

                    if (type1 is AnyType || (type1 is AnySimpleType && type2.IsSimpleType()))
                    {
                        return type2;
                    }
                }

                return AnyType.INSTANCE;
            }
        }

        public override int Fingerprint
        {
            get
            {
                int fp1 = nodetest1.Fingerprint;
                int fp2 = nodetest2.Fingerprint;
                if (fp1 == fp2)
                {
                    return fp1;
                }

                if (fp2 == -1 && @operator == Token.INTERSECT)
                {
                    return fp1;
                }

                if (fp1 == -1 && @operator == Token.INTERSECT)
                {
                    return fp2;
                }

                return -1;
            }
        }

        public override StructuredQName MatchingNodeName
        {
            get
            {
                StructuredQName n1 = nodetest1.MatchingNodeName;
                StructuredQName n2 = nodetest2.MatchingNodeName;
                if (n1 != null && n1.Equals(n2))
                {
                    return n1;
                }

                if (n1 == null && @operator == Token.INTERSECT)
                {
                    return n2;
                }

                if (n2 == null && @operator == Token.INTERSECT)
                {
                    return n1;
                }

                return null;
            }
        }

        public override double DefaultPriority
        {
            get
            {
                if (@operator == Token.UNION)
                {
                    return nodetest1.DefaultPriority;
                }
                else
                {

                    // typically it's element(E, T), element(E:*, T), etc
                    return nodetest1 is NameTest ? 0.25 : 0.125;
                }
            }
        }

        public NodeTest[] ComponentNodeTests => new NodeTest[]
            {
                nodetest1,
                nodetest2
            };

        public int Operator => @operator;
        public CombinedNodeTest(NodeTest nt1, int @operator, NodeTest nt2)
        {
            nodetest1 = nt1 ?? AnyNodeTest.GetInstance();
            this.@operator = @operator;
            nodetest2 = nt2 ?? AnyNodeTest.GetInstance();
        }

        public override UType GetUType()
        {
            UType u1 = nodetest1.GetUType();
            UType u2 = nodetest2.GetUType();
            switch (@operator)
            {
                case Token.UNION:
                    return u1.Union(u2);
                case Token.INTERSECT:
                    return u1.Intersection(u2);
                case Token.EXCEPT:
                    return u1;
                default:
                    throw new ArgumentException("Unknown operator in Combined Node Test");
            }
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            switch (@operator)
            {
                case Token.UNION:
                    return nodetest1 == null || nodetest2 == null || nodetest1.Matches(nodeKind, name, annotation) || nodetest2.Matches(nodeKind, name, annotation);
                case Token.INTERSECT:
                    return (nodetest1 == null || nodetest1.Matches(nodeKind, name, annotation)) && (nodetest2 == null || nodetest2.Matches(nodeKind, name, annotation));
                case Token.EXCEPT:
                    return (nodetest1 == null || nodetest1.Matches(nodeKind, name, annotation)) && !(nodetest2 == null || nodetest2.Matches(nodeKind, name, annotation));
                default:
                    throw new ArgumentException("Unknown operator in Combined Node Test");
            }
        }

        public override IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            switch (@operator)
            {
                case Token.UNION:
                    return IntUnionPredicate.MakeUnion(nodetest1.GetMatcher(tree), (nodetest2.GetMatcher(tree)));
                case Token.INTERSECT:
                    return IntIntersectionPredicate.MakeIntersection(nodetest1.GetMatcher(tree), (nodetest2.GetMatcher(tree)));
                case Token.EXCEPT:
                    return IntExceptPredicate.MakeDifference(nodetest1.GetMatcher(tree), nodetest2.GetMatcher(tree));
                default:
                    throw new ArgumentException("Unknown operator in Combined Node Test");
            }
        }

        public override bool Test(NodeInfo node)
        {
            switch (@operator)
            {
                case Token.UNION:
                    return nodetest1 == null || nodetest2 == null || nodetest1.Test(node) || nodetest2.Test(node);
                case Token.INTERSECT:
                    return (nodetest1 == null || nodetest1.Test(node)) && (nodetest2 == null || nodetest2.Test(node));
                case Token.EXCEPT:
                    return (nodetest1 == null || nodetest1.Test(node)) && !(nodetest2 == null || nodetest2.Test(node));
                default:
                    throw new ArgumentException("Unknown operator in Combined Node Test");
            }
        }

        public override string ToString()
        {
            return MakeString(false);
        }

        private string MakeString(bool forExport)
        {
            if (nodetest1 is NameTest && @operator == Token.INTERSECT)
            {
                int kind = nodetest1.PrimitiveType;
                string skind = kind == Types.Type.ELEMENT ? "element(" : "attribute(";
                string content = "";
                if (nodetest2 is ContentTypeTest)
                {
                    ISchemaType schemaType = ((ContentTypeTest)nodetest2).GetSchemaType();
                    if (forExport)
                    {
                        schemaType = TypeHierarchy.GetNearestNamedType(schemaType);
                    }

                    content = ", " + schemaType.EQName;
                    if (nodetest2.IsNillable())
                    {
                        content += "?";
                    }
                }

                string name = nodetest1.MatchingNodeName.EQName;
                return skind + name + content + ')';
            }
            else
            {
                string nt1 = nodetest1 == null ? "item()" : nodetest1.ToString();
                string nt2 = nodetest2 == null ? "item()" : nodetest2.ToString();
                return '(' + nt1 + ' ' + Token.tokens[@operator] + ' ' + nt2 + ')';
            }
        }

        public override IAtomicType GetAtomizedItemType()
        {
            IAtomicType type1 = nodetest1.GetAtomizedItemType();
            IAtomicType type2 = nodetest2.GetAtomizedItemType();
            if (type1.IsSameType(type2))
            {
                return type1;
            }

            if (@operator == Token.INTERSECT)
            {
                if (type2.Equals(BuiltInAtomicType.ANY_ATOMIC))
                {
                    return type1;
                }

                if (type1.Equals(BuiltInAtomicType.ANY_ATOMIC))
                {
                    return type2;
                }
            }

            return BuiltInAtomicType.ANY_ATOMIC;
        }

        public override bool IsAtomizable(TypeHierarchy th)
        {
            switch (@operator)
            {
                case Token.UNION:
                    return nodetest1.IsAtomizable(th) || nodetest2.IsAtomizable(th);
                case Token.INTERSECT:
                    return nodetest1.IsAtomizable(th) && nodetest2.IsAtomizable(th);
                case Token.EXCEPT:
                    return nodetest1.IsAtomizable(th);
                default:
                    return true;
            }
        }

        public override bool IsNillable()
        {

            // this should err on the safe side
            return nodetest1.IsNillable() && nodetest2.IsNillable();
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return nodetest1.GetHashCode() ^ nodetest2.GetHashCode();
        }

        public override bool Equals(object other)
        {
            return other is CombinedNodeTest && ((CombinedNodeTest)other).nodetest1.Equals(nodetest1) && ((CombinedNodeTest)other).nodetest2.Equals(nodetest2) && ((CombinedNodeTest)other).@operator == @operator;
        }

        public override string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            string explanation = base.ExplainMismatch(item, th);
            if (explanation != null)
            {
                return explanation;
            }

            if (@operator == Token.INTERSECT)
            {

                // the most common case
                if (!nodetest1.Test((NodeInfo)item))
                {
                    return nodetest1.ExplainMismatch(item, th);
                }
                else if (!nodetest2.Test((NodeInfo)item))
                {
                    return nodetest2.ExplainMismatch(item, th);
                }
            }

            return null;
        }
    }
}
