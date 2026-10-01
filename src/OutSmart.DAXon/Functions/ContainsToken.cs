////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    internal sealed class ContainsToken : CollatingFunctionFixed
    {
        public override bool IsSubstringMatchingFunction()
        {
            return true;
        }

        private static bool ContainsTokenFn(ISequenceIterator arg0, UnicodeString arg1, IStringCollator collator)
        {
            if (arg1 == null)
            {
                return false;
            }

            UnicodeString search = Whitespace.Trim(arg1);
            if (search.IsEmpty())
            {
                return false;
            }

            for (IItem item; (item = arg0.Next()) != null;)
            {
                ISequenceIterator tokens = new Whitespace.Tokenizer(item.UnicodeStringValue);
                for (IItem token; (token = tokens.Next()) != null;)
                {
                    if (collator.ComparesEqual(search, token.UnicodeStringValue))
                    {
                        tokens.Dispose();
                        arg0.Dispose();
                        return true;
                    }
                }
            }

            return false;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return BooleanValue.Get(ContainsTokenFn(arguments[0].Iterate(), arguments[1].Head().UnicodeStringValue, StringCollator));
        }
    }
}