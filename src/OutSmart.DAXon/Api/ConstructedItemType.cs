////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Api
{
    // An item type with no constant in ItemType: the declared type of a parameter or a variable, the static type of
    // a result. The second argument is the Configuration (or the Processor) whose type hierarchy answers for it.
    public class ConstructedItemType : ItemType
    {
        private readonly Configuration config;

        public ConstructedItemType() : base(null) { }

        public ConstructedItemType(Types.ItemType underlying, object processor) : base(underlying)
        {
            config = processor as Configuration ?? (processor as Processor)?.UnderlyingConfiguration;
        }

        public override bool Matches(XdmItem item)
        {
            return underlyingType != null && underlyingType.Matches(item.UnderlyingValue, Hierarchy());
        }

        public override bool Subsumes(ItemType other)
        {
            return underlyingType != null && Hierarchy().IsSubType(other.UnderlyingItemType, underlyingType);
        }

        private TypeHierarchy Hierarchy()
        {
            return (config ?? new Configuration()).GetTypeHierarchy();
        }
    }
}
