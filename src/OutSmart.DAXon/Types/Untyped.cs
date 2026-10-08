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

    // deep-equal frontier (2026-06-17): xs:untyped IS a complex type
    // (upstream is `enum Untyped implements ComplexType`). This stub previously declared only
    // ISchemaTypeStubBase, so `(IComplexType)node.GetSchemaType()` in fn:deep-equal's element
    // comparison (which casts then calls GetVariety()/IsSimpleContent()) threw InvalidCast for
    // untyped HE elements, whose GetSchemaType() returns Untyped.INSTANCE. Declare IComplexType and
    // implement it faithfully to upstream net.sf.saxon.type.Untyped (mixed open content model:
    // any child/descendant element allowed ZERO_OR_MORE, attributes untypedAtomic, element wildcard,
    // treated as a restriction of xs:anyType).
    internal sealed class Untyped : ISchemaTypeStubBase, IComplexType
    {
        private static readonly Untyped _instance = new Untyped();
        public static readonly Untyped INSTANCE = _instance;
        // MUST return XS_UNTYPED (276): TinyTree.GetTypedValueOfElement keys on
        // GetSchemaType(n).GetFingerprint() == XS_UNTYPED to read an untyped element's string value.
        // The ISchemaTypeStubBase default (-1) made that branch fall through -> typed value null ->
        // every untyped element atomized to empty (FinDim Bug A Layer 6: normalize-space/!='' empty).
        public override int Fingerprint => StandardNames.XS_UNTYPED;
        // xs:untyped derives from xs:anyType (was the inherited NIE stub — see AnySimpleType.GetBaseType).
        public override ISchemaType BaseType => AnyType.INSTANCE;
        public override string Name => "untyped";
        // Identity members were inherited NIE stubs; reached since ToString overrides became real
        // (R10 CS0114 sweep) — RoleDiagnostic composes element(N, xs:untyped) in type-error messages.
        public override string EQName => "Q{" + NamespaceUri.SCHEMA + "}untyped";
        public override string DisplayName => "xs:untyped";
        public override NamespaceUri TargetNamespace => NamespaceUri.SCHEMA;

        // --- IComplexType members (faithful to net.sf.saxon.type.Untyped) ---
        public ComplexVariety Variety => ComplexVariety.MIXED;
        public ISimpleType SimpleContentType => null;
        public static Untyped GetInstance() => _instance;
        public override bool IsComplexType() => true;
        public bool IsSimpleContent() => false;
        public bool IsEmptyContent() => false;
        public bool IsMixedContent() => true;
        public ISchemaType GetElementParticleType(int elementName, bool considerExtensions) => this;
        public int GetElementParticleCardinality(int elementName, bool considerExtensions) => StaticProperty.ALLOWS_ZERO_OR_MORE;
        public ISimpleType GetAttributeUseType(StructuredQName attributeName) => BuiltInAtomicType.UNTYPED_ATOMIC;
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
