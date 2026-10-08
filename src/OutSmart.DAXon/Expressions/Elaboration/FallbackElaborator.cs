////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal class FallbackElaborator : Elaborator
    {
        public FallbackElaborator()
        {
        }

        public override ISequenceEvaluator Eagerly()
        {
            return new EagerPullEvaluator(ElaborateForPull());
        }

        public override ISequenceEvaluator Lazily(bool repeatable, bool lazyEvaluationRequired)
        {
            Expression expr = GetExpression();
            if (repeatable)
            {
                return new MemoClosureEvaluator(expr, ElaborateForPull());
            }
            else
            {
                return new LazyPullEvaluator(ElaborateForPull());
            }
        }

        public override IPullEvaluator ElaborateForPull()
        {
            return (context) => GetExpression().Iterate(context);
        }

        public override IPushEvaluator ElaborateForPush()
        {
            return (output, context) =>
            {
                GetExpression().Process(output, context);
                return null;
            };
        }

        public override IItemEvaluator ElaborateForItem()
        {
            return (context) => GetExpression().EvaluateItem(context);
        }

        public override IBooleanEvaluator ElaborateForBoolean()
        {
            return (context) => GetExpression().EffectiveBooleanValue(context);
        }

        public override IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent)
        {
            if (zeroLengthWhenAbsent)
            {
                return (context) => GetExpression().EvaluateAsString(context);
            }
            else
            {
                return (context) =>
                {
                    IItem item = GetExpression().EvaluateItem(context);
                    return item == null ? null : item.UnicodeStringValue;
                };
            }
        }
    }
}