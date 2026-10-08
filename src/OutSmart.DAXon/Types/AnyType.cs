////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Collections;

namespace OutSmart.DAXon.Types
{

    // Xs:anyType IS the root complex type
    // (upstream is `enum AnyType implements ComplexType`). This stub previously declared only
    // ISchemaTypeStubBase, so `contentType is IComplexType` was FALSE in
    // AxisExpression.ComputeCardinality's CHILD branch -> it returned StaticProperty.EMPTY for every
    // child::ELEMENT step whose context content-type is xs:anyType (the untyped/any default). Every
    // relative element step therefore had static cardinality EMPTY, so TypeChecker skipped atomization
    // (SXWN9027 "NameTest cannot be converted to xs:string") and the optimizer treated the step as
    // always-empty -> FinDim/Trans produced []. Declare IComplexType and implement it faithfully to
    // upstream net.sf.saxon.type.AnyType (open content model: any child/descendant element allowed
    // ZERO_OR_MORE, attributes allowed, element wildcard).
    internal sealed class AnyType : ISchemaTypeStubBase, IComplexType
    {
        private static readonly AnyType _instance = new AnyType();
        public static readonly AnyType INSTANCE = _instance;
        // xs:anyType is the root of the type hierarchy: base type is null (terminates the base-type walk in
        // TypeHierarchy.SchemaTypeRelationship). Was the inherited NIE stub.
        public override ISchemaType BaseType => null;
        public override string Name => "anyType";
        public override string EQName => "Q{" + NamespaceUri.SCHEMA + "}anyType";
        public override string DisplayName => "xs:anyType";
        public override NamespaceUri TargetNamespace => NamespaceUri.SCHEMA;
        public override int Fingerprint => StandardNames.XS_ANY_TYPE;
        public ComplexVariety Variety => ComplexVariety.MIXED;
        public ISimpleType SimpleContentType => AnySimpleType.GetInstance(); // upstream AnyType.getSimpleContentType()
        public static AnyType GetInstance() => _instance;
        public override bool IsComplexType() => true;
        public bool IsSimpleContent() => false;
        public bool IsEmptyContent() => false;
        public bool IsMixedContent() => true;
        public ISchemaType GetElementParticleType(int elementName, bool considerExtensions) => this;
        public int GetElementParticleCardinality(int elementName, bool considerExtensions) => StaticProperty.ALLOWS_ZERO_OR_MORE;
        public ISimpleType GetAttributeUseType(StructuredQName attributeName) => AnySimpleType.GetInstance();
        public int GetAttributeUseCardinality(StructuredQName attributeName) => StaticProperty.ALLOWS_ZERO_OR_ONE;
        public bool AllowsAttributes() => true;
        public void GatherAllPermittedChildren(IntHashSet children, bool ignoreWildcards) => children.Add(-1);
        public void GatherAllPermittedDescendants(IntHashSet descendants) => descendants.Add(-1);
        public ISchemaType GetDescendantElementType(int fingerprint) => this;
        public int GetDescendantElementCardinality(int elementFingerprint) => StaticProperty.ALLOWS_ZERO_OR_MORE;
        public bool ContainsElementWildcard() => true;
        public bool HasAssertions() => false;
    }
}
