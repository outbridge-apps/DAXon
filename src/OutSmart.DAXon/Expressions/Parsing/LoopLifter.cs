////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions.Parsing
{
    /// <summary>
    /// Loop lifting: a subexpression inside a loop that does not depend on the loop's controlling variable or focus
    /// is bound to a lazily evaluated variable outside the loop. Handles focus-dependent loops (xsl:for-each),
    /// variable-dependent loops (for expressions) and the special ones (xsl:for-each-group, xsl:merge,
    /// xsl:analyze-string). One instance per component (template, function, query body), holding data about it.
    /// Saxon's net.sf.saxon.expr.parser.LoopLifter. It differs in how it stands deep trees and bodies without a loop
    /// (see Process), in never placing a lifted variable inside one evaluated eagerly (see IsEagerSequence), and in
    /// stacking at most MaxStackedLets lifted variables at one place.
    /// </summary>
    internal sealed class LoopLifter
    {
        /// <summary>Apply loop lifting to an expression, typically the body of a template or function.</summary>
        public static Expression Process(Expression exp, ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            if (exp is Literal || exp is VariableReference)
            {
                return exp;
            }

            RetainedStaticContext rsc = exp.LocalRetainedStaticContext;

            // The walks recurse once per level, guarded as the compiler's others are. The optimization is optional: an
            // expression too deeply nested for the read-only first walks is compiled unlifted rather than refused.
            LoopLifter lifter;
            try
            {
                if (!HasRepeatedOperand(exp))
                {
                    return exp;
                }

                lifter = new LoopLifter(exp, visitor.GetConfiguration(), visitor.IsOptimizeForStreaming());
                lifter.GatherInfo(exp);
            }
            catch (RecursionDepthError)
            {
                return exp;
            }

            lifter.LoopLift(exp);
            if (!lifter.changed)
            {
                return exp;
            }

            lifter.root.SetRetainedStaticContext(rsc);
            lifter.root.ParentExpression = null;
            ExpressionTool.ResetPropertiesWithinSubtree(lifter.root);
            Expression e2 = lifter.root.Optimize(visitor, contextInfo);
            e2.ParentExpression = null;
            return e2;
        }

        private Expression root;
        private readonly Configuration config;
        private int sequence;
        private int eagerDepth;
        private bool changed;
        private readonly bool tracing;
        private readonly bool streaming;

        private sealed class ExpInfo
        {
            public Expression Expression;
            public int LoopLevel;
            public bool MultiThreaded;
            public ExpressionSet Dependees;
        }

        // Expressions compared by identity: an expression's Equals compares content.
        private sealed class Identity : IEqualityComparer<Expression>
        {
            internal static readonly Identity Instance = new Identity();

            public bool Equals(Expression x, Expression y) => ReferenceEquals(x, y);

            public int GetHashCode(Expression obj) => RuntimeHelpers.GetHashCode(obj);
        }

        // A set of expressions by identity, for the dominant case of none (null) or one.
        private sealed class ExpressionSet
        {
            private Expression firstExpression;
            private HashSet<Expression> furtherExpressions;

            public void Add(Expression exp)
            {
                if (firstExpression == null)
                {
                    firstExpression = exp;
                }
                else if (firstExpression != exp)
                {
                    (furtherExpressions ??= new HashSet<Expression>(Identity.Instance)).Add(exp);
                }
            }

            public void AddAll(ExpressionSet other)
            {
                if (other.firstExpression != null)
                {
                    Add(other.firstExpression);
                    if (other.furtherExpressions != null)
                    {
                        foreach (Expression exp in other.furtherExpressions)
                        {
                            Add(exp);
                        }
                    }
                }
            }

            public bool Contains(Expression exp)
            {
                if (firstExpression == null)
                {
                    return false;
                }

                return firstExpression == exp || (furtherExpressions != null && furtherExpressions.Contains(exp));
            }
        }

        private readonly Dictionary<Expression, ExpInfo> expInfoMap = new Dictionary<Expression, ExpInfo>(Identity.Instance);

        // Each lift stacks one more let above its target: a flat body with 800 invariant parts became a let chain as
        // deep, too deep for a 256 KB thread (Saxon builds the same chain). Past this many at one target the rest stay:
        // 256 KB holds ~600 on .NET Framework, ~50 on .NET before its JIT has tiered up.
        private const int MaxStackedLets = 32;
        private readonly HashSet<Expression> liftedLets = new HashSet<Expression>(Identity.Instance);

        private LoopLifter(Expression root, Configuration config, bool streaming)
        {
            this.root = root;
            this.config = config;
            this.tracing = config.GetBooleanProperty(Feature<bool>.TRACE_OPTIMIZER_DECISIONS);
            this.streaming = streaming;
        }

        // Information about an expression and its subtree, entered in expInfoMap.
        // Without an operand evaluated repeatedly there is no loop to lift out of, and the walk that records every
        // expression is skipped (a port addition: most template bodies have none, and the record cost 8% of a compile).
        private static bool HasRepeatedOperand(Expression exp)
        {
            ExpressionTool.ProbeTreeDepth();
            foreach (Operand o in exp.Operands())
            {
                if (o.IsEvaluatedRepeatedly() || HasRepeatedOperand(o.GetChildExpression()))
                {
                    return true;
                }
            }

            return false;
        }

        private void GatherInfo(Expression exp)
        {
            GatherInfo(exp, 0, false);
        }

        private void GatherInfo(Expression exp, int loopLevel, bool multiThreaded)
        {
            ExpressionTool.ProbeTreeDepth();
            var info = new ExpInfo { Expression = exp, LoopLevel = loopLevel, MultiThreaded = multiThreaded };
            expInfoMap[exp] = info;
            Expression scope = exp.ScopingExpression;
            if (scope != null)
            {
                MarkDependencies(exp, scope);
            }

            bool threaded = multiThreaded || exp.IsMultiThreaded(config);

            // Don't loop-lift out of a conditional, because it can lead to type errors,
            // or out of a try/catch, because it can lead to errors not being caught
            Expression choose = GetContainingConditional(exp);
            if (choose != null)
            {
                MarkDependencies(exp, choose);
            }

            foreach (Operand o in exp.Operands())
            {
                GatherInfo(o.GetChildExpression(), o.IsEvaluatedRepeatedly() ? loopLevel + 1 : loopLevel, threaded);
            }
        }

        // Saxon's ConditionalInstruction, which in this edition only Choose implements.
        private static Expression GetContainingConditional(Expression exp)
        {
            Expression parent = exp.ParentExpression;
            while (parent != null)
            {
                if (parent is Choose)
                {
                    Operand o = ExpressionTool.FindOperand(parent, exp);
                    if (o == null)
                    {
                        throw new System.InvalidOperationException("Loop lifting: " + exp.ToShortString() + " is not an operand of its parent");
                    }

                    if (o.OperandRole.IsInChoiceGroup())
                    {
                        return parent;
                    }
                }
                else if (parent is TryCatch)
                {
                    return parent;
                }

                exp = parent;
                parent = parent.ParentExpression;
            }

            return null;
        }

        // bug 3465: expressions returning streamed nodes cannot be loop-lifted, because such nodes must not be bound to
        // a variable
        private bool MayReturnStreamedNodes(Expression exp)
        {
            return streaming && !exp.GetItemType().GetUType().Intersection(UType.ANY_NODE).Equals(UType.VOID);
        }

        // Register the dependencies of an expression, and of its ancestors below it, on an ancestor that binds a
        // variable or the focus.
        private void MarkDependencies(Expression exp, Expression variableSetter)
        {
            if (variableSetter != null)
            {
                Expression parent = exp;
                while (parent != null && parent != variableSetter)
                {
                    ExpInfo parentInfo = expInfoMap[parent];
                    (parentInfo.Dependees ??= new ExpressionSet()).Add(variableSetter);
                    parent = parent.ParentExpression;
                }
            }
        }

        private void LoopLift(Expression exp)
        {
            ExpressionTool.ProbeTreeDepth();
            ExpInfo info = expInfoMap[exp];
            if (!info.MultiThreaded)
            {
                if (info.LoopLevel > 0 && exp.NetCost > 0)
                {
                    if (info.Dependees == null && exp.IsLiftable(streaming) && !MayReturnStreamedNodes(exp))
                    {
                        if (!IsFull(root))
                        {
                            root = Lift(exp, root);
                        }
                    }
                    else
                    {
                        Expression child = exp;
                        ExpInfo expInfo = expInfoMap[exp];
                        Expression parent = exp.ParentExpression;
                        int eagerLeft = eagerDepth;
                        while (parent != null)
                        {
                            if (expInfo.Dependees != null && expInfo.Dependees.Contains(parent))
                            {
                                ExpInfo childInfo = expInfoMap[child];
                                if (expInfo.LoopLevel != childInfo.LoopLevel)
                                {
                                    Operand o = ExpressionTool.FindOperand(parent, child);
                                    if (exp.IsLiftable(streaming) && !(child is PseudoExpression) && !o.OperandRole.IsConstrainedClass() && eagerLeft == 0 && !IsFull(child))
                                    {
                                        Expression lifted = Lift(exp, child);
                                        o.SetChildExpression(lifted);
                                    }
                                }

                                break;
                            }

                            if (IsEagerSequence(parent, child))
                            {
                                eagerLeft--;
                            }

                            child = parent;
                            parent = parent.ParentExpression;
                        }
                    }
                }

                foreach (Operand o in exp.Operands())
                {
                    if (!o.OperandRole.IsConstrainedClass())
                    {
                        Expression operand = o.GetChildExpression();
                        bool eager = IsEagerSequence(exp, operand);
                        eagerDepth += eager ? 1 : 0;
                        LoopLift(operand);
                        eagerDepth -= eager ? 1 : 0;
                    }
                }
            }
        }

        // A lazy variable is never placed inside the value of a variable that must be evaluated eagerly: such a variable
        // is evaluated before an xsl:try that uses it so that its errors are not caught, and a lazy variable lifted into
        // its value deferred them into the try (xslt30 try-031, which Saxon 12.9 fails so). Lifting above it is safe.
        private static bool IsEagerSequence(Expression parent, Expression child)
        {
            return parent is LetExpression let && let.IsNeedsEagerEvaluation() && child == let.Sequence;
        }

        // Whether MaxStackedLets lifted lets already stand at the target, the top of their chain.
        private bool IsFull(Expression target)
        {
            int n = 0;
            for (Expression e = target; e is LetExpression let && liftedLets.Contains(let); e = let.GetAction())
            {
                if (++n >= MaxStackedLets)
                {
                    return true;
                }
            }

            return false;
        }

        private Expression Lift(Expression child, Expression newAction)
        {
            changed = true;
            ExpInfo childInfo = expInfoMap[child];
            ExpInfo actionInfo = expInfoMap[newAction];
            int hoist = childInfo.LoopLevel - actionInfo.LoopLevel;
            Expression oldParent = child.ParentExpression;
            Operand oldOperand = ExpressionTool.FindOperand(oldParent, child);

            var let = new LetExpression();
            let.SetVariableQName(new StructuredQName("vv", NamespaceUri.SAXON_GENERATED_VARIABLE, "v" + sequence++));
            SequenceType type = SequenceType.MakeSequenceType(child.GetItemType(), child.GetCardinality());
            let.SetRequiredType(type);
            ExpressionTool.CopyLocationInfo(child, let);
            let.Sequence = child;
            let.SetNeedsLazyEvaluation(true);
            let.SetAction(newAction);
            let.AdoptChildExpression(newAction);

            var letInfo = new ExpInfo { Expression = let, Dependees = childInfo.Dependees, LoopLevel = actionInfo.LoopLevel };
            if (childInfo.Dependees != null && actionInfo.Dependees != null)
            {
                letInfo.Dependees.AddAll(actionInfo.Dependees);
            }

            expInfoMap[let] = letInfo;
            liftedLets.Add(let);
            ExpressionTool.ProcessExpressionTree(child, null, (expression, result) =>
            {
                expInfoMap[expression].LoopLevel -= hoist;
                return false;
            });

            var var = new LocalVariableReference(let);
            int properties = child.GetSpecialProperties() & StaticProperty.NOT_UNTYPED_ATOMIC;
            var.SetStaticType(type, null, properties);
            var.SetInLoop(true);
            let.AddReference(var, true);
            ExpressionTool.CopyLocationInfo(child, var);
            oldOperand.SetChildExpression(var);
            if (tracing)
            {
                Logger err = config.Logger;
                err.Info("OPT : At line " + child.GetLocation().GetLineNumber() + " of " + child.GetLocation().GetSystemId());
                err.Info("OPT : Lifted (" + child.ToShortString() + ") above (" + newAction.ToShortString() + ") on line " + newAction.GetLocation().GetLineNumber());
                err.Info("OPT : Expression after rewrite: " + let);
            }

            return let;
        }
    }
}
