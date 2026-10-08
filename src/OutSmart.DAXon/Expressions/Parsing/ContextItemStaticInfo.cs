////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class ContextItemStaticInfo
    {

        /// <summary>
        /// Default information when nothing else is known
        /// </summary>
        public static readonly ContextItemStaticInfo DEFAULT = new ContextItemStaticInfo(AnyItemType.GetInstance(), true);
        public static readonly ContextItemStaticInfo ABSENT = new ContextItemStaticInfo(ErrorType.GetInstance(), true);
        private readonly ItemType itemType;
        private readonly bool contextMaybeUndefined;
        private Expression contextSettingExpression;
        private bool parentless;

        public virtual Expression ContextSettingExpression
        {
            get => contextSettingExpression; set
            {
                contextSettingExpression = value;
            }
        }

        public virtual UType ContextItemUType => itemType.GetUType();
        public ContextItemStaticInfo(ItemType itemType, bool maybeUndefined)
        {
            this.itemType = itemType;
            this.contextMaybeUndefined = maybeUndefined;
        }

        public virtual ItemType GetItemType()
        {
            return itemType;
        }

        public virtual bool IsPossiblyAbsent()
        {
            return contextMaybeUndefined;
        }

        /// <summary>
        /// Set streaming posture. The Saxon-HE version of this method has no effect.
        /// </summary>
        public virtual void SetContextPostureStriding()
        {
        }

        /// <summary>
        /// Set streaming posture. The Saxon-HE version of this method has no effect.
        /// </summary>
        public virtual void SetContextPostureGrounded()
        {
        }

        public virtual bool IsStrictStreamabilityRules()
        {
            return false;
        }

        public virtual void SetParentless(bool parentless)
        {
            this.parentless = parentless;
        }

        public virtual bool IsParentless()
        {
            return parentless;
        }
    }
}