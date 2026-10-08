////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the XPath function boolean()
    /// </summary>
    internal sealed class BooleanFn : SystemFunction
    {

        public override string StreamerName => "BooleanFn";
        public override void SupplyTypeInformation(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType, Expression[] arguments)
        {
            XPathException err = TypeChecker.EbvError(arguments[0], visitor.GetConfiguration().GetTypeHierarchy());
            if (err != null)
            {
                throw err;
            }
        }

        public static Expression RewriteEffectiveBooleanValue(Expression exp, ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            bool forStreaming = visitor.IsOptimizeForStreaming();
            Expression e2 = ExpressionTool.UnsortedIfHomogeneous(exp, forStreaming);
            bool changed = e2 != exp;
            exp = e2;
            if (exp is Literal)
            {
                IGroundedValue val = ((Literal)exp).GroundedValue;
                if (val is BooleanValue)
                {
                    return exp;
                }

                return Literal.MakeLiteral(BooleanValue.Get(ExpressionTool.EffectiveBooleanValue(val.Iterate())), exp);
            }

            if (exp is ValueComparison)
            {
                ValueComparison vc = (ValueComparison)exp;
                if (vc.ResultWhenEmpty == null)
                {
                    vc.ResultWhenEmpty = BooleanValue.FALSE;
                }

                return exp;
            }
            else if (exp.IsCallOn(typeof(BooleanFn)))
            {
                return ((SystemFunctionCall)exp).GetArg(0);
            }
            else if (th.IsSubType(exp.GetItemType(), BuiltInAtomicType.BOOLEAN) && exp.GetCardinality() == StaticProperty.EXACTLY_ONE)
            {
                return exp;
            }
            else if (exp.IsCallOn(typeof(Count)))
            {

                // rewrite boolean(count(x)) => exists(x)
                Expression exists = SystemFunction.MakeCall("exists", exp.GetRetainedStaticContext(), ((SystemFunctionCall)exp).GetArg(0));
                ExpressionTool.CopyLocationInfo(exp, exists);
                return exists.Optimize(visitor, contextItemType);
            }
            else if (exp.GetItemType() is NodeTest)
            {

                // rewrite boolean(x) => exists(x)
                Expression exists = SystemFunction.MakeCall("exists", exp.GetRetainedStaticContext(), exp);
                ExpressionTool.CopyLocationInfo(exp, exists);
                return exists.Optimize(visitor, contextItemType);
            }
            else
            {
                return changed ? exp : null;
            }
        }

        public override ISequence Call(IXPathContext c, ISequence[] arguments)
        {
            bool bValue = ExpressionTool.EffectiveBooleanValue(arguments[0].Iterate());
            return BooleanValue.Get(bValue);
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            return new AnonymousSystemFunctionCall(this, arguments);
        }

        public override Elaborator GetElaborator()
        {
            return new BooleanFnElaborator();
        }

        private sealed class AnonymousSystemFunctionCall : SystemFunctionCall
        {

            private readonly BooleanFn parent;
            public AnonymousSystemFunctionCall(BooleanFn parent, Expression[] arguments) : base(parent, arguments)
            {
                this.parent = parent;
            }
            public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
            {
                Expression e = base.Optimize(visitor, contextItemType);
                if (e == this)
                {
                    Expression ebv = RewriteEffectiveBooleanValue(this.GetArg(0), visitor, contextItemType);
                    if (ebv != null)
                    {
                        ebv = ebv.Optimize(visitor, contextItemType);
                        if (ebv.GetItemType() == BuiltInAtomicType.BOOLEAN && ebv.GetCardinality() == StaticProperty.EXACTLY_ONE)
                        {
                            ebv.ParentExpression = ParentExpression;
                            return ebv;
                        }
                        else
                        {
                            SetArg(0, ebv);
                            AdoptChildExpression(ebv);
                            return this;
                        }
                    }
                }

                return e;
            }

            public override bool EffectiveBooleanValue(IXPathContext c)
            {
                try
                {
                    return GetArg(0).EffectiveBooleanValue(c);
                }
                catch (XPathException e)
                {
                    throw e.MaybeWithLocation(GetLocation()).MaybeWithContext(c);
                }
            }

            public override IItem EvaluateItem(IXPathContext context)
            {
                return BooleanValue.Get(EffectiveBooleanValue(context));
            }
        }

        internal sealed class BooleanFnElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                Expression arg = fnc.GetArg(0);
                return arg.MakeElaborator().ElaborateForBoolean();
            }
        }
    }
}
