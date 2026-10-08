////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Expressions
{
    public interface ILocalBinding : IBinding
    {
        int LocalSlotNumber { get; }
        /// <summary>
        /// Say that the bound value has the potential to be indexed
        /// </summary>
        void SetIndexedVariable();
        /// <summary>
        /// Say that the bound value has the potential to be indexed
        /// </summary>
        bool IsIndexedVariable();
    }
}