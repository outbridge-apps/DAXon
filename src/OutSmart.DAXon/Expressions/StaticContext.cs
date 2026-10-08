////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions
{
    public interface IStaticContext
    {
        Configuration GetConfiguration();
        PackageData GetPackageData();
        IXPathContext MakeEarlyEvaluationContext();
        RetainedStaticContext MakeRetainedStaticContext();
        ILocation GetContainingLocation();
        void IssueWarning(string message, string errorCode, ILocation locator);
        string GetSystemId();
        string StaticBaseURI { get; }
        Expression BindVariable(StructuredQName qName);
        IFunctionLibrary GetFunctionLibrary();
        string GetDefaultCollationName();
        NamespaceUri GetDefaultElementNamespace();
        UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy();



        NamespaceUri GetDefaultFunctionNamespace();
        bool IsInBackwardsCompatibleMode();
        bool IsImportedSchema(NamespaceUri @namespace);
        HashSet<NamespaceUri> GetImportedSchemaNamespaces();
        INamespaceResolver GetNamespaceResolver();
        Types.ItemType GetRequiredContextItemType();
        DecimalFormatManager GetDecimalFormatManager();
        int GetXPathVersion();
        KeyManager GetKeyManager();
        Types.ItemType ResolveTypeAlias(StructuredQName typeName);
        OptimizerOptions GetOptimizerOptions();


    }
}
