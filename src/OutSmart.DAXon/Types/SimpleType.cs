////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
namespace OutSmart.DAXon.Types
{
    public interface ISimpleType : ISchemaType, IHyperType
    {
        bool IsAtomicType();
        bool IsListType();
        bool IsUnionType();
        bool IsBuiltInType();
        ISchemaType BuiltInBaseType { get; }
        IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules);
        ValidationFailure ValidateContent(UnicodeString value, INamespaceResolver nsResolver, ConversionRules rules);
        int WhitespaceAction { get; }
        UnicodeString Preprocess(UnicodeString input);
        UnicodeString Postprocess(UnicodeString input);
    }
}