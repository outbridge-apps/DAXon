////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;

namespace OutSmart.DAXon.Internal.Collections
{
    // String-keyed map with a DEFAULTS chain (java.util.Properties semantics): lookups fall
    // through to the defaults instance, writes stay local. The serialization pipeline layers
    // local output properties over stylesheet/global defaults this way.
    public class Properties : Dictionary<string, string>
    {
        private readonly Properties _defaults;

        public Properties() { }
        public Properties(Properties defaults) { _defaults = defaults; }

        public string GetProperty(string key)
        {
            if (TryGetValue(key, out var v))
                return v;
            return _defaults?.GetProperty(key);
        }
        public string GetProperty(string key, string defaultValue) => GetProperty(key) ?? defaultValue;
        public object SetProperty(string key, string value)
        {
            TryGetValue(key, out var prev);
            this[key] = value;
            return prev;
        }

        // Includes keys inherited from the defaults chain, as in Java — enumerating an
        // instance whose values all live in its defaults must not come back empty.
        public ICollection<string> StringPropertyNames()
        {
            if (_defaults == null)
                return Keys;
            var names = new HashSet<string>(Keys);
            foreach (string k in _defaults.StringPropertyNames())
                names.Add(k);
            return names;
        }
    }
}
