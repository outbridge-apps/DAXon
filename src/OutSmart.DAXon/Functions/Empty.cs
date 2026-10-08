////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implementation of the fn:empty function
    /// </summary>
    internal sealed class Empty : Aggregate
    {

        // Rewrite
        //    @Override
        //    public Expression makeFunctionCall(Expression[] arguments) {
        //        return new SystemFunctionCall(this, arguments) {
        //
        public override string StreamerName => "Empty";
        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {

            // See if we can deduce the answer from the cardinality
            int c = arguments[0].GetCardinality();
            if (!Cardinality.AllowsZero(c))
            {
                return Literal.MakeLiteral(BooleanValue.FALSE, arguments[0]);
            }
            else if (c == StaticProperty.ALLOWS_ZERO)
            {
                return Literal.MakeLiteral(BooleanValue.TRUE, arguments[0]);
            }


            // Don't sort the argument
            Expression unorderedArg0 = arguments[0].Unordered(false, visitor.IsOptimizeForStreaming());
            if (unorderedArg0 != arguments[0])
            {
                return MakeFunctionCall(unorderedArg0);
            }


            // Rewrite
            //    empty(A|B) => empty(A) and empty(B)
            if (arguments[0] is VennExpression && !visitor.IsOptimizeForStreaming())
            {
                VennExpression v = (VennExpression)arguments[0];
                if (v.Operator == Token.UNION)
                {
                    Expression e0 = SystemFunction.MakeCall("empty", GetRetainedStaticContext(), v.GetLhsExpression());
                    Expression e1 = SystemFunction.MakeCall("empty", GetRetainedStaticContext(), v.GetRhsExpression());
                    return new AndExpression(e0, e1).Optimize(visitor, contextInfo);
                }
            }

            return null;
        }

        // Rewrite
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return BooleanValue.Get(EmptyFn(arguments[0].Iterate()));
        }

        private static bool EmptyFn(ISequenceIterator iter)
        {
            bool result;
            if (iter is ILookaheadIterator && ((ILookaheadIterator)iter).SupportsHasNext())
            {
                result = !((ILookaheadIterator)iter).HasNext;
            }
            else
            {
                result = iter.Next() == null;
            }

            iter.Dispose();
            return result;
        }

        public override Elaborator GetElaborator()
        {
            return new EmptyFnElaborator();
        }

        private sealed class EmptyFnElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                Expression arg = fnc.GetArg(0);
                IPullEvaluator puller = arg.MakeElaborator().ElaborateForPull();
                return (context) => EmptyFn(puller.Iterate(context));
            }
        }
    }
}
