////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Types
{
    internal interface IComplexType : ISchemaType
    {
        ComplexVariety Variety { get; }
        bool IsSimpleContent();
        ISimpleType SimpleContentType { get; }
        bool IsEmptyContent();
        bool IsMixedContent();
        ISchemaType GetElementParticleType(int elementName, bool considerExtensions);
        int GetElementParticleCardinality(int elementName, bool considerExtensions);
        ISimpleType GetAttributeUseType(StructuredQName attributeName);
        int GetAttributeUseCardinality(StructuredQName attributeName);
        bool AllowsAttributes();
        void GatherAllPermittedChildren(IntHashSet children, bool ignoreWildcards);
        void GatherAllPermittedDescendants(IntHashSet descendants);
        ISchemaType GetDescendantElementType(int fingerprint);
        int GetDescendantElementCardinality(int elementFingerprint);
        bool ContainsElementWildcard();
        bool HasAssertions();
    }
}