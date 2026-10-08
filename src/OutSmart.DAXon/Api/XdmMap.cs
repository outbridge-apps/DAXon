////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Values.Maps;

namespace OutSmart.DAXon.Api
{

    // upstream: XdmMap extends XdmFunctionItem (a map IS an item) — as XdmValue it broke
    // XdmValue.Wrap's singleton dispatch contract (callers cast Wrap(item) to XdmItem)
    public class XdmMap : XdmFunctionItem
    {
        // The empty map.
        public XdmMap() : base(new HashTrieMap()) { }
        public XdmMap(OutSmart.DAXon.Model.IItem map) : base(map) { }

        // A copy of the entries: changing it leaves the map as it was.
        public override Dictionary<XdmAtomicValue, XdmValue> AsMap()
        {
            var entries = new Dictionary<XdmAtomicValue, XdmValue>();
            if (UnderlyingValue is MapItem map)
            {
                foreach (OutSmart.DAXon.Values.Maps.KeyValuePair entry in map.KeyValuePairs())
                {
                    entries[new XdmAtomicValue(entry.key)] = XdmValue.Wrap(entry.value);
                }
            }

            return entries;
        }

        // upstream s9api XdmMap.put: functional add — returns a NEW map, the receiver is unchanged.
        public virtual XdmMap Put(XdmAtomicValue key, XdmValue value)
        {
            MapItem map = UnderlyingValue as MapItem ?? new HashTrieMap();
            return new XdmMap(map.AddEntry((AtomicValue)key.UnderlyingValue, (IGroundedValue)value.UnderlyingValue));
        }
    }
}
