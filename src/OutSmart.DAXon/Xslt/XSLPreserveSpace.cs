////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Transformation;
using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;

namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:preserve-space or xsl:strip-space elements in stylesheet. <br>
    /// </summary>
    internal sealed class XSLPreserveSpace : StyleElement
    {
        private string elements;
        public override bool IsDeclaration()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                if (f.Equals("elements"))
                {
                    elements = att.Value;
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (elements == null)
            {
                ReportAbsence("elements");
                elements = "*"; // for error recovery
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            CheckEmpty();
            CheckTopLevel("XTSE0010", false);
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            if (Fingerprint == StandardNames.XSL_STRIP_SPACE)
            {
                if (Fingerprint == StandardNames.XSL_STRIP_SPACE)
                {
                    string elements = GetAttributeValue(NamespaceUri.NULL, "elements");
                    if (elements != null && !(elements.Trim().Length == 0))
                    {
                        top.GetStylesheetPackage().SetStripsWhitespace(true);
                    }
                }
            }
        }

        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
            Stripper.StripRuleTarget preserve = Fingerprint == StandardNames.XSL_PRESERVE_SPACE ? Stripper.PRESERVE : Stripper.STRIP;
            PrincipalStylesheetModule psm = GetCompilation().GetPrincipalStylesheetModule();
            ISpaceStrippingRule stripperRules = psm.GetStylesheetPackage().StripperRules;
            if (!(stripperRules is SelectedElementsSpaceStrippingRule))
            {
                stripperRules = new SelectedElementsSpaceStrippingRule(true);
                psm.GetStylesheetPackage().StripperRules = stripperRules;
            }

            SelectedElementsSpaceStrippingRule rules = (SelectedElementsSpaceStrippingRule)stripperRules;

            // elements is a space-separated list of element names or name tests
            try
            {
                foreach (string s in elements.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    NodeTest nt;
                    if (s.Equals("*"))
                    {
                        nt = NodeKindTest.ELEMENT;
                        rules.AddRule(nt, preserve, decl.Module, decl.SourceElement.GetLineNumber());
                    }
                    else if (s.StartsWith("Q{", StringComparison.Ordinal))
                    {
                        int brace = s.IndexOf('}');
                        if (brace < 0)
                        {
                            CompileError("No closing '}' in EQName");
                        }
                        else if (brace == s.Length - 1)
                        {
                            CompileError("Missing local part in EQName");
                        }
                        else
                        {
                            NamespaceUri uri = NamespaceUri.Of(s.Substring(2, brace - 2) /*Java substring(begin,END) -> C# (start,LENGTH)*/);
                            string local = s.Substring(brace + 1);
                            if (local.Equals("*"))
                            {
                                nt = new NamespaceTest(GetNamePool(), Types.Type.ELEMENT, uri);
                            }
                            else
                            {
                                nt = new NameTest(Types.Type.ELEMENT, uri, local, GetNamePool());
                            }

                            rules.AddRule(nt, preserve, decl.Module, decl.SourceElement.GetLineNumber());
                        }
                    }
                    else if (s.EndsWith(":*", StringComparison.Ordinal))
                    {
                        if (s.Length == 2)
                        {
                            CompileError("No prefix before ':*'");
                        }

                        string prefix = s.Substring(0, s.Length - 2);
                        NamespaceUri uri = GetURIForPrefix(prefix, false);
                        if (uri == null)
                        {
                            UndeclaredNamespaceError(prefix, "XTSE0280", "elements");
                        }

                        nt = new NamespaceTest(GetNamePool(), Types.Type.ELEMENT, uri);
                        rules.AddRule(nt, preserve, decl.Module, decl.SourceElement.GetLineNumber());
                    }
                    else if (s.StartsWith("*:", StringComparison.Ordinal))
                    {
                        if (s.Length == 2)
                        {
                            CompileErrorInAttribute("No local name after '*:'", "XTSE0010", "elements");
                        }

                        string localname = s.Substring(2);
                        nt = new LocalNameTest(GetNamePool(), Types.Type.ELEMENT, localname);
                        rules.AddRule(nt, preserve, decl.Module, decl.SourceElement.GetLineNumber());
                    }
                    else
                    {
                        string prefix;
                        string localName;
                        NamespaceUri uri;
                        try
                        {
                            string[] parts = NameChecker.GetQNameParts(s);
                            prefix = parts[0];
                            if (parts[0].Equals(""))
                            {
                                uri = DefaultXPathNamespace;
                            }
                            else
                            {
                                uri = GetURIForPrefix(prefix, false);
                                if (uri == null)
                                {
                                    UndeclaredNamespaceError(prefix, "XTSE0280", "elements");
                                }
                            }

                            localName = parts[1];
                        }
                        catch (QNameException err)
                        {
                            CompileError("Element name " + s + " is not a valid QName", "XTSE0280");
                            return;
                        }

                        NamePool target = GetNamePool();
                        int nameCode = target.AllocateFingerprint(uri, localName);
                        nt = new NameTest(Types.Type.ELEMENT, nameCode, GetNamePool());
                        rules.AddRule(nt, preserve, decl.Module, decl.SourceElement.GetLineNumber());
                    }
                }
            }
            catch (XPathException e)
            {
                CompileError(e.MaybeWithLocation(AllocateLocation()));
            }
        }
    }
}