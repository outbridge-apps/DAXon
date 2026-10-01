////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLItemType : StyleElement
    {
        private StructuredQName itemTypeName;
        private bool resolved = false;
        public override bool IsDeclaration()
        {
            return true;
        }

        public override void PrepareAttributes()
        {

            // Avoid reporting errors twice
            if (itemTypeName != null)
            {
                return;
            }

            string typeAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                switch (f)
                {
                    case "name":
                        itemTypeName = MakeQName(value, null, "name");
                        break;
                    case "as":
                        typeAtt = value;
                        break;
                    case "visibility":
                        if (!value.Equals("private"))
                        {
                            CompileErrorInAttribute("Not implemented", "XTSE0010", "visibility");
                        }

                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (itemTypeName == null)
            {
                ReportAbsence("name");
            }

            if (typeAtt == null)
            {
                ReportAbsence("as");
            }
        }

        public override StructuredQName GetObjectName()
        {
            if (itemTypeName == null)
            {
                PrepareAttributes();
            }

            return itemTypeName;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            CheckEmpty();
            CheckTopLevel("XTSE0010", false);
            GetConfiguration().CheckLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION, "saxon:item-type", GetPackageData().LocalLicenseId);
        }

        public ItemType TryToResolve()
        {
            IStaticContext env = new TypeAliasContext(this);
            resolved = true;
            ItemType type = MakeItemType(env);
            return resolved ? type : null;
        }

        private void MarkUnresolved()
        {
            resolved = false;
        }

        private ItemType MakeItemType(IStaticContext env)
        {
            try
            {
                XPathParser parser = GetConfiguration().NewExpressionParser("XP", false, env);
                QNameParser qp = new QNameParser(env.GetNamespaceResolver()).WithAcceptEQName(true).WithErrorOnBadSyntax("XPST0003").WithErrorOnUnresolvedPrefix("XPST0081");
                parser.SetQNameParser(qp);
                string typeAtt = GetAttributeValue("as");
                if (typeAtt == null)
                {
                    ReportAbsence("as");
                    typeAtt = "item()";
                }

                SequenceType st = parser.ParseExtendedSequenceType(typeAtt, env);
                if (st.GetCardinality() != StaticProperty.ALLOWS_ONE)
                {
                    CompileError("Item type must not include an occurrence indicator");
                }

                return st.PrimaryType;
            }
            catch (XPathException err)
            {
                CompileError(err);

                // recovery path after reporting an error, e.g. undeclared namespace prefix
                return AnyItemType.GetInstance();
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            return null;
        }

        private sealed class TypeAliasContext : ExpressionContext
        {
            public TypeAliasContext(XSLItemType declaration) : base(declaration, NamespaceUri.NULL.QName("as"))
            {
            }

            public override ItemType ResolveTypeAlias(StructuredQName typeName)
            {
                ItemType resolved = base.ResolveTypeAlias(typeName);
                if (resolved == null)
                {
                    ((XSLItemType)GetStyleElement()).MarkUnresolved();
                    return AnyItemType.GetInstance();
                }
                else
                {
                    return resolved;
                }
            }
        }
    }
}
