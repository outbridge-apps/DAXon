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

        public override IFold GetFold(IXPathContext context, params ISequence[] additionalArguments)
        {
            return new AverageFold(context);
        }

        private sealed class AverageFold : IFold
        {
            private readonly IXPathContext context;
            private AtomicValue data;
            private bool atStart = true;
            private readonly ConversionRules rules;
            private readonly StringConverter toDouble;
            private int count = 0;
            public AverageFold(IXPathContext context)
            {
                this.context = context;
                this.rules = context.GetConfiguration().GetConversionRules();
                this.toDouble = BuiltInAtomicType.DOUBLE.GetStringConverter(rules);
            }

            public void ProcessItem(IItem item)
            {
                AtomicValue next = (AtomicValue)item;
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
                return data is DoubleValue && data.IsNaN();
            }

            public ISequence Result()
            {
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