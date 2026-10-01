////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Patterns
{
    /// <summary>
    /// A NodeTestPattern is a pattern that consists simply of a NodeTest
    /// </summary>
    internal sealed class NodeTestPattern : Pattern
    {
        private readonly NodeTest nodeTest;

        public override int Fingerprint => nodeTest.Fingerprint;
        public NodeTestPattern(NodeTest test)
        {
            nodeTest = test;
            SetPriority(test.DefaultPriority);
        }

        public override bool Matches(IItem item, IXPathContext context)
        {
            return item is NodeInfo && nodeTest.Test((NodeInfo)item);
        }

        public override ItemType GetItemType()
        {
            return nodeTest;
        }

        public override UType GetUType()
        {
            return nodeTest.GetUType();
        }

        public override string Reconstruct()
        {
            return nodeTest.ToString();
        }

        public override string ToShortString()
        {
            return nodeTest.ToShortString();
        }

        public override bool Equals(object other)
        {
            return (other is NodeTestPattern) && ((NodeTestPattern)other).nodeTest.Equals(nodeTest);
        }

        protected override int ComputeHashCode()
        {
            return 0x7aeffea8 ^ nodeTest.GetHashCode();
        }

        public override Pattern ConvertToTypedPattern(string val)
        {
            if (nodeTest is NameTest && nodeTest.GetUType() == UType.ELEMENT)
            {
                ISchemaDeclaration decl = GetConfiguration().GetElementDeclaration(nodeTest.MatchingNodeName);
                if (decl == null)
                {
                    if ("lax".Equals(val))
                    {
                        return this;
                    }
                    else
                    {

                        // See spec bug 25517
                        throw new XPathException("The mode specifies typed='strict', " + "but there is no schema element declaration named " + nodeTest, "XTSE3105");
                    }
                }
                else
                {
                    NodeTest schemaNodeTest = decl.MakeSchemaNodeTest();
                    return new NodeTestPattern(schemaNodeTest);
                }
            }
            else
            {
                return this;
            }
        }

        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("p.nodeTest");
            presenter.EmitAttribute("test", AlphaCode.FromItemType(nodeTest));
            presenter.EndElement();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            NodeTestPattern n = new NodeTestPattern(nodeTest.Copy());
            n.SetPriority(DefaultPriority);
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;
            return n;
        }

        public NodeTest GetNodeTest()
        {
            return nodeTest;
        }
    }
}
