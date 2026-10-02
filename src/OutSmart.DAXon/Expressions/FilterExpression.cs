////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.Numerics;
namespace OutSmart.DAXon.Expressions
{
    public sealed class FilterExpression : BinaryExpression, IContextSwitchingExpression
    {
        public const int FILTERED = 10000;
        public static readonly OperandRole FILTER_PREDICATE = new OperandRole(OperandRole.USES_NEW_FOCUS | OperandRole.HIGHER_ORDER, OperandUsage.INSPECTION, SequenceType.ANY_SEQUENCE);
        private bool filterIsPositional; // true if the value of the filter might depend on
        private bool filterIsSingletonBoolean; // true if the filter expression always returns a single boolean
        private bool filterIsIndependent; // true if the filter expression does not
        public bool doneReorderingPredicates = false;
        private bool indexingDisabled;

        public Expression Base
        {
            get => GetLhsExpression(); set
            {
                SetLhsExpression(value);
            }
        }

        public Expression Filter
        {
            get => GetRhsExpression(); set
            {
                SetRhsExpression(value);
            }
        }

        public override string ExpressionName => "filter";

        public override double Cost => Math.Max(GetLhsExpression().Cost + 5 * GetRhsExpression().Cost, MAX_COST);

        public override int ImplementationMethod => ITERATE_METHOD;

        public override IntegerValue[] IntegerBounds => Base.IntegerBounds;

        public override string StreamerName => "FilterExpression";
        public FilterExpression(Expression @base, Expression filter) : base(@base, Token.LSQB, filter)
        {
            @base.SetFiltered(true);
        }

        protected override OperandRole GetOperandRole(int arg)
        {
            return arg == 0 ? OperandRole.SAME_FOCUS_ACTION : FILTER_PREDICATE;
        }

        public void DisableIndexing()
        {
            indexingDisabled = true;
        }

        public override ItemType GetItemType()
        {

            // special case the expression B[. instance of x]
            if (Filter is InstanceOfExpression && ((InstanceOfExpression)Filter).BaseExpression is ContextItemExpression)
            {
                return ((InstanceOfExpression)Filter).RequiredItemType;
            }

            return Base.GetItemType();
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return Base.GetStaticUType(contextItemType);
        }

        public Expression GetSelectExpression()
        {
            return Base;
        }

        public bool IsFilterIsPositional()
        {
            return filterIsPositional;
        }

        public Expression GetActionExpression()
        {
            return Filter;
        }

        public bool IsPositional(TypeHierarchy th)
        {
            return IsPositionalFilter(Filter, th);
        }

        public bool IsSimpleBooleanFilter()
        {
            return filterIsSingletonBoolean;
        }

        public bool IsIndependentFilter()
        {
            return filterIsIndependent;
        }

        public override Expression Simplify()
        {
            Base = Base.Simplify();
            Filter = Filter.Simplify();

            // ignore the filter if the base expression is an empty sequence
            if (Literal.IsEmptySequence(Base))
            {
                return Base;
            }


            // check whether the filter is a constant true() or false()
            if (Filter is Literal && !(((Literal)Filter).GroundedValue is NumericValue))
            {
                try
                {
                    if (Filter.EffectiveBooleanValue(new EarlyEvaluationContext(GetConfiguration())))
                    {
                        return Base;
                    }
                    else
                    {
                        return Literal.MakeEmptySequence();
                    }
                }
                catch (XPathException e)
                {
                    throw e.MaybeWithLocation(GetLocation());
                }
            }


            // check whether the filter is [last()] (note, [position()=last()] is handled elsewhere)
            if (Filter.IsCallOn(typeof(PositionAndLast.Last)))
            {
                Filter = new IsLastExpression(true);
                AdoptChildExpression(Filter);
            }

            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            Lhs.TypeCheck(visitor, contextInfo);
            Base.SetFiltered(true);
            if (Literal.IsEmptySequence(Base))
            {
                return Base;
            }

            ContextItemStaticInfo baseItemType = config.MakeContextItemStaticInfo(GetSelectExpression().GetItemType(), false);
            baseItemType.ContextSettingExpression = Base;
            Rhs.TypeCheck(visitor, baseItemType);

            // The filter expression usually doesn't need to be sorted
            Expression filter2 = ExpressionTool.UnsortedIfHomogeneous(Filter, visitor.IsOptimizeForStreaming());
            if (filter2 != Filter)
            {
                Filter = filter2;
            }


            // detect head expressions (E[1]) and treat them specially
            if (Literal.IsConstantOne(Filter))
            {
                Expression fie = FirstItemExpression.MakeFirstItemExpression(Base);
                ExpressionTool.CopyLocationInfo(this, fie);
                return fie;
            }


            // determine whether the filter might depend on position
            filterIsPositional = IsPositionalFilter(Filter, th);

            // determine whether the filter always evaluates to a single boolean
            filterIsSingletonBoolean = ComputeFilterIsSingletonBoolean();

            // determine whether the filter expression is independent of the focus
            filterIsIndependent = (Filter.Dependencies & StaticProperty.DEPENDS_ON_FOCUS) == 0;
            ExpressionTool.ResetStaticProperties(this);
            return this;
        }

