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
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    internal abstract class PositionAndLast : ContextAccessorFunction
    {
        private bool contextPossiblyUndefined = true;
        public override int NetCost => 0;

        public override IntegerValue[] IntegerBounds => new IntegerValue[]
            {
                Int64Value.PLUS_ONE,
                Expression.MAX_SEQUENCE_LENGTH
            };

        public override IFunctionItem BindContext(IXPathContext context)
        {
            Int64Value value;
            try
            {
                value = EvaluateItem(context);
            }
            catch (XPathException e)
            {

                // This happens when we do a dynamic lookup of position() or last() when there is no context item
                SymbolicName.F name = new SymbolicName.F(GetFunctionName(), GetArity());
                ICallable callable = new CallableDelegate((context1, arguments) =>
                {
                    throw e;
                });
                return new CallableFunction(name, callable, FunctionItemType);
            }

            ConstantFunction fn = new ConstantFunction(value);
            fn.Details = Details;
            fn.SetRetainedStaticContext(GetRetainedStaticContext());
            return fn;
        }

        public override void SupplyTypeInformation(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, Expression[] arguments)
        {
            base.SupplyTypeInformation(visitor, contextInfo, arguments);
            if (contextInfo.GetItemType() == ErrorType.GetInstance())
            {
                throw new XPathException("The context item is absent at this point", "XPDY0002");
            }
            else
            {
                contextPossiblyUndefined = contextInfo.IsPossiblyAbsent();
            }
        }

        public virtual bool IsContextPossiblyUndefined()
        {
            return contextPossiblyUndefined;
        }

        public abstract Int64Value EvaluateItem(IXPathContext c);
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return EvaluateItem(context);
        }

        internal sealed class Position : PositionAndLast
        {
            public override Int64Value EvaluateItem(IXPathContext c)
            {
                IFocusIterator currentIterator = c.GetCurrentIterator();
                if (currentIterator == null)
                {
                    throw new XPathException("The context item is absent, so position() is undefined").WithXPathContext(c).WithErrorCode("XPDY0002");
                }

                return Int64Value.MakeIntegerValue(currentIterator.Position());
            }

            public override Elaborator GetElaborator()
            {
                return new PositionFnElaborator();
            }

            internal sealed class PositionFnElaborator : ItemElaborator
            {
                public override IItemEvaluator ElaborateForItem()
                {
                    SystemFunctionCall sfc = (SystemFunctionCall)GetExpression();
                    Position fn = (Position)sfc.TargetFunction;
                    if (fn.IsContextPossiblyUndefined())
                    {
                        return (context) =>
                        {
                            IFocusIterator focus = context.GetCurrentIterator();
                            if (focus == null)
                            {
                                throw new XPathException("The context item is absent, so position() is undefined").WithXPathContext(context).WithLocation(sfc.GetLocation()).WithErrorCode("XPDY0002");
                            }

                            return Int64Value.MakeIntegerValue(focus.Position());
                        };
                    }
                    else
                    {
                        return (context) => Int64Value.MakeIntegerValue(context.GetCurrentIterator().Position());
                    }
                }
            }
        }

        internal sealed class Last : PositionAndLast
        {

            public override string StreamerName => "Last";
            public override Int64Value EvaluateItem(IXPathContext c)
            {
                try
                {
                    return Int64Value.MakeIntegerValue(c.GetLast());
                }
                catch (UncheckedXPathException e)
                {
                    throw XPathException.MakeXPathException(e);
                }
            }

            public override Elaborator GetElaborator()
            {
                return new LastFnElaborator();
            }

            internal sealed class LastFnElaborator : ItemElaborator
            {
                public override IItemEvaluator ElaborateForItem()
                {
                    SystemFunctionCall sfc = (SystemFunctionCall)GetExpression();
                    Last fn = (Last)sfc.TargetFunction;
                    if (fn.IsContextPossiblyUndefined())
                    {
                        return (context) =>
                        {
                            IFocusIterator focus = context.GetCurrentIterator();
                            if (focus == null)
                            {
                                throw new XPathException("The context item is absent, so last() is undefined").WithXPathContext(context).WithLocation(sfc.GetLocation()).WithErrorCode("XPDY0002");
                            }

                            return Int64Value.MakeIntegerValue(context.GetLast());
                        };
                    }
                    else
                    {
                        return (context) => Int64Value.MakeIntegerValue(context.GetLast());
                    }
                }
            }
        }
    }
}
