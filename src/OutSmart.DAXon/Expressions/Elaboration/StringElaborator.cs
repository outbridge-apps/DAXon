////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal abstract class StringElaborator : Elaborator
    {
        public virtual bool ReturnZeroLengthWhenAbsent()
        {
            return false;
        }

        public override IPullEvaluator ElaborateForPull()
        {
            IUnicodeStringEvaluator strEval = ElaborateForUnicodeString(ReturnZeroLengthWhenAbsent());
            return (context) =>
            {
                UnicodeString value = strEval.Eval(context);
                if (value == null)
                {
                    return EmptyIterator.GetInstance();
                }
                else
                {
                    return SingletonIterator.MakeIterator(StringValue.MakeUStringValue(value));
                }
            };
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

        public override IItemEvaluator ElaborateForItem()
        {
            IUnicodeStringEvaluator strEval = ElaborateForUnicodeString(ReturnZeroLengthWhenAbsent());
            return (context) =>
            {
                UnicodeString value = strEval.Eval(context);
                if (value == null)
                {
                    return null;
                }
                else
                {
                    return StringValue.MakeUStringValue(value);
                }
            };
        }

        public override IBooleanEvaluator ElaborateForBoolean()
        {
            IUnicodeStringEvaluator strEval = ElaborateForUnicodeString(ReturnZeroLengthWhenAbsent());
            return (context) =>
            {
                UnicodeString value = strEval.Eval(context);
                return value != null && !value.IsEmpty();
            };
        }

        public abstract override IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent);
    }
}
