////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;

namespace OutSmart.DAXon.Model
{
    // DummyNamespaceResolver implements INamespaceResolver (8 callers).
    internal sealed class DummyNamespaceResolver : INamespaceResolver
    {
        private static readonly DummyNamespaceResolver _instance = new DummyNamespaceResolver();
        public DummyNamespaceResolver() { }
        public static DummyNamespaceResolver GetInstance() => _instance;
        // Upstream: the empty prefix maps to no-namespace, any other prefix is unknown (null).
        // ValidateContent passes this resolver for QName-ish content — the stub crashed there.
        public NamespaceUri GetURIForPrefix(string prefix, bool useDefault) => string.IsNullOrEmpty(prefix) ? NamespaceUri.NULL : null;
        public IEnumerator<string> IteratePrefixes() => Enumerable.Empty<string>().GetEnumerator();
    }
}
