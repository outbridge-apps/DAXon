////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public class NumberInstruction : Expression
    {
        public const int SINGLE = 0;
        public const int MULTI = 1;
        public const int ANY = 2;
        public const int SIMPLE = 3;
        public static readonly string[] LEVEL_NAMES = new string[]
        {
            "single",
            "multi",
            "any",
            "simple"
        };
        private readonly Operand selectOp;
        private readonly int level;
        private readonly Operand countOp;
        private readonly Operand fromOp;
        private readonly bool hasVariablesInPatterns = false;

        public virtual int Level => level;

        public virtual Patterns.Pattern From => fromOp == null ? null : (Patterns.Pattern)fromOp.GetChildExpression();

        public virtual Expression Select => selectOp.GetChildExpression();

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string ExpressionName => "xsl:number";
        public NumberInstruction(Expression select, int level, Patterns.Pattern count, Patterns.Pattern from)
        {
            selectOp = new Operand(this, select, new OperandRole(0, OperandUsage.NAVIGATION, SequenceType.SINGLE_NODE));
            this.level = level;
            if (count != null)
            {
                countOp = new Operand(this, count, OperandRole.INSPECT);
            }

            if (from != null)
            {
                fromOp = new Operand(this, from, OperandRole.INSPECT);
            }

            this.hasVariablesInPatterns = Patterns.Pattern.PatternContainsVariable(count) || Patterns.Pattern.PatternContainsVariable(from);
        }

        public override bool IsInstruction()
        {
            return true;
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandSparseList(selectOp, countOp, fromOp);
        }

        public virtual Patterns.Pattern GetCount()
        {
            return countOp == null ? null : (Patterns.Pattern)countOp.GetChildExpression();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            NumberInstruction exp = new NumberInstruction(Copy(selectOp, rebindings), level, Copy(GetCount(), rebindings), Copy(From, rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        private Expression Copy(Operand op, RebindingMap rebindings)
        {
            return op == null ? null : op.GetChildExpression().Copy(rebindings);
        }

        private Patterns.Pattern Copy(Patterns.Pattern op, RebindingMap rebindings)
        {
            return (Patterns.Pattern)((Patterns.Pattern)op == null ? null : op.Copy(rebindings));
        }

        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.INTEGER;
        }

        protected override int ComputeCardinality()
        {
            switch (level)
            {
                case SIMPLE:
                case SINGLE:
                case ANY:
                    return StaticProperty.ALLOWS_ZERO_OR_ONE;
                case MULTI:
                default:
                    return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e = base.Optimize(visitor, contextInfo);
            if (e != this)
            {
                return e;
            }

            if (GetPackageData().TargetEdition == "EE")
            {
                e = visitor.ObtainOptimizer().OptimizeNumberInstruction(this, contextInfo);
                if (e != null)
                {
                    return e;
                }
            }

            return this;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            NodeInfo source = (NodeInfo)selectOp.GetChildExpression().EvaluateItem(context);
            return GetPlaceMarker(source, context);
        }

        private ISequenceIterator GetPlaceMarker(NodeInfo source, IXPathContext context)
        {
            IList<AtomicValue> numbers = new List<AtomicValue>(1);
            switch (level)
            {
                case SIMPLE:
                    {
                        long value = Navigator.GetNumberSimple(source, context);
                        if (value != 0)
                        {
                            numbers.Add(Int64Value.MakeIntegerValue(value));
                        }

                        break;
                    }

                case SINGLE:
                    {
                        long value = Navigator.GetNumberSingle(source, GetCount(), From, context);
                        if (value != 0)
                        {
                            numbers.Add(Int64Value.MakeIntegerValue(value));
                        }

                        break;
                    }

                case ANY:
                    {
                        long value = Navigator.GetNumberAny(this, source, GetCount(), From, context, hasVariablesInPatterns);
                        if (value != 0)
                        {
                            numbers.Add(Int64Value.MakeIntegerValue(value));
                        }

                        break;
                    }

                case MULTI:
                    {
                        foreach (long n in Navigator.GetNumberMulti(source, GetCount(), From, context))
                        {
                            numbers.Add(Int64Value.MakeIntegerValue(n));
                        }

                        break;
                    }
            }

            return new ListIterator.Of<AtomicValue>(numbers);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("nodeNum", this);
            @out.EmitAttribute("level", LEVEL_NAMES[level]);
            @out.SetChildRole("select");
            selectOp.GetChildExpression().Export(@out);
            if (countOp != null)
            {
                @out.SetChildRole("count");
                GetCount().Export(@out);
            }

            if (fromOp != null)
            {
                @out.SetChildRole("from");
                From.Export(@out);
            }

            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new NumberInstructionElaborator();
        }

        private sealed class NumberInstructionElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                NumberInstruction expr = (NumberInstruction)GetExpression();
                IItemEvaluator sourceEval = expr.selectOp.GetChildExpression().MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    NodeInfo source = (NodeInfo)sourceEval.Eval(context);
                    return expr.GetPlaceMarker(source, context);
                };
            }
        }
    }
}
