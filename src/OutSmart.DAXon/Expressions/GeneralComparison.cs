////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions
{
    public abstract class GeneralComparison : BinaryExpression, IComparisonExpression
    {

        protected int singletonOperator;
        protected IAtomicComparer comparer;
        protected bool runtimeCheckNeeded = true;
        protected ComparisonCardinality comparisonCardinality = ComparisonCardinality.MANY_TO_MANY;
        protected bool doneWarnings;

        public override string ExpressionName => "GeneralComparison";

        public IStringCollator StringCollator => comparer.Collator;

        public int SingletonOperator => singletonOperator;

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public override int IntrinsicDependencies
        {
            get
            {

                // The expression is dependent on the static namespace context if one operand might deliver
                // untypedAtomic and the other might deliver a QName
                TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                if (MayInvolveCastToQName(th, GetLhsExpression(), GetRhsExpression()) || MayInvolveCastToQName(th, GetRhsExpression(), GetLhsExpression()))
                {
                    return StaticProperty.DEPENDS_ON_STATIC_CONTEXT;
                }
                else
                {
                    return 0;
                }
            }
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        protected virtual GeneralComparison InverseComparison
        {
            get
            {
                GeneralComparison20 gc2 = new GeneralComparison20(GetRhsExpression(), Token.Inverse(@operator), GetLhsExpression());
                gc2.SetRetainedStaticContext(GetRetainedStaticContext());
                return gc2;
            }
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public override string StreamerName => "GeneralComparison";
        public GeneralComparison(Expression p0, int op, Expression p1) : base(p0, op, p1)
        {
            singletonOperator = GetCorrespondingSingletonOperator(op);
        }

        public virtual bool NeedsRuntimeCheck()
        {
            return runtimeCheckNeeded;
        }

        public virtual void SetNeedsRuntimeCheck(bool needsCheck)
        {
            runtimeCheckNeeded = needsCheck;
        }

        public virtual ComparisonCardinality GetComparisonCardinality()
        {
            return comparisonCardinality;
        }

        public virtual void SetComparisonCardinality(ComparisonCardinality card)
        {
            comparisonCardinality = card;
        }

        public virtual void SetAtomicComparer(IAtomicComparer comparer)
        {
            this.comparer = comparer;
        }

        public virtual INamespaceResolver GetNamespaceResolver()
        {
            return GetRetainedStaticContext();
        }

        public IAtomicComparer GetAtomicComparer()
        {
            return comparer;
        }

        public bool ConvertsUntypedToOther()
        {
            return true;
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            Expression oldOp0 = GetLhsExpression();
            Expression oldOp1 = GetRhsExpression();
            Lhs.TypeCheck(visitor, contextInfo);
            Rhs.TypeCheck(visitor, contextInfo);

            // If either operand is statically empty, return false
            if (Literal.IsEmptySequence(GetLhsExpression()) || Literal.IsEmptySequence(GetRhsExpression()))
            {
                return Literal.MakeLiteral(BooleanValue.FALSE, this);
            }


            // Neither operand needs to be sorted
            SetLhsExpression(GetLhsExpression().Unordered(false, false));
            SetRhsExpression(GetRhsExpression().Unordered(false, false));
            Values.SequenceType atomicType = Values.SequenceType.ATOMIC_SEQUENCE;
            TypeChecker tc = config.GetTypeChecker(false);
            Func<RoleDiagnostic> role0 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, Token.tokens[@operator], 0);
            SetLhsExpression(tc.StaticTypeCheck(GetLhsExpression(), atomicType, role0, visitor));
            Func<RoleDiagnostic> role1 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, Token.tokens[@operator], 1);
            SetRhsExpression(tc.StaticTypeCheck(GetRhsExpression(), atomicType, role1, visitor));
            if (GetLhsExpression() != oldOp0)
            {
                AdoptChildExpression(GetLhsExpression());
            }

            if (GetRhsExpression() != oldOp1)
            {
                AdoptChildExpression(GetRhsExpression());
            }

            Types.ItemType t0 = GetLhsExpression().GetItemType(); // this is always an atomic type or union type or xs:error
            Types.ItemType t1 = GetRhsExpression().GetItemType(); // this is always an atomic type or union type or xs:error
            if (t0 is ErrorType || t1 is ErrorType)
            {
                return Literal.MakeLiteral(BooleanValue.FALSE, this);
            }

            if (t0.GetUType().Union(t1.GetUType()).Overlaps(UType.EXTENSION))
            {
                throw new XPathException("Cannot perform comparisons involving external objects").AsTypeError().WithErrorCode("XPTY0004").WithLocation(GetLocation());
            }

            BuiltInAtomicType pt0 = (BuiltInAtomicType)t0.GetPrimitiveItemType();
            BuiltInAtomicType pt1 = (BuiltInAtomicType)t1.GetPrimitiveItemType();
            int c0 = GetLhsExpression().GetCardinality();
            int c1 = GetRhsExpression().GetCardinality();
            if (c0 == StaticProperty.EMPTY || c1 == StaticProperty.EMPTY)
            {
                return Literal.MakeLiteral(BooleanValue.FALSE, this);
            }

            if (t0.Equals(BuiltInAtomicType.ANY_ATOMIC) || t0.Equals(BuiltInAtomicType.UNTYPED_ATOMIC) || t1.Equals(BuiltInAtomicType.ANY_ATOMIC) || t1.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
            }
            else
            {
                if (!Types.Type.IsPossiblyComparable(pt0, pt1, visitor.StaticContext.GetXPathVersion()))
                {
                    string message = "In {" + ToShortString() + "}: cannot compare " + t0 + " to " + t1;
                    if (Cardinality.AllowsZero(c0) || Cardinality.AllowsZero(c1))
                    {
                        if (!doneWarnings)
                        {

                            // avoid duplicate warnings
                            doneWarnings = true;
                            string which = "one";
                            if (Cardinality.AllowsZero(c0) && !Cardinality.AllowsZero(c1))
                            {
                                which = "the first";
                            }
                            else if (Cardinality.AllowsZero(c1) && !Cardinality.AllowsZero(c0))
                            {
                                which = "the second";
                            }

                            visitor.StaticContext.IssueWarning(message + ". The comparison can succeed only if " + which + " operand is empty, and in that case will always be false", DAXonErrorCode.SXWN9025, GetLocation());
                        }
                    }
                    else
                    {
                        throw new XPathException(message).WithErrorCode("XPTY0004").AsTypeError().WithLocation(GetLocation());
                    }
                }
            }

            runtimeCheckNeeded = !Types.Type.IsGuaranteedGenerallyComparable(pt0, pt1, Token.IsOrderedOperator(singletonOperator));
            if (!Cardinality.AllowsMany(c0) && !Cardinality.AllowsMany(c1) && !t0.Equals(BuiltInAtomicType.ANY_ATOMIC) && !t1.Equals(BuiltInAtomicType.ANY_ATOMIC))
            {

                // Use a value comparison if both arguments are singletons, and if the comparison operator to
                // be used can be determined.
                Expression e0 = GetLhsExpression();
                Expression e1 = GetRhsExpression();
                if (t0.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
                {
                    if (t1.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
                    {
                        e0 = new CastExpression(GetLhsExpression(), BuiltInAtomicType.STRING, Cardinality.AllowsZero(c0));
                        AdoptChildExpression(e0);
                        e1 = new CastExpression(GetRhsExpression(), BuiltInAtomicType.STRING, Cardinality.AllowsZero(c1));
                        AdoptChildExpression(e1);
                    }
                    else if (NumericType.IsNumericType(t1))
                    {
                        SetAtomicComparer(new UntypedNumericComparer());
                        return this; //                    Expression vun = makeCompareUntypedToNumeric(getLhsExpression(), getRhsExpression(), singletonOperator);
                        //                    return vun.typeCheck(visitor, contextInfo);
                    }
                    else
                    {
                        e0 = new CastExpression(GetLhsExpression(), pt1, Cardinality.AllowsZero(c0));
                        AdoptChildExpression(e0);
                    }
                }
                else if (t1.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
                {
                    if (NumericType.IsNumericType(t0))
                    {
                        SetAtomicComparer(new UntypedNumericComparer());
                        return this; //                    e1 = new CastExpression(getRhsExpression(), BuiltInAtomicType.DOUBLE, false);
                        //                    adoptChildExpression(e1);
                        //                    Expression vun = makeCompareUntypedToNumeric(getRhsExpression(), getLhsExpression(), Token.inverse(singletonOperator));
                        //                    return vun.typeCheck(visitor, contextInfo);
                    }
                    else
                    {
                        e1 = new CastExpression(GetRhsExpression(), pt0, Cardinality.AllowsZero(c1));
                        AdoptChildExpression(e1);
                    }
                }

                ValueComparison vc = new ValueComparison(e0, singletonOperator, e1);

                vc.ResultWhenEmpty = BooleanValue.FALSE;
                ExpressionTool.CopyLocationInfo(this, vc);
                Optimizer.Trace(config, "Replaced general comparison by value comparison", vc);
                return vc.TypeCheck(visitor, contextInfo);
            }

            IStaticContext env = visitor.StaticContext;
            string defaultCollationName = GetRetainedStaticContext().DefaultCollationName;
            IStringCollator collation = config.GetCollation(defaultCollationName);
            if (collation == null)
            {
                collation = CodepointCollator.GetInstance();
            }

            comparer = GenericAtomicComparer.MakeAtomicComparer(pt0, pt1, collation, config.ConversionContext);

            // evaluate the expression now if both arguments are constant
            if ((GetLhsExpression() is Literal) && (GetRhsExpression() is Literal))
            {
                return Literal.MakeLiteral(EvaluateItem(env.MakeEarlyEvaluationContext()), this);
            }

            return this;
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        private static Expression MakeMinOrMax(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, Expression exp, string function)
        {
            if (Cardinality.AllowsMany(exp.GetCardinality()))
            {
                SystemFunction fn = SystemFunction.MakeFunction(function, exp.GetRetainedStaticContext(), 1);
                ((Minimax)fn).SetIgnoreNaN(true);
                Expression x = fn.MakeOptimizedFunctionCall(visitor, contextInfo, exp);
                if (x == null)
                {
                    x = fn.MakeFunctionCall(exp);
                }

                return x;
            }
            else
            {
                return exp;
            }
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        private bool MayInvolveCastToQName(TypeHierarchy th, Expression e1, Expression e2)
        {
            // Guard the atomized item type: it may be AnyItemType (e.g. an untyped node atomizes to a
            // non-ISimpleType) — Saxon checks `s1 instanceof AtomicType` before the namespace-sensitive test.
            // An unconditional (ISimpleType) cast threw InvalidCastException (functx:path-to-node-with-pos).
            OutSmart.DAXon.Types.ItemType s1 = e1.GetItemType().GetAtomizedItemType();
            bool nsSensitive = s1 == BuiltInAtomicType.ANY_ATOMIC || (s1 is ISimpleType && ((ISimpleType)s1).IsNamespaceSensitive());
            return nsSensitive && th.Relationship(e2.GetItemType().GetAtomizedItemType(), BuiltInAtomicType.UNTYPED_ATOMIC) != Affinity.DISJOINT && (e2.GetSpecialProperties() & StaticProperty.NOT_UNTYPED_ATOMIC) == 0;
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public override bool Equals(object other)
        {
            return other is GeneralComparison && base.Equals(other) && comparer.Equals(((GeneralComparison)other).comparer);
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode();
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            IStaticContext env = visitor.StaticContext;
            Lhs.Optimize(visitor, contextInfo);
            Rhs.Optimize(visitor, contextInfo);

            // If either operand is statically empty, return false
            if (Literal.IsEmptySequence(GetLhsExpression()) || Literal.IsEmptySequence(GetRhsExpression()))
            {
                return Literal.MakeLiteral(BooleanValue.FALSE, this);
            }


            // Neither operand needs to be sorted
            SetLhsExpression(GetLhsExpression().Unordered(false, false));
            SetRhsExpression(GetRhsExpression().Unordered(false, false));
            if (GetLhsExpression() is Literal && GetRhsExpression() is Literal)
            {
                return Literal.MakeLiteral(EvaluateItem(visitor.StaticContext.MakeEarlyEvaluationContext()).Materialize(), this);
            }

            Types.ItemType t0 = GetLhsExpression().GetItemType();
            Types.ItemType t1 = GetRhsExpression().GetItemType();
            int c0 = GetLhsExpression().GetCardinality();
            int c1 = GetRhsExpression().GetCardinality();

            // Check if neither argument allows a sequence of >1
            bool many0 = Cardinality.AllowsMany(c0);
            bool many1 = Cardinality.AllowsMany(c1);
            if (many0)
            {
                if (many1)
                {
                    comparisonCardinality = ComparisonCardinality.MANY_TO_MANY;
                }
                else
                {
                    comparisonCardinality = ComparisonCardinality.MANY_TO_ONE;
                }
            }
            else
            {
                if (many1)
                {
                    GeneralComparison mc = InverseComparison;
                    mc.comparisonCardinality = ComparisonCardinality.MANY_TO_ONE;
                    ExpressionTool.CopyLocationInfo(this, mc);
                    mc.comparer = comparer;
                    mc.runtimeCheckNeeded = runtimeCheckNeeded;
                    return mc.Optimize(visitor, contextInfo);
                }
                else
                {
                    comparisonCardinality = ComparisonCardinality.ONE_TO_ONE;
                }
            }


            // look for (N to M = I)
            if (@operator == Token.EQUALS)
            {

                // First a variable range...
                if (GetLhsExpression() is RangeExpression)
                {
                    Expression min = ((RangeExpression)GetLhsExpression()).StartExpression;
                    Expression max = ((RangeExpression)GetLhsExpression()).EndExpression;
                    IntegerRangeTest ir = new IntegerRangeTest(GetRhsExpression(), min, max);
                    ExpressionTool.CopyLocationInfo(this, ir);
                    return ir;
                }

                if (GetRhsExpression() is RangeExpression)
                {
                    Expression min = ((RangeExpression)GetRhsExpression()).StartExpression;
                    Expression max = ((RangeExpression)GetRhsExpression()).EndExpression;
                    IntegerRangeTest ir = new IntegerRangeTest(GetLhsExpression(), min, max);
                    ExpressionTool.CopyLocationInfo(this, ir);
                    return ir;
                }


                // Now a fixed range...
                if (GetLhsExpression() is Literal)
                {
                    IGroundedValue value0 = ((Literal)GetLhsExpression()).GroundedValue;
                    if (value0 is IntegerRange && ((IntegerRange)value0).GetStep() == 1)
                    {
                        long min = ((IntegerRange)value0).Start;
                        long max = ((IntegerRange)value0).End;
                        IntegerRangeTest ir = new IntegerRangeTest(GetRhsExpression(), Literal.MakeLiteral(Int64Value.MakeIntegerValue(min), this), Literal.MakeLiteral(Int64Value.MakeIntegerValue(max), this));
                        ExpressionTool.CopyLocationInfo(this, ir);
                        return ir;
                    }
                }

                if (GetRhsExpression() is Literal)
                {
                    IGroundedValue value1 = ((Literal)GetRhsExpression()).GroundedValue;
                    if (value1 is IntegerRange && ((IntegerRange)value1).GetStep() == 1)
                    {
                        long min = ((IntegerRange)value1).Start;
                        long max = ((IntegerRange)value1).End;
                        IntegerRangeTest ir = new IntegerRangeTest(GetLhsExpression(), Literal.MakeLiteral(Int64Value.MakeIntegerValue(min), this), Literal.MakeLiteral(Int64Value.MakeIntegerValue(max), this));
                        ExpressionTool.CopyLocationInfo(this, ir);
                        return ir;
                    }
                }
            }


            // If the operator is gt, ge, lt, le then replace X < Y by min(X) < max(Y)
            // This optimization is done only in the case where at least one of the
            // sequences is known to be purely numeric. It isn't safe if both sequences
            // contain untyped atomic values, because in that case, the type of the
            // comparison isn't known in advance. For example [(1, U1) < ("fred", U2)]
            // involves both string and numeric comparisons.
            // Generally, do this optimization for a many-to-many comparison, because it prevents
            // early exit on a many-to-one comparison. But with a many-to-one comparison, do it
            // if the "many" branch can be lifted up the expression tree.
            if (@operator != Token.EQUALS && @operator != Token.NE && (comparisonCardinality == ComparisonCardinality.MANY_TO_MANY || comparisonCardinality == ComparisonCardinality.MANY_TO_ONE && (ManyOperandIsLiftable() || ManyOperandIsRangeExpression())) && (NumericType.IsNumericType(t0) || NumericType.IsNumericType(t1)))
            {

                ValueComparison vc;
                switch (@operator)
                {
                    case Token.LT:
                    case Token.LE:
                        vc = new ValueComparison(MakeMinOrMax(visitor, contextInfo, GetLhsExpression(), "min"), singletonOperator, MakeMinOrMax(visitor, contextInfo, GetRhsExpression(), "max"));
                        vc.ResultWhenEmpty = BooleanValue.FALSE;

                        break;
                    case Token.GT:
                    case Token.GE:
                        vc = new ValueComparison(MakeMinOrMax(visitor, contextInfo, GetLhsExpression(), "max"), singletonOperator, MakeMinOrMax(visitor, contextInfo, GetRhsExpression(), "min"));
                        vc.ResultWhenEmpty = BooleanValue.FALSE;

                        break;
                    default:
                        throw new NotSupportedException("Unknown operator " + @operator);
                }

                ExpressionTool.CopyLocationInfo(this, vc);
                vc.SetRetainedStaticContext(GetRetainedStaticContext());
                return vc.TypeCheck(visitor, contextInfo);
            }


            // evaluate the expression now if both arguments are constant
            if ((GetLhsExpression() is Literal) && (GetRhsExpression() is Literal))
            {
                return Literal.MakeLiteral(EvaluateItem(env.MakeEarlyEvaluationContext()), this);
            }


            // Finally, convert to use the GeneralComparisonEE algorithm if in Saxon-EE
            return visitor.ObtainOptimizer().OptimizeGeneralComparison(visitor, this, false, contextInfo);
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        private bool ManyOperandIsLiftable()
        {
            if (ParentExpression is IContextSwitchingExpression && ((IContextSwitchingExpression)ParentExpression).GetActionExpression() == this)
            {
                foreach (Operand o in Operands())
                {
                    if (Cardinality.AllowsMany(o.GetChildExpression().GetCardinality()))
                    {
                        if (ExpressionTool.DependsOnFocus(o.GetChildExpression()))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            return false;
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        private bool ManyOperandIsRangeExpression()
        {
            foreach (Operand o in Operands())
            {
                Expression e = o.GetChildExpression();
                if (Cardinality.AllowsMany(e.GetCardinality()))
                {
                    return (e is RangeExpression || e is Literal && ((Literal)e).GroundedValue is IntegerRange);
                }
            }

            return false; // shouldn't reach here.
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public override IItem EvaluateItem(IXPathContext context)
        {
            return BooleanValue.Get(MakeElaborator().ElaborateForBoolean().Eval(context));
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            return MakeElaborator().ElaborateForBoolean().Eval(context);
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public static bool Compare(AtomicValue a0, int @operator, AtomicValue a1, IAtomicComparer comparer, bool checkTypes, IXPathContext context, INamespaceResolver nsResolver)
        {
            bool u0 = a0.IsUntypedAtomic();
            bool u1 = a1.IsUntypedAtomic();
            if (u0 != u1)
            {

                // one value untyped, the other not
                ConversionRules rules = context.GetConfiguration().GetConversionRules();
                if (u0)
                {

                    // a0 is untyped atomic
                    if (a1 is NumericValue)
                    {
                        return UntypedNumericComparer.QuickCompare((StringValue)a0, (NumericValue)a1, @operator, rules);
                    }
                    else if (a1 is StringValue)
                    {
                    }
                    else
                    {
                        IAtomicType prim = a1.PrimitiveType;
                        StringConverter sc = prim.GetStringConverter(rules);
                        if (a1 is QualifiedNameValue)
                        {
                            sc = (StringConverter)sc.SetNamespaceResolver(nsResolver);
                        }

                        a0 = sc.ConvertString(a0.UnicodeStringValue).AsAtomic();
                    }
                }
                else
                {

                    // a1 is untyped atomic
                    if (a0 is NumericValue)
                    {
                        return UntypedNumericComparer.QuickCompare((StringValue)a1, (NumericValue)a0, Token.Inverse(@operator), rules);
                    }
                    else if (a0 is StringValue)
                    {
                    }
                    else
                    {
                        IAtomicType prim = a0.PrimitiveType;
                        StringConverter sc = prim.GetStringConverter(rules);
                        if (a0 is QualifiedNameValue)
                        {
                            sc = (StringConverter)sc.SetNamespaceResolver(nsResolver);
                        }

                        a1 = sc.ConvertString(a1.UnicodeStringValue).AsAtomic();
                    }
                }

                checkTypes = false; // No further checking needed if conversion succeeded
            }

            return ValueComparison.Compare(a0, @operator, a1, comparer, checkTypes);
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public override Types.ItemType GetItemType()
        {
            return BuiltInAtomicType.BOOLEAN;
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.BOOLEAN;
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public static int GetCorrespondingSingletonOperator(int op)
        {
            switch (op)
            {
                case Token.EQUALS:
                    return Token.FEQ;
                case Token.GE:
                    return Token.FGE;
                case Token.NE:
                    return Token.FNE;
                case Token.LT:
                    return Token.FLT;
                case Token.GT:
                    return Token.FGT;
                case Token.LE:
                    return Token.FLE;
                default:
                    return op;
            }
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        protected override string Tag()
        {
            return "gc";
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        protected override void ExplainExtraAttributes(ExpressionPresenter @out)
        {
            string cc = "";
            switch (comparisonCardinality)
            {
                case ComparisonCardinality.ONE_TO_ONE:
                    cc = "1:1";
                    break;
                case ComparisonCardinality.MANY_TO_ONE:
                    cc = "N:1";
                    break;
                case ComparisonCardinality.MANY_TO_MANY:
                    cc = "M:N";
                    break;
            }

            @out.EmitAttribute("card", cc);
            @out.EmitAttribute("comp", comparer.Save());
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        public override Elaborator GetElaborator()
        {
            return new GeneralComparisonElaborator();
        }
        public enum ComparisonCardinality
        {
            ONE_TO_ONE,
            MANY_TO_ONE,
            MANY_TO_MANY
        }

        /*c0 == StaticProperty.EXACTLY_ONE*/
        /*c1 == StaticProperty.EXACTLY_ONE */
        /// <summary>
        /// Elaborator for a general comparison expression such as (A = B).
        /// </summary>
        internal sealed class GeneralComparisonElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                GeneralComparison exp = (GeneralComparison)GetExpression();
                ComparisonCardinality cardinality = exp.GetComparisonCardinality();
                bool needsRunTimeCheck = exp.NeedsRuntimeCheck();
                IAtomicComparer comparer = exp.GetAtomicComparer();
                RetainedStaticContext staticContext = exp.GetRetainedStaticContext();
                int singletonOperator = exp.SingletonOperator;

                // Fused `childName = $string` filter predicate (the join-by-field shape): matching
                // children are compared straight off the Tiny arrays against the comparand's string,
                // no per-row atomizing iterator, no comparand re-iteration. Existence semantics and
                // codepoint equality are exactly the generic pair-loop's behaviour for
                // untypedAtomic-vs-string under the default collation. The generic runtime
                // comparability check is a no-op for this pair (untypedAtomic from an untyped
                // atomize vs a statically xs:string comparand is always comparable), so the flag
                // does not gate the fusion; other operators, non-codepoint collation, a comparand
                // that might allow many items or contain a constant-folded error stay generic.
                if (singletonOperator == Token.FEQ && comparer is Sorting.CodepointCollatingComparer)
                {
                    Expression childSide = null, cmpSide = null;
                    if (Elaboration.FusedChildAtomizer.MatchAtomizer(exp.GetLhsExpression(), out int lfp)
                        && exp.GetLhsExpression() is Atomizer la && la.IsUntyped())
                    {
                        childSide = exp.GetLhsExpression();
                        cmpSide = exp.GetRhsExpression();
                    }
                    else if (Elaboration.FusedChildAtomizer.MatchAtomizer(exp.GetRhsExpression(), out int rfp)
                        && exp.GetRhsExpression() is Atomizer ra && ra.IsUntyped())
                    {
                        childSide = exp.GetRhsExpression();
                        cmpSide = exp.GetLhsExpression();
                    }

                    // The comparand's static type is often erased (a variable without as=), so the
                    // singleton/string gates run per evaluation; the off-path re-evaluation through
                    // the generic loop is only allowed for pure memoized comparands.
                    if (childSide != null
                        && (cmpSide is LocalVariableReference || cmpSide is GlobalVariableReference || cmpSide is Literal)
                        && !ErrorExpression.IsContainedIn(cmpSide))
                    {
                        Elaboration.FusedChildAtomizer.MatchAtomizer(childSide, out int childFp);
                        IPullEvaluator cmpEval = cmpSide.MakeElaborator().ElaborateForPull();
                        IBooleanEvaluator genericFallback = BuildGenericBoolean(exp, cardinality, needsRunTimeCheck, comparer, staticContext, singletonOperator);
                        return (context) =>
                        {
                            ISequenceIterator cmpIter = cmpEval.Iterate(context);
                            IItem cmp = cmpIter.Next();
                            if (cmp == null)
                            {
                                return false;   // empty comparand: no pair can match
                            }

                            if (!(cmp is StringValue) || cmpIter.Next() != null)
                            {
                                // non-string or multi-item comparand: untyped values convert to the
                                // comparand's own type in a general comparison — generic loop
                                cmpIter.Dispose();
                                return genericFallback.Eval(context);
                            }

                            if (context.GetContextItem() is Trees.Tiny.TinyParentNodeImpl tiny && tiny.tree.TypeArray == null)
                            {
                                Text.UnicodeString target = cmp.UnicodeStringValue;
                                Trees.Tiny.TinyTree tree = tiny.tree;
                                int p = tiny.nodeNr;
                                int child = p + 1;
                                if (child < tree.numberOfNodes && tree.depth[child] == tree.depth[p] + 1)
                                {
                                    byte[] kinds = tree.nodeKind;
                                    int[] nextArr = tree.next;
                                    int[] nameCodes = tree.nameCode;
                                    int n = child;
                                    while (n >= 0)
                                    {
                                        int cur = n;
                                        int n2 = nextArr[cur];
                                        n = n2 > cur ? n2 : -1;
                                        int k = kinds[cur];
                                        if ((k == Types.Type.ELEMENT || k == Types.Type.TEXTUAL_ELEMENT)
                                            && (nameCodes[cur] & NamePool.FP_MASK) == childFp
                                            && Trees.Tiny.TinyParentNodeImpl.GetStringValue(tree, cur).Equals(target))
                                        {
                                            return true;
                                        }
                                    }
                                }

                                return false;
                            }

                            return genericFallback.Eval(context);
                        };
                    }
                }

                return BuildGenericBoolean(exp, cardinality, needsRunTimeCheck, comparer, staticContext, singletonOperator);
            }

            private IBooleanEvaluator BuildGenericBoolean(GeneralComparison exp, ComparisonCardinality cardinality, bool needsRunTimeCheck, IAtomicComparer comparer, RetainedStaticContext staticContext, int singletonOperator)
            {
                switch (cardinality)
                {
                    case ComparisonCardinality.ONE_TO_ONE:
                        {
                            IItemEvaluator p0 = exp.GetLhsExpression().MakeElaborator().ElaborateForItem();
                            IItemEvaluator p1 = exp.GetRhsExpression().MakeElaborator().ElaborateForItem();
                            return (context) =>
                            {
                                AtomicValue av0 = (AtomicValue)p0.Eval(context);
                                if (av0 == null)
                                {
                                    return false;
                                }

                                AtomicValue av1 = (AtomicValue)p1.Eval(context);
                                if (av1 == null)
                                {
                                    return false;
                                }

                                return Compare(av0, singletonOperator, av1, comparer.ProvideContext(context), needsRunTimeCheck, context, staticContext);
                            };
                        }

                    case ComparisonCardinality.MANY_TO_ONE:
                        {
                            IPullEvaluator p0 = exp.GetLhsExpression().MakeElaborator().ElaborateForPull();
                            IItemEvaluator p1 = exp.GetRhsExpression().MakeElaborator().ElaborateForItem();
                            return (context) => EvaluateManyToOne(p0.Iterate(context), (AtomicValue)p1.Eval(context), singletonOperator, comparer, needsRunTimeCheck, staticContext, exp.GetLocation(), context);
                        }

                    case ComparisonCardinality.MANY_TO_MANY:
                        {
                            IPullEvaluator p0 = exp.GetLhsExpression().MakeElaborator().ElaborateForPull();
                            IPullEvaluator p1 = exp.GetRhsExpression().MakeElaborator().ElaborateForPull();
                            return (context) => EvaluateManyToMany(p0.Iterate(context), p1.Iterate(context), singletonOperator, comparer, needsRunTimeCheck, staticContext, exp.GetLocation(), context);
                        }

                    default:
                        throw new NotSupportedException();
                }
            }

            // `exists x in range: x op value`, decided from the range bounds (bounds access does not
            // consume the iterator). Extracted from the EvaluateManyToOne fast path so the
            // many-to-many route can reuse it; untyped conversion mirrors the original code.
            private static bool RangeOpValue(RangeIterator ri, int singletonOperator, AtomicValue value)
            {
                if (value.IsUntypedAtomic())
                {
                    value = StringConverter.StringToInteger.INSTANCE.ConvertString(value.UnicodeStringValue).AsAtomic();
                }

                switch (singletonOperator)
                {
                    case Token.FEQ:
                        return ri.ContainsEq((NumericValue)value);
                    case Token.FNE:
                        return ri.First.CompareTo(ri.GetLast()) != 0 || ri.First.CompareTo(((NumericValue)value)) != 0;
                    case Token.FLE:
                        return ri.GetMin().CompareTo(((NumericValue)value)) <= 0;
                    case Token.FLT:
                        return ri.GetMin().CompareTo(((NumericValue)value)) < 0;
                    case Token.FGE:
                        return ri.GetMax().CompareTo(((NumericValue)value)) >= 0;
                    case Token.FGT:
                        return ri.GetMax().CompareTo(((NumericValue)value)) > 0;
                    default:
                        throw new InvalidOperationException();
                }
            }

            // `exists x in r0, y in r1: x op y` from the two ranges' bounds. FEQ is interval
            // overlap (integer ranges hold every integer between their bounds).
            private static bool RangeOpRange(RangeIterator r0, int singletonOperator, RangeIterator r1)
            {
                switch (singletonOperator)
                {
                    case Token.FEQ:
                        return r0.GetMin().CompareTo(r1.GetMax()) <= 0 && r0.GetMax().CompareTo(r1.GetMin()) >= 0;
                    case Token.FNE:
                        return r0.First.CompareTo(r0.GetLast()) != 0 || r1.First.CompareTo(r1.GetLast()) != 0
                            || r0.First.CompareTo(r1.First) != 0;
                    case Token.FLE:
                        return r0.GetMin().CompareTo(r1.GetMax()) <= 0;
                    case Token.FLT:
                        return r0.GetMin().CompareTo(r1.GetMax()) < 0;
                    case Token.FGE:
                        return r0.GetMax().CompareTo(r1.GetMin()) >= 0;
                    case Token.FGT:
                        return r0.GetMax().CompareTo(r1.GetMin()) > 0;
                    default:
                        throw new InvalidOperationException();
                }
            }

            // x op y  ⟺  y invert(op) x
            private static int InvertOperator(int singletonOperator)
            {
                switch (singletonOperator)
                {
                    case Token.FLT: return Token.FGT;
                    case Token.FLE: return Token.FGE;
                    case Token.FGT: return Token.FLT;
                    case Token.FGE: return Token.FLE;
                    default: return singletonOperator;
                }
            }

            public bool EvaluateManyToOne(ISequenceIterator iter0, AtomicValue value1, int singletonOperator, IAtomicComparer comparer, bool runTimeCheckNeeded, RetainedStaticContext staticContext, ILocation loc, IXPathContext context)
            {
                try
                {
                    if (value1 == null || (value1.IsNaN() && singletonOperator != Token.FNE))
                    {
                        iter0.Dispose();
                        return false;
                    }

                    if (iter0 is RangeIterator)
                    {
                        return RangeOpValue((RangeIterator)iter0, singletonOperator, value1);
                    }

                    AtomicValue item0;
                    IAtomicComparer boundComparer = comparer.ProvideContext(context);
                    while ((item0 = (AtomicValue)iter0.Next()) != null)
                    {
                        if (Compare(item0, singletonOperator, value1, boundComparer, runTimeCheckNeeded, context, staticContext))
                        {
                            iter0.Dispose();
                            return true;
                        }
                    }

                    return false;
                }
                catch (XPathException e)
                {
                    throw e.MaybeWithLocation(loc).MaybeWithContext(context);
                }
            }

            public bool EvaluateManyToMany(ISequenceIterator iter0, ISequenceIterator iter1, int singletonOperator, IAtomicComparer comparer, bool runTimeCheckNeeded, RetainedStaticContext staticContext, ILocation loc, IXPathContext context)
            {
                try
                {
                    // Range operands fold to a bounds decision instead of an element scan — the
                    // shortcut EvaluateManyToOne has always taken; a one-to-many comparison lands
                    // here because the elaborator has no dedicated ONE_TO_MANY route.
                    if (iter0 is RangeIterator && iter1 is RangeIterator)
                    {
                        bool res = RangeOpRange((RangeIterator)iter0, singletonOperator, (RangeIterator)iter1);
                        iter0.Dispose();
                        iter1.Dispose();
                        return res;
                    }

                    if (iter0 is RangeIterator || iter1 is RangeIterator)
                    {
                        bool rangeOnLeft = iter0 is RangeIterator;
                        RangeIterator range = (RangeIterator)(rangeOnLeft ? iter0 : iter1);
                        ISequenceIterator items = rangeOnLeft ? iter1 : iter0;
                        // exists x in range: x op y decides item-on-right; item-on-left inverts.
                        int rangeOp = rangeOnLeft ? singletonOperator : InvertOperator(singletonOperator);
                        IAtomicComparer itemComparer = comparer.ProvideContext(context);
                        AtomicValue item;
                        while ((item = (AtomicValue)items.Next()) != null)
                        {
                            bool hit;
                            if (item.IsNaN() && singletonOperator != Token.FNE)
                            {
                                continue;
                            }

                            if (item is NumericValue || item.IsUntypedAtomic())
                            {
                                hit = RangeOpValue(range, rangeOp, item);
                            }
                            else
                            {
                                // Non-numeric operand: comparing it to any of the range's integers
                                // behaves identically (XPTY0004 or false) — probe one element.
                                hit = rangeOnLeft
                                    ? Compare(range.First, singletonOperator, item, itemComparer, runTimeCheckNeeded, context, staticContext)
                                    : Compare(item, singletonOperator, range.First, itemComparer, runTimeCheckNeeded, context, staticContext);
                            }

                            if (hit)
                            {
                                iter0.Dispose();
                                iter1.Dispose();
                                return true;
                            }
                        }

                        range.Dispose();
                        return false;
                    }

                    bool exhausted0 = false;
                    bool exhausted1 = false;
                    IList<AtomicValue> value0 = new List<AtomicValue>();
                    IList<AtomicValue> value1 = new List<AtomicValue>();
                    IAtomicComparer boundComparer = comparer.ProvideContext(context);

                    // Read items from the two sequences alternately, in each case comparing the item to
                    // all items that have previously been read from the other sequence. In the worst case
                    // the number of comparisons is N*M, and the memory usage is (max(N,M)*2) where N and M
                    // are the number of items in the two sequences. In practice, either M or N is often 1,
                    // meaning that in this case neither list will ever hold more than one item.
                    while (true)
                    {
                        if (!exhausted0)
                        {
                            AtomicValue item0 = (AtomicValue)iter0.Next();
                            if (item0 == null)
                            {
                                if (exhausted1)
                                {
                                    return false;
                                }

                                exhausted0 = true;
                            }
                            else
                            {
                                foreach (AtomicValue item1 in value1)
                                {
                                    if (Compare(item0, singletonOperator, item1, boundComparer, runTimeCheckNeeded, context, staticContext))
                                    {
                                        iter0.Dispose();
                                        iter1.Dispose();
                                        return true;
                                    }
                                }

                                if (!exhausted1)
                                {
                                    value0.Add(item0);
                                }
                            }
                        }

                        if (!exhausted1)
                        {
                            AtomicValue item1 = (AtomicValue)iter1.Next();
                            if (item1 == null)
                            {
                                if (exhausted0)
                                {
                                    return false;
                                }

                                exhausted1 = true;
                            }
                            else
                            {
                                foreach (AtomicValue item0 in value0)
                                {
                                    if (Compare(item0, singletonOperator, item1, boundComparer, runTimeCheckNeeded, context, staticContext))
                                    {
                                        iter0.Dispose();
                                        iter1.Dispose();
                                        return true;
                                    }
                                }

                                if (!exhausted0)
                                {
                                    value1.Add(item1);
                                }
                            }
                        }
                    }
                }
                catch (XPathException e)
                {
                    throw e.MaybeWithLocation(loc).MaybeWithContext(context);
                }
            }
        }
    }
}
