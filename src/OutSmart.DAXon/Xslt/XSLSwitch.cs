////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:switch element in the stylesheet (XSLT 4.0).
    /// </summary>
    internal sealed class XSLSwitch : XSLChooseOrSwitch
    {
        private Expression select;
        private LetExpression switchVar;
        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("select"))
                {
                    select = MakeExpression(value, att);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (select == null)
            {
                ReportAbsence("select");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            RequireXslt40Element();
            select = TypeCheck("select", select);
            base.Validate(decl);
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            LetExpression var = new LetExpression();
            var.SetVariableQName(new StructuredQName("vv", NamespaceUri.SAXON_GENERATED_VARIABLE, "v" + GetHashCode()));
            var.Sequence = select;
            var.SetRequiredType(SequenceType.SINGLE_ATOMIC); // TODO type coercion
            switchVar = var;
            Expression choose = base.Compile(exec, decl);
            switchVar.SetAction(choose);
            return switchVar;
        }

        protected override void CompileConditions(Compilation exec, ComponentDeclaration decl, Expression[] conditions)
        {
            int w = 0;
            foreach (NodeInfo curr in Children())
            {
                if (curr is XSLWhen)
                {
                    Expression values = ((XSLWhen)curr).Condition;
                    conditions[w] = new GeneralComparison20(new LocalVariableReference(switchVar), Token.EQUALS, values);
                    w++;
                }
                else if (curr is XSLOtherwise)
                {
                    Expression otherwise = Literal.MakeLiteral(BooleanValue.TRUE);
                    otherwise.SetRetainedStaticContext(MakeRetainedStaticContext());
                    conditions[w] = otherwise;
                    w++;
                }
            }
        }
    }
}