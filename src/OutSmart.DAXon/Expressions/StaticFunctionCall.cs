////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using System;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    public class StaticFunctionCall : FunctionCall, ICallable
    {
        private readonly IFunctionItem target;

        public virtual IFunctionItem TargetFunction => target;

        public override string ExpressionName => "staticFunctionCall";
        public StaticFunctionCall(IFunctionItem target, Expression[] arguments)
        {
            if (target.GetArity() != arguments.Length)
            {
                throw new ArgumentException("Function call to " + target.GetFunctionName() + " with wrong number of arguments (" + arguments.Length + ")");
            }

            this.target = target;
            SetOperanda(arguments, target.OperandRoles);
        }

        public override IFunctionItem GetTargetFunction(IXPathContext context)
        {
            return TargetFunction;
        }

        public override StructuredQName GetFunctionName()
        {
            return target.GetFunctionName();
        }

        public override bool IsCallOn(System.Type function)
        {
            return function.IsAssignableFrom(target.GetType());
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            CheckFunctionCall(target, visitor);
            return base.TypeCheck(visitor, contextInfo);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Expression[] args = new Expression[GetArity()];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = GetArg(i).Copy(rebindings);
            }

            return new StaticFunctionCall(target, args);
        }

        protected override int ComputeCardinality()
        {
            return target.FunctionItemType.ResultType.GetCardinality();
        }

        public override ItemType GetItemType()
        {
            return target.FunctionItemType.ResultType.PrimaryType;
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            UType result = GetItemType().GetUType();
            foreach (Operand o in Operands())
            {
                if (o.Usage == OperandUsage.TRANSMISSION)
                {
                    result = result.Intersection(o.GetChildExpression().GetStaticUType(contextItemType));
                }
            }

            return result;
        }

        public virtual ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return target.Call(context, arguments);
        }

        public override void Export(ExpressionPresenter @out)
        {
            if (target is OriginalFunction)
            {
                ExpressionPresenter.ExportOptions options = @out.GetOptions();
                OriginalFunction pf = (OriginalFunction)target;
                @out.StartElement("origFC", this);
                @out.EmitAttribute("name", pf.GetFunctionName());
                @out.EmitAttribute("pack", options.packageMap.GetOrDefault(pf.GetComponent().ContainingPackage) + "");
                foreach (Operand o in Operands())
                {
                    o.GetChildExpression().Export(@out);
                }

                @out.EndElement();
            }
            else
            {
                if (target is UnionCastableFunction)
                {

                    // Bug 2611. Bug 3822.
                    IUnionType targetType = ((UnionConstructorFunction)target).TargetType;
                    @out.StartElement("castable", this);
                    if (targetType is LocalUnionType)
                    {
                        @out.EmitAttribute("to", AlphaCode.FromItemType(targetType));
                    }
                    else
                    {
                        @out.EmitAttribute("as", targetType.ToExportString());
                    }

                    @out.EmitAttribute("flags", "u" + (((UnionConstructorFunction)target).IsAllowEmpty() ? "e" : ""));
                    foreach (Operand o in Operands())
                    {
                        o.GetChildExpression().Export(@out);
                    }

                    @out.EndElement();
                }
                else if (target is ListCastableFunction)
                {

                    // Bug 2611. Bug 3822.
                    @out.StartElement("castable", this);
                    @out.EmitAttribute("as", ((ListConstructorFunction)target).TargetType.GetStructuredQName());
                    @out.EmitAttribute("flags", "l" + (((ListConstructorFunction)target).IsAllowEmpty() ? "e" : ""));
                    foreach (Operand o in Operands())
                    {
                        o.GetChildExpression().Export(@out);
                    }

                    @out.EndElement();
                }
                else if (target is UnionConstructorFunction)
                {

                    // Bug 2611.
                    IUnionType targetType = ((UnionConstructorFunction)target).TargetType;
                    @out.StartElement("cast", this);
                    if (targetType is LocalUnionType)
                    {
                        @out.EmitAttribute("to", AlphaCode.FromItemType(targetType));
                    }
                    else
                    {
                        @out.EmitAttribute("as", targetType.ToExportString());
                    }

                    @out.EmitAttribute("flags", "u" + (((UnionConstructorFunction)target).IsAllowEmpty() ? "e" : ""));
                    foreach (Operand o in Operands())
                    {
                        o.GetChildExpression().Export(@out);
                    }

                    @out.EndElement();
                }
                else if (target is ListConstructorFunction)
                {

                    // Bug 2611.
                    @out.StartElement("cast", this);
                    @out.EmitAttribute("as", ((ListConstructorFunction)target).TargetType.GetStructuredQName());
                    @out.EmitAttribute("flags", "l" + (((ListConstructorFunction)target).IsAllowEmpty() ? "e" : ""));
                    foreach (Operand o in Operands())
                    {
                        o.GetChildExpression().Export(@out);
                    }

                    @out.EndElement();
                }
                else
                {
                    base.Export(@out);
                }
            }
        }

        public override Elaborator GetElaborator()
        {
            return new StaticFunctionCallElaborator();
        }

        private sealed class StaticFunctionCallElaborator : FunctionCallElaborator
        {
            public override void SetExpression(Expression expr)
            {
                base.SetExpression(expr);
                AllocateArgumentEvaluators((FunctionCall)expr, true);
            }

            public override IPullEvaluator ElaborateForPull()
            {
                StaticFunctionCall expr = (StaticFunctionCall)GetExpression();
                return (context) => expr.Call(context, EvaluateArguments(context)).Iterate();
            }

            public override IItemEvaluator ElaborateForItem()
            {
                StaticFunctionCall expr = (StaticFunctionCall)GetExpression();
                return (context) => expr.Call(context, EvaluateArguments(context)).Head();
            }
        }
    }
}