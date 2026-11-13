////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;

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
        private bool finished;
        private IItem previous;
        private IItem current;
        private IItem next;
        private int position = -1;
        private readonly List<WindowClause.Window> currentWindows = new List<WindowClause.Window>();
        private long added;   // members added to open windows, for WindowClause.Added
        private long visits;  // windows looked at, for WindowClause.Visited

        public WindowClausePull(TuplePull source, WindowClause windowClause)
        {
            this.windowClause = windowClause;
            this.source = source;
        }

        public override bool NextTuple(IXPathContext context)
        {
            StackGuard.Probe();
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
                    WindowClause.Visited(currentWindows, ref visits);
                    // See if we need to start a new window
                    if ((windowClause.IsSlidingWindow() || currentWindows.Count == 0 || autoClose) &&
                            windowClause.MatchesStart(previous, current, next, position, context))
                    {
                        // See if we need to end the previous window
                        if (autoClose && currentWindows.Count != 0)
                        {
                            // automatically end the previous window
                            WindowClause.Window w0 = currentWindows[0];
                            w0.End(previous, position - 1, oldPrevious, current);
                            earliest = Despatch(w0);
                            currentWindows.Clear();
                        }

                        // Create the new window
                        currentWindows.Add(new WindowClause.Window(current, position, previous, next));
                    }

                    // Add the current item to all active windows
                    foreach (WindowClause.Window active in currentWindows)
                    {
                        if (!active.IsFinished())
                        {
                            active.contents.Add(current);
                            WindowClause.Added(currentWindows, ref added);
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
                                w.End(current, position, previous, next);
                                if (earliest == null)
                                {
                                    earliest = Despatch(w);
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

        private WindowClause.Window Despatch(WindowClause.Window w)
        {
            windowClause.CheckWindowContents(w);

            // In ordered mode, we must despatch windows in order of start position not in order of end
            // position. So we don't despatch it yet if there are unfinished windows with an earlier start.
            return LookForEarliest();
        }

        private WindowClause.Window LookForEarliest()
        {
            WindowClause.Window w = WindowClause.EarliestFinished(currentWindows, ref visits);
            if (w != null)
            {
                w.despatched = true;
            }

            return w;
        }

        private void ProcessWindow(WindowClause.Window w, IXPathContext context)
        {
            windowClause.BindWindow(w, context);
            w.despatched = true;
        }

        public override void Dispose()
        {
            StackGuard.Probe();
            baseIterator?.Dispose();
            source.Dispose();
        }
    }
}
