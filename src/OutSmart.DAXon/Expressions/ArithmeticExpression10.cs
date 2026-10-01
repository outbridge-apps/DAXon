////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using SequenceType = OutSmart.DAXon.Values.SequenceType;

namespace OutSmart.DAXon.Expressions.Compatibility
{
    /// <summary>
    /// Arithmetic Expression evaluated in XPath 1.0 backwards-compatibility mode: see
    /// <see cref="ArithmeticExpression"/> for the non-backwards-compatible case.
    /// </summary>
    internal sealed class ArithmeticExpression10 : ArithmeticExpression, ICallable
    {
        public ArithmeticExpression10(Expression p0, int @operator, Expression p1) : base(p0, @operator, p1)
        {
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Lhs.TypeCheck(visitor, contextInfo);
            Rhs.TypeCheck(visitor, contextInfo);

            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();

            if (Literal.IsEmptySequence(GetLhsExpression()))
            {
                return Literal.MakeLiteral(DoubleValue.NaN, this);
            }

            if (Literal.IsEmptySequence(GetRhsExpression()))
            {
                return Literal.MakeLiteral(DoubleValue.NaN, this);
            }

            Expression oldOp0 = GetLhsExpression();
            Expression oldOp1 = GetRhsExpression();

            SequenceType atomicType = SequenceType.OPTIONAL_ATOMIC;
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(true);

            Func<RoleDiagnostic> role0 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, Token.tokens[@operator], 0);
            SetLhsExpression(tc.StaticTypeCheck(GetLhsExpression(), atomicType, role0, visitor));

            Func<RoleDiagnostic> role1 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, Token.tokens[@operator], 1);
            SetRhsExpression(tc.StaticTypeCheck(GetRhsExpression(), atomicType, role1, visitor));

            ItemType itemType0 = GetLhsExpression().GetItemType();
            if (itemType0 is ErrorType)
            {
                return Literal.MakeLiteral(DoubleValue.NaN, this);
            }

            IAtomicType type0 = (IAtomicType)itemType0.GetPrimitiveItemType();

            ItemType itemType1 = GetRhsExpression().GetItemType();
            if (itemType1 is ErrorType)
            {
                return Literal.MakeLiteral(DoubleValue.NaN, this);
            }

            IAtomicType type1 = (IAtomicType)itemType1.GetPrimitiveItemType();

            // If both operands are integers, use integer arithmetic and convert the result to a double
            if (th.IsSubType(type0, BuiltInAtomicType.INTEGER) &&
                    th.IsSubType(type1, BuiltInAtomicType.INTEGER) &&
                    (@operator == Token.PLUS || @operator == Token.MINUS || @operator == Token.MULT))
            {
                ArithmeticExpression arith = new ArithmeticExpression(GetLhsExpression(), @operator, GetRhsExpression());
                Expression n = SystemFunction.MakeCall("number", GetRetainedStaticContext(), arith);
                return n.TypeCheck(visitor, contextInfo);
            }

            if (calculator == null)
            {
                SetLhsExpression(CreateConversionCode(GetLhsExpression(), config, type0));
            }

            type0 = (IAtomicType)GetLhsExpression().GetItemType().GetPrimitiveItemType();

            if (calculator == null)
            {
                SetRhsExpression(CreateConversionCode(GetRhsExpression(), config, type1));
            }

            type1 = (IAtomicType)GetRhsExpression().GetItemType().GetPrimitiveItemType();

            if (GetLhsExpression() != oldOp0)
            {
                AdoptChildExpression(GetLhsExpression());
            }

            if (GetRhsExpression() != oldOp1)
            {
                AdoptChildExpression(GetRhsExpression());
            }

            if (@operator == Token.NEGATE)
            {
                if (GetRhsExpression() is Literal)
                {
                    IGroundedValue v = ((Literal)GetRhsExpression()).GroundedValue;
                    if (v is NumericValue)
                    {
                        return Literal.MakeLiteral(((NumericValue)v).Negate(), this);
                    }
                }

                NegateExpression ne = new NegateExpression(GetRhsExpression());
                ne.SetBackwardsCompatible(true);
                return ne.TypeCheck(visitor, contextInfo);
            }

