////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Types
{
    public interface ISchemaType : ISchemaComponent
    {
        string Name { get; }
        NamespaceUri TargetNamespace { get; }
        int Fingerprint { get; }
        string DisplayName { get; }
        StructuredQName GetStructuredQName();
        string EQName { get; }
        bool IsComplexType();
        bool IsSimpleType();
        bool IsAtomicType();
        bool IsAnonymousType();
        int GetBlock();
        ISchemaType BaseType { get; }
        int DerivationMethod { get; }
        int FinalProhibitions { get; }
        bool AllowsDerivation(int derivation);
        void AnalyzeContentExpression(Expression expression, int kind);
        IAtomicSequence Atomize(NodeInfo node);
        bool IsSameType(ISchemaType other);
        string Description { get; }
        void CheckTypeDerivationIsOK(ISchemaType @base, int block);
        string GetSystemId();
        bool IsIdType();
        bool IsIdRefType();
    }
}