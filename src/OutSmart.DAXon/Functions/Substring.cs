////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the XPath substring() function
    /// </summary>
    internal sealed class Substring : SystemFunction, ICallable
    {
        public override Expression TypeCheckCaller(FunctionCall caller, ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e2 = base.TypeCheckCaller(caller, visitor, contextInfo);
            if (e2 != caller)
            {
                return e2;
            }

            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            if (caller.GetArg(1).IsCallOn(typeof(Number_1)))
            {
                Expression a1 = ((StaticFunctionCall)caller.GetArg(1)).GetArg(0);
                if (th.IsSubType(a1.GetItemType(), BuiltInAtomicType.INTEGER))
                {
                    caller.SetArg(1, a1);
                }
            }

            if (GetArity() > 2 && caller.GetArg(2).IsCallOn(typeof(Number_1)))
            {
                Expression a2 = ((StaticFunctionCall)caller.GetArg(2)).GetArg(0);
                if (th.IsSubType(a2.GetItemType(), BuiltInAtomicType.INTEGER))
                {
                    caller.SetArg(2, a2);
                }
            }

            return caller;
        }

        public static StringValue SubstringFn(StringValue sv, NumericValue start)
        {
            long slength = sv.Length();
            long lstart;
            if (start is Int64Value)
            {

                lstart = ((Int64Value)start).LongValue();
                if (lstart > slength)
                {
                    return StringValue.EMPTY_STRING;
                }
                else if (lstart <= 0)
                {
                    lstart = 1;
                }
            }
            else
            {

                //NumericValue rstart = start.round();
                // We need to be careful to handle cases such as plus/minus infinity
                if (start.IsNaN())
                {
                    return StringValue.EMPTY_STRING;
                }
                else if (start.Signum() <= 0)
                {
                    return sv;
                }
                else if (start.CompareTo(slength) > 0)
                {
                    return StringValue.EMPTY_STRING;
                }
                else
                {
                    lstart = JavaMath.Round(start.GetDoubleValue());
                    if (lstart < 1)
                    {
                        // 0 < start < 0.5 rounds to 0: every position is at or after it
                        return sv;
                    }
                }
            }

            if (lstart > slength)
            {
                return StringValue.EMPTY_STRING;
            }

            return new StringValue(sv.Content.Substring((int)lstart - 1, slength));
        }

        public static StringValue SubstringFn(StringValue sv, NumericValue start, NumericValue len)
        {
            long slength = sv.Length();
            long lstart;
            if (start is Int64Value)
            {

                lstart = ((Int64Value)start).LongValue();
                if (lstart > slength)
                {
                    return StringValue.EMPTY_STRING;
                }
            }
            else
            {

                // We need to be careful to handle cases such as plus/minus infinity and NaN
                if (start.IsNaN())
                {
                    return StringValue.EMPTY_STRING;
                }
                else if (start.CompareTo(slength) > 0)
                {

                    // this works even where the string contains surrogate pairs,
                    // because the Java length is always >= the XPath length
                    return StringValue.EMPTY_STRING;
                }
                else
                {
                    double dstart = start.GetDoubleValue();
                    lstart = double.IsInfinity(dstart) ? -int.MaxValue : JavaMath.Round(dstart);
                }
            }

            long llen;
            if (len is Int64Value)
            {
                llen = ((Int64Value)len).LongValue();
                if (llen <= 0)
                {
                    return StringValue.EMPTY_STRING;
                }
            }
            else
            {
                if (len.IsNaN())
                {
                    return StringValue.EMPTY_STRING;
                }

                if (len.Signum() <= 0)
                {
                    return StringValue.EMPTY_STRING;
                }

                double dlen = len.GetDoubleValue();
                if (double.IsInfinity(dlen))
                {
                    llen = int.MaxValue;
                }
                else
                {
                    llen = JavaMath.Round(len.GetDoubleValue());
                }
            }

            // Upstream returns EMPTY when lstart+llen wraps and truncates lend through (int) before
            // the min — both give wrong answers (or a raw crash) for the everyday rest-of-string
            // idiom substring($s, $pos, 999999999999). Spec arithmetic is xs:double, which cannot
            // overflow, so the honest reading of a wrapped/huge end is "past the end of the string".
            long lend = lstart + llen;
            if (lend < lstart)
            {
                lend = long.MaxValue;
            }

            int a1 = (int)lstart - 1;   // lstart <= slength <= int.MaxValue by the guards above
            if (a1 >= slength)
            {
                return StringValue.EMPTY_STRING;
            }

            long a2 = Math.Min(slength, lend - 1);
            if (a1 < 0)
            {
                if (a2 < 0)
                {
                    return StringValue.EMPTY_STRING;
                }
                else
                {
                    a1 = 0;
                }
            }

            return new StringValue(sv.Content.Substring(a1, a2));
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue arg0 = (StringValue)arguments[0].Head();
            if (arg0 == null)
            {
                return StringValue.EMPTY_STRING;
            }

            NumericValue arg1 = (NumericValue)arguments[1].Head();
            if (arguments.Length == 2)
            {
                return SubstringFn(arg0, arg1);
            }
            else
            {
                NumericValue arg2 = (NumericValue)arguments[2].Head();
                if (arg2 == null)
                {

                    // Third argument can be an empty sequence in 4.0
                    if (GetRetainedStaticContext().GetPackageData().HostLanguageVersion < 40)
                    {
                        XPathException err = new XPathException("3rd argument of substring() must not be an empty sequence (unless 4.0 is enabled)", "XPTY0004");
                        err.SetIsTypeError(true);
                        throw err;
                    }
                    else
                    {
                        return SubstringFn(arg0, arg1);
                    }
                }

                return SubstringFn(arg0, arg1, arg2);
            }
        }

        public override Elaborator GetElaborator()
        {
            return new SubstringFnElaborator();
        }
        ISequence ICallable.Call(IXPathContext arg0, ISequence[] arg1) => Call(arg0, arg1);

        internal sealed class SubstringFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                IItemEvaluator arg0Eval = fnc.GetArg(0).MakeElaborator().ElaborateForItem();
                IItemEvaluator arg1Eval = fnc.GetArg(1).MakeElaborator().ElaborateForItem();
                bool nullable = Cardinality.AllowsZero(fnc.GetArg(0).GetCardinality());

                // substring($s, 1, 1) / substring($s, 2) — literal integer positions (the
                // per-character recursion idiom): the spec's round/NaN/infinity ladder collapses
                // at elaboration time to a bounds check + one slice. Only in-range literals take
                // the lane (start >= 1, length >= 1); the generic evaluator keeps every edge.
                if (fnc.GetArg(1) is Literal startLit && startLit.GroundedValue is Int64Value startVal && startVal.LongValue() >= 1)
                {
                    long lstart = startVal.LongValue();
                    if (fnc.GetArity() == 2)
                    {
                        return (context) =>
                        {
                            StringValue sv = (StringValue)arg0Eval.Eval(context);
                            if (nullable && sv == null)
                            {
                                return StringValue.EMPTY_STRING;
                            }

                            long slength = sv.Length();
                            return lstart > slength
                                ? StringValue.EMPTY_STRING
                                : new StringValue(sv.Content.Substring((int)lstart - 1, slength));
                        };
                    }

                    if (fnc.GetArg(2) is Literal lenLit && lenLit.GroundedValue is Int64Value lenVal && lenVal.LongValue() >= 1)
                    {
                        long llen = lenVal.LongValue();
                        return (context) =>
                        {
                            StringValue sv = (StringValue)arg0Eval.Eval(context);
                            if (nullable && sv == null)
                            {
                                return StringValue.EMPTY_STRING;
                            }

                            long slength = sv.Length();
                            if (lstart > slength)
                            {
                                return StringValue.EMPTY_STRING;
                            }

                            long end = lstart - 1 + llen;   // exclusive, codepoints
                            if (end > slength || end < lstart)   // second arm: lstart-1+llen wrapped past long.MaxValue
                            {
                                end = slength;
                            }

                            return new StringValue(sv.Content.Substring((int)lstart - 1, end));
                        };
                    }
                }

                if (fnc.GetArity() == 2)
                {
                    return (context) =>
                    {
                        StringValue sv = (StringValue)arg0Eval.Eval(context);
                        if (nullable && sv == null)
                        {
                            return StringValue.EMPTY_STRING;
                        }

                        NumericValue start = (NumericValue)arg1Eval.Eval(context);
                        return SubstringFn(sv, start);
                    };
                }
                else
                {
                    IItemEvaluator arg2Eval = fnc.GetArg(2).MakeElaborator().ElaborateForItem();
                    bool disallowEmpty = fnc.GetRetainedStaticContext().GetPackageData().HostLanguageVersion < 40;
                    return (context) =>
                    {
                        StringValue sv = (StringValue)arg0Eval.Eval(context);
                        if (nullable && sv == null)
                        {
                            return StringValue.EMPTY_STRING;
                        }

                        NumericValue start = (NumericValue)arg1Eval.Eval(context);
                        NumericValue len = (NumericValue)arg2Eval.Eval(context);
                        if (len == null)
                        {

                            // Third argument can be an empty sequence in 4.0
                            if (disallowEmpty)
                            {
                                XPathException err = new XPathException("3rd argument of substring() must not be an empty sequence (unless 4.0 is enabled)", "XPTY0004");
                                err.SetIsTypeError(true);
                                throw err;
                            }
                            else
                            {
                                return SubstringFn(sv, start);
                            }
                        }

                        return SubstringFn(sv, start, len);
                    };
                }
            }
        }
    }
}