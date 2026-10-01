////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the node-name() function with a single argument
    /// </summary>
    internal sealed class NodeName_1 : ScalarSystemFunction
    {
        public override AtomicValue Evaluate(IItem item, IXPathContext context)
        {
            if (!(item is NodeInfo))
            {
                // e.g. 79[node-name()] — the context/argument is not a node. Upstream reports XPTY0004; the
                // unguarded (NodeInfo) cast raised a code-less InvalidCastException instead.
                throw new XPathException("Argument to fn:node-name() is not a node", "XPTY0004");
            }
            return INodeName((NodeInfo)item);
        }

        public static QNameValue INodeName(NodeInfo node)
        {
            if ((node.GetLocalPart().Length == 0))
            {
                return null;
            }

            return new QNameValue(node.GetPrefix(), node.GetNamespaceUri(), node.GetLocalPart(), BuiltInAtomicType.QNAME);
        }

        public override Elaborator GetElaborator()
        {
            return new NodeNameFnElaborator();
        }

        /// <summary>
        /// Elaborator for the fn:node-name() function
        /// </summary>
        internal sealed class NodeNameFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                Expression arg = fnc.GetArg(0);
                IItemEvaluator argEval = arg.MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    IItem __it = argEval.Eval(context);
                    if (__it != null && !(__it is NodeInfo))
                    {
                        throw new XPathException("Argument to fn:node-name() is not a node", "XPTY0004");
                    }
                    NodeInfo node = (NodeInfo)__it;
                    if (node == null || (node.GetLocalPart().Length == 0))
                    {
                        return null;
                    }

                    return new QNameValue(node.GetPrefix(), node.GetNamespaceUri(), node.GetLocalPart(), BuiltInAtomicType.QNAME);
                };
            }
        }
    }
}