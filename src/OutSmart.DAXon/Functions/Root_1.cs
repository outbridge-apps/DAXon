////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the XPath 2.0 root() function with one argument
    /// </summary>
    internal sealed class Root_1 : SystemFunction
    {

        public override string StreamerName => "Root";
        public override int GetSpecialProperties(Expression[] arguments)
        {
            int prop = StaticProperty.ORDERED_NODESET | StaticProperty.SINGLE_DOCUMENT_NODESET | StaticProperty.NO_NODES_NEWLY_CREATED;
            if ((GetArity() == 0) || (arguments[0].GetSpecialProperties() & StaticProperty.CONTEXT_DOCUMENT_NODESET) != 0)
            {
                prop |= StaticProperty.CONTEXT_DOCUMENT_NODESET;
            }

            return prop;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NodeInfo node = (NodeInfo)arguments[0].Head();
            if (node == null)
            {
                return EmptySequence.GetInstance();
            }
            else
            {
                return node.Root;
            }
        }

        public override Elaborator GetElaborator()
        {
            return new RootFnElaborator();
        }

        internal sealed class RootFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                IItemEvaluator arg0Eval = fnc.GetArg(0).MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    NodeInfo focus = (NodeInfo)arg0Eval.Eval(context);
                    return focus == null ? null : focus.Root;
                };
            }
        }
    }
}
