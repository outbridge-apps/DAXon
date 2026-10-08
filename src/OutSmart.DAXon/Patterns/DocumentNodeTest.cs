////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Patterns
{
    /// <summary>
    /// A DocumentNodeTest implements the test document-node(element(~,~))
    /// </summary>
    // This is messy because the standard interface for a NodeTest does not allow
    // any navigation from the node in question - it only tests for the node kind,
    // node name, and type annotation of the node.
    internal sealed class DocumentNodeTest : NodeTest
    {
        private readonly NodeTest elementTest;

        public override double DefaultPriority => elementTest.DefaultPriority;

        public override int PrimitiveType => Types.Type.DOCUMENT;

        public NodeTest ElementTest => elementTest;
        public DocumentNodeTest(NodeTest elementTest)
        {
            this.elementTest = elementTest;
        }

        public override UType GetUType()
        {
            return UType.DOCUMENT;
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            if (nodeKind != Types.Type.DOCUMENT)
            {
                return false;
            }

            throw new NotSupportedException("DocumentNodeTest doesn't support this method");
        }

        public override bool Test(NodeInfo node)
        {
            if (node.GetNodeKind() != Types.Type.DOCUMENT)
            {
                return false;
            }

            IAxisIterator iter = node.IterateAxis(AxisInfo.CHILD);

            // The match is true if there is exactly one element node child, no text node
            // children, and the element node matches the element test.
            bool found = false;
            NodeInfo n;
            while ((n = iter.Next()) != null)
            {
                int kind = n.GetNodeKind();
                if (kind == Types.Type.TEXT)
                {
                    return false;
                }
                else if (kind == Types.Type.ELEMENT)
                {
                    if (found)
                    {
                        return false;
                    }

                    if (elementTest.Test(n))
                    {
                        found = true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return found;
        }

        public override string ToString()
        {
            return "document-node(" + elementTest + ')';
        }

        public override int GetHashCode()
        {
            return elementTest.GetHashCode() ^ 12345;
        }

        public override bool Equals(object other)
        {
            return other is DocumentNodeTest && ((DocumentNodeTest)other).elementTest.Equals(elementTest);
        }

        public override string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            string explanation = base.ExplainMismatch(item, th);
            if (explanation != null)
            {
                return explanation;
            }

            NodeInfo node = (NodeInfo)item;
            IAxisIterator iter = node.IterateAxis(AxisInfo.CHILD);

            bool found = false;
            NodeInfo n;
            while ((n = iter.Next()) != null)
            {
                int kind = n.GetNodeKind();
                if (kind == Types.Type.TEXT)
                {
                    return ("The supplied document node has text node children");
                }
                else if (kind == Types.Type.ELEMENT)
                {
                    if (found)
                    {
                        return ("The supplied document node has more than one element child");
                    }

                    if (elementTest.Test(n))
                    {
                        found = true;
                    }
                    else
                    {
                        string s = "The supplied document node has an element child (" + Err.Depict(n) + ") that does not satisfy the element test";
                        string more = elementTest.ExplainMismatch(n, th);
                        if (more != null)
                        {
                            s += ". " + more;
                        }

                        return (s);
                    }
                }
            }

            return null;
        }
    }
}