        private bool ComputeFilterIsSingletonBoolean()
        {
            return !Cardinality.AllowsMany(Filter.GetCardinality()) && Filter.GetItemType().Equals(BuiltInAtomicType.BOOLEAN);
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            if (visitor.IsOptimizeForStreaming() && GetSelectExpression() is GlobalVariableReference)
            {

                // the leading variable reference can't be a streamed node, so ignore streaming for this expression (bug 5475)
                visitor.SetOptimizeForStreaming(false);
                Expression eOpt = Optimize(visitor, contextItemType);
                visitor.SetOptimizeForStreaming(true);
                return eOpt;
            }

            Configuration config = visitor.GetConfiguration();
            Optimizer opt = visitor.ObtainOptimizer();
            bool tracing = config.GetBooleanProperty(Feature<bool>.TRACE_OPTIMIZER_DECISIONS);
            TypeHierarchy th = config.GetTypeHierarchy();
            Lhs.Optimize(visitor, contextItemType);
            if (Literal.IsEmptySequence(GetSelectExpression()))
            {
                return GetSelectExpression();
            }

            Base.SetFiltered(true);
            ContextItemStaticInfo baseItemType = config.MakeContextItemStaticInfo(GetSelectExpression().GetItemType(), false);
            baseItemType.ContextSettingExpression = Base;
            Rhs.Optimize(visitor, baseItemType);

            // The filter expression usually doesn't need to be sorted
            Expression filter2 = ExpressionTool.UnsortedIfHomogeneous(Filter, visitor.IsOptimizeForStreaming());
            if (filter2 != Filter)
            {
                Filter = filter2;
            }


            // Rewrite child.X[last()] as child.X[empty(following-sibling.X)] - especially useful for patterns
            if (Filter is IsLastExpression && ((IsLastExpression)Filter).Condition && Base is AxisExpression && ((AxisExpression)Base).Axis == AxisInfo.CHILD)
            {
                NodeTest test = ((AxisExpression)Base).GetNodeTest();
                AxisExpression fs = new AxisExpression(AxisInfo.FOLLOWING_SIBLING, test);
                Filter = SystemFunction.MakeCall("empty", GetRetainedStaticContext(), fs);
                if (tracing)
                {
                    Optimizer.Trace(config, "Replaced [last()] predicate by test for following-sibling", this);
                }
            }


            // rewrite axis::*[local-name() = 'literal'] as axis::*:local (people write this a lot in XSLT 1.0)
            if (Base is AxisExpression && ((AxisExpression)Base).GetNodeTest() == NodeKindTest.ELEMENT && Filter is CompareToStringConstant && ((CompareToStringConstant)Filter).SingletonOperator == Token.FEQ && ((CompareToStringConstant)Filter).GetLhsExpression().IsCallOn(typeof(LocalNameFn1)) && ((SystemFunctionCall)((CompareToStringConstant)Filter).GetLhsExpression()).GetArg(0) is ContextItemExpression)
            {
                AxisExpression ax2 = new AxisExpression(((AxisExpression)Base).Axis, new LocalNameTest(config.GetNamePool(), Types.Type.ELEMENT, ((CompareToStringConstant)Filter).Comparand.ToString()));
                ExpressionTool.CopyLocationInfo(this, ax2);
                return ax2;
            }


            // if the result of evaluating the filter cannot include numeric values, then we can use
            // its effective boolean value
            ItemType filterType = Filter.GetItemType();
            if (!th.IsSubType(filterType, BuiltInAtomicType.BOOLEAN) && th.Relationship(filterType, NumericType.GetInstance()) == Affinity.DISJOINT)
            {
                Expression f = SystemFunction.MakeCall("boolean", GetRetainedStaticContext(), Filter);
                Filter = f.Optimize(visitor, baseItemType);
            }


            // the filter expression may have been reduced to a constant boolean by previous optimizations
            if (Filter is Literal && ((Literal)Filter).GroundedValue is BooleanValue)
            {
                if (((BooleanValue)((Literal)Filter).GroundedValue).GetBooleanValue())
                {
                    if (tracing)
                    {
                        opt.Trace("Redundant filter removed", Base);
                    }

                    return Base;
                }
                else
                {
                    Expression result = Literal.MakeEmptySequence();
                    ExpressionTool.CopyLocationInfo(this, result);
                    if (tracing)
                    {
                        opt.Trace("Filter expression eliminated because predicate is always false", result);
                    }

                    return result;
                }
            }


            // determine whether the filter might depend on position
            filterIsPositional = IsPositionalFilter(Filter, th);
            filterIsSingletonBoolean = ComputeFilterIsSingletonBoolean();

            // determine whether the filter is indexable
            if (!filterIsPositional && !visitor.IsOptimizeForStreaming() && !indexingDisabled)
            {
                int isIndexable = opt.IsIndexableFilter(Filter);

                // If the filter is indexable consider creating a key, or an indexed filter expression
                // (This happens in Saxon-EE only)
                if (isIndexable != 0)
                {
                    bool contextIsDoc = contextItemType != null && contextItemType.GetItemType() != ErrorType.GetInstance() && th.IsSubType(contextItemType.GetItemType(), NodeKindTest.DOCUMENT);
                    Expression f = opt.TryIndexedFilter(this, visitor, isIndexable > 0, contextIsDoc);
                    if (f != this)
                    {
                        return f.TypeCheck(visitor, contextItemType).Optimize(visitor, contextItemType);
                    }
                }
            }


            // if the filter is positional, try changing f[a and b] to f[a][b] to increase
            // the chances of finishing early.
            if (filterIsPositional && Filter is BooleanExpression && ((BooleanExpression)Filter).@operator == Token.AND)
            {
                BooleanExpression bf = (BooleanExpression)Filter;
                if (IsExplicitlyPositional(bf.GetLhsExpression()) && !IsExplicitlyPositional(bf.GetRhsExpression()))
                {
                    Expression p0 = ForceToBoolean(bf.GetLhsExpression());
                    Expression p1 = ForceToBoolean(bf.GetRhsExpression());
                    FilterExpression f1 = new FilterExpression(Base, p0);
                    ExpressionTool.CopyLocationInfo(this, f1);
                    FilterExpression f2 = new FilterExpression(f1, p1);
                    ExpressionTool.CopyLocationInfo(this, f2);
                    if (tracing)
                    {
                        opt.Trace("Composite filter replaced by nested filter expressions", f2);
                    }

                    return f2.Optimize(visitor, contextItemType);
                }

                if (IsExplicitlyPositional(bf.GetRhsExpression()) && !IsExplicitlyPositional(bf.GetLhsExpression()))
                {
                    Expression p0 = ForceToBoolean(bf.GetLhsExpression());
                    Expression p1 = ForceToBoolean(bf.GetRhsExpression());
                    FilterExpression f1 = new FilterExpression(Base, p1);
                    ExpressionTool.CopyLocationInfo(this, f1);
                    FilterExpression f2 = new FilterExpression(f1, p0);
                    ExpressionTool.CopyLocationInfo(this, f2);
                    if (tracing)
                    {
                        opt.Trace("Composite filter replaced by nested filter expressions", f2);
                    }

                    return f2.Optimize(visitor, contextItemType);
                }
            }

            if (Filter is IsLastExpression && ((IsLastExpression)Filter).Condition)
            {
                if (Base is Literal)
                {
                    Filter = Literal.MakeLiteral(new Int64Value(((Literal)Base).GroundedValue.GetLength()), this);
                }
                else
                {
                    return new LastItemExpression(Base);
                }
            }

            Expression subsequence = TryToRewritePositionalFilter(visitor, tracing);
            if (subsequence != null)
            {
                if (tracing)
                {
                    subsequence.SetRetainedStaticContext(GetRetainedStaticContext()); // Avoids errors in debug explain
                    opt.Trace("Rewrote Filter Expression as:", subsequence);
                }

                ExpressionTool.CopyLocationInfo(this, subsequence);
                return subsequence.Simplify().TypeCheck(visitor, contextItemType).Optimize(visitor, contextItemType);
            }


            // If there are two non-positional filters, consider changing their order based on the estimated cost
            // of evaluation, so we evaluate the cheapest predicates first
            if (!filterIsPositional && !doneReorderingPredicates && !(ParentExpression is FilterExpression))
            {
                FilterExpression f2 = opt.ReorderPredicates(this, visitor, contextItemType);
                if (f2 != this)
                {
                    f2.doneReorderingPredicates = true;
                    return f2;
                }
            }

            ISequence sequence = TryEarlyEvaluation(visitor);
            if (sequence != null)
            {
                IGroundedValue value = sequence.Materialize();
                return Literal.MakeLiteral(value, this);
            }

            return this;
        }

