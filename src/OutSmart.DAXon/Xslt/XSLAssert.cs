////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using System;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:assert element in an XSLT 3.0 stylesheet.
    /// </summary>
    internal sealed class XSLAssert : StyleElement
    {
        private Expression test = null;
        private Expression select = null;
        private Expression errorCode = null;
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
            string testAtt = null;
            string selectAtt = null;
            string errorCodeAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "test":
                        testAtt = value;
                        test = MakeExpression(testAtt, att);
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
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (testAtt == null)
            {
                ReportAbsence("test");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            select = TypeCheck("select", select);
            test = TypeCheck("test", test);
            if (errorCode == null)
            {
                errorCode = new StringLiteral("Q{http://www.w3.org/2005/xqt-errors}XTMM9001");
            }
            else
            {
                errorCode = TypeCheck("error-code", errorCode);
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (exec.GetCompilerInfo().IsAssertionsEnabled())
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

                MessageInstr msg = new MessageInstr(select, new StringLiteral("yes"), errorCode);
                msg.SetIsAssert(true);
                if (!(errorCode is StringLiteral))
                {

                    // evaluation of the error code may need the namespace context
                    msg.SetRetainedStaticContext(MakeRetainedStaticContext());
                }

                Expression condition = SystemFunction.MakeCall("not", test.GetRetainedStaticContext(), test);
                return new Choose(new Expression[] { condition }, new Expression[] { msg });
            }
            else
            {

                // assertions are disabled (the default)
                return Literal.MakeEmptySequence();
            }
        }
    }
}