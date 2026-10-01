////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Internal.Resolver
{

    /// <summary>Stub for org.xmlresolver.ResolverFeature&lt;T&gt;. The named feature constants are
    /// declared on a non-generic base so call sites can reference them as ResolverFeature.X without a
    /// type argument (Java accesses these statics via the raw type).</summary>
    internal class ResolverFeature
    {
        public static readonly ResolverFeature<object> CATALOG_FILES = new ResolverFeature<object>("catalog-files");
        public string Name { get; }
        public ResolverFeature(string name) { Name = name; }
    }
    internal sealed class ResolverFeature<T> : ResolverFeature
    {
        public ResolverFeature(string name) : base(name) { }
    }
}
