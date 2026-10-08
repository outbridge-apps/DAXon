////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:sequence element in the stylesheet.
    /// </summary>
    internal class XSLSequence : StyleElement
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

        protected override bool MayContainFallback()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
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
            foreach (NodeInfo child in Children())
            {
                if (!(child is XSLFallback))
                {
                    if (select != null)
                    {
                        CompileError("An " + DisplayName + " element with a select attribute must be empty", "XTSE3185");
                    }

                    break;
                }
            }

            select = TypeCheck("select", select);
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (select == null)
            {
                select = CompileSequenceConstructor(exec, decl, false);
            }

            if (GetConfiguration().GetBooleanProperty(Feature<bool>.STRICT_STREAMABILITY))
            {
                select = new SequenceInstr(select);
            }

            return select;
        }
    }
}