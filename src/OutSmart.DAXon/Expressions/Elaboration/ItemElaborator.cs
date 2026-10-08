////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal abstract class ItemElaborator : Elaborator
    {
        public override ISequenceEvaluator Eagerly()
        {
            bool maybeEmpty = Cardinality.AllowsZero(GetExpression().GetCardinality());
            IItemEvaluator ie = ElaborateForItem();
            if (maybeEmpty)
            {
                return new OptionalItemEvaluator(ie);
            }
            else
            {
                return new SingleItemEvaluator(ie);
            }
        }

        public override IPullEvaluator ElaborateForPull()
        {
            IItemEvaluator ie = ElaborateForItem();
            return (context) => SingletonIterator.MakeIterator(ie.Eval(context));
        }

        public override IPushEvaluator ElaborateForPush()
        {
            IItemEvaluator ie = ElaborateForItem();
            return (@out, context) =>
            {
                IItem it = ie.Eval(context);
                if (it != null)
                {
                    @out.Append(it);
                }

                return null;
            };
        }

        public abstract override IItemEvaluator ElaborateForItem();
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