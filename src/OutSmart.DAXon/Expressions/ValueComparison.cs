////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    public sealed class ValueComparison : BinaryExpression, IComparisonExpression, INegatable
    {
        private BooleanValue resultWhenEmpty = null;
        private bool needsRuntimeCheck;

        public override string ExpressionName => "ValueComparison";

        public IStringCollator StringCollator
        {
            get
            {
                try
                {
                    return GetConfiguration().GetCollation((GetRetainedStaticContext().DefaultCollationName));
                }
                catch (XPathException e)
                {
                    throw new InvalidOperationException(e.Message, e);
                }
            }
        }

        public int SingletonOperator => @operator;

        public BooleanValue ResultWhenEmpty { get => resultWhenEmpty; set => resultWhenEmpty = value; }
        public ValueComparison(Expression p1, int op, Expression p2) : base(p1, op, p2)
        {
        }

        public IAtomicComparer GetAtomicComparer()
        {

            // TODO: this is scaffolding. ValueComparison no longer uses an IAtomicComparer, but this method
            // is retained for paths that require one, e.g. EqualityPatternOptimizer
            ItemType t0 = GetLhsExpression().GetItemType().GetPrimitiveItemType();
            if (!(t0 is BuiltInAtomicType))
            {

                // This can happen after loading from a SEF file; the static type information is not always available
                t0 = BuiltInAtomicType.ANY_ATOMIC;
            }

            ItemType t1 = GetRhsExpression().GetItemType().GetPrimitiveItemType();
            if (!(t1 is BuiltInAtomicType))
            {

                // This can happen after loading from a SEF file; the static type information is not always available
                t1 = BuiltInAtomicType.ANY_ATOMIC;
            }

            return GenericAtomicComparer.MakeAtomicComparer((BuiltInAtomicType)t0, (BuiltInAtomicType)t1, StringCollator, GetConfiguration().ConversionContext);
        }

        public bool ConvertsUntypedToOther()
        {
            return false;
        }

        public bool NeedsRuntimeComparabilityCheck()
        {
            return needsRuntimeCheck;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            ResetLocalStaticProperties();
            Lhs.TypeCheck(visitor, contextInfo);
            Rhs.TypeCheck(visitor, contextInfo);
            Configuration config = visitor.GetConfiguration();
            if (Literal.IsEmptySequence(GetLhsExpression()))
            {
                return resultWhenEmpty == null ? GetLhsExpression() : Literal.MakeLiteral(resultWhenEmpty, this);
            }

            if (Literal.IsEmptySequence(GetRhsExpression()))
            {
                return resultWhenEmpty == null ? GetRhsExpression() : Literal.MakeLiteral(resultWhenEmpty, this);
            }

            if (ConvertsUntypedToOther())
            {
                return this; // we've already done all that needs to be done
            }

            SequenceType optionalAtomic = SequenceType.OPTIONAL_ATOMIC;
            TypeChecker tc = config.GetTypeChecker(false);
            Func<RoleDiagnostic> role0 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, Token.tokens[@operator], 0);
            SetLhsExpression(tc.StaticTypeCheck(GetLhsExpression(), optionalAtomic, role0, visitor));
            Func<RoleDiagnostic> role1 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, Token.tokens[@operator], 1);
            SetRhsExpression(tc.StaticTypeCheck(GetRhsExpression(), optionalAtomic, role1, visitor));
            IPlainType t0 = (IPlainType)GetLhsExpression().GetItemType().GetAtomizedItemType();
            IPlainType t1 = (IPlainType)GetRhsExpression().GetItemType().GetAtomizedItemType();
            if (t0.GetUType().Union(t1.GetUType()).Overlaps(UType.EXTENSION))
            {
                throw new XPathException("Cannot perform comparisons involving external objects").AsTypeError().WithErrorCode("XPTY0004").WithLocation(GetLocation());
            }

            BuiltInAtomicType p0 = (BuiltInAtomicType)t0.GetPrimitiveItemType();
            if (p0.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                p0 = BuiltInAtomicType.STRING;
            }

            BuiltInAtomicType p1 = (BuiltInAtomicType)t1.GetPrimitiveItemType();
            if (p1.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                p1 = BuiltInAtomicType.STRING;
            }

            needsRuntimeCheck = p0.Equals(BuiltInAtomicType.ANY_ATOMIC) || p1.Equals(BuiltInAtomicType.ANY_ATOMIC);
            if (!needsRuntimeCheck && !Types.Type.IsPossiblyComparable(p0, p1, visitor.StaticContext.GetXPathVersion()))
            {
                bool opt0 = Cardinality.AllowsZero(GetLhsExpression().GetCardinality());
                bool opt1 = Cardinality.AllowsZero(GetRhsExpression().GetCardinality());
                if (opt0 || opt1)
                {

                    // This is a comparison such as (xs:integer? eq xs:date?). This is almost
                    // certainly an error, but we need to let it through because it will work if
                    // one of the operands is an empty sequence.
                    string which = null;
                    if (opt0)
                    {
                        which = "the first operand is";
                    }

                    if (opt1)
                    {
                        which = "the second operand is";
                    }

                    if (opt0 && opt1)
                    {
                        which = "one or both operands are";
                    }

                    visitor.StaticContext.IssueWarning("Comparison of " + t0 + (opt0 ? "?" : "") + " to " + t1 + (opt1 ? "?" : "") + " will fail unless " + which + " empty", DAXonErrorCode.SXWN9026, GetLocation());
                    needsRuntimeCheck = true;
                }
                else
                {
                    string message = "In {" + ToShortString() + "}: cannot compare " + t0 + " to " + t1;
                    throw new XPathException(message).AsTypeError().WithErrorCode("XPTY0004").WithLocation(GetLocation());
                }
            }

            if (!(@operator == Token.FEQ || @operator == Token.FNE))
            {
                MustBeOrdered(t0, p0);
                MustBeOrdered(t1, p1);
            }

            return this;
        }

        private void MustBeOrdered(IPlainType t1, BuiltInAtomicType p1)
        {
            if (!p1.IsOrdered(true))
            {
                throw new XPathException("Type " + t1.ToString() + " is not an ordered type").WithErrorCode("XPTY0004").AsTypeError().WithLocation(GetLocation());
            }
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Lhs.Optimize(visitor, contextInfo);
            Rhs.Optimize(visitor, contextInfo);
            return visitor.ObtainOptimizer().OptimizeValueComparison(this, visitor, contextInfo);
        }

        public bool IsNegatable(TypeHierarchy th)
        {

            // Expression is not negatable if it might involve NaN
            return IsNeverNaN(GetLhsExpression(), th) && IsNeverNaN(GetRhsExpression(), th);
        }

        private bool IsNeverNaN(Expression exp, TypeHierarchy th)
        {
            return th.Relationship(exp.GetItemType(), BuiltInAtomicType.DOUBLE) == Affinity.DISJOINT && th.Relationship(exp.GetItemType(), BuiltInAtomicType.FLOAT) == Affinity.DISJOINT;
        }

        public Expression Negate()
        {
            ValueComparison vc = new ValueComparison(GetLhsExpression(), Token.Negate(@operator), GetRhsExpression());
            if (resultWhenEmpty == null || resultWhenEmpty == BooleanValue.FALSE)
            {
                vc.resultWhenEmpty = BooleanValue.TRUE;
            }
            else
            {
                vc.resultWhenEmpty = BooleanValue.FALSE;
            }

            vc.needsRuntimeCheck = needsRuntimeCheck;
            ExpressionTool.CopyLocationInfo(this, vc);
            return vc;
        }

        public override bool Equals(object other)
        {
            return other is ValueComparison && base.Equals(other);
        }

        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ValueComparison vc = new ValueComparison(GetLhsExpression().Copy(rebindings), @operator, GetRhsExpression().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, vc);
            vc.resultWhenEmpty = resultWhenEmpty;
            vc.needsRuntimeCheck = needsRuntimeCheck;
            return vc;
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            return MakeElaborator().ElaborateForBoolean().Eval(context);
        }

        public static bool Compare(AtomicValue v0, int op, AtomicValue v1, IAtomicComparer comparer, bool checkTypes)
        {
            if (checkTypes && !Types.Type.IsGuaranteedComparable(v0.PrimitiveType, v1.PrimitiveType, Token.IsOrderedOperator(op)))
            {
                throw new XPathException("Cannot compare " + Types.Type.DisplayTypeName(v0) + " to " + Types.Type.DisplayTypeName(v1)).WithErrorCode("XPTY0004").AsTypeError();
            }

            if (v0.IsNaN() || v1.IsNaN())
            {
                return op == Token.FNE;
            }

            try
            {
                switch (op)
                {
                    case Token.FEQ:
                        return comparer.ComparesEqual(v0, v1);
                    case Token.FNE:
                        return !comparer.ComparesEqual(v0, v1);
                    case Token.FGT:
                        return comparer.CompareAtomicValues(v0, v1) > 0;
                    case Token.FLT:
                        return comparer.CompareAtomicValues(v0, v1) < 0;
                    case Token.FGE:
                        return comparer.CompareAtomicValues(v0, v1) >= 0;
                    case Token.FLE:
                        return comparer.CompareAtomicValues(v0, v1) <= 0;
                    default:
                        throw new NotSupportedException("Unknown operator " + op);
                }
            }
            catch (ComparisonException err)
            {
                throw err.GetReason();
            }
            catch (InvalidCastException)
            {

                throw new XPathException("Cannot compare " + Types.Type.DisplayTypeName(v0) + " to " + Types.Type.DisplayTypeName(v1)).WithErrorCode("XPTY0004").AsTypeError();
            }
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return (BooleanValue)MakeElaborator().ElaborateForItem().Eval(context);
        }

        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.BOOLEAN;
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.BOOLEAN;
        }

        protected override int ComputeCardinality()
        {
            if (resultWhenEmpty != null)
            {
                return StaticProperty.EXACTLY_ONE;
            }
            else
            {
                return base.ComputeCardinality();
            }
        }

        protected override string Tag()
        {
            return "vc";
        }

        protected override void ExplainExtraAttributes(ExpressionPresenter @out)
        {
            if (resultWhenEmpty != null)
            {
                @out.EmitAttribute("onEmpty", resultWhenEmpty.GetBooleanValue() ? "1" : "0");
            }

            if (@out.GetOptions().target == "JS" && @out.GetOptions().targetVersion >= 2)
            {

                // for backwards compatibility, output a comp attribute
                IAtomicComparer comparer = GetAtomicComparer();
                @out.EmitAttribute("comp", comparer.Save());
            }
        }

        public override Elaborator GetElaborator()
        {
            return new ValueComparisonElaborator();
        }

        internal sealed class ValueComparisonElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                ValueComparison expr = (ValueComparison)GetExpression();
                IItemEvaluator p0 = expr.GetLhsExpression().MakeElaborator().ElaborateForItem();
                IItemEvaluator p1 = expr.GetRhsExpression().MakeElaborator().ElaborateForItem();
                BooleanValue resultWhenEmpty = expr.ResultWhenEmpty;
                IStringCollator defaultCollation;
                try
                {
                    defaultCollation = expr.GetConfiguration().GetCollation(expr.GetRetainedStaticContext().DefaultCollationName);
                }
                catch (XPathException)
                {
                    throw new InvalidOperationException("Unknown default collation in static context: " + expr.GetRetainedStaticContext().DefaultCollationName);
                }

                int @operator = expr.Operator;
                int card0 = expr.GetLhsExpression().GetCardinality();
                int card1 = expr.GetRhsExpression().GetCardinality();
                if (card0 == StaticProperty.ALLOWS_ZERO || card1 == StaticProperty.ALLOWS_ZERO)
                {
                    return (context) => resultWhenEmpty;
                }

                GenericAtomicComparer.IAtomicComparisonFunction comparer = GenericAtomicComparer.MakeAtomicComparisonFunction(OperandType(expr.GetLhsExpression()), OperandType(expr.GetRhsExpression()), defaultCollation, @operator, true, expr.GetRetainedStaticContext().GetPackageData().HostLanguageVersion);
                bool nullable0 = Cardinality.AllowsZero(card0);
                bool nullable1 = Cardinality.AllowsZero(card1);
                if (!nullable0 && !nullable1)
                {
                    return (context) => BooleanValue.Get(comparer.Compare((AtomicValue)p0.Eval(context), (AtomicValue)p1.Eval(context), context));
                }
                else
                {
                    return (context) =>
                    {
                        AtomicValue v0 = (AtomicValue)p0.Eval(context);
                        if (v0 == null)
                        {
                            return resultWhenEmpty; // normally false
                        }

                        AtomicValue v1 = (AtomicValue)p1.Eval(context);
                        if (v1 == null)
                        {
                            return resultWhenEmpty; // normally false
                        }

                        return BooleanValue.Get(comparer.Compare(v0, v1, context));
                    };
                }
            }

            // `position() mod M op K` with integer literals evaluates the whole predicate in longs —
            // no Int64Value boxing per item, no comparer dispatch. Byte-identical: XPath integer mod
            // takes the dividend's sign exactly like C# %, and the generic comparer's
            // Int64Value.CompareTo is an exact long comparison (NaN impossible for integers).
            private static IBooleanEvaluator TryFusePositionModComparison(ValueComparison expr)
            {
                if (!(expr.GetLhsExpression() is ArithmeticExpression arith)
                    || arith.Operator != Token.MOD
                    || !(arith.GetLhsExpression() is SystemFunctionCall posCall)
                    || !(posCall.TargetFunction is PositionAndLast.Position posFn)
                    || !(arith.GetRhsExpression() is Literal modLit)
                    || !(modLit.GroundedValue is Int64Value modVal)
                    || !(expr.GetRhsExpression() is Literal cmpLit)
                    || !(cmpLit.GroundedValue is Int64Value cmpVal))
                {
                    return null;
                }

                long m = modVal.LongValue();
                if (m == 0)
                {
                    return null;   // generic path raises FOAR0001
                }

                int op = expr.Operator;
                if (op != Token.FEQ && op != Token.FNE && op != Token.FGT && op != Token.FGE && op != Token.FLT && op != Token.FLE)
                {
                    return null;
                }

                long k = cmpVal.LongValue();
                bool checkFocus = posFn.IsContextPossiblyUndefined();
                return (context) =>
                {
                    IFocusIterator focus = context.GetCurrentIterator();
                    if (focus == null && checkFocus)
                    {
                        throw new XPathException("The context item is absent, so position() is undefined").WithXPathContext(context).WithLocation(posCall.GetLocation()).WithErrorCode("XPDY0002");
                    }

                    long l = focus.Position() % m;
                    switch (op)
                    {
                        case Token.FEQ: return l == k;
                        case Token.FNE: return l != k;
                        case Token.FGT: return l > k;
                        case Token.FGE: return l >= k;
                        case Token.FLT: return l < k;
                        default: return l <= k;
                    }
                };
            }

            private BuiltInAtomicType OperandType(Expression operand)
            {
                ItemType type = operand.GetItemType();
                if (type == AnyItemType.GetInstance())
                {
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
                else
                {
                    return (BuiltInAtomicType)type.GetPrimitiveItemType();
                }
            }

            public override IBooleanEvaluator ElaborateForBoolean()
            {
                ValueComparison expr = (ValueComparison)GetExpression();
                IBooleanEvaluator fusedPositional = TryFusePositionModComparison(expr);
                if (fusedPositional != null)
                {
                    return fusedPositional;
                }

                IItemEvaluator p0 = expr.GetLhsExpression().MakeElaborator().ElaborateForItem();
                IItemEvaluator p1 = expr.GetRhsExpression().MakeElaborator().ElaborateForItem();
                IStringCollator defaultCollation;
                try
                {
                    defaultCollation = expr.GetConfiguration().GetCollation(expr.GetRetainedStaticContext().DefaultCollationName);
                }
                catch (XPathException)
                {
                    throw new InvalidOperationException("Unknown default collation in static context: " + expr.GetRetainedStaticContext().DefaultCollationName);
                }

                int @operator = expr.Operator;
                bool resultWhenEmpty = expr.ResultWhenEmpty != null && expr.ResultWhenEmpty.GetBooleanValue();
                int card0 = expr.GetLhsExpression().GetCardinality();
                int card1 = expr.GetRhsExpression().GetCardinality();
                if (card0 == StaticProperty.ALLOWS_ZERO || card1 == StaticProperty.ALLOWS_ZERO)
                {
                    return (context) => resultWhenEmpty;
                }

                ItemType t0 = expr.GetLhsExpression().GetItemType().GetPrimitiveItemType();
                if (!(t0 is BuiltInAtomicType))
                {

                    // This can happen after loading from a SEF file; the static type information is not always available
                    t0 = BuiltInAtomicType.ANY_ATOMIC;
                }

                ItemType t1 = expr.GetRhsExpression().GetItemType().GetPrimitiveItemType();
                if (!(t1 is BuiltInAtomicType))
                {

                    // This can happen after loading from a SEF file; the static type information is not always available
                    t1 = BuiltInAtomicType.ANY_ATOMIC;
                }

                bool nullable0 = Cardinality.AllowsZero(card0);
                bool nullable1 = Cardinality.AllowsZero(card1);

                // Plain xs:string vs xs:string under the codepoint collation: the verdict is one
                // direct codepoint CompareTo — no comparison-function interface hop, no NaN or
                // type gates (strings have neither). The per-character recursion idiom
                // ($c ge '0' and $c le '9') pays this comparison per character.
                if (ReferenceEquals(t0, BuiltInAtomicType.STRING) && ReferenceEquals(t1, BuiltInAtomicType.STRING)
                    && defaultCollation is CodepointCollator && !nullable0 && !nullable1)
                {
                    int strOp = @operator;
                    return (context) =>
                    {
                        int c = ((StringValue)p0.Eval(context)).Content.CompareTo(((StringValue)p1.Eval(context)).Content);
                        return CompareToConstant.InterpretComparisonResult(strOp, c);
                    };
                }

                // xs:integer vs xs:integer: a total order with no NaN and no promotion, so the
                // polymorphic IntegerValue.CompareTo (Int64 or BigInteger either side) is the
                // whole verdict (string-length($x) = 0 and friends sit on per-call paths).
                if (ReferenceEquals(t0, BuiltInAtomicType.INTEGER) && ReferenceEquals(t1, BuiltInAtomicType.INTEGER)
                    && !nullable0 && !nullable1)
                {
                    int intOp = @operator;
                    return (context) =>
                    {
                        int c = ((IntegerValue)p0.Eval(context)).CompareTo((IntegerValue)p1.Eval(context));
                        return CompareToConstant.InterpretComparisonResult(intOp, c);
                    };
                }

                GenericAtomicComparer.IAtomicComparisonFunction comparer = GenericAtomicComparer.MakeAtomicComparisonFunction((BuiltInAtomicType)t0, (BuiltInAtomicType)t1, defaultCollation, @operator, true, expr.GetRetainedStaticContext().GetPackageData().HostLanguageVersion);
                if (!nullable0 && !nullable1)
                {
                    return (context) => comparer.Compare((AtomicValue)p0.Eval(context), (AtomicValue)p1.Eval(context), context);
                }
                else
                {
                    return (context) =>
                    {
                        AtomicValue v0 = (AtomicValue)p0.Eval(context);
                        if (v0 == null)
                        {
                            return resultWhenEmpty; // normally false
                        }

                        AtomicValue v1 = (AtomicValue)p1.Eval(context);
                        if (v1 == null)
                        {
                            return resultWhenEmpty; // normally false
                        }

                        return comparer.Compare(v0, v1, context);
                    };
                }
            }
        }
    }
}
