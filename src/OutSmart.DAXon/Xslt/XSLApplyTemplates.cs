////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:apply-templates element in the stylesheet
    /// </summary>
    internal sealed class XSLApplyTemplates : StyleElement
    {
        private Expression select;
        private Expression separator;
        private StructuredQName modeName; // null if no name specified or if conventional values such as #current used
        private bool useCurrentMode;
        private bool useTailRecursion;
        private bool defaultedSelectExpression = true;
        private Mode mode;
        private string modeAttribute;
        public override bool IsInstruction()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string selectAtt;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "mode":
                        modeAttribute = Whitespace.Trim(value);
                        break;
                    case "select":
                        selectAtt = value;
                        select = MakeExpression(selectAtt, att);
                        defaultedSelectExpression = false;
                        break;
                    case "separator":
                        if (RequireXslt40Attribute("separator"))
                        {
                            separator = MakeAttributeValueTemplate(value, att);
                        }

                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (modeAttribute != null)
            {
                switch (modeAttribute)
                {
                    case "#current":
                        useCurrentMode = true;
                        break;
                    case "#unnamed":
                        modeName = Mode.UNNAMED_MODE_NAME;
                        break;
                    case "#default":

                        // do nothing;
                        break;
                    default:
                        modeName = MakeQName(modeAttribute, null, "mode");
                        break;
                }
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {

            // get the Mode object
            if (useCurrentMode)
            {

                // give a warning if we're not inside an xsl:template
                if (IterateAxis(AxisInfo.ANCESTOR, new NameTest(Types.Type.ELEMENT, StandardNames.XSL_TEMPLATE, GetNamePool())).Next() == null)
                {
                    IssueWarning("Specifying mode=\"#current\" when not inside an xsl:template serves no useful purpose", DAXonErrorCode.SXWN9023);
                }
            }
            else
            {
                PrincipalStylesheetModule psm = GetPrincipalStylesheetModule();
                if (modeName == null)
                {

                    // XSLT 3.0 allows a default mode to be specified on a containing element
                    modeName = DefaultMode;
                    if ((modeName == null || modeName.Equals(Mode.UNNAMED_MODE_NAME)) && psm.IsDeclaredModes() && !psm.GetRuleManager().IsUnnamedModeExplicit())
                    {
                        CompileError("The unnamed mode must be explicitly declared in an xsl:mode declaration", "XTSE3085");
                    }
                }
                else if (modeName.Equals(Mode.UNNAMED_MODE_NAME) && psm.IsDeclaredModes() && !psm.GetRuleManager().IsUnnamedModeExplicit())
                {
                    CompileError("The #unnamed mode must be explicitly declared in an xsl:mode declaration", "XTSE3085");
                }

                SymbolicName sName = new SymbolicName(StandardNames.XSL_MODE, modeName);
                StylesheetPackage containingPackage = decl.SourceElement.ContainingPackage;
                Dictionary<SymbolicName, Component> componentIndex = containingPackage.ComponentIndex;

                // see if there is a mode with this name in a used package
                Component existing = componentIndex.GetOrDefault(sName);
                if (existing != null)
                {
                    mode = (Mode)existing.GetActor();
                }

                if (mode == null)
                {
                    if (psm.IsDeclaredModes())
                    {
                        CompileError("Mode name " + modeName.DisplayName + " must be explicitly declared in an xsl:mode declaration", "XTSE3085");
                    }

                    mode = psm.GetRuleManager().ObtainMode(modeName, true);
                }
            }


            // handle sorting if requested
            foreach (NodeInfo child in Children())
            {
                if (child.GetNodeKind() == Types.Type.TEXT)
                {

                    // with xml:space=preserve, white space nodes may still be there
                    if (!Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {
                        CompileError("No character data is allowed within xsl:apply-templates", "XTSE0010");
                    }
                }
                else if (!(child is XSLSort || child is XSLWithParam))
                {
                    CompileError("Invalid element " + Err.Wrap(child.DisplayName, Err.ELEMENT) + " within xsl:apply-templates", "XTSE0010");
                }
            }

            if (select == null)
            {
                Expression here = new ContextItemExpression();
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.CONTEXT_ITEM, "", 0, "XTTE0510");
                here = new ItemChecker(here, AnyNodeTest.GetInstance(), role);
                select = new SimpleStepExpression(here, new AxisExpression(AxisInfo.CHILD, null));
                select.SetLocation(AllocateLocation());
                select.SetRetainedStaticContext(MakeRetainedStaticContext());
            }

            select = TypeCheck("select", select);
            if (separator != null)
            {
                separator = TypeCheck("separator", separator);
            }
        }

        public override bool MarkTailCalls()
        {
            useTailRecursion = true;
            return true;
        }

        public override Expression Compile(Compilation compilation, ComponentDeclaration decl)
        {
            SortKeyDefinitionList sortKeys = MakeSortKeys(compilation, decl);
            if (sortKeys != null)
            {
                useTailRecursion = false;
            }

            Expression sortedSequence = select;
            if (sortKeys != null)
            {
                sortedSequence = new SortExpression(select, sortKeys);
            }

            CompileSequenceConstructor(compilation, decl, true);
            RuleManager rm = compilation.GetPrincipalStylesheetModule().GetRuleManager();
            ApplyTemplates app = new ApplyTemplates(sortedSequence, useCurrentMode, useTailRecursion, defaultedSelectExpression, IsWithinDeclaredStreamableConstruct(), mode, rm);
            app.SetLocation(SaveLocation());
            app.SetActualParams(GetWithParamInstructions(app, compilation, decl, false));
            app.SetTunnelParams(GetWithParamInstructions(app, compilation, decl, true));
            if (separator != null)
            {
                app.SeparatorExpression = separator;
            }

            return app;
        }
    }
}