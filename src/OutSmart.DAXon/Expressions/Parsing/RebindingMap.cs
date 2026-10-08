////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class RebindingMap
    {
        private Dictionary<IBinding, IBinding> map = null; // created lazily
        // Indexer for `rebindings[binding]` syntax
        public virtual IBinding this[IBinding key]
        {
            get { return Get(key); }
        }
        public virtual void Put(IBinding oldBinding, IBinding newBinding)
        {
            if (map == null)
            {
                map = new Dictionary<IBinding, IBinding>();
            }

            map[oldBinding] = newBinding;
        }

        public virtual IBinding Get(IBinding oldBinding)
        {
            return map == null ? null : map.GetOrDefault(oldBinding);
        }

    }
}