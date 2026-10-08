////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;

namespace OutSmart.DAXon.Types
{
    // Stubs for additional excluded Type classes referenced by 40+ files each.
    // ISchemaTypeStubBase — minimal stub implementation of
    // ISchemaType so Untyped/AnyType/AnySimpleType (Java enum-singletons that
    // can't be C# enums implementing interfaces) can satisfy CS0029/CS1503
    // conversions to ISchemaType.
    internal abstract class ISchemaTypeStubBase : ISchemaType
    {
        // The three subclasses (AnySimpleType / AnyType / Untyped) each override the identity
        // members with their fixed XSD-singleton answers — abstract makes that a compile-time
        // requirement instead of a throwing fallback.
        public abstract string Name { get; }
        public abstract NamespaceUri TargetNamespace { get; }
        public virtual int Fingerprint => -1;
        public abstract string DisplayName { get; }
        public abstract string EQName { get; }
        public abstract ISchemaType BaseType { get; }
        public virtual int DerivationMethod => 0;
        public virtual int FinalProhibitions => 0;
        public virtual string Description => GetType().Name;
        public virtual SchemaValidationStatus ValidationStatus => SchemaValidationStatus.VALIDATED;
        public virtual int RedefinitionLevel => 0;
        public virtual StructuredQName GetStructuredQName() => new StructuredQName("xs", NamespaceUri.SCHEMA, Name);
        public virtual bool IsComplexType() => false;
        public virtual bool IsSimpleType() => false;
        public virtual bool IsAtomicType() => false;
        public virtual bool IsAnonymousType() => false;
        public virtual int GetBlock() => 0;
        public virtual bool AllowsDerivation(int derivation) => true;
        public virtual void AnalyzeContentExpression(Expression expression, int kind) { }
        // Upstream AnySimpleType/AnyType/Untyped (the three subclasses) all atomize a node the same
        // way: to untypedAtomic over the string value.
        public virtual IAtomicSequence Atomize(NodeInfo node) => OutSmart.DAXon.Values.StringValue.MakeUntypedAtomic(node.UnicodeStringValue);
        public virtual bool IsSameType(ISchemaType other) => this == other;
        public virtual void CheckTypeDerivationIsOK(ISchemaType @base, int block) { }
        public virtual string GetSystemId() => null; // built-in types come from no source document
        public virtual bool IsIdType() => false;
        public virtual bool IsIdRefType() => false;
    }
}