        private ISequence TryEarlyEvaluation(ExpressionVisitor visitor)
        {

            // Attempt early evaluation of a filter expression if the base sequence is constant and the
            // filter depends only on the context. (This can't be done if, for example, the predicate uses
            // local variables, even variables declared within the predicate)
            try
            {
                if (Base is Literal && !ExpressionTool.RefersToVariableOrFunction(Filter) && (Filter.Dependencies & ~StaticProperty.DEPENDS_ON_FOCUS) == 0)
                {
                    IXPathContext context = visitor.StaticContext.MakeEarlyEvaluationContext();
                    return SequenceTool.ToGroundedValue(Iterate(context));
                }
            }
            catch (UncheckedXPathException e) when (e.GetXPathException()?.IsTimeLimit() == true)
            {
                // The time limit stopped the compile, not the filter: a compile that went on past it would
                // succeed or fail by luck. Unwrapped, so the compile reports it like any other static error.
                throw e.GetXPathException();
            }
            catch (Exception e) when (!(e is XPathException x && x.IsTimeLimit()))
            {

                // can happen for a variety of reasons, for example the filter references a global parameter,
                // references the doc() function, uses element constructors, etc.
                return null;
            }

            return null;
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet target = Base.AddToPathMap(pathMap, pathMapNodeSet);
            Filter.AddToPathMap(pathMap, target);
            return target;
        }

        private static Expression ForceToBoolean(Expression @in)
        {
            if (@in.GetItemType().PrimitiveType == StandardNames.XS_BOOLEAN)
            {
                return @in;
            }

            return SystemFunction.MakeCall("boolean", @in.GetRetainedStaticContext(), @in);
        }

