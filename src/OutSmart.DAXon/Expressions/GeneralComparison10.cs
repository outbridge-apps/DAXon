////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Expressions.Compatibility
{
    /// <summary>
    /// GeneralComparison10: the operators =, !=, &lt;, &gt; etc under XPath 1.0
    /// backwards-compatibility semantics.
    /// </summary>
    internal sealed class GeneralComparison10 : BinaryExpression, ICallable
    {
        protected int singletonOperator;
        protected IAtomicComparer comparer;
        private bool atomize0 = true;
        private bool atomize1 = true;
        private bool maybeBoolean0 = true;
        private bool maybeBoolean1 = true;

        public GeneralComparison10(Expression p0, int op, Expression p1) : base(p0, op, p1)
        {
            singletonOperator = GeneralComparison.GetCorrespondingSingletonOperator(op);
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Lhs.TypeCheck(visitor, contextInfo);
            Rhs.TypeCheck(visitor, contextInfo);

            IStaticContext env = visitor.StaticContext;
            IStringCollator comp = visitor.GetConfiguration().GetCollation(env.GetDefaultCollationName());
            if (comp == null)
            {
                comp = CodepointCollator.GetInstance();
            }

            IXPathContext context = env.MakeEarlyEvaluationContext();
            comparer = new GenericAtomicComparer(comp, context);

            // evaluate the expression now if both arguments are constant
            if ((GetLhsExpression() is Literal) && (GetRhsExpression() is Literal))
            {
                return Literal.MakeLiteral((IGroundedValue)EvaluateItem(context), this);
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            IStaticContext env = visitor.StaticContext;

            Lhs.Optimize(visitor, contextInfo);
            Rhs.Optimize(visitor, contextInfo);

            // Neither operand needs to be sorted
            SetLhsExpression(GetLhsExpression().Unordered(false, false));
            SetRhsExpression(GetRhsExpression().Unordered(false, false));

            // evaluate the expression now if both arguments are constant
            if ((GetLhsExpression() is Literal) && (GetRhsExpression() is Literal))
            {
                return Literal.MakeLiteral((IGroundedValue)EvaluateItem(env.MakeEarlyEvaluationContext()), this);
            }

            TypeHierarchy th = config.GetTypeHierarchy();
            ItemType type0 = GetLhsExpression().GetItemType();
            ItemType type1 = GetRhsExpression().GetItemType();

            if (type0.IsPlainType())
            {
                atomize0 = false;
            }

            if (type1.IsPlainType())
            {
                atomize1 = false;
            }

            if (th.Relationship(type0, BuiltInAtomicType.BOOLEAN) == Affinity.DISJOINT)
            {
                maybeBoolean0 = false;
            }

            if (th.Relationship(type1, BuiltInAtomicType.BOOLEAN) == Affinity.DISJOINT)
            {
                maybeBoolean1 = false;
            }

            if (!maybeBoolean0 && !maybeBoolean1)
            {
                // First atomize the operands where necessary. We didn't do this earlier because of the
                // special 1.0 (node-set=boolean) semantics, but if we don't have a boolean we can do it now.
                if (!(type0 is IAtomicType))
                {
                    SetLhsExpression(Atomizer.MakeAtomizer(GetLhsExpression(), null).Simplify());
                    type0 = GetLhsExpression().GetItemType();
                }

                if (!(type1 is IAtomicType))
                {
                    SetRhsExpression(Atomizer.MakeAtomizer(GetRhsExpression(), null).Simplify());
                    type1 = GetRhsExpression().GetItemType();
                }

                // Now consider numeric operands
                Affinity n0 = th.Relationship(type0, NumericType.GetInstance());
                Affinity n1 = th.Relationship(type1, NumericType.GetInstance());
                bool maybeNumeric0 = n0 != Affinity.DISJOINT;
                bool maybeNumeric1 = n1 != Affinity.DISJOINT;
                bool numeric0 = n0 == Affinity.SUBSUMED_BY || n0 == Affinity.SAME_TYPE;
                bool numeric1 = n1 == Affinity.SUBSUMED_BY || n1 == Affinity.SAME_TYPE;

                // Use the 2.0 path if we don't have to deal with the possibility of boolean values,
                // or the complications of converting values to numbers
                if (@operator == Token.EQUALS || @operator == Token.NE)
                {
                    if ((!maybeNumeric0 && !maybeNumeric1) || (numeric0 && numeric1))
                    {
                        GeneralComparison gc = new GeneralComparison20(GetLhsExpression(), @operator, GetRhsExpression());
                        gc.SetRetainedStaticContext(GetRetainedStaticContext());
                        gc.SetAtomicComparer(comparer);
                        Expression binExp = visitor.ObtainOptimizer()
                                .OptimizeGeneralComparison(visitor, gc, false, contextInfo);
                        ExpressionTool.CopyLocationInfo(this, binExp);
                        return binExp.TypeCheck(visitor, contextInfo).Optimize(visitor, contextInfo);
                    }
                }
                else if (numeric0 && numeric1)
                {
                    GeneralComparison gc = new GeneralComparison20(GetLhsExpression(), @operator, GetRhsExpression());
                    gc.SetRetainedStaticContext(GetRetainedStaticContext());
                    Expression binExp = visitor.ObtainOptimizer()
                            .OptimizeGeneralComparison(visitor, gc, false, contextInfo);
                    ExpressionTool.CopyLocationInfo(this, binExp);
                    return binExp.TypeCheck(visitor, contextInfo).Optimize(visitor, contextInfo);
                }
            }

            return this;
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return BooleanValue.Get(EffectiveBooleanValue(context));
        }

        public ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return BooleanValue.Get(EffectiveBooleanValue(arguments[0].Iterate(), arguments[1].Iterate(), context));
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            return EffectiveBooleanValue(GetLhsExpression().Iterate(context), GetRhsExpression().Iterate(context), context);
        }

        public override Elaborator GetElaborator()
        {
            return new GeneralComparison10Elaborator();
        }

        // The operands elaborated once. Through the fallback they were evaluated interpretively, and a
        // function-call operand rebuilt its closures on every comparison (XSLT 1.0 stylesheets).
        private sealed class GeneralComparison10Elaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                GeneralComparison10 expr = (GeneralComparison10)GetExpression();
                IPullEvaluator lhs = expr.GetLhsExpression().MakeElaborator().ElaborateForPull();
                IPullEvaluator rhs = expr.GetRhsExpression().MakeElaborator().ElaborateForPull();
                return (context) => expr.EffectiveBooleanValue(lhs(context), rhs(context), context);
            }
        }

        private bool EffectiveBooleanValue(ISequenceIterator iter0, ISequenceIterator iter1, IXPathContext context)
        {
            // If the first operand is a singleton boolean,
            // compare it with the effective boolean value of the other operand
            if (maybeBoolean0)
            {
                IItem i01 = iter0.Next();
                IItem i02 = i01 == null ? null : iter0.Next();
                if (i01 is BooleanValue && i02 == null)
                {
                    iter0.Dispose();
                    bool b = ExpressionTool.EffectiveBooleanValue(iter1);
                    return Compare((BooleanValue)i01, singletonOperator, BooleanValue.Get(b), comparer, context);
                }

                if (i01 == null && !maybeBoolean1)
                {
                    iter0.Dispose();
                    return false;
                }

                // Reconstitute the original iterator
                if (i02 != null)
                {
                    iter0 = new PrependSequenceIterator(i02, iter0);
                }

                if (i01 != null)
                {
                    iter0 = new PrependSequenceIterator(i01, iter0);
                }
            }

            // If the second operand is a singleton boolean,
            // compare it with the effective boolean value of the other operand
            if (maybeBoolean1)
            {
                IItem i11 = iter1.Next();
                IItem i12 = i11 == null ? null : iter1.Next();
                if (i11 is BooleanValue && i12 == null)
                {
                    iter1.Dispose();
                    bool b = ExpressionTool.EffectiveBooleanValue(iter0);
                    return Compare(BooleanValue.Get(b), singletonOperator, (BooleanValue)i11, comparer, context);
                }

                if (i11 == null && !maybeBoolean0)
                {
                    iter1.Dispose();
                    return false;
                }

                // Reconstitute the original iterator
                if (i12 != null)
                {
                    iter1 = new PrependSequenceIterator(i12, iter1);
                }

                if (i11 != null)
                {
                    iter1 = new PrependSequenceIterator(i11, iter1);
                }
            }

            // Atomize both operands where necessary
            if (atomize0)
            {
                iter0 = Atomizer.GetAtomizingIterator(iter0, false);
            }

            if (atomize1)
            {
                iter1 = Atomizer.GetAtomizingIterator(iter1, false);
            }

            // If either iterator is known to be empty, quit now
            if (iter0 is EmptyIterator || iter1 is EmptyIterator)
            {
                return false;
            }

            // If the operator is one of <, >, <=, >=, then convert both operands to sequences of xs:double
            // using the number() function
            if (@operator == Token.LT || @operator == Token.LE || @operator == Token.GT || @operator == Token.GE)
            {
                Configuration config = context.GetConfiguration();
                IItemMappingFunction map = ItemMapper.Of(item => Number_1.Convert((AtomicValue)item, config));
                iter0 = new ItemMappingIterator(iter0, map, true);
                iter1 = new ItemMappingIterator(iter1, map, true);
            }

            // Compare all pairs of atomic values in the two atomized sequences
            List<AtomicValue> seq1 = null;
            AtomicValue item0;
            while ((item0 = (AtomicValue)iter0.Next()) != null)
            {
                if (iter1 != null)
                {
                    while (true)
                    {
                        AtomicValue item1 = (AtomicValue)iter1.Next();
                        if (item1 == null)
                        {
                            iter1 = null;
                            if (seq1 == null)
                            {
                                // second operand is empty
                                return false;
                            }

                            break;
                        }

                        try
                        {
                            if (Compare(item0, singletonOperator, item1, comparer, context))
                            {
                                return true;
                            }

                            if (seq1 == null)
                            {
                                seq1 = new List<AtomicValue>(40);
                            }

                            seq1.Add(item1);
                        }
                        catch (XPathException e)
                        {
                            throw e.MaybeWithLocation(GetLocation()).MaybeWithContext(context);
                        }
                    }
                }
                else
                {
                    foreach (AtomicValue item1 in seq1)
                    {
                        if (Compare(item0, singletonOperator, item1, comparer, context))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            GeneralComparison10 gc = new GeneralComparison10(GetLhsExpression().Copy(rebindings), @operator, GetRhsExpression().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, gc);
            gc.SetRetainedStaticContext(GetRetainedStaticContext());
            gc.comparer = comparer;
            gc.atomize0 = atomize0;
            gc.atomize1 = atomize1;
            gc.maybeBoolean0 = maybeBoolean0;
            gc.maybeBoolean1 = maybeBoolean1;
            return gc;
        }

        private static bool Compare(AtomicValue a0,
                                    int @operator,
                                    AtomicValue a1,
                                    IAtomicComparer comparer,
                                    IXPathContext context)
        {
            comparer = comparer.ProvideContext(context);
            ConversionRules rules = context.GetConfiguration().GetConversionRules();

            BuiltInAtomicType t0 = a0.PrimitiveType;
            BuiltInAtomicType t1 = a1.PrimitiveType;

            // If either operand is a number, convert both operands to xs:double using
            // the rules of the number() function, and compare them
            if (t0.IsPrimitiveNumeric() || t1.IsPrimitiveNumeric())
            {
                DoubleValue v0 = Number_1.Convert(a0, context.GetConfiguration());
                DoubleValue v1 = Number_1.Convert(a1, context.GetConfiguration());
                return ValueComparison.Compare(v0, @operator, v1, comparer, false);
            }

            // If either operand is a string, or if both are untyped atomic, convert
            // both operands to strings and compare them
            if (t0.Equals(BuiltInAtomicType.STRING) || t1.Equals(BuiltInAtomicType.STRING) ||
                    (t0.Equals(BuiltInAtomicType.UNTYPED_ATOMIC) && t1.Equals(BuiltInAtomicType.UNTYPED_ATOMIC)))
            {
                StringValue s0 = new StringValue(a0.UnicodeStringValue);
                StringValue s1 = new StringValue(a1.UnicodeStringValue);
                return ValueComparison.Compare(s0, @operator, s1, comparer, false);
            }

            // If either operand is untyped atomic,
            // convert it to the type of the other operand, and compare
            if (t0.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                a0 = t1.GetStringConverter(rules).Convert(a0).AsAtomic();
            }

            if (t1.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                a1 = t0.GetStringConverter(rules).Convert(a1).AsAtomic();
            }

            return ValueComparison.Compare(a0, @operator, a1, comparer, false);
        }

        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.BOOLEAN;
        }

        protected override void ExplainExtraAttributes(ExpressionPresenter @out)
        {
            @out.EmitAttribute("cardinality", "many-to-many (1.0)");
            @out.EmitAttribute("comp", comparer.Save());
        }

        protected override string Tag()
        {
            return "gc10";
        }
    }
}
