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
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{

    // fn:substring-after: same pattern as SubstringBefore above (the full upstream port is excluded only
    // because its elaborator drags the StringElaborator cluster).
    internal sealed class SubstringAfter : CollatingFunctionFixed
    {
        public SubstringAfter() { }
        public static Func<SubstringAfter> New() => () => new SubstringAfter();
        public override bool IsSubstringMatchingFunction() => true;
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            UnicodeString s0 = GetUniStringArg(arguments[0]);
            UnicodeString s1 = GetUniStringArg(arguments[1]);
            IStringCollator collator = StringCollator;
            return new StringValue(((ISubstringMatcher)collator).SubstringAfter(s0, s1));
        }

        public override Expressions.Elaboration.Elaborator GetElaborator()
        {
            return new SubstringMatchElaborator();
        }
    }
}
