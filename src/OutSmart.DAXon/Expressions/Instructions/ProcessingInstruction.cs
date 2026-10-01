////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class ProcessingInstruction : SimpleNodeConstructor
    {

        private static readonly UnicodeString PI_TERMINATOR = new Twine8(StringConstants.PI_END);
        private readonly Operand nameOp;

        public Expression NameExp
        {
            get => nameOp.GetChildExpression(); set
            {
                nameOp.SetChildExpression(value);
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_PROCESSING_INSTRUCTION;

        public override int Dependencies => NameExp.Dependencies | base.Dependencies;
        public ProcessingInstruction(Expression name)
        {
            nameOp = new Operand(this, name, OperandRole.SINGLE_ATOMIC);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(selectOp, nameOp);
        }

        public override Types.ItemType GetItemType()
        {
            return NodeKindTest.PROCESSING_INSTRUCTION;
        }

        public override int GetCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ProcessingInstruction exp = new ProcessingInstruction(NameExp.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            exp.Select = Select.Copy(rebindings);
            return exp;
        }

        public override void LocalTypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            IStaticContext env = visitor.StaticContext;
            nameOp.TypeCheck(visitor, contextItemType);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "processing-instruction/name", 0);

            // See bug 2110. XQuery does not use the function conversion rules here, and disallows xs:anyURI.
            // In XSLT the name is an AVT so we automatically get a string; in XQuery we'll use the standard
            // mechanism to get an atomic value, and then check the type "by hand" at run time.
            NameExp = visitor.GetConfiguration().GetTypeChecker(false).StaticTypeCheck(NameExp, Values.SequenceType.SINGLE_ATOMIC, role, visitor);
            Expression nameExp = NameExp;
            AdoptChildExpression(nameExp);

            // Do early checking of name if known statically
            if (nameExp is Literal && ((Literal)nameExp).GroundedValue is AtomicValue)
            {
                AtomicValue val = (AtomicValue)((Literal)nameExp).GroundedValue;
                CheckName(val, env.MakeEarlyEvaluationContext());
            }


            // Do early checking of content if known statically
            if (Select is StringLiteral)
            {
                UnicodeString s = ((StringLiteral)Select).GroundedValue.UnicodeStringValue;
                UnicodeString s2 = CheckContent(s, env.MakeEarlyEvaluationContext());
                if (!s2.Equals(s))
                {
                    Select = new StringLiteral(s2);
                }
            }
        }

        public override void ProcessValue(UnicodeString value, Outputter output, IXPathContext context)
        {
            string expandedName = EvaluateName(context);
            UnicodeString data = CheckContent(value, context);
            output.ProcessingInstruction(expandedName, data, GetLocation(), ReceiverOption.NONE);
        }

        public override UnicodeString CheckContent(UnicodeString data, IXPathContext context)
        {
            if (IsXSLT())
            {
                return CheckContentXSLT(data);
            }
            else
            {
                try
                {
                    return CheckContentXQuery(data);
                }
                catch (XPathException err)
                {
                    throw err.WithLocation(GetLocation()).WithXPathContext(context);
                }
            }
        }

        public static UnicodeString CheckContentXSLT(UnicodeString data)
        {
            long hh;
            while ((hh = data.IndexOf(PI_TERMINATOR, 0)) >= 0)
            {
                data = data.Substring(0, hh + 1).Concat(StringConstants.SINGLE_SPACE.Concat(data.Substring(hh + 1)));
            }

            return Whitespace.RemoveLeadingWhitespace(data);
        }
        public static UnicodeString CheckContentXQuery(UnicodeString data)
        {
            if (data.IndexOf(PI_TERMINATOR, 0) >= 0)
            {
                throw new XPathException("Invalid characters (?>) in processing instruction", "XQDY0026");
            }

            return Whitespace.RemoveLeadingWhitespace(data);
        }

        public override INodeName EvaluateNodeName(IXPathContext context)
        {
            string expandedName = EvaluateName(context);
            return new NoNamespaceName(expandedName);
        }

        private string EvaluateName(IXPathContext context)
        {
            AtomicValue av = (AtomicValue)NameExp.EvaluateItem(context);
            if (av is StringValue && !(av is AnyURIValue))
            {

                // Always true under XSLT
                return CheckName(av, context);
            }
            else
            {
                XPathException e = new XPathException("Processing instruction name is not a string").WithXPathContext(context).WithErrorCode("XPTY0004");
                throw DynamicError(GetLocation(), e, context);
            }
        }

        public string CheckName(AtomicValue name, IXPathContext context)
        {
            if (name is StringValue && !(name is AnyURIValue))
            {
                string expandedName = Whitespace.Trim(name.GetStringValue());
                if (!NameChecker.IsValidNCName(expandedName))
                {
                    XPathException e = new XPathException("Processing instruction name " + Err.Wrap(expandedName) + " is not a valid NCName").WithXPathContext(context).WithErrorCode(IsXSLT() ? "XTDE0890" : "XQDY0041");
                    throw DynamicError(GetLocation(), e, context);
                }

                if (expandedName.Equals("xml", global::System.StringComparison.OrdinalIgnoreCase))
                {
                    XPathException e = new XPathException("Processing instructions cannot be named 'xml' in any combination of upper/lower case").WithXPathContext(context).WithErrorCode(IsXSLT() ? "XTDE0890" : "XQDY0064");
                    throw DynamicError(GetLocation(), e, context);
                }

                return expandedName;
            }
            else
            {
                XPathException e = new XPathException("Processing instruction name " + Err.Wrap(name.UnicodeStringValue) + " is not of type xs:string or xs:untypedAtomic").WithXPathContext(context).WithErrorCode("XPTY0004").AsTypeError();
                throw DynamicError(GetLocation(), e, context);
            }
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("procInst", this);
            if (IsLocal())
            {
                @out.EmitAttribute("flags", "l");
            }

            @out.SetChildRole("name");
            NameExp.Export(@out);
            @out.SetChildRole("select");
            Select.Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new ProcessingInstructionElaborator();
        }

        private sealed class ProcessingInstructionElaborator : SimpleNodePushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                ProcessingInstruction expr = (ProcessingInstruction)GetExpression();
                ILocation loc = expr.GetLocation();
                IUnicodeStringEvaluator contentEval = expr.Select.MakeElaborator().ElaborateForUnicodeString(true);
                IItemEvaluator nameEval = expr.NameExp.MakeElaborator().ElaborateForItem();
                if (expr.IsXSLT())
                {
                    return (@out, context) =>
                    {
                        StringValue name = (StringValue)nameEval.Eval(context);
                        string checkedName = expr.CheckName(name, context);
                        UnicodeString content = contentEval.Eval(context);
                        content = ProcessingInstruction.CheckContentXSLT(content);
                        @out.ProcessingInstruction(checkedName, content, loc, ReceiverOption.NONE);
                        return null;
                    };
                }
                else
                {
                    return (@out, context) =>
                    {
                        AtomicValue name = (AtomicValue)nameEval.Eval(context);
                        string checkedName = expr.CheckName(name, context);
                        UnicodeString content = contentEval.Eval(context);
                        ProcessingInstruction.CheckContentXQuery(content);
                        @out.ProcessingInstruction(checkedName, content, loc, ReceiverOption.NONE);
                        return null;
                    };
                }
            }
        }
    }
}