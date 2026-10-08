////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Types
{
    public class AnyExternalObjectType : ItemType
    {
        public static AnyExternalObjectType THE_INSTANCE = new AnyExternalObjectType();

        public virtual int PrimitiveType => -1;

        public virtual string BasicAlphaCode => "X";

        public virtual double DefaultPriority => -1;
        protected AnyExternalObjectType()
        {
        }

        public virtual bool IsAtomicType()
        {
            return false;
        }

        public virtual bool Matches(IItem item, TypeHierarchy th)
        {
            return item.GetGenre() == Genre.EXTERNAL;
        }

        public virtual bool IsPlainType()
        {
            return false;
        }

        public virtual ItemType GetPrimitiveItemType()
        {
            return this;
        }

        public virtual UType GetUType()
        {
            return UType.EXTENSION;
        }

        public virtual IAtomicType GetAtomizedItemType()
        {
            return BuiltInAtomicType.STRING;
        }

        public virtual bool IsAtomizable(TypeHierarchy th)
        {
            return true;
        }

        public virtual Genre GetGenre()
        {
            return Genre.EXTERNAL;
        }
    }
}