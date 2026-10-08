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
using System;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:message element in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLMessage : StyleElement
    {
        private Expression terminate = null;
        private Expression select = null;
        private Expression errorCode = null;
        private Expression timer = null;
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
            string terminateAtt = null;
            string selectAtt = null;
            string errorCodeAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                string value = att.Value;
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                switch (f)
                {
                    case "terminate":
                        terminateAtt = Whitespace.Trim(value);
                        terminate = MakeAttributeValueTemplate(terminateAtt, att);
                        break;
                    case "select":
                        selectAtt = value;
                        select = MakeExpression(selectAtt, att);
                        break;
                    case "error-code":
                        errorCodeAtt = value;
                        errorCode = MakeAttributeValueTemplate(errorCodeAtt, att);
                        break;
                    default:
                        if (attName.HasURI(NamespaceUri.SAXON) && attName.GetLocalPart().Equals("time"))
                        {
                            IsExtensionAttributeAllowed(attName.DisplayName);
                            bool timed = ProcessBooleanAttribute("saxon:time", value);
                            if (timed)
                            {
                                timer = MakeExpression("format-dateTime(Q{http://saxon.sf.net/}timestamp(),'[Y0001]-[M01]-[D01]T[H01]:[m01]:[s01].[f,3-3] - ')", att);
                            }
                        }
                        else
                        {
                            CheckUnknownAttribute(attName);
                        }

                        break;
                }
            }

            if (terminateAtt == null)
            {
                terminateAtt = "no";
                terminate = MakeAttributeValueTemplate(terminateAtt, null);
            }

            CheckAttributeValue("terminate", terminateAtt, true, StyleElement.YES_NO);
        }

        public override void Validate(ComponentDeclaration decl)
        {
            select = TypeCheck("select", select);
            terminate = TypeCheck("terminate", terminate);
            if (errorCode == null)
            {
                errorCode = new StringLiteral("Q{http://www.w3.org/2005/xqt-errors}XTMM9000");
            }
            else
            {
                errorCode = TypeCheck("error-code", errorCode);
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            Expression b = CompileSequenceConstructor(exec, decl, true);
            if (b != null)
            {
                if (select == null)
                {
                    select = b;
                }
                else
                {
                    select = Block.MakeBlock(select, b);
                    select.SetLocation(AllocateLocation());
                }
            }

            if (timer != null)
            {
                select = Block.MakeBlock(timer, select);
            }

            if (select == null)
            {
                select = new StringLiteral("xsl:message (no content)");
            }

            if (errorCode is StringLiteral)
            {

                // resolve any QName prefix now
                string code = ((StringLiteral)errorCode).Stringify();
                if (code.Contains(":") && !code.StartsWith("Q{", StringComparison.Ordinal))
                {
                    StructuredQName name = MakeQName(code, null, "error-code");
                    errorCode = new StringLiteral(name.EQName);
                }
            }

            MessageInstr m = new MessageInstr(select, terminate, errorCode);
            m.SetLocation(SaveLocation());
            m.SetRetainedStaticContext(MakeRetainedStaticContext());
            return m;
        }
    }
}