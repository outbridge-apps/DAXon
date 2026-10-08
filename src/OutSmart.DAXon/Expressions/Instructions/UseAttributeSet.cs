////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Linq;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class UseAttributeSet : Instruction, IComponentInvocation, IContextOriginator
    {
        private readonly StructuredQName targetName;
        private AttributeSet target;
        private readonly bool declaredStreamable;
        private int bindingSlot = -1;

        public int BindingSlot
        {
            get => bindingSlot; set
            {
                bindingSlot = value;
            }
        }

        public Component FixedTarget
        {
            get
            {
                if (target != null && bindingSlot < 0)
                {
                    return target.DeclaringComponent;
                }

                return null;
            }
        }

        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_XSLT_CONTEXT | StaticProperty.DEPENDS_ON_FOCUS;

        public StructuredQName TargetAttributeSetName => targetName;

        public override string ExpressionName => "useAS";

        public override string StreamerName => "UseAttributeSet";
        public UseAttributeSet(StructuredQName name, bool streamable)
        {
            this.targetName = name;
            this.declaredStreamable = streamable;
        }

        public override bool IsInstruction()
        {
            return false;
        }

        public static Expression MakeUseAttributeSets(StructuredQName[] targets, StyleElement instruction)
        {
            IList<UseAttributeSet> list = MakeUseAttributeSetInstructions(targets, instruction);
            return MakeCompositeExpression(list);
        }

        public static IList<UseAttributeSet> MakeUseAttributeSetInstructions(StructuredQName[] targets, StyleElement instruction)
        {
            IList<UseAttributeSet> list = new List<UseAttributeSet>(targets.Length);
            foreach (StructuredQName name in targets)
            {
                UseAttributeSet use = MakeUseAttributeSet(name, instruction);
                if (use != null)
                {
                    list.Add(use);
                }
            }

            return list;
        }

        public static Expression MakeCompositeExpression(IList<UseAttributeSet> targets)
        {
            if (targets.Count == 0)
            {
                return Literal.MakeEmptySequence();
            }
            else if (targets.Count == 1)
            {
                return targets[0];
            }
            else
            {
                return new Block(targets.ToArray());
            }
        }

        private static UseAttributeSet MakeUseAttributeSet(StructuredQName name, StyleElement instruction)
        {
            AttributeSet target;
            if (name.HasURI(NamespaceUri.XSLT) && name.GetLocalPart().Equals("original"))
            {
                target = (AttributeSet)instruction.GetXslOriginal(StandardNames.XSL_ATTRIBUTE_SET);
            }
            else
            {
                Component invokee = instruction.ContainingPackage.GetComponent(new SymbolicName(StandardNames.XSL_ATTRIBUTE_SET, name));
                instruction.GetPrincipalStylesheetModule().GetAttributeSetDeclarations(name);
                if (invokee == null)
                {
                    instruction.CompileError("Unknown attribute set " + name.EQName, "XTSE0710");
                    return null; // to prevent compile warnings
                }

                target = (AttributeSet)invokee.GetActor();
            }

            UseAttributeSet invocation = new UseAttributeSet(name, target.IsDeclaredStreamable());
            invocation.SetTarget(target);
            invocation.BindingSlot = -1;
            invocation.SetRetainedStaticContext(instruction.MakeRetainedStaticContext());
            return invocation;
        }

        public bool IsDeclaredStreamable()
        {
            return declaredStreamable;
        }

        public void SetTarget(AttributeSet target)
        {
            this.target = target;
        }

        public SymbolicName GetSymbolicName()
        {
            return new SymbolicName(StandardNames.XSL_ATTRIBUTE_SET, targetName);
        }

        public override IEnumerable<Operand> Operands()
        {
            return new List<Operand>();
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            UseAttributeSet ua = new UseAttributeSet(targetName, declaredStreamable);
            ua.SetTarget(target);
            ua.BindingSlot = bindingSlot;
            return ua;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return this;
        }

        public override ItemType GetItemType()
        {
            return NodeKindTest.ATTRIBUTE;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("useAS", this);
            @out.EmitAttribute("name", targetName);
            @out.EmitAttribute("bSlot", "" + BindingSlot);
            if (IsDeclaredStreamable())
            {
                @out.EmitAttribute("flags", "s");
            }

            @out.EndElement();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is UseAttributeSet))
            {
                return false;
            }

            return targetName.Equals(((UseAttributeSet)obj).targetName);
        }

        protected override int ComputeHashCode()
        {
            return 0x56423719 ^ targetName.GetHashCode();
        }

        public override Elaborator GetElaborator()
        {
            return new UseAttributeSetElaborator();
        }

        internal sealed class UseAttributeSetElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                UseAttributeSet expr = (UseAttributeSet)GetExpression();
                return (output, context) =>
                {
                    Component target;
                    if (expr.bindingSlot < 0)
                    {
                        target = expr.FixedTarget;
                    }
                    else
                    {
                        target = context.GetTargetComponent(expr.bindingSlot);
                        if (target.IsHiddenAbstractComponent())
                        {
                            throw new XPathException("Cannot expand an abstract attribute set (" + expr.targetName.DisplayName + ") with no implementation", "XTDE3052").WithLocation(expr.GetLocation());
                        }
                    }

                    if (target == null)
                    {
                        throw new InvalidOperationException("Failed to locate attribute set " + expr.TargetAttributeSetName.EQName);
                    }

                    AttributeSet @as = (AttributeSet)target.GetActor();
                    XPathContextMajor c2 = context.NewContext();
                    c2.SetCurrentComponent(target);
                    c2.Origin = expr;
                    SlotManager sm = @as.GetStackFrameMap();
                    if (sm == null)
                    {
                        sm = SlotManager.EMPTY;
                    }

                    c2.OpenStackFrame(sm);
                    @as.Expand(output, c2);
                    return null;
                };
            }
        }
    }
}