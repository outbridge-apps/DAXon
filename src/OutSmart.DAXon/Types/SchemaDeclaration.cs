////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Types
{
    public interface ISchemaDeclaration
    {
        int Fingerprint { get; }
        StructuredQName ComponentName { get; }
        ISchemaType GetType();
        NodeTest MakeSchemaNodeTest();
        /// <summary>
        /// Determine, in the case of an IElement IDeclaration, whether it is nillable.
        /// </summary>
        bool IsNillable();
        /// <summary>
        /// Determine, in the case of an IElement IDeclaration, whether the declaration is abstract
        /// </summary>
        bool IsAbstract();
        bool HasTypeAlternatives();
    }
}