////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values.Arrays;

namespace OutSmart.DAXon.Api
{
    public class XdmArray : XdmFunctionItem
    {
        // The empty array.
        public XdmArray() : base(SimpleArrayItem.EMPTY_ARRAY) { }
        public XdmArray(IItem array) : base(array) { }

        // upstream s9api XdmArray(XdmItem[]): each member becomes one array member.
        public XdmArray(XdmItem[] members) : base(MakeArray(members)) { }

        private static IItem MakeArray(XdmItem[] members)
        {
            var list = new List<IGroundedValue>(members.Length);
            foreach (XdmItem m in members)
                list.Add((IGroundedValue)m.UnderlyingValue);

            return new SimpleArrayItem(list);
        }
    }
}
