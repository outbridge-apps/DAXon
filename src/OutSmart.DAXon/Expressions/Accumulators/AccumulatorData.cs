////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Accumulators
{
    /// <summary>
    /// Holds the values of an accumulator function for one non-streamed document
    /// </summary>
    internal sealed class AccumulatorData : IIAccumulatorData
    {
        private readonly Accumulator accumulator;
        private readonly IList<DataPoint> values = new List<DataPoint>();
        private bool building;
        public AccumulatorData(Accumulator acc)
        {
            this.accumulator = acc;
        }

        public Accumulator GetAccumulator()
        {
            return accumulator;
        }

        public void BuildIndex(NodeInfo doc, IXPathContext context)
        {

            try
            {
                if (building)
                {
                    throw new XPathException("Accumulator " + accumulator.AccumulatorName.DisplayName + " requires access to its own value", "XTDE3400");
                }

                building = true;
                Expression initialValue = accumulator.InitialValueExpression;
                XPathContextMajor c2 = context.NewContext();
                SlotManager sf = accumulator.SlotManagerForInitialValueExpression;
                ISequence[] slots = new ISequence[sf.NumberOfVariables];
                c2.SetStackFrame(sf, slots);
                c2.SetCurrentIterator(new ManualIterator(doc));
                ISequence val = SequenceTool.ToGroundedValue(initialValue.Iterate(c2));
                values.Add(new DataPoint(new Visit(doc, false), val));
                ITraceListener listener = null;
                if (context.GetController().IsTracing())
                {
                    listener = context.GetController().GetTraceListener();
                }

                val = VisitFn(doc, val, c2, listener);
                values.Add(new DataPoint(new Visit(doc, true), val));
                /* trimToSize(): no-op memory hint, removed (CS0201) */
                building = false;
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            } //diagnosticPrint();
        }

        /*
     * Diagnostic output of the entire data structure
     */
        //    }
        // Walks the document without recursing per level: its depth is the input's, and a deep one
        // overflowed here and was reported as a cyclic accumulator (XTDE3400).
        private ISequence VisitFn(NodeInfo root, ISequence value, IXPathContext context, ITraceListener listener)
        {
            try
            {
                // Rules can build another accumulator's index, so walks nest through here.
                StackGuard.Probe();
                Controller run = context.GetController();
                var open = new Stack<KeyValuePair<NodeInfo, IEnumerator<NodeInfo>>>();
                value = Enter(root, value, context, listener);
                open.Push(new KeyValuePair<NodeInfo, IEnumerator<NodeInfo>>(root, root.Children().GetEnumerator()));
                while (open.Count > 0)
                {
                    run?.CheckTimeout();   // the walk is as long as the document, as a copy
                    KeyValuePair<NodeInfo, IEnumerator<NodeInfo>> top = open.Peek();
                    if (top.Value.MoveNext())
                    {
                        NodeInfo kid = top.Value.Current;
                        value = Enter(kid, value, context, listener);
                        open.Push(new KeyValuePair<NodeInfo, IEnumerator<NodeInfo>>(kid, kid.Children().GetEnumerator()));
                    }
                    else
                    {
                        open.Pop();
                        value = Leave(top.Key, value, context, listener);
                    }
                }

                return value;
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                // Filtered: nested walks each have this catch and the innermost describes. XTDE3400 stays
                // uncatchable by xsl:try, as when it was an XPathException.StackOverflow.
                throw e.Describe("Too many nested accumulator evaluations. The accumulator definition may have cyclic dependencies", "XTDE3400", accumulator);
            }
        }

        private ISequence Enter(NodeInfo node, ISequence value, IXPathContext context, ITraceListener listener)
        {
            if (listener != null)
            {
                listener.StartCurrentItem(node);
            }

            ((ManualIterator)context.GetCurrentIterator()).SetContextItem(node);
            Rule rule = accumulator.PreDescentRules.GetRule(node, context);
            if (rule != null)
            {
                value = ProcessRule(rule, node, false, value, context);
                LogChange(node, value, context, " BEFORE ");
            }

            return value;
        }

        private ISequence Leave(NodeInfo node, ISequence value, IXPathContext context, ITraceListener listener)
        {
            ((ManualIterator)context.GetCurrentIterator()).SetContextItem(node);
            Rule rule = accumulator.PostDescentRules.GetRule(node, context);
            if (rule != null)
            {
                value = ProcessRule(rule, node, true, value, context);
                LogChange(node, value, context, " AFTER ");
            }

            if (listener != null)
            {
                listener.EndCurrentItem(node);
            }

            return value;
        }

        /*
     * Diagnostic output of the entire data structure
     */
        //    }
        private void LogChange(NodeInfo node, ISequence value, IXPathContext context, string phase)
        {
            if (accumulator.IsTracing())
            {
                context.GetConfiguration().Logger.Info(accumulator.AccumulatorName.DisplayName + phase + Navigator.GetPath(node) + ": " + Err.DepictSequence(value));
            }
        }

        /*
     * Diagnostic output of the entire data structure
     */
        private ISequence ProcessRule(Rule rule, NodeInfo node, bool isPostDescent, ISequence value, IXPathContext context)
        {
            AccumulatorRule target = (AccumulatorRule)rule.GetAction();
            XPathContextMajor c2 = context.NewCleanContext();
            Controller controller = c2.GetController();
            ManualIterator initialNode = new ManualIterator(node);
            c2.SetCurrentIterator(initialNode);
            c2.OpenStackFrame(target.GetStackFrameMap());
            c2.SetLocalVariable(0, value);
            c2.SetCurrentComponent(accumulator.DeclaringComponent);
            c2.TemporaryOutputState = StandardNames.XSL_ACCUMULATOR_RULE;
            value = target.NewValueEvaluator.Evaluate(c2).Materialize();

            if (node.GetParent() == null && !isPostDescent && values.Count == 1)
            {

                // Overwrite the accumulator's initial value with the "before document start" value. Bug 4786.
                values.Clear();
            }

            values.Add(new DataPoint(new Visit(node, isPostDescent), value));
            return value;
        }

        /*
     * Diagnostic output of the entire data structure
     */
        public ISequence GetValue(NodeInfo node, bool postDescent)
        {
            Visit visit = new Visit(node, postDescent);
            return Search(0, values.Count, visit); //System.Console.Error.println("Searched " + values.size() + " " + ((TinyNodeImpl) visit.node).getNodeNumber() + " : " + seq);
        }

        /*
     * Diagnostic output of the entire data structure
     */
        private ISequence Search(int start, int end, Visit sought)
        {

            if (start == end)
            {

                // sometimes we want the value for the visit we've found, sometimes for the previous visit
                int rel = sought.CompareTo(values[start].visit);
                if (rel < 0)
                {
                    return values[start - 1].value;
                }
                else
                {
                    return values[start].value;
                }
            }

            int mid = (start + end) / 2;
            if (sought.CompareTo(values[mid].visit) <= 0)
            {
                return Search(start, mid, sought);
            }
            else
            {
                return Search(mid + 1, end, sought);
            } // 9.6:
        }

        /*
     * Diagnostic output of the entire data structure
     */
        /*|| (rel == 0 && sought.isPostDescent)*/
        // 9.6:
        /// <summary>
        /// Class representing one of the two visits to a node during a tree-walk
        /// </summary>
        private sealed class Visit : IComparable<Visit>
        {
            public NodeInfo node;
            public bool isPostDescent;
            public Visit(NodeInfo node, bool isPostDescent)
            {
                this.node = node;
                this.isPostDescent = isPostDescent;
            }

            public int CompareTo(Visit other)
            {
                int relation = Navigator.ComparePosition(node, other.node);
                switch (relation)
                {
                    case AxisInfo.SELF:
                        if (isPostDescent == other.isPostDescent)
                        {
                            return 0;
                        }
                        else
                        {
                            return isPostDescent ? +1 : -1;
                        }

                    case AxisInfo.PRECEDING:
                        return -1;
                    case AxisInfo.FOLLOWING:
                        return +1;
                    case AxisInfo.ANCESTOR:
                        return isPostDescent ? +1 : -1;
                    case AxisInfo.DESCENDANT:
                        return other.isPostDescent ? -1 : +1;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        /*
     * Diagnostic output of the entire data structure
     */
        /*|| (rel == 0 && sought.isPostDescent)*/
        // 9.6:
        /// <summary>
        /// Class representing a value of the accumulator immediately after a particular visit to a node.
        /// </summary>
        private sealed class DataPoint
        {
            public Visit visit;
            public ISequence value;
            public DataPoint(Visit visit, ISequence value)
            {
                this.visit = visit;
                this.value = value;
            }
        }
    }
}
