////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    internal sealed class ElementAvailable : SystemFunction
    {
        public static bool IsXslt30Element(int fp)
        {
            switch (fp)
            {
                case StandardNames.XSL_ACCEPT:
                case StandardNames.XSL_ACCUMULATOR:
                case StandardNames.XSL_ACCUMULATOR_RULE:
                case StandardNames.XSL_ANALYZE_STRING:
                case StandardNames.XSL_APPLY_IMPORTS:
                case StandardNames.XSL_APPLY_TEMPLATES:
                case StandardNames.XSL_ASSERT:
                case StandardNames.XSL_ATTRIBUTE:
                case StandardNames.XSL_ATTRIBUTE_SET:
                case StandardNames.XSL_BREAK:
                case StandardNames.XSL_CALL_TEMPLATE:
                case StandardNames.XSL_CATCH:
                case StandardNames.XSL_CHARACTER_MAP:
                case StandardNames.XSL_CHOOSE:
                case StandardNames.XSL_COMMENT:
                case StandardNames.XSL_CONTEXT_ITEM:
                case StandardNames.XSL_COPY:
                case StandardNames.XSL_COPY_OF:
                case StandardNames.XSL_DECIMAL_FORMAT:
                case StandardNames.XSL_DOCUMENT:
                case StandardNames.XSL_ELEMENT:
                case StandardNames.XSL_EVALUATE:
                case StandardNames.XSL_EXPOSE:
                case StandardNames.XSL_FALLBACK:
                case StandardNames.XSL_FOR_EACH:
                case StandardNames.XSL_FOR_EACH_GROUP:
                case StandardNames.XSL_FORK:
                case StandardNames.XSL_FUNCTION:
                case StandardNames.XSL_GLOBAL_CONTEXT_ITEM:
                case StandardNames.XSL_IF:
                case StandardNames.XSL_IMPORT:
                case StandardNames.XSL_IMPORT_SCHEMA:
                case StandardNames.XSL_INCLUDE:
                case StandardNames.XSL_ITEM_TYPE:
                case StandardNames.XSL_ITERATE:
                case StandardNames.XSL_KEY:
                case StandardNames.XSL_MAP:
                case StandardNames.XSL_MAP_ENTRY:
                case StandardNames.XSL_MATCHING_SUBSTRING:
                case StandardNames.XSL_MERGE:
                case StandardNames.XSL_MERGE_ACTION:
                case StandardNames.XSL_MERGE_KEY:
                case StandardNames.XSL_MERGE_SOURCE:
                case StandardNames.XSL_MESSAGE:
                case StandardNames.XSL_MODE:
                case StandardNames.XSL_NAMESPACE:
                case StandardNames.XSL_NAMESPACE_ALIAS:
                case StandardNames.XSL_NEXT_ITERATION:
                case StandardNames.XSL_NEXT_MATCH:
                case StandardNames.XSL_NON_MATCHING_SUBSTRING:
                case StandardNames.XSL_NUMBER:
                case StandardNames.XSL_ON_COMPLETION:
                case StandardNames.XSL_ON_EMPTY:
                case StandardNames.XSL_ON_NON_EMPTY:
                case StandardNames.XSL_OTHERWISE:
                case StandardNames.XSL_OUTPUT:
                case StandardNames.XSL_OUTPUT_CHARACTER:
                case StandardNames.XSL_OVERRIDE:
                case StandardNames.XSL_PACKAGE:
                case StandardNames.XSL_PARAM:
                case StandardNames.XSL_PERFORM_SORT:
                case StandardNames.XSL_PRESERVE_SPACE:
                case StandardNames.XSL_PROCESSING_INSTRUCTION:
                case StandardNames.XSL_RESULT_DOCUMENT:
                case StandardNames.XSL_SEQUENCE:
                case StandardNames.XSL_SORT:
                case StandardNames.XSL_SOURCE_DOCUMENT:
                case StandardNames.XSL_STRIP_SPACE:
                case StandardNames.XSL_STYLESHEET:
                case StandardNames.XSL_SWITCH:
                case StandardNames.XSL_TEMPLATE:
                case StandardNames.XSL_TEXT:
                case StandardNames.XSL_TRANSFORM:
                case StandardNames.XSL_TRY:
                case StandardNames.XSL_USE_PACKAGE:
                case StandardNames.XSL_VALUE_OF:
                case StandardNames.XSL_VARIABLE:
                case StandardNames.XSL_WHEN:
                case StandardNames.XSL_WHERE_POPULATED:
                case StandardNames.XSL_WITH_PARAM:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsXslt40Element(int fp)
        {
            return IsXslt30Element(fp) || fp == StandardNames.XSL_ARRAY || fp == StandardNames.XSL_ARRAY_MEMBER;
        }

        public override int GetSpecialProperties(Expression[] arguments)
        {
            try
            {
                if (arguments[0] is StringLiteral)
                {
                    string arg = ((StringLiteral)arguments[0]).Stringify();
                    StructuredQName elem = GetElementName(arg);
                    if (elem.HasURI(NamespaceUri.XSLT) && elem.GetLocalPart().Equals("evaluate"))
                    {
                        return base.GetSpecialProperties(arguments) | StaticProperty.DEPENDS_ON_RUNTIME_ENVIRONMENT;
                    }
                }
            }
            catch (XPathException e)
            {
            }

            return base.GetSpecialProperties(arguments);
        }

        private bool IsElementAvailable(string lexicalName, string targetEdition, IXPathContext context)
        {
            StructuredQName qName = GetElementName(lexicalName);
            if (qName.HasURI(NamespaceUri.XSLT))
            {
                int fp = context.GetConfiguration().GetNamePool().GetFingerprint(NamespaceUri.XSLT, qName.GetLocalPart());
                int xsltVersion = GetRetainedStaticContext().GetPackageData().HostLanguageVersion;
                if (fp == StandardNames.XSL_EVALUATE)
                {
                    return !context.GetConfiguration().GetBooleanProperty(Feature<bool>.DISABLE_XSL_EVALUATE);
                }

                if (fp == StandardNames.XSL_IMPORT_SCHEMA)
                {
                    return context.GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.SCHEMA_VALIDATION) && targetEdition.Equals("EE");
                }

                return xsltVersion == 40 ? IsXslt40Element(fp) : IsXslt30Element(fp);
            }
            else if (qName.HasURI(NamespaceUri.IXSL) && !targetEdition.Equals("JS"))
            {
                return false;
            }

            return context.GetConfiguration().IsExtensionElementAvailable(qName);
        }

        private StructuredQName GetElementName(string lexicalName)
        {
            try
            {
                if (NameChecker.IsValidNCName(StringTool.CodePoints(lexicalName)))
                {
                    NamespaceUri uri = GetRetainedStaticContext().GetURIForPrefix("", true);
                    return new StructuredQName("", uri, lexicalName);
                }
                else
                {
                    return StructuredQName.FromLexicalQName(lexicalName, false, true, GetRetainedStaticContext());
                }
            }
            catch (XPathException e)
            {
                XPathException err = new XPathException("Invalid element name passed to element-available(): " + e.Message);
                err.SetErrorCode("XTDE1440");
                throw err;
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            string lexicalQName = arguments[0].Head().GetStringValue();
            bool b = IsElementAvailable(lexicalQName, GetRetainedStaticContext().GetPackageData().TargetEdition, context);
            return BooleanValue.Get(b);
        }
    }
}