        private Expression TryToRewritePositionalFilter(ExpressionVisitor visitor, bool tracing)
        {
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            if (Filter is Literal)
            {
                IGroundedValue val = ((Literal)Filter).GroundedValue;
                if (val is NumericValue)
                {
                    Expression result;
                    int lvalue = ((NumericValue)val).AsSubscript();
                    if (lvalue != -1)
                    {
                        if (lvalue == 1)
                        {
                            result = FirstItemExpression.MakeFirstItemExpression(Base);
                        }
                        else
                        {
                            result = new SubscriptExpression(Base, Filter);
                        }
                    }
                    else
                    {
                        result = Literal.MakeEmptySequence();
                    }

                    if (tracing)
                    {
                        Optimizer.Trace(config, "Rewriting numeric filter expression with constant subscript", result);
                    }

                    return result;
                }
                else
                {
                    Expression result = ExpressionTool.EffectiveBooleanValue(val.Iterate()) ? Base : Literal.MakeEmptySequence();
                    if (tracing)
                    {
                        Optimizer.Trace(config, "Rewriting boolean filter expression with constant subscript", result);
                    }

                    return result;
                }
            }

            if (NumericType.IsNumericType(Filter.GetItemType()) && !Cardinality.AllowsMany(Filter.GetCardinality()) && (Filter.Dependencies & StaticProperty.DEPENDS_ON_FOCUS) == 0)
            {
                Expression result = new SubscriptExpression(Base, Filter);
                if (tracing)
                {
                    Optimizer.Trace(config, "Rewriting numeric filter expression with focus-independent subscript", result);
                }

                return result;
            }

            if (Filter is IComparisonExpression)
            {

                Expression lhs = ((IComparisonExpression)Filter).GetLhsExpression();
                Expression rhs = ((IComparisonExpression)Filter).GetRhsExpression();
                int @operator = ((IComparisonExpression)Filter).SingletonOperator;
                Expression comparand;
                if (lhs.IsCallOn(typeof(PositionAndLast.Position)) && NumericType.IsNumericType(rhs.GetItemType()))
                {
                    comparand = rhs;
                }
                else if (rhs.IsCallOn(typeof(PositionAndLast.Position)) && NumericType.IsNumericType(lhs.GetItemType()))
                {
                    comparand = lhs;
                    @operator = Token.Inverse(@operator);
                }
                else
                {
                    return null;
                }

                if (ExpressionTool.DependsOnFocus(comparand))
                {
                    return null;
                }

                int card = comparand.GetCardinality();
                if (Cardinality.AllowsMany(card))
                {
                    return null;
                }


                // If the comparand might be an empty sequence, do the base rewrite and then wrap the
                // rewritten expression EXP in "let $n := comparand if exists($n) then EXP else ()
                if (Cardinality.AllowsZero(card))
                {
                    LetExpression let = new LetExpression();
                    let.SetRequiredType(SequenceType.MakeSequenceType(comparand.GetItemType(), card));
                    let.SetVariableQName(new StructuredQName("pp", NamespaceUri.SAXON, "pp" + let.GetHashCode()));
                    let.Sequence = comparand;
                    comparand = new LocalVariableReference(let);
                    LocalVariableReference existsArg = new LocalVariableReference(let);
                    Expression exists = SystemFunction.MakeCall("exists", GetRetainedStaticContext(), existsArg);
                    Expression rewrite = TryToRewritePositionalFilterSupport(Base, comparand, @operator, th);
                    if (rewrite == null)
                    {
                        return this;
                    }

                    Expression choice = Choose.MakeConditional(exists, rewrite);
                    let.SetAction(choice);
                    return let;
                }
                else
                {
                    return TryToRewritePositionalFilterSupport(Base, comparand, @operator, th);
                }
            }
            else if (Filter is IntegerRangeTest)
            {

                // rewrite SEQ[position() = N to M]
                // => let $n := N return subsequence(SEQ, $n, (M - ($n - 1))
                // (precise form is optimized for the case where $n is a literal, especially N = 1)
                Expression val = ((IntegerRangeTest)Filter).Value;
                if (!val.IsCallOn(typeof(PositionAndLast)))
                {
                    return null;
                }

                Expression min = ((IntegerRangeTest)Filter).GetMin();
                Expression max = ((IntegerRangeTest)Filter).GetMax();
                if (ExpressionTool.DependsOnFocus(min))
                {
                    return null;
                }

                if (ExpressionTool.DependsOnFocus(max))
                {
                    if (max.IsCallOn(typeof(PositionAndLast.Last)))
                    {
                        Expression result = SystemFunction.MakeCall("subsequence", GetRetainedStaticContext(), Base, min);
                        if (tracing)
                        {
                            Optimizer.Trace(config, "Rewriting numeric range filter expression using subsequence()", result);
                        }

                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }

                LetExpression let = new LetExpression();
                let.SetRequiredType(SequenceType.SINGLE_INTEGER);
                let.SetVariableQName(new StructuredQName("nn", NamespaceUri.SAXON, "nn" + let.GetHashCode()));
                let.Sequence = min;
                min = new LocalVariableReference(let);
                LocalVariableReference min2 = new LocalVariableReference(let);
                Expression minMinusOne = new ArithmeticExpression(min2, Token.MINUS, Literal.MakeLiteral(Int64Value.MakeIntegerValue(1), this));
                Expression length = new ArithmeticExpression(max, Token.MINUS, minMinusOne);
                Expression subs = SystemFunction.MakeCall("subsequence", GetRetainedStaticContext(), Base, min, length);
                let.SetAction(subs);
                if (tracing)
                {
                    Optimizer.Trace(config, "Rewriting numeric range filter expression using subsequence()", subs);
                }

                return let;
            }
            else
            {
                return null;
            }
        }

        private static Expression TryToRewritePositionalFilterSupport(Expression start, Expression comparand, int @operator, TypeHierarchy th)
        {
            if (th.IsSubType(comparand.GetItemType(), BuiltInAtomicType.INTEGER))
            {
                switch (@operator)
                {
                    case Token.FEQ:
                        {
                            if (Literal.IsConstantOne(comparand))
                            {
                                return FirstItemExpression.MakeFirstItemExpression(start);
                            }
                            else if (comparand is Literal && ((IntegerValue)((Literal)comparand).GroundedValue).AsBigInteger().CompareTo(BigInteger.Zero) <= 0)
                            {
                                return Literal.MakeEmptySequence();
                            }
                            else
                            {
                                return new SubscriptExpression(start, comparand);
                            }
                        }

                    case Token.FLT:
                        {
                            Expression[] args = new Expression[3];
                            args[0] = start;
                            args[1] = Literal.MakeLiteral(Int64Value.MakeIntegerValue(1), start);
                            if (Literal.IsAtomic(comparand))
                            {
                                long n = ((NumericValue)((Literal)comparand).GroundedValue).LongValue();
                                args[2] = Literal.MakeLiteral(Int64Value.MakeIntegerValue(n - 1), start);
                            }
                            else
                            {
                                ArithmeticExpression decrement = new ArithmeticExpression(comparand, Token.MINUS, Literal.MakeLiteral(Int64Value.MakeIntegerValue(1), start));
                                decrement.SetCalculator(Calculator.GetCalculator(StandardNames.XS_INTEGER, StandardNames.XS_INTEGER, Calculator.MINUS, true));
                                args[2] = decrement;
                            }

                            return SystemFunction.MakeCall("subsequence", start.GetRetainedStaticContext(), args);
                        }

                    case Token.FLE:
                        {
                            Expression[] args = new Expression[3];
                            args[0] = start;
                            args[1] = Literal.MakeLiteral(Int64Value.MakeIntegerValue(1), start);
                            args[2] = comparand;
                            return SystemFunction.MakeCall("subsequence", start.GetRetainedStaticContext(), args);
                        }

                    case Token.FNE:
                        {
                            return SystemFunction.MakeCall("remove", start.GetRetainedStaticContext(), start, comparand);
                        }

                    case Token.FGT:
                        {
                            Expression[] args = new Expression[2];
                            args[0] = start;
                            if (Literal.IsAtomic(comparand))
                            {
                                long n = ((NumericValue)((Literal)comparand).GroundedValue).LongValue();
                                args[1] = Literal.MakeLiteral(Int64Value.MakeIntegerValue(n + 1), start);
                            }
                            else
                            {
                                args[1] = new ArithmeticExpression(comparand, Token.PLUS, Literal.MakeLiteral(Int64Value.MakeIntegerValue(1), start));
                            }

                            return SystemFunction.MakeCall("subsequence", start.GetRetainedStaticContext(), args);
                        }

                    case Token.FGE:
                        {
                            return SystemFunction.MakeCall("subsequence", start.GetRetainedStaticContext(), start, comparand);
                        }

                    default:
                        throw new ArgumentException("operator");
                }
            }
            else
            {

                // the comparand is not known statically to be an integer
                switch (@operator)
                {
                    case Token.FEQ:
                        {
                            return new SubscriptExpression(start, comparand);
                        }

                    case Token.FLT:
                        {

                            // rewrite SEQ[position() lt V] as
                            // let $N := V return subsequence(SEQ, 1, if (is-whole-number($N)) then $N-1 else floor($N)))
                            LetExpression let = new LetExpression();
                            let.SetRequiredType(SequenceType.MakeSequenceType(comparand.GetItemType(), StaticProperty.ALLOWS_ONE));
                            let.SetVariableQName(new StructuredQName("pp", NamespaceUri.SAXON, "pp" + let.GetHashCode()));
                            let.Sequence = comparand;
                            LocalVariableReference isWholeArg = new LocalVariableReference(let);
                            LocalVariableReference arithArg = new LocalVariableReference(let);
                            LocalVariableReference floorArg = new LocalVariableReference(let);
                            Expression isWhole = VendorFunctionSetHE.GetInstance().MakeFunction("is-whole-number", 1).MakeFunctionCall(isWholeArg);
                            Expression minusOne = new ArithmeticExpression(arithArg, Token.MINUS, Literal.MakeLiteral(Int64Value.MakeIntegerValue(1), start));
                            Expression floor = SystemFunction.MakeCall("floor", start.GetRetainedStaticContext(), floorArg);
                            Expression choice = Choose.MakeConditional(isWhole, minusOne, floor);
                            Expression subs = SystemFunction.MakeCall("subsequence", start.GetRetainedStaticContext(), start, Literal.MakeLiteral(Int64Value.MakeIntegerValue(1), start), choice);
                            let.SetAction(subs);

                            return let;
                        }

                    case Token.FLE:
                        {
                            Expression floor = SystemFunction.MakeCall("floor", start.GetRetainedStaticContext(), comparand);
                            return SystemFunction.MakeCall("subsequence", start.GetRetainedStaticContext(), start, Literal.MakeLiteral(Int64Value.MakeIntegerValue(1), start), floor);
                        }

                    case Token.FNE:
                        {

                            // rewrite SEQ[position() ne V] as
                            // let $N := V return remove(SEQ, if (is-whole-number($N)) then xs:integer($N) else 0)
                            LetExpression let = new LetExpression();
                            ExpressionTool.CopyLocationInfo(start, let);
                            let.SetRequiredType(SequenceType.MakeSequenceType(comparand.GetItemType(), StaticProperty.ALLOWS_ONE));
                            let.SetVariableQName(new StructuredQName("pp", NamespaceUri.SAXON, "pp" + let.GetHashCode()));
                            let.Sequence = comparand;
                            LocalVariableReference isWholeArg = new LocalVariableReference(let);
                            LocalVariableReference castArg = new LocalVariableReference(let);
                            Expression isWhole = VendorFunctionSetHE.GetInstance().MakeFunction("is-whole-number", 1).MakeFunctionCall(isWholeArg);
                            ExpressionTool.CopyLocationInfo(start, isWhole);
                            Expression cast = new CastExpression(castArg, BuiltInAtomicType.INTEGER, false);
                            ExpressionTool.CopyLocationInfo(start, cast);
                            Expression choice = Choose.MakeConditional(isWhole, cast, Literal.MakeLiteral(Int64Value.MakeIntegerValue(0), start));
                            Expression rem = SystemFunction.MakeCall("remove", start.GetRetainedStaticContext(), start, choice);
                            let.SetAction(rem);
                            return let;
                        }

                    case Token.FGT:
                        {

                            // rewrite SEQ[position() gt V] as
                            // let $N := V return subsequence(SEQ, if (is-whole-number($N)) then $N+1 else ceiling($N)))
                            LetExpression let = new LetExpression();
                            let.SetRequiredType(SequenceType.MakeSequenceType(comparand.GetItemType(), StaticProperty.ALLOWS_ONE));
                            let.SetVariableQName(new StructuredQName("pp", NamespaceUri.SAXON, "pp" + let.GetHashCode()));
                            let.Sequence = comparand;
                            LocalVariableReference isWholeArg = new LocalVariableReference(let);
                            LocalVariableReference arithArg = new LocalVariableReference(let);
                            LocalVariableReference ceilingArg = new LocalVariableReference(let);
                            Expression isWhole = VendorFunctionSetHE.GetInstance().MakeFunction("is-whole-number", 1).MakeFunctionCall(isWholeArg);
                            Expression plusOne = new ArithmeticExpression(arithArg, Token.PLUS, Literal.MakeLiteral(Int64Value.MakeIntegerValue(1), start));
                            Expression ceiling = SystemFunction.MakeCall("ceiling", start.GetRetainedStaticContext(), ceilingArg);
                            Expression choice = Choose.MakeConditional(isWhole, plusOne, ceiling);
                            Expression subs = SystemFunction.MakeCall("subsequence", start.GetRetainedStaticContext(), start, choice);
                            let.SetAction(subs);
                            return let;
                        }

                    case Token.FGE:
                        {

                            // rewrite SEQ[position() ge V] => subsequence(SEQ, ceiling(V))
                            Expression ceiling = SystemFunction.MakeCall("ceiling", start.GetRetainedStaticContext(), comparand);
                            return SystemFunction.MakeCall("subsequence", start.GetRetainedStaticContext(), start, ceiling);
                        }

                    default:
                        throw new ArgumentException("operator");
                }
            }
        }

        public override Expression Unordered(bool retainAllNodes, bool forStreaming)
        {
            if (!filterIsPositional)
            {
                Base = Base.Unordered(retainAllNodes, forStreaming);
            }

            return this;
        }

        public static bool IsPositionalFilter(Expression exp, TypeHierarchy th)
        {
            ItemType type = exp.GetItemType();
            if (type.Equals(BuiltInAtomicType.BOOLEAN))
            {

                // common case, get it out of the way quickly
                return IsExplicitlyPositional(exp);
            }

            return type.Equals(BuiltInAtomicType.ANY_ATOMIC) || type is AnyItemType || type.Equals(BuiltInAtomicType.INTEGER) || type.Equals(NumericType.GetInstance()) || NumericType.IsNumericType(type) || IsExplicitlyPositional(exp);
        }

        private static bool IsExplicitlyPositional(Expression exp)
        {
            return (exp.Dependencies & (StaticProperty.DEPENDS_ON_POSITION | StaticProperty.DEPENDS_ON_LAST)) != 0;
        }

        protected override int ComputeCardinality()
        {
            if (Filter is Literal && ((Literal)Filter).GroundedValue is NumericValue)
            {
                if (((NumericValue)((Literal)Filter).GroundedValue).CompareTo(1) == 0 && !Cardinality.AllowsZero(Base.GetCardinality()))
                {
                    return StaticProperty.ALLOWS_ONE;
                }
                else
                {
                    return StaticProperty.ALLOWS_ZERO_OR_ONE;
                }
            }

            if (filterIsIndependent)
            {
                ItemType filterType = Filter.GetItemType().GetPrimitiveItemType();
                if (filterType == BuiltInAtomicType.INTEGER || filterType == BuiltInAtomicType.DOUBLE || filterType == BuiltInAtomicType.DECIMAL || filterType == BuiltInAtomicType.FLOAT)
                {
                    return StaticProperty.ALLOWS_ZERO_OR_ONE;
                }

                if (Filter is ArithmeticExpression)
                {
                    return StaticProperty.ALLOWS_ZERO_OR_ONE;
                }
            }

            if (Filter is IsLastExpression && ((IsLastExpression)Filter).Condition)
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }

            if (!Cardinality.AllowsMany(Base.GetCardinality()))
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }

            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        protected override int ComputeSpecialProperties()
        {
            return Base.GetSpecialProperties();
        }

