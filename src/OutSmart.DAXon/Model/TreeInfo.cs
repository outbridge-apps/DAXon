////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    public interface ITreeInfo
    {
        NodeInfo GetRootNode();
        Configuration GetConfiguration();
        long GetDocumentNumber();
        bool IsTyped();



        bool IsMutable();



        NodeInfo SelectID(string id, bool getParent);
        IEnumerator<string> UnparsedEntityNames { get; }
        String[] GetUnparsedEntity(string name);
        ISpaceStrippingRule SpaceStrippingRule { get; set; }
        void SetUserData(string key, object value);
        object GetUserData(string key);
        Durability GetDurability();


    }
}
