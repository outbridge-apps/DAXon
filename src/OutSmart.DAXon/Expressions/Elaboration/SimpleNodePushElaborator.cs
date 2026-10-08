////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using System;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal abstract class SimpleNodePushElaborator : Elaborator
    {
        public override ISequenceEvaluator Eagerly()
        {
            IItemEvaluator itemEval = ElaborateForItem();
            return new OptionalItemEvaluator(itemEval);
        }

        public override IPullEvaluator ElaborateForPull()
        {
            IItemEvaluator itemEval = ElaborateForItem();
            return (context) => SingletonIterator.MakeIterator(itemEval.Eval(context));
        }

        public override IPushEvaluator ElaborateForPush()
        {

            // Must be implemented in a subclass
            throw new NotSupportedException();
        }

        public override IItemEvaluator ElaborateForItem()
        {
            SimpleNodeConstructor instr = (SimpleNodeConstructor)GetExpression();
            IItemEvaluator select = instr.Select.MakeElaborator().ElaborateForItem();
            short kind = (short)instr.GetItemType().PrimitiveType;
            Configuration config = GetConfiguration();
            return (context) =>
            {
                IItem contentItem = select.Eval(context);
                UnicodeString content;
                if (contentItem == null)
                {
                    content = EmptyUnicodeString.GetInstance();
                }
                else
                {
                    content = contentItem.UnicodeStringValue;
                    content = instr.CheckContent(content, context);
                }

                Orphan o = new Orphan(config);
                o.SetNodeKind(kind);
                o.SetStringValue(content);
                o.SetNodeName(instr.EvaluateNodeName(context)); // TODO elaborate this
                return o;
            };
        }

        public override IBooleanEvaluator ElaborateForBoolean()
        {
            IItemEvaluator ie = ElaborateForItem();
            return (context) => ExpressionTool.EffectiveBooleanValue(ie.Eval(context));
        }

        public override IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent)
        {
            IItemEvaluator ie = ElaborateForItem();
            return (context) =>
            {
                IItem item = ie.Eval(context);
                return item == null ? HandleNullUnicodeString(zeroLengthWhenAbsent) : item.UnicodeStringValue;
            };
        }
    }
}