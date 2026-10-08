////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Patterns
{
    internal sealed class ContentTypeTest : NodeTest
    {
        private readonly int kind; // element or attribute
        private readonly ISchemaType schemaType;
        private readonly Configuration config;
        private readonly bool nillable = false;

        public override double DefaultPriority => 0;

        public override int PrimitiveType => kind;

        public override ISchemaType ContentType => schemaType;
        public ContentTypeTest(int nodeKind, ISchemaType schemaType, Configuration config, bool nillable)
        {
            this.kind = nodeKind;
            this.schemaType = schemaType;
            this.config = config;
            this.nillable = nillable;
        }

        public override UType GetUType()
        {
            return kind == Types.Type.ELEMENT ? UType.ELEMENT : UType.ATTRIBUTE;
        }

        public override bool IsNillable()
        {
            return nillable;
        }

        public ISchemaType GetSchemaType()
        {
            return schemaType;
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            return kind == nodeKind && MatchesAnnotation(annotation);
        }

        public override IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            byte[] nodeKindArray = tree.NodeKindArray;
            return IntPredicateLambda.Of((nodeNr) => (nodeKindArray[nodeNr] & 0x0f) == kind && MatchesAnnotation(((TinyTree)tree).GetSchemaType(nodeNr)) && (nillable || !((TinyTree)tree).IsNilled(nodeNr)));
        }

        public override bool Test(NodeInfo node)
        {
            return node.GetNodeKind() == kind && MatchesAnnotation(node.GetSchemaType()) && (nillable || !Nilled_1.IsNilled(node));
        }

        private bool MatchesAnnotation(ISchemaType annotation)
        {
            if (annotation == null)
            {
                return false;
            }

            if (schemaType == AnyType.INSTANCE)
            {
                return true;
            }

            if (annotation.Equals(schemaType))
            {
                return true;
            }


            // see if the type annotation is a subtype of the required type
            Affinity r = config.GetTypeHierarchy().SchemaTypeRelationship(annotation, schemaType);
            return r == Affinity.SAME_TYPE || r == Affinity.SUBSUMED_BY;
        }

        public override IAtomicType GetAtomizedItemType()
        {
            ISchemaType type = schemaType;
            try
            {
                if (type.IsAtomicType())
                {
                    return (IAtomicType)type;
                }
                else if (type is IListType)
                {
                    ISimpleType mem = ((IListType)type).GetItemType();
                    if (mem.IsAtomicType())
                    {
                        return (IAtomicType)mem;
                    }
                }
                else if (type is IComplexType && ((IComplexType)type).IsSimpleContent())
                {
                    ISimpleType ctype = ((IComplexType)type).SimpleContentType;
                    if (ctype.IsAtomicType())
                    {
                        return (IAtomicType)ctype;
                    }
                    else if (ctype is IListType)
                    {
                        ISimpleType mem = ((IListType)ctype).GetItemType();
                        if (mem.IsAtomicType())
                        {
                            return (IAtomicType)mem;
                        }
                    }
                }
            }
            catch (MissingComponentException)
            {
                return BuiltInAtomicType.ANY_ATOMIC;
            }

            return BuiltInAtomicType.ANY_ATOMIC;
        }

        public override bool IsAtomizable(TypeHierarchy th)
        {
            return !(schemaType.IsComplexType() && ((IComplexType)schemaType).Variety == ComplexVariety.ELEMENT_ONLY);
        }

        public override string ToString()
        {
            return (kind == Types.Type.ELEMENT ? "element(*, " : "attribute(*, ") + schemaType.EQName + ')';
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return kind << 20 ^ schemaType.GetHashCode();
        }

        /// <summary>
        /// Indicates whether some other object is "equal to" this one.
        /// </summary>
        public override bool Equals(object other)
        {
            return other is ContentTypeTest && ((ContentTypeTest)other).kind == kind && ((ContentTypeTest)other).schemaType == schemaType && ((ContentTypeTest)other).nillable == nillable;
        }

        /// <summary>
        /// Indicates whether some other object is "equal to" this one.
        /// </summary>
        public override string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            string explanation = base.ExplainMismatch(item, th);
            if (explanation != null)
            {
                return explanation;
            }

            NodeInfo node = (NodeInfo)item;
            if (!MatchesAnnotation(((NodeInfo)item).GetSchemaType()))
            {
                if (node.GetSchemaType() == Untyped.INSTANCE)
                {
                    return ("The supplied node has not been schema-validated");
                }

                if (node.GetSchemaType() == BuiltInAtomicType.UNTYPED_ATOMIC)
                {
                    return ("The supplied node has not been schema-validated");
                }

                return ("The supplied node has the wrong type annotation (" + node.GetSchemaType().Description + ")");
            }

            if (Nilled_1.IsNilled(node) && !nillable)
            {
                return ("The supplied node has xsi:nil='true', which the required type does not allow");
            }

            return null;
        }
    }
}
