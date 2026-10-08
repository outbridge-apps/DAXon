////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:namespace-alias element in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLNamespaceAlias : StyleElement
    {
        private NamespaceUri stylesheetURI;
        private NamespaceBinding resultNamespaceBinding;

        public NamespaceUri StylesheetURI => stylesheetURI;

        public NamespaceBinding ResultNamespaceBinding => resultNamespaceBinding;
        public override bool IsDeclaration()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string stylesheetPrefix = null;
            string resultPrefix = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("stylesheet-prefix"))
                {
                    stylesheetPrefix = Whitespace.Trim(value);
                }
                else if (f.Equals("result-prefix"))
                {
                    resultPrefix = Whitespace.Trim(value);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (stylesheetPrefix == null)
            {
                ReportAbsence("stylesheet-prefix");
                stylesheetPrefix = "";
            }

            if (stylesheetPrefix.Equals("#default"))
            {
                stylesheetPrefix = "";
            }

            if (resultPrefix == null)
            {
                ReportAbsence("result-prefix");
                resultPrefix = "";
            }

            if (resultPrefix.Equals("#default"))
            {
                resultPrefix = "";
            }

            stylesheetURI = GetURIForPrefix(stylesheetPrefix, true);
            if (stylesheetURI == null)
            {
                CompileError("stylesheet-prefix " + stylesheetPrefix + " has not been declared", "XTSE0812");

                // recovery action
                stylesheetURI = NamespaceUri.NULL;
                resultNamespaceBinding = NamespaceBinding.DEFAULT_UNDECLARATION;
                return;
            }

            NamespaceUri resultURI = GetURIForPrefix(resultPrefix, true);
            if (resultURI == null)
            {
                CompileError("result-prefix " + resultPrefix + " has not been declared", "XTSE0812");

                // recovery action
                stylesheetURI = NamespaceUri.NULL;
                resultURI = NamespaceUri.NULL;
            }

            resultNamespaceBinding = new NamespaceBinding(resultPrefix, resultURI);
        }

        public override void Validate(ComponentDeclaration decl)
        {
            CheckTopLevel("XTSE0010", false);
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            return null;
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            top.AddNamespaceAlias(decl);
        }
    }
}