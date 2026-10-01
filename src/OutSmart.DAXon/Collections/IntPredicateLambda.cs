////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Collections
{
    // IntPredicateLambda now implements IIntPredicateProxy (20 callsites assign to IIntPredicateProxy field).
    internal sealed class IntPredicateLambda : IIntPredicateProxy
    {
        private readonly Func<int, bool> _f;
        public IntPredicateLambda(Func<int, bool> f) { _f = f; }
        public bool Test(int value) => _f?.Invoke(value) ?? false;
        public IIntPredicateProxy Union(IIntPredicateProxy other) => new IntPredicateLambda(v => Test(v) || (other?.Test(v) ?? false));
        public static IntPredicateLambda Of(Func<int, bool> f) => new IntPredicateLambda(f);
    }
}
