////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:processing-instruction element in the stylesheet.
    /// </summary>
    internal sealed class XSLProcessingInstruction : XSLLeafNodeConstructor
    {
        Expression name;

        protected override string ErrorCodeForSelectPlusContent => "XTSE0880";
        public override void PrepareAttributes()
        {
            name = PrepareAttributesNameAndSelect();
        }

        public override void Validate(ComponentDeclaration decl)
        {
            name = TypeCheck("name", name);
            select = TypeCheck("select", select);
            base.Validate(decl);
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            ProcessingInstruction inst = new ProcessingInstruction(name);
            CompileContent(exec, decl, inst, new StringLiteral(StringValue.SINGLE_SPACE));
            return inst.WithLocation(SaveLocation());
        }
    }
}