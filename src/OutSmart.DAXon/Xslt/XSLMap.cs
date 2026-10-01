////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:map instructions in an XSLT 3.0 stylesheet. <br>
    /// </summary>
    internal sealed class XSLMap : StyleElement
    {
        private Expression select = null;
        private Expression onDuplicates = null;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (attName.GetLocalPart().Equals("on-duplicates"))
                {
                    if (attName.GetNamespaceUri().IsEmpty())
                    {
                        if (RequireXslt40Attribute("on-duplicates"))
                        {
                            onDuplicates = MakeExpression(value, att);
                        }
                    }
                    else if (attName.HasURI(NamespaceUri.SAXON))
                    {
                        if (GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
                        {
                            onDuplicates = MakeExpression(value, att);
                        }
                        else
                        {
                            IssueWarning("saxon:on-duplicates ignored - requires Saxon-PE license", DAXonErrorCode.SXWN9013);
                        }
                    }
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            select = CompileSequenceConstructor(exec, decl, false);
            select = select.Simplify();

            // Custom type-checking; the checking performed by map:merge() gives poor diagnostics
            TypeChecker tc = GetConfiguration().GetTypeChecker(false);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.MISC, "xsl:map sequence constructor", 0, "XTTE3375");
            select = tc.StaticTypeCheck(select, SequenceType.MakeSequenceType(MapType.ANY_MAP_TYPE, StaticProperty.ALLOWS_ZERO_OR_MORE), role, MakeExpressionVisitor());
            Expression optionsExp;
            if (onDuplicates != null)
            {
                optionsExp = MapFunctionSet.GetInstance(31).MakeFunction("entry", 2).MakeFunctionCall(Literal.MakeLiteral(new QNameValue("", NamespaceUri.SAXON, "on-duplicates")), onDuplicates);
            }
            else
            {
                HashTrieMap options = new HashTrieMap();
                options.InitialPut(StringValue.Bmp("duplicates"), StringValue.Bmp("reject"));
                options.InitialPut(new QNameValue("", NamespaceUri.SAXON, "duplicates-error-code"), StringValue.Bmp("XTDE3365"));
                optionsExp = Literal.MakeLiteral(options, select);
            }

            Expression exp = MapFunctionSet.GetInstance(31).MakeFunction("merge", 2).MakeFunctionCall(select, optionsExp);
            if (GetConfiguration().GetBooleanProperty(Feature<bool>.STRICT_STREAMABILITY))
            {
                exp = new SequenceInstr(exp);
            }

            return exp;
        }
    }
}