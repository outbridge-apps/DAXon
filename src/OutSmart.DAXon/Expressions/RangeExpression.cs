////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.Numerics;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class RangeExpression : Expression
    {
        private readonly Operand start;
        private readonly Operand end;

        public Expression StartExpression => start.GetChildExpression();

        public Expression EndExpression => end.GetChildExpression();

        public override IntegerValue[] IntegerBounds
        {
            get
            {
                IntegerValue[] start = StartExpression.IntegerBounds;
                IntegerValue[] end = EndExpression.IntegerBounds;
                if (start == null || end == null)
                {
                    return null;
                }
                else
                {

                    // range is from the smallest possible start value to the largest possible end value
                    return new IntegerValue[]
                    {
                    start[0],
                    end[1]
                    };
                }
            }
        }

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string ExpressionName => "range";
        public RangeExpression(Expression start, Expression end)
        {
            this.start = new Operand(this, start, OperandRole.SINGLE_ATOMIC);
            this.end = new Operand(this, end, OperandRole.SINGLE_ATOMIC);
            AdoptChildExpression(start);
            AdoptChildExpression(end);
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            start.TypeCheck(visitor, contextInfo);
            end.TypeCheck(visitor, contextInfo);
            bool backCompat = visitor.StaticContext.IsInBackwardsCompatibleMode();
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(backCompat);
            Func<RoleDiagnostic> role0 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, "to", 0);
            start.SetChildExpression(tc.StaticTypeCheck(StartExpression, SequenceType.OPTIONAL_INTEGER, role0, visitor));
            Func<RoleDiagnostic> role2 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, "to", 1);
            end.SetChildExpression(tc.StaticTypeCheck(EndExpression, SequenceType.OPTIONAL_INTEGER, role2, visitor));
            return MakeConstantRange();
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            start.Optimize(visitor, contextInfo);
            end.Optimize(visitor, contextInfo);
            return MakeConstantRange();
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(start, end);
        }

        private Expression MakeConstantRange()
        {
            if (StartExpression is Literal && EndExpression is Literal)
            {
                Expression result;
                IGroundedValue v0 = ((Literal)StartExpression).GroundedValue;
                IGroundedValue v2 = ((Literal)EndExpression).GroundedValue;
                if (v0.GetLength() == 0 || v2.GetLength() == 0)
                {
                    result = Literal.MakeEmptySequence();
                }
                else if (v0 is Int64Value && v2 is Int64Value)
                {
                    long i0 = ((Int64Value)v0).LongValue();
                    long i2 = ((Int64Value)v2).LongValue();
                    if (i0 > i2)
                    {
                        result = Literal.MakeEmptySequence();
                    }
                    else
                    {
                        if (IntegerRange.CountExceedsLimit(i0, 1, i2))
                        {
                            throw new XPathException("Maximum length of sequence in Saxon is " + int.MaxValue, "XPDY0130");
                        }

                        result = Literal.MakeLiteral(new IntegerRange(i0, 1, i2), this);
                    }
                }
                else
                {
                    BigInteger i0 = ((IntegerValue)v0).AsBigInteger();
                    BigInteger i2 = ((IntegerValue)v2).AsBigInteger();
                    if (i0.Equals(BigInteger.Zero) || i0.CompareTo(i2) > 0)
                    {
                        result = Literal.MakeEmptySequence();
                    }
                    else if (i0.Equals(i2))
                    {
                        result = Literal.MakeLiteral(Int64Value.MakeIntegerValue(i0), this);
                    }
                    else
                    {

                        //                    if (Math.abs((i2 - i0) / i1) > int.MaxValue) {
                        //                        throw new XPathException("Maximum length of sequence in Saxon is " + int.MaxValue, "XPDY0130");
                        //                    }
                        return this;
                    }
                }

                ExpressionTool.CopyLocationInfo(this, result);
                return result;
            }

            return this;
        }

        /// <summary>
        /// Get the data type of the items returned
        /// </summary>
        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.INTEGER;
        }

        /// <summary>
        /// Get the data type of the items returned
        /// </summary>
        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.DECIMAL;
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            RangeExpression exp = new RangeExpression(StartExpression.Copy(rebindings), EndExpression.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            return p | StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        public override bool Equals(object other)
        {
            if (other is RangeExpression && HasCompatibleStaticContext((Expression)other))
            {
                RangeExpression b = (RangeExpression)other;
                Expression start1 = StartExpression;
                Expression end1 = EndExpression;
                Expression start2 = b.StartExpression;
                Expression end2 = b.EndExpression;
                return start1.Equals(start2) && end1.Equals(end2);
            }

            return false;
        }

        protected override int ComputeHashCode()
        {
            return StartExpression.GetHashCode() ^ (EndExpression.GetHashCode() << 7);
        }

        public override string ToString()
        {
            return StartExpression.ToString() + " to " + EndExpression.ToString();
        }

        public override string ToShortString()
        {
            return StartExpression.ToShortString() + " to " + EndExpression.ToShortString();
        }

        public override void Export(ExpressionPresenter @out)
        {

            // TODO: export "by" expression
            @out.StartElement("to", this);
            StartExpression.Export(@out);
            EndExpression.Export(@out);
            @out.EndElement();
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            IntegerValue av1 = (IntegerValue)StartExpression.EvaluateItem(context);
            IntegerValue av3 = (IntegerValue)EndExpression.EvaluateItem(context);
            return AscendingRangeIterator.MakeRangeIterator(av1, Int64Value.PLUS_ONE, av3);
        }

        public override Elaborator GetElaborator()
        {
            return new RangeElaborator();
        }

        internal sealed class RangeElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                RangeExpression expr = (RangeExpression)GetExpression();
                IItemEvaluator iv1 = expr.StartExpression.MakeElaborator().ElaborateForItem();
                IItemEvaluator iv3 = expr.EndExpression.MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    IntegerValue av1 = (IntegerValue)iv1.Eval(context);
                    IntegerValue av3 = (IntegerValue)iv3.Eval(context);
                    return AscendingRangeIterator.MakeRangeIterator(av1, Int64Value.PLUS_ONE, av3);
                };
            }
        }
    }
}