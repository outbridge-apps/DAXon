////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using System.Linq;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class AbsentExtensionElement : StyleElement
    {
        CallTemplate instruction;
        public override bool IsInXsltNamespace()
        {
            return GetNodeName().HasURI(NamespaceUri.XSLT);
        }

        public override bool IsInstruction()
        {
            return true;
        }

        /// <summary>
        /// Determine whether this type of element is allowed to contain a template-body
        /// </summary>
        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void ProcessAllAttributes()
        {
            if (reportingCircumstances == OnFailure.IGNORED_INSTRUCTION)
            {
                return;
            }

            if (reportingCircumstances == OnFailure.REPORT_ALWAYS)
            {
                CompileError(validationError);
            }

            if (EffectiveVersion >= 40 && !IsInXsltNamespace())
            {
                PrincipalStylesheetModule pack = GetPrincipalStylesheetModule();
                NamedTemplate template = pack.GetNamedTemplate(GetNodeName().GetStructuredQName());
                if (template != null)
                {
                    CallTemplate insn = new CallTemplate(template, template.TemplateName, false, ((StyleElement)GetParent()).IsWithinDeclaredStreamableConstruct());
                    IList<NamedTemplate.LocalParamInfo> declaredParams = template.LocalParamDetails;

                    // Check that all the required parameters are supplied
                    foreach (NamedTemplate.LocalParamInfo param in declaredParams)
                    {
                        if (param.isRequired && !param.isTunnel)
                        {
                            if (Attributes().Get(param.name.GetNamespaceUri(), param.name.GetLocalPart()) == null)
                            {
                                CompileError("No value supplied for required parameter " + Err.Wrap(param.name.DisplayName, Err.VARIABLE), "XTSE0690");
                            }
                        }
                    }


                    // check that all supplied parameters are declared, and handle the parameter type
                    IList<WithParam> @params = new List<WithParam>();
                    foreach (AttributeInfo att in Attributes().AsList())
                    {
                        StructuredQName name = att.GetNodeName().GetStructuredQName();
                        if (!name.HasURI(NamespaceUri.XSLT) && !name.HasURI(NamespaceUri.XML))
                        {
                            foreach (NamedTemplate.LocalParamInfo param in declaredParams)
                            {
                                if (param.name.Equals(name) && !param.isTunnel)
                                {
                                    SequenceType required = param.requiredType;
                                    WithParam withParam = new WithParam();
                                    withParam.VariableQName = name;
                                    withParam.RequiredType = SequenceType.ANY_SEQUENCE;
                                    if (required.GetCardinality() == StaticProperty.EXACTLY_ONE && required.PrimaryType.IsPlainType())
                                    {
                                        if (required.PrimaryType == BuiltInAtomicType.BOOLEAN)
                                        {
                                            Expression avt = MakeAttributeValueTemplate(att.Value, att);
                                            Expression toBool = VendorFunctionSetHE.GetInstance().MakeFunction("yes-no-boolean", 1).MakeFunctionCall(avt);
                                            withParam.SetSelectExpression(insn, toBool);
                                        }
                                        else
                                        {
                                            Expression avt = MakeAttributeValueTemplate(att.Value, att);
                                            if (required.PrimaryType != BuiltInAtomicType.STRING)
                                            {
                                                avt = new AtomicSequenceConverter(avt, BuiltInAtomicType.UNTYPED_ATOMIC);
                                            }

                                            withParam.SetSelectExpression(insn, avt);
                                        }
                                    }
                                    else
                                    {
                                        Expression select = MakeExpression(att.Value, att);
                                        withParam.SetSelectExpression(insn, select);
                                    }

                                    @params.Add(withParam);
                                }
                            }
                        }
                    }

                    insn.SetActualParameters(@params.ToArray(), new WithParam[] { });
                    this.instruction = insn;
                }
            }

            if (IsTopLevel() && ForwardsCompatibleModeIsEnabled())
            {
            }
            else
            {
                base.ProcessAllAttributes();
            }
        }

        public override void PrepareAttributes()
        {
        }

        public override bool MarkTailCalls()
        {
            if (instruction != null)
            {
                instruction.SetTailRecursive(true);
            }

            return true;
        }

        public override void ValidateSubtree(ComponentDeclaration decl, bool excludeStylesheet)
        {
            if (reportingCircumstances == OnFailure.IGNORED_INSTRUCTION || (IsTopLevel() && ForwardsCompatibleModeIsEnabled()))
            {
            }
            else
            {
                base.ValidateSubtree(decl, excludeStylesheet);
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (instruction != null)
            {
                return instruction;
            }

            if (IsTopLevel() || reportingCircumstances == OnFailure.IGNORED_INSTRUCTION)
            {
                return null;
            }


            // if there are fallback children, compile the code for the fallback elements
            if (validationError == null)
            {
                validationError = new XmlProcessingIncident("Unknown instruction");
            }

            return FallbackProcessing(exec, decl, this);
        }
    }
}
