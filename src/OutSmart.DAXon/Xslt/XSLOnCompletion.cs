////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:on-completion element in the stylesheet (XSLT 3.0). <br>
    /// </summary>
    internal sealed class XSLOnCompletion : StyleElement
    {
        private Expression select;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("select"))
                {
                    string selectAtt = value;
                    select = MakeExpression(selectAtt, att);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            StyleElement parent = (StyleElement)GetParent();
            if (!(parent is XSLIterate))
            {
                CompileError("xsl:on-completion is not allowed as a child of " + parent.DisplayName, "XTSE0010");
            }

            IAxisIterator iter = IterateAxis(AxisInfo.PRECEDING_SIBLING, NodeKindTest.ELEMENT);
            NodeInfo sib;
            while ((sib = iter.Next()) != null)
            {
                if (!(sib is XSLFallback || sib is XSLLocalParam))
                {
                    CompileError("xsl:on-completion must be the first child of xsl:iterate after the xsl:param elements", "XTSE0010");
                }
            }

            if (select != null && IterateAxis(AxisInfo.CHILD).Next() != null)
            {
                CompileError("An xsl:on-completion element with a select attribute must be empty", "XTSE3125");
            }

            select = TypeCheck("select", select);
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (select == null)
            {
                return CompileSequenceConstructor(exec, decl, true);
            }
            else
            {
                return select;
            }
        }
    }
}