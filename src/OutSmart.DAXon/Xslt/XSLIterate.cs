////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System.Collections.Generic;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:iterate elements in stylesheet. <br>
    /// </summary>
    internal sealed class XSLIterate : StyleElement
    {
        Expression select = null;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool IsPermittedChild(StyleElement child)
        {
            return child is XSLLocalParam || child is XSLOnCompletion;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override bool MayContainParam()
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
                switch (f)
                {
                    case "select":
                        select = MakeExpression(value, att);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (select == null)
            {
                select = Literal.MakeEmptySequence();
                ReportAbsence("select");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {

            select = TypeCheck("select", select);
            if (!HasChildNodes())
            {
                IssueWarning("An empty xsl:iterate instruction has no effect", DAXonErrorCode.SXWN9009);
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            IList<NodeInfo> nonFinallyChildren = new List<NodeInfo>();
            Expression finallyExp = null;
            IList<XSLLocalParam> @params = new List<XSLLocalParam>();
            foreach (NodeInfo node in Children())
            {
                if (node is XSLLocalParam)
                {
                    @params.Add((XSLLocalParam)node);
                }
                else if (node is XSLOnCompletion)
                {
                    finallyExp = ((XSLOnCompletion)node).Compile(exec, decl);
                }
                else
                {
                    nonFinallyChildren.Add(node);
                }
            }

            LocalParam[] compiledParams = new LocalParam[@params.Count];
            for (int i = 0; i < @params.Count; i++)
            {
                compiledParams[i] = (LocalParam)@params[i].Compile(exec, decl);
                if (compiledParams[i].IsImplicitlyRequiredParam())
                {

                    // see spec bug 25158; Saxon bug 2041
                    CompileError("The parameter must be given an initial value because () is not valid, given the declared type", "XTSE3520");
                }
            }

            LocalParamBlock paramBlock = new LocalParamBlock(compiledParams);
            Expression action = CompileSequenceConstructor(exec, decl, new NodeListIterator(nonFinallyChildren), false);
            if (action == null)
            {

                // body of xsl:iterate is empty: it's a no-op.
                return Literal.MakeEmptySequence();
            }

            try
            {
                action = action.Simplify();
                return new IterateInstr(select, paramBlock, action, finallyExp).WithLocation(SaveLocation());
            }
            catch (XPathException err)
            {
                CompileError(err);
                return null;
            }
        }
    }
}