////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLValueOf : XSLLeafNodeConstructor
    {
        private bool disable = false;
        private Expression separator;

        protected override string ErrorCodeForSelectPlusContent => "XTSE0870";
        public override void PrepareAttributes()
        {
            string selectAtt = null;
            string disableAtt = null;
            string separatorAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "disable-output-escaping":
                        disableAtt = Whitespace.Trim(value);
                        break;
                    case "select":
                        selectAtt = value;
                        select = MakeExpression(selectAtt, att);
                        break;
                    case "separator":
                        separatorAtt = value;
                        separator = MakeAttributeValueTemplate(separatorAtt, att);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (disableAtt != null)
            {
                disable = ProcessBooleanAttribute("disable-output-escaping", disableAtt);
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            base.Validate(decl);
            select = TypeCheck("select", select);
            separator = TypeCheck("separator", separator);
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            Configuration config = GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            if (separator == null && select != null && XPath10ModeIsEnabled())
            {

                // Handle XSLT 1.0 backwards compatibility
                select = config.GetTypeChecker(true).ProcessValueOf(select, config);
            }
            else
            {
                if (separator == null)
                {
                    if (select == null)
                    {
                        separator = new StringLiteral(StringValue.EMPTY_STRING);
                    }
                    else
                    {
                        separator = new StringLiteral(StringValue.SINGLE_SPACE);
                    }
                }
            }

            ValueOf inst = new ValueOf(select, disable, false);
            inst.SetRetainedStaticContext(MakeRetainedStaticContext());
            CompileContent(exec, decl, inst, separator);
            return inst.WithLocation(SaveLocation());
        }
    }
}