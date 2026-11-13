////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implementation of the fn:avg function
    /// </summary>
    internal sealed class Average : FoldingFunction
    {
        public override int GetCardinality(Expression[] arguments)
        {
            if (!Cardinality.AllowsZero(arguments[0].GetCardinality()))
            {
                return StaticProperty.EXACTLY_ONE;
            }
            else
            {
                return base.GetCardinality(arguments);
            }
        }


        public override Elaborator GetElaborator()
        {
            return new AverageElaborator();
        }

        private sealed class AverageElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                IItemEvaluator nodes = TryNodeFold(fnc, context => new AverageFold(context), true);
                if (nodes != null)
                {
                    return nodes;
                }

                IPullEvaluator puller = fnc.GetArg(0).MakeElaborator().ElaborateForPull();
                return (context) => RunFold(new AverageFold(context), puller.Iterate(context)).Head();
            }
        }

        public override IFold GetFold(IXPathContext context, params ISequence[] additionalArguments)
        {
            return new AverageFold(context);
        }

        private sealed class AverageFold : IUntypedDoubleFold
        {
            private readonly IXPathContext context;
            private AtomicValue data;
            private bool atStart = true;
            private readonly ConversionRules rules;
            private readonly StringConverter toDouble;
            private int count;
            // Unboxed totals for a run of xs:integer or xs:double values (untypedAtomic converts to double), as SumFold
            // keeps them; any other value, or overflow, puts the total back into data.
            private long longTotal;
            private bool onLongPath;
            private double dblTotal;
            private bool onDoublePath;
            public AverageFold(IXPathContext context)
            {
                this.context = context;
                this.rules = context.GetConfiguration().GetConversionRules();
                this.toDouble = BuiltInAtomicType.DOUBLE.GetStringConverter(rules);
            }

            public void ProcessUntypedDouble(double d)
            {
                if (onDoublePath)
                {
                    count++;
                    dblTotal += d;
                    return;
                }

                ProcessItem(new DoubleValue(d));
            }

            public void ProcessItem(IItem item)
            {
                AtomicValue next = (AtomicValue)item;
                if (onLongPath)
                {
                    if (next is Int64Value iv)
                    {
                        try
                        {
                            longTotal = checked(longTotal + iv.LongValue());
                            count++;
                            return;
                        }
                        catch (System.OverflowException)
                        {
                            // the generic path promotes to a big integer
                        }
                    }

                    onLongPath = false;
                    data = Int64Value.MakeIntegerValue(longTotal);
                }
                else if (onDoublePath)
                {
                    if (next.IsUntypedAtomic())
                    {
                        next = toDouble.Convert(next).AsAtomic();
                    }

                    if (next is DoubleValue nd)
                    {
                        count++;
                        dblTotal += nd.GetDoubleValue();
                        return;
                    }

                    onDoublePath = false;
                    data = new DoubleValue(dblTotal);
                }
                else if (!atStart && data is Int64Value di && next is Int64Value ni)
                {
                    try
                    {
                        longTotal = checked(di.LongValue() + ni.LongValue());
                        onLongPath = true;
                        count++;
                        return;
                    }
                    catch (System.OverflowException)
                    {
                        // generic path below
                    }
                }
                else if (!atStart && data is DoubleValue dd)
                {
                    AtomicValue nx = next.IsUntypedAtomic() ? toDouble.Convert(next).AsAtomic() : next;
                    if (nx is DoubleValue ndd)
                    {
                        dblTotal = dd.GetDoubleValue() + ndd.GetDoubleValue();
                        onDoublePath = true;
                        count++;
                        return;
                    }
                }

                if (next.IsUntypedAtomic())
                {
                    next = toDouble.Convert(next).AsAtomic();
                }

                count++;
                if (atStart)
                {
                    if (next is NumericValue || next is DayTimeDurationValue || next is YearMonthDurationValue)
                    {
                        data = next;
                        atStart = false;
                    }
                    else if (next is DurationValue)
                    {
                        throw new XPathException("Input to avg() contains a duration (" + Err.Depict(next) + ") that is neither an xs:dayTimeDuration nor an xs:yearMonthDuration", "FORG0006");
                    }
                    else
                    {
                        throw new XPathException("Input to avg() contains a value (" + Err.Depict(next) + ") that is neither numeric, nor a duration", "FORG0006");
                    }
                }
                else
                {
                    if (data is NumericValue)
                    {
                        if (!(next is NumericValue))
                        {
                            throw new XPathException("Input to avg() contains a mix of numeric and non-numeric values", "FORG0006");
                        }

                        data = ArithmeticExpression.Compute(data, Calculator.PLUS, next, context);
                    }
                    else if (data is DurationValue)
                    {
                        if (!(next is DurationValue))
                        {
                            throw new XPathException("Input to avg() contains a mix of duration and non-duration values", "FORG0006");
                        }

                        try
                        {
                            data = ((DurationValue)data).Add((DurationValue)next);
                        }
                        catch (XPathException e)
                        {
                            throw e.ReplacingErrorCode("XPTY0004", "FORG0006");
                        }
                    }
                    else
                    {
                        throw new XPathException("Input to avg() contains a value (" + Err.Depict(data) + ") that is neither numeric, nor a duration", "FORG0006");
                    }
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
                if (onLongPath)
                {
                    onLongPath = false;
                    data = Int64Value.MakeIntegerValue(longTotal);
                }
                else if (onDoublePath)
                {
                    onDoublePath = false;
                    data = new DoubleValue(dblTotal);
                }

                if (atStart)
                {
                    return EmptySequence.GetInstance();
                }
                else if (data is NumericValue)
                {
                    return ArithmeticExpression.Compute(data, Calculator.DIV, new Int64Value(count), context);
                }
                else
                {
                    return ((DurationValue)data).Divide(count);
                }
            }
        }
    }
}