////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/expr/IdentityComparison.java (replaces the Phase 4.8c throwing stub).
// Implements the node identity/order operators: is, << (precedes), >> (follows).

using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions
{
    internal sealed class IdentityComparison : BinaryExpression
    {
        // Set for an "X is Y" comparison emulating generate-id(X) = generate-id(Y): the empty-sequence
        // handling differs (both empty -> true for generate-id, () for is).
        private bool generateIdEmulation;

        public override string ExpressionName => "nodeComparison";

        public IdentityComparison(Expression p1, int op, Expression p2) : base(p1, op, p2)
        {
        }

        public bool IsGenerateIdEmulation()
        {
            return generateIdEmulation;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Lhs.TypeCheck(visitor, contextInfo);
            Rhs.TypeCheck(visitor, contextInfo);

            if (!generateIdEmulation)
            {
                if (Literal.IsEmptySequence(GetLhsExpression()) || Literal.IsEmptySequence(GetRhsExpression()))
                {
                    return Literal.MakeEmptySequence();
                }
            }

            System.Func<RoleDiagnostic> role0 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, Token.tokens[Operator], 0);
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
            SetLhsExpression(tc.StaticTypeCheck(GetLhsExpression(), SequenceType.OPTIONAL_NODE, role0, visitor));

            System.Func<RoleDiagnostic> role1 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, Token.tokens[Operator], 1);
            SetRhsExpression(tc.StaticTypeCheck(GetRhsExpression(), SequenceType.OPTIONAL_NODE, role1, visitor));

            if (!Cardinality.AllowsZero(GetLhsExpression().GetCardinality()) && !Cardinality.AllowsZero(GetRhsExpression().GetCardinality()))
            {
                generateIdEmulation = false;
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Expression r = base.Optimize(visitor, contextItemType);
            if (r != this)
            {
                if (!generateIdEmulation)
                {
                    if (Literal.IsEmptySequence(GetLhsExpression()) || Literal.IsEmptySequence(GetRhsExpression()))
                    {
                        return Literal.MakeEmptySequence();
                    }
                }
            }

            return r;
        }

        protected override OperandRole GetOperandRole(int arg)
        {
            return OperandRole.INSPECT;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            IdentityComparison ic = new IdentityComparison(GetLhsExpression().Copy(rebindings), Operator, GetRhsExpression().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, ic);
            ic.generateIdEmulation = generateIdEmulation;
            return ic;
        }

        protected override string Tag()
        {
            switch (Operator)
            {
                case Token.IS: return "is";
                case Token.PRECEDES: return "precedes";
                case Token.FOLLOWS: return "follows";
                default: return "?";
            }
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            NodeInfo node0 = GetNode(GetLhsExpression(), context);
            if (node0 == null)
            {
                if (generateIdEmulation)
                {
                    return BooleanValue.Get(GetNode(GetRhsExpression(), context) == null);
                }

                return null;
            }

            NodeInfo node1 = GetNode(GetRhsExpression(), context);
            if (node1 == null)
            {
                if (generateIdEmulation)
                {
                    return BooleanValue.FALSE;
                }

                return null;
            }

            return BooleanValue.Get(CompareIdentity(node0, node1));
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            NodeInfo node0 = GetNode(GetLhsExpression(), context);
            if (node0 == null)
            {
                return generateIdEmulation && GetNode(GetRhsExpression(), context) == null;
            }

            NodeInfo node1 = GetNode(GetRhsExpression(), context);
            return node1 != null && CompareIdentity(node0, node1);
        }

        private bool CompareIdentity(NodeInfo node0, NodeInfo node1)
        {
            switch (Operator)
            {
                case Token.IS:
                    return node0.Equals(node1);
                case Token.PRECEDES:
                    return GlobalOrderComparer.GetInstance().Compare(node0, node1) < 0;
                case Token.FOLLOWS:
                    return GlobalOrderComparer.GetInstance().Compare(node0, node1) > 0;
                default:
                    throw new System.NotSupportedException("Unknown node identity test");
            }
        }

        private static NodeInfo GetNode(Expression exp, IXPathContext c)
        {
            return (NodeInfo)exp.EvaluateItem(c);
        }

        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.BOOLEAN;
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.BOOLEAN;
        }

        protected override string DisplayOperator()
        {
            if (generateIdEmulation)
            {
                return "is-g";
            }

            return base.DisplayOperator();
        }

        public override Elaborator GetElaborator()
        {
            return new IdentityComparisonElaborator();
        }

        /// <summary>Elaborator for an identity comparison (operators is, &lt;&lt;, &gt;&gt;).</summary>
        internal sealed class IdentityComparisonElaborator : ItemElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                IdentityComparison exp = (IdentityComparison)GetExpression();
                IItemEvaluator p0 = exp.GetLhsExpression().MakeElaborator().ElaborateForItem();
                IItemEvaluator p1 = exp.GetRhsExpression().MakeElaborator().ElaborateForItem();
                bool nullable0 = Cardinality.AllowsZero(exp.GetLhsExpression().GetCardinality());
                bool nullable1 = Cardinality.AllowsZero(exp.GetRhsExpression().GetCardinality());
                int op = exp.Operator;
                bool gie = exp.IsGenerateIdEmulation();

                switch (op)
                {
                    case Token.IS:
                        if (nullable0 || nullable1)
                        {
                            if (gie)
                            {
                                return (context) =>
                                {
                                    NodeInfo v0 = (NodeInfo)p0.Eval(context);
                                    NodeInfo v1 = (NodeInfo)p1.Eval(context);
                                    if (v0 == null)
                                    {
                                        return v1 == null;
                                    }
                                    return v0.Equals(v1);
                                };
                            }
                            else
                            {
                                return (context) =>
                                {
                                    NodeInfo v0 = (NodeInfo)p0.Eval(context);
                                    NodeInfo v1 = (NodeInfo)p1.Eval(context);
                                    if (v0 == null || v1 == null)
                                    {
                                        return false;
                                    }
                                    return v0.Equals(v1);
                                };
                            }
                        }
                        else
                        {
                            return (context) => p0.Eval(context).Equals(p1.Eval(context));
                        }

                    case Token.PRECEDES:
                        return (context) =>
                        {
                            NodeInfo v0 = (NodeInfo)p0.Eval(context);
                            NodeInfo v1 = (NodeInfo)p1.Eval(context);
                            if (v0 == null || v1 == null)
                            {
                                return false;
                            }
                            return GlobalOrderComparer.GetInstance().Compare(v0, v1) < 0;
                        };

                    case Token.FOLLOWS:
                        return (context) =>
                        {
                            NodeInfo v0 = (NodeInfo)p0.Eval(context);
                            NodeInfo v1 = (NodeInfo)p1.Eval(context);
                            if (v0 == null || v1 == null)
                            {
                                return false;
                            }
                            return GlobalOrderComparer.GetInstance().Compare(v0, v1) > 0;
                        };

                    default:
                        throw new System.InvalidOperationException();
                }
            }

            public override IItemEvaluator ElaborateForItem()
            {
                IdentityComparison exp = (IdentityComparison)GetExpression();
                IItemEvaluator p0 = exp.GetLhsExpression().MakeElaborator().ElaborateForItem();
                IItemEvaluator p1 = exp.GetRhsExpression().MakeElaborator().ElaborateForItem();
                bool nullable0 = Cardinality.AllowsZero(exp.GetLhsExpression().GetCardinality());
                bool nullable1 = Cardinality.AllowsZero(exp.GetRhsExpression().GetCardinality());
                int op = exp.Operator;
                bool gie = exp.IsGenerateIdEmulation();

                switch (op)
                {
                    case Token.IS:
                        if (nullable0 || nullable1)
                        {
                            if (gie)
                            {
                                return (context) =>
                                {
                                    NodeInfo v0 = (NodeInfo)p0.Eval(context);
                                    NodeInfo v1 = (NodeInfo)p1.Eval(context);
                                    if (v0 == null)
                                    {
                                        return v1 == null ? (IItem)BooleanValue.TRUE : BooleanValue.FALSE;
                                    }
                                    if (v1 == null)
                                    {
                                        return BooleanValue.FALSE;
                                    }
                                    return BooleanValue.Get(v0.Equals(v1));
                                };
                            }
                            else
                            {
                                return (context) =>
                                {
                                    NodeInfo v0 = (NodeInfo)p0.Eval(context);
                                    if (v0 == null)
                                    {
                                        return null;
                                    }
                                    NodeInfo v1 = (NodeInfo)p1.Eval(context);
                                    if (v1 == null)
                                    {
                                        return null;
                                    }
                                    return BooleanValue.Get(v0.Equals(v1));
                                };
                            }
                        }
                        else
                        {
                            return (context) => BooleanValue.Get(p0.Eval(context).Equals(p1.Eval(context)));
                        }

                    case Token.PRECEDES:
                        return (context) =>
                        {
                            NodeInfo v0 = (NodeInfo)p0.Eval(context);
                            if (v0 == null)
                            {
                                return null;
                            }
                            NodeInfo v1 = (NodeInfo)p1.Eval(context);
                            if (v1 == null)
                            {
                                return null;
                            }
                            return BooleanValue.Get(GlobalOrderComparer.GetInstance().Compare(v0, v1) < 0);
                        };

                    case Token.FOLLOWS:
                        return (context) =>
                        {
                            NodeInfo v0 = (NodeInfo)p0.Eval(context);
                            if (v0 == null)
                            {
                                return null;
                            }
                            NodeInfo v1 = (NodeInfo)p1.Eval(context);
                            if (v1 == null)
                            {
                                return null;
                            }
                            return BooleanValue.Get(GlobalOrderComparer.GetInstance().Compare(v0, v1) > 0);
                        };

                    default:
                        throw new System.InvalidOperationException();
                }
            }
        }
    }
}
