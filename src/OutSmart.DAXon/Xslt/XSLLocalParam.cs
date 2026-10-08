////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    public class XSLLocalParam : XSLGeneralVariable
    {
        private HashSet<SourceBinding.BindingProperty> permittedAttributes = new HashSet<SourceBinding.BindingProperty> { SourceBinding.BindingProperty.TUNNEL, SourceBinding.BindingProperty.REQUIRED, SourceBinding.BindingProperty.SELECT, SourceBinding.BindingProperty.AS };
        Expression conversion = null;
        private int slotNumber = -9876; // initial value designed solely to show up when debugging
        private LocalParam compiledParam;
        private bool prepared = false;

        public virtual int SlotNumber => slotNumber;

        private int ParameterPosition => Navigator.GetNumberSimple(this, null) - 1;

        public virtual Func<Expression> DefaultValueExpressionSupplier
        {
            get
            {
                if (!prepared)
                {
                    PrepareAttributes();
                }

                return () =>
                {
                    Expression select = sourceBinding.GetSelectExpression();
                    return select == null ? Literal.MakeEmptySequence() : select;
                };
            }
        }

        public virtual LocalParam CompiledParam => compiledParam;
        public override SourceBinding GetBindingInformation(StructuredQName name)
        {
            if (name.Equals(sourceBinding.VariableQName))
            {
                return sourceBinding;
            }
            else
            {
                return null;
            }
        }

        public override void PrepareAttributes()
        {
            if (!prepared)
            {
                prepared = true;
                sourceBinding.SetProperty(SourceBinding.BindingProperty.PARAM, true);
                if (GetParent() is XSLFunction)
                {
                    sourceBinding.SetProperty(SourceBinding.BindingProperty.REQUIRED, true);
                    if (GetCompilation().GetCompilerInfo().XsltVersion != 40)
                    {
                        permittedAttributes.Remove(SourceBinding.BindingProperty.SELECT);
                        sourceBinding.SetProperty(SourceBinding.BindingProperty.DISALLOWS_CONTENT, true);
                    }
                }

                sourceBinding.PrepareAttributes(permittedAttributes);
                if (sourceBinding.HasProperty(SourceBinding.BindingProperty.TUNNEL) && !(GetParent() is XSLTemplate))
                {
                    CompileError("For attribute 'tunnel' within an " + GetParent().DisplayName + " parameter, the only permitted value is 'no'", "XTSE0020");
                }

                if (GetParent() is XSLFunction)
                {
                    int pos = ParameterPosition;
                    if (GetCompilation().GetCompilerInfo().XsltVersion >= 40)
                    {
                        UserFunction uf = ((XSLFunction)GetParent()).GetCompiledFunction();
                        if (pos < uf.GetParameterDefinitions().Length)
                        {

                            // (if not, something is wrong; it will be reported later)
                            UserFunctionParameter ufp = new UserFunctionParameter();
                            uf.GetParameterDefinitions()[pos] = ufp;
                            ufp.SetRequiredType(GetRequiredType());
                            ufp.SetVariableQName(GetVariableQName());
                            ufp.SetSlotNumber(SlotNumber);
                            ufp.SetRequired(IsRequiredParam());
                            if (pos == 0 && uf.DeclaredStreamability != FunctionStreamability.UNCLASSIFIED)
                            {
                                ufp.FunctionStreamability = uf.DeclaredStreamability;
                            }

                            Expression defaultVal = sourceBinding.GetSelectExpression();
                            if (defaultVal != null)
                            {
                                if (!(defaultVal is Literal || defaultVal is ContextItemExpression))
                                {
                                    CompileError("The default value for a function parameter must be either a literal, or '.' (temporary Saxon restriction)");
                                }
                            }

                            if (defaultVal == null && !sourceBinding.HasProperty(SourceBinding.BindingProperty.REQUIRED))
                            {
                                defaultVal = new DefaultedArgumentExpression();
                            }

                            ufp.DefaultValueExpression = defaultVal;
                        }
                    }
                    else
                    {
                        if (!sourceBinding.HasProperty(SourceBinding.BindingProperty.REQUIRED))
                        {
                            CompileError("For attribute 'required' within an " + GetParent().DisplayName + " parameter, the only permitted value is 'yes'", "XTSE0020");
                        }
                    }
                }
            }
        }

        public virtual void PrepareTemplateSignatureAttributes()
        {
            if (!prepared)
            {
                sourceBinding.SetProperty(SourceBinding.BindingProperty.PARAM, true);
                sourceBinding.PrepareTemplateSignatureAttributes();
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            StructuredQName name = sourceBinding.VariableQName;
            NodeInfo parent = GetParent();
            bool isFunction = GetParent() is XSLFunction;
            if (!((parent is StyleElement) && ((StyleElement)parent).MayContainParam()))
            {
                CompileError("xsl:param must be immediately within a template, function or stylesheet", "XTSE0010");
            }

            if (HasChildNodes() && isFunction && GetCompilation().GetCompilerInfo().XsltVersion != 40)
            {
                CompileError("Function parameters cannot have a default value", "XTSE0760");
            }


            // it must be a text node; allow it if all whitespace
            SequenceTool.Supply(IterateAxis(AxisInfo.PRECEDING_SIBLING), (node) =>
            {
                if (node is XSLLocalParam)
                {
                    if (name.Equals(((XSLLocalParam)node).sourceBinding.VariableQName))
                    {
                        CompileError("The name of the parameter (" + name + ") is not unique", "XTSE0580");
                    }

                    if (isFunction && IsRequiredParam() && !((XSLLocalParam)node).IsRequiredParam())
                    {
                        CompileError("Parameter " + name + " is required, but an earlier parameter " + ((XSLLocalParam)node).sourceBinding.VariableQName + " is optional", "XTSE0761");
                        ((XSLLocalParam)node).sourceBinding.SetProperty(SourceBinding.BindingProperty.REQUIRED, true);
                    }
                }
                else if (node is StyleElement && ((StyleElement)node).Fingerprint != StandardNames.XSL_CONTEXT_ITEM)
                {
                    CompileError("xsl:param must not be preceded by other instructions", "XTSE0010");
                }
                else
                {
                    if (!Whitespace.IsAllWhite(node.UnicodeStringValue))
                    {
                        CompileError("xsl:param must not be preceded by text", "XTSE0010");
                    }
                }
            });
            SlotManager p = ContainingSlotManager;
            if (p == null)
            {
                CompileError("Local variable must be declared within a template or function", "XTSE0010");
            }
            else
            {
                slotNumber = p.AllocateSlotNumber(name, null);
            }

            if (sourceBinding.HasProperty(SourceBinding.BindingProperty.REQUIRED))
            {
                if (sourceBinding.GetSelectExpression() != null)
                {

                    // NB, we do this test before setting the default select attribute
                    string errorCode = isFunction ? "XTSE0760" : "XTSE0010";
                    CompileError("The select attribute must be omitted when required='yes'", errorCode);
                }

                if (HasChildNodes())
                {
                    string errorCode = isFunction ? "XTSE0760" : "XTSE0010";
                    CompileError("A parameter specifying required='yes' must have empty content", errorCode);
                }
            }

            base.Validate(decl);
        }

        public virtual bool IsTunnelParam()
        {
            return sourceBinding.HasProperty(SourceBinding.BindingProperty.TUNNEL);
        }

        public virtual bool IsRequiredParam()
        {
            return sourceBinding.HasProperty(SourceBinding.BindingProperty.REQUIRED);
        }

        protected override bool SeesAvuncularVariables()
        {
            return !(GetParent() is XSLFunction);
        }

        public override void FixupReferences()
        {
            sourceBinding.FixupReferences(null);
            base.FixupReferences();
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {

            if (GetParent() is XSLFunction)
            {
                if (GetCompilation().GetCompilerInfo().XsltVersion >= 40 && !IsRequiredParam())
                {
                    sourceBinding.HandleSequenceConstructor(exec, decl);
                    Expression selectExpression = sourceBinding.GetSelectExpression();
                    if (selectExpression == null)
                    {
                        selectExpression = Literal.MakeEmptySequence();
                    }
                    else
                    {
                        Expression underlyingExpression = selectExpression;
                        while (underlyingExpression is ItemChecker || underlyingExpression is CardinalityChecker)
                        {
                            underlyingExpression = ((UnaryExpression)underlyingExpression).BaseExpression;
                        }

                        if (!(underlyingExpression is Literal || underlyingExpression is ContextItemExpression))
                        {
                            CompileError("The default value for a function parameter must be either a literal, or '.' (temporary Saxon restriction)");
                        }
                    }

                    int pos = ParameterPosition;
                    ((XSLFunction)GetParent()).GetCompiledFunction().GetParameterDefinitions()[pos].DefaultValueExpression = selectExpression;
                }

                return null;
            }
            else
            {
                SequenceType declaredType = GetRequiredType();
                StructuredQName name = sourceBinding.VariableQName;
                int slot = SlotNumber;
                if (declaredType != null)
                {
                    SuppliedParameterReference pref = new SuppliedParameterReference(slot);
                    pref.SetRetainedStaticContext(MakeRetainedStaticContext());
                    pref.SetLocation(AllocateLocation());
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.PARAM, name.DisplayName, 0, "XTTE0590");
                    conversion = exec.GetConfiguration().GetTypeChecker(false).StaticTypeCheck(pref, declaredType, role, MakeExpressionVisitor());
                }

                sourceBinding.HandleSequenceConstructor(exec, decl);
                LocalParam binding = new LocalParam();
                binding.SetLocation(AllocateLocation());
                binding.SelectExpression = sourceBinding.GetSelectExpression();
                binding.Conversion = conversion;
                binding.SetVariableQName(name);
                binding.SlotNumber = slot;
                binding.SetRequiredType(GetRequiredType());
                binding.SetRequiredParam(sourceBinding.HasProperty(SourceBinding.BindingProperty.REQUIRED));
                binding.SetImplicitlyRequiredParam(sourceBinding.HasProperty(SourceBinding.BindingProperty.IMPLICITLY_REQUIRED));
                binding.SetTunnel(sourceBinding.HasProperty(SourceBinding.BindingProperty.TUNNEL));
                sourceBinding.FixupBinding(binding);
                return compiledParam = binding;
            }
        }

        public virtual SequenceType GetRequiredType()
        {
            SequenceType declaredType = sourceBinding.DeclaredType;
            if (declaredType != null)
            {
                return declaredType;
            }
            else
            {
                return SequenceType.ANY_SEQUENCE;
            }
        }
    }
}