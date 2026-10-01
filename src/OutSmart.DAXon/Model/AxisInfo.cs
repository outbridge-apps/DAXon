////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// An axis, that is a direction of navigation in the document structure.
    /// </summary>
    internal sealed class AxisInfo
    {
        /// <summary>
        /// Constant representing the ancestor axis
        /// </summary>
        public const int ANCESTOR = 0;
        /// <summary>
        /// Constant representing the ancestor-or-self axis
        /// </summary>
        public const int ANCESTOR_OR_SELF = 1;
        /// <summary>
        /// Constant representing the attribute axis
        /// </summary>
        public const int ATTRIBUTE = 2;
        /// <summary>
        /// Constant representing the child axis
        /// </summary>
        public const int CHILD = 3;
        /// <summary>
        /// Constant representing the descendant axis
        /// </summary>
        public const int DESCENDANT = 4;
        /// <summary>
        /// Constant representing the descendant-or-self axis
        /// </summary>
        public const int DESCENDANT_OR_SELF = 5;
        /// <summary>
        /// Constant representing the following axis
        /// </summary>
        public const int FOLLOWING = 6;
        /// <summary>
        /// Constant representing the following-sibling axis
        /// </summary>
        public const int FOLLOWING_SIBLING = 7;
        /// <summary>
        /// Constant representing the namespace axis
        /// </summary>
        public const int NAMESPACE = 8;
        /// <summary>
        /// Constant representing the parent axis
        /// </summary>
        public const int PARENT = 9;
        /// <summary>
        /// Constant representing the preceding axis
        /// </summary>
        public const int PRECEDING = 10;
        /// <summary>
        /// Constant representing the preceding-sibling axis
        /// </summary>
        public const int PRECEDING_SIBLING = 11;
        /// <summary>
        /// Constant representing the self axis
        /// </summary>
        public const int SELF = 12;
        // preceding-or-ancestor axis gives all preceding nodes including ancestors,
        // in reverse document order
        /// <summary>
        /// Constant representing the preceding-or-ancestor axis. This axis is used internally by the xsl:number implementation, it returns the union of the preceding axis and the ancestor axis.
        /// </summary>
        public const int PRECEDING_OR_ANCESTOR = 13;

        private const int DOC = 1 << 9 /* Types.DOCUMENT */;
        private const int ELE = 1 << 1 /* OutSmart.DAXon.Types.Type.ELEMENT */;
        private const int ATT = 1 << 2 /* OutSmart.DAXon.Types.Type.ATTRIBUTE */;
        private const int TEX = 1 << 3 /* Types.TEXT */;
        private const int PIN = 1 << 7 /* Types.PROCESSING_INSTRUCTION */;
        private const int COM = 1 << 8 /* Types.COMMENT */;
        private const int NAM = 1 << 13 /* OutSmart.DAXon.Types.Type.NAMESPACE */;
        /// <summary>
        /// Table indicating the principal node type of each axis
        /// </summary>
        public static readonly short[] principalNodeType = new short[]
        {
            Types.Type.ELEMENT,
            Types.Type.ELEMENT,
            Types.Type.ATTRIBUTE,
            Types.Type.ELEMENT,
            Types.Type.ELEMENT,
            Types.Type.ELEMENT,
            Types.Type.ELEMENT,
            Types.Type.ELEMENT,
            Types.Type.NAMESPACE,
            Types.Type.ELEMENT,
            Types.Type.ELEMENT,
            Types.Type.ELEMENT,
            Types.Type.ELEMENT,
            Types.Type.ELEMENT
        };
        /// <summary>
        /// Table indicating for each axis whether it @is in forwards document order
        /// </summary>
        public static readonly bool[] isForwards = new[]
        {
            false,
            false,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            false,
            false,
            true,
            false
        };
        public static readonly bool[] isPeerAxis = new[]
        {
            false,
            false,
            true,
            true,
            false,
            false,
            false,
            true,
            true,
            true,
            false,
            true,
            true,
            false
        };
        public static readonly bool[] isSubtreeAxis = new[]
        {
            false,
            false,
            true,
            true,
            true,
            true,
            false,
            false,
            true,
            false,
            false,
            false,
            true,
            false
        };
        /// <summary>
        /// Table giving the name of each axis as used in XPath, for example "ancestor-or-self"
        /// </summary>
        public static readonly string[] axisName = new[]
        {
            "ancestor",
            "ancestor-or-self",
            "attribute",
            "child",
            "descendant",
            "descendant-or-self",
            "following",
            "following-sibling",
            "namespace",
            "parent",
            "preceding",
            "preceding-sibling",
            "self",
            "preceding-or-ancestor"
        };

        /// <summary>
        /// The following table indicates the kinds of node found on each axis
        /// </summary>
        private static readonly int[] nodeKindTable = new[]
        {
            DOC | ELE,
            DOC | ELE | ATT | TEX | PIN | COM | NAM,
            ATT,
            ELE | TEX | PIN | COM,
            ELE | TEX | PIN | COM,
            DOC | ELE | ATT | TEX | PIN | COM | NAM,
            ELE | TEX | PIN | COM,
            ELE | TEX | PIN | COM,
            NAM,
            DOC | ELE,
            ELE | TEX | PIN | COM,
            ELE | TEX | PIN | COM,
            DOC | ELE | ATT | TEX | PIN | COM | NAM
        };

        public static int[] inverseAxis = new[]
        {
            DESCENDANT,
            DESCENDANT_OR_SELF,
            PARENT,
            PARENT,
            ANCESTOR,
            ANCESTOR_OR_SELF,
            PRECEDING,
            PRECEDING_SIBLING,
            PARENT,
            CHILD,
            FOLLOWING,
            FOLLOWING_SIBLING,
            SELF
        };
        public static int[] excludeSelfAxis = new[]
        {
            ANCESTOR,
            ANCESTOR,
            ATTRIBUTE,
            CHILD,
            DESCENDANT,
            DESCENDANT,
            FOLLOWING,
            FOLLOWING_SIBLING,
            NAMESPACE,
            PARENT,
            PRECEDING,
            PRECEDING_SIBLING,
            SELF
        };
        private static readonly IntHashMap<UType> axisTransitions = new IntHashMap<UType>(50);

        static AxisInfo()
        {

            // Declare as triples the relationships that can exist between nodes. The first argument is the type
            // of the origin node; the second is the axis; the third is the set of node kinds that can be found
            // using this axis, when starting from this origin.
            E(PrimitiveUType.DOCUMENT, ANCESTOR, UType.VOID);
            E(PrimitiveUType.DOCUMENT, ANCESTOR_OR_SELF, UType.DOCUMENT);
            E(PrimitiveUType.DOCUMENT, ATTRIBUTE, UType.VOID);
            E(PrimitiveUType.DOCUMENT, CHILD, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.DOCUMENT, DESCENDANT, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.DOCUMENT, DESCENDANT_OR_SELF, UType.DOCUMENT.Union(UType.CHILD_NODE_KINDS));
            E(PrimitiveUType.DOCUMENT, FOLLOWING, UType.VOID);
            E(PrimitiveUType.DOCUMENT, FOLLOWING_SIBLING, UType.VOID);
            E(PrimitiveUType.DOCUMENT, NAMESPACE, UType.VOID);
            E(PrimitiveUType.DOCUMENT, PARENT, UType.VOID);
            E(PrimitiveUType.DOCUMENT, PRECEDING, UType.VOID);
            E(PrimitiveUType.DOCUMENT, PRECEDING_SIBLING, UType.VOID);
            E(PrimitiveUType.DOCUMENT, SELF, UType.DOCUMENT);
            E(PrimitiveUType.ELEMENT, ANCESTOR, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.ELEMENT, ANCESTOR_OR_SELF, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.ELEMENT, ATTRIBUTE, UType.ATTRIBUTE);
            E(PrimitiveUType.ELEMENT, CHILD, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.ELEMENT, DESCENDANT, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.ELEMENT, DESCENDANT_OR_SELF, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.ELEMENT, FOLLOWING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.ELEMENT, FOLLOWING_SIBLING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.ELEMENT, NAMESPACE, UType.NAMESPACE);
            E(PrimitiveUType.ELEMENT, PARENT, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.ELEMENT, PRECEDING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.ELEMENT, PRECEDING_SIBLING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.ELEMENT, SELF, UType.ELEMENT);
            E(PrimitiveUType.ATTRIBUTE, ANCESTOR, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.ATTRIBUTE, ANCESTOR_OR_SELF, UType.ATTRIBUTE.Union(UType.PARENT_NODE_KINDS));
            E(PrimitiveUType.ATTRIBUTE, ATTRIBUTE, UType.VOID);
            E(PrimitiveUType.ATTRIBUTE, CHILD, UType.VOID);
            E(PrimitiveUType.ATTRIBUTE, DESCENDANT, UType.VOID);
            E(PrimitiveUType.ATTRIBUTE, DESCENDANT_OR_SELF, UType.ATTRIBUTE);
            E(PrimitiveUType.ATTRIBUTE, FOLLOWING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.ATTRIBUTE, FOLLOWING_SIBLING, UType.VOID);
            E(PrimitiveUType.ATTRIBUTE, NAMESPACE, UType.VOID);
            E(PrimitiveUType.ATTRIBUTE, PARENT, UType.ELEMENT);
            E(PrimitiveUType.ATTRIBUTE, PRECEDING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.ATTRIBUTE, PRECEDING_SIBLING, UType.VOID);
            E(PrimitiveUType.ATTRIBUTE, SELF, UType.ATTRIBUTE);
            E(PrimitiveUType.TEXT, ANCESTOR, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.TEXT, ANCESTOR_OR_SELF, UType.TEXT.Union(UType.PARENT_NODE_KINDS));
            E(PrimitiveUType.TEXT, ATTRIBUTE, UType.VOID);
            E(PrimitiveUType.TEXT, CHILD, UType.VOID);
            E(PrimitiveUType.TEXT, DESCENDANT, UType.VOID);
            E(PrimitiveUType.TEXT, DESCENDANT_OR_SELF, UType.TEXT);
            E(PrimitiveUType.TEXT, FOLLOWING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.TEXT, FOLLOWING_SIBLING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.TEXT, NAMESPACE, UType.VOID);
            E(PrimitiveUType.TEXT, PARENT, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.TEXT, PRECEDING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.TEXT, PRECEDING_SIBLING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.TEXT, SELF, UType.TEXT);
            E(PrimitiveUType.PI, ANCESTOR, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.PI, ANCESTOR_OR_SELF, UType.PI.Union(UType.PARENT_NODE_KINDS));
            E(PrimitiveUType.PI, ATTRIBUTE, UType.VOID);
            E(PrimitiveUType.PI, CHILD, UType.VOID);
            E(PrimitiveUType.PI, DESCENDANT, UType.VOID);
            E(PrimitiveUType.PI, DESCENDANT_OR_SELF, UType.PI);
            E(PrimitiveUType.PI, FOLLOWING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.PI, FOLLOWING_SIBLING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.PI, NAMESPACE, UType.VOID);
            E(PrimitiveUType.PI, PARENT, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.PI, PRECEDING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.PI, PRECEDING_SIBLING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.PI, SELF, UType.PI);
            E(PrimitiveUType.COMMENT, ANCESTOR, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.COMMENT, ANCESTOR_OR_SELF, UType.COMMENT.Union(UType.PARENT_NODE_KINDS));
            E(PrimitiveUType.COMMENT, ATTRIBUTE, UType.VOID);
            E(PrimitiveUType.COMMENT, CHILD, UType.VOID);
            E(PrimitiveUType.COMMENT, DESCENDANT, UType.VOID);
            E(PrimitiveUType.COMMENT, DESCENDANT_OR_SELF, UType.COMMENT);
            E(PrimitiveUType.COMMENT, FOLLOWING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.COMMENT, FOLLOWING_SIBLING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.COMMENT, NAMESPACE, UType.VOID);
            E(PrimitiveUType.COMMENT, PARENT, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.COMMENT, PRECEDING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.COMMENT, PRECEDING_SIBLING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.COMMENT, SELF, UType.COMMENT);
            E(PrimitiveUType.NAMESPACE, ANCESTOR, UType.PARENT_NODE_KINDS);
            E(PrimitiveUType.NAMESPACE, ANCESTOR_OR_SELF, UType.NAMESPACE.Union(UType.PARENT_NODE_KINDS));
            E(PrimitiveUType.NAMESPACE, ATTRIBUTE, UType.VOID);
            E(PrimitiveUType.NAMESPACE, CHILD, UType.VOID);
            E(PrimitiveUType.NAMESPACE, DESCENDANT, UType.VOID);
            E(PrimitiveUType.NAMESPACE, DESCENDANT_OR_SELF, UType.NAMESPACE);
            E(PrimitiveUType.NAMESPACE, FOLLOWING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.NAMESPACE, FOLLOWING_SIBLING, UType.VOID);
            E(PrimitiveUType.NAMESPACE, NAMESPACE, UType.VOID);
            E(PrimitiveUType.NAMESPACE, PARENT, UType.ELEMENT);
            E(PrimitiveUType.NAMESPACE, PRECEDING, UType.CHILD_NODE_KINDS);
            E(PrimitiveUType.NAMESPACE, PRECEDING_SIBLING, UType.VOID);
            E(PrimitiveUType.NAMESPACE, SELF, UType.NAMESPACE);
        }

        public static int GetAxisNumber(string name)
        {
            switch (name)
            {
                case "ancestor":
                    return ANCESTOR;
                case "ancestor-or-self":
                    return ANCESTOR_OR_SELF;
                case "attribute":
                    return ATTRIBUTE;
                case "child":
                    return CHILD;
                case "descendant":
                    return DESCENDANT;
                case "descendant-or-self":
                    return DESCENDANT_OR_SELF;
                case "following":
                    return FOLLOWING;
                case "following-sibling":
                    return FOLLOWING_SIBLING;
                case "namespace":
                    return NAMESPACE;
                case "parent":
                    return PARENT;
                case "preceding":
                    return PRECEDING;
                case "preceding-sibling":
                    return PRECEDING_SIBLING;
                case "self":
                    return SELF;
                case "preceding-or-ancestor":
                    return PRECEDING_OR_ANCESTOR;
                default:
                    throw new XPathException("Unknown axis name: " + name);
            }
        }
        public static bool ContainsNodeKind(int axis, int nodeKind)
        {
            return nodeKind == Types.Type.NODE || (nodeKindTable[axis] & (1 << nodeKind)) != 0;
        }
        private static void E(PrimitiveUType origin, int axis, UType target)
        {
            axisTransitions.Put(MakeKey(origin, axis), target);
        }

        private static int MakeKey(PrimitiveUType origin, int axis)
        {
            return origin.GetBit() << 16 | axis;
        }

        public static UType GetTargetUType(UType origin, int axis)
        {
            UType resultType = UType.VOID;
            HashSet<PrimitiveUType> origins = origin.Intersection(UType.ANY_NODE).Decompose();
            foreach (PrimitiveUType u in origins)
            {
                UType r = axisTransitions[MakeKey(u, axis)];
                if (r == null)
                {
                    throw new InvalidOperationException("Unknown transitions for primitive type " + u.ToString() + "::" + axis);
                }

                resultType = resultType.Union(r);
            }

            return resultType;
        }
    }
}