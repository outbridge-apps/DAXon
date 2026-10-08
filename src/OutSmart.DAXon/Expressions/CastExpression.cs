////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class CastExpression : CastingExpression, ICallable
    {

        public override IntegerValue[] IntegerBounds
        {
            get
            {
                if (converter == Converter.BooleanToInteger.INSTANCE)
                {
                    return new IntegerValue[]
                    {
                    Int64Value.ZERO,
                    Int64Value.PLUS_ONE
                    };
                }
                else
                {
                    return null;
                }
            }
        }

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override string ExpressionName => "cast";
        public CastExpression(Expression source, IAtomicType target, bool allowEmpty) : base(source, target, allowEmpty)
        {
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            SequenceType atomicType = SequenceType.MakeSequenceType(BuiltInAtomicType.ANY_ATOMIC, GetCardinality());
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.TYPE_OP, "cast as", 0);
            ItemType sourceItemType;
            TypeChecker tc = config.GetTypeChecker(false);
            Expression operand = tc.StaticTypeCheck(BaseExpression, atomicType, role, visitor);
            BaseExpression = operand;
            sourceItemType = operand.GetItemType();
            if (sourceItemType is ErrorType)
            {
                if (AllowsEmpty())
                {
                    return Literal.MakeEmptySequence();
                }
                else
                {
                    throw new XPathException("Cast does not allow an empty sequence as input").WithErrorCode("XPTY0004").WithLocation(GetLocation()).AsTypeError();
                }
            }

            IPlainType sourceType = (IPlainType)sourceItemType;
            Affinity r = th.Relationship(sourceType, TargetType);
            if (r == Affinity.SAME_TYPE)
            {
                return operand;
            }
            else if (r == Affinity.SUBSUMED_BY)
            {

                // It's generally true that any expression defined to return an X is allowed to return a subtype of X.
                // However, people seem to get upset if we treat the cast as a no-op.
                converter = new UpCastingConverter(TargetType);
            }
            else
            {
                ConversionRules rules = visitor.GetConfiguration().GetConversionRules();
                if (sourceType.IsAtomicType() && sourceType != BuiltInAtomicType.ANY_ATOMIC)
                {

                    converter = rules.GetConverter((IAtomicType)sourceType, TargetType);
                    if (converter == null)
                    {
                        throw new XPathException("Casting from " + sourceType + " to " + TargetType + " can never succeed").WithErrorCode("XPTY0004").WithLocation(GetLocation()).AsTypeError();
                    }
                    else
                    {
                        if (TargetType.IsNamespaceSensitive())
                        {
                            converter = converter.SetNamespaceResolver(GetRetainedStaticContext());
                        }
                    }
                }
            }

            if (operand is Literal)
            {
                return PreEvaluate();
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            Expression e2 = base.Optimize(visitor, contextInfo);
            if (e2 != this)
            {
                return e2;
            }


            // Eliminate pointless casting between untypedAtomic and string
            Expression operand = BaseExpression;
            if (TargetType == BuiltInAtomicType.UNTYPED_ATOMIC)
            {
                if (operand.IsCallOn(typeof(String_1)))
                {
                    Expression e = ((SystemFunctionCall)operand).GetArg(0);
                    if (e.GetItemType() is IAtomicType && e.GetCardinality() == StaticProperty.EXACTLY_ONE)
                    {
                        return new CastExpression(e, BuiltInAtomicType.UNTYPED_ATOMIC, AllowsEmpty());
                    }
                }
                else if (operand is CastExpression)
                {
                    if (((CastExpression)operand).TargetType == BuiltInAtomicType.UNTYPED_ATOMIC)
                    {
                        return operand;
                    }
                    else if (((CastExpression)operand).TargetType == BuiltInAtomicType.STRING)
                    {
                        ((CastExpression)operand).TargetType = BuiltInAtomicType.UNTYPED_ATOMIC;
                        return operand;
                    }
                }
                else if (operand is AtomicSequenceConverter)
                {
                    if (operand.GetItemType() == BuiltInAtomicType.UNTYPED_ATOMIC)
                    {
                        return operand;
                    }
                    else if (operand.GetItemType() == BuiltInAtomicType.STRING)
                    {
                        AtomicSequenceConverter old = (AtomicSequenceConverter)operand;
                        AtomicSequenceConverter asc = new AtomicSequenceConverter(old.BaseExpression, BuiltInAtomicType.UNTYPED_ATOMIC);
                        return asc.TypeCheck(visitor, contextInfo).Optimize(visitor, contextInfo);
                    }
                }
            }


            // avoid converting anything to a string and then back again
            if (operand.IsCallOn(typeof(String_1)))
            {
                Expression e = ((SystemFunctionCall)operand).GetArg(0);
                ItemType et = e.GetItemType();
                if (et is IAtomicType && e.GetCardinality() == StaticProperty.EXACTLY_ONE && th.IsSubType(et, TargetType))
                {
                    return e;
                }
            }


            // avoid converting anything to untypedAtomic and then back again
            if (operand is CastExpression)
            {
                ItemType it = ((CastExpression)operand).TargetType;
                if (th.IsSubType(it, BuiltInAtomicType.STRING) || th.IsSubType(it, BuiltInAtomicType.UNTYPED_ATOMIC))
                {
                    Expression e = ((CastExpression)operand).BaseExpression;
                    ItemType et = e.GetItemType();
                    if (et is IAtomicType && e.GetCardinality() == StaticProperty.EXACTLY_ONE && th.IsSubType(et, TargetType))
                    {
                        return e;
                    }
                }
            }

            if (operand is AtomicSequenceConverter)
            {
                ItemType it = operand.GetItemType();
                if (th.IsSubType(it, BuiltInAtomicType.STRING) || th.IsSubType(it, BuiltInAtomicType.UNTYPED_ATOMIC))
                {
                    Expression e = ((AtomicSequenceConverter)operand).BaseExpression;
                    ItemType et = e.GetItemType();
                    if (et is IAtomicType && e.GetCardinality() == StaticProperty.EXACTLY_ONE && th.IsSubType(et, TargetType))
                    {
                        return e;
                    }
                }
            }


            // if the operand can't be empty, then set allowEmpty to false to provide more information for analysis
            if (!Cardinality.AllowsZero(operand.GetCardinality()))
            {
                SetAllowEmpty(false);
                ResetLocalStaticProperties();
            }

            if (operand is Literal)
            {
                return PreEvaluate();
            }

            return this;
        }

        protected Expression PreEvaluate()
        {
            IGroundedValue literalOperand = ((Literal)BaseExpression).GroundedValue;
            if (literalOperand is AtomicValue && converter != null)
            {
                IConversionResult result = converter is StringConverter __scFix2 ? __scFix2.ConvertString(((AtomicValue)literalOperand).UnicodeStringValue) : converter.Convert((AtomicValue)literalOperand);
                if (result is ValidationFailure)
                {
                    ValidationFailure err = (ValidationFailure)result;
                    string code = err.GetErrorCode();
                    if (code == null)
                    {
                        code = "FORG0001";
                    }

                    throw new XPathException(err.GetMessage(), code, this.GetLocation());
                }
                else
                {
                    return Literal.MakeLiteral((AtomicValue)result, this);
                }
            }

            if (literalOperand.GetLength() == 0)
            {
                if (AllowsEmpty())
                {
                    return BaseExpression;
                }
                else
                {
                    XPathException err = new XPathException("Cast can never succeed: the operand must not be an empty sequence", "XPTY0004", this.GetLocation());
                    err.SetIsTypeError(true);
                    throw err;
                }
            }

            return this;
        }

        /// <summary>
        /// Get the static cardinality of the expression
        /// </summary>
        protected override int ComputeCardinality()
        {
            return AllowsEmpty() && Cardinality.AllowsZero(BaseExpression.GetCardinality()) ? StaticProperty.ALLOWS_ZERO_OR_ONE : StaticProperty.EXACTLY_ONE;
        }

        public override ItemType GetItemType()
        {
            return TargetType;
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return TargetType.GetUType();
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            if (TargetType == BuiltInAtomicType.UNTYPED_ATOMIC)
            {
                p = p & ~StaticProperty.NOT_UNTYPED_ATOMIC;
            }

            return p;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            CastExpression c2 = new CastExpression(BaseExpression.Copy(rebindings), TargetType, AllowsEmpty());
            ExpressionTool.CopyLocationInfo(this, c2);
            c2.converter = converter;
            c2.SetRetainedStaticContext(GetRetainedStaticContext());
            c2.SetOperandIsStringLiteral(IsOperandIsStringLiteral());
            return c2;
        }

        public ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            AtomicValue result = DoCast((AtomicValue)arguments[0].Head(), context);
            return SequenceTool.ItemOrEmpty(result);
        }

        public AtomicValue DoCast(AtomicValue value, IXPathContext context)
        {
            if (value == null)
            {
                if (AllowsEmpty())
                {
                    return null;
                }
                else
                {
                    throw new XPathException("Cast does not allow an empty sequence").WithXPathContext(context).WithLocation(GetLocation()).WithErrorCode("XPTY0004");
                }
            }

            Converter converter = this.converter;
            if (converter == null)
            {
                ConversionRules rules = context.GetConfiguration().GetConversionRules();
                converter = rules.GetConverter(value.PrimitiveType, TargetType);
                if (converter == null)
                {
                    throw new XPathException("Casting from " + value.PrimitiveType + " to " + TargetType + " is not permitted").WithXPathContext(context).WithLocation(GetLocation()).WithErrorCode("XPTY0004");
                }

                if (TargetType.IsNamespaceSensitive())
                {
                    converter = converter.SetNamespaceResolver(GetRetainedStaticContext());
                }
            }

            IConversionResult result = converter is StringConverter __scFix ? __scFix.ConvertString(value.UnicodeStringValue) : converter.Convert(value);
            if (result is ValidationFailure)
            {
                ValidationFailure err = (ValidationFailure)result;
                throw err.MakeException().MaybeWithErrorCode("FORG0001").MaybeWithLocation(GetLocation());
            }

            return (AtomicValue)result;
        }

        /// <summary>
        /// Evaluate the expression
        /// </summary>
        public override IItem EvaluateItem(IXPathContext context)
        {
            return (AtomicValue)MakeElaborator().ElaborateForItem().Eval(context);
        }

        public override bool Equals(object other)
        {
            return other is CastExpression && BaseExpression.IsEqual(((CastExpression)other).BaseExpression) && TargetType == ((CastExpression)other).TargetType && AllowsEmpty() == ((CastExpression)other).AllowsEmpty();
        }

        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode() ^ TargetType.GetHashCode();
        }

        public override string ToString()
        {
            return TargetType.EQName + "(" + BaseExpression.ToString() + ")";
        }

        public override string ToShortString()
        {
            return TargetType.DisplayName + "(" + BaseExpression.ToShortString() + ")";
        }

        public override void Export(ExpressionPresenter @out)
        {
            Export(@out, "cast");
        }

        public override Elaborator GetElaborator()
        {
            return new CastExprElaborator();
        }

        /// <summary>
        /// Elaborator for {@code cast as} expression, or the equivalent constructor function call
        /// </summary>
        internal sealed class CastExprElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                CastExpression exp = (CastExpression)GetExpression();
                Expression arg = exp.BaseExpression;
                IItemEvaluator argEval = arg.MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    AtomicValue value = (AtomicValue)argEval.Eval(context);
                    return exp.DoCast(value, context);
                };
            }
        }
    }
}
