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
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLIf : StyleElement
    {
        private Expression test;
        private Expression thenExp;
        private Expression elseExp;
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
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                switch (f)
                {
                    case "test":
                        test = MakeExpression(att.Value, att);
                        break;
                    case "then":

                        // Saxon extension
                        if (RequireXslt40Attribute("then"))
                        {
                            thenExp = MakeExpression(att.Value, att);
                        }

                        break;
                    case "else":

                        // Saxon extension
                        if (RequireXslt40Attribute("else"))
                        {
                            elseExp = MakeExpression(att.Value, att);
                        }

                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (test == null)
            {
                ReportAbsence("test");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            test = TypeCheck("test", test);
            thenExp = TypeCheck("then", thenExp);
            elseExp = TypeCheck("else", elseExp);
            if (thenExp != null && HasChildNodes())
            {
                CompileError("xsl:if element must be empty if @then is present", "XTSE0010");
            }
        }

        public override bool MarkTailCalls()
        {
            StyleElement last = LastChildInstruction;
            return last != null && last.MarkTailCalls();
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (test is Literal)
            {
                IGroundedValue testVal = ((Literal)test).GroundedValue;

                // condition known statically, so we only need compile the code if true.
                // This can happen with expressions such as test="function-available('abc')".
                try
                {
                    if (testVal.EffectiveBooleanValue())
                    {
                        return CompileSequenceConstructor(exec, decl, true);
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (XPathException err)
                {
                }
            }

            Expression action = CompileSequenceConstructor(exec, decl, true);
            if (action == null)
            {
                return null;
            }

            Expression[] conditions;
            Expression[] actions;
            if (elseExp == null)
            {
                conditions = new Expression[]
                {
                    test
                };
                actions = new Expression[]
                {
                    action
                };
            }
            else
            {
                conditions = new Expression[]
                {
                    test,
                    Literal.MakeLiteral(BooleanValue.TRUE)
                };
                actions = new Expression[]
                {
                    action,
                    elseExp
                };
            }

            Choose choose = new Choose(conditions, actions);
            choose.SetInstruction(true);
            choose.SetLocation(SaveLocation());
            return choose;
        }

        // fall through to non-optimizing case
        public override Expression CompileSequenceConstructor(Compilation compilation, ComponentDeclaration decl, bool includeParams)
        {
            if (thenExp == null)
            {
                return base.CompileSequenceConstructor(compilation, decl, includeParams);
            }
            else
            {
                return thenExp;
            }
        }
    }
}