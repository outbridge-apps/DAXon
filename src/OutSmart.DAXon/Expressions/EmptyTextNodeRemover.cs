////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions
{
    // Faithful port of net/sf/saxon/expr/EmptyTextNodeRemover.java (Saxon 12.9).
    // Removes zero-length text nodes from a sequence (simple content construction, phase 1
    // degenerate case when no merging of adjacent text nodes is needed).
    internal sealed class EmptyTextNodeRemover : UnaryExpression, IItemMappingFunction
    {

        public override int ImplementationMethod => Expression.ITERATE_METHOD | ITEM_FEED_METHOD | WATCH_METHOD;

        public override string StreamerName => "EmptyTextNodeRemover";

        public override string ExpressionName => "emptyTextNodeRemover";
        public EmptyTextNodeRemover(Expression p0) : base(p0)
        {
        }

        public override Types.ItemType GetItemType()
        {
            return BaseExpression.GetItemType();
        }

        protected override int ComputeCardinality()
        {
            return BaseExpression.GetCardinality() | StaticProperty.ALLOWS_ZERO;
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SAME_FOCUS_ACTION;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            EmptyTextNodeRemover e2 = new EmptyTextNodeRemover(BaseExpression.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, e2);
            return e2;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return new ItemMappingIterator(BaseExpression.Iterate(context), this);
        }

        public IItem MapItem(IItem item)
        {
            if (item is NodeInfo &&
                ((NodeInfo)item).GetNodeKind() == Types.Type.TEXT &&
                item.UnicodeStringValue.IsEmpty())
            {
                return null;
            }
            else
            {
                return item;
            }
        }

        public override Elaborator GetElaborator()
        {
            return new EmptyTextNodeRemoverElaborator();
        }

        internal sealed class EmptyTextNodeRemoverElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                EmptyTextNodeRemover expr = (EmptyTextNodeRemover)GetExpression();
                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();

                return context => new ItemMappingIterator(baseEval(context), expr);
            }
        }
    }
}
