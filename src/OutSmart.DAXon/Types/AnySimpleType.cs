////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Types
{

    // Xs:anySimpleType is the root of all simple types, so AnySimpleType genuinely IS an
    // ISimpleType; the stub only declared ISchemaTypeStubBase, so (ISimpleType)AnySimpleType.INSTANCE
    // (CastInjector-injected at BuiltInAtomicType) failed CS0030. Declare ISimpleType and stub its
    // members (ISchemaType + IsAtomicType come from the base; ISimpleType adds the rest + IHyperType).
    internal sealed class AnySimpleType : ISchemaTypeStubBase, ISimpleType
    {
        private static readonly AnySimpleType _instance = new AnySimpleType();
        public static readonly AnySimpleType INSTANCE = _instance;
        // Upstream AnySimpleType.getFingerprint() = XS_ANY_SIMPLE_TYPE. The inherited stub returned -1,
        // so MakeAtomicType(XS_ANY_ATOMIC_TYPE, AnySimpleType.INSTANCE, …) recorded baseFingerprint=-1,
        // xs:anyAtomicType.GetBaseType() returned null, and SchemaTypeRelationship's derivation walk
        // never reached anySimpleType — every atomic content type was reported DISJOINT with
        // attribute()'s anySimpleType content (spurious XTTE0505 on @as="attribute(...)").
        public override int Fingerprint => StandardNames.XS_ANY_SIMPLE_TYPE;
        public override string Name => "anySimpleType";
        public override string EQName => "Q{" + NamespaceUri.SCHEMA + "}anySimpleType";
        public override string DisplayName => "xs:anySimpleType";
        public override NamespaceUri TargetNamespace => NamespaceUri.SCHEMA;
        // xs:anySimpleType derives from xs:anyType. Was the NIE stub inherited from ISchemaTypeStubBase, which
        // crashed TypeHierarchy.SchemaTypeRelationship when it walked the base-type chain of a content type
        // like xs:anyAtomicType (K2-DefaultNamespaceProlog-17 / ForExprType002: `for $x as attribute(n, T)`).
        public override ISchemaType BaseType => AnyType.INSTANCE;
        public ISchemaType BuiltInBaseType => this;
        public int WhitespaceAction => 0;
        public static AnySimpleType GetInstance() => _instance;
        public override bool IsSimpleType() => true;
        public bool IsListType() => false;
        public bool IsUnionType() => false;
        public bool IsBuiltInType() => true;
        // Upstream: xs:anySimpleType content is always valid and types as untypedAtomic.
        public IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules) => OutSmart.DAXon.Values.StringValue.MakeUntypedAtomic(value);
        public ValidationFailure ValidateContent(UnicodeString value, INamespaceResolver nsResolver, ConversionRules rules) => null;
        public UnicodeString Preprocess(UnicodeString input) => input;
        public UnicodeString Postprocess(UnicodeString input) => input;
        public bool IsNamespaceSensitive() => false;
    }
}
