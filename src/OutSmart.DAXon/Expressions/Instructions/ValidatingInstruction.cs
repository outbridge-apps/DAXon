////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Interface implemented by instructions that have validation and type attributes
    /// </summary>
    public interface IValidatingInstruction
    {
        ISchemaType GetSchemaType();
        int GetValidationAction();
    }
}