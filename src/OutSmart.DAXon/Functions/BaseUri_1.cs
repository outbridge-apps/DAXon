////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the fn:base-uri() function in XPath 2.0
    /// </summary>
    internal sealed class BaseUri_1 : SystemFunction, ICallable
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NodeInfo node = (NodeInfo)arguments[0].Head();
            if (node == null)
            {
                return EmptySequence.GetInstance();
            }

            string s = node.GetBaseURI();
            if (s == null)
            {
                return EmptySequence.GetInstance();
            }

            return new AnyURIValue(s);
        }

        public override Elaborator GetElaborator()
        {
            return new BaseUriFnElaborator();
        }

        internal sealed class BaseUriFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                Expression arg = fnc.GetArg(0);
                bool nullable = Cardinality.AllowsZero(arg.GetCardinality());
                IItemEvaluator argEval = arg.MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    NodeInfo node = (NodeInfo)argEval.Eval(context);
                    if (nullable && node == null)
                    {
                        return null;
                    }

                    string s = node.GetBaseURI();
                    if (s == null)
                    {
                        return null;
                    }

                    return new AnyURIValue(s);
                };
            }
        }
    }
}
