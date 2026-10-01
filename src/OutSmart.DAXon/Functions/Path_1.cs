////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
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

            StringBuilder fsb = new StringBuilder(256);
            IAxisIterator iter = node.IterateAxis(AxisInfo.ANCESTOR_OR_SELF);
            NodeInfo n;
            while ((n = iter.Next()) != null)
            {
                if (n.GetParent() == null)
                {
                    if (n.GetNodeKind() == Types.Type.DOCUMENT)
                    {
                        return new StringValue(fsb.ToString());
                    }
                    else
                    {
                        fsb.Insert(0, "Q{http://www.w3.org/2005/xpath-functions}root()");
                        return new StringValue(fsb.ToString());
                    }
                }

                StringBuilder fsb2 = new StringBuilder(256);
                switch (n.GetNodeKind())
                {
                    case Types.Type.DOCUMENT:
                        return new StringValue(fsb.ToString());
                    case Types.Type.ELEMENT:
                        fsb2.Append("/Q{").Append(n.GetNamespaceUri()).Append('}');
                        fsb2.Append(n.GetLocalPart());
                        fsb2.Append('[').Append(Navigator.GetNumberSimple(n, context)).Append(']');
                        fsb2.Append(fsb);
                        fsb = fsb2;
                        break;
                    case Types.Type.ATTRIBUTE:
                        fsb2.Append("/@");
                        string attURI = n.GetNamespaceUri().ToString();
                        if (attURI != "")
                        {
                            fsb2.Append("Q{").Append(attURI).Append('}');
                        }

                        fsb2.Append(n.GetLocalPart());
                        fsb2.Append(fsb);
                        fsb = fsb2;
                        break;
                    case Types.Type.TEXT:
                        fsb2.Append("/text()[").Append(Navigator.GetNumberSimple(n, context) + "]").Append(fsb);
                        fsb = fsb2;
                        break;
                    case Types.Type.COMMENT:
                        fsb2.Append("/comment()[").Append(Navigator.GetNumberSimple(n, context)).Append(']');
                        fsb2.Append(fsb);
                        fsb = fsb2;
                        break;
                    case Types.Type.PROCESSING_INSTRUCTION:
                        fsb2.Append("/processing-instruction(").Append(n.GetLocalPart()).Append(")[");
                        fsb2.Append(Navigator.GetNumberSimple(n, context)).Append(']');
                        fsb2.Append(fsb);
                        fsb = fsb2;
                        break;
                    case Types.Type.NAMESPACE:
                        fsb2.Append("/namespace::");
                        if ((n.GetLocalPart().Length == 0))
                        {
                            fsb2.Append("*[Q{" + NamespaceConstant.FN + "}local-name()=\"\"]");
                        }
                        else
                        {
                            fsb.Append(n.GetLocalPart());
                        }

                        fsb2.Append(fsb);
                        fsb = fsb2;
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }


            // should not reach here...
            fsb.Insert(0, "Q{http://www.w3.org/2005/xpath-functions}root()");
            return new StringValue(fsb.ToString());
        }
    }
}