////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    public class QuantifiedExpression : Assignation
    {
        private int @operator; // Token.SOME or Token.EVERY
        public override string ExpressionName => Token.tokens[@operator];

        public virtual int Operator
        {
            get => @operator; set
            {
                this.@operator = value;
            }
        }

        public override int ImplementationMethod => EVALUATE_METHOD;

        /// <summary>
        /// Determine the static cardinality
        /// </summary>
        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {

            // The order of events is critical here. First we ensure that the type of the
            // sequence expression is established. This is used to establish the type of the variable,
            // which in turn is required when type-checking the action part.
            SequenceOp.TypeCheck(visitor, contextInfo);
            if (Literal.IsEmptySequence(Sequence))
            {
                return Literal.MakeLiteral(BooleanValue.Get(@operator != Token.SOME), this);
            }


            // "some" and "every" have no ordering constraints
            Sequence = Sequence.Unordered(false, false);
            SequenceType decl = GetRequiredType();
            if (decl.GetCardinality() == StaticProperty.ALLOWS_ZERO)
            {
                throw new XPathException("Range variable will never satisfy the type empty-sequence()", "XPTY0004").AsTypeError().WithLocation(GetLocation());
            }

            SequenceType sequenceType = SequenceType.MakeSequenceType(decl.PrimaryType, StaticProperty.ALLOWS_ZERO_OR_MORE);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, GetVariableQName().DisplayName, 0);
            Sequence = TypeChecker.StrictTypeCheck(Sequence, sequenceType, role, visitor.StaticContext);
            ItemType actualItemType = Sequence.GetItemType();
            RefineTypeInformation(actualItemType, StaticProperty.EXACTLY_ONE, null, Sequence.GetSpecialProperties(), this);

            //declaration = null;     // let the garbage collector take it
            ActionOp.TypeCheck(visitor, contextInfo);
            XPathException err = TypeChecker.EbvError(GetAction(), visitor.GetConfiguration().GetTypeHierarchy());
            if (err != null)
            {
                throw err.WithLocation(GetLocation());
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            SequenceOp.Optimize(visitor, contextItemType);
            ActionOp.Optimize(visitor, contextItemType);
            Expression ebv = BooleanFn.RewriteEffectiveBooleanValue(GetAction(), visitor, contextItemType);
            if (ebv != null)
            {
                SetAction(ebv);
                AdoptChildExpression(ebv);
            }

            if (Literal.HasEffectiveBooleanValue(ebv, true))
            {

                // some $x satisfies true() => exists($x)
                // every $x satisfies true() => true()
                if (Operator == Token.SOME)
                {
                    return SystemFunction.MakeCall("exists", GetRetainedStaticContext(), Sequence);
                }
                else
                {
                    Expression e2 = new Literal(BooleanValue.TRUE);
                    ExpressionTool.CopyLocationInfo(this, e2);
                    return e2;
                }
            }
            else if (Literal.HasEffectiveBooleanValue(ebv, false))
            {

                // some $x satisfies false() => false()
                // every $x satisfies false() => empty($x)
                if (Operator == Token.SOME)
                {
                    Expression e2 = new Literal(BooleanValue.FALSE);
                    ExpressionTool.CopyLocationInfo(this, e2);
                    return e2;
                }
                else
                {
                    return SystemFunction.MakeCall("empty", GetRetainedStaticContext(), Sequence);
                }
            }

            if (Sequence is Literal)
            {
                IGroundedValue seq = ((Literal)Sequence).GroundedValue;
                int len = seq.GetLength();
                if (len == 0)
                {
                    Expression e2 = new Literal(BooleanValue.Get(Operator == Token.EVERY));
                    ExpressionTool.CopyLocationInfo(this, e2);
                    return e2;
                }
                else if (len == 1)
                {
                    if (GetAction() is VariableReference && ((VariableReference)GetAction()).GetBinding() == this)
                    {
                        return SystemFunction.MakeCall("boolean", GetRetainedStaticContext(), Sequence);
                    }
                    else
                    {
                        ReplaceVariable(Sequence);
                        return GetAction();
                    }
                }
            }


            // if streaming, convert to an expression that can be streamed
            if (visitor.IsOptimizeForStreaming())
            {
                Expression e3 = visitor.ObtainOptimizer().OptimizeQuantifiedExpressionForStreaming(this);
                if (e3 != null && e3 != this)
                {
                    return e3.Optimize(visitor, contextItemType);
                }
            }

            return this;
        }

        public override void CheckForUpdatingSubexpressions()
        {
            Sequence.CheckForUpdatingSubexpressions();
            GetAction().CheckForUpdatingSubexpressions();
        }

        public override bool IsUpdatingExpression()
        {
            return false;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            QuantifiedExpression qe = new QuantifiedExpression();
            ExpressionTool.CopyLocationInfo(this, qe);
            qe.Operator = @operator;
            qe.SetVariableQName(variableName);
            qe.SetRequiredType(requiredType);
            qe.Sequence = Sequence.Copy(rebindings);
            rebindings.Put(this, qe);
            Expression newAction = GetAction().Copy(rebindings);
            qe.SetAction(newAction);
            qe.variableName = variableName;
            qe.slotNumber = slotNumber;
            return qe;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            return p | StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        /// <summary>
        /// Evaluate the expression to return a singleton value
        /// </summary>
        public override IItem EvaluateItem(IXPathContext context)
        {
            return BooleanValue.Get(EffectiveBooleanValue(context));
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {

            // First create an iteration of the base sequence.
            ISequenceIterator @base = Sequence.Iterate(context);

            // Now test to see if some or all of the tests are true. The same
            // logic is used for the SOME and EVERY operators
            bool some = @operator == Token.SOME;
            int slot = LocalSlotNumber;
            IItem it;
            while ((it = @base.Next()) != null)
            {
                context.SetLocalVariable(slot, it);
                if (some == GetAction().EffectiveBooleanValue(context))
                {
                    @base.Dispose();
                    return some;
                }
            }

            return !some;
        }

        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.BOOLEAN;
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.BOOLEAN;
        }

        public override string ToString()
        {
            return (@operator == Token.SOME ? "some" : "every") + " $" + VariableEQName + " in " + Sequence + " satisfies " + ExpressionTool.Parenthesize(GetAction());
        }

        public override string ToShortString()
        {
            return (@operator == Token.SOME ? "some" : "every") + " $" + VariableName + " in " + Sequence.ToShortString() + " satisfies ...";
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement(Token.tokens[@operator], this);
            @out.EmitAttribute("var", GetVariableQName());
            @out.EmitAttribute("slot", "" + slotNumber);
            Sequence.Export(@out);
            GetAction().Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new QuantifiedExprElaborator();
        }

        /// <summary>
        /// Elaborator for a quantified expression ({@code some|every X in Y satisfies Z})
        /// </summary>
        internal sealed class QuantifiedExprElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                QuantifiedExpression expr = (QuantifiedExpression)GetExpression();
                IPullEvaluator selectEval = expr.Sequence.MakeElaborator().ElaborateForPull();
                IBooleanEvaluator satisfiesEval = expr.GetAction().MakeElaborator().ElaborateForBoolean();
                bool some = expr.Operator == Token.SOME;
                int slot = expr.LocalSlotNumber;
                return (context) =>
                {
                    ISequenceIterator @base = selectEval.Iterate(context);
                    for (IItem it; (it = @base.Next()) != null;)
                    {
                        context.SetLocalVariable(slot, it);
                        if (some == satisfiesEval.Eval(context))
                        {
                            @base.Dispose();
                            return some;
                        }
                    }

                    return !some;
                };
            }
        }
    }
}
