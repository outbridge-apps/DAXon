////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Patterns
{
    // Delegating resolver that answers the empty prefix with a fixed default namespace (upstream
    // NamespaceResolverWithDefault). Was a hollow stub that DROPPED both ctor args and threw from
    // GetURIForPrefix - the XQuery parser builds one for direct constructors with a default
    // element namespace.
    internal sealed class NamespaceResolverWithDefault : INamespaceResolver
    {
        private readonly INamespaceResolver baseResolver;
        private readonly NamespaceUri defaultNamespace;

        public NamespaceResolverWithDefault(INamespaceResolver resolver, NamespaceUri defaultNs)
        {
            this.baseResolver = resolver;
            this.defaultNamespace = defaultNs;
        }

        public NamespaceUri GetURIForPrefix(string prefix, bool useDefault)
        {
            if (useDefault && string.IsNullOrEmpty(prefix))
            {
                return defaultNamespace;
            }

            return baseResolver.GetURIForPrefix(prefix, useDefault);
        }

        public IEnumerator<string> IteratePrefixes() => baseResolver.IteratePrefixes();
    }
}
