////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:map-entry instructions in an XSLT 3.0 stylesheet. <br>
    /// </summary>
    internal sealed class XSLMapEntry : StyleElement
    {
        Expression key;
        Expression select;
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
            string keyAtt = null;
            string selectAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("key"))
                {
                    keyAtt = value;
                    key = MakeExpression(keyAtt, att);
                }
                else if (f.Equals("select"))
                {
                    selectAtt = value;
                    select = MakeExpression(selectAtt, att);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (keyAtt == null)
            {
                ReportAbsence("key");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            key = TypeCheck("key", key);
            select = TypeCheck("select", select);
            if (select != null)
            {
                foreach (NodeInfo kid in Children())
                {
                    if (!(kid is XSLFallback))
                    {
                        CompileError("An xsl:map-entry element with a select attribute must be empty", "XTSE3280");
                        return;
                    }
                }
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (select == null)
            {
                select = CompileSequenceConstructor(exec, decl, false);
                select = select.Simplify();
            }

            Expression exp = MapFunctionSet.GetInstance(31).MakeFunction("entry", 2).MakeFunctionCall(key, select);
            if (GetConfiguration().GetBooleanProperty(Feature<bool>.STRICT_STREAMABILITY))
            {
                exp = new SequenceInstr(exp);
            }

            return exp;
        }
    }
}