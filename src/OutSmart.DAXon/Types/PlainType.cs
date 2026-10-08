////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using System.Collections.Generic;
namespace OutSmart.DAXon.Types
{
    public interface IPlainType : ItemType, IHyperType
    {
        StructuredQName TypeName { get; }
        IList<IPlainType> PlainMemberTypes { get; }
        bool Matches(IItem item, TypeHierarchy th);
        IAtomicType GetPrimitiveItemType();
    }
}