////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class IntegerRangeTest : Expression
    {
        private readonly Operand valueOp;
        private readonly Operand minOp;
        private readonly Operand maxOp;

        public Expression Value
        {
            get => valueOp.GetChildExpression(); set
            {
                valueOp.SetChildExpression(value);
            }
        }

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override string ExpressionName => "intRangeTest";
        public IntegerRangeTest(Expression value, Expression min, Expression max)
        {
            valueOp = new Operand(this, value, OperandRole.ATOMIC_SEQUENCE);
            minOp = new Operand(this, min, OperandRole.SINGLE_ATOMIC);
            maxOp = new Operand(this, max, OperandRole.SINGLE_ATOMIC);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(valueOp, minOp, maxOp);
        }

        public Expression GetMin()
        {
            return minOp.GetChildExpression();
        }

        public Expression GetMax()
        {
            return maxOp.GetChildExpression();
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {

            // Already done, we only get one of these expressions after the operands have been analyzed
            return this;
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            if (Literal.IsEmptySequence(GetMin()) || Literal.IsEmptySequence(GetMax()) || Literal.IsEmptySequence(Value))
            {
                return new Literal(BooleanValue.FALSE);
            }

            if (GetMin() is Literal && GetMax() is Literal && Value is Literal)
            {
                BooleanValue result = (BooleanValue)EvaluateItem(visitor.MakeDynamicContext());
                return new Literal(result);
            }

            return this;
        }

        /// <summary>
        /// Get the data type of the items returned
        /// </summary>
        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.BOOLEAN;
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            IntegerRangeTest exp = new IntegerRangeTest(Value.Copy(rebindings), GetMin().Copy(rebindings), GetMax().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override bool Equals(object other)
        {
            return other is IntegerRangeTest && ((IntegerRangeTest)other).Value.IsEqual(Value) && ((IntegerRangeTest)other).GetMin().IsEqual(GetMin()) && ((IntegerRangeTest)other).GetMax().IsEqual(GetMax());
        }

        /// <summary>
        /// Hashcode supporting equals()
        /// </summary>
        protected override int ComputeHashCode()
        {
            int h = Value.GetHashCode() + 77;
            h ^= GetMin().GetHashCode() ^ GetMax().GetHashCode();
            return h;
        }

        public override IItem EvaluateItem(IXPathContext c)
        {
            return BooleanValue.Get(EffectiveBooleanValue(c));
        }

        public override bool EffectiveBooleanValue(IXPathContext c)
        {
            try
            {
                return Eval((context) => GetMin().EvaluateItem(context), (context) => GetMax().EvaluateItem(context), (context) => Value.Iterate(context), c);
            }
            catch (XPathException e)
            {
                throw e.MaybeWithLocation(GetLocation()).MaybeWithContext(c);
            }
        }

        public static bool Eval(IItemEvaluator minEval, IItemEvaluator maxEval, IPullEvaluator valueEval, IXPathContext c)
        {
            IntegerValue minVal = null;
            IntegerValue maxVal = null;
            StringConverter toDouble = null;
            ISequenceIterator iter = valueEval.Iterate(c);
            AtomicValue atom;
            while ((atom = (AtomicValue)iter.Next()) != null)
            {
                if (minVal == null)
                {
                    minVal = (IntegerValue)minEval.Eval(c);
                    if (minVal == null)
                    {
                        return false;
                    }

                    maxVal = (IntegerValue)maxEval.Eval(c);
                    if (maxVal == null || maxVal.CompareTo(minVal) < 0)
                    {

                        // bug 3666
                        return false;
                    }
                }

                NumericValue v;
                if (atom.IsUntypedAtomic())
                {
                    if (toDouble == null)
                    {
                        toDouble = BuiltInAtomicType.DOUBLE.GetStringConverter(c.GetConfiguration().GetConversionRules());
                    }

                    IConversionResult result = toDouble.ConvertString(atom.UnicodeStringValue);
                    if (result is ValidationFailure)
                    {
                        throw new XPathException("Failed to convert untypedAtomic value {" + atom.UnicodeStringValue + "}  to xs:integer", "FORG0001");
                    }
                    else
                    {
                        v = (DoubleValue)result.AsAtomic();
                    }
                }
                else if (atom is NumericValue)
                {
                    v = (NumericValue)atom;
                }
                else
                {
                    XPathException e = new XPathException("Cannot compare value of type " + atom.GetUType() + " to xs:integer", "XPTY0004");
                    e.SetIsTypeError(true);
                    throw e;
                }

                if (v.IsWholeNumber() && v.CompareTo(minVal) >= 0 && v.CompareTo(maxVal) <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("intRangeTest", this);
            Value.Export(destination);
            GetMin().Export(destination);
            GetMax().Export(destination);
            destination.EndElement();
        }

        public override string ToString()
        {
            return ExpressionTool.Parenthesize(Value) + " = (" + ExpressionTool.Parenthesize(GetMin()) + " to " + ExpressionTool.Parenthesize(GetMax()) + ")";
        }

        public override Elaborator GetElaborator()
        {
            return new IntegerRangeTestElaborator();
        }

        internal sealed class IntegerRangeTestElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                IntegerRangeTest expr = (IntegerRangeTest)GetExpression();
                IItemEvaluator iv1 = expr.GetMin().MakeElaborator().ElaborateForItem();
                IItemEvaluator iv2 = expr.GetMax().MakeElaborator().ElaborateForItem();
                IPullEvaluator iv3 = expr.Value.MakeElaborator().ElaborateForPull();
                return (context) => IntegerRangeTest.Eval(iv1, iv2, iv3, context);
            }
        }
    }
}
