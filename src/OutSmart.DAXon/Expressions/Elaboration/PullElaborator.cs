////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal abstract class PullElaborator : Elaborator
    {
        public abstract override IPullEvaluator ElaborateForPull();
        public override ISequenceEvaluator Eagerly()
        {
            IPullEvaluator pull = ElaborateForPull();
            return new EagerPullEvaluator(pull);
        }

        public override IPushEvaluator ElaborateForPush()
        {
            IPullEvaluator pull = ElaborateForPull();
            return (@out, context) =>
            {
                try
                {
                    ISequenceIterator iter = pull.Iterate(context);
                    for (IItem it; (it = iter.Next()) != null;)
                    {
                        @out.Append(it);
                    }

                    return null;
                }
                catch (UncheckedXPathException err)
                {
                    throw err.GetXPathException();
                }
            };
        }

        public override IItemEvaluator ElaborateForItem()
        {
            IPullEvaluator pull = ElaborateForPull();
            return (context) =>
            {
                try
                {
                    return pull.Iterate(context).Next();
                }
                catch (UncheckedXPathException err)
                {
                    throw err.GetXPathException();
                }
            };
        }

        public override IBooleanEvaluator ElaborateForBoolean()
        {
            IPullEvaluator pull = ElaborateForPull();
            return (context) =>
            {
                try
                {
                    return ExpressionTool.EffectiveBooleanValue(pull.Iterate(context));
                }
                catch (UncheckedXPathException err)
                {
                    throw err.GetXPathException();
                }
            };
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