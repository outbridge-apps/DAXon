////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// Implements a sliding or tumbling window clause of a FLWOR expression in tuple-pull mode. The entire
    /// window processing is activated once for each input tuple, and it generates one output tuple for each
    /// identified window.
    /// </summary>
    internal sealed class WindowClausePull : TuplePull
    {
        private readonly WindowClause windowClause;
        private readonly TuplePull source;
        private ISequenceIterator baseIterator;
        private bool finished = false;
        private IItem previous = null;
        private IItem current = null;
        private IItem next = null;
        private int position = -1;
        private readonly List<WindowClause.Window> currentWindows = new List<WindowClause.Window>();

        public WindowClausePull(TuplePull source, WindowClause windowClause, IXPathContext context)
        {
            this.windowClause = windowClause;
            this.source = source;
        }

        public override bool NextTuple(IXPathContext context)
        {
            // First see if there are any windows waiting to be delivered
            WindowClause.Window earliest = LookForEarliest();
            if (earliest != null)
            {
                ProcessWindow(earliest, context);
                return true;
            }

            // If we're not currently processing an input sequence, try to get a new one
            if (finished || baseIterator == null)
            {
                if (source.NextTuple(context))
                {
                    baseIterator = windowClause.Sequence.Iterate(context);
                    finished = false;
                    previous = null;
                    position = -1;
                    current = null;
                    next = null;
                }
                else if (baseIterator == null)
                {
                    return false;
                }
            }

            while (!finished)
            {
                // advance the input sequence
                bool autoClose = windowClause.IsTumblingWindow() && windowClause.EndCondition == null;

                IItem oldPrevious = previous;
                previous = current;
                current = next;
                next = baseIterator.Next();
                if (next == null)
                {
                    finished = true;
                    // but still complete this time round the loop
                }

                position++;
                if (position > 0)
                {
                    // See if we need to start a new window
                    if ((windowClause.IsSlidingWindow() || currentWindows.Count == 0 || autoClose) &&
                            windowClause.MatchesStart(previous, current, next, position, context))
                    {
                        // See if we need to end the previous window
                        if (autoClose && currentWindows.Count != 0)
                        {
                            // automatically end the previous window
                            WindowClause.Window w0 = currentWindows[0];
                            w0.endItem = previous;
                            w0.endPreviousItem = oldPrevious;
                            w0.endNextItem = current;
                            w0.endPosition = position - 1;
                            earliest = Despatch(w0, context);
                            currentWindows.Clear();
                        }

                        // Create the new window
                        WindowClause.Window window = new WindowClause.Window();
                        window.startPosition = position;
                        window.startItem = current;
                        window.startPreviousItem = previous;
                        window.startNextItem = next;
                        window.contents = new List<IItem>();
                        currentWindows.Add(window);
                    }

                    // Add the current item to all active windows
                    foreach (WindowClause.Window active in currentWindows)
                    {
                        if (!active.IsFinished())
                        {
                            active.contents.Add(current);
                        }
                    }

                    // See if this item marks the end of any active windows
                    bool explicitEndCondition = windowClause.EndCondition != null;
                    bool implicitEndCondition = finished && windowClause.IsIncludeUnclosedWindows();
                    if (explicitEndCondition || implicitEndCondition)
                    {
                        List<WindowClause.Window> removals = new List<WindowClause.Window>();
                        foreach (WindowClause.Window w in currentWindows)
                        {
                            if (!w.IsFinished() &&
                                    (implicitEndCondition ||
                                             windowClause.MatchesEnd(w, previous, current, next, position, context)))
                            {
                                w.endItem = current;
                                w.endPreviousItem = previous;
                                w.endNextItem = next;
                                w.endPosition = position;
                                if (earliest == null)
                                {
                                    earliest = Despatch(w, context);
                                    if (w.IsDespatched())
                                    {
                                        removals.Add(w);
                                    }
                                }
                            }
                        }

                        foreach (WindowClause.Window w in removals)
                        {
                            currentWindows.Remove(w);
                        }
                    }

                    // if there's a window ready to be delivered, deliver it
                    if (earliest != null)
                    {
                        ProcessWindow(earliest, context);
                        return true;
                    }
                }
            }

            // At the end of the input sequence, there may be a window that hasn't been despatched because
            // earlier windows were still unclosed: see SlidingWindowExpr564
            foreach (WindowClause.Window w in currentWindows)
            {
                if (w.IsFinished() && !w.IsDespatched())
                {
                    ProcessWindow(w, context);
                    currentWindows.Remove(w);
                    return true;
                }
            }

            return false;
        }

        private WindowClause.Window Despatch(WindowClause.Window w, IXPathContext context)
        {
            windowClause.CheckWindowContents(w);

            // In ordered mode, we must despatch windows in order of start position not in order of end
            // position. So we don't despatch it yet if there are unfinished windows with an earlier start.
            return LookForEarliest();
        }

        private WindowClause.Window LookForEarliest()
        {
            int earliestStart = int.MaxValue;
            WindowClause.Window earliestWindow = null;
            foreach (WindowClause.Window u in currentWindows)
            {
                if (u.startPosition < earliestStart && !u.IsDespatched())
                {
                    earliestStart = u.startPosition;
                    earliestWindow = u;
                }
            }

            if (earliestWindow == null || !earliestWindow.IsFinished())
            {
                // if the earliest window is unfinished, we can't do anything yet
                return null;
            }
            else
            {
                earliestWindow.despatched = true;
                return earliestWindow;
            }
        }

        private void ProcessWindow(WindowClause.Window w, IXPathContext context)
        {
            WindowClause clause = windowClause;
            LocalVariableBinding binding;
            binding = clause.GetVariableBinding(WindowClause.WINDOW_VAR);
            context.SetLocalVariable(binding.LocalSlotNumber, SequenceExtent.MakeSequenceExtent(w.contents));

            binding = clause.GetVariableBinding(WindowClause.START_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(w.startItem));
            }

            binding = clause.GetVariableBinding(WindowClause.START_ITEM_POSITION);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, new Int64Value(w.startPosition));
            }

            binding = clause.GetVariableBinding(WindowClause.START_NEXT_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(w.startNextItem));
            }

            binding = clause.GetVariableBinding(WindowClause.START_PREVIOUS_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(w.startPreviousItem));
            }

            binding = clause.GetVariableBinding(WindowClause.END_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(w.endItem));
            }

            binding = clause.GetVariableBinding(WindowClause.END_ITEM_POSITION);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, new Int64Value(w.endPosition));
            }

            binding = clause.GetVariableBinding(WindowClause.END_NEXT_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(w.endNextItem));
            }

            binding = clause.GetVariableBinding(WindowClause.END_PREVIOUS_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(w.endPreviousItem));
            }

            w.despatched = true;
        }

        public override void Dispose()
        {
            baseIterator?.Dispose();
            source.Dispose();
        }
    }
}
