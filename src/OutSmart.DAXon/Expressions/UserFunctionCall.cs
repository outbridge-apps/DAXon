////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// This class represents a call to a user-defined function in the stylesheet or query.
    /// </summary>
    public class UserFunctionCall : FunctionCall, IUserFunctionResolvable, IComponentInvocation, IContextOriginator
    {

        public const int NOT_TAIL_CALL = 0;
        public const int FOREIGN_TAIL_CALL = 1;
        public const int SELF_TAIL_CALL = 2;

        private const int UNHANDLED_DEPENDENCIES = StaticProperty.DEPENDS_ON_POSITION | StaticProperty.DEPENDS_ON_LAST | StaticProperty.DEPENDS_ON_XSLT_CONTEXT | StaticProperty.DEPENDS_ON_USER_FUNCTIONS;
        private SequenceType staticType;
        private UserFunction function;
        private int bindingSlot = -1;
        private int tailCall = NOT_TAIL_CALL;
        private StructuredQName name;
        private bool beingInlined = false;
        private volatile ISequenceEvaluator[] argumentEvaluators = null; // built once under lock, then read lock-free
        private UnboundFunctionLibrary.UnboundFunctionCallDetails unboundCallDetails;

        public int BindingSlot
        {
            get => bindingSlot; set
            {
                this.bindingSlot = value;
            }
        }

        public Component FixedTarget
        {
            get
            {
                Visibility v = function.DeclaringComponent.GetVisibility();
                if (v == Visibility.PRIVATE || v == Visibility.FINAL)
                {
                    return function.DeclaringComponent;
                }
                else
                {
                    return null;
                }
            }
        }

        public virtual UnboundFunctionLibrary.UnboundFunctionCallDetails UnboundCallDetails => unboundCallDetails;

        public virtual ISequenceEvaluator[] ArgumentEvaluators => argumentEvaluators;

        //
        //
        //
        //
        //    }
        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_USER_FUNCTIONS;

        //
        //
        //
        //
        //    }
        public override int ImplementationMethod
        {
            get
            {
                if (Cardinality.AllowsMany(GetCardinality()))
                {
                    return ITERATE_METHOD | PROCESS_METHOD;
                }
                else
                {
                    return EVALUATE_METHOD;
                }
            }
        }

        //
        //
        //
        //
        //    }
        public override string ExpressionName => "userFunctionCall";
        /// <summary>
        /// Create a function call to a user-written function in a query or stylesheet
        /// </summary>
        public UserFunctionCall()
        {
        }

        /// <summary>
        /// Create an unbound function call (typically, a forwards reference in XQuery)
        /// </summary>
        public UserFunctionCall(UnboundFunctionLibrary.UnboundFunctionCallDetails details)
        {
            this.unboundCallDetails = details;
        }
        public virtual bool IsBeingInlined()
        {
            return beingInlined;
        }

        public virtual void SetBeingInlined(bool beingInlined)
        {
            this.beingInlined = beingInlined;
        }

        public virtual void CopyFrom(UserFunctionCall ufc2)
        {
            staticType = ufc2.staticType;
            function = ufc2.function;
            bindingSlot = ufc2.bindingSlot;
            tailCall = ufc2.tailCall;
            name = ufc2.name;
            beingInlined = false;
            argumentEvaluators = ufc2.argumentEvaluators;
            unboundCallDetails = null;
        }

        public void SetFunctionName(StructuredQName name)
        {
            this.name = name;
        }

        public virtual void SetStaticType(SequenceType type)
        {
            staticType = type;
        }

        public void SetFunction(UserFunction compiledFunction)
        {
            function = compiledFunction;
        }

        public virtual UserFunction GetFunction()
        {
            return function;
        }

        public virtual bool IsTailCall()
        {
            return tailCall != NOT_TAIL_CALL;
        }

        public virtual bool IsRecursiveTailCall()
        {
            return tailCall == SELF_TAIL_CALL;
        }

        public override StructuredQName GetFunctionName()
        {
            if (name == null)
            {
                return function.GetFunctionName();
            }
            else
            {
                return name;
            }
        }

        public SymbolicName GetSymbolicName()
        {
            return new SymbolicName.F(GetFunctionName(), GetArity());
        }

        public virtual Component GetTarget()
        {
            return function.DeclaringComponent;
        }
        public virtual void AllocateArgumentEvaluators()
        {
            // Built into a local and published with a single volatile store: EvaluateArguments
            // reads the field without the lock, so it must never observe a half-filled array.
            ISequenceEvaluator[] evaluators = new ISequenceEvaluator[GetArity()];
            UserFunction target = GetFunction();
            int i = 0;
            foreach (Operand o in Operands())
            {
                Expression arg = o.GetChildExpression();
                if (arg is ErrorExpression && ((ErrorExpression)arg).ErrorCodeLocalPart.Equals("UseDefault"))
                {
                    arg = target.GetDefaultValueExpression(i).Copy(new RebindingMap());
                    o.SetChildExpression(arg);
                }

                if (i == 0 && target.DeclaredStreamability.IsConsuming())
                {
                    evaluators[i] = new StreamingArgumentEvaluator(arg);
                }
                else if (target.GetParameterDefinitions()[i].IsIndexedVariable())
                {
                    IPullEvaluator argPull = arg.MakeElaborator().ElaborateForPull();
                    evaluators[i] = new IndexedVariableEvaluator(argPull);
                }
                else if ((arg.Dependencies & UNHANDLED_DEPENDENCIES) != 0)
                {

                    // If the argument contains a call to a user-defined function, then it might be a recursive call.
                    // It's better to evaluate it now, rather than waiting until we are on a new stack frame, as
                    // that can blow the stack if done repeatedly. (See test func42)
                    // If the argument contains calls to position(), last(), regex-group(), current-group(),
                    // current-merge-group(), etc, then in general we can't save the values in a Closure
                    // so we need to evaluate the argument eagerly. (Tests position-0103, merge-096).
                    evaluators[i] = arg.MakeElaborator().Eagerly(); //            } else if (!Cardinality.allowsMany(arg.getCardinality()) && arg.getCost() < 20) {
                    //                // the argument is cheap to evaluate and doesn't use much memory...
                    //                argumentEvaluators[i] = new OptionalItemEvaluator(argEval);
                }
                else if (arg is Block && ((Block)arg).IsCandidateForSharedAppend())
                {

                    // If the expression is a Block, that is, it is appending a value to a sequence,
                    // then we have the opportunity to use a shared list underpinning the old value and
                    // the new. This takes precedence over lazy evaluation (it would be possible to do this
                    // lazily, but more difficult). We currently do this for any Block that has a variable
                    // reference as one of its subexpressions. The most common case is that the first argument is a reference
                    // to an argument of recursive function, where the recursive function returns the result of
                    // appending to the sequence.
                    evaluators[i] = new SharedAppendEvaluator((Block)arg);
                }
                else
                {
                    evaluators[i] = new LearningEvaluator(arg, arg.MakeElaborator().Lazily(true, false));
                }

                i++;
            }

            argumentEvaluators = evaluators;
        }

        //
        //
        //
        //
        //    }
        public override Expression PreEvaluate(ExpressionVisitor visitor)
        {
            return this;
        }

        public override ItemType GetItemType()
        {
            if (staticType == null)
            {

                // the actual type is not known yet, so we return an approximation
                return AnyItemType.GetInstance();
            }
            else
            {
                return staticType.PrimaryType;
            }
        }

        //
        //
        //
        //
        //    }
        public override UType GetStaticUType(UType contextItemType)
        {
            UserFunction f = GetFunction();
            if (f == null)
            {

                // Happens when called during parsing
                return UType.ANY;
            }

            return f.ResultType.PrimaryType.GetUType();
        }

        public override bool IsUpdatingExpression()
        {
            return function.IsUpdating();
        }

        protected override int ComputeSpecialProperties()
        {

            // Inherit the properties of the function being called if possible. But we have to prevent
            // looping when the function is recursive. For safety, we only consider the properties of the
            // function body if it contains no further function calls. Also, we can only do this safely if
            // the function is private or final
            if (function == null)
            {
                return base.ComputeSpecialProperties();
            }
            else if (function.GetBody() != null && (function.DeclaredVisibility == Visibility.PRIVATE || function.DeclaredVisibility == Visibility.FINAL))
            {
                int props;
                IList<UserFunction> calledFunctions = new List<UserFunction>();
                ExpressionTool.GatherCalledFunctions(function.GetBody(), calledFunctions);
                if (calledFunctions.Count == 0)
                {
                    props = function.GetBody().GetSpecialProperties();
                }
                else
                {
                    props = base.ComputeSpecialProperties();
                }

                if (function.GetDeterminism() != UserFunction.Determinism.PROACTIVE)
                {
                    props |= StaticProperty.NO_NODES_NEWLY_CREATED;
                }

                return props;
            }
            else
            {
                return base.ComputeSpecialProperties();
            }
        }

        //
        //
        //
        //
        //    }
        public override Expression Copy(RebindingMap rebindings)
        {
            if (function == null)
            {

                // not bound yet, we have no way to register the new copy with the XSLFunction
                throw new NotSupportedException("UserFunctionCall.copy()");
            }

            UserFunctionCall ufc = new UserFunctionCall();
            ufc.SetFunction(function);
            ufc.SetStaticType(staticType);
            ExpressionTool.CopyLocationInfo(this, ufc);
            int numArgs = GetArity();
            Expression[] a2 = new Expression[numArgs];
            for (int i = 0; i < numArgs; i++)
            {
                a2[i] = GetArg(i).Copy(rebindings);
            }

            ufc.Arguments = a2;
            return ufc;
        }

        //
        //
        //
        //
        //    }
        protected override int ComputeCardinality()
        {
            if (staticType == null)
            {

                // the actual type is not known yet, so we return an approximation
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
            else
            {
                return staticType.GetCardinality();
            }
        }

        //
        //
        //
        //
        //    }
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e = base.TypeCheck(visitor, contextInfo);
            if (e != this)
            {
                return e;
            }

            if (function != null)
            {
                CheckFunctionCall(function, visitor);
                if (staticType == null || staticType == SequenceType.ANY_SEQUENCE)
                {

                    // try to get a better type
                    staticType = function.ResultType;
                }
            }

            return this;
        }

        //
        //
        //
        //
        //    }
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Expression e = base.Optimize(visitor, contextItemType);
            if (e == this && function != null)
            {
                return visitor.ObtainOptimizer().TryInlineFunctionCall(this, visitor, contextItemType);
            }

            return e;
        }

        //
        //
        //
        //
        //    }
        public override void ResetLocalStaticProperties()
        {
            base.ResetLocalStaticProperties(); //argumentEvaluators = null;
        }

        //
        //
        //
        //
        //    }
        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            return AddExternalFunctionCallToPathMap(pathMap, pathMapNodeSet);
        }

        //
        //
        //
        //
        //    }
        public override int MarkTailFunctionCalls(StructuredQName qName, int arity)
        {
            tailCall = GetFunctionName().Equals(qName) && arity == GetArity() ? SELF_TAIL_CALL : FOREIGN_TAIL_CALL;
            return tailCall;
        }

        //
        //
        //
        //
        //    }
        public override IItem EvaluateItem(IXPathContext c)
        {
            return MakeElaborator().ElaborateForItem().Eval(c);
        }

        //
        //
        //
        //
        //    }
        public override ISequenceIterator Iterate(IXPathContext c)
        {
            return MakeElaborator().ElaborateForPull().Iterate(c);
        }

        //
        //
        //
        //
        //    }
        public override void Process(Outputter output, IXPathContext context)
        {
            ITailCall tc = MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context);
            DispatchTailCall(tc);
        }

        //
        //
        //
        //
        //    }
        public virtual Component GetTargetComponent(IXPathContext context)
        {
            if (bindingSlot == -1)
            {

                // fallback for non-package code
                return function.DeclaringComponent;
            }
            else
            {
                return context.GetTargetComponent(bindingSlot);
            }
        }

        //
        //
        //
        //
        //    }
        public override IFunctionItem GetTargetFunction(IXPathContext context)
        {
            return (UserFunction)GetTargetComponent(context).GetActor();
        }

        //
        //
        //
        //
        //    }
        public virtual ISequence[] EvaluateArguments(IXPathContext c, bool streamed)
        {
            int numArgs = GetArity();
            ISequence[] actualArgs = SequenceTool.MakeSequenceArray(numArgs);

            // Lock-free after first call: this runs once per function call, so a per-call
            // Monitor would stay on the hot path forever. The field is volatile and published
            // only once fully built.
            ISequenceEvaluator[] evaluators = argumentEvaluators;
            if (evaluators == null)
            {
                lock (syncLock)
                {
                    if (argumentEvaluators == null)
                    {
                        AllocateArgumentEvaluators();
                    }
                }

                evaluators = argumentEvaluators;
            }

            for (int i = 0; i < numArgs; i++)
            {
                ISequenceEvaluator eval = evaluators[i];
                if (eval == null || (eval is StreamingArgumentEvaluator && !streamed))
                {
                    eval = Arguments[0].MakeElaborator().Eagerly();
                }

                actualArgs[i] = eval.Evaluate(c);
            }

            return actualArgs;
        }

        /// <summary>
        /// Argument evaluators with the per-call fallbacks of EvaluateArguments(c, streamed:false)
        /// resolved once, for the elaborated direct-call path that binds arguments straight into
        /// the callee's frame.
        /// </summary>
        internal ISequenceEvaluator[] ResolvedArgumentEvaluators()
        {
            ISequenceEvaluator[] evaluators = argumentEvaluators;
            if (evaluators == null)
            {
                lock (syncLock)
                {
                    if (argumentEvaluators == null)
                    {
                        AllocateArgumentEvaluators();
                    }
                }

                evaluators = argumentEvaluators;
            }

            ISequenceEvaluator[] resolved = null;
            for (int i = 0; i < evaluators.Length; i++)
            {
                if (evaluators[i] == null || evaluators[i] is StreamingArgumentEvaluator)
                {
                    if (resolved == null)
                    {
                        resolved = (ISequenceEvaluator[])evaluators.Clone();
                    }

                    resolved[i] = Arguments[0].MakeElaborator().Eagerly();
                }
            }

            return resolved ?? evaluators;
        }

        //
        //
        //
        //
        //    }
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("ufCall", this);
            if (GetFunctionName() != null)
            {
                @out.EmitAttribute("name", GetFunctionName());
                @out.EmitAttribute("tailCall", tailCall == NOT_TAIL_CALL ? "false" : tailCall == SELF_TAIL_CALL ? "self" : "foreign");
            }

            @out.EmitAttribute("bSlot", "" + BindingSlot);
            foreach (Operand o in Operands())
            {
                o.GetChildExpression().Export(@out);
            }

            if (GetFunctionName() == null)
            {
                @out.SetChildRole("inline");
                function.GetBody().Export(@out);
                @out.EndElement();
            }

            @out.EndElement();
        }

        //
        //
        //
        //
        //    }
        public override object GetProperty(string name)
        {
            if (name.Equals("target"))
            {
                return function;
            }

            return base.GetProperty(name);
        }

        //
        //
        //
        //
        //    }
        public override StructuredQName GetObjectName()
        {
            return GetFunctionName();
        }

        //
        //
        //
        //
        //    }
        public override Elaborator GetElaborator()
        {
            if (IsTailCall())
            {
                return new TailCallElaborator();
            }
            else
            {
                return new UserFunctionCallElaborator();
            }
        }

        //
        //
        //
        //
        //    }
        private sealed class TailCallElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                UserFunctionCall expr = (UserFunctionCall)GetExpression();
                if (expr.bindingSlot >= 0)
                {
                    return (context) =>
                    {
                        TailCallLoop.TailCallComponent info = new TailCallLoop.TailCallComponent();
                        Component target = expr.GetTargetComponent(context);
                        info.component = target;
                        info.function = (UserFunction)target.GetActor();
                        if (target.IsHiddenAbstractComponent())
                        {
                            throw new XPathException("Cannot call an abstract function (" + expr.GetFunctionName().DisplayName + ") with no implementation", "XTDE3052");
                        }

                        ISequence[] actualArgs = expr.EvaluateArguments(context, false);
                        ((XPathContextMajor)context).RequestTailCall(info, actualArgs);
                        return EmptyIterator.GetInstance();
                    };
                }
                else
                {
                    TailCallLoop.TailCallFunction info = new TailCallLoop.TailCallFunction();
                    info.function = expr.GetFunction();
                    return (context) =>
                    {
                        ISequence[] actualArgs = expr.EvaluateArguments(context, false);
                        ((XPathContextMajor)context).RequestTailCall(info, actualArgs);
                        return EmptyIterator.GetInstance();
                    };
                }
            }
        }

        //
        //
        //
        //
        //    }
        private sealed class UserFunctionCallElaborator : PullElaborator
        {
            private void TestNotAbstract(UserFunctionCall expr, Component target)
            {
                if (target.IsHiddenAbstractComponent())
                {
                    throw new XPathException("Cannot call an abstract function (" + expr.GetFunctionName().DisplayName + ") with no implementation", "XTDE3052");
                }
            }

            private RecursionDepthError ReportStackOverflow(RecursionDepthError e, Expression expr)
            {
                return e.Describe("Too many nested function calls. May be due to infinite recursion", DAXonErrorCode.SXLM0001, expr.GetLocation());
            }

            // Slim direct call: when Call is not overridden (MemoFunction), the per-call ceremony
            // collapses - arguments bound once into a right-sized frame array (the generic path
            // allocates args[] and then SetStackFrame reallocates+copies to slots[]), evaluator
            // fallbacks pre-resolved. For XQuery (bindingSlot < 0) the target is fixed by
            // construction; for XSLT components the elaboration-time FixedTarget is only a HINT -
            // package overrides and hidden-abstract components can rebind at runtime (use-package
            // tests), so the call site re-checks the actual actor per call and falls back.
            //
            // Right-sized replacement for EvaluateArguments: one array sized for the whole stack
            // frame (the classic path allocates args[] and then SetStackFrame reallocates+copies
            // it to slots[]), evaluator fallbacks pre-resolved. This frame pops before the callee
            // body runs, exactly like EvaluateArguments in the classic path.
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static ISequence[] BindArguments(ISequenceEvaluator[] argEvals, int frameSize, IXPathContext context)
            {
                ISequence[] vars = new ISequence[frameSize];
                for (int i = 0; i < argEvals.Length; i++)
                {
                    vars[i] = argEvals[i].Evaluate(context);
                }

                return vars;
            }

            // The classic component-call ceremony, for calls whose runtime actor does not match
            // the elaboration-time hint (package override rebinding).
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static ISequence GenericComponentCall(UserFunctionCall expr, Component target, IXPathContext context)
            {
                ISequence[] actualArgs = expr.EvaluateArguments(context, false);
                UserFunction targetFunction = (UserFunction)target.GetActor();
                XPathContextMajor c2 = targetFunction.MakeNewContext(context, expr, target);
                return targetFunction.Call(c2, actualArgs);
            }

            private static bool TryMakeSlim(UserFunctionCall expr, out UserFunction target, out ISequenceEvaluator[] argEvals, out SlotManager frameMap)
            {
                target = null;
                argEvals = null;
                frameMap = null;
                UserFunction fn;
                if (expr.bindingSlot < 0)
                {
                    fn = expr.GetFunction();
                }
                else
                {
                    Component fixedTarget = expr.FixedTarget;
                    fn = fixedTarget == null ? null : fixedTarget.GetActor() as UserFunction;
                }

                if (fn == null || fn.GetType() != typeof(UserFunction))
                {
                    return false;
                }

                SlotManager map = fn.GetStackFrameMap();
                if (map == null || expr.GetArity() > map.NumberOfVariables)
                {
                    return false;
                }

                target = fn;
                argEvals = expr.ResolvedArgumentEvaluators();
                frameMap = map;
                return true;
            }

            public override IPullEvaluator ElaborateForPull()
            {
                UserFunctionCall expr = (UserFunctionCall)GetExpression();
                if (TryMakeSlim(expr, out UserFunction slimTarget, out ISequenceEvaluator[] slimArgEvals, out SlotManager slimFrameMap))
                {
                    int slimFrameSize = slimFrameMap.NumberOfVariables;
                    if (expr.bindingSlot < 0)
                    {
                        return (context) =>
                        {
                            try
                            {
                                ISequence[] vars = BindArguments(slimArgEvals, slimFrameSize, context);
                                XPathContextMajor c2 = slimTarget.MakeNewContext(context, expr);
                                return slimTarget.CallRightSized(c2, vars).Iterate();
                            }
                            catch (RecursionDepthError e) when (!e.Described)
                            {
                                // Filtered: one such catch per recursion level; only the innermost describes.
                                throw ReportStackOverflow(e, expr);
                            }
                        };
                    }

                    return (context) =>
                    {
                        Component target = expr.GetTargetComponent(context);
                        try
                        {
                            ISequence result;
                            if (ReferenceEquals(target.GetActor(), slimTarget) && !target.IsHiddenAbstractComponent())
                            {
                                ISequence[] vars = BindArguments(slimArgEvals, slimFrameSize, context);
                                XPathContextMajor c2 = slimTarget.MakeNewContext(context, expr, target);
                                result = slimTarget.CallRightSized(c2, vars);
                            }
                            else
                            {
                                TestNotAbstract(expr, target);
                                result = GenericComponentCall(expr, target, context);
                            }

                            return result.Iterate();
                        }
                        catch (RecursionDepthError e) when (!e.Described)
                        {
                            // Filtered: one such catch per recursion level; only the innermost describes.
                            throw ReportStackOverflow(e, expr);
                        }
                    };
                }

                if (expr.bindingSlot >= 0)
                {

                    // XSLT packages in general need dynamic binding
                    return (context) =>
                    {
                        Component target = expr.GetTargetComponent(context);
                        TestNotAbstract(expr, target);
                        try
                        {
                            ISequence[] actualArgs = expr.EvaluateArguments(context, false);
                            UserFunction targetFunction = (UserFunction)target.GetActor();
                            XPathContextMajor c2 = targetFunction.MakeNewContext(context, expr, target);
                            return targetFunction.Call(c2, actualArgs).Iterate();
                        }
                        catch (RecursionDepthError e) when (!e.Described)
                        {
                            // Filtered: one such catch per recursion level; only the innermost describes.
                            throw ReportStackOverflow(e, expr);
                        }
                    };
                }
                else
                {

                    // Non-package case (XQuery)
                    UserFunction targetFunction = expr.GetFunction();
                    return (context) =>
                    {
                        try
                        {
                            ISequence[] actualArgs = expr.EvaluateArguments(context, false);
                            XPathContextMajor c2 = targetFunction.MakeNewContext(context, expr);
                            return targetFunction.Call(c2, actualArgs).Iterate();
                        }
                        catch (RecursionDepthError e) when (!e.Described)
                        {
                            // Filtered: one such catch per recursion level; only the innermost describes.
                            throw ReportStackOverflow(e, expr);
                        }
                    };
                }
            }

            public override IItemEvaluator ElaborateForItem()
            {
                UserFunctionCall expr = (UserFunctionCall)GetExpression();
                if (TryMakeSlim(expr, out UserFunction slimTarget, out ISequenceEvaluator[] slimArgEvals, out SlotManager slimFrameMap))
                {
                    int slimFrameSize = slimFrameMap.NumberOfVariables;
                    if (expr.bindingSlot < 0)
                    {
                        return (context) =>
                        {
                            try
                            {
                                ISequence[] vars = BindArguments(slimArgEvals, slimFrameSize, context);
                                XPathContextMajor c2 = slimTarget.MakeNewContext(context, expr);
                                return slimTarget.CallRightSized(c2, vars).Head();
                            }
                            catch (RecursionDepthError e) when (!e.Described)
                            {
                                // Filtered: one such catch per recursion level; only the innermost describes.
                                throw ReportStackOverflow(e, expr);
                            }
                        };
                    }

                    return (context) =>
                    {
                        Component target = expr.GetTargetComponent(context);
                        try
                        {
                            ISequence result;
                            if (ReferenceEquals(target.GetActor(), slimTarget) && !target.IsHiddenAbstractComponent())
                            {
                                ISequence[] vars = BindArguments(slimArgEvals, slimFrameSize, context);
                                XPathContextMajor c2 = slimTarget.MakeNewContext(context, expr, target);
                                result = slimTarget.CallRightSized(c2, vars);
                            }
                            else
                            {
                                TestNotAbstract(expr, target);
                                result = GenericComponentCall(expr, target, context);
                            }

                            return result.Head();
                        }
                        catch (RecursionDepthError e) when (!e.Described)
                        {
                            // Filtered: one such catch per recursion level; only the innermost describes.
                            throw ReportStackOverflow(e, expr);
                        }
                    };
                }

                if (expr.bindingSlot >= 0)
                {

                    // XSLT packages in general need dynamic binding
                    return (context) =>
                    {
                        Component target = expr.GetTargetComponent(context);
                        TestNotAbstract(expr, target);
                        try
                        {
                            ISequence[] actualArgs = expr.EvaluateArguments(context, false);
                            UserFunction targetFunction = (UserFunction)target.GetActor();
                            XPathContextMajor c2 = targetFunction.MakeNewContext(context, expr, target);
                            return targetFunction.Call(c2, actualArgs).Head();
                        }
                        catch (RecursionDepthError e) when (!e.Described)
                        {
                            // Filtered: one such catch per recursion level; only the innermost describes.
                            throw ReportStackOverflow(e, expr);
                        }
                    };
                }
                else
                {

                    // Non-package case (XQuery)
                    UserFunction targetFunction = expr.GetFunction();
                    return (context) =>
                    {
                        try
                        {
                            ISequence[] actualArgs = expr.EvaluateArguments(context, false);
                            XPathContextMajor c2 = targetFunction.MakeNewContext(context, expr);
                            return targetFunction.Call(c2, actualArgs).Head();
                        }
                        catch (RecursionDepthError e) when (!e.Described)
                        {
                            // Filtered: one such catch per recursion level; only the innermost describes.
                            throw ReportStackOverflow(e, expr);
                        }
                    };
                }
            }

            public override IPushEvaluator ElaborateForPush()
            {
                UserFunctionCall expr = (UserFunctionCall)GetExpression();
                if (expr.IsTailCall())
                {
                    throw new InvalidOperationException("Not using tail call path");
                }


                // If the function call is evaluated in push mode, evaluate the function itself in push mode
                if (expr.bindingSlot >= 0)
                {

                    // XSLT packages in general need dynamic binding
                    return (output, context) =>
                    {
                        Component target = expr.GetTargetComponent(context);
                        TestNotAbstract(expr, target);
                        try
                        {
                            ISequence[] actualArgs = expr.EvaluateArguments(context, false);
                            UserFunction targetFunction = (UserFunction)target.GetActor();
                            XPathContextMajor c2 = targetFunction.MakeNewContext(context, expr, target);
                            targetFunction.Process(c2, actualArgs, output);
                        }
                        catch (RecursionDepthError e) when (!e.Described)
                        {
                            // Filtered: one such catch per recursion level; only the innermost describes.
                            throw ReportStackOverflow(e, expr);
                        }

                        return null;
                    };
                }
                else
                {

                    // Non-package case (XQuery)
                    UserFunction targetFunction = expr.GetFunction();
                    return (output, context) =>
                    {
                        try
                        {
                            ISequence[] actualArgs = expr.EvaluateArguments(context, false);
                            XPathContextMajor c2 = targetFunction.MakeNewContext(context, expr);
                            targetFunction.Process(c2, actualArgs, output);
                        }
                        catch (RecursionDepthError e) when (!e.Described)
                        {
                            // Filtered: one such catch per recursion level; only the innermost describes.
                            throw ReportStackOverflow(e, expr);
                        }

                        return null;
                    };
                }
            }

            public override IUpdateEvaluator ElaborateForUpdate()
            {
                UserFunctionCall expr = (UserFunctionCall)GetExpression();
                UserFunction targetFunction = expr.GetFunction(); // This is XQuery: target function is known
                return (context, pul) =>
                {
                    ISequence[] actualArgs = expr.EvaluateArguments(context, false);
                    XPathContextMajor c2 = context.NewCleanContext();
                    c2.Origin = expr;
                    targetFunction.CallUpdating(actualArgs, c2, pul);
                };
            }
        }
    }
}
