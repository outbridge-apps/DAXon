////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{

    // LocalNameFn1 is a SEPARATE class (not a NameFn1 flag) because FilterExpression:445 keys the
    // [local-name()='x'] positional-filter optimization on IsCallOn(typeof(LocalNameFn1)) - a shared class
    // would wrongly match fn:name() too (different semantics under prefixes).
    internal sealed class LocalNameFn1 : ScalarSystemFunction
    {
        public LocalNameFn1() { }
        public static Func<LocalNameFn1> New() => () => new LocalNameFn1();
        public override AtomicValue Evaluate(IItem item, IXPathContext context)
        {
            return StringValue.MakeStringValue(((NodeInfo)item).GetLocalPart());
        }
        public override ISequence ResultWhenEmpty()
        {
            return StringValue.EMPTY_STRING;
        }
    }
}
