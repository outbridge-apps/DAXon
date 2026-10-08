////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Types;
using System.Collections.Generic;
namespace OutSmart.DAXon.Values.Maps
{
    public interface IRecordType : IFunctionItemType
    {
        IEnumerable<string> FieldNames { get; }
        SequenceType GetFieldType(string field);
        bool IsOptionalField(string field);
        bool IsExtensible();
    }
}