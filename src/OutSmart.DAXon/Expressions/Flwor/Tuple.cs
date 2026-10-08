////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// A tuple, as it appears in an XQuery tuple stream handled by extended FLWOR expressions.
    /// </summary>
    internal sealed class Tuple : ObjectValue<ISequence[]>
    {
        public Tuple(ISequence[] members) : base(members)
        {
        }

        public ISequence[] GetMembers()
        {
            return GetObject();
        }
    }
}