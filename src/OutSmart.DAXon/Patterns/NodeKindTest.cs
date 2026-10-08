////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Collections;
using System;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Patterns
{
    internal sealed class NodeKindTest : NodeTest
    {
        public static readonly NodeKindTest DOCUMENT = new NodeKindTest(Types.Type.DOCUMENT);
        public static readonly NodeKindTest ELEMENT = new NodeKindTest(Types.Type.ELEMENT);
        public static readonly NodeKindTest ATTRIBUTE = new NodeKindTest(Types.Type.ATTRIBUTE);
        public static readonly NodeKindTest TEXT = new NodeKindTest(Types.Type.TEXT);
        public static readonly NodeKindTest COMMENT = new NodeKindTest(Types.Type.COMMENT);
        public static readonly NodeKindTest PROCESSING_INSTRUCTION = new NodeKindTest(Types.Type.PROCESSING_INSTRUCTION);
        public static readonly NodeKindTest NAMESPACE = new NodeKindTest(Types.Type.NAMESPACE);
        private readonly int kind;
        private readonly UType uType;

        public override double DefaultPriority => -0.5;

        public override int PrimitiveType => kind;

        public override ISchemaType ContentType
        {
            get
            {
                switch (kind)
                {
                    case Types.Type.DOCUMENT:
                        return AnyType.INSTANCE;
                    case Types.Type.ELEMENT:
                        return AnyType.INSTANCE;
                    case Types.Type.ATTRIBUTE:
                        return AnySimpleType.INSTANCE;
                    case Types.Type.COMMENT:
                        return BuiltInAtomicType.STRING;
                    case Types.Type.TEXT:
                        return BuiltInAtomicType.UNTYPED_ATOMIC;
                    case Types.Type.PROCESSING_INSTRUCTION:
                        return BuiltInAtomicType.STRING;
                    case Types.Type.NAMESPACE:
                        return BuiltInAtomicType.STRING;
                    default:
                        throw new InvalidOperationException("Unknown node kind");
                }
            }
        }
        private NodeKindTest(int nodeKind)
        {
            kind = nodeKind;
            uType = UType.FromTypeCode(nodeKind);
        }

        public int GetNodeKind()
        {
            return kind;
        }

        public override UType GetUType()
        {
            return UType.FromTypeCode(kind); // runtime: live recompute (override of NodeTest virtual); static-init-safe
        }

        public static NodeTest MakeNodeKindTest(int kind)
        {
            switch (kind)
            {
                case Types.Type.DOCUMENT:
                    return DOCUMENT;
                case Types.Type.ELEMENT:
                    return ELEMENT;
                case Types.Type.ATTRIBUTE:
                    return ATTRIBUTE;
                case Types.Type.COMMENT:
                    return COMMENT;
                case Types.Type.TEXT:
                    return TEXT;
                case Types.Type.PROCESSING_INSTRUCTION:
                    return PROCESSING_INSTRUCTION;
                case Types.Type.NAMESPACE:
                    return NAMESPACE;
                case Types.Type.NODE:
                    return AnyNodeTest.GetInstance();
                default:
                    throw new ArgumentException("Unknown node kind " + kind + " in NodeKindTest");
            }
        }

        public override bool Matches(IItem item, TypeHierarchy th)
        {
            return item is NodeInfo && kind == ((NodeInfo)item).GetNodeKind();
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            return kind == nodeKind;
        }

        public override IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            byte[] nodeKindArray = tree.NodeKindArray;
            if (kind == Types.Type.TEXT)
            {
                return IntPredicateLambda.Of((nodeNr) =>
                {
                    int k = nodeKindArray[nodeNr];
                    return k == Types.Type.TEXT || k == Types.Type.WHITESPACE_TEXT;
                });
            }
            else
            {
                return IntPredicateLambda.Of((nodeNr) => (nodeKindArray[nodeNr] & 0x0f) == kind);
            }
        }

        public override bool Test(NodeInfo node)
        {
            return node.GetNodeKind() == kind;
        }

        public override IAtomicType GetAtomizedItemType()
        {
            switch (kind)
            {
                case Types.Type.DOCUMENT:
                    return BuiltInAtomicType.UNTYPED_ATOMIC;
                case Types.Type.ELEMENT:
                    return BuiltInAtomicType.ANY_ATOMIC;
                case Types.Type.ATTRIBUTE:
                    return BuiltInAtomicType.ANY_ATOMIC;
                case Types.Type.COMMENT:
                    return BuiltInAtomicType.STRING;
                case Types.Type.TEXT:
                    return BuiltInAtomicType.UNTYPED_ATOMIC;
                case Types.Type.PROCESSING_INSTRUCTION:
                    return BuiltInAtomicType.STRING;
                case Types.Type.NAMESPACE:
                    return BuiltInAtomicType.STRING;
                default:
                    throw new InvalidOperationException("Unknown node kind");
            }
        }

        public override string ToString()
        {
            return Describe(kind);
        }

        public static string Describe(int kind)
        {
            switch (kind)
            {
                case Types.Type.DOCUMENT:
                    return "document-node()";
                case Types.Type.ELEMENT:
                    return "element()";
                case Types.Type.ATTRIBUTE:
                    return "attribute()";
                case Types.Type.COMMENT:
                    return "comment()";
                case Types.Type.TEXT:
                    return "text()";
                case Types.Type.PROCESSING_INSTRUCTION:
                    return "processing-instruction()";
                case Types.Type.NAMESPACE:
                    return "namespace-node()";
                default:
                    return "** error **";
            }
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return kind;
        }

        public override bool Equals(object other)
        {
            return other is NodeKindTest && ((NodeKindTest)other).kind == kind;
        }

        public override string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            string explanation = base.ExplainMismatch(item, th);
            if (explanation != null)
            {
                return explanation;
            }

            if (item is NodeInfo)
            {
                UType actualKind = UType.GetUType(item);
                if (!GetUType().Overlaps(actualKind))
                {
                    return ("The supplied value is " + actualKind.ToStringWithIndefiniteArticle());
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return ("The supplied value is " + Err.DescribeGenre(item.GetGenre()));
            }
        }

        public override string ToShortString()
        {
            switch (GetNodeKind())
            {
                case Types.Type.ELEMENT:
                    return "*";
                case Types.Type.ATTRIBUTE:
                    return "@*";
                case Types.Type.DOCUMENT:
                    return "/";
                default:
                    return ToString();
            }
        }
    }
}