        public override bool Equals(object other)
        {
            if (other is FilterExpression)
            {
                FilterExpression f = (FilterExpression)other;
                return Base.IsEqual(f.Base) && Filter.IsEqual(f.Filter);
            }

            return false;
        }

        protected override int ComputeHashCode()
        {
            return "FilterExpression".GetHashCode() + Base.GetHashCode() + Filter.GetHashCode();
        }

        public override Patterns.Pattern ToPattern(Configuration config)
        {
            Expression @base = GetSelectExpression();
            Expression filter = Filter;
            TypeHierarchy th = config.GetTypeHierarchy();
            Patterns.Pattern basePattern = @base.ToPattern(config);
            if (!IsPositional(th))
            {
                return new BasePatternWithPredicate(basePattern, filter);
            }
            else if (basePattern is NodeTestPattern && basePattern.GetItemType() is NodeTest && filterIsPositional && @base is AxisExpression && ((AxisExpression)@base).Axis == AxisInfo.CHILD && (filter.Dependencies & StaticProperty.DEPENDS_ON_LAST) == 0)
            {
                if (filter is Literal && ((Literal)filter).GroundedValue is IntegerValue)
                {
                    return new SimplePositionalPattern((NodeTest)basePattern.GetItemType(), (int)((IntegerValue)((Literal)filter).GroundedValue).LongValue());
                }
                else
                {
                    return new GeneralPositionalPattern((NodeTest)basePattern.GetItemType(), filter);
                }
            }

            if (@base.GetItemType() is NodeTest)
            {
                return new GeneralNodePattern(this, (NodeTest)@base.GetItemType());
            }
            else
            {
                throw new XPathException("The filtered expression in an XSLT 2.0 pattern must be a simple step");
            }
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {

            // Fast path where the filter value is independent of the focus
            if (filterIsIndependent)
            {
                try
                {
                    ISequenceIterator it = Filter.Iterate(context);
                    IItem first = it.Next();
                    if (first == null)
                    {
                        return EmptyIterator.GetInstance();
                    }

                    if (first is NumericValue)
                    {
                        if (it.Next() != null)
                        {
                            ExpressionTool.EbvError("sequence of two or more items starting with a numeric value", Filter);
                        }
                        else
                        {

                            // Filter is a constant number
                            int pos = ((NumericValue)first).AsSubscript();
                            if (pos != -1)
                            {
                                if (Base is VariableReference)
                                {
                                    ISequence baseVal = ((VariableReference)Base).EvaluateVariable(context);
                                    if (baseVal is MemoClosure)
                                    {
                                        IItem m = ((MemoClosure)baseVal).ItemAt(pos - 1);
                                        return SingletonIterator.MakeIterator(m);
                                    }
                                    else
                                    {
                                        IItem m = baseVal.Materialize().ItemAt(pos - 1);
                                        return SingletonIterator.MakeIterator(m);
                                    }
                                }
                                else if (Base is Literal)
                                {
                                    IItem i = ((Literal)Base).GroundedValue.ItemAt(pos - 1);
                                    return SingletonIterator.MakeIterator(i);
                                }
                                else
                                {
                                    return SubsequenceIterator.Make(Base.Iterate(context), pos, pos);
                                }
                            }


                            // a non-integer value or non-positive number will never be equal to position()
                            return EmptyIterator.GetInstance();
                        }
                    }
                    else
                    {

                        // Filter is focus-independent, but not numeric: need to use the effective boolean value
                        bool ebv = false;
                        if (first is NodeInfo)
                        {
                            ebv = true;
                        }
                        else if (first is BooleanValue)
                        {
                            ebv = ((BooleanValue)first).GetBooleanValue();
                            if (it.Next() != null)
                            {
                                ExpressionTool.EbvError("sequence of two or more items starting with a boolean value", Filter);
                            }
                        }
                        else if (first is StringValue)
                        {
                            ebv = !((StringValue)first).IsEmpty();
                            if (it.Next() != null)
                            {
                                ExpressionTool.EbvError("sequence of two or more items starting with a boolean value", Filter);
                            }
                        }
                        else
                        {
                            ExpressionTool.EbvError("sequence starting with an atomic value other than a boolean, number, or string", Filter);
                        }

                        if (ebv)
                        {
                            return Base.Iterate(context);
                        }
                        else
                        {
                            return EmptyIterator.GetInstance();
                        }
                    }
                }
                catch (XPathException e)
                {
                    throw e.MaybeWithLocation(GetLocation());
                }
            }

            IPullEvaluator puller = MakeElaborator().ElaborateForPull();
            return puller.Iterate(context);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            FilterExpression fe = new FilterExpression(Base.Copy(rebindings), Filter.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, fe);
            fe.filterIsIndependent = filterIsIndependent;
            fe.filterIsPositional = filterIsPositional;
            fe.filterIsSingletonBoolean = filterIsSingletonBoolean;
            fe.indexingDisabled = indexingDisabled;
            return fe;
        }

        public override string ToString()
        {
            return ExpressionTool.Parenthesize(Base) + "[" + Filter + "]";
        }

        public override string ToShortString()
        {
            return Base.ToShortString() + "[" + Filter.ToShortString() + "]";
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("filter", this);
            string flags = "";
            if (filterIsIndependent)
            {
                flags += "i";
            }

            if (filterIsPositional)
            {
                flags += "p";
            }

            if (filterIsSingletonBoolean)
            {
                flags += "b";
            }

            @out.EmitAttribute("flags", flags);
            Base.Export(@out);
            Filter.Export(@out);
            @out.EndElement();
        }

        public void SetFlags(string flags)
        {
            filterIsIndependent = flags.Contains("i");
            filterIsPositional = flags.Contains("p");
            filterIsSingletonBoolean = flags.Contains("b");
        }

        public override Elaborator GetElaborator()
        {
            return new FilterExprElaborator();
        }

        /// <summary>
        /// Elaborator for a filter expression
        /// </summary>
        internal sealed class FilterExprElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                FilterExpression expr = (FilterExpression)GetExpression();
                IPullEvaluator baseEval = expr.Base.MakeElaborator().ElaborateForPull();
                IPullEvaluator generic;
                if (expr.IsSimpleBooleanFilter())
                {
                    IBooleanEvaluator conditionEval = expr.Filter.MakeElaborator().ElaborateForBoolean();
                    generic = (context) =>
                    {
                        ISequenceIterator @base = baseEval.Iterate(context);
                        IXPathContext c2 = context.NewMinorContext();
                        c2.SetCurrentIterator(c2.GetController().MakeFocusTracker(@base, false));
                        return new SimpleFilteredIterator(c2, conditionEval);
                    };
                }
                else
                {
                    IPullEvaluator conditionEval = expr.Filter.MakeElaborator().ElaborateForPull();
                    generic = (context) =>
                    {
                        ISequenceIterator @base = baseEval.Iterate(context);
                        IXPathContext c2 = context.NewMinorContext();
                        c2.SetCurrentIterator(c2.GetController().MakeFocusTracker(@base, false));
                        return new PositionalFilteredIterator(c2, conditionEval);
                    };
                }

                // Both compiled forms of the leaf-element filter `//*[not(*)]` (see FusedLeafFilter):
                // on an untyped Tiny context the leaf verdict reads straight off the node arrays.
                if (Elaboration.FusedLeafFilter.MatchLeafElements(expr))
                {
                    return (context) => context.GetContextItem() is Trees.Tiny.TinyParentNodeImpl tiny && tiny.tree.TypeArray == null
                        ? (ISequenceIterator)new Elaboration.FusedLeafFilter.LeafElementIterator(tiny)
                        : generic(context);
                }

                if (Elaboration.FusedLeafFilter.MatchLeafTexts(expr))
                {
                    return (context) => context.GetContextItem() is Trees.Tiny.TinyParentNodeImpl tiny && tiny.tree.TypeArray == null
                        ? (ISequenceIterator)new Elaboration.FusedLeafFilter.LeafTextIterator(tiny, false)
                        : generic(context);
                }

                return generic;
            }

