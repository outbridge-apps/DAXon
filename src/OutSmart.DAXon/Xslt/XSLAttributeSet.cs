////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:attribute-set element in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLAttributeSet : StyleElement, IStylesheetComponent
    {
        private string nameAtt;
        private string useAtt;
        private string visibilityAtt;
        private SlotManager stackFrameMap;
        private readonly IList<ComponentDeclaration> attributeSetElements = new List<ComponentDeclaration>();
        private StructuredQName[] useAttributeSetNames;
        private readonly IList<Expression> containedInstructions = new List<Expression>();
        private bool validated;
        private Visibility visibility;
        private bool streamable;

        public StructuredQName AttributeSetName => GetObjectName();

        public IList<Expression> ContainedInstructions => containedInstructions;
        public AttributeSet GetActor()
        {
            return (AttributeSet)GetPrincipalStylesheetModule().GetStylesheetPackage().GetComponent(new SymbolicName(StandardNames.XSL_ATTRIBUTE_SET, GetObjectName())).GetActor();
        }

        public SymbolicName GetSymbolicName()
        {
            return new SymbolicName(StandardNames.XSL_ATTRIBUTE_SET, GetObjectName());
        }

        public void CheckCompatibility(Component component)
        {
            if (((AttributeSet)component.GetActor()).IsDeclaredStreamable() && !IsDeclaredStreamable())
            {
                CompileError("The overridden attribute set is declared streamable, " + "so the overriding attribute set must also be declared streamable");
            }
        }

        public override bool IsDeclaration()
        {
            return true;
        }

        public bool IsDeclaredStreamable()
        {
            return streamable;
        }

        public override void PrepareAttributes()
        {
            useAtt = null;
            string streamableAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "name":
                        nameAtt = Whitespace.Trim(value);
                        break;
                    case "use-attribute-sets":
                        useAtt = value;
                        break;
                    case "streamable":
                        streamableAtt = value;
                        break;
                    case "visibility":
                        visibilityAtt = Whitespace.Trim(value);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (nameAtt == null)
            {
                ReportAbsence("name");
                SetObjectName(new StructuredQName("", NamespaceUri.NULL, "attribute-set-error-name"));
                return;
            }

            if (visibilityAtt == null)
            {
                visibility = Visibility.PRIVATE;
            }
            else
            {
                visibility = InterpretVisibilityValue(visibilityAtt, "");
            }

            if (streamableAtt != null)
            {
                streamable = ProcessStreamableAtt(streamableAtt);
            }

            SetObjectName(MakeQName(nameAtt, null, "name"));
        }

        public override StructuredQName GetObjectName()
        {
            StructuredQName o = base.GetObjectName();
            if (o == null)
            {
                PrepareAttributes();
                o = GetObjectName();
            }

            return o;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (validated)
            {
                return;
            }

            CheckTopLevel("XTSE0010", true);
            stackFrameMap = GetConfiguration().MakeSlotManager();
            foreach (NodeInfo child in Children())
            {
                if (child is XSLAttribute)
                {
                    if (visibility == Visibility.ABSTRACT)
                    {
                        CompileError("An abstract attribute-set must contain no xsl:attribute instructions");
                    }
                }
                else
                {
                    CompileError("Only xsl:attribute is allowed within xsl:attribute-set", "XTSE0010");
                }
            }

            if (useAtt != null)
            {
                if (visibility == Visibility.ABSTRACT)
                {
                    CompileError("An abstract attribute-set must have no @use-attribute-sets attribute");
                }


                // identify any attribute sets that this one refers to
                useAttributeSetNames = GetUsedAttributeSets(useAtt);
            }

            validated = true;
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            top.IndexAttributeSet(decl);
        }

        // Whether this set reaches itself through the sets it uses: one walk with a visited set, where a recursion per
        // path overflowed the stack on a long chain and took exponential time on sets that share the ones they use.
        // Walked whole with no cycle found: no later walk need enter it (it cannot reach the set walking, or it would reach
        // itself). Each set walked every set it reached, so a chain of 10 000 took 15 s to compile.
        private bool acyclic;
        private bool gathered;   // attributeSetElements filled from use-attribute-sets

        private void CheckCircularity()
        {
            var seen = new HashSet<XSLAttributeSet>();
            bool whole = true;
            var todo = new Stack<XSLAttributeSet>();
            foreach (ComponentDeclaration used in attributeSetElements)
            {
                todo.Push((XSLAttributeSet)used.SourceElement);
            }

            while (todo.Count > 0)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
                XSLAttributeSet m = todo.Pop();
                if (m == this)
                {
                    CompileError("The definition of the attribute set is circular", "XTSE0720");
                    return;
                }

                // A set whose references are not gathered yet (its CompileDeclaration has not run) is not walked: the last
                // set of a cycle to gather them finds the cycle. Its empty list must not make the walk look whole.
                if (!m.validated || (m.useAtt != null && !m.gathered))
                {
                    whole = false;
                    continue;
                }

                if (m.acyclic || !seen.Add(m))
                {
                    continue;
                }

                foreach (ComponentDeclaration used in m.attributeSetElements)
                {
                    XSLAttributeSet element = (XSLAttributeSet)used.SourceElement;
                    todo.Push(element);
                    if (m.streamable && !element.streamable)
                    {
                        m.CompileError("Attribute-set is declared streamable but references a non-streamable attribute set " + element.AttributeSetName.DisplayName, "XTSE3430");
                    }
                }
            }

            acyclic = whole;
        }

        public SlotManager GetSlotManager()
        {
            return stackFrameMap;
        }

        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
            if (IsActionCompleted(ACTION_COMPILE))
            {
                return;
            }

            if (useAtt != null)
            {

                // identify any attribute sets that this one refers to
                IList<UseAttributeSet> invocations = UseAttributeSet.MakeUseAttributeSetInstructions(useAttributeSetNames, this);
                if (invocations.Count > 0)
                {
                    containedInstructions.Add(UseAttributeSet.MakeCompositeExpression(invocations));
                }


                // check for circularity, to the extent possible within a single package
                foreach (StructuredQName name in useAttributeSetNames)
                {
                    GetPrincipalStylesheetModule().GetAttributeSets(name, attributeSetElements);
                }

                gathered = true;
                CheckCircularity();


                // check for consistency of streamability attribute
                if (streamable)
                {
                    foreach (ComponentDeclaration attributeSetElement in attributeSetElements)
                    {
                        if (!((XSLAttributeSet)attributeSetElement.SourceElement).streamable)
                        {
                            CompileError("Attribute set is declared streamable, " + "but references an attribute set that is not declared streamable", "XTSE0730");
                        }
                    }
                }
            }

            XSLAttribute node;
            IAxisIterator iter = IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT);
            while ((node = (XSLAttribute)iter.Next()) != null)
            {
                Expression inst = node.Compile(compilation, decl);
                inst.SetRetainedStaticContext(MakeRetainedStaticContext());
                inst = inst.Simplify();
                SetInstructionLocation(this, inst);
                containedInstructions.Add(inst);
            }

            SetActionCompleted(ACTION_COMPILE);
        }

        public void Optimize(ComponentDeclaration declaration)
        {
        }
        Actor IStylesheetComponent.GetActor() => GetActor();
    }
}

