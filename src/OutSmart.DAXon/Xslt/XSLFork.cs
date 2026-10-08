////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:fork elements in XSLT 3.0 stylesheet.
    /// </summary>
    internal sealed class XSLFork : StyleElement
    {
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return false;
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
            int foundGroup = 0;
            int foundSequence = 0;
            foreach (NodeInfo child in Children())
            {
                if (child is XSLSequence)
                {
                    foundSequence++;
                }
                else if (child is XSLForEachGroup)
                {
                    foundGroup++;
                }
                else if (child is XSLFallback)
                {
                }
                else
                {
                    CompileError(child.DisplayName + " cannot appear as a child of xsl:fork");
                }
            }

            if (foundGroup > 1)
            {
                CompileError("xsl:fork contains more than one xsl:for-each-group instruction");
            }

            if (foundGroup > 0 && foundSequence > 0)
            {
                CompileError("Cannot mix xsl:sequence and xsl:for-each-group within xsl:fork");
            }
        }

        // no action
        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            Expression content = CompileSequenceConstructor(exec, decl, true);
            if (content is Block)
            {
                return new Fork(((Block)content).GetOperanda()).WithLocation(SaveLocation());
            }
            else
            {
                return content;
            }
        }
    }
}