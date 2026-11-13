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
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLDocument : StyleElement
    {
        private int validationAction = Validation.STRIP;
        private ISchemaType schemaType;
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
            string validationAtt = null;
            string typeAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("validation"))
                {
                    validationAtt = Whitespace.Trim(value);
                }
                else if (f.Equals("type"))
                {
                    typeAtt = Whitespace.Trim(value);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            validationAction = ValidationAction(validationAtt, typeAtt, out schemaType);
        }

        public override void Validate(ComponentDeclaration decl)
        {
        }

        //
        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            DocumentInstr inst = new DocumentInstr(false, null);
            inst.SetValidationAction(validationAction, schemaType);
            Expression b = CompileSequenceConstructor(exec, decl, true);
            if (b == null)
            {
                b = Literal.MakeEmptySequence();
            }

            inst.SetContentExpression(b);
            inst.SetLocation(AllocateLocation());
            return inst;
        }
    }
}