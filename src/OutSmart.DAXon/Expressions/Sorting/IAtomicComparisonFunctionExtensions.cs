////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Sorting
{

    // Extension methods at namespace level (extensions can't live in nested classes).
    internal static class IAtomicComparisonFunctionExtensions
    {
        public static bool Compare(this GenericAtomicComparer.IAtomicComparisonFunction f, AtomicValue v0, AtomicValue v1, IXPathContext context)
            => f == null ? false : f(v0, v1, context);
    }
}
