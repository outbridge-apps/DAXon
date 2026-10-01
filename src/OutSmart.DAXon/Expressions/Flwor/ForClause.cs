////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using static OutSmart.DAXon.Expressions.Flwor.Clause.ClauseName;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// A "for" clause in a FLWOR expression
    /// </summary>
    internal sealed class ForClause : Clause
    {
        protected LocalVariableBinding rangeVariable;
        protected LocalVariableBinding positionVariable;
        protected Operand sequenceOp;
        protected volatile IPullEvaluator sequenceOperandEvaluator;   // volatile: published once, read hot (round 11)
        protected bool allowsEmpty;


        public override ClauseName ClauseKey => FOR;

        public Expression Sequence
        {
            get => sequenceOp.GetChildExpression(); set
            {
                sequenceOp.SetChildExpression(value);
            }
        }

        public LocalVariableBinding RangeVariable
        {
            get => rangeVariable; set
            {
                rangeVariable = value;
            }
        }

        public LocalVariableBinding PositionVariable
        {
            get => positionVariable; set
            {
                positionVariable = value;
            }
        }

        public override LocalVariableBinding[] RangeVariables
        {
            get
            {
                if (positionVariable == null)
                {
                    return new LocalVariableBinding[]
                    {
                    rangeVariable
                    };
                }
                else
                {
                    return new LocalVariableBinding[]
                    {
                    rangeVariable,
                    positionVariable
                    };
                }
            }
        }
        public ForClause()
        {
        }

        public override Clause Copy(FLWORExpression flwor, RebindingMap rebindings)
        {
            ForClause f2 = new ForClause();
            f2.Location = Location;
            f2.SetPackageData(GetPackageData());
            f2.rangeVariable = rangeVariable.Copy();
            if (positionVariable != null)
            {
                f2.positionVariable = positionVariable.Copy();
            }

            f2.InitSequence(flwor, Sequence.Copy(rebindings));
            f2.allowsEmpty = allowsEmpty;
            return f2;
        }

        public void InitSequence(FLWORExpression flwor, Expression sequence)
        {
            sequenceOp = new Operand(flwor, sequence, IsRepeated() ? OperandRole.REPEAT_NAVIGATE : OperandRole.NAVIGATE);
        }

        protected internal ISequenceIterator GetIterator(IXPathContext context)
        {
            if (sequenceOperandEvaluator == null)
            {
                sequenceOperandEvaluator = Sequence.MakeElaborator().ElaborateForPull();
            }

            return sequenceOperandEvaluator.Iterate(context);
        }

        public void SetAllowingEmpty(bool option)
        {
            allowsEmpty = option;
        }

        public bool IsAllowingEmpty()
        {
            return allowsEmpty;
        }

        public override void TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            SequenceType decl = rangeVariable.GetRequiredType();
            if (allowsEmpty && !Cardinality.AllowsZero(decl.GetCardinality()))
            {
                Func<RoleDiagnostic> emptyRole = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, rangeVariable.GetVariableQName().DisplayName, 0);
                Expression checker = CardinalityChecker.MakeCardinalityChecker(Sequence, StaticProperty.ALLOWS_ONE_OR_MORE, emptyRole);
                Sequence = checker;
            }

            SequenceType sequenceType = SequenceType.MakeSequenceType(decl.PrimaryType, StaticProperty.ALLOWS_ZERO_OR_MORE);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, rangeVariable.GetVariableQName().DisplayName, 0);
            if (visitor.StaticContext.GetXPathVersion() < 40)
            {
                Sequence = TypeChecker.StrictTypeCheck(Sequence, sequenceType, role, visitor.StaticContext);
            }
            else
            {
                TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
                Sequence = tc.StaticTypeCheck(Sequence, sequenceType, role, visitor);
            }
        }

        public override TuplePull GetPullStream(TuplePull @base, IXPathContext context)
        {
            if (allowsEmpty)
            {
                return new ForClauseOuterPull(@base, this);
            }
            else
            {
                return new ForClausePull(@base, this);
            }
        }

        public override TuplePush GetPushStream(TuplePush destination, Outputter output, IXPathContext context)
        {
            if (allowsEmpty)
            {
                return new ForClauseOuterPush(output, destination, this);
            }
            else
            {
                return new ForClausePush(output, destination, this);
            }
        }

        public bool AddPredicate(FLWORExpression flwor, ExpressionVisitor visitor, ContextItemStaticInfo contextItemType, Expression condition)
        {
            Configuration config = GetConfiguration();
            Optimizer opt = visitor.ObtainOptimizer();
            bool debug = config.GetBooleanProperty(Feature<bool>.TRACE_OPTIMIZER_DECISIONS);

            // assert: condition has no dependency on context item. We removed any such dependency before we got here.
            TypeHierarchy th = config.GetTypeHierarchy();
            Expression head = null;
            Expression selection = Sequence;
            ItemType selectionContextItemType = contextItemType == null ? null : contextItemType.GetItemType();
            if (Sequence is SlashExpression)
            {
                if (((SlashExpression)Sequence).IsAbsolute())
                {
                    head = ((SlashExpression)Sequence).FirstStep;
                    selection = ((SlashExpression)Sequence).RemainingSteps;
                    selectionContextItemType = head.GetItemType();
                }
                else
                {
                    SlashExpression p = ((SlashExpression)Sequence).TryToMakeAbsolute();
                    if (p != null)
                    {
                        Sequence = p;
                        head = p.FirstStep;
                        selection = p.RemainingSteps;
                        selectionContextItemType = head.GetItemType();
                    }
                }
            }

            bool changed = false;
            if (positionVariable != null && positionVariable.NominalReferenceCount == 0)
            {

                // Eliminating an unused position variable opens up optimisation opportunities: bug 4947
                positionVariable = null;
            }


            // Process each term in the where clause independently
            // Upstream also recognises CompareToIntegerConstant here; this port's optimizer never
            // creates that form (the class was an empty stub, now deleted), so it is not tested for.
            if (positionVariable != null && (condition is ValueComparison || condition is GeneralComparison) && ExpressionTool.DependsOnVariable(condition, new IBinding[] { positionVariable }))
            {
                IComparisonExpression comp = (IComparisonExpression)condition;
                Expression[] operands = new Expression[]
                {
                    comp.GetLhsExpression(),
                    comp.GetRhsExpression()
                };
                if (ExpressionTool.DependsOnVariable(flwor, new IBinding[] { positionVariable }))
                {

                    // cannot convert a positional where clause into a positional predicate if there are
                    // other references to the position variable
                    return false;
                }

                for (int op = 0; op < 2; op++)
                {

                    // If the where clause is a simple test on the position variable, for example
                    //    for $x at $p in EXPR where $p = 5 return A
                    // then absorb the where condition into a predicate, rewriting it as
                    //    for $x in EXPR[position() = 5] return A
                    // This takes advantage of the optimizations applied to positional filter expressions
                    // Only do this if the sequence expression has not yet been changed, because
                    // the position in a predicate after the first is different.  And only do it if this
                    // is the only reference to the position variable, because if there are other references,
                    // the existence of the predicate will change the values of the position variable.
                    IBinding[] thisVar = new[]
                    {
                        this.RangeVariable
                    };
                    if (positionVariable != null && operands[op] is VariableReference && !changed)
                    {
                        IList<VariableReference> varRefs = new List<VariableReference>();
                        ExpressionTool.GatherVariableReferences(condition, positionVariable, varRefs);
                        if (varRefs.Count == 1 && varRefs[0] == operands[op] && !ExpressionTool.DependsOnFocus(operands[1 - op]) && !ExpressionTool.DependsOnVariable(operands[1 - op], thisVar))
                        {
                            RetainedStaticContext rsc = new RetainedStaticContext(visitor.StaticContext);
                            Expression position = SystemFunction.MakeCall("position", rsc);
                            Expression predicate = condition.Copy(new RebindingMap());
                            Operand child = op == 0 ? ((IComparisonExpression)predicate).Lhs : ((IComparisonExpression)predicate).Rhs;
                            child.SetChildExpression(position);
                            if (debug)
                            {
                                opt.Trace("Replaced positional variable in predicate by position()", predicate);
                            }

                            selection = new FilterExpression(selection, predicate);
                            ExpressionTool.CopyLocationInfo(predicate, selection);
                            ContextItemStaticInfo cit = config.MakeContextItemStaticInfo(selectionContextItemType, true);
                            selection = selection.TypeCheck(visitor, cit);
                            if (!ExpressionTool.DependsOnVariable(flwor, new IBinding[] { positionVariable }))
                            {
                                positionVariable = null;
                            }

                            changed = true;
                            break;
                        }
                    }
                }
            }

            if (positionVariable == null)
            {
                IBinding[] thisVar = new[]
                {
                    this.RangeVariable
                };
                if (opt.IsVariableReplaceableByDot(condition, thisVar))
                {

                    // When rewriting the where expression as a filter, we have to replace references to the
                    // range variable by references to the context item. If we can do this directly, we do. But
                    // if the reference to the range variable occurs inside a predicate, or on the rhs of slash,
                    // we have to bind a new variable to the context item. So for example "for $x in S where
                    // T[abc = $x]" gets rewritten as "for $x in S[let $dot := . return T[abc = $dot]]"
                    //if (useDotDirectly) {
                    Expression replacement = new ContextItemExpression();
                    bool found = ExpressionTool.InlineVariableReferences(condition, this.RangeVariable, replacement);
                    if (found)
                    {
                        ContextItemStaticInfo cit = config.MakeContextItemStaticInfo(Sequence.GetItemType(), true);
                        Expression predicate = condition.TypeCheck(visitor, cit);

                        // If the result of the predicate might be a number, wrap it in a call of boolean()
                        Affinity rel = th.Relationship(predicate.GetItemType(), BuiltInAtomicType.INTEGER);
                        if (rel != Affinity.DISJOINT)
                        {
                            RetainedStaticContext rsc = new RetainedStaticContext(visitor.StaticContext);
                            predicate = SystemFunction.MakeCall("boolean", rsc, predicate);
                        }

                        selection = new FilterExpression(selection, predicate);
                        ExpressionTool.CopyLocationInfo(predicate, selection);
                        cit = config.MakeContextItemStaticInfo(selectionContextItemType, true);
                        selection = selection.TypeCheck(visitor, cit);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                if (head == null)
                {
                    Sequence = selection;
                }
                else if (head is RootExpression && selection.IsCallOn(typeof(KeyFn)))
                {
                    Sequence = selection;
                }
                else
                {
                    Expression path = ExpressionTool.MakePathExpression(head, selection);
                    if (path is SlashExpression)
                    {
                        ExpressionTool.CopyLocationInfo(condition, path);
                        Expression k = visitor.ObtainOptimizer().ConvertPathExpressionToKey((SlashExpression)path, visitor);
                        Sequence = k == null ? path : k;
                        sequenceOp.TypeCheck(visitor, contextItemType);
                        sequenceOp.Optimize(visitor, contextItemType);
                    }
                }
            }

            return changed;
        }

        public override void ProcessOperands(IOperandProcessor processor)
        {
            processor.ProcessOperand(sequenceOp);
        }

        public override void GatherVariableReferences(ExpressionVisitor visitor, IBinding binding, IList<VariableReference> references)
        {
            ExpressionTool.GatherVariableReferences(Sequence, binding, references);
        }

        public override void RefineVariableType(ExpressionVisitor visitor, IList<VariableReference> references, Expression returnExpr)
        {
            ItemType actualItemType = Sequence.GetItemType();
            if (actualItemType is ErrorType)
            {
                actualItemType = AnyItemType.GetInstance();
            }

            foreach (VariableReference @ref in references)
            {
                @ref.RefineVariableType(actualItemType, allowsEmpty ? StaticProperty.ALLOWS_ZERO_OR_ONE : StaticProperty.EXACTLY_ONE, null, Sequence.GetSpecialProperties());
            }
        }

        public override void AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet varPath = Sequence.AddToPathMap(pathMap, pathMapNodeSet);
            pathMap.RegisterPathForVariable(rangeVariable, varPath);
        }

        public override void Explain(ExpressionPresenter @out)
        {
            @out.StartElement(ClauseKey.ToString().ToLowerInvariant());
            @out.EmitAttribute("var", RangeVariable.GetVariableQName());
            @out.EmitAttribute("slot", RangeVariable.LocalSlotNumber + "");
            LocalVariableBinding posVar = PositionVariable;
            if (posVar != null)
            {
                @out.EmitAttribute("at", posVar.GetVariableQName());
                @out.EmitAttribute("at-slot", posVar.LocalSlotNumber + "");
            }

            Sequence.Export(@out);
            @out.EndElement();
        }

        public override string ToShortString()
        {
            return Stringify(true);
        }

        public override string ToString()
        {
            return Stringify(false);
        }

        private string Stringify(bool abbreviate)
        {
            StringBuilder fsb = new StringBuilder(64);
            fsb.Append(ClauseKey.ToString().ToLowerInvariant());
            fsb.Append(" $");
            fsb.Append(rangeVariable.GetVariableQName().DisplayName);
            fsb.Append(' ');
            LocalVariableBinding posVar = PositionVariable;
            if (posVar != null)
            {
                fsb.Append("at $");
                fsb.Append(posVar.GetVariableQName().DisplayName);
                fsb.Append(' ');
            }

            fsb.Append("in ");
            fsb.Append(abbreviate ? Sequence.ToShortString() : Sequence.ToString());
            return fsb.ToString();
        }
    }
}