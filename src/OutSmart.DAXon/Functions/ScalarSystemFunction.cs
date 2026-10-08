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
namespace OutSmart.DAXon.Functions
{
    public abstract class ScalarSystemFunction : SystemFunction
    {
        public abstract AtomicValue Evaluate(IItem arg, IXPathContext context);
        public virtual ISequence ResultWhenEmpty()
        {
            return EmptySequence.GetInstance();
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IItem val0 = arguments[0].Head();
            if (val0 == null)
            {
                return ResultWhenEmpty();
            }

            return SequenceTool.ItemOrEmpty(Evaluate(val0, context));
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            SystemFunctionCall call = new AnonymousSystemFunctionCall(this, arguments);
            call.SetRetainedStaticContext(GetRetainedStaticContext());
            return call;
        }

        // Fused item-level call for arity-1 scalars: arg → Evaluate, no ISequence[]/LazySequence per call.
        // Subclasses with a custom elaborator (Abs, Number_1, StringLength_1, ...) override this again.
        public override Elaborator GetElaborator()
        {
            return new ScalarFunctionElaborator();
        }

        internal class ScalarFunctionElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                ScalarSystemFunction fn = (ScalarSystemFunction)fnc.TargetFunction;
                IItemEvaluator argEval = fnc.GetArg(0).MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    try
                    {
                        IItem val = argEval.Eval(context);
                        return val == null ? fn.ResultWhenEmpty().Head() : fn.Evaluate(val, context);
                    }
                    catch (XPathException e)
                    {
                        throw e.MaybeWithLocation(fnc.GetLocation()).MaybeWithContext(context);
                    }
                };
            }
        }

        private sealed class AnonymousSystemFunctionCall : SystemFunctionCall
        {

            private readonly ScalarSystemFunction parent;
            public AnonymousSystemFunctionCall(ScalarSystemFunction parent, Expression[] arguments) : base(parent, arguments)
            {
                this.parent = parent;
            }
            public override IItem EvaluateItem(IXPathContext context)
            {

                // cut out some of the call overhead
                IItem val = GetArg(0).EvaluateItem(context);
                if (val == null)
                {
                    return (AtomicValue)parent.ResultWhenEmpty().Head();
                }
                else
                {
                    return parent.Evaluate(val, context);
                }
            }
        }
    }
}