////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class CompareToStringConstant : CompareToConstant
    {
        private readonly UnicodeString comparand;

        public UnicodeString Comparand => comparand;

        public override string ExpressionName => "compareToString";

        public override IStringCollator StringCollator => CodepointCollator.GetInstance();
        public CompareToStringConstant(Expression operand, int @operator, UnicodeString comparand) : base(operand)
        {
            this.@operator = @operator;
            this.comparand = comparand;
        }

        public override Expression GetRhsExpression()
        {
            return new StringLiteral(comparand);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            CompareToStringConstant c2 = new CompareToStringConstant(GetLhsExpression().Copy(rebindings), @operator, comparand);
            ExpressionTool.CopyLocationInfo(this, c2);
            return c2;
        }

        public override bool Equals(object other)
        {
            return other is CompareToStringConstant && ((CompareToStringConstant)other).GetLhsExpression().IsEqual(GetLhsExpression()) && ((CompareToStringConstant)other).comparand.Equals(comparand) && ((CompareToStringConstant)other).@operator == @operator;
        }

        protected override int ComputeHashCode()
        {
            int h = 0x484b12a0;
            return h + GetLhsExpression().GetHashCode() ^ comparand.GetHashCode();
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            UnicodeString s = GetLhsExpression().EvaluateItem(context).UnicodeStringValue;
            int c = CodepointCollator.GetInstance().CompareStrings(s, comparand);
            return InterpretComparisonResult(@operator, c);
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("compareToString", this);
            destination.EmitAttribute("op", Token.tokens[@operator]);
            destination.EmitAttribute("val", Comparand.ToString());
            GetLhsExpression().Export(destination);
            destination.EndElement();
        }

        public override string ToString()
        {
            return ExpressionTool.Parenthesize(GetLhsExpression()) + " " + Token.tokens[@operator] + " " + comparand.ToString();
        }

        public override string ToShortString()
        {
            return GetLhsExpression().ToShortString() + " " + Token.tokens[@operator] + " \"" + comparand + "\"";
        }

        public override IAtomicComparer GetAtomicComparer()
        {
            return CodepointCollatingComparer.GetInstance();
        }

        public override Elaborator GetElaborator()
        {
            return new CompareToStringConstantElaborator();
        }

        /// <summary>
        /// Elaborator for a "compare to string constant" expression
        /// </summary>
        internal sealed class CompareToStringConstantElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                CompareToStringConstant expression = (CompareToStringConstant)GetExpression();
                Expression arg = expression.BaseExpression;
                IUnicodeStringEvaluator argEval = arg.MakeElaborator().ElaborateForUnicodeString(false);
                bool nullable = Cardinality.AllowsZero(expression.GetCardinality());
                int @operator = expression.ComparisonOperator;
                UnicodeString comparand = expression.Comparand;
                return (context) =>
                {
                    UnicodeString value = argEval.Eval(context);
                    if (nullable && value == null)
                    {
                        return false;
                    }

                    int c = value.CompareTo(comparand);
                    return InterpretComparisonResult(@operator, c);
                };
            }
        }
    }
}
