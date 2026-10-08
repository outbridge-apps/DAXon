////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Xslt
{
    internal class XSLGlobalVariable : StyleElement, IStylesheetComponent
    {
        private SlotManager slotManager; // used to manage local variables declared inside this global variable
        protected SourceBinding sourceBinding;
        protected GlobalVariable compiledVariable;

        private int state;
        protected bool redundant;

        public virtual GlobalVariable CompiledVariable => compiledVariable;

        protected virtual HashSet<SourceBinding.BindingProperty> PermittedAttributes => new HashSet<SourceBinding.BindingProperty> { SourceBinding.BindingProperty.ASSIGNABLE, SourceBinding.BindingProperty.SELECT, SourceBinding.BindingProperty.AS, SourceBinding.BindingProperty.STATIC, SourceBinding.BindingProperty.VISIBILITY };
        public XSLGlobalVariable()
        {
            sourceBinding = new SourceBinding(this);
            sourceBinding.SetProperty(SourceBinding.BindingProperty.GLOBAL, true);
        }

        public virtual SourceBinding GetSourceBinding()
        {
            return sourceBinding;
        }

        public virtual StructuredQName GetVariableQName()
        {
            return sourceBinding.VariableQName;
        }

        public override StructuredQName GetObjectName()
        {
            return sourceBinding.VariableQName;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void PostValidate()
        {
            sourceBinding.PostValidate();
        }
        public Actor GetActor()
        {
            GlobalVariable gv = CompiledVariable;
            if (gv == null)
            {
                gv = this is XSLGlobalParam ? new GlobalParam() : new GlobalVariable();
                gv.SetPackageData(GetCompilation().GetPackageData());
                gv.ObtainDeclaringComponent(this);
                gv.SetRequiredType(sourceBinding.DeclaredType);
                gv.DeclaredVisibility = DeclaredVisibility;
                gv.SetVariableQName(sourceBinding.VariableQName);
                gv.SetSystemId(GetSystemId());
                gv.SetLineNumber(GetLineNumber());
                gv.SetColumnNumber(GetColumnNumber());
                RetainedStaticContext rsc = MakeRetainedStaticContext();
                gv.SetRetainedStaticContext(rsc);
                if (gv.GetBody() != null)
                {
                    gv.GetBody().SetRetainedStaticContext(rsc);
                }

                compiledVariable = gv;
            }

            return gv;
        }

        public SymbolicName GetSymbolicName()
        {
            return new SymbolicName(StandardNames.XSL_VARIABLE, GetObjectName());
        }

        public void CheckCompatibility(Component component)
        {
            SequenceType st1 = GetSourceBinding().DeclaredType;
            if (st1 == null)
            {
                st1 = SequenceType.ANY_SEQUENCE;
            }

            GlobalVariable other = (GlobalVariable)component.GetActor();
            TypeHierarchy th = component.DeclaringPackage.GetConfiguration().GetTypeHierarchy();
            Affinity relation = th.SequenceTypeRelationship(st1, other.GetRequiredType());
            if (relation != Affinity.SAME_TYPE)
            {
                CompileError("The declared type of the overriding variable $" + GetVariableQName().DisplayName + " is different from that of the overridden variable", "XTSE3070");
            }
        }

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
            if (state == 2)
            {
                return;
            }

            if (state == 1)
            {
                CompileError("Circular reference to variable", "XTDE0640");
            }

            state = 1;

            sourceBinding.PrepareAttributes(PermittedAttributes);
            state = 2;
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            top.IndexVariableDeclaration(decl);
        }

        public override void Validate(ComponentDeclaration decl)
        {
            slotManager = GetConfiguration().MakeSlotManager();
            sourceBinding.Validate();
        }

        public virtual bool IsAssignable()
        {
            return sourceBinding.HasProperty(SourceBinding.BindingProperty.ASSIGNABLE);
        }

        public override bool IsDeclaration()
        {
            return true;
        }

        public override bool IsInstruction()
        {
            return false;
        }

        public virtual SequenceType GetRequiredType()
        {
            return sourceBinding.GetInferredType(true);
        }

        public override void FixupReferences()
        {
            sourceBinding.FixupReferences(compiledVariable);
            base.FixupReferences();
        }

        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {

            // Commented out: Can't eliminate unused variables at this stage because they might be xsl:expose'd as public
            //        boolean unused = sourceBinding.getReferences().isEmpty() && !isAssignable() &&
            //                getVisibility() == Visibility.PRIVATE;
            //            redundant = true;
            //            // Remove the global variable from the package (otherwise a failure can occur
            //            // when pre-evaluating global variables)
            //        }
            if (!redundant)
            {
                sourceBinding.HandleSequenceConstructor(compilation, decl);
                GlobalVariable inst = CompiledVariable;
                if (inst == null)
                {
                    inst = new GlobalVariable();
                    inst.SetPackageData(GetCompilation().GetPackageData());
                    inst.ObtainDeclaringComponent(this);
                    inst.SetVariableQName(sourceBinding.VariableQName);
                }

                if (sourceBinding.IsStatic())
                {
                    inst.SetStatic(true);
                    IGroundedValue value = compilation.GetStaticVariable(sourceBinding.VariableQName);
                    if (value == null)
                    {
                        throw new InvalidOperationException();
                    }

                    Expression select = Literal.MakeLiteral(value);
                    select.SetRetainedStaticContext(MakeRetainedStaticContext());
                    inst.SetBody(select);
                }
                else
                {
                    Expression select = sourceBinding.GetSelectExpression();
                    inst.SetBody(select);
                    if (compilation.GetCompilerInfo().CodeInjector != null && select != null)
                    {

                        // select==null happens when the variable is abstract: bug 6378
                        compilation.GetCompilerInfo().CodeInjector.Process(inst);
                    }
                }

                inst.SetRetainedStaticContext(MakeRetainedStaticContext());
                InitializeBinding(inst);
                inst.SetAssignable(IsAssignable());
                inst.SetRequiredType(GetRequiredType());
                sourceBinding.FixupBinding(inst);
                compiledVariable = inst;
                Component overridden = OverriddenComponent;
                if (overridden != null)
                {
                    CheckCompatibility(overridden);
                }
            }
        }

        protected virtual void InitializeBinding(GlobalVariable var)
        {
            Expression select = var.GetBody();
            Expression exp2 = select;
            if (exp2 != null)
            {
                try
                {
                    ExpressionVisitor visitor = MakeExpressionVisitor();
                    GlobalContextRequirement gcr = GetPackageData().ContextItemRequirements;
                    ContextItemStaticInfo cisi = gcr == null ? GetConfiguration().DefaultContextItemStaticInfo : gcr.MakeGlobalContextInfo(GetConfiguration());
                    exp2 = select.Simplify().TypeCheck(visitor, cisi);
                }
                catch (XPathException err)
                {
                    CompileError(err);
                }

                SetInstructionLocation(this, exp2);
                AllocateLocalSlots(exp2);
            }

            if (slotManager != null && slotManager.NumberOfVariables > 0)
            {
                var.SetContainsLocals(slotManager);
            }


            if (exp2 != select)
            {
                var.SetBody(exp2);
            }
        }

        public SlotManager GetSlotManager()
        {
            return slotManager;
        }

        public void Optimize(ComponentDeclaration declaration)
        {
            if (!redundant && compiledVariable.GetBody() != null)
            {
                Expression exp2 = compiledVariable.GetBody();
                ExpressionVisitor visitor = MakeExpressionVisitor();
                exp2 = ExpressionTool.OptimizeComponentBody(exp2, GetCompilation(), visitor, GetConfiguration().MakeContextItemStaticInfo(AnyItemType.GetInstance(), true), false);
                AllocateLocalSlots(exp2);
                if (slotManager != null && slotManager.NumberOfVariables > 0)
                {
                    compiledVariable.SetContainsLocals(slotManager);
                }

                if (exp2 != compiledVariable.GetBody())
                {
                    compiledVariable.SetBody(exp2);
                }
            }
        }

        public virtual void SetRedundant(bool redundant)
        {
            this.redundant = redundant;
        }
    }
}
