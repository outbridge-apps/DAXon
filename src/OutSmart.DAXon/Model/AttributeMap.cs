////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    public interface IAttributeMap : IEnumerable<AttributeInfo>
    {
        int Size();
        AttributeInfo Get(INodeName name);











        AttributeInfo Get(NamespaceUri uri, string local);












        AttributeInfo GetByFingerprint(int fingerprint, NamePool namePool);












        string GetValue(NamespaceUri uri, string local);




        string GetValue(string local);




        IAttributeMap Put(AttributeInfo att);













        IAttributeMap Remove(INodeName name);












        void Verify();


        IAttributeMap Apply(Func<AttributeInfo, AttributeInfo> mapper);









        List<AttributeInfo> AsList();









        AttributeInfo ItemAt(int index);


    }
}
