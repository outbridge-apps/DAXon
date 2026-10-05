////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:accept and xsl:expose elements in stylesheet.
    /// </summary>
    public abstract class XSLAcceptExpose : StyleElement
    {
        private readonly HashSet<ComponentTest> explicitComponentTests = new HashSet<ComponentTest>();
        private readonly HashSet<ComponentTest> wildcardComponentTests = new HashSet<ComponentTest>();
        private Visibility visibility = Visibility.UNDEFINED;

        public virtual HashSet<ComponentTest> ExplicitComponentTests
        {
            get
            {
                PrepareAttributes();
                return explicitComponentTests;
            }
        }

        public virtual HashSet<ComponentTest> WildcardComponentTests
        {
            get
            {
                PrepareAttributes();
                return wildcardComponentTests;
            }
        }
        public override Visibility GetVisibility()
        {
            return visibility;
        }

        public override void PrepareAttributes()
        {
            if (visibility != Visibility.UNDEFINED)
            {
                return;
            }

            string componentAtt = null;
            string namesAtt = null;
            string visibilityAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "names":
                        namesAtt = Whitespace.Trim(value);
                        break;
                    case "component":
                        componentAtt = Whitespace.Trim(value);
                        break;
                    case "visibility":
                        visibilityAtt = Whitespace.Trim(value);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (visibilityAtt == null)
            {
                ReportAbsence("visibility");
                visibility = Visibility.PRIVATE;
            }
            else
            {
                visibility = InterpretVisibilityValue(visibilityAtt, this is XSLAccept ? "ha" : "");
                if (visibility == Visibility.UNDEFINED)
                {
                    visibility = Visibility.PRIVATE; // fall back in case of errors
                }
            }

            int componentTypeCode = StandardNames.XSL_FUNCTION;
            if (componentAtt == null)
            {
                ReportAbsence("component");
            }
            else
            {
                string local = Whitespace.Trim(componentAtt);
                switch (local)
                {
                    case "function":
                        componentTypeCode = StandardNames.XSL_FUNCTION;
                        break;
                    case "template":
                        componentTypeCode = StandardNames.XSL_TEMPLATE;
                        break;
                    case "variable":
                        componentTypeCode = StandardNames.XSL_VARIABLE;
                        break;
                    case "attribute-set":
                        componentTypeCode = StandardNames.XSL_ATTRIBUTE_SET;
                        break;
                    case "mode":
                        componentTypeCode = StandardNames.XSL_MODE;
                        break;
                    case "*":

                        // spec change bug 29478
                        componentTypeCode = -1;
                        break;
                    default:
                        CompileError("The component type is not one of the allowed names (function, template, variable, attribute-set, or mode)", "XTSE0020");
                        return;
                }
            }

            if (namesAtt == null)
            {
                ReportAbsence("names");
                namesAtt = "";
            }

            foreach (string tok in namesAtt.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                IQNameTest test;
                int hash = tok.LastIndexOf('#');
                if (hash > 0 && tok.IndexOf('}', hash) < 0)
                {

                    // ignore any '#' within a namespace URI of an EQName
                    if (componentTypeCode == -1)
                    {
                        CompileErrorInAttribute("When component='*' is specified, all names must be wildcards", this is XSLAccept ? "XTSE3032" : "XTSE3022", "names");
                    }
                    else if (componentTypeCode == StandardNames.XSL_FUNCTION)
                    {
                        StructuredQName name = MakeQName(tok.Substring(0, hash), null, "names");
                        test = new NameTest(Types.Type.ELEMENT, name.GetNamespaceUri(), name.GetLocalPart(), GetNamePool());
                        int arity = 0;
                        try
                        {
                            arity = int.Parse(tok.Substring(hash + 1), CultureInfo.InvariantCulture);
                        }
                        catch (Exception err) when (err is FormatException || err is OverflowException)
                        {
                            CompileErrorInAttribute("Malformed function arity in '" + tok + "'", "XTSE0020", "names");
                        }

                        explicitComponentTests.Add(new ComponentTest(componentTypeCode, test, arity));
                    }
                    else
                    {
                        CompileErrorInAttribute("Cannot specify arity for components other than functions", "XTSE3020", "names");
                    }
                }
                else if (tok.Equals("*"))
                {
                    test = AnyNodeTest.GetInstance();
                    AddWildCardTest(componentTypeCode, test);
                }
                else if (tok.EndsWith(":*", StringComparison.Ordinal))
                {
                    if (tok.Length == 2)
                    {
                        CompileErrorInAttribute("No prefix before ':*'", "XTSE0020", "names");
                    }

                    string prefix = tok.Substring(0, tok.Length - 2);
                    NamespaceUri uri = GetURIForPrefix(prefix, false);
                    if (uri == null)
                    {
                        CompileErrorInAttribute("Undeclared prefix " + prefix, "XTSE0020", "names");
                        uri = NamespaceUri.ANONYMOUS; // for recovery
                    }

                    test = new NamespaceTest(GetNamePool(), Types.Type.ELEMENT, uri);
                    AddWildCardTest(componentTypeCode, test);
                }
                else if (tok.StartsWith("Q{", StringComparison.Ordinal) && tok.EndsWith("}*", StringComparison.Ordinal))
                {
                    string uri = tok.Substring(2, tok.Length - 4) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                    test = new NamespaceTest(GetNamePool(), Types.Type.ELEMENT, NamespaceUri.Of(uri));
                    wildcardComponentTests.Add(new ComponentTest(componentTypeCode, test, -1));
                }
                else if (tok.StartsWith("*:", StringComparison.Ordinal))
                {
                    if (tok.Length == 2)
                    {
                        CompileErrorInAttribute("No local name after '*:'", "XTSE0020", "names");
                    }

                    string localname = tok.Substring(2);
                    test = new LocalNameTest(GetNamePool(), Types.Type.ELEMENT, localname);
                    AddWildCardTest(componentTypeCode, test);
                }
                else
                {
                    if (componentTypeCode == -1)
                    {
                        CompileErrorInAttribute("When component='*' is specified, all names must be wildcards", this is XSLAccept ? "XTSE3032" : "XTSE3022", "names");
                    }
                    else if (componentTypeCode == StandardNames.XSL_FUNCTION)
                    {
                        CompileErrorInAttribute("The name " + tok + " identifies a function, so the arity must be given (XSLT 3.0 erratum E36)", "XTSE3020", "names");
                    }
                    else
                    {
                        StructuredQName name = MakeQName(tok, null, "names");
                        test = new NameTest(Types.Type.ELEMENT, name.GetNamespaceUri(), name.GetLocalPart(), GetNamePool());
                        explicitComponentTests.Add(new ComponentTest(componentTypeCode, test, -1));
                    }
                }
            }
        }

        private void AddWildCardTest(int componentTypeCode, IQNameTest test)
        {
            if (componentTypeCode == -1)
            {
                wildcardComponentTests.Add(new ComponentTest(StandardNames.XSL_FUNCTION, test, -1));
                wildcardComponentTests.Add(new ComponentTest(StandardNames.XSL_TEMPLATE, test, -1));
                wildcardComponentTests.Add(new ComponentTest(StandardNames.XSL_VARIABLE, test, -1));
                wildcardComponentTests.Add(new ComponentTest(StandardNames.XSL_ATTRIBUTE_SET, test, -1));
                wildcardComponentTests.Add(new ComponentTest(StandardNames.XSL_MODE, test, -1));
            }
            else
            {
                wildcardComponentTests.Add(new ComponentTest(componentTypeCode, test, -1));
            }
        }
    }
}