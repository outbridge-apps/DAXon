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
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{

    // NormalizeSpace_1 (fn:normalize-space#1) — REAL elaborator-free impl ported from the excluded
    // NormalizeSpace_1.cs. Same approach as String_1: ScalarSystemFunction.Call delegates to Evaluate;
    // the real file's GetElaborator() (NormalizeSpaceFnElaborator : StringElaborator) is intentionally
    // omitted to avoid the StringElaborator compile cluster that explodes the pipeline. Correctness from
    // Evaluate; the elaborator (optimization) is deferred.
    internal sealed class NormalizeSpace_1 : ScalarSystemFunction
    {
        public NormalizeSpace_1() { }
        public static Func<NormalizeSpace_1> New() => () => new NormalizeSpace_1();
        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            return new StringValue(NormalizeSpace(arg.UnicodeStringValue));
        }
        public static UnicodeString NormalizeSpace(UnicodeString sv)
        {
            if (sv == null)
            {
                return EmptyUnicodeString.GetInstance();
            }
            return Whitespace.CollapseWhitespace(sv);
        }
        public override ISequence ResultWhenEmpty()
        {
            return StringValue.EMPTY_STRING;
        }
    }
}
