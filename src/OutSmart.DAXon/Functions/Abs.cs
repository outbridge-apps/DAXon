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
    /// This class implements the fn:abs() function
    /// </summary>
    internal sealed class Abs : ScalarSystemFunction
    {
        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            return ((NumericValue)arg).Abs();
        }

        public override Elaborator GetElaborator()
        {
            return new AbsElaborator();
        }

        internal sealed class AbsElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                IItemEvaluator argEval = fnc.GetArg(0).MakeElaborator().ElaborateForItem();
                bool nullable = Cardinality.AllowsZero(fnc.GetArg(0).GetCardinality());
                if (nullable)
                {
                    return (context) =>
                    {
                        NumericValue result = (NumericValue)argEval.Eval(context);
                        if (result == null)
                        {
                            return null;
                        }

                        return result.Abs();
                    };
                }
                else
                {
                    return (context) => ((NumericValue)argEval.Eval(context)).Abs();
                }
            }
        }
    }
}