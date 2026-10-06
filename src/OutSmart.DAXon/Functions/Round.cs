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
using OutSmart.DAXon.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the fn:round() function
    /// </summary>
    public sealed class Round : SystemFunction
    {

        public static Func<Round> New() => () => new Round();
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

            RoundingRule roundingRule = RoundingRule.HALF_TO_CEILING;
            NumericValue scaleVal = arguments.Length >= 2 ? (NumericValue)arguments[1].Head() : null;
            if (arguments.Length >= 3)
            {
                StringValue rounding = (StringValue)arguments[2].Head();
                roundingRule = rounding == null ? RoundingRule.HALF_TO_CEILING : GetRoundingRule(rounding.GetStringValue());
            }

            if (KeepsEveryDigit(scaleVal))
            {
                return val0;
            }

            int scaleRnd = Precision(scaleVal);
            if (roundingRule == RoundingRule.HALF_TO_CEILING)
            {
                return val0.Round(scaleRnd);
            }
            else
            {
                return val0.Round(scaleRnd, roundingRule);
            }
        }

        public override Elaborator GetElaborator()
        {
            return new RoundElaborator();
        }

        // A precision past int keeps every digit, as round-half-to-even does; a plain (int) cast made 2^31 into
        // -2^31, which rounds every digit away.
        private static bool KeepsEveryDigit(NumericValue precision)
        {
            return precision != null && precision.CompareTo(int.MaxValue) > 0;
        }

        // Below int every digit is rounded away, as at int.MinValue.
        private static int Precision(NumericValue precision)
        {
            if (precision == null)
            {
                return 0;
            }

            return precision.CompareTo(int.MinValue) < 0 ? int.MinValue : (int)precision.LongValue();
        }

        public static RoundingRule GetRoundingRule(string s)
        {
            switch (s)
            {
                case "toward-zero":
                    return RoundingRule.TOWARD_ZERO;
                case "away-from-zero":
                    return RoundingRule.AWAY_FROM_ZERO;
                case "ceiling":
                    return RoundingRule.CEILING;
                case "floor":
                    return RoundingRule.FLOOR;
                case "half-toward-zero":
                    return RoundingRule.HALF_TOWARD_ZERO;
                case "half-away-from-zero":
                    return RoundingRule.HALF_AWAY_FROM_ZERO;
                case "half-to-ceiling":
                    return RoundingRule.HALF_TO_CEILING;
                case "half-to-floor":
                    return RoundingRule.HALF_TO_FLOOR;
                case "half-to-even":
                    return RoundingRule.HALF_TO_EVEN;
                default:

                    // Temp for 12.x - not checked by type signature
                    throw new XPathException("Invalid rounding mode " + s, "XPTY0004");
            }
        }

        internal sealed class RoundElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                IItemEvaluator argEval = fnc.GetArg(0).MakeElaborator().ElaborateForItem();
                bool nullable = Cardinality.AllowsZero(fnc.GetArg(0).GetCardinality());
                if (fnc.GetArity() == 1)
                {
                    if (nullable)
                    {
                        return (context) =>
                        {
                            NumericValue result = (NumericValue)argEval.Eval(context);
                            if (result == null)
                            {
                                return null;
                            }

                            return result.Round(0);
                        };
                    }
                    else
                    {
                        return (context) => ((NumericValue)argEval.Eval(context)).Round(0);
                    }
                }
                else if (fnc.GetArity() == 2)
                {
                    IItemEvaluator scaleArg = fnc.GetArg(1).MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        NumericValue result = (NumericValue)argEval.Eval(context);
                        if (result == null)
                        {
                            return null;
                        }

                        IntegerValue scaleArgVal = ((IntegerValue)scaleArg.Eval(context));
                        if (KeepsEveryDigit(scaleArgVal))
                        {
                            return result;
                        }

                        return result.Round(Precision(scaleArgVal));
                    };
                }
                else
                {
                    IItemEvaluator scaleArg = fnc.GetArg(1).MakeElaborator().ElaborateForItem();
                    IItemEvaluator modeArg = fnc.GetArg(2).MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        NumericValue result = (NumericValue)argEval.Eval(context);
                        if (result == null)
                        {
                            return null;
                        }

                        IntegerValue scaleArgVal = ((IntegerValue)scaleArg.Eval(context));
                        StringValue midpointModeVal = ((StringValue)modeArg.Eval(context));
                        RoundingRule mode = midpointModeVal == null ? RoundingRule.HALF_TO_CEILING : GetRoundingRule(midpointModeVal.GetStringValue());
                        if (KeepsEveryDigit(scaleArgVal))
                        {
                            return result;
                        }

                        int scale = Precision(scaleArgVal);
                        if (mode == RoundingRule.HALF_TO_CEILING)
                        {
                            return result.Round(scale);
                        }
                        else
                        {
                            return result.Round(scale, mode);
                        }
                    };
                }
            }
        }

        public enum RoundingRule
        {
            FLOOR,
            CEILING,
            TOWARD_ZERO,
            AWAY_FROM_ZERO,
            HALF_TO_FLOOR,
            HALF_TO_CEILING,
            HALF_TOWARD_ZERO,
            HALF_AWAY_FROM_ZERO,
            HALF_TO_EVEN
        }
    }
}