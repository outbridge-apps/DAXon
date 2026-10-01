////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
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
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class SwitchCaseComparison : BinaryExpression, IComparisonExpression
    {
        private IAtomicComparer comparer;
        private bool knownToBeComparable = false;
        private bool allowMultiple;

        public IStringCollator StringCollator => comparer.Collator;

        public int SingletonOperator => @operator;

        public override string ExpressionName => "equivalent";
        public SwitchCaseComparison(Expression p1, int @operator, Expression p2, bool allowMultiple) : base(p1, @operator, p2)
        {
            this.allowMultiple = allowMultiple;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            IStaticContext env = visitor.StaticContext;
            string defaultCollationName = env.GetDefaultCollationName();
            Configuration config = visitor.GetConfiguration();
            IStringCollator collation = config.GetCollation(defaultCollationName);
            if (collation == null)
            {
                collation = CodepointCollator.GetInstance();
            }

            comparer = new SwitchCaseComparer(collation, config.ConversionContext);
            Expression oldOp0 = GetLhsExpression();
            Expression oldOp1 = GetRhsExpression();
            Lhs.TypeCheck(visitor, contextInfo);
            Rhs.TypeCheck(visitor, contextInfo);

            // Neither operand needs to be sorted
            SetLhsExpression(GetLhsExpression().Unordered(false, false));
            SetRhsExpression(GetRhsExpression().Unordered(false, false));
            SequenceType lhsType = SequenceType.OPTIONAL_ATOMIC;
            SequenceType rhsType = allowMultiple ? SequenceType.ATOMIC_SEQUENCE : SequenceType.OPTIONAL_ATOMIC;
            TypeChecker tc = config.GetTypeChecker(false);
            Func<RoleDiagnostic> role0 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, "eq", 0);
            SetLhsExpression(tc.StaticTypeCheck(GetLhsExpression(), lhsType, role0, visitor));
            Func<RoleDiagnostic> role1 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, "eq", 1);
            SetRhsExpression(tc.StaticTypeCheck(GetRhsExpression(), rhsType, role1, visitor));
            if (GetLhsExpression() != oldOp0)
            {
                AdoptChildExpression(GetLhsExpression());
            }

            if (GetRhsExpression() != oldOp1)
            {
                AdoptChildExpression(GetRhsExpression());
            }

            ItemType t0 = GetLhsExpression().GetItemType(); // this is always an atomic type or empty-sequence()
            ItemType t1 = GetRhsExpression().GetItemType(); // this is always an atomic type or empty-sequence()
            if (t0 is ErrorType)
            {
                t0 = BuiltInAtomicType.ANY_ATOMIC;
            }

            if (t1 is ErrorType)
            {
                t1 = BuiltInAtomicType.ANY_ATOMIC;
            }

            if (t0.GetUType().Union(t1.GetUType()).Overlaps(UType.EXTENSION))
            {
                throw new XPathException("Cannot perform comparisons involving external objects").AsTypeError().WithErrorCode("XPTY0004").WithLocation(GetLocation());
            }

            BuiltInAtomicType pt0 = (BuiltInAtomicType)t0.GetPrimitiveItemType();
            BuiltInAtomicType pt1 = (BuiltInAtomicType)t1.GetPrimitiveItemType();
            if (t0.Equals(BuiltInAtomicType.ANY_ATOMIC) || t0.Equals(BuiltInAtomicType.UNTYPED_ATOMIC) || t1.Equals(BuiltInAtomicType.ANY_ATOMIC) || t1.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
            }
            else
            {
                if (Types.Type.IsGuaranteedComparable(pt0, pt1, false))
                {
                    knownToBeComparable = true;
                }
                else if (!Types.Type.IsPossiblyComparable(pt0, pt1, visitor.StaticContext.GetXPathVersion()))
                {
                    env.IssueWarning("Cannot compare " + t0 + " to " + t1, DAXonErrorCode.SXWN9025, GetLocation()); // This is not an error in a switch statement, but it means the branch will never be chosen
                }
            }

            try
            {
                if ((GetLhsExpression() is Literal) && (GetRhsExpression() is Literal))
                {
                    IGroundedValue v = EvaluateItem(visitor.StaticContext.MakeEarlyEvaluationContext()).Materialize();
                    return Literal.MakeLiteral(v, this);
                }
            }
            catch (XPathException err)
            {
            }

            return this;
        }

        public IAtomicComparer GetAtomicComparer()
        {
            return comparer;
        }

        public bool ConvertsUntypedToOther()
        {
            return false;
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.BOOLEAN;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            SwitchCaseComparison sc = new SwitchCaseComparison(GetLhsExpression().Copy(rebindings), @operator, GetRhsExpression().Copy(rebindings), allowMultiple);
            ExpressionTool.CopyLocationInfo(this, sc);
            sc.comparer = comparer;
            sc.knownToBeComparable = knownToBeComparable;
            return sc;
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return BooleanValue.Get(EffectiveBooleanValue(context));
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            return MakeElaborator().ElaborateForBoolean().Eval(context);
        }

        protected override void ExplainExtraAttributes(ExpressionPresenter @out)
        {
            @out.EmitAttribute("cardinality", "singleton");
        }

        public override Elaborator GetElaborator()
        {
            return new EquivalenceComparisonElaborator();
        }

        private sealed class EquivalenceComparisonElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                SwitchCaseComparison expr = (SwitchCaseComparison)GetExpression();
                IItemEvaluator eval0 = expr.GetLhsExpression().MakeElaborator().ElaborateForItem();
                if (expr.allowMultiple)
                {

                    // Switch expression has been generalized in 4.0
                    IPullEvaluator eval1 = expr.GetRhsExpression().MakeElaborator().ElaborateForPull();
                    return (context) =>
                    {
                        AtomicValue v0 = (AtomicValue)eval0.Eval(context);
                        ISequenceIterator iter1 = eval1.Iterate(context);
                        IAtomicComparer comp2 = expr.comparer.ProvideContext(context);
                        if (v0 == null)
                        {
                            bool empty = iter1.Next() == null;
                            iter1.Dispose();
                            return empty;
                        }
                        else
                        {
                            AtomicValue v1;
                            while ((v1 = (AtomicValue)iter1.Next()) != null)
                            {
                                if (((expr.knownToBeComparable || Types.Type.IsGuaranteedComparable(v0.PrimitiveType, v1.PrimitiveType, false))) && comp2.ComparesEqual(v0, v1))
                                {
                                    iter1.Dispose();
                                    return true;
                                }
                            }

                            return false;
                        }
                    };
                }
                else
                {
                    IItemEvaluator eval1 = expr.GetRhsExpression().MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        AtomicValue v0 = (AtomicValue)eval0.Eval(context);
                        AtomicValue v1 = (AtomicValue)eval1.Eval(context);
                        if (v0 == null || v1 == null)
                        {
                            return (v0 == v1);
                        }

                        IAtomicComparer comp2 = expr.comparer.ProvideContext(context);
                        return ((expr.knownToBeComparable || Types.Type.IsGuaranteedComparable(v0.PrimitiveType, v1.PrimitiveType, false))) && comp2.ComparesEqual(v0, v1);
                    };
                }
            }
        }
    }
}
