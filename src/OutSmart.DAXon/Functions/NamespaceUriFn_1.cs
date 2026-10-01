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
    /// This class supports the namespace-uri() function
    /// </summary>
    internal sealed class NamespaceUriFn_1 : ScalarSystemFunction
    {
        public override AtomicValue Evaluate(IItem item, IXPathContext context)
        {
            NamespaceUri uri = ((NodeInfo)item).GetNamespaceUri();
            return new AnyURIValue(uri.ToUnicodeString());
        }

        public override ISequence ResultWhenEmpty()
        {
            return new AnyURIValue("");
        }

        public override Elaborator GetElaborator()
        {
            return new NamespaceUriFnElaborator();
        }

        /// <summary>
        /// Elaborator for the namespace-uri() function
        /// </summary>
        internal sealed class NamespaceUriFnElaborator : ItemElaborator
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
                    NamespaceUri uri = nullable && node == null ? NamespaceUri.NULL : node.GetNamespaceUri();
                    return new AnyURIValue(uri.ToUnicodeString());
                };
            }
        }
    }
}
