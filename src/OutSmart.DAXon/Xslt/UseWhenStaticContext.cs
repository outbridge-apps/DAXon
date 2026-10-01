////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class UseWhenStaticContext : AbstractStaticContext, IStaticContext
    {
        private readonly INamespaceResolver namespaceContext;
        private readonly IFunctionLibrary functionLibrary;
        private readonly Compilation compilation;
        public UseWhenStaticContext(Compilation compilation, INamespaceResolver namespaceContext)
        {
            Configuration config = compilation.GetConfiguration();
            SetConfiguration(config);
            this.compilation = compilation;
            SetPackageData(compilation.GetPackageData());
            this.namespaceContext = namespaceContext;
            int version = compilation.GetCompilerInfo().XsltVersion;
            SetXPathLanguageLevel(version == 40 ? 40 : 31);
            FunctionLibraryList lib = new FunctionLibraryList();
            lib.AddFunctionLibrary(GetConfiguration().GetUseWhenFunctionLibrary(version));
            lib.AddFunctionLibrary(GetConfiguration().GetBuiltInExtensionLibraryList(version));
            lib.AddFunctionLibrary(new ConstructorFunctionLibrary(GetConfiguration()));
            lib.AddFunctionLibrary(config.GetIntegratedFunctionLibrary());
            config.AddExtensionBinders(lib);
            functionLibrary = lib;
        }

        public override RetainedStaticContext MakeRetainedStaticContext()
        {
            return new RetainedStaticContext(this);
        }

        public override void IssueWarning(string s, string errorCode, ILocation locator)
        {
            compilation.GetCompilerInfo().ErrorReporter.Report(new XmlProcessingIncident(s, errorCode, locator).AsWarning());
        }

        public override string GetSystemId()
        {
            return StaticBaseURI;
        }

        public override Expression BindVariable(StructuredQName qName)
        {
            IGroundedValue val = compilation.GetStaticVariable(qName);
            if (val != null)
            {
                return Literal.MakeLiteral(val);
            }
            else
            {
                throw new XPathException("Variables (other than XSLT 3.0 static variables) cannot be used in a static expression: " + qName.DisplayName).WithErrorCode("XPST0008").AsStaticError();
            }
        }

        public override IFunctionLibrary GetFunctionLibrary()
        {
            return functionLibrary;
        }

        public override string GetDefaultCollationName()
        {
            return NamespaceConstant.CODEPOINT_COLLATION_URI;
        }

        /// <summary>
        /// Get the default function @namespace
        /// </summary>
        public override NamespaceUri GetDefaultFunctionNamespace()
        {
            return NamespaceUri.FN;
        }

        public override bool IsInBackwardsCompatibleMode()
        {
            return false;
        }

        public override bool IsImportedSchema(NamespaceUri @namespace)
        {
            return false;
        }

        public override HashSet<NamespaceUri> GetImportedSchemaNamespaces()
        {
            return new HashSet<NamespaceUri>();
        }

        public override INamespaceResolver GetNamespaceResolver()
        {
            return namespaceContext;
        }

        public override DecimalFormatManager GetDecimalFormatManager()
        {
            return null;
        }

        public override Types.ItemType ResolveTypeAlias(StructuredQName typeName)
        {
            return null;
        }
    }
}
