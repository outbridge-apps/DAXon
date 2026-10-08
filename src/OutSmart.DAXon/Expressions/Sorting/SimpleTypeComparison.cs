////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class SimpleTypeComparison
    {
        private static readonly SimpleTypeComparison _i = new SimpleTypeComparison();
        public static SimpleTypeComparison GetInstance() => _i;
        public bool Equal(object a, object b) => object.Equals(a, b);
        public int Hash(object key) => key?.GetHashCode() ?? 0;
    }
}
