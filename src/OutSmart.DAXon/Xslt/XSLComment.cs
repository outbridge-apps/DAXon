////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:comment elements in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLComment : XSLLeafNodeConstructor
    {

        protected override string ErrorCodeForSelectPlusContent => "XTSE0940";
        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("select"))
                {
                    select = MakeExpression(value, att);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            select = TypeCheck("select", select);
            base.Validate(decl);
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            Comment inst = new Comment();
            CompileContent(exec, decl, inst, new StringLiteral(StringValue.SINGLE_SPACE));
            return inst.WithLocation(SaveLocation());
        }
    }
}