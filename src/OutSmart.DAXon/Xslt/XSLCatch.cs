////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLCatch : StyleElement
    {
        private Expression select;
        private IQNameTest nameTest;
        public override bool IsInstruction()
        {
            return false;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        protected override bool SeesAvuncularVariables()
        {
            return false;
        }

        public override void PrepareAttributes()
        {
            string errorAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("select"))
                {
                    string selectAtt = value;
                    select = MakeExpression(selectAtt, att);
                }
                else if (f.Equals("errors"))
                {
                    errorAtt = value;
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (errorAtt == null)
            {

                // default is "catch all errors"
                nameTest = AnyNodeTest.GetInstance(); // for error recovery
            }
            else
            {
                IList<IQNameTest> tests = ParseNameTests(errorAtt);
                if (tests.Count == 0)
                {
                    CompileError("xsl:catch/@errors must not be empty");
                }

                if (tests.Count == 1)
                {
                    nameTest = tests[0];
                }
                else
                {
                    nameTest = (IQNameTest)new UnionQNameTest(tests);
                }
            }
        }

        private IList<IQNameTest> ParseNameTests(string elements)
        {
            IList<IQNameTest> result = new List<IQNameTest>();
            foreach (string s in elements.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                IQNameTest nt;
                if (s.Equals("*"))
                {
                    nt = AnyNodeTest.GetInstance();
                    result.Add(nt);
                }
                else if (s.EndsWith(":*", StringComparison.Ordinal))
                {
                    if (s.Length == 2)
                    {
                        CompileError("No prefix before ':*'");
                        result.Add(AnyNodeTest.GetInstance());
                    }

                    string prefix = s.Substring(0, s.Length - 2);
                    NamespaceUri uri = GetURIForPrefix(prefix, false);
                    nt = new NamespaceTest(GetNamePool(), Types.Type.ELEMENT, uri);
                    result.Add(nt);
                }
                else if (s.StartsWith("*:", StringComparison.Ordinal))
                {
                    if (s.Length == 2)
                    {
                        CompileErrorInAttribute("No local name after '*:'", "XTSE0010", "errors");
                        result.Add(AnyNodeTest.GetInstance());
                    }

                    string localname = s.Substring(2);
                    nt = new LocalNameTest(GetNamePool(), Types.Type.ELEMENT, localname);
                    result.Add(nt);
                }
                else if (s.StartsWith("Q{", StringComparison.Ordinal))
                {
                    int brace = s.IndexOf('}');
                    if (brace < 0)
                    {
                        CompileErrorInAttribute("No closing '}' in EQName", "XTSE0010", "errors");
                    }
                    else if (brace == s.Length - 1)
                    {
                        CompileErrorInAttribute("Missing local part in EQName", "XTSE0010", "errors");
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

                        result.Add(nt);
                    }
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
                            uri = NamespaceUri.NULL;
                        }
                        else
                        {
                            uri = GetURIForPrefix(prefix, false);
                            if (uri == null)
                            {
                                UndeclaredNamespaceError(prefix, "XTSE0280", "errors");
                                result.Add(AnyNodeTest.GetInstance());
                                break;
                            }
                        }

                        localName = parts[1];
                    }
                    catch (QNameException)
                    {
                        CompileErrorInAttribute("Error code " + s + " is not a valid QName", "XTSE0280", "errors");
                        result.Add(AnyNodeTest.GetInstance());
                        break;
                    }

                    NamePool target = GetNamePool();
                    int nameCode = target.AllocateFingerprint(uri, localName);
                    nt = new NameTest(Types.Type.ELEMENT, nameCode, GetNamePool());
                    result.Add(nt);
                }
            }

            return result;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            select = TypeCheck("select", select);
            if (select != null && HasChildNodes())
            {
                CompileError("An xsl:catch element with a select attribute must be empty", "XTSE3150");
            }

            if (!(GetParent() is XSLTry))
            {
                CompileError("xsl:catch may appear only as a child of xsl:try");
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (select == null)
            {
                select = CompileSequenceConstructor(exec, decl, true);
            }

            ((XSLTry)GetParent()).AddCatchClause(nameTest, select);
            return null;
        }
    }
}