            internal sealed class PositionalFilteredIterator : ISequenceIterator
            {
                private readonly IXPathContext outerContext;
                private readonly IFocusIterator @base;
                private readonly IPullEvaluator condition;
                private readonly OutSmart.DAXon.Core.Controller controller;
                public PositionalFilteredIterator(IXPathContext outerContext, IPullEvaluator condition)
                {
                    this.outerContext = outerContext;
                    this.@base = outerContext.GetCurrentIterator();
                    this.condition = condition;
                    this.controller = outerContext.GetController();
                }

                public IItem Next()
                {
                    try
                    {
                        while (true)
                        {
                            controller.CheckTimeout();
                            IItem next = @base.Next();
                            if (next == null)
                            {
                                return null;
                            }

                            if (FilterIterator.TestPredicateValue(condition.Iterate(outerContext), @base.Position(), null))
                            {
                                return next;
                            }
                        }
                    }
                    catch (XPathException e)
                    {
                        throw new UncheckedXPathException(e);
                    }
                }
                public void Dispose() { }
            }

            internal sealed class SimpleFilteredIterator : ISequenceIterator
            {
                private readonly IXPathContext outerContext;
                private readonly IFocusIterator @base;
                private readonly IBooleanEvaluator condition;
                private readonly OutSmart.DAXon.Core.Controller controller;
                public SimpleFilteredIterator(IXPathContext outerContext, IBooleanEvaluator condition)
                {
                    this.outerContext = outerContext;
                    this.@base = outerContext.GetCurrentIterator();
                    this.condition = condition;
                    this.controller = outerContext.GetController();
                }

                public IItem Next()
                {
                    try
                    {
                        while (true)
                        {
                            controller.CheckTimeout();
                            IItem next = @base.Next();
                            if (next == null)
                            {
                                return null;
                            }

                            if (condition.Eval(outerContext))
                            {
                                return next;
                            }
                        }
                    }
                    catch (XPathException e)
                    {
                        throw new UncheckedXPathException(e);
                    }
                }
                public void Dispose() { }
            }
        }
    }
}
