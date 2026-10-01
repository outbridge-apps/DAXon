////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    internal sealed class PartialApply : Expression
    {
        private readonly Operand baseOp;
        private readonly Operand[] boundArgumentsOp; // contains null where the question marks appear

        public Expression BaseExpression
        {
            get => baseOp.GetChildExpression(); set
            {
                baseOp.SetChildExpression(value);
            }
        }

        public int NumberOfPlaceHolders
        {
            get
            {
                int n = 0;
                foreach (Operand o in boundArgumentsOp)
                {
                    if (o == null)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public override int ImplementationMethod => EVALUATE_METHOD;

        //
        public override string ExpressionName => "partialApply";
        public PartialApply(Expression @base, Expression[] boundArguments)
        {
            baseOp = new Operand(this, @base, OperandRole.INSPECT);
            AdoptChildExpression(@base);
            boundArgumentsOp = new Operand[boundArguments.Length];
            for (int i = 0; i < boundArguments.Length; i++)
            {
                if (boundArguments[i] != null)
                {
                    boundArgumentsOp[i] = new Operand(this, boundArguments[i], OperandRole.NAVIGATE);
                    AdoptChildExpression(boundArguments[i]);
                }
            }
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            ItemType baseType = BaseExpression.GetItemType();
            SequenceType requiredFunctionType;
            SequenceType[] argTypes = new SequenceType[boundArgumentsOp.Length];
            ArrayTools.Fill(argTypes, SequenceType.ANY_SEQUENCE);
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
            for (int i = 0; i < boundArgumentsOp.Length; i++)
            {
                Operand op = boundArgumentsOp[i];
                if (op != null)
                {
                    Expression arg = op.GetChildExpression();
                    if (baseType is SpecificFunctionType && i < ((SpecificFunctionType)baseType).GetArity())
                    {
                        int pos = i;
                        Func<RoleDiagnostic> argRole = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, "saxon:call", pos);
                        SequenceType requiredArgType = ((SpecificFunctionType)baseType).ArgumentTypes[i];
                        argTypes[i] = requiredArgType;
                        Expression a3 = tc.StaticTypeCheck(arg, requiredArgType, argRole, visitor);
                        if (a3 != arg)
                        {
                            op.SetChildExpression(a3);
                        }
                    }
                }
            }


            //        requiredFunctionType = SequenceType.makeSequenceType(
            //                new SpecificFunctionType(argTypes,
            //                        (baseType instanceof AnyFunctionType) ? ((AnyFunctionType) baseType).getResultType() : SequenceType.ANY_SEQUENCE),
            //                        StaticProperty.EXACTLY_ONE);
            //
            //        Func<RoleDiagnostic> role =
            //                () -> new RoleDiagnostic(RoleDiagnostic.FUNCTION, "saxon:call", 0);
            //        setBaseExpression(tc.staticTypeCheck(getBaseExpression(), requiredFunctionType, role, visitor));
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.DYNAMIC_FUNCTION, BaseExpression.ToShortString(), 0);
            BaseExpression = tc.StaticTypeCheck(BaseExpression, SequenceType.SINGLE_FUNCTION, role, visitor);
            return this;
        }

        //
        public override ItemType GetItemType()
        {
            ItemType baseItemType = BaseExpression.GetItemType();
            SequenceType resultType = SequenceType.ANY_SEQUENCE;
            if (baseItemType is SpecificFunctionType)
            {
                resultType = ((SpecificFunctionType)baseItemType).ResultType;
            }

            int placeholders = NumberOfPlaceHolders;
            SequenceType[] argTypes = new SequenceType[placeholders];
            if (baseItemType is SpecificFunctionType)
            {
                for (int i = 0, j = 0; i < boundArgumentsOp.Length; i++)
                {
                    if (boundArgumentsOp[i] == null)
                    {
                        argTypes[j++] = ((SpecificFunctionType)baseItemType).ArgumentTypes[i];
                    }
                }
            }
            else
            {
                ArrayTools.Fill(argTypes, SequenceType.ANY_SEQUENCE);
            }

            return new SpecificFunctionType(argTypes, resultType);
        }

        protected override int ComputeSpecialProperties()
        {
            return StaticProperty.COMPUTED_FUNCTION;
        }

        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> operanda = new List<Operand>(boundArgumentsOp.Length + 1);
            operanda.Add(baseOp);
            foreach (Operand o in boundArgumentsOp)
            {
                if (o != null)
                {
                    operanda.Add(o);
                }
            }

            return operanda;
        }

        public override bool Equals(object other)
        {
            if (!(other is PartialApply))
            {
                return false;
            }
            else
            {
                PartialApply pa2 = (PartialApply)other;
                if (!BaseExpression.IsEqual(pa2.BaseExpression))
                {
                    return false;
                }

                if (boundArgumentsOp.Length != pa2.boundArgumentsOp.Length)
                {
                    return false;
                }

                for (int i = 0; i < boundArgumentsOp.Length; i++)
                {
                    if ((boundArgumentsOp[i] == null) != (pa2.boundArgumentsOp[i] == null))
                    {
                        return false;
                    }

                    if (boundArgumentsOp[i] != null && !boundArgumentsOp[i].Equals(pa2.boundArgumentsOp[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        //
        protected override int ComputeHashCode()
        {
            int h = 0x236b92a0;
            int i = 0;
            foreach (Operand o in Operands())
            {
                h ^= o == null ? i++ : o.GetChildExpression().GetHashCode();
            }

            return h;
        }

        //
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("partialApply", this);
            BaseExpression.Export(@out);
            foreach (Operand o in boundArgumentsOp)
            {
                if (o == null)
                {
                    @out.StartElement("null", this);
                    @out.EndElement();
                }
                else
                {
                    o.GetChildExpression().Export(@out);
                }
            }

            @out.EndElement();
        }

        //
        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        //
        public override Expression Copy(RebindingMap rebindings)
        {
            Expression[] boundArgumentsCopy = new Expression[boundArgumentsOp.Length];
            for (int i = 0; i < boundArgumentsOp.Length; i++)
            {
                if (boundArgumentsOp[i] == null)
                {
                    boundArgumentsCopy[i] = null;
                }
                else
                {
                    boundArgumentsCopy[i] = boundArgumentsOp[i].GetChildExpression().Copy(rebindings);
                }
            }

            PartialApply exp = new PartialApply(BaseExpression.Copy(rebindings), boundArgumentsCopy);
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        //
        public override string ToString()
        {
            StringBuilder buff = new StringBuilder(64);
            bool par = BaseExpression.Operands().Any();
            if (par)
            {
                buff.Append("(" + BaseExpression.ToString() + ")");
            }
            else
            {
                buff.Append(BaseExpression.ToString());
            }

            buff.Append('(');
            for (int i = 0; i < boundArgumentsOp.Length; i++)
            {
                if (boundArgumentsOp[i] == null)
                {
                    buff.Append('?');
                }
                else
                {
                    buff.Append(boundArgumentsOp[i].GetChildExpression().ToString());
                }

                if (i != boundArgumentsOp.Length - 1)
                {
                    buff.Append(", ");
                }
            }

            buff.Append(')');
            return buff.ToString();
        }

        //
        // MUST be a true override of Expression.EvaluateItem (net472 has no covariant returns, so the
        // upstream `PartialApply evaluateItem` covariant signature became a SHADOW here): base-typed dispatch
        // (e.g. `declare context item := contains(?, 'e')` evaluating the initializer through
        // Expression.EvaluateItem) hit the base EvaluateItem<->Iterate mutual recursion -> StackOverflow.
        public override IItem EvaluateItem(IXPathContext context)
        {
            return (IFunctionItem)MakeElaborator().ElaborateForItem().Eval(context);
        }

        //
        public override Elaborator GetElaborator()
        {
            return new PartialApplyElaborator();
        }

        //
        private sealed class PartialApplyElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                PartialApply expr = (PartialApply)GetExpression();
                IItemEvaluator functionEval = expr.BaseExpression.MakeElaborator().ElaborateForItem();
                int len = expr.boundArgumentsOp.Length;
                ISequenceEvaluator[] boundArgumentsEvaluators = new ISequenceEvaluator[len];
                for (int i = 0; i < len; i++)
                {
                    if (expr.boundArgumentsOp[i] == null)
                    {
                        boundArgumentsEvaluators[i] = null;
                    }
                    else
                    {
                        boundArgumentsEvaluators[i] = expr.boundArgumentsOp[i].GetChildExpression().MakeElaborator().Eagerly();
                    }
                }

                return (context) =>
                {
                    IFunctionItem f = (IFunctionItem)functionEval.Eval(context);
                    if (f.GetArity() != len)
                    {
                        throw new XPathException("The number of arguments supplied in the partial function application is " + len + ", but the arity of the function item is " + f.GetArity(), "XPTY0004");
                    }

                    ISequence[] values = new ISequence[len];
                    for (int i = 0; i < boundArgumentsEvaluators.Length; i++)
                    {
                        if (boundArgumentsEvaluators[i] != null)
                        {
                            values[i] = boundArgumentsEvaluators[i].Evaluate(context);
                        }
                    }

                    return (IItem)new CurriedFunction(f, values);
                };
            }
        }
    }
}