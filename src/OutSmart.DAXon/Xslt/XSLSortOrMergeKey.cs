////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using System;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Xslt
{
    internal abstract class XSLSortOrMergeKey : StyleElement
    {
        protected SortKeyDefinition sortKeyDefinition;
        protected Expression select;
        protected Expression order;
        protected Expression dataType;
        protected Expression caseOrder;
        protected Expression lang;
        protected Expression collationName;
        protected Expression stable;
        protected bool useDefaultCollation = true;

        protected internal virtual Expression Stable => stable;
        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        protected virtual string GetErrorCode()
        {
            return "XTSE1015";
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (select != null && HasChildNodes())
            {
                CompileError("An " + DisplayName + " element with a select attribute must be empty", GetErrorCode());
            }

            if (select == null && !HasChildNodes())
            {
                select = new ContextItemExpression();
                select.SetRetainedStaticContext(GetStaticContext().MakeRetainedStaticContext());
            }


            // Get the named or default collation
            if (useDefaultCollation)
            {
                collationName = new StringLiteral(GetDefaultCollationName());
            }

            IStringCollator stringCollator = null;
            if (collationName is StringLiteral)
            {
                string collationString = ((StringLiteral)collationName).Stringify();
                try
                {
                    URI collationURI = new URI(collationString);
                    if (!collationURI.IsAbsolute())
                    {
                        URI @base = new URI(GetBaseURI());
                        collationURI = @base.Resolve(collationURI);
                        collationString = collationURI.ToString();
                    }
                }
                catch (URISyntaxException)
                {
                    CompileError("Collation name '" + collationString + "' is not a valid URI");
                    collationString = NamespaceConstant.CODEPOINT_COLLATION_URI;
                }

                try
                {
                    stringCollator = FindCollation(collationString, GetBaseURI());
                }
                catch (XPathException err)
                {
                    CompileError("Failed to load collation " + collationString + ": " + err.Message, "XTDE1035");
                    stringCollator = CodepointCollator.GetInstance(); // for recovery paths
                }

                if (stringCollator == null)
                {
                    CompileError("Collation " + collationString + " has not been defined", "XTDE1035");
                    stringCollator = CodepointCollator.GetInstance(); // for recovery paths
                }
            }

            select = TypeCheck("select", select);
            order = TypeCheck("order", order);
            caseOrder = TypeCheck("case-order", caseOrder);
            lang = TypeCheck("lang", lang);
            dataType = TypeCheck("data-type", dataType);
            collationName = TypeCheck("collation", collationName);
            if (select != null)
            {
                try
                {
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, DisplayName + "//select", 0);
                    select = GetConfiguration().GetTypeChecker(false).StaticTypeCheck(select, SequenceType.ATOMIC_SEQUENCE, role, MakeExpressionVisitor());
                }
                catch (XPathException err)
                {
                    CompileError(err);
                }
            }

            sortKeyDefinition = new SortKeyDefinition();
            sortKeyDefinition.Order = order;
            sortKeyDefinition.CaseOrder = caseOrder;
            sortKeyDefinition.Language = lang;
            sortKeyDefinition.SetSortKey(select, true);
            sortKeyDefinition.DataTypeExpression = dataType;
            sortKeyDefinition.CollationNameExpression = collationName;
            sortKeyDefinition.Collation = stringCollator;
            sortKeyDefinition.BaseURI = GetBaseURI();
            sortKeyDefinition.Stable = stable;
            sortKeyDefinition.SetBackwardsCompatible(XPath10ModeIsEnabled());
        }

        public override void PrepareAttributes()
        {
            string selectAtt = null;
            string orderAtt = null;
            string dataTypeAtt = null;
            string caseOrderAtt = null;
            string langAtt = null;
            string collationAtt = null;
            string stableAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "select":
                        selectAtt = value;
                        select = MakeExpression(selectAtt, att);
                        break;
                    case "order":
                        orderAtt = Whitespace.Trim(value);
                        order = MakeAttributeValueTemplate(orderAtt, att);
                        break;
                    case "data-type":
                        dataTypeAtt = Whitespace.Trim(value);
                        dataType = MakeAttributeValueTemplate(dataTypeAtt, att);
                        break;
                    case "case-order":
                        caseOrderAtt = Whitespace.Trim(value);
                        caseOrder = MakeAttributeValueTemplate(caseOrderAtt, att);
                        break;
                    case "lang":
                        langAtt = Whitespace.Trim(value);
                        lang = MakeAttributeValueTemplate(langAtt, att);
                        break;
                    case "collation":
                        collationAtt = Whitespace.Trim(value);
                        collationName = MakeAttributeValueTemplate(collationAtt, att);
                        break;
                    case "stable":
                        stableAtt = Whitespace.Trim(value);
                        stable = MakeAttributeValueTemplate(stableAtt, att);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (orderAtt == null)
            {
                order = new StringLiteral("ascending");
            }
            else
            {
                CheckAttributeValue("order", orderAtt, true, new string[] { "ascending", "descending" });
            }

            if (dataTypeAtt == null)
            {
                dataType = null;
            }

            if (caseOrderAtt == null)
            {
                caseOrder = new StringLiteral("#default");
            }
            else
            {
                CheckAttributeValue("case-order", caseOrderAtt, true, new string[] { "lower-first", "upper-first" });
                useDefaultCollation = false;
            }

            if (langAtt == null || langAtt.Equals(""))
            {
                lang = new StringLiteral(StringValue.EMPTY_STRING);
            }
            else
            {
                useDefaultCollation = false;
                if (lang is StringLiteral)
                {
                    UnicodeString s = ((StringLiteral)lang).GetString();
                    if (!s.IsEmpty())
                    {
                        ValidationFailure vf = StringConverter.StringToLanguage.INSTANCE.Validate(s);
                        if (vf != null)
                        {
                            CompileError("The lang attribute must be a valid language code", "XTDE0030");
                            lang = new StringLiteral(StringValue.EMPTY_STRING);
                        }
                    }
                }
            }

            if (stableAtt == null)
            {
                stable = null;
            }
            else
            {
                CheckAttributeValue("stable", stableAtt, true, StyleElement.YES_NO);
            }

            if (collationAtt != null)
            {
                useDefaultCollation = false;
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (select == null)
            {
                Expression b = CompileSequenceConstructor(exec, decl, true);
                if (b == null)
                {
                    b = Literal.MakeEmptySequence();
                    b.SetRetainedStaticContext(MakeRetainedStaticContext());
                }

                try
                {
                    Expression atomizedSortKey = Atomizer.MakeAtomizer(b, null);
                    atomizedSortKey = atomizedSortKey.Simplify();
                    ExpressionTool.CopyLocationInfo(b, atomizedSortKey);
                    sortKeyDefinition.SetSortKey(atomizedSortKey, true);
                    select = atomizedSortKey;
                }
                catch (XPathException e)
                {
                    CompileError(e);
                }
            }


            // Simplify the sort key definition - this is especially important in the case where
            // all aspects of the sort key are known statically.
            sortKeyDefinition = (SortKeyDefinition)sortKeyDefinition.Simplify();

            // not an executable instruction
            return null;
        }

        public virtual SortKeyDefinition GetSortKeyDefinition()
        {
            return sortKeyDefinition;
        }
    }
}