            // Get a calculator to implement the arithmetic operation. If the types are not yet specifically known,
            // we allow this to return an "ANY" calculator which defers the decision. However, we only allow this if
            // at least one of the operand types is AnyAtomicType or (otherwise unspecified) numeric.
            bool mustResolve = !(type0.Equals(BuiltInAtomicType.ANY_ATOMIC) || type1.Equals(BuiltInAtomicType.ANY_ATOMIC)
                    || type0.Equals(NumericType.GetInstance()) || type1.Equals(NumericType.GetInstance()));

            calculator = AssignCalculator(type0, type1, mustResolve);

            try
            {
                if ((GetLhsExpression() is Literal) && (GetRhsExpression() is Literal))
                {
                    return Literal.MakeLiteral(
                            EvaluateItem(visitor.StaticContext.MakeEarlyEvaluationContext()).Materialize(), this);
                }
            }
            catch (XPathException)
            {
                // if early evaluation fails, suppress the error: the value might not be needed at run-time
            }

            return this;
        }

        public override void SetCalculator(Calculator calc)
        {
            this.calculator = calc;
        }

        private Calculator AssignCalculator(IAtomicType type0, IAtomicType type1, bool mustResolve)
        {
            Calculator calc = Calculator.GetCalculator(type0.Fingerprint, type1.Fingerprint,
                    ArithmeticExpression.MapOpCode(@operator), mustResolve);

            if (calc == null)
            {
                throw new XPathException("Arithmetic operator is not defined for arguments of types (" +
                        type0.Description + ", " + type1.Description + ")")
                        .WithLocation(GetLocation())
                        .WithErrorCode("XPTY0004");
            }

            return calc;
        }

        private Expression CreateConversionCode(Expression operand, Configuration config, IAtomicType type)
        {
            TypeHierarchy th = config.GetTypeHierarchy();
            if (Cardinality.AllowsMany(operand.GetCardinality()))
            {
                Expression fie = FirstItemExpression.MakeFirstItemExpression(operand);
                ExpressionTool.CopyLocationInfo(this, fie);
                operand = fie;
            }

            if (th.IsSubType(type, BuiltInAtomicType.DOUBLE) ||
                    th.IsSubType(type, BuiltInAtomicType.DATE) ||
                    th.IsSubType(type, BuiltInAtomicType.TIME) ||
                    th.IsSubType(type, BuiltInAtomicType.DATE_TIME) ||
                    th.IsSubType(type, BuiltInAtomicType.DURATION))
            {
                return operand;
            }

            if (th.IsSubType(type, BuiltInAtomicType.BOOLEAN) ||
                    th.IsSubType(type, BuiltInAtomicType.STRING) ||
                    th.IsSubType(type, BuiltInAtomicType.UNTYPED_ATOMIC) ||
                    th.IsSubType(type, BuiltInAtomicType.FLOAT) ||
                    th.IsSubType(type, BuiltInAtomicType.DECIMAL))
            {
                if (operand is Literal)
                {
                    IGroundedValue val = ((Literal)operand).GroundedValue;
                    return Literal.MakeLiteral(Number_1.Convert((AtomicValue)val, config), this);
                }
                else
                {
                    return SystemFunction.MakeCall("number", GetRetainedStaticContext(), operand);
                }
            }

            // If we can't determine the primitive type at compile time, we generate a run-time typeswitch
            LetExpression let = new LetExpression();
            let.SetRequiredType(SequenceType.OPTIONAL_ATOMIC);
            let.SetVariableQName(new StructuredQName("nn", NamespaceUri.SAXON, "nn" + let.GetHashCode()));
            let.Sequence = operand;

            LocalVariableReference var = new LocalVariableReference(let);
            Expression isDouble = new InstanceOfExpression(var, BuiltInAtomicType.DOUBLE.ZeroOrOne());

            var = new LocalVariableReference(let);
            Expression isDecimal = new InstanceOfExpression(var, BuiltInAtomicType.DECIMAL.ZeroOrOne());

            var = new LocalVariableReference(let);
            Expression isFloat = new InstanceOfExpression(var, BuiltInAtomicType.FLOAT.ZeroOrOne());

            var = new LocalVariableReference(let);
            Expression isString = new InstanceOfExpression(var, BuiltInAtomicType.STRING.ZeroOrOne());

            var = new LocalVariableReference(let);
            Expression isUntypedAtomic = new InstanceOfExpression(var, BuiltInAtomicType.UNTYPED_ATOMIC.ZeroOrOne());

            var = new LocalVariableReference(let);
            Expression isBoolean = new InstanceOfExpression(var, BuiltInAtomicType.BOOLEAN.ZeroOrOne());

            Expression condition = new OrExpression(isDouble, isDecimal);
            condition = new OrExpression(condition, isFloat);
            condition = new OrExpression(condition, isString);
            condition = new OrExpression(condition, isUntypedAtomic);
            condition = new OrExpression(condition, isBoolean);

            var = new LocalVariableReference(let);
            Expression fn = SystemFunction.MakeCall("number", GetRetainedStaticContext(), var);

            var = new LocalVariableReference(let);
            var.SetStaticType(SequenceType.SINGLE_ATOMIC, null, 0);
            Expression action = Choose.MakeConditional(condition, fn, var);
            let.SetAction(action);
            return let;
        }

        public override ItemType GetItemType()
        {
            if (calculator == null)
            {
                return BuiltInAtomicType.ANY_ATOMIC; // type is not known statically
            }
            else
            {
                ItemType t1 = GetLhsExpression().GetItemType();
                if (!(t1 is IAtomicType))
                {
                    t1 = t1.GetAtomizedItemType();
                }

                ItemType t2 = GetRhsExpression().GetItemType();
                if (!(t2 is IAtomicType))
                {
                    t2 = t2.GetAtomizedItemType();
                }

                return calculator.GetResultType((IAtomicType)t1.GetPrimitiveItemType(),
                        (IAtomicType)t2.GetPrimitiveItemType());
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ArithmeticExpression10 a2 = new ArithmeticExpression10(GetLhsExpression().Copy(rebindings), @operator, GetRhsExpression().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, a2);
            a2.calculator = calculator;
            return a2;
        }

        protected override string Tag()
        {
            return "arith10";
        }

        protected override void ExplainExtraAttributes(ExpressionPresenter @out)
        {
            @out.EmitAttribute("calc", calculator.Code());
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            Calculator calc = calculator;
            AtomicValue v1 = (AtomicValue)GetLhsExpression().EvaluateItem(context);
            if (v1 == null)
            {
                return DoubleValue.NaN;
            }

            AtomicValue v2 = (AtomicValue)GetRhsExpression().EvaluateItem(context);
            if (v2 == null)
            {
                return DoubleValue.NaN;
            }

            if (calc == null)
            {
                // Fallback for a failure to assign the calculator earlier at compile time. It has been
                // known to happen when Simplify() is called without TypeCheck().
                calc = AssignCalculator(v1.PrimitiveType, v2.PrimitiveType, true);
            }

            return calc.Compute(v1, v2, context);
        }

        public ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            Calculator calc = calculator;
            AtomicValue v1 = (AtomicValue)arguments[0].Head();
            if (v1 == null)
            {
                return DoubleValue.NaN;
            }

            AtomicValue v2 = (AtomicValue)arguments[1].Head();
            if (v2 == null)
            {
                return DoubleValue.NaN;
            }

            if (calc == null)
            {
                calc = AssignCalculator(v1.PrimitiveType, v2.PrimitiveType, true);
            }

            return calc.Compute(v1, v2, context);
        }
    }
}
