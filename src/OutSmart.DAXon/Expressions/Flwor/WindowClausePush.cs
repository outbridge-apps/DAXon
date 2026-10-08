////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// Implements a sliding or tumbling window clause of a FLWOR expression in tuple-push mode. The entire
    /// window processing is activated once for each input tuple, and it generates one output tuple for each
    /// identified window.
    /// </summary>
    internal sealed class WindowClausePush : TuplePush
    {
        private readonly WindowClause windowClause;
        private readonly TuplePush destination;
        internal List<WindowClause.Window> currentWindows = new List<WindowClause.Window>();

        public WindowClausePush(Outputter outputter, TuplePush destination, WindowClause windowClause) : base(outputter)
        {
            this.windowClause = windowClause;
            this.destination = destination;
        }

        public override void ProcessTuple(IXPathContext context)
        {
            currentWindows = new List<WindowClause.Window>();
            bool autoClose = windowClause.IsTumblingWindow() && windowClause.EndCondition == null;
            IItem previous = null;
            IItem current = null;
            IItem next = null;
            int position = -1;
            ISequenceIterator iter = windowClause.Sequence.Iterate(context);
            bool finished = false;
            while (!finished)
            {
                IItem previousPrevious = previous;
                previous = current;
                current = next;
                next = iter.Next();
                if (next == null)
                {
                    finished = true;
                    // but still complete this time round the loop
                }

                position++;
                if (position > 0)
                {
                    if ((windowClause.IsSlidingWindow() || currentWindows.Count == 0 || autoClose) &&
                            windowClause.MatchesStart(previous, current, next, position, context))
                    {
                        if (autoClose && currentWindows.Count != 0)
                        {
                            // automatically end the previous window
                            WindowClause.Window w0 = currentWindows[0];
                            w0.endItem = previous;
                            w0.endPreviousItem = previousPrevious;
                            w0.endNextItem = current;
                            w0.endPosition = position - 1;
                            Despatch(w0, context);
                            currentWindows.Clear();
                        }

                        WindowClause.Window window = new WindowClause.Window();
                        window.startPosition = position;
                        window.startItem = current;
                        window.startPreviousItem = previous;
                        window.startNextItem = next;
                        window.contents = new List<IItem>();
                        currentWindows.Add(window);
                    }

                    foreach (WindowClause.Window active in currentWindows)
                    {
                        if (!active.IsFinished())
                        {
                            active.contents.Add(current);
                        }
                    }

                    if (windowClause.EndCondition != null)
                    {
                        List<WindowClause.Window> removals = new List<WindowClause.Window>();
                        foreach (WindowClause.Window w in currentWindows)
                        {
                            if (!w.IsFinished() && windowClause.MatchesEnd(w, previous, current, next, position, context))
                            {
                                w.endItem = current;
                                w.endPreviousItem = previous;
                                w.endNextItem = next;
                                w.endPosition = position;
                                Despatch(w, context);
                                if (w.IsDespatched())
                                {
                                    removals.Add(w);
                                }
                            }
                        }

                        foreach (WindowClause.Window w in removals)
                        {
                            currentWindows.Remove(w);
                        }
                    }
                }
            }

            // on completion, first discard windows that aren't finished and don't auto-close
            if (!windowClause.IsIncludeUnclosedWindows())
            {
                for (int i = currentWindows.Count - 1; i >= 0; i--)
                {
                    if (!currentWindows[i].IsFinished())
                    {
                        currentWindows.RemoveAt(i);
                    }
                }
            }

            // now despatch any remaining windows that are finished or that auto-close
            foreach (WindowClause.Window w in currentWindows)
            {
                if (w.IsFinished())
                {
                    if (!w.IsDespatched())
                    {
                        Despatch(w, context);
                    }
                }
                else if (windowClause.IsIncludeUnclosedWindows())
                {
                    w.endItem = current;
                    w.endPreviousItem = previous;
                    w.endNextItem = null;
                    w.endPosition = position;
                    Despatch(w, context);
                }
            }
        }

        private void Despatch(WindowClause.Window w, IXPathContext context)
        {
            windowClause.CheckWindowContents(w);

            // In ordered mode, we must despatch windows in order of start position not in order of end
            // position. So we don't despatch it yet if there are unfinished windows with an earlier start.
            while (true)
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
                    return;
                }
                else
                {
                    WindowClause clause = windowClause;
                    LocalVariableBinding binding;
                    binding = clause.GetVariableBinding(WindowClause.WINDOW_VAR);
                    context.SetLocalVariable(binding.LocalSlotNumber, SequenceExtent.MakeSequenceExtent(earliestWindow.contents));

                    binding = clause.GetVariableBinding(WindowClause.START_ITEM);
                    if (binding != null)
                    {
                        context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(earliestWindow.startItem));
                    }

                    binding = clause.GetVariableBinding(WindowClause.START_ITEM_POSITION);
                    if (binding != null)
                    {
                        context.SetLocalVariable(binding.LocalSlotNumber, new Int64Value(earliestWindow.startPosition));
                    }

                    binding = clause.GetVariableBinding(WindowClause.START_NEXT_ITEM);
                    if (binding != null)
                    {
                        context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(earliestWindow.startNextItem));
                    }

                    binding = clause.GetVariableBinding(WindowClause.START_PREVIOUS_ITEM);
                    if (binding != null)
                    {
                        context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(earliestWindow.startPreviousItem));
                    }

                    binding = clause.GetVariableBinding(WindowClause.END_ITEM);
                    if (binding != null)
                    {
                        context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(earliestWindow.endItem));
                    }

                    binding = clause.GetVariableBinding(WindowClause.END_ITEM_POSITION);
                    if (binding != null)
                    {
                        context.SetLocalVariable(binding.LocalSlotNumber, new Int64Value(earliestWindow.endPosition));
                    }

                    binding = clause.GetVariableBinding(WindowClause.END_NEXT_ITEM);
                    if (binding != null)
                    {
                        context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(earliestWindow.endNextItem));
                    }

                    binding = clause.GetVariableBinding(WindowClause.END_PREVIOUS_ITEM);
                    if (binding != null)
                    {
                        context.SetLocalVariable(binding.LocalSlotNumber, WindowClause.MakeValue(earliestWindow.endPreviousItem));
                    }

                    destination.ProcessTuple(context);
                    earliestWindow.despatched = true;
                }
            } // and loop round to see if there's another finished window that we can despatch
        }

        public override void Dispose()
        {
            currentWindows = null;
            destination.Dispose();
        }
    }
}
