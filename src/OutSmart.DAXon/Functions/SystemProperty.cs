////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.functions.extfn.EXPathArchive.Archive;
//import com.saxonica.functions.extfn.EXPathBinaryFunctionSet;
//import com.saxonica.functions.extfn.EXPathFileFunctionSet;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implementation of the XSLT system-property() function
    /// </summary>
    internal sealed class SystemProperty : SystemFunction, ICallable
    {
        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            if (arguments[0] is StringLiteral && visitor.GetTargetEdition().Equals(visitor.GetConfiguration().EditionCode))
            {
                try
                {
                    string name = ((StringLiteral)arguments[0]).Stringify();
                    StructuredQName qName = StructuredQName.FromLexicalQName(name, false, true, GetRetainedStaticContext());
                    if (qName.HasURI(NamespaceUri.XSLT))
                    {
                        string local = qName.GetLocalPart();
                        if (local.Equals("version") || local.Equals("vendor") || local.Equals("vendor-url") || local.Equals("product-name") || local.Equals("product-version") || local.Equals("supports-backwards-compatibility") || local.Equals("xpath-version") || local.Equals("xsd-version"))
                        {
                            string result = GetProperty(NamespaceConstant.XSLT, local, GetRetainedStaticContext());
                            return new StringLiteral(result);
                        }
                    }
                }
                catch (XPathException)
                {
                }
            }

            return null;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments) /*Java covariant StringValue widened (C# 7.3)*/
        {
            string name = arguments[0].Head().GetStringValue();
            try
            {
                StructuredQName qName = StructuredQName.FromLexicalQName(name, false, true, GetRetainedStaticContext());
                return new StringValue(GetProperty(qName.GetNamespaceUri().ToString(), qName.GetLocalPart(), GetRetainedStaticContext()));
            }
            catch (XPathException err)
            {
                throw new XPathException("Invalid system property name. " + err.Message, "XTDE1390", context);
            }
        }

        public static string YesOrNo(bool whatever)
        {
            return whatever ? "yes" : "no";
        }

        public static string GetProperty(string uri, string local, RetainedStaticContext rsc)
        {
            Configuration config = rsc.GetConfiguration();
            string edition = rsc.GetPackageData().TargetEdition;
            if (uri.Equals(NamespaceConstant.XSLT))
            {
                switch (local)
                {
                    case "version":
                        return "3.0";
                    case "vendor":
                        return Core.Version.ProductVendor;
                    case "vendor-url":
                        return Core.Version.WebSiteAddress;
                    case "product-name":
                        return Core.Version.ProductName;
                    case "product-version":
                        return Core.Version.GetProductVariantAndVersion(edition);
                    case "is-schema-aware":
                        bool schemaAware = rsc.GetPackageData().IsSchemaAware();
                        return YesOrNo(schemaAware);
                    case "supports-serialization":
                        return "yes";
                    case "supports-backwards-compatibility":
                        return "yes";
                    case "supports-namespace-axis":
                        return "yes";
                    case "supports-streaming":
                        return YesOrNo(edition == "EE" && config.IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XSLT) && !config.GetConfigurationProperty(Feature<string>.STREAMABILITY).Equals("off"));
                    case "supports-dynamic-evaluation":
                        return YesOrNo(!config.GetBooleanProperty(Feature<bool>.DISABLE_XSL_EVALUATE));
                    case "supports-higher-order-functions":
                        return "yes";
                    case "xpath-version":
                        return "3.1";
                    case "xsd-version":
                        return rsc.GetConfiguration().XsdVersion == Configuration.XSD10 ? "1.0" : "1.1";
                }

                return "";
            }
            else if ((uri.Length == 0) && config.GetBooleanProperty(Feature<bool>.ALLOW_EXTERNAL_FUNCTIONS))
            {
                if (!OutSmart.DAXon.Internal.ResourceGate.PermitsEnvironment(config, local))
                {
                    return "";
                }

                string val = Environment.GetEnvironmentVariable(local);
                return val == null ? "" : val;
            }
            else
            {
                return "";
            }
        }
        ISequence ICallable.Call(IXPathContext arg0, ISequence[] arg1) => Call(arg0, arg1);
    }
}

