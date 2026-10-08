////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections.Zeno;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class Block : Instruction
    {
        private readonly Operand[] operanda;
        private bool allNodesUntyped;

        public int Count { get { return Size(); } }

        public override string ExpressionName => "sequence";

        // no-op
        public override int ImplementationMethod => ITERATE_METHOD | PROCESS_METHOD;

        // no-op
        public override string StreamerName => "Block";
        public Block(Expression[] children)
        {
            operanda = new Operand[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                operanda[i] = new Operand(this, children[i], OperandRole.SAME_FOCUS_ACTION);
            }

            foreach (Expression e in children)
            {
                AdoptChildExpression(e);
            }
        }

        public override bool IsInstruction()
        {
            return false;
        }

        private Expression Child(int n)
        {
            return operanda[n].GetChildExpression();
        }

        private void SetChild(int n, Expression child)
        {
            operanda[n].SetChildExpression(child);
        }
        public int Size()
        {
            return operanda.Length;
        }

        public override IEnumerable<Operand> Operands()
        {
            return operanda.ToList();
        }

        public override bool HasVariableBinding(IBinding binding)
        {
            if (binding is LocalParam)
            {
                foreach (Operand o in operanda)
                {
                    if (o.GetChildExpression() == binding)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static Expression MakeBlock(Expression e1, Expression e2)
        {
            if (e1 == null || Literal.IsEmptySequence(e1))
            {
                return e2;
            }

            if (e2 == null || Literal.IsEmptySequence(e2))
            {
                return e1;
            }

            if (e1 is Block || e2 is Block)
            {
                IList<Expression> list = new List<Expression>(10);
                if (e1 is Block)
                {
                    foreach (Operand o in e1.Operands())
                    {
                        list.Add(o.GetChildExpression());
                    }
                }
                else
                {
                    list.Add(e1);
                }

                if (e2 is Block)
                {
                    foreach (Operand o in e2.Operands())
                    {
                        list.Add(o.GetChildExpression());
                    }
                }
                else
                {
                    list.Add(e2);
                }

                Expression[] exps = new Expression[list.Count];
                exps = list.ToArray();
                return new Block(exps);
            }
            else
            {
                Expression[] exps = new[]
                {
                    e1,
                    e2
                };
                return new Block(exps);
            }
        }

        public static Expression MakeBlock(IList<Expression> list)
        {
            if (list.Count == 0)
            {
                return Literal.MakeEmptySequence();
            }
            else if (list.Count == 1)
            {
                return list[0];
            }
            else
            {
                Expression[] exps = new Expression[list.Count];
                exps = list.ToArray();
                return new Block(exps);
            }
        }

        public Operand[] GetOperanda()
        {
            return operanda;
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
                Expression childExpr = o.GetChildExpression();
                if (!(childExpr is AxisExpression))
                {
                    allAxisExpressions = false;
                    allChildAxis = false;
                    allSubtreeAxis = false;
                    break;
                }

                int axis = ((AxisExpression)childExpr).Axis;
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
                if (Size() == 2 && ((AxisExpression)Child(0)).Axis == AxisInfo.ATTRIBUTE && ((AxisExpression)Child(1)).Axis == AxisInfo.CHILD)
                {
                    p |= StaticProperty.ORDERED_NODESET;
                }
            }

            return p;
        }

        public override bool ImplementsStaticTypeCheck()
        {
            return true;
        }

        public override Expression StaticTypeCheck(SequenceType req, bool backwardsCompatible, Func<RoleDiagnostic> roleSupplier, ExpressionVisitor visitor)
        {
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(backwardsCompatible);
            if (backwardsCompatible && !Cardinality.AllowsMany(req.GetCardinality()))
            {
                Expression first = FirstItemExpression.MakeFirstItemExpression(this);
                return tc.StaticTypeCheck(first, req, roleSupplier, visitor);
            }

            Expression[] @checked = new Expression[operanda.Length];
            SequenceType subReq = req;
            if (req.GetCardinality() != StaticProperty.ALLOWS_ZERO_OR_MORE)
            {
                subReq = SequenceType.MakeSequenceType(req.PrimaryType, StaticProperty.ALLOWS_ZERO_OR_MORE);
            }

            for (int i = 0; i < operanda.Length; i++)
            {
                @checked[i] = tc.StaticTypeCheck(operanda[i].GetChildExpression(), subReq, roleSupplier, visitor);
            }

            Block b2 = new Block(@checked);
            ExpressionTool.CopyLocationInfo(this, b2);
            b2.allNodesUntyped = allNodesUntyped;
            int reqCard = req.GetCardinality();
            int suppliedCard = b2.GetCardinality();
            if (!Cardinality.Subsumes(req.GetCardinality(), suppliedCard))
            {
                if ((reqCard & suppliedCard) == 0)
                {
                    RoleDiagnostic role = roleSupplier();
                    throw new XPathException("The required cardinality of the " + role.GetMessage() + " is " + Cardinality.Describe(reqCard) + ", but the supplied cardinality is " + Cardinality.Describe(suppliedCard), role.ErrorCode, GetLocation()).AsTypeError().WithFailingExpression(this);
                }
                else
                {
                    return CardinalityChecker.MakeCardinalityChecker(b2, reqCard, roleSupplier);
                }
            }

            return b2;
        }

        public static bool NeverReturnsTypedNodes(Instruction insn, TypeHierarchy th)
        {
            foreach (Operand o in insn.Operands())
            {
                Expression exp = o.GetChildExpression();
                if (!exp.HasSpecialProperty(StaticProperty.ALL_NODES_UNTYPED))
                {
                    ItemType it = exp.GetItemType();
                    if (th.Relationship(it, NodeKindTest.ELEMENT) != Affinity.DISJOINT || th.Relationship(it, NodeKindTest.ATTRIBUTE) != Affinity.DISJOINT)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public Expression MergeAdjacentTextInstructions()
        {
            bool[] isLiteralText = new bool[Size()];
            bool hasAdjacentTextNodes = false;
            for (int i = 0; i < Size(); i++)
            {
                isLiteralText[i] = Child(i) is ValueOf && ((ValueOf)Child(i)).Select is StringLiteral && !((ValueOf)Child(i)).IsDisableOutputEscaping();
                if (i > 0 && isLiteralText[i] && isLiteralText[i - 1])
                {
                    hasAdjacentTextNodes = true;
                }
            }

            if (hasAdjacentTextNodes)
            {
                IList<Expression> content = new List<Expression>(Size());
                string pendingText = null;
                for (int i = 0; i < Size(); i++)
                {
                    if (isLiteralText[i])
                    {
                        pendingText = (pendingText == null ? "" : pendingText) + ((StringLiteral)((ValueOf)Child(i)).Select).GetString();
                    }
                    else
                    {
                        if (pendingText != null)
                        {
                            ValueOf inst = new ValueOf(new StringLiteral(pendingText), false, false);
                            content.Add(inst);
                            pendingText = null;
                        }

                        content.Add(Child(i));
                    }
                }

                if (pendingText != null)
                {
                    ValueOf inst = new ValueOf(new StringLiteral(pendingText), false, false);
                    content.Add(inst);
                }

                return MakeBlock(content);
            }
            else
            {
                return this;
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Expression[] c2 = new Expression[Size()];
            for (int c = 0; c < Size(); c++)
            {
                c2[c] = Child(c).Copy(rebindings);
            }

            Block b2 = new Block(c2);
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

            ItemType t1 = null;
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            for (int i = 0; i < Size(); i++)
            {
                Expression childExpr = Child(i);
                if (!(childExpr is MessageInstr))
                {
                    ItemType t = childExpr.GetItemType();
                    t1 = t1 == null ? t : Types.Type.GetCommonSuperType(t1, t, th);
                    if (t1 is AnyItemType)
                    {
                        return t1; // no point going any further
                    }
                }
            }

            return t1 == null ? ErrorType.GetInstance() : t1;
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            if (IsInstruction())
            {
                return base.GetStaticUType(contextItemType);
            }
            else
            {
                if (Size() == 0)
                {
                    return UType.VOID;
                }

                UType t1 = Child(0).GetStaticUType(contextItemType);
                for (int i = 1; i < Size(); i++)
                {
                    t1 = t1.Union(Child(i).GetStaticUType(contextItemType));
                    if (t1 == UType.ANY)
                    {
                        return t1; // no point going any further
                    }
                }

                return t1;
            }
        }

        protected override int ComputeCardinality()
        {
            if (Size() == 0)
            {
                return StaticProperty.EMPTY;
            }

            int c1 = Child(0).GetCardinality();
            for (int i = 1; i < Size(); i++)
            {
                c1 = Cardinality.Sum(c1, Child(i).GetCardinality());
                if (c1 == StaticProperty.ALLOWS_MANY)
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

        public override void CheckForUpdatingSubexpressions()
        {
            if (Size() < 2)
            {
                return;
            }

            bool updating = false;
            bool nonUpdating = false;
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                if (ExpressionTool.IsNotAllowedInUpdatingContext(child))
                {
                    if (updating)
                    {
                        throw new XPathException("If any subexpression is updating, then all must be updating", "XUST0001").WithLocation(child.GetLocation());
                    }

                    nonUpdating = true;
                }

                if (child.IsUpdatingExpression())
                {
                    if (nonUpdating)
                    {
                        throw new XPathException("If any subexpression is updating, then all must be updating", "XUST0001").WithLocation(child.GetLocation());
                    }

                    updating = true;
                }
            }
        }

        public override bool IsVacuousExpression()
        {

            // true if all subexpressions are vacuous
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                if (!child.IsVacuousExpression())
                {
                    return false;
                }
            }

            return true;
        }

        public override Expression Simplify()
        {
            bool allAtomic = true;
            bool nested = false;
            for (int c = 0; c < Size(); c++)
            {
                SetChild(c, Child(c).Simplify());
                if (!Literal.IsAtomic(Child(c)))
                {
                    allAtomic = false;
                }

                if (Child(c) is Block)
                {
                    nested = true;
                }
                else if (Literal.IsEmptySequence(Child(c)))
                {
                    nested = true;
                }
            }

            if (Size() == 1)
            {
                Expression e = GetOperanda()[0].GetChildExpression();
                e.ParentExpression = ParentExpression;
                return e;
            }
            else if (Size() == 0)
            {
                Expression result = Literal.MakeEmptySequence();
                ExpressionTool.CopyLocationInfo(this, result);
                result.ParentExpression = ParentExpression;
                return result;
            }
            else if (nested)
            {
                IList<Expression> list = new List<Expression>(Size() * 2);
                Flatten(list);
                Expression[] children = new Expression[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    children[i] = list[i];
                }

                Block newBlock = new Block(children);
                ExpressionTool.CopyLocationInfo(this, newBlock);
                return newBlock.Simplify();
            }
            else if (allAtomic)
            {
                AtomicValue[] values = new AtomicValue[Size()];
                for (int c = 0; c < Size(); c++)
                {
                    values[c] = (AtomicValue)((Literal)Child(c)).GroundedValue;
                }

                Expression result = Literal.MakeLiteral(new SequenceExtent.Of<AtomicValue>(values), this);
                result.ParentExpression = ParentExpression;
                return result;
            }
            else
            {
                return this;
            }
        }

        private void Flatten(IList<Expression> targetList)
        {
            IList<IItem> currentLiteralList = null;
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                if (Literal.IsEmptySequence(child))
                {
                }
                else if (child is Block)
                {
                    FlushCurrentLiteralList(currentLiteralList, targetList);
                    currentLiteralList = null;
                    ((Block)child).Flatten(targetList);
                }
                else if (child is Literal && !(((Literal)child).GroundedValue is IntegerRange))
                {
                    ISequenceIterator iterator = ((Literal)child).GroundedValue.Iterate();
                    if (currentLiteralList == null)
                    {
                        currentLiteralList = new List<IItem>(10);
                    }

                    for (IItem item; (item = iterator.Next()) != null;)
                    {
                        currentLiteralList.Add(item);
                    } // no-op
                }
                else
                {
                    FlushCurrentLiteralList(currentLiteralList, targetList);
                    currentLiteralList = null;
                    targetList.Add(child);
                }
            }

            FlushCurrentLiteralList(currentLiteralList, targetList);
        }

        // no-op
        private void FlushCurrentLiteralList(IList<IItem> currentLiteralList, IList<Expression> list)
        {
            if (currentLiteralList != null)
            {
                ListIterator.Of<IItem> iter = new ListIterator.Of<IItem>(currentLiteralList);
                Literal lit = Literal.MakeLiteral(iter.Materialize(), this);
                list.Add(lit);
            }
        }

        // no-op
        public bool IsCandidateForSharedAppend()
        {
            foreach (Operand o in Operands())
            {
                Expression exp = o.GetChildExpression();
                if (exp is VariableReference || exp is Literal)
                {
                    return true;
                }
            }

            return false;
        }

        // no-op
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            if (NeverReturnsTypedNodes(this, visitor.GetConfiguration().GetTypeHierarchy()))
            {
                ResetLocalStaticProperties();
                allNodesUntyped = true;
            }

            return this;
        }

        // no-op
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            OptimizeChildren(visitor, contextInfo);
            bool canSimplify = false;
            bool prevLiteral = false;

            // Simplify the expression by collapsing nested blocks and merging adjacent literals
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                if (child is Block)
                {
                    canSimplify = true;
                    break;
                }

                if (child is Literal && !(((Literal)child).GroundedValue is IntegerRange))
                {
                    if (prevLiteral || Literal.IsEmptySequence(child))
                    {
                        canSimplify = true;
                        break;
                    }

                    prevLiteral = true;
                }
                else
                {
                    prevLiteral = false;
                }
            }

            if (canSimplify)
            {
                IList<Expression> list = new List<Expression>(Size() * 2);
                Flatten(list);
                Expression result = Block.MakeBlock(list);
                result.SetRetainedStaticContext(GetRetainedStaticContext());
                return result;
            }

            if (Size() == 0)
            {
                return Literal.MakeEmptySequence();
            }
            else if (Size() == 1)
            {
                return Child(0);
            }
            else
            {
                return this;
            }
        }

        // no-op
        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                child.CheckPermittedContents(parentType, false);
            }
        }

        // no-op
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("sequence", this);
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                child.Export(@out);
            }

            @out.EndElement();
        }

        // no-op
        public override string ToShortString()
        {
            return "(" + Child(0).ToShortString() + ", ...)";
        }

        // no-op
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            if (Size() == 0)
            {
                return EmptyIterator.GetInstance();
            }
            else if (Size() == 1)
            {
                return Child(0).Iterate(context);
            }
            else
            {
                return (ISequenceIterator)(new BlockIterator(operanda, context));
            }
        }

        // no-op
        public override Elaborator GetElaborator()
        {
            return new BlockElaborator();
        }

        // no-op
        internal interface IChainAction
        {
            ZenoSequence Perform(ZenoSequence @in, IXPathContext context);
        }

        // no-op
        internal sealed class BlockElaborator : PullElaborator
        {
            public override ISequenceEvaluator Lazily(bool repeatable, bool lazyEvaluationRequired)
            {
                Block expr = (Block)GetExpression();
                if (expr.IsCandidateForSharedAppend())
                {
                    return new SharedAppendEvaluator(expr);
                }
                else
                {
                    return base.Lazily(repeatable, lazyEvaluationRequired);
                }
            }

            public override IPullEvaluator ElaborateForPull()
            {
                Block expr = (Block)GetExpression();
                Operand[] operanda = expr.GetOperanda();
                int size = operanda.Length;
                IPullEvaluator[] actions = new IPullEvaluator[size];
                for (int i = 0; i < size; i++)
                {
                    actions[i] = operanda[i].GetChildExpression().MakeElaborator().ElaborateForPull();
                }

                return (context) => new BlockIterator(actions, context);
            }

            public override IPushEvaluator ElaborateForPush()
            {
                Block expr = (Block)GetExpression();
                Operand[] operanda = expr.GetOperanda();
                int size = operanda.Length;
                IPushEvaluator[] actions = new IPushEvaluator[size];
                for (int i = 0; i < size; i++)
                {
                    actions[i] = operanda[i].GetChildExpression().MakeElaborator().ElaborateForPush();
                }

                switch (size)
                {
                    case 2:
                        {
                            IPushEvaluator act0 = actions[0];
                            IPushEvaluator act1 = actions[1];
                            return (@out, context) =>
                            {
                                ITailCall tail = act0.ProcessLeavingTail(@out, context);
                                Expression.DispatchTailCall(tail);
                                return act1.ProcessLeavingTail(@out, context);
                            };
                        }

                    case 3:
                        {
                            IPushEvaluator act0 = actions[0];
                            IPushEvaluator act1 = actions[1];
                            IPushEvaluator act2 = actions[2];
                            return (@out, context) =>
                            {
                                ITailCall tail = act0.ProcessLeavingTail(@out, context);
                                Expression.DispatchTailCall(tail);
                                tail = act1.ProcessLeavingTail(@out, context);
                                Expression.DispatchTailCall(tail);
                                return act2.ProcessLeavingTail(@out, context);
                            };
                        }

                    case 4:
                        {
                            IPushEvaluator act0 = actions[0];
                            IPushEvaluator act1 = actions[1];
                            IPushEvaluator act2 = actions[2];
                            IPushEvaluator act3 = actions[3];
                            return (@out, context) =>
                            {
                                ITailCall tail = act0.ProcessLeavingTail(@out, context);
                                Expression.DispatchTailCall(tail);
                                tail = act1.ProcessLeavingTail(@out, context);
                                Expression.DispatchTailCall(tail);
                                tail = act2.ProcessLeavingTail(@out, context);
                                Expression.DispatchTailCall(tail);
                                return act3.ProcessLeavingTail(@out, context);
                            };
                        }

                    default:
                        return (@out, context) =>
                        {
                            ITailCall tail = null;
                            for (int i = 0; i < size; i++)
                            {
                                while (tail != null)
                                {
                                    tail = tail.ProcessLeavingTail();
                                }

                                tail = actions[i].ProcessLeavingTail(@out, context);
                            }

                            return tail;
                        };
                }
            }

            // Unroll loop for small sequence constructors
            public override IUpdateEvaluator ElaborateForUpdate()
            {
                Block expr = (Block)GetExpression();
                Operand[] operanda = expr.GetOperanda();
                int size = operanda.Length;
                IUpdateEvaluator[] actions = new IUpdateEvaluator[size];
                for (int i = 0; i < size; i++)
                {
                    actions[i] = expr.Child(i).MakeElaborator().ElaborateForUpdate();
                }

                return (context, pul) =>
                {
                    foreach (IUpdateEvaluator action in actions)
                    {
                        action.RegisterUpdates(context, pul);
                    }
                };
            }

            private sealed class BlockIterator : AbstractBlockIterator
            {
                private readonly IPullEvaluator[] pullers;
                public BlockIterator(IPullEvaluator[] pullers, IXPathContext context)
                {
                    this.pullers = pullers;
                    Init(pullers.Length, context);
                }

                public override ISequenceIterator GetNthChildIterator(int n)
                {
                    return pullers[n].Iterate(context);
                }
            }
        }
    }
}