////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// InstanceOf Expression: implements "Expr instance of data-type"
    /// </summary>
    internal sealed class InstanceOfExpression : UnaryExpression
    {
        ItemType targetType;
        int targetCardinality;

        public ItemType RequiredItemType => targetType;

        public int RequiredCardinality => targetCardinality;

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override string ExpressionName => "instance";

        public override string StreamerName => "InstanceOf";
        public InstanceOfExpression(Expression source, SequenceType target) : base(source)
        {
            targetType = target.PrimaryType;
            if (targetType == null)
            {
                throw new ArgumentException("Primary item type must not be null");
            }

            targetCardinality = target.GetCardinality();
        }

        protected override OperandRole GetOperandRole()
        {
            return targetType is DocumentNodeTest ? OperandRole.ABSORB : OperandRole.INSPECT;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            Expression operand = BaseExpression;
            if (operand is Literal)
            {
                Literal lit = Literal.MakeLiteral(EvaluateItem(visitor.StaticContext.MakeEarlyEvaluationContext()), this);
                ExpressionTool.CopyLocationInfo(this, lit);
                return lit;
            }


            // See if we can get the answer by static analysis.
            if (Cardinality.Subsumes(targetCardinality, operand.GetCardinality()))
            {
                TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
                Affinity relation = th.Relationship(operand.GetItemType(), targetType);
                if (relation == Affinity.SAME_TYPE || relation == Affinity.SUBSUMED_BY)
                {
                    Literal lit = Literal.MakeLiteral(BooleanValue.TRUE, this);
                    ExpressionTool.CopyLocationInfo(this, lit);
                    return lit;
                }
                else if (relation == Affinity.DISJOINT)
                {

                    // if the item types are disjoint, the result might still be true if both sequences are empty
                    if (!Cardinality.AllowsZero(targetCardinality) || !Cardinality.AllowsZero(operand.GetCardinality()))
                    {
                        Literal lit = Literal.MakeLiteral(BooleanValue.FALSE, this);
                        ExpressionTool.CopyLocationInfo(this, lit);
                        return lit;
                    }
                }
            }
            else if ((targetCardinality & operand.GetCardinality()) == 0)
            {
                Literal lit = Literal.MakeLiteral(BooleanValue.FALSE, this);
                ExpressionTool.CopyLocationInfo(this, lit);
                return lit;
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e = base.Optimize(visitor, contextInfo);
            if (e != this)
            {
                return e;
            }

            if (Cardinality.Subsumes(targetCardinality, BaseExpression.GetCardinality()))
            {
                TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
                Affinity relation = th.Relationship(BaseExpression.GetItemType(), targetType);
                if (relation == Affinity.SAME_TYPE || relation == Affinity.SUBSUMED_BY)
                {
                    return Literal.MakeLiteral(BooleanValue.TRUE, this);
                }
                else if (relation == Affinity.DISJOINT)
                {

                    // if the item types are disjoint, the result might still be true if both sequences are empty
                    if (!Cardinality.AllowsZero(targetCardinality) || !Cardinality.AllowsZero(BaseExpression.GetCardinality()))
                    {
                        return Literal.MakeLiteral(BooleanValue.FALSE, this);
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        public override bool Equals(object other)
        {
            return base.Equals(other) && targetType == ((InstanceOfExpression)other).targetType && targetCardinality == ((InstanceOfExpression)other).targetCardinality;
        }

        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode() ^ targetType.GetHashCode() ^ targetCardinality;
        }

        /// <summary>
        /// Determine the cardinality
        /// </summary>
        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        /// <summary>
        /// Determine the cardinality
        /// </summary>
        public override Expression Copy(RebindingMap rebindings)
        {
            InstanceOfExpression exp = new InstanceOfExpression(BaseExpression.Copy(rebindings), SequenceType.MakeSequenceType(targetType, targetCardinality));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        /// <summary>
        /// Determine the data type of the result of the InstanceOf expression
        /// </summary>
        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.BOOLEAN;
        }

        /// <summary>
        /// Determine the data type of the result of the InstanceOf expression
        /// </summary>
        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.BOOLEAN;
        }

        /// <summary>
        /// Evaluate the expression
        /// </summary>
        public override IItem EvaluateItem(IXPathContext context)
        {
            return BooleanValue.Get(EffectiveBooleanValue(context));
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            return MakeElaborator().ElaborateForBoolean().Eval(context);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("instance", this);
            SequenceType st = SequenceType.MakeSequenceType(targetType, targetCardinality);
            @out.EmitAttribute("of", st.ToAlphaCode());
            BaseExpression.Export(@out);
            @out.EndElement();
        }

        public override string ToString()
        {
            string occ = Cardinality.GetOccurrenceIndicator(targetCardinality);
            return "(" + BaseExpression.ToString() + " instance of " + targetType.ToString() + occ + ")";
        }

        public override string ToShortString()
        {
            string occ = Cardinality.GetOccurrenceIndicator(targetCardinality);
            return BaseExpression.ToShortString() + " instance of " + targetType.ToString() + occ;
        }

        public override Elaborator GetElaborator()
        {
            return new InstanceOfElaborator();
        }

        /// <summary>
        /// Elaborator for an {@code instance of} expression
        /// </summary>
        internal sealed class InstanceOfElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                InstanceOfExpression exp = (InstanceOfExpression)GetExpression();
                TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                Expression arg = exp.BaseExpression;
                int requiredCardinality = exp.RequiredCardinality;
                bool allowsMany = Cardinality.AllowsMany(requiredCardinality);
                bool allowsZero = Cardinality.AllowsZero(requiredCardinality);
                ItemType requiredType = exp.RequiredItemType;
                if (requiredCardinality == StaticProperty.EXACTLY_ONE && !Cardinality.AllowsMany(arg.GetCardinality()))
                {
                    IItemEvaluator itemEval = arg.MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        IItem item = itemEval.Eval(context);
                        return item != null && requiredType.Matches(item, th);
                    };
                }
                else
                {
                    IPullEvaluator argPull = arg.MakeElaborator().ElaborateForPull();
                    bool itemTypeOK = th.IsSubType(arg.GetItemType(), requiredType);
                    return (context) =>
                    {
                        ISequenceIterator iter = argPull.Iterate(context);
                        int count = 0;
                        for (IItem item; (item = iter.Next()) != null;)
                        {
                            count++;
                            if (!itemTypeOK && !requiredType.Matches(item, th))
                            {
                                iter.Dispose();
                                return false;
                            }

                            if (!allowsMany && count == 2)
                            {
                                iter.Dispose();
                                return false;
                            }
                        }

                        return allowsZero || count != 0;
                    };
                }
            }
        }
    }
}
