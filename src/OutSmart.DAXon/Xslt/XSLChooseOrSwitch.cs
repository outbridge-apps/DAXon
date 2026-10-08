////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:choose or xsl:switch element in the stylesheet.
    /// </summary>
    internal abstract class XSLChooseOrSwitch : StyleElement
    {
        private StyleElement otherwise;
        private int numberOfWhens = 0;
        public override bool IsInstruction()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                CheckUnknownAttribute(attName);
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            XSLFallback fallback = null;
            foreach (NodeInfo curr in Children())
            {
                if (curr is XSLWhen)
                {
                    if (otherwise != null)
                    {
                        otherwise.CompileError("xsl:otherwise must come last", "XTSE0010");
                    }
                    else if (fallback != null)
                    {
                        fallback.CompileError("xsl:fallback must come last", "XTSE0010");
                    }

                    numberOfWhens++;
                }
                else if (curr is XSLOtherwise)
                {
                    if (otherwise != null)
                    {
                        ((XSLOtherwise)curr).CompileError("Only one xsl:otherwise is allowed in an " + DisplayName, "XTSE0010");
                    }
                    else if (fallback != null)
                    {
                        fallback.CompileError("xsl:fallback must come last", "XTSE0010");
                    }
                    else
                    {
                        otherwise = (StyleElement)curr;
                    }
                }
                else if (curr is XSLFallback && this is XSLSwitch)
                {
                    fallback = (XSLFallback)curr;
                }
                else if (curr is StyleElement)
                {
                    ((StyleElement)curr).CompileError("Only xsl:when and xsl:otherwise are allowed here", "XTSE0010");
                }
                else
                {
                    CompileError("Only xsl:when and xsl:otherwise are allowed within " + DisplayName, "XTSE0010");
                }
            }

            if (numberOfWhens == 0)
            {
                CompileError(DisplayName + " must contain at least one xsl:when", "XTSE0010");
            }
        }

        public override bool MarkTailCalls()
        {
            bool found = false;
            foreach (NodeInfo curr in Children(new TypeIsInstancePredicate(typeof(StyleElement))))
            {
                found |= ((StyleElement)curr).MarkTailCalls();
            }

            return found;
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            int entries = numberOfWhens + (otherwise == null ? 0 : 1);
            Expression[] conditions = new Expression[entries];
            Expression[] actions = new Expression[entries];
            CompileActions(exec, decl, actions);
            CompileConditions(exec, decl, conditions);
            Choose choose = new Choose(conditions, actions);
            choose.SetInstruction(true);
            choose.SetLocation(SaveLocation());
            return choose;
        }

        protected abstract void CompileConditions(Compilation exec, ComponentDeclaration decl, Expression[] conditions);
        protected virtual void CompileActions(Compilation exec, ComponentDeclaration decl, Expression[] actions)
        {
            int w = 0;
            foreach (NodeInfo curr in Children())
            {
                if (curr is XSLWhen || curr is XSLOtherwise)
                {
                    Expression b = ((StyleElement)curr).CompileSequenceConstructor(exec, decl, true);
                    if (b == null)
                    {
                        b = Literal.MakeEmptySequence();
                        b.SetRetainedStaticContext(MakeRetainedStaticContext());
                    }

                    try
                    {
                        b = b.Simplify();
                        actions[w] = b;
                    }
                    catch (XPathException e)
                    {
                        CompileError(e);
                    }

                    SetInstructionLocation((StyleElement)curr, actions[w]);
                    w++;
                }
            }
        }
    }
}