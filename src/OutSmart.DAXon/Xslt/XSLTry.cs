////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using System.Collections.Generic;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLTry : StyleElement
    {
        private Expression select;
        private bool rollbackOutput = true;
        private readonly IList<IQNameTest> catchTests = new List<IQNameTest>();
        private readonly IList<Expression> catchExprs = new List<Expression>();
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
            string rollbackOutputAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "select":
                        string selectAtt = value;
                        select = MakeExpression(selectAtt, att);
                        break;
                    case "rollback-output":
                        rollbackOutputAtt = value;
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (rollbackOutputAtt != null)
            {
                rollbackOutput = ProcessBooleanAttribute("rollback-output", rollbackOutputAtt);
            }
        }

        protected override bool IsPermittedChild(StyleElement child)
        {
            return child is XSLCatch;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            select = TypeCheck("select", select);
            bool foundCatch = false;
            foreach (NodeInfo kid in Children())
            {
                if (kid is XSLCatch)
                {
                    foundCatch = true;
                }
                else if (kid is XSLFallback)
                {
                }
                else
                {
                    if (foundCatch)
                    {
                        CompileError("xsl:catch elements must come after all other children of xsl:try (excepting xsl:fallback)", "XTSE0010");
                    }

                    if (select != null)
                    {
                        CompileError("An " + DisplayName + " element with a select attribute must be empty", "XTSE3140");
                    }
                }
            }

            if (!foundCatch)
            {
                CompileError("xsl:try must have at least one xsl:catch child element", "XTSE0010");
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            Expression content = CompileSequenceConstructor(exec, decl, true);
            if (select == null)
            {
                select = content;
            }

            TryCatch expr = new TryCatch(select);
            for (int i = 0; i < catchTests.Count; i++)
            {
                expr.AddCatchExpression(catchTests[i], catchExprs[i]);
            }

            expr.SetRollbackOutput(rollbackOutput);
            return expr;
        }

        public void AddCatchClause(IQNameTest nameTest, Expression catchExpr)
        {
            catchTests.Add(nameTest);
            catchExprs.Add(catchExpr);
        }
    }
}