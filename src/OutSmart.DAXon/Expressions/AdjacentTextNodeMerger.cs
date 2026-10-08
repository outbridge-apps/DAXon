////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions
{
    // Faithful port of net/sf/saxon/expr/AdjacentTextNodeMerger.java (Saxon 12.9).
    // First phase of "constructing simple content": eliminates empty text nodes and combines
    // adjacent text nodes into one. Was never ported — the XSLLeafNodeConstructor call was
    // commented out ("streaming not used"), but the class carries spec semantics: xsl:value-of
    // over a sequence containing zero-length text nodes kept the empty items (message-0304).
    internal sealed class AdjacentTextNodeMerger : UnaryExpression
    {

        public override int ImplementationMethod => Expression.PROCESS_METHOD | Expression.ITERATE_METHOD | ITEM_FEED_METHOD | WATCH_METHOD;

        public override string StreamerName => "AdjacentTextNodeMerger";

        public override string ExpressionName => "mergeAdj";
        public AdjacentTextNodeMerger(Expression p0) : base(p0)
        {
        }

        public static Expression MakeAdjacentTextNodeMerger(Expression @base)
        {
            if (@base is Literal && ((Literal)@base).GroundedValue is IAtomicSequence)
            {
                return @base;
            }
            else
            {
                return new AdjacentTextNodeMerger(@base);
            }
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SAME_FOCUS_ACTION;
        }

        public override Expression Simplify()
        {
            Expression operand = BaseExpression;
            if (operand is Literal && ((Literal)operand).GroundedValue is AtomicValue)
            {
                return operand;
            }
            else
            {
                return base.Simplify();
            }
        }

        private static bool CanDeliverAdjacentTextNodes(Expression expr, TypeHierarchy th)
        {
            return th.Relationship(expr.GetItemType(), NodeKindTest.TEXT) != Affinity.DISJOINT
                    && Cardinality.AllowsMany(expr.GetCardinality());
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);

            // This wrapper expression is unnecessary if the base expression cannot return text nodes,
            // or if it can return at most one item
            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            if (!CanDeliverAdjacentTextNodes(BaseExpression, th))
            {
                Expression @base = BaseExpression;
                @base.ParentExpression = ParentExpression;
                return @base;
            }

            // In a Choose expression, we can push the wrapper down to the action branches (whence it may disappear)
            if (BaseExpression is Choose)
            {
                Choose choose = (Choose)BaseExpression;
                for (int i = 0; i < choose.Size(); i++)
                {
                    if (CanDeliverAdjacentTextNodes(choose.GetAction(i), th))
                    {
                        AdjacentTextNodeMerger atm2 = new AdjacentTextNodeMerger(choose.GetAction(i));
                        choose.SetAction(i, atm2);
                    }
                }
                return choose;
            }
            // In a Block expression, check whether adjacent text nodes can occur (used in test strmode089)
            if (BaseExpression is Block)
            {
                Block block = (Block)BaseExpression;
                Operand[] actions = block.GetOperanda();
                bool prevtext = false;
                bool needed = false;
                bool maybeEmpty = false;
                foreach (Operand o in actions)
                {
                    Expression action = o.GetChildExpression();
                    bool maybetext;
                    if (action is ValueOf)
                    {
                        maybetext = true;
                        Expression content = ((ValueOf)action).Select;
                        if (content is StringLiteral)
                        {
                            // if it's empty, we could remove it now, but that's awkward and probably doesn't happen
                            maybeEmpty |= ((StringLiteral)content).GetString().IsEmpty();
                        }
                        else
                        {
                            maybeEmpty = true;
                        }
                    }
                    else
                    {
                        maybetext = action.GetStaticUType(contextInfo.ContextItemUType).Overlaps(UType.TEXT);
                        maybeEmpty |= maybetext;
                    }
                    if (prevtext && maybetext)
                    {
                        needed = true;
                        break; // may contain adjacent text nodes
                    }
                    if (maybetext && Cardinality.AllowsMany(action.GetCardinality()))
                    {
                        needed = true;
                        break; // may contain adjacent text nodes
                    }
                    prevtext = maybetext;
                }
                if (!needed)
                {
                    // We don't need to merge adjacent text nodes, we only need to remove empty ones.
                    if (maybeEmpty)
                    {
                        return new EmptyTextNodeRemover(block);
                    }
                    else
                    {
                        return block;
                    }
                }
            }
            return this;
        }

        public override Types.ItemType GetItemType()
        {
            return BaseExpression.GetItemType();
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return BaseExpression.GetStaticUType(contextItemType);
        }

        protected override int ComputeCardinality()
        {
            return BaseExpression.GetCardinality() | StaticProperty.ALLOWS_ZERO;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            AdjacentTextNodeMerger a2 = new AdjacentTextNodeMerger(BaseExpression.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, a2);
            return a2;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return new AdjacentTextNodeMergingIterator(BaseExpression.Iterate(context));
        }

        public static bool IsTextNode(IItem item)
        {
            return item is NodeInfo && ((NodeInfo)item).GetNodeKind() == Types.Type.TEXT;
        }

        public override Elaborator GetElaborator()
        {
            return new AdjacentTextNodeMergerElaborator();
        }

        internal sealed class AdjacentTextNodeMergerElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                AdjacentTextNodeMerger expr = (AdjacentTextNodeMerger)GetExpression();
                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();

                return context => new AdjacentTextNodeMergingIterator(baseEval(context));
            }
        }
    }
}
