////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal abstract class BooleanElaborator : Elaborator
    {
        public override IPullEvaluator ElaborateForPull()
        {
            IBooleanEvaluator b = ElaborateForBoolean();
            return (context) => SingletonIterator.MakeIterator(BooleanValue.Get(b.Eval(context)));
        }

        public override IPushEvaluator ElaborateForPush()
        {
            IBooleanEvaluator b = ElaborateForBoolean();
            return (@out, context) =>
            {
                @out.Append(BooleanValue.Get(b.Eval(context)));
                return null;
            };
        }

        public override IItemEvaluator ElaborateForItem()
        {
            IBooleanEvaluator b = ElaborateForBoolean();
            return (context) => BooleanValue.Get(b.Eval(context));
        }

        public abstract override IBooleanEvaluator ElaborateForBoolean();
        public override IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent)
        {
            IBooleanEvaluator b = ElaborateForBoolean();
            return (context) => b.Eval(context) ? StringConstants.TRUE : StringConstants.FALSE;
        }
    }
}