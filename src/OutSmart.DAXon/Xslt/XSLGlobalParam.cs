////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLGlobalParam : XSLGlobalVariable
    {

        Expression conversion = null;
        protected override HashSet<SourceBinding.BindingProperty> PermittedAttributes => new HashSet<SourceBinding.BindingProperty> { SourceBinding.BindingProperty.REQUIRED, SourceBinding.BindingProperty.SELECT, SourceBinding.BindingProperty.AS, SourceBinding.BindingProperty.STATIC };
        public XSLGlobalParam()
        {
            sourceBinding.SetProperty(SourceBinding.BindingProperty.PARAM, true);
        }

        public override Visibility GetVisibility()
        {
            string statik = GetAttributeValue("static");
            if (statik == null)
            {
                return Visibility.PUBLIC;
            }
            else
            {
                bool isStatic = ProcessBooleanAttribute("static", statik);
                return isStatic ? Visibility.PRIVATE : Visibility.PUBLIC;
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (sourceBinding.HasProperty(SourceBinding.BindingProperty.REQUIRED))
            {
                if (sourceBinding.GetSelectExpression() != null)
                {

                    // NB, we do this test before setting the default select attribute
                    CompileError("The select attribute must be absent when required='yes'", "XTSE0010");
                }

                if (HasChildNodes())
                {
                    CompileError("A parameter specifying required='yes' must have empty content", "XTSE0010");
                }

                Visibility vis = GetVisibility();
                if (!sourceBinding.IsStatic() && !(vis == Visibility.PUBLIC || vis == Visibility.FINAL || vis == Visibility.ABSTRACT))
                {
                    CompileError("The visibility of a required non-static parameter must be public, final, or abstract", "XTSE3370");
                }
            }

            base.Validate(decl);
        }

        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
            if (sourceBinding.IsStatic())
            {
                base.CompileDeclaration(compilation, decl);
            }
            else if (!redundant)
            {
                sourceBinding.HandleSequenceConstructor(compilation, decl);
                GlobalParam binding = (GlobalParam)compiledVariable;
                binding.SetPackageData(GetCompilation().GetPackageData());
                binding.ObtainDeclaringComponent(this);
                Expression select = sourceBinding.GetSelectExpression();
                binding.SetBody(select);
                binding.SetVariableQName(sourceBinding.VariableQName);
                InitializeBinding(binding);
                if (select != null && compilation.GetCompilerInfo().CodeInjector != null)
                {
                    compilation.GetCompilerInfo().CodeInjector.Process(binding);
                }

                binding.SetRequiredType(GetRequiredType());
                binding.SetRequiredParam(sourceBinding.HasProperty(SourceBinding.BindingProperty.REQUIRED));
                binding.SetImplicitlyRequiredParam(sourceBinding.HasProperty(SourceBinding.BindingProperty.IMPLICITLY_REQUIRED));
                sourceBinding.FixupBinding(binding);

                //compiledVariable = binding;
                Component overridden = OverriddenComponent;
                if (overridden != null)
                {
                    CheckCompatibility(overridden);
                }
            }
        }

        public override SequenceType GetRequiredType()
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