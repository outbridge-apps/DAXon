////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    internal sealed class Tokenize_3 : RegexFunction
    {
        protected override bool AllowRegexMatchingEmptyString()
        {
            return false;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            AtomicValue sv = (AtomicValue)arguments[0].Head();
            if (sv == null)
            {
                return EmptySequence.GetInstance();
            }

            UnicodeString input = sv.UnicodeStringValue;
            if (input.IsEmpty())
            {
                return EmptySequence.GetInstance();
            }

            IRegularExpression re = GetRegularExpression(arguments, 1, 2);
            if (re == null)
            {
                return SequenceTool.ToLazySequence(new Whitespace.Tokenizer(input));
            }

            return SequenceTool.ToLazySequence(re.Tokenize(input));
        }
    }
}