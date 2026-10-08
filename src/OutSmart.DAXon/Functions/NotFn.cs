////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the XPath functions boolean(), not(), true(), and false()
    /// </summary>
    internal sealed class NotFn : SystemFunction
    {

        public override string StreamerName => "NotFn";
        public override void SupplyTypeInformation(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType, Expression[] arguments)
        {
            XPathException err = TypeChecker.EbvError(arguments[0], visitor.GetConfiguration().GetTypeHierarchy());
            if (err != null)
            {
                throw err;
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return BooleanValue.Get(!ExpressionTool.EffectiveBooleanValue(arguments[0].Iterate()));
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            return new AnonymousSystemFunctionCall(this, arguments);
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            TypeHierarchy th = visitor.StaticContext.GetConfiguration().GetTypeHierarchy();
            if (arguments[0] is INegatable && ((INegatable)arguments[0]).IsNegatable(th))
            {
                return ((INegatable)arguments[0]).Negate();
            }

            if (arguments[0].GetItemType() is NodeTest)
            {
                SystemFunction empty = SystemFunction.MakeFunction("empty", GetRetainedStaticContext(), 1);
                return empty.MakeFunctionCall(arguments[0]).Optimize(visitor, contextInfo);
            }

            return null;
        }

        public override Elaborator GetElaborator()
        {
            return new NotFnElaborator();
        }

        private sealed class AnonymousSystemFunctionCall : SystemFunctionCall
        {

            private readonly NotFn parent;
            public AnonymousSystemFunctionCall(NotFn parent, Expression[] arguments) : base(parent, arguments)
            {
                this.parent = parent;
            }
            public override bool EffectiveBooleanValue(IXPathContext c)
            {
                try
                {
                    return !this.GetArg(0).EffectiveBooleanValue(c);
                }
                catch (XPathException e)
                {
                    throw e.MaybeWithLocation(this.GetLocation()).MaybeWithContext(c);
                }
            }
        }

        internal sealed class NotFnElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                Expression arg = fnc.GetArg(0);
                IBooleanEvaluator argEval = arg.MakeElaborator().ElaborateForBoolean();
                return (context) => !argEval.Eval(context);
            }
        }
    }
}
