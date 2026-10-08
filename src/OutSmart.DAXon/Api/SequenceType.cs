////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// A SequenceType is the combination of an ItemType and an OccurrenceIndicator
    /// </summary>
    public class SequenceType
    {
        /// <summary>
        /// Constant representing the universal sequence type <code>item()*</code>, which permits any value
        /// </summary>
        public static readonly SequenceType ANY = new SequenceType(ItemType.ANY_ITEM, OccurrenceIndicator.ZERO_OR_MORE);
        public static readonly SequenceType EMPTY = new SequenceType(ItemType.ERROR, OccurrenceIndicator.ZERO);
        private readonly ItemType itemType;
        private readonly OccurrenceIndicator occurrenceIndicator;

        public virtual Values.SequenceType UnderlyingSequenceType => Values.SequenceType.MakeSequenceType(itemType.UnderlyingItemType, occurrenceIndicator.GetCardinality());
        private SequenceType(ItemType itemType, OccurrenceIndicator occurrenceIndicator)
        {
            this.itemType = itemType;
            this.occurrenceIndicator = occurrenceIndicator;
        }

        public static SequenceType MakeSequenceType(ItemType itemType, OccurrenceIndicator occurrenceIndicator)
        {
            return new SequenceType(itemType, occurrenceIndicator);
        }

        public virtual ItemType GetItemType()
        {
            return itemType;
        }

        public virtual OccurrenceIndicator GetOccurrenceIndicator()
        {
            return occurrenceIndicator;
        }

        public virtual bool Matches(XdmValue value)
        {
            return value.Matches(this);
        }

        public override bool Equals(object other)
        {
            return other is SequenceType && ((SequenceType)other).GetOccurrenceIndicator().Equals(GetOccurrenceIndicator()) && ((SequenceType)other).GetItemType().Equals(GetItemType());
        }

        public override int GetHashCode()
        {
            return GetItemType().GetHashCode() ^ (GetOccurrenceIndicator().GetHashCode() << 17);
        }
    }
}
