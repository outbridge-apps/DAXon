////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the XSLT function current-group()
    /// </summary>
    internal sealed class CurrentGroupCall : Expression, ICallable
    {
        private bool inHigherOrderOperand;
        private ItemType itemType = AnyItemType.GetInstance();
        private ForEachGroup controllingInstruction; // may be unknown, when current group has dynamic scope
        public override Expression ScopingExpression => ControllingInstruction;

        public ForEachGroup ControllingInstruction
        {
            get
            {
                if (controllingInstruction == null)
                {
                    controllingInstruction = FindControllingInstruction(this);
                }

                return controllingInstruction;
            }
        }

        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_CURRENT_GROUP;

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string StreamerName => "CurrentGroup";

        public void SetControllingInstruction(ForEachGroup instruction, ItemType itemType, bool isHigherOrder)
        {
            ResetLocalStaticProperties();
            this.controllingInstruction = instruction;
            this.inHigherOrderOperand = isHigherOrder;
            this.itemType = itemType;
        }

        public override void ResetLocalStaticProperties()
        {
            base.ResetLocalStaticProperties();
            this.controllingInstruction = null;
            this.itemType = AnyItemType.GetInstance();
        }

        public static ForEachGroup FindControllingInstruction(Expression exp)
        {
            Expression child = exp;
            Expression parent = exp.ParentExpression;
            while (parent != null)
            {
                if (parent is ForEachGroup && (child == ((ForEachGroup)parent).GetActionExpression() || child == ((ForEachGroup)parent).GetSortKeyDefinitionList()))
                {
                    return (ForEachGroup)parent;
                }

                child = parent;
                parent = parent.ParentExpression;
            }

            return null;
        }

        /// <summary>
        /// Determine the item type of the value returned by the function
        /// </summary>
        public override ItemType GetItemType()
        {
            if (itemType == AnyItemType.GetInstance() && controllingInstruction != null)
            {
                itemType = controllingInstruction.GetSelectExpression().GetItemType();
            }

            return itemType;
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("currentGroup");
            @out.EndElement();
        }

        protected override int ComputeSpecialProperties()
        {
            if (ControllingInstruction == null)
            {
                return 0;
            }
            else
            {
                return controllingInstruction.GetSelectExpression().GetSpecialProperties();
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            CurrentGroupCall cg = new CurrentGroupCall();
            cg.inHigherOrderOperand = inHigherOrderOperand;
            cg.itemType = itemType;
            cg.controllingInstruction = controllingInstruction;
            return cg;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IGroupIterator gi = context.GetCurrentGroupIterator();
            if (gi == null)
            {
                throw new XPathException("There is no current group", "XTDE1061").WithLocation(GetLocation());
            }

            return gi.CurrentGroup();
        }

        public override string ToString()
        {
            return "current-group()";
        }

        public override string ToShortString()
        {
            return ToString();
        }

        public override Elaborator GetElaborator()
        {
            return new CurrentGroupCallElaborator();
        }

        private sealed class CurrentGroupCallElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                CurrentGroupCall expr = (CurrentGroupCall)GetExpression();
                return (context) =>
                {
                    IGroupIterator gi = context.GetCurrentGroupIterator();
                    if (gi == null)
                    {
                        throw new XPathException("There is no current group", "XTDE1061").WithLocation(expr.GetLocation());
                    }

                    return gi.CurrentGroup().Iterate();
                };
            }
        }
    }
}
