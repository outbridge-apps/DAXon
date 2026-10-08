////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:next-match element in the stylesheet
    /// </summary>
    internal sealed class XSLNextMatch : StyleElement
    {
        private bool useTailRecursion = false;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainFallback()
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
            foreach (NodeInfo child in Children())
            {
                if (child is XSLWithParam || child is XSLFallback)
                {
                }
                else if (child.GetNodeKind() == Types.Type.TEXT)
                {

                    // with xml:space=preserve, white space nodes may still be there
                    if (!Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {
                        CompileError("No character data is allowed within xsl:next-match", "XTSE0010");
                    }
                }
                else
                {
                    CompileError("Child element " + child.DisplayName + " is not allowed as a child of xsl:next-match", "XTSE0010");
                }
            }
        }

        // OK;
        public override bool MarkTailCalls()
        {
            useTailRecursion = true;
            return true;
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            NextMatch inst = new NextMatch(useTailRecursion);
            inst.SetLocation(AllocateLocation());
            inst.SetActualParams(GetWithParamInstructions(inst, exec, decl, false));
            inst.SetTunnelParams(GetWithParamInstructions(inst, exec, decl, true));
            return inst;
        }
    }
}