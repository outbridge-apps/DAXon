////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using static OutSmart.DAXon.Expressions.Flwor.Clause.ClauseName;
using System;
using System.Collections.Generic;
using System.Linq;
namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// This class represents an "group by" clause in a FLWOR expression
    /// </summary>
    internal sealed class GroupByClause : Clause
    {
        Configuration config;
        LocalVariableBinding[] bindings; // Variables bound in the output tuple stream.
        internal GenericAtomicComparer[] comparers; // One comparer per grouping variable (accessed by GroupByClausePull/Push)
        Operand retainedTupleOp;
        Operand groupingTupleOp;

        public override ClauseName ClauseKey => GROUP_BY;

        public TupleExpression RetainedTupleExpression
        {
            get => (TupleExpression)retainedTupleOp.GetChildExpression(); set
            {
                retainedTupleOp.SetChildExpression(value);
            }
        }

        public TupleExpression GroupingTupleExpression
        {
            get => (TupleExpression)groupingTupleOp.GetChildExpression(); set
            {
                groupingTupleOp.SetChildExpression(value);
            }
        }

        public override LocalVariableBinding[] RangeVariables => bindings;
        //TupleExpression retainedTupleExpression;  // variables declared in the FLWOR expression other than grouping variables
        //TupleExpression groupingTupleExpression;  // variables listed in the group by clause
        public GroupByClause(Configuration config)
        {
            this.config = config;
        }

        public override bool ContainsNonInlineableVariableReference(IBinding binding)
        {
            return RetainedTupleExpression.IncludesBinding(binding) || GroupingTupleExpression.IncludesBinding(binding);
        }

        public override Clause Copy(FLWORExpression flwor, RebindingMap rebindings)
        {
            GroupByClause g2 = new GroupByClause(config);
            g2.Location = Location;
            g2.SetPackageData(GetPackageData());
            g2.bindings = new LocalVariableBinding[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                g2.bindings[i] = bindings[i].Copy();
            }

            g2.comparers = comparers;
            g2.InitRetainedTupleExpression(flwor, (TupleExpression)RetainedTupleExpression.Copy(rebindings));
            g2.InitGroupingTupleExpression(flwor, (TupleExpression)GroupingTupleExpression.Copy(rebindings));
            return g2;
        }

        public void InitRetainedTupleExpression(FLWORExpression flwor, TupleExpression expr)
        {
            retainedTupleOp = new Operand(flwor, expr, OperandRole.FLWOR_TUPLE_CONSTRAINED);
        }

        public override void Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            IList<LocalVariableBinding> list = new List<LocalVariableBinding>(bindings.ToList());
            IList<LocalVariableReference> retainingExpr = new List<LocalVariableReference>();
            foreach (Operand o in RetainedTupleExpression.Operands())
            {
                retainingExpr.Add((LocalVariableReference)o.GetChildExpression());
            }

            int groupingSize = GroupingTupleExpression.Size;
            for (int i = list.Count - 1; i >= groupingSize; i--)
            {
                if (list[i].NominalReferenceCount == 0)
                {
                    list.RemoveAt(i);
                    retainingExpr.RemoveAt(i - groupingSize);
                }
            }

            bindings = list.ToArray();
            RetainedTupleExpression.SetVariables(retainingExpr);
        }

        public void InitGroupingTupleExpression(FLWORExpression flwor, TupleExpression expr)
        {
            groupingTupleOp = new Operand(flwor, expr, OperandRole.FLWOR_TUPLE_CONSTRAINED);
        }

        public void SetVariableBindings(LocalVariableBinding[] bindings)
        {
            this.bindings = bindings;
        }

        public void SetComparers(GenericAtomicComparer[] comparers)
        {
            this.comparers = comparers;
        }

        public override TuplePull GetPullStream(TuplePull @base, IXPathContext context)
        {
            return new GroupByClausePull(@base, this, context);
        }

        public override TuplePush GetPushStream(TuplePush destination, Outputter output, IXPathContext context)
        {
            return new GroupByClausePush(output, destination, this, context);
        }

        public override void ProcessOperands(IOperandProcessor processor)
        {
            processor.ProcessOperand(groupingTupleOp);
            processor.ProcessOperand(retainedTupleOp);
        }

        public override void Explain(ExpressionPresenter @out)
        {
            @out.StartElement("group-by");
            foreach (Operand o in RetainedTupleExpression.Operands())
            {
                LocalVariableReference @ref = (LocalVariableReference)o.GetChildExpression();
                @out.StartSubsidiaryElement("by");
                @out.EmitAttribute("var", @ref.DisplayName);
                @out.EmitAttribute("slot", @ref.GetBinding().LocalSlotNumber + "");
                @out.EndSubsidiaryElement();
            }

            @out.EndElement();
        }

        public override string ToString()
        {
            return "group by ... ";
        }

        public void ProcessGroup(IList<ObjectToBeGrouped> group, IXPathContext context)
        {
            LocalVariableBinding[] bindings = RangeVariables;
            ISequence[] groupingValues = group[0].groupingValues.GetMembers();
            for (int j = 0; j < groupingValues.Length; j++)
            {
                ISequence v = groupingValues[j];
                context.SetLocalVariable(bindings[j].LocalSlotNumber, v);
            }

            for (int j = groupingValues.Length; j < bindings.Length; j++)
            {
                IList<IItem> concatenatedValue = new List<IItem>();
                foreach (ObjectToBeGrouped otbg in group)
                {
                    ISequence val = otbg.retainedValues.GetMembers()[j - groupingValues.Length];
                    ISequenceIterator si = val.Iterate();
                    IItem it;
                    while ((it = si.Next()) != null)
                    {
                        concatenatedValue.Add(it);
                    }
                }

                SequenceExtent se = new SequenceExtent.Of<IItem>(concatenatedValue);
                context.SetLocalVariable(bindings[j].LocalSlotNumber, se);
            }
        }

        public TupleComparisonKey GetComparisonKey(Tuple t, GenericAtomicComparer[] comparers)
        {
            return new TupleComparisonKey(t.GetMembers(), comparers);
        }

        public override void AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            throw new NotSupportedException("Cannot use document projection with group-by");
        }

        internal sealed class ObjectToBeGrouped
        {
            public Tuple groupingValues;
            public Tuple retainedValues;
        }

        internal sealed class TupleComparisonKey
        {
            // Note: this is over-engineered. Each grouping value is required to be either a single atomic
            // value or an empty sequence.
            private readonly ISequence[] groupingValues;
            private readonly GenericAtomicComparer[] comparers;
            public TupleComparisonKey(ISequence[] groupingValues, GenericAtomicComparer[] comparers)
            {
                this.groupingValues = groupingValues;
                this.comparers = comparers;
            }

            public override int GetHashCode()
            {
                int h = 0x77557755 ^ groupingValues.Length;
                for (int i = 0; i < groupingValues.Length; i++)
                {
                    GenericAtomicComparer comparer = comparers[i];
                    int implicitTimezone = comparer.Context.GetImplicitTimezone();
                    try
                    {
                        ISequenceIterator atoms = groupingValues[i].Iterate();
                        while (true)
                        {
                            AtomicValue val = (AtomicValue)atoms.Next();
                            if (val == null)
                            {
                                break;
                            }

                            h ^= i + val.GetXPathMatchKey(comparer.Collator, implicitTimezone).GetHashCode();
                        }
                    }
                    catch (XPathException e)
                    {
                    }
                }

                return h;
            }

            public override bool Equals(object other)
            {
                if (!(other is TupleComparisonKey))
                {
                    return false;
                }

                if (groupingValues.Length != ((TupleComparisonKey)other).groupingValues.Length)
                {
                    return false;
                }

                for (int i = 0; i < groupingValues.Length; i++)
                {
                    try
                    {
                        if (!DAXonDeepEqual.DeepEqual(groupingValues[i].Iterate(), ((TupleComparisonKey)other).groupingValues[i].Iterate(), comparers[i], comparers[i].Context, 0))
                        {
                            return false;
                        }
                    }
                    catch (XPathException e)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}