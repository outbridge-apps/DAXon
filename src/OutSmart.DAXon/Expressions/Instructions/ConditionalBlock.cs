////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An XSLT 3.0 sequence constructor containing xsl:on-empty and/or xsl:on-non-empty instructions
    /// </summary>
    internal sealed class ConditionalBlock : Instruction
    {
        private readonly Operand[] operanda;
        private bool allNodesUntyped;

        public override string ExpressionName => "condSeq";

        public override int ImplementationMethod => PROCESS_METHOD;

        public override string StreamerName => "ConditionalBlock";
        public ConditionalBlock(Expression[] children)
        {
            operanda = new Operand[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                operanda[i] = new Operand(this, children[i], OperandRole.SAME_FOCUS_ACTION);
                if (children[i] is OnEmptyExpr)
                {
                    operanda[i].OperandRole = OperandRole.SAME_FOCUS_ACTION.WithConstrainedClass();
                }
            }
        }

        public ConditionalBlock(IList<Expression> children) : this(children.ToArray())
        {
        }

        public Expression GetChildExpression(int n)
        {
            return operanda[n].GetChildExpression();
        }

        public int Size()
        {
            return operanda.Length;
        }

        public override IEnumerable<Operand> Operands()
        {
            return operanda.ToList();
        }

        protected override int ComputeSpecialProperties()
        {
            if (Size() == 0)
            {

                // An empty sequence has all special properties except "has side effects".
                return StaticProperty.SPECIAL_PROPERTY_MASK & ~StaticProperty.HAS_SIDE_EFFECTS;
            }

            int p = base.ComputeSpecialProperties();
            if (allNodesUntyped)
            {
                p |= StaticProperty.ALL_NODES_UNTYPED;
            }


            // if all the expressions are axis expressions, we have a same-document node-set
            bool allAxisExpressions = true;
            bool allChildAxis = true;
            bool allSubtreeAxis = true;
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                if (!(child is AxisExpression))
                {
                    allAxisExpressions = false;
                    allChildAxis = false;
                    allSubtreeAxis = false;
                    break;
                }

                int axis = ((AxisExpression)child).Axis;
                if (axis != AxisInfo.CHILD)
                {
                    allChildAxis = false;
                }

                if (!AxisInfo.isSubtreeAxis[axis])
                {
                    allSubtreeAxis = false;
                }
            }

            if (allAxisExpressions)
            {
                p |= StaticProperty.CONTEXT_DOCUMENT_NODESET | StaticProperty.SINGLE_DOCUMENT_NODESET | StaticProperty.NO_NODES_NEWLY_CREATED;

                // if they all use the child axis, then we have a peer node-set
                if (allChildAxis)
                {
                    p |= StaticProperty.PEER_NODESET;
                }

                if (allSubtreeAxis)
                {
                    p |= StaticProperty.SUBTREE_NODESET;
                }


                // special case: selecting attributes then children, node-set is sorted
                if (Size() == 2 && ((AxisExpression)GetChildExpression(0)).Axis == AxisInfo.ATTRIBUTE && ((AxisExpression)GetChildExpression(1)).Axis == AxisInfo.CHILD)
                {
                    p |= StaticProperty.ORDERED_NODESET;
                }
            }

            return p;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Expression[] c2 = new Expression[Size()];
            for (int c = 0; c < Size(); c++)
            {
                c2[c] = GetChildExpression(c).Copy(rebindings);
            }

            ConditionalBlock b2 = new ConditionalBlock(c2);
            for (int c = 0; c < Size(); c++)
            {
                b2.AdoptChildExpression(c2[c]);
            }

            b2.allNodesUntyped = allNodesUntyped;
            ExpressionTool.CopyLocationInfo(this, b2);
            return b2;
        }

        public override ItemType GetItemType()
        {
            if (Size() == 0)
            {
                return ErrorType.GetInstance();
            }

            ItemType t1 = GetChildExpression(0).GetItemType();
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            for (int i = 1; i < Size(); i++)
            {
                t1 = Types.Type.GetCommonSuperType(t1, GetChildExpression(i).GetItemType(), th);
                if (t1 is AnyItemType)
                {
                    return t1; // no point going any further
                }
            }

            return t1;
        }

        public override int GetCardinality()
        {
            if (Size() == 0)
            {
                return StaticProperty.EMPTY;
            }

            int c1 = GetChildExpression(0).GetCardinality();
            for (int i = 1; i < Size(); i++)
            {
                c1 = Cardinality.Sum(c1, GetChildExpression(i).GetCardinality());
                if (c1 == StaticProperty.ALLOWS_ZERO_OR_MORE)
                {
                    break;
                }
            }

            return c1;
        }

        public override bool MayCreateNewNodes()
        {
            return SomeOperandCreatesNewNodes();
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            if (Block.NeverReturnsTypedNodes(this, visitor.GetConfiguration().GetTypeHierarchy()))
            {
                ResetLocalStaticProperties();
                allNodesUntyped = true;
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


            // This code was written when xsl:on-empty instructions were allowed anywhere, so it's more general
            // than strictly necessary.
            int lastOrdinaryInstruction = -1;
            bool alwaysNonEmpty = false;
            bool alwaysEmpty = true;
            for (int c = 0; c < Size(); c++)
            {
                if (!(GetChildExpression(c) is OnEmptyExpr || GetChildExpression(c) is OnNonEmptyExpr))
                {
                    lastOrdinaryInstruction = c;
                    if (GetChildExpression(c).GetItemType().GetUType().Intersection(UType.DOCUMENT.Union(UType.TEXT)).Equals(UType.VOID))
                    {
                        int card = GetChildExpression(c).GetCardinality();
                        if (!Cardinality.AllowsZero(card))
                        {
                            alwaysNonEmpty = true;
                        }

                        if (card != StaticProperty.ALLOWS_ZERO)
                        {
                            alwaysEmpty = false;
                        }
                    }
                    else
                    {
                        alwaysEmpty = false;
                        alwaysNonEmpty = false;
                        break;
                    }
                }
            }

            if (alwaysEmpty)
            {
                visitor.StaticContext.IssueWarning("The result of the sequence constructor will always be empty, so xsl:on-empty " + "instructions will always be evaluated, and xsl:on-non-empty instructions will never be evaluated", DAXonErrorCode.SXWN9029, GetLocation());
                IList<Expression> retain = new List<Expression>();
                for (int c = 0; c < Size(); c++)
                {
                    if (GetChildExpression(c) is OnNonEmptyExpr)
                    {
                    }
                    else if (GetChildExpression(c) is OnEmptyExpr)
                    {
                        retain.Add(((OnEmptyExpr)GetChildExpression(c)).BaseExpression);
                    }
                    else
                    {
                        retain.Add(GetChildExpression(c));
                    }
                }

                return Block.MakeBlock(retain);
            }

            if (alwaysNonEmpty)
            {
                visitor.StaticContext.IssueWarning("The result of the sequence constructor will never be empty, so xsl:on-empty " + "instructions will never be evaluated, and xsl:on-non-empty instructions will always be evaluated", DAXonErrorCode.SXWN9029, GetLocation());
                IList<Expression> retain = new List<Expression>();
                for (int c = 0; c < Size(); c++)
                {
                    if (GetChildExpression(c) is OnEmptyExpr)
                    {
                    }
                    else if (GetChildExpression(c) is OnNonEmptyExpr)
                    {
                        retain.Add(((OnNonEmptyExpr)GetChildExpression(c)).BaseExpression);
                    }
                    else
                    {
                        retain.Add(GetChildExpression(c));
                    }
                }

                return Block.MakeBlock(retain);
            }

            if (lastOrdinaryInstruction == -1)
            {

                // all instructions are either xsl:on-empty or xsl:on-non-empty
                // We can discard the xsl:on-non-empty instructions, and make the on-empty instructions unconditional
                IList<Expression> retain = new List<Expression>();
                for (int c = 0; c < Size(); c++)
                {
                    if (GetChildExpression(c) is OnEmptyExpr)
                    {
                        retain.Add(((OnEmptyExpr)GetChildExpression(c)).BaseExpression);
                    }
                }

                return Block.MakeBlock(retain);
            }

            return this;
        }

        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                child.CheckPermittedContents(parentType, false);
            }
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("condSeq", this);
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                child.Export(@out);
            }

            @out.EndElement();
        }

        public override string ToShortString()
        {
            return "(" + GetChildExpression(0).ToShortString() + ", ...)";
        }

        public override Elaborator GetElaborator()
        {
            return new ConditionalBlockElaborator();
        }

        private sealed class ConditionalBlockElaborator : PushElaborator
        {
            private const int ON_EMPTY = 0;
            private const int ON_NON_EMPTY = 1;
            private const int ALWAYS = 2;
            public override IPushEvaluator ElaborateForPush()
            {
                ConditionalBlock expr = (ConditionalBlock)GetExpression();
                IPushEvaluator[] pushers = new IPushEvaluator[expr.operanda.Length];
                int[] instruction = new int[expr.operanda.Length];
                for (int i = 0; i < pushers.Length; i++)
                {
                    Expression child = expr.operanda[i].GetChildExpression();
                    pushers[i] = child.MakeElaborator().ElaborateForPush();
                    if (child is OnEmptyExpr)
                    {
                        instruction[i] = ON_EMPTY;
                    }
                    else if (child is OnNonEmptyExpr)
                    {
                        instruction[i] = ON_NON_EMPTY;
                    }
                    else
                    {
                        instruction[i] = ALWAYS;
                    }
                }

                return (output, context) =>
                {
                    IList<IPushEvaluator> onNonEmptyPending = new List<IPushEvaluator>();
                    IAction action = () =>
                    {
                        foreach (IPushEvaluator e in onNonEmptyPending)
                        {
                            DispatchTailCall(e.ProcessLeavingTail(output, context));
                        }
                    };
                    SignificantItemDetector significantItemDetector = new SignificantItemDetector(output, action);
                    for (int i = 0; i < instruction.Length; i++)
                    {
                        try
                        {
                            switch (instruction[i])
                            {
                                case ON_EMPTY:

                                    // Ignore on-empty instructions until the end
                                    break;
                                case ON_NON_EMPTY:
                                    if (significantItemDetector.IsEmpty())
                                    {
                                        onNonEmptyPending.Add(pushers[i]);
                                    }
                                    else
                                    {
                                        DispatchTailCall(pushers[i].ProcessLeavingTail(output, context));
                                    }

                                    break;
                                case ALWAYS:
                                    DispatchTailCall(pushers[i].ProcessLeavingTail(significantItemDetector, context));
                                    break;
                            }
                        }
                        catch (XPathException e)
                        {
                            throw e.MaybeWithLocation(expr.operanda[i].GetChildExpression().GetLocation()).MaybeWithContext(context);
                        }
                    }


                    // At the end, if the content produced until now is empty, process the on-empty instructions
                    if (significantItemDetector.IsEmpty())
                    {
                        for (int i = 0; i < instruction.Length; i++)
                        {
                            if (instruction[i] == ON_EMPTY)
                            {
                                DispatchTailCall(pushers[i].ProcessLeavingTail(output, context));
                            }
                        }
                    }

                    return null;
                };
            }
        }
    }
}