////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:text elements in stylesheet. <BR>
    /// </summary>
    internal sealed class XSLText : XSLLeafNodeConstructor
    {
        private bool disable;
        private StringValue value;

        protected override string ErrorCodeForSelectPlusContent => null;
        public override void PrepareAttributes()
        {
            string disableAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                if (f.Equals("disable-output-escaping"))
                {
                    disableAtt = Whitespace.Trim(value);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (disableAtt != null)
            {
                disable = ProcessBooleanAttribute("disable-output-escaping", disableAtt);
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            value = StringValue.EMPTY_STRING;
            foreach (NodeInfo child in Children())
            {
                if (child is StyleElement)
                {
                    ((StyleElement)child).CompileError("xsl:text must not contain child elements", "XTSE0010");
                    return;
                }
                else
                {
                    value = new StringValue(child.UnicodeStringValue); //continue;
                }
            }

            base.Validate(decl);
        }

        // not applicable
        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (IsExpandingText())
            {
                TextImpl child = (TextImpl)IterateAxis(AxisInfo.CHILD).Next();
                if (child != null)
                {
                    IList<Expression> contents = new List<Expression>(10);
                    CompileContentValueTemplate(child, contents);
                    Expression block = Block.MakeBlock(contents);
                    block.SetLocation(AllocateLocation());
                    return block;
                }
                else
                {
                    return new ValueOf(new StringLiteral(StringValue.EMPTY_STRING), disable, false);
                }
            }
            else
            {
                return new ValueOf(Literal.MakeLiteral(value), disable, false);
            }
        }
    }
}