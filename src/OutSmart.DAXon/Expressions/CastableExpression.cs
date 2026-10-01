////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class CastableExpression : CastingExpression
    {

        /// <summary>
        /// Optimize the expression
        /// </summary>
        public override int ImplementationMethod => EVALUATE_METHOD;

        public override string ExpressionName => "castable";
        public CastableExpression(Expression source, IAtomicType target, bool allowEmpty) : base(source, target, allowEmpty)
        {
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            SequenceType atomicType = SequenceType.ATOMIC_SEQUENCE;
            Configuration config = visitor.GetConfiguration();
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.TYPE_OP, "castable as", 0);
            TypeChecker tc = config.GetTypeChecker(false);
            Expression operand = tc.StaticTypeCheck(BaseExpression, atomicType, role, visitor);
            BaseExpression = operand;
            if (operand is Literal)
            {
                return PreEvaluate();
            }

            return this;
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        private Expression PreEvaluate()
        {
            IGroundedValue literalOperand = ((Literal)BaseExpression).GroundedValue;
            if (literalOperand is AtomicValue && converter != null)
            {
                IConversionResult result = converter.Convert((AtomicValue)literalOperand);
                return Literal.MakeLiteral(BooleanValue.Get(!(result is ValidationFailure)), this);
            }

            int length = literalOperand.GetLength();
            if (length == 0)
            {
                return Literal.MakeLiteral(BooleanValue.Get(AllowsEmpty()), this);
            }

            if (length > 1)
            {
                return Literal.MakeLiteral(BooleanValue.FALSE, this);
            }

            return this;
        }

        /// <summary>
        /// Optimize the expression
        /// </summary>
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            OptimizeChildren(visitor, contextInfo);
            if (BaseExpression is Literal)
            {
                return PreEvaluate();
            }

            return this;
        }

        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        public override bool Equals(object other)
        {
            return other is CastableExpression && BaseExpression.IsEqual(((CastableExpression)other).BaseExpression) && TargetType == ((CastableExpression)other).TargetType && AllowsEmpty() == ((CastableExpression)other).AllowsEmpty();
        }

        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode() ^ 0x5555;
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
            return StaticProperty.EXACTLY_ONE;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            CastableExpression ce = new CastableExpression(BaseExpression.Copy(rebindings), TargetType, AllowsEmpty());
            ExpressionTool.CopyLocationInfo(this, ce);
            ce.SetRetainedStaticContext(GetRetainedStaticContext());
            ce.converter = converter;
            return ce;
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return BooleanValue.Get(EffectiveBooleanValue(context));
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            return MakeElaborator().ElaborateForBoolean().Eval(context);
        }

        private bool IsCastable(AtomicValue value, IAtomicType targetType, IXPathContext context)
        {
            Converter converter = this.converter;
            if (converter == null)
            {
                converter = context.GetConfiguration().GetConversionRules().GetConverter(value.PrimitiveType, targetType);
                if (converter == null)
                {
                    return false;
                }

                if (converter.IsAlwaysSuccessful())
                {
                    return true;
                }

                if (TargetType.IsNamespaceSensitive())
                {
                    converter = converter.SetNamespaceResolver(GetRetainedStaticContext());
                }
            }

            return !(converter.Convert(value) is ValidationFailure);
        }

        public override string ToString()
        {
            return BaseExpression.ToString() + " castable as " + TargetType.EQName;
        }

        public override void Export(ExpressionPresenter @out)
        {
            Export(@out, "castable");
        }

        public override Elaborator GetElaborator()
        {
            return new CastableExpressionElaborator();
        }

        private sealed class CastableExpressionElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                CastableExpression expr = (CastableExpression)GetExpression();
                IPullEvaluator argPull = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) =>
                {

                    // This method does its own atomization so that it can distinguish between atomization
                    // failures and casting failures
                    int count = 0;
                    ISequenceIterator iter = argPull.Iterate(context);
                    for (IItem item; (item = iter.Next()) != null;)
                    {
                        if (item is NodeInfo)
                        {
                            IAtomicSequence atomizedValue = item.Atomize();
                            int length = SequenceTool.GetLength(atomizedValue);
                            count += length;
                            if (count > 1)
                            {
                                return false;
                            }

                            if (length != 0)
                            {
                                AtomicValue av = atomizedValue.Head();
                                if (!expr.IsCastable(av, expr.TargetType, context))
                                {
                                    return false;
                                }
                            }
                        }
                        else if (item is AtomicValue)
                        {
                            AtomicValue av = (AtomicValue)item;
                            count++;
                            if (count > 1)
                            {
                                return false;
                            }

                            if (!expr.IsCastable(av, expr.TargetType, context))
                            {
                                return false;
                            }
                        }
                        else if (item is ArrayItem)
                        {
                            return false;
                        }
                        else
                        {
                            throw new XPathException("Input to cast cannot be atomized", "XPTY0004");
                        }
                    }

                    return count != 0 || expr.AllowsEmpty();
                };
            }
        }
    }
}
