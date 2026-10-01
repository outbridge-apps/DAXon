////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
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
    /// This class supports the round-to-half-even() function
    /// </summary>
    internal sealed class RoundHalfToEven : SystemFunction
    {
        public override int GetCardinality(Expression[] arguments)
        {
            return arguments[0].GetCardinality();
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NumericValue val0 = (NumericValue)arguments[0].Head();
            if (val0 == null)
            {
                return EmptySequence.GetInstance();
            }

            int scale = 0;
            if (arguments.Length == 2)
            {
                NumericValue scaleVal = (NumericValue)arguments[1].Head();
                if (scaleVal != null)
                {
                    if (scaleVal.CompareTo(int.MaxValue) > 0)
                    {
                        return val0;
                    }
                    else if (scaleVal.CompareTo(int.MinValue) < 0)
                    {
                        scale = int.MinValue;
                    }
                    else
                    {
                        scale = (int)scaleVal.LongValue();
                    }
                }
            }

            return val0.Round(scale, Round.RoundingRule.HALF_TO_EVEN);
        }

        public override Elaborator GetElaborator()
        {
            return new RoundHalfToEvenElaborator();
        }

        internal sealed class RoundHalfToEvenElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                IItemEvaluator arg0eval = fnc.GetArg(0).MakeElaborator().ElaborateForItem();
                bool nullable = Cardinality.AllowsZero(fnc.GetArg(0).GetCardinality());
                if (fnc.GetArity() == 1)
                {
                    if (nullable)
                    {
                        return (context) =>
                        {
                            NumericValue result = (NumericValue)arg0eval.Eval(context);
                            if (result == null)
                            {
                                return null;
                            }

                            return result.Round(0, Round.RoundingRule.HALF_TO_EVEN);
                        };
                    }
                    else
                    {
                        return (context) => ((NumericValue)arg0eval.Eval(context)).Round(0, Round.RoundingRule.HALF_TO_EVEN);
                    }
                }
                else if (fnc.GetArg(1) is Literal && ((Literal)fnc.GetArg(1)).GroundedValue is NumericValue)
                {
                    NumericValue scaleVal = (NumericValue)((Literal)fnc.GetArg(1)).GroundedValue;
                    if (scaleVal.CompareTo(int.MaxValue) > 0)
                    {
                        return arg0eval;
                    }
                    else
                    {
                        try
                        {
                            int scale = scaleVal.CompareTo(int.MinValue) < 0 ? int.MinValue : (int)scaleVal.LongValue();
                            return (context) =>
                            {
                                NumericValue result = (NumericValue)arg0eval.Eval(context);
                                if (result == null)
                                {
                                    return null;
                                }

                                return result.Round(scale, Round.RoundingRule.HALF_TO_EVEN);
                            };
                        }
                        catch (XPathException e)
                        {
                            return (context) =>
                            {
                                throw e;
                            };
                        }
                    }
                }
                else
                {
                    IItemEvaluator scaleArg = fnc.GetArg(1).MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        NumericValue result = (NumericValue)arg0eval.Eval(context);
                        if (result == null)
                        {
                            return null;
                        }

                        NumericValue scaleVal = (NumericValue)scaleArg.Eval(context);
                        int scale = 0;
                        if (scaleVal != null)
                        {
                            if (scaleVal.CompareTo(int.MaxValue) > 0)
                            {
                                return result;
                            }
                            else if (scaleVal.CompareTo(int.MinValue) < 0)
                            {
                                scale = int.MinValue;
                            }
                            else
                            {
                                scale = (int)scaleVal.LongValue();
                            }
                        }

                        return result.Round(scale, Round.RoundingRule.HALF_TO_EVEN);
                    };
                }
            }
        }
    }
}