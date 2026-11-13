////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using System;
using System.Text;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the fn:path function with one argument
    /// </summary>
    internal sealed class Path_1 : ScalarSystemFunction
    {
        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            return MakePath((NodeInfo)arg, context);
        }

        public static StringValue MakePath(NodeInfo node, IXPathContext context)
        {
            if (node.GetNodeKind() == Types.Type.DOCUMENT)
            {
                return StringValue.MakeStringValue("/");
            }

            // The steps from the node up, joined once at the end: prepending each copied the path built so far,
            // quadratic in the depth (32 000 levels took 32 s under a 2 s limit).
            var steps = new List<string>();
            IAxisIterator iter = node.IterateAxis(AxisInfo.ANCESTOR_OR_SELF);
            NodeInfo n;
            while ((n = iter.Next()) != null)
            {
                Core.Controller.CheckActiveTimeout();
                if (n.GetParent() == null)
                {
                    return Joined(n.GetNodeKind() == Types.Type.DOCUMENT ? "" : Root, steps);
                }

                switch (n.GetNodeKind())
                {
                    case Types.Type.DOCUMENT:
                        return Joined("", steps);
                    case Types.Type.ELEMENT:
                        steps.Add("/Q{" + n.GetNamespaceUri() + "}" + n.GetLocalPart() + "[" + Navigator.GetNumberSimple(n, context) + "]");
                        break;
                    case Types.Type.ATTRIBUTE:
                        string attURI = n.GetNamespaceUri().ToString();
                        steps.Add("/@" + (attURI != "" ? "Q{" + attURI + "}" : "") + n.GetLocalPart());
                        break;
                    case Types.Type.TEXT:
                        steps.Add("/text()[" + Navigator.GetNumberSimple(n, context) + "]");
                        break;
                    case Types.Type.COMMENT:
                        steps.Add("/comment()[" + Navigator.GetNumberSimple(n, context) + "]");
                        break;
                    case Types.Type.PROCESSING_INSTRUCTION:
                        steps.Add("/processing-instruction(" + n.GetLocalPart() + ")[" + Navigator.GetNumberSimple(n, context) + "]");
                        break;
                    case Types.Type.NAMESPACE:
                        steps.Add("/namespace::" + (n.GetLocalPart().Length == 0 ? "*[Q{" + NamespaceConstant.FN + "}local-name()=\"\"]" : n.GetLocalPart()));
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            // should not reach here...
            return Joined(Root, steps);
        }

        private const string Root = "Q{http://www.w3.org/2005/xpath-functions}root()";

        private static StringValue Joined(string start, List<string> steps)
        {
            var path = new StringBuilder(start);
            for (int i = steps.Count - 1; i >= 0; i--)
            {
                path.Append(steps[i]);
            }

            return new StringValue(path.ToString());
        }
    }
}