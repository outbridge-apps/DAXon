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
    /// An xsl:where-populated element in the stylesheet.
    /// </summary>
    internal sealed class XSLWherePopulated : StyleElement
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
                CheckUnknownAttribute(attName);
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
        }

        // no action
        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (select == null)
            {
                select = CompileSequenceConstructor(exec, decl, false);
            }

            return new WherePopulated(select);
        }
    }
}