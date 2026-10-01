////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    public class SystemFunctionCall : StaticFunctionCall, INegatable
    {

        //    }
        public new SystemFunction TargetFunction => (SystemFunction)base.TargetFunction;

        public override int IntrinsicDependencies
        {
            get
            {
                int properties = TargetFunction.Details.properties;
                int dep = 0;
                if ((properties & BuiltInFunctionSet.LATE) != 0)
                {
                    dep = StaticProperty.DEPENDS_ON_RUNTIME_ENVIRONMENT;
                }

                if ((properties & BuiltInFunctionSet.FOCUS) != 0)
                {
                    if ((properties & BuiltInFunctionSet.CDOC) != 0)
                    {
                        dep |= StaticProperty.DEPENDS_ON_CONTEXT_DOCUMENT;
                    }

                    if ((properties & BuiltInFunctionSet.CITEM) != 0)
                    {
                        dep |= StaticProperty.DEPENDS_ON_CONTEXT_ITEM;
                    }

                    if ((properties & BuiltInFunctionSet.POSN) != 0)
                    {
                        dep |= StaticProperty.DEPENDS_ON_POSITION;
                    }

                    if ((properties & BuiltInFunctionSet.LAST) != 0)
                    {
                        dep |= StaticProperty.DEPENDS_ON_LAST;
                    }
                }

                if ((properties & BuiltInFunctionSet.BASE) != 0)
                {
                    dep |= StaticProperty.DEPENDS_ON_STATIC_CONTEXT;
                }

                if ((properties & BuiltInFunctionSet.DCOLL) != 0)
                {
                    dep |= StaticProperty.DEPENDS_ON_STATIC_CONTEXT;
                }

                if (IsCallOn(typeof(RegexGroup)) || IsCallOn(typeof(CurrentMergeGroup)) || IsCallOn(typeof(CurrentMergeKey)))
                {
                    dep |= StaticProperty.DEPENDS_ON_CURRENT_GROUP;
                }

                return dep;
            }
        }

        public override int NetCost => TargetFunction.NetCost;

        public override Expression ScopingExpression
        {
            get
            {
                if (IsCallOn(typeof(RegexGroup)))
                {
                    Expression parent = ParentExpression;
                    while (parent != null)
                    {
                        if (parent is AnalyzeString)
                        {
                            return parent;
                        }

                        parent = parent.ParentExpression;
                    }

                    return null;
                }
                else
                {
                    return base.ScopingExpression;
                }
            }
        }

        //    }
        public override IntegerValue[] IntegerBounds
        {
            get
            {
                SystemFunction fn = TargetFunction;
                if ((fn.Details.properties & BuiltInFunctionSet.FILTER) != 0)
                {
                    return GetArg(0).IntegerBounds;
                }

                return fn.IntegerBounds;
            }
        }

        public override string ExpressionName => "sysFuncCall";
        public SystemFunctionCall(SystemFunction target, Expression[] arguments) : base(target, arguments)
        {
        }

        public override void SetRetainedStaticContext(RetainedStaticContext rsc)
        {
            base.SetRetainedStaticContext(rsc);
            TargetFunction.SetRetainedStaticContext(rsc);
        }

        public override Expression PreEvaluate(ExpressionVisitor visitor)
        {
            SystemFunction target = TargetFunction;
            if ((target.Details.properties & BuiltInFunctionSet.LATE) == 0)
            {
                return base.PreEvaluate(visitor);
            }
            else
            {

                // Early evaluation of this function is suppressed
                return this;
            }
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            CheckFunctionCall(TargetFunction, visitor);

            // Give the function an opportunity to use the type information now available
            TargetFunction.SupplyTypeInformation(visitor, contextInfo, Arguments);
            if ((TargetFunction.Details.properties & BuiltInFunctionSet.LATE) == 0)
            {
                return PreEvaluateIfConstant(visitor);
            }

            return this;
        }

        protected override int ComputeCardinality()
        {
            return TargetFunction.GetCardinality(Arguments);
        }

        protected override int ComputeSpecialProperties()
        {
            return TargetFunction.GetSpecialProperties(Arguments);
        }

        public override bool IsLiftable(bool forStreaming)
        {

            // xsl:map-entry is not liftable when streaming because of the special streamability
            // rules for xsl:map; similarly XPath map constructor expressions.
            // The tests for current-merge-group/key were added to fix bug 3652 - it seems
            // an inelegant solution because it's being handled differently from other context
            // dependencies, but it works.
            return base.IsLiftable(forStreaming) && !IsCallOn(typeof(CurrentMergeGroup)) && !IsCallOn(typeof(CurrentMergeKey)) && (!forStreaming || !IsCallOn(typeof(MapFunctionSet.MapEntry)));
        }

        //    }
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            int properties = TargetFunction.Details.properties;
            if ((properties & BuiltInFunctionSet.CTRL) != 0)
            {

                // Mechanism devised for saxon:unindexed: don't optimize the arguments first
                Expression sfo = TargetFunction.MakeOptimizedFunctionCall(visitor, contextInfo, Arguments);
                if (sfo != null)
                {
                    sfo.ParentExpression = ParentExpression;
                    ExpressionTool.CopyLocationInfo(this, sfo);

                    //                if (sfo instanceof SystemFunctionCall) {
                    //                }
                    return sfo;
                }
            }

            Expression sf = base.Optimize(visitor, contextInfo);
            if (sf == this)
            {

                // Give the function an opportunity to regenerate the function call, with more information about
                // the types of the arguments than was previously available
                Expression sfo = TargetFunction.MakeOptimizedFunctionCall(visitor, contextInfo, Arguments);
                if (sfo != null)
                {
                    sfo.ParentExpression = ParentExpression;
                    ExpressionTool.CopyLocationInfo(this, sfo);

                    return sfo;
                }
            }

            Optimizer opt = visitor.ObtainOptimizer();
            if (sf is SystemFunctionCall && opt.IsOptionSet(OptimizerOptions.CONSTANT_FOLDING))
            {

                // If any arguments are known to be empty, pre-evaluate the result
                BuiltInFunctionSet.Entry details = ((SystemFunctionCall)sf).TargetFunction.Details;
                if ((details.properties & BuiltInFunctionSet.UO) != 0)
                {

                    // First argument does not need to be in any particular order
                    SetArg(0, GetArg(0).Unordered(true, visitor.IsOptimizeForStreaming()));
                }

                if (GetArity() <= details.resultIfEmpty.Length)
                {

                    // the condition eliminates concat, which is a special case.
                    for (int i = 0; i < GetArity(); i++)
                    {
                        if (Literal.IsEmptySequence(GetArg(i)) && details.resultIfEmpty[i] != null)
                        {
                            return Literal.MakeLiteral(details.resultIfEmpty[i].Materialize(), this);
                        }
                    }
                } //((SystemFunctionCall) sf).allocateArgumentEvaluators(((SystemFunctionCall) sf).getArguments());
            }

            return sf;
        }

        //    }
        public override bool IsVacuousExpression()
        {
            return IsCallOn(typeof(Exception));
        }

        public override ItemType GetItemType()
        {
            return TargetFunction.GetResultItemType(Arguments);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Expression[] args = new Expression[GetArity()];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = GetArg(i).Copy(rebindings);
            }

            SystemFunction target = TargetFunction;
            if (target is IStatefulSystemFunction)
            {
                target = ((IStatefulSystemFunction)target).Copy();
            }

            Expression e = target.MakeFunctionCall(args);
            e.SetLocation(GetLocation());
            e.SetRetainedStaticContext(GetRetainedStaticContext());
            return e;
        }

        public bool IsNegatable(TypeHierarchy th)
        {
            return IsCallOn(typeof(NotFn)) || IsCallOn(typeof(BooleanFn)) || IsCallOn(typeof(Empty)) || IsCallOn(typeof(Exists));
        }

        public Expression Negate()
        {
            SystemFunction fn = TargetFunction;
            if (fn is NotFn)
            {
                Expression arg = GetArg(0);
                if (arg.GetItemType() == BuiltInAtomicType.BOOLEAN && arg.GetCardinality() == StaticProperty.EXACTLY_ONE)
                {
                    return arg;
                }
                else
                {
                    return SystemFunction.MakeCall("boolean", GetRetainedStaticContext(), arg);
                }
            }
            else if (fn is BooleanFn)
            {
                return SystemFunction.MakeCall("not", GetRetainedStaticContext(), GetArg(0));
            }
            else if (fn is Exists)
            {
                return SystemFunction.MakeCall("empty", GetRetainedStaticContext(), GetArg(0));
            }
            else if (fn is Empty)
            {
                return SystemFunction.MakeCall("exists", GetRetainedStaticContext(), GetArg(0));
            }

            throw new NotSupportedException();
        }

        public override Expression Unordered(bool retainAllNodes, bool forStreaming)
        {
            SystemFunction fn = TargetFunction;
            if (fn is Reverse)
            {
                return GetArg(0);
            }

            if (fn is TreatFn)
            {
                SetArg(0, GetArg(0).Unordered(retainAllNodes, forStreaming));
            }

            return this;
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            if (IsCallOn(typeof(Doc)) || IsCallOn(typeof(DocumentFn)) || IsCallOn(typeof(CollectionFn)))
            {
                GetArg(0).AddToPathMap(pathMap, pathMapNodeSet);
                return new PathMap.PathMapNodeSet(pathMap.MakeNewRoot(this));
            }
            else if (IsCallOn(typeof(KeyFn)))
            {
                return ((KeyFn)TargetFunction).AddToPathMap(pathMap, pathMapNodeSet);
            }
            else
            {
                return base.AddToPathMap(pathMap, pathMapNodeSet);
            }
        }

        public override Patterns.Pattern ToPattern(Configuration config)
        {
            SystemFunction fn = TargetFunction;
            if (fn is Root_1)
            {
                if (GetArg(0) is ContextItemExpression || (GetArg(0) is ItemChecker && ((ItemChecker)GetArg(0)).BaseExpression is ContextItemExpression))
                {
                    return new NodeSetPattern(this);
                }
            }

            return base.ToPattern(config);
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return base.Call(context, arguments);
        }

        public override bool IsUpdatingExpression()
        {
            // XQuery Update (fn:put) is not supported: the old check compared against an empty
            // stub type that no code ever created, so this was already constant false.
            return false;
        }

        public override void Export(ExpressionPresenter @out)
        {
            if (GetFunctionName().HasURI(NamespaceUri.FN))
            {
                @out.StartElement("fn", this);
                string localPart = GetFunctionName().GetLocalPart();
                @out.EmitAttribute("name", localPart);
                TargetFunction.ExportAttributes(@out);
                if (localPart.Equals("concat") && "JS".Equals(@out.GetOptions().target) && @out.GetOptions().targetVersion >= 2 && GetArity() == 1 && GetArg(0) is Block)
                {

                    // We've reduced concat to a single sequence-valued argument; now we need to spread it out to multiple
                    // arguments. See bug #5383
                    foreach (Operand o in GetArg(0).Operands())
                    {
                        if (o.GetChildExpression() is Literal)
                        {
                            foreach (IItem it in ((Literal)o.GetChildExpression()).GroundedValue.AsIterable())
                            {
                                Literal.ExportValue(it, @out);
                            }
                        }
                        else
                        {
                            o.GetChildExpression().Export(@out);
                        }
                    }
                }
                else
                {
                    foreach (Operand o in Operands())
                    {
                        o.GetChildExpression().Export(@out);
                    }
                }

                TargetFunction.ExportAdditionalArguments(this, @out);
                @out.EndElement();
            }
            else
            {

                // Function was implemented as an IntegratedFunctionCall in 9.7 and we retain the same export format
                @out.StartElement("ifCall", this);
                @out.EmitAttribute("name", GetFunctionName());
                @out.EmitAttribute("type", TargetFunction.FunctionItemType.ResultType.ToAlphaCode());
                TargetFunction.ExportAttributes(@out);
                foreach (Operand o in Operands())
                {
                    o.GetChildExpression().Export(@out);
                }

                TargetFunction.ExportAdditionalArguments(this, @out);
                @out.EndElement();
            }
        }

        public override Elaborator GetElaborator()
        {
            SystemFunction fn = TargetFunction;
            Elaborator fnElaborator = fn.GetElaborator();

            if (fnElaborator != null)
            {
                return fnElaborator;
            }
            else
            {
                return new SystemFunctionCallElaborator();
            }
        }

        internal abstract class Optimized : SystemFunctionCall
        {
            public Optimized(SystemFunction target, Expression[] arguments) : base(target, arguments)
            {
            }

            public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
            {
                return this; // prevent infinite optimization
            }
        }

        /// <summary>
        /// Elaborator for a system function call, used in cases where the specific function call has no custom support
        /// </summary>
        internal sealed class SystemFunctionCallElaborator : FunctionCallElaborator
        {
            public override void SetExpression(Expression expr)
            {
                base.SetExpression(expr);
                AllocateArgumentEvaluators((FunctionCall)expr, false);
            }

            public override IPullEvaluator ElaborateForPull()
            {
                SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                SystemFunction fn = expr.TargetFunction;
                switch (argumentEvaluators.Length)
                {
                    case 0:
                        return (context) =>
                        {
                            try
                            {
                                return fn.Call(context, StackFrame.EMPTY_ARRAY_OF_SEQUENCE).Iterate();
                            }
                            catch (XPathException err)
                            {
                                throw err.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                            }
                        };
                    case 1:
                        return (context) =>
                        {
                            try
                            {
                                return fn.Call(context, new ISequence[] { argumentEvaluators[0].Evaluate(context) }).Iterate();
                            }
                            catch (XPathException err)
                            {
                                throw err.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                            }
                        };
                    case 2:
                        return (context) =>
                        {
                            try
                            {
                                return fn.Call(context, new ISequence[] { argumentEvaluators[0].Evaluate(context), argumentEvaluators[1].Evaluate(context) }).Iterate();
                            }
                            catch (XPathException err)
                            {
                                throw err.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                            }
                        };
                    default:
                        return (context) =>
                        {
                            try
                            {
                                return fn.Call(context, EvaluateArguments(context)).Iterate();
                            }
                            catch (XPathException err)
                            {
                                throw err.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                            }
                        };
                }
            }

            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                SystemFunction fn = expr.TargetFunction;
                switch (argumentEvaluators.Length)
                {
                    case 0:
                        return (context) =>
                        {
                            try
                            {
                                return fn.Call(context, StackFrame.EMPTY_ARRAY_OF_SEQUENCE).Head();
                            }
                            catch (XPathException e)
                            {
                                throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                            }
                        };
                    case 1:
                        return (context) =>
                        {
                            try
                            {
                                return fn.Call(context, new ISequence[] { argumentEvaluators[0].Evaluate(context) }).Head();
                            }
                            catch (XPathException e)
                            {
                                throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                            }
                        };
                    case 2:
                        return (context) =>
                        {
                            try
                            {
                                return fn.Call(context, new ISequence[] { argumentEvaluators[0].Evaluate(context), argumentEvaluators[1].Evaluate(context) }).Head();
                            }
                            catch (XPathException e)
                            {
                                throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                            }
                        };
                    default:
                        return (context) =>
                        {
                            try
                            {
                                return fn.Call(context, EvaluateArguments(context)).Head();
                            }
                            catch (XPathException e)
                            {
                                throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                            }
                        };
                }
            }

            public override IPushEvaluator ElaborateForPush()
            {
                SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                SystemFunction fn = expr.TargetFunction;
                if (fn is IPushableFunction)
                {
                    return (output, context) =>
                    {
                        ISequence[] actualArgs = EvaluateArguments(context);
                        try
                        {
                            ((IPushableFunction)fn).Process(output, context, actualArgs);
                        }
                        catch (XPathException e)
                        {
                            throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithFailingExpression(expr).MaybeWithContext(context);
                        }

                        return null;
                    };
                }
                else
                {
                    return base.ElaborateForPush();
                }
            }

            public override IUpdateEvaluator ElaborateForUpdate()
            {
                SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                if (expr.IsVacuousExpression())
                {

                    // typically, a call on fn:error
                    IPullEvaluator eval = ElaborateForPull();
                    return (context, pul) =>
                    {
                        eval.Iterate(context).Next();
                    };
                }
                else
                {
                    throw new NotSupportedException("Expression " + expr.ToShortString() + " is not an updating expression");
                }
            }
        }
    }
}