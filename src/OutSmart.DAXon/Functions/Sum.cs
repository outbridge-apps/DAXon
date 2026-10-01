////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implementation of the fn:sum function
    /// </summary>
    internal sealed class Sum : FoldingFunction
    {
        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            Expression[] newArgs = new Expression[2];
            newArgs[0] = arguments[0];
            if (arguments.Length < 2 || arguments[1] is DefaultedArgumentExpression)
            {
                newArgs[1] = FunctionLiteral.MakeLiteral(Int64Value.ZERO);
                SetArity(2);
            }
            else
            {
                newArgs[1] = arguments[1];
            }

            return base.MakeFunctionCall(newArgs);
        }

        // The elaborated path short-circuits sum(m to n) into a closed form; this interpreted
        // entry is what compile-time pre-evaluation of all-literal arguments and dynamic sum#1
        // calls use. Without the same short-circuit the argument wrapper materialized a literal
        // range before folding — sum(1 to 1000000000) exhausted memory at COMPILE time.
        // Overflow falls through to the generic fold (BigInteger promotion); the zero argument
        // only matters for empty input, which a constructed IntegerRange never is.
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            if (arguments[0] is IntegerRange ir && IntegerRange.TrySum(ir.start, ir.step, ir.end, out long ranged))
            {
                return Int64Value.MakeIntegerValue(ranged);
            }

            ISequenceIterator iter = arguments[0].Iterate();
            if (iter is Expressions.AscendingRangeIterator ari && ari.TryComputeTotal(out long asc))
            {
                return Int64Value.MakeIntegerValue(asc);
            }

            if (iter is Expressions.DescendingRangeIterator dri && dri.TryComputeTotal(out long desc))
            {
                return Int64Value.MakeIntegerValue(desc);
            }

            return RunFold(GetFold(context, TailArguments(arguments)), iter);
        }

        public override Types.ItemType GetResultItemType(Expression[] args)
        {
            TypeHierarchy th = GetRetainedStaticContext().GetConfiguration().GetTypeHierarchy();
            Types.ItemType @base = Atomizer.GetAtomizedItemType(args[0], false, th);
            if (@base.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                @base = BuiltInAtomicType.DOUBLE;
            }

            if (Cardinality.AllowsZero(args[0].GetCardinality()))
            {
                if (GetArity() == 1)
                {
                    return Types.Type.GetCommonSuperType(@base, BuiltInAtomicType.INTEGER, th);
                }
                else
                {
                    return Types.Type.GetCommonSuperType(@base, args[1].GetItemType(), th);
                }
            }
            else
            {
                return @base.GetPrimitiveItemType();
            }
        }

        public override int GetCardinality(Expression[] arguments)
        {
            if (GetArity() == 1 || arguments[1].GetCardinality() == 1)
            {
                return StaticProperty.EXACTLY_ONE;
            }
            else
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }
        }

        public override IFold GetFold(IXPathContext context, params ISequence[] additionalArguments)
        {
            if (additionalArguments.Length > 0)
            {
                AtomicValue z = (AtomicValue)additionalArguments[0].Head();
                return new SumFold(context, z);
            }
            else
            {
                return new SumFold(context, Int64Value.ZERO);
            }
        }

        public override Elaborator GetElaborator()
        {
            return new SumFnElaborator();
        }

        internal sealed class SumFold : IFold
        {
            private readonly IXPathContext context;
            private readonly AtomicValue zeroValue; // null means empty sequence
            private AtomicValue data;
            private bool atStart = true;
            private readonly ConversionRules rules;
            private readonly StringConverter toDouble;
            // Unboxed accumulators: a run of xs:integer (long), xs:double, or compact xs:decimal
            // values is summed without the per-step Calculator dispatch + boxed result. Any other
            // type (or overflow) materializes the total into `data` and continues on the generic
            // path. The decimal accumulator is a (mantissa, scale) pair; stripping trailing zeros
            // only at the end (BigDecimalValue's constructor) yields the same canonical value the
            // per-step DecimalPlusDecimal + strip sequence produces.
            private long longTotal;
            private bool onLongPath;
            private double dblTotal;
            private bool onDoublePath;
            private long decUnscaled;
            private int decScale;
            private bool onDecimalPath;
            public SumFold(IXPathContext context, AtomicValue zeroValue)
            {
                this.context = context;
                this.zeroValue = zeroValue;
                this.rules = context.GetConfiguration().GetConversionRules();
                this.toDouble = BuiltInAtomicType.DOUBLE.GetStringConverter(rules);
                {
                }
            }

            public void ProcessItem(IItem item)
            {
                AtomicValue next = (AtomicValue)item;
                if (atStart)
                {
                    // A single-item sum returns the item unchanged (derived type annotations
                    // included), so the unboxed paths only engage from the SECOND value on --
                    // arithmetic on two values yields the primitive type anyway.
                    atStart = false;
                    if (next.IsUntypedAtomic())
                    {
                        data = toDouble.Convert(next).AsAtomic();
                        return;
                    }
                    else if (next is NumericValue || next is DayTimeDurationValue || next is YearMonthDurationValue)
                    {
                        data = next;
                        return;
                    }
                    else
                    {
                        throw new XPathException("Input to sum() contains a value of type " + next.PrimitiveType.DisplayName + " which is neither numeric, nor a duration").WithXPathContext(context).WithErrorCode("FORG0006");
                    }
                }

                if (onLongPath)
                {
                    if (next is Int64Value iv)
                    {
                        try
                        {
                            longTotal = checked(longTotal + iv.LongValue());
                            return;
                        }
                        catch (OverflowException)
                        {
                            // fall through to the generic path (promotes to big integer)
                        }
                    }

                    onLongPath = false;
                    data = Int64Value.MakeIntegerValue(longTotal);
                }
                else if (onDoublePath)
                {
                    if (next.IsUntypedAtomic())
                    {
                        dblTotal += ((DoubleValue)toDouble.Convert(next).AsAtomic()).GetDoubleValue();
                        return;
                    }

                    if (next is DoubleValue dv1)
                    {
                        dblTotal += dv1.GetDoubleValue();
                        return;
                    }

                    onDoublePath = false;
                    data = new DoubleValue(dblTotal);
                }
                else if (onDecimalPath)
                {
                    if (next is BigDecimalValue nbv
                        && nbv.GetDecimalValue().TryGetCompactParts(out long nu, out int ns)
                        && Internal.Numerics.BigDecimal.TryAddCompactParts(decUnscaled, decScale, nu, ns, out long dru, out int drs))
                    {
                        decUnscaled = dru;
                        decScale = drs;
                        return;
                    }

                    onDecimalPath = false;
                    data = new BigDecimalValue(Internal.Numerics.BigDecimal.FromCompact(decUnscaled, decScale));
                }
                else if (data is Int64Value d0 && next is Int64Value n0)
                {
                    try
                    {
                        longTotal = checked(d0.LongValue() + n0.LongValue());
                        onLongPath = true;
                        data = null;
                        return;
                    }
                    catch (OverflowException)
                    {
                        // generic path below
                    }
                }
                else if (data is DoubleValue dd)
                {
                    if (next.IsUntypedAtomic())
                    {
                        dblTotal = dd.GetDoubleValue() + ((DoubleValue)toDouble.Convert(next).AsAtomic()).GetDoubleValue();
                        onDoublePath = true;
                        data = null;
                        return;
                    }

                    if (next is DoubleValue nn)
                    {
                        dblTotal = dd.GetDoubleValue() + nn.GetDoubleValue();
                        onDoublePath = true;
                        data = null;
                        return;
                    }
                }
                else if (data is BigDecimalValue db && next is BigDecimalValue nb
                    && db.GetDecimalValue().TryGetCompactParts(out long du, out int ds)
                    && nb.GetDecimalValue().TryGetCompactParts(out long nu0, out int ns0)
                    && Internal.Numerics.BigDecimal.TryAddCompactParts(du, ds, nu0, ns0, out long ru0, out int rs0))
                {
                    decUnscaled = ru0;
                    decScale = rs0;
                    onDecimalPath = true;
                    data = null;
                    return;
                }

                if (data is NumericValue)
                {
                    if (next.IsUntypedAtomic())
                    {
                        next = toDouble.Convert(next).AsAtomic();
                    }
                    else if (!(next is NumericValue))
                    {
                        throw new XPathException("Input to sum() contains a mix of numeric and non-numeric values").WithXPathContext(context).WithErrorCode("FORG0006");
                    }

                    data = ArithmeticExpression.Compute(data, Calculator.PLUS, next, context);
                }
                else if (data is DurationValue)
                {
                    if (!((data is DayTimeDurationValue) || (data is YearMonthDurationValue)))
                    {
                        throw new XPathException("Input to sum() contains a duration that is neither a dayTimeDuration nor a yearMonthDuration").WithXPathContext(context).WithErrorCode("FORG0006");
                    }

                    if (!(next is DurationValue))
                    {
                        throw new XPathException("Input to sum() contains a mix of duration and non-duration values").WithXPathContext(context).WithErrorCode("FORG0006");
                    }

                    data = ((DurationValue)data).Add((DurationValue)next);
                }
                else
                {
                    throw new XPathException("Input to sum() contains a value of type " + data.PrimitiveType.DisplayName + " which is neither numeric, nor a duration").WithXPathContext(context).WithErrorCode("FORG0006");
                }
            }

            public bool IsFinished()
            {
                if (onDoublePath)
                {
                    return double.IsNaN(dblTotal);
                }

                return data is DoubleValue && data.IsNaN();
            }

            public ISequence Result()
            {
                if (atStart)
                {
                    return SequenceTool.ItemOrEmpty(zeroValue);
                }
                else if (onLongPath)
                {
                    return Int64Value.MakeIntegerValue(longTotal);
                }
                else if (onDoublePath)
                {
                    return new DoubleValue(dblTotal);
                }
                else if (onDecimalPath)
                {
                    return new BigDecimalValue(Internal.Numerics.BigDecimal.FromCompact(decUnscaled, decScale));
                }
                else
                {
                    return data;
                }
            }
        }

        internal sealed class SumFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                IPullEvaluator puller = fnc.GetArg(0).MakeElaborator().ElaborateForPull();
                bool defaultSecondArg = fnc.GetArity() < 2 || fnc.GetArg(1) is DefaultedArgumentExpression;
                IItemEvaluator zero = defaultSecondArg ? (context) => Int64Value.ZERO : fnc.GetArg(1).MakeElaborator().ElaborateForItem();

                // Each lane returns null when the argument shape is not its own; any runtime bail
                // inside a lane replays the whole sum on the generic path, which is pure.
                return TryLiteralRangeSum(fnc, puller, zero)
                    ?? TryLongTermSum(fnc, puller, zero)
                    ?? TryLeafLengthSum(fnc, puller, zero)
                    ?? TryRangeBoundsSum(fnc, puller, zero)
                    ?? BuildGenericSum(puller, zero);
            }

            // sum over a LITERAL integer range mapped by a long-compilable body — both the
            // simple-map form `RANGE!(.*.)` (body over the context item) and `for $i in RANGE
            // return f($i)` once the optimizer folds the range to a literal. The bounds are
            // known at elaboration, so the loop runs over bare longs: no range iterator, no
            // per-term box, no per-term closure re-dispatch.
            private static IItemEvaluator TryLiteralRangeSum(SystemFunctionCall fnc, IPullEvaluator puller, IItemEvaluator zero)
            {
                Expression seq = null;
                HigherOrder.FusedArity2Caller.LongBody body = null;
                if (fnc.GetArg(0) is Expressions.Instructions.ForEach feMap && feMap.SeparatorExpression == null)
                {
                    seq = feMap.GetSelectExpression();
                    body = HigherOrder.FusedArity2Caller.CompileLongForContext(feMap.GetActionExpression());
                }
                else if (fnc.GetArg(0) is ForExpression forRange
                    && forRange.GetAction().GetCardinality() == StaticProperty.EXACTLY_ONE)
                {
                    seq = forRange.Sequence;
                    body = HigherOrder.FusedArity2Caller.CompileLongFor(forRange.GetAction(), forRange);
                }

                if (body == null || !(seq is Literal rangeLit) || !(rangeLit.GroundedValue is IntegerRange ir) || ir.step != 1)
                {
                    return null;
                }

                IItemEvaluator genericSum = BuildGenericSum(puller, zero);
                long lo = ir.start;
                long hi = ir.end;   // lo <= hi by IntegerRange construction — never empty
                return (context) =>
                {
                    long total = 0;
                    for (long v = lo; v <= hi; v++)
                    {
                        if ((v & 0xfff) == 0)
                        {
                            Core.Controller.CheckActiveTimeout();
                        }

                        if (!body(v, 0, out long term)
                            || HigherOrder.FusedArity2Caller.NearOverflow(total)
                            || HigherOrder.FusedArity2Caller.NearOverflow(term))
                        {
                            return genericSum.Eval(context);
                        }

                        total += term;
                    }

                    return Int64Value.MakeIntegerValue(total);
                };
            }

            // sum(for $x in SEQ return int-arith): terms come from the long lane, the total
            // stays a long — no mapped Int64Value per item. Any deviation (non-plain-integer
            // item, lane guard, near-overflow total) replays the WHOLE sum on the generic
            // path, which is pure, so the result is the generic one by construction.
            private static IItemEvaluator TryLongTermSum(SystemFunctionCall fnc, IPullEvaluator puller, IItemEvaluator zero)
            {
                if (!(fnc.GetArg(0) is ForExpression forex)
                    || forex.GetAction().GetCardinality() != StaticProperty.EXACTLY_ONE
                    || ErrorExpression.IsContainedIn(forex.Sequence))
                {
                    return null;
                }

                HigherOrder.FusedArity2Caller.LongBody bodyLane =
                    HigherOrder.FusedArity2Caller.CompileLongFor(forex.GetAction(), forex);
                if (bodyLane == null)
                {
                    return null;
                }

                IPullEvaluator baseEval = forex.Sequence.MakeElaborator().ElaborateForPull();
                IItemEvaluator genericSum = BuildGenericSum(puller, zero);
                return (context) =>
                {
                    ISequenceIterator it = baseEval.Iterate(context);
                    long total = 0;

                    // Range base: the terms come from the range's own longs — no boxed
                    // base item per step at all (mirrors the fold range lane).
                    if (it is Expressions.AscendingRangeIterator ari && ari.GetStep().LongValue() == 1)
                    {
                        long start = ari.GetMin().LongValue();
                        long limit = ari.GetMax().LongValue();
                        long n = limit - start + 1;   // <= 2^31 by range construction
                        long v = start;
                        for (long i = 0; i < n; i++, v++)
                        {
                            Core.Controller.CheckActiveTimeout();
                            if (!bodyLane(v, 0, out long rterm)
                                || HigherOrder.FusedArity2Caller.NearOverflow(total)
                                || HigherOrder.FusedArity2Caller.NearOverflow(rterm))
                            {
                                return genericSum.Eval(context);
                            }

                            total += rterm;
                        }

                        return Int64Value.MakeIntegerValue(total);
                    }

                    bool any = false;
                    for (IItem item; (item = it.Next()) != null;)
                    {
                        Core.Controller.CheckActiveTimeout();
                        if (!(item is Int64Value iv)
                            || !ReferenceEquals(iv.GetItemType(), BuiltInAtomicType.INTEGER)
                            || !bodyLane(iv.LongValue(), 0, out long term)
                            || HigherOrder.FusedArity2Caller.NearOverflow(total)
                            || HigherOrder.FusedArity2Caller.NearOverflow(term))
                        {
                            it.Dispose();
                            return genericSum.Eval(context);
                        }

                        total += term;
                        any = true;
                    }

                    return any ? Int64Value.MakeIntegerValue(total) : zero.Eval(context);
                };
            }

            // sum(//*[not(*)]/string-length(.)): each term is the structural string-value
            // length of a leaf element, read straight off the Tiny arrays — no element
            // wrappers, no per-item Int64Value boxes. Terms are non-negative and bounded by
            // the codepoint-addressed text buffer, so the long total cannot overflow (the
            // generic fold's BigInteger promotion is unreachable on this data).
            private static IItemEvaluator TryLeafLengthSum(SystemFunctionCall fnc, IPullEvaluator puller, IItemEvaluator zero)
            {
                if (!(fnc.GetArg(0) is SlashExpression pathArg)
                    || !(pathArg.GetSelectExpression() is FilterExpression leafFilter)
                    || !Expressions.Elaboration.FusedLeafFilter.MatchLeafElements(leafFilter)
                    || !Expressions.Elaboration.FusedLeafFilter.IsStringLengthOfSelf(pathArg.GetActionExpression()))
                {
                    return null;
                }

                IItemEvaluator genericSum = BuildGenericSum(puller, zero);
                return (context) =>
                {
                    if (context.GetContextItem() is Trees.Tiny.TinyParentNodeImpl tiny && tiny.tree.TypeArray == null)
                    {
                        Trees.Tiny.TinyTree tree = tiny.tree;
                        short[] d = tree.depth;
                        int stop = d[tiny.nodeNr];
                        int nn = tree.numberOfNodes;
                        long total = 0;
                        bool any = false;
                        for (int n = tiny.nodeNr + 1; n < nn && d[n] > stop; n++)
                        {
                            if (Expressions.Elaboration.FusedLeafFilter.IsLeafElement(tree, n))
                            {
                                total += Trees.Tiny.TinyParentNodeImpl.GetStringValueLength(tree, n);
                                any = true;
                            }
                        }

                        return any ? Int64Value.MakeIntegerValue(total) : zero.Eval(context);
                    }

                    return genericSum.Eval(context);
                };
            }

            // sum(m to n) with a statically visible range: evaluate the two bounds as single
            // items and run the closed form (IntegerRange.TrySum) — without constructing the
            // range iterator per call at all. Empty bound or m > n is the empty range (the
            // zero argument); non-Int64 bounds or overflow fall back to the generic fold,
            // which reproduces the BigInteger promotion.
            private static IItemEvaluator TryRangeBoundsSum(SystemFunctionCall fnc, IPullEvaluator puller, IItemEvaluator zero)
            {
                if (!(fnc.GetArg(0) is RangeExpression range)
                    || ErrorExpression.IsContainedIn(range.StartExpression)
                    || ErrorExpression.IsContainedIn(range.EndExpression))
                {
                    return null;
                }

                IItemEvaluator loEval = range.StartExpression.MakeElaborator().ElaborateForItem();
                IItemEvaluator hiEval = range.EndExpression.MakeElaborator().ElaborateForItem();
                IItemEvaluator genericSum = BuildGenericSum(puller, zero);
                return (context) =>
                {
                    IItem lo = loEval.Eval(context);
                    IItem hi = hiEval.Eval(context);
                    if (lo == null || hi == null)
                    {
                        return zero.Eval(context);
                    }

                    if (lo is Int64Value l64 && hi is Int64Value h64)
                    {
                        long m = l64.LongValue();
                        long n = h64.LongValue();
                        if (m > n)
                        {
                            return zero.Eval(context);
                        }

                        if (IntegerRange.TrySum(m, 1, n, out long total))
                        {
                            return Int64Value.MakeIntegerValue(total);
                        }
                    }

                    return genericSum.Eval(context);
                };
            }

            private static IItemEvaluator BuildGenericSum(IPullEvaluator puller, IItemEvaluator zero)
            {
                return (context) =>
                {
                    ISequenceIterator iter = puller.Iterate(context);

                    if (iter is Text.CodepointIterator cpi)
                    {
                        // sum(string-to-codepoints(...)): fold the raw ints — no Int64Value per
                        // character. Total stays far below long overflow (codepoints <= 0x10FFFF,
                        // count < 2^31), and a sum of xs:integer is the same Int64Value the
                        // generic fold would produce; an empty stream returns the zero argument.
                        Collections.IIntIterator cps = cpi.RawCodepoints;
                        long total = 0;
                        bool any = false;
                        while (cps.MoveNext())
                        {
                            total += cps.Current;
                            any = true;
                        }

                        return any ? Int64Value.MakeIntegerValue(total) : zero.Eval(context);
                    }

                    // sum(m to n): closed-form arithmetic series — no per-step Int64Value.
                    // Overflow (or a partially-consumed iterator) falls through to the generic
                    // fold, which reproduces the BigInteger promotion.
                    if (iter is Expressions.AscendingRangeIterator ari && ari.TryComputeTotal(out long rangeTotal))
                    {
                        return Int64Value.MakeIntegerValue(rangeTotal);
                    }

                    if (iter is Expressions.DescendingRangeIterator dri && dri.TryComputeTotal(out long rangeTotal2))
                    {
                        return Int64Value.MakeIntegerValue(rangeTotal2);
                    }

                    SumFold fold = new SumFold(context, (AtomicValue)zero.Eval(context));
                    for (IItem it; (it = iter.Next()) != null;)
                    {
                        fold.ProcessItem(it);
                    }

                    return fold.Result().Head();
                };
            }
        }
    }
}