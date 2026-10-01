////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// A xsl:break element in the stylesheet
    /// </summary>
    internal sealed class XSLBreak : XSLBreakOrContinue
    {
        private Expression select;
        public override void PrepareAttributes()
        {
            string selectAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("select"))
                {
                    selectAtt = value;
                    select = MakeExpression(selectAtt, att);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            ValidatePosition();
            if (xslIterate == null)
            {
                CompileError(DisplayName + " must be a descendant of an xsl:iterate instruction", "XTSE3120"); //XTSE0010
            }

            if (select != null && HasChildNodes())
            {
                CompileError("An xsl:break element with a select attribute must be empty", "XTSE3125");
            }

            select = TypeCheck("select", select);
        }

        //XTSE0010
        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {

            // xsl:break containing a sequence constructor is compiled into a call on the sequence constructor, then
            // the break instruction
            Expression val = select;
            if (val == null)
            {
                val = CompileSequenceConstructor(exec, decl, false);
            }

            Expression brake = new BreakInstr().WithLocation(SaveLocation());
            brake.SetRetainedStaticContext(MakeRetainedStaticContext());
            return Block.MakeBlock(val, brake);
        }
    }
}