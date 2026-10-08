////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Model
{
    public class NamespaceDeltaMap : NamespaceMap, INamespaceBindingSet, INamespaceResolver
    {
        private static readonly NamespaceDeltaMap EMPTY_MAP = new NamespaceDeltaMap();

        private NamespaceDeltaMap()
        {
            prefixes = new string[]
            {
            };
            uris = new NamespaceUri[]
            {
            };
        }
        public static NamespaceDeltaMap EmptyMap()
        {
            return EMPTY_MAP;
        }

        protected override NamespaceMap MakeNamespaceMap()
        {
            return new NamespaceDeltaMap();
        }

        public override bool AllowsNamespaceUndeclarations()
        {
            return true;
        }

        public override NamespaceMap Put(string prefix, NamespaceUri uri)
        {
            return (NamespaceDeltaMap)base.Put(prefix, uri);
        }

        public override NamespaceMap Remove(string prefix)
        {
            return (NamespaceDeltaMap)base.Remove(prefix);
        }
    }
}
