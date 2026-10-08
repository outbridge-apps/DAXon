////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:call-template element in the stylesheet
    /// </summary>
    internal sealed class XSLCallTemplate : StyleElement
    {
        private static readonly StructuredQName ERROR_TEMPLATE_NAME = new StructuredQName("saxon", NamespaceUri.SAXON, "error-template");
        private StructuredQName calledTemplateName; // the name of the called template
        private NamedTemplate template; // the template to be called (which may subsequently be overridden in another package)
        private bool useTailRecursion;
        public override bool IsInstruction()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string nameAttribute = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("name"))
                {
                    nameAttribute = Whitespace.Trim(value);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (nameAttribute == null)
            {
                calledTemplateName = ERROR_TEMPLATE_NAME;
                ReportAbsence("name");
                return;
            }

            calledTemplateName = MakeQName(nameAttribute, null, "name");
        }

        public override void Validate(ComponentDeclaration decl)
        {
            foreach (NodeInfo child in Children())
            {
                if (child is XSLWithParam)
                {
                }
                else if (child is XSLFallback && MayContainFallback())
                {
                }
                else if (child.GetNodeKind() == Types.Type.TEXT)
                {

                    // with xml:space=preserve, white space nodes may still be there
                    if (!Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {
                        CompileError("No character data is allowed within xsl:call-template", "XTSE0010");
                    }
                }
                else
                {
                    CompileError("Child element " + Err.Wrap(child.DisplayName, Err.ELEMENT) + " is not allowed as a child of xsl:call-template", "XTSE0010");
                }
            }

            if (calledTemplateName == null)
            {
                calledTemplateName = ERROR_TEMPLATE_NAME;
            }

            if (!calledTemplateName.Equals(ERROR_TEMPLATE_NAME))
            {
                template = FindTemplate(calledTemplateName);
            }
        }

        // OK;
        public override void PostValidate()
        {

            // check that a parameter is supplied for each required parameter
            // of the called template
            if (template != null)
            {
                CheckParams();
            }
            else
            {
                throw new InvalidOperationException("Target template not known");
            }
        }

        // OK;
        private void CheckParams()
        {
            IList<NamedTemplate.LocalParamInfo> declaredParams = template.LocalParamDetails;
            foreach (NamedTemplate.LocalParamInfo param in declaredParams)
            {
                if (param.isRequired && !param.isTunnel)
                {
                    bool ok = false;
                    foreach (NodeInfo withParam in Children(new TypeIsInstancePredicate(typeof(XSLWithParam))))
                    {
                        if (((XSLWithParam)withParam).GetVariableQName().Equals(param.name))
                        {
                            ok = true;
                            break;
                        }
                    }

                    if (!ok)
                    {
                        CompileError("No value supplied for required parameter " + Err.Wrap(param.name.DisplayName, Err.VARIABLE), "XTSE0690");
                    }
                }
            }


            // check that every supplied parameter is declared in the called
            // template
            foreach (NodeInfo w in Children())
            {
                if (w is XSLWithParam && !((XSLWithParam)w).IsTunnelParam())
                {
                    XSLWithParam withParam = (XSLWithParam)w;
                    bool ok = false;
                    foreach (NamedTemplate.LocalParamInfo param in declaredParams)
                    {
                        if (param.name.Equals(withParam.GetVariableQName()) && !param.isTunnel)
                        {

                            // Note: see bug 10534
                            ok = true;
                            SequenceType required = param.requiredType;
                            withParam.CheckAgainstRequiredType(required);
                            break;
                        }
                    }

                    if (!ok && !XPath10ModeIsEnabled())
                    {
                        CompileError("Parameter " + withParam.GetVariableQName().DisplayName + " is not declared in the called template", "XTSE0680");
                    }
                }
            }
        }

        // OK;
        private NamedTemplate FindTemplate(StructuredQName templateName)
        {
            PrincipalStylesheetModule pack = GetPrincipalStylesheetModule();
            NamedTemplate template = pack.GetNamedTemplate(templateName);
            if (template == null)
            {
                if (templateName.HasURI(NamespaceUri.XSLT) && templateName.GetLocalPart().Equals("original"))
                {

                    // Handle xsl:original
                    return (NamedTemplate)GetXslOriginal(StandardNames.XSL_TEMPLATE);
                }

                CompileError("Cannot find a template named " + calledTemplateName, "XTSE0650");
            }

            return template;
        }

        // OK;
        public override bool MarkTailCalls()
        {
            useTailRecursion = true;
            return true;
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (template == null)
            {
                return null; // error already reported
            }

            CallTemplate call = new CallTemplate(template, calledTemplateName, useTailRecursion, IsWithinDeclaredStreamableConstruct());
            call.SetLocation(AllocateLocation());
            call.SetActualParameters(GetWithParamInstructions(call, exec, decl, false), GetWithParamInstructions(call, exec, decl, true));
            return call;
        }
    }
}