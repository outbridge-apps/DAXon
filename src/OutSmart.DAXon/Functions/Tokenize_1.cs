////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{

    // Tokenize_1 (fn:tokenize#1, XPath 3.1) — REAL impl ported from the excluded Tokenize_1.cs.
    // Whitespace-tokenizes the single argument (no regex, no elaborator). Used by KeyManager's idref
    // key: tokenize(string(.)). Tokenizer = OutSmart.DAXon.Values.Whitespace.Tokenizer(UnicodeString).
    internal sealed class Tokenize_1 : SystemFunction
    {
        public Tokenize_1() { }
        public static Func<Tokenize_1> New() => () => new Tokenize_1();
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IItem item = arguments[0].Head();
            if (item == null)
            {
                return EmptySequence.GetInstance();
            }
            return SequenceTool.ToLazySequence(new Whitespace.Tokenizer(item.UnicodeStringValue));
        }
    }
}
