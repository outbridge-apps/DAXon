////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;

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
        private long visits;  // windows looked at, for WindowClause.Visited

        public WindowClausePush(Outputter outputter, TuplePush destination, WindowClause windowClause) : base(outputter)
        {
            this.windowClause = windowClause;
            this.destination = destination;
        }

        public override void ProcessTuple(IXPathContext context)
        {
            StackGuard.Probe();
            currentWindows = new List<WindowClause.Window>();
            long added = 0;   // members added to open windows, for WindowClause.Added
            visits = 0;
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
                    WindowClause.Visited(currentWindows, ref visits);
                    if ((windowClause.IsSlidingWindow() || currentWindows.Count == 0 || autoClose) &&
                            windowClause.MatchesStart(previous, current, next, position, context))
                    {
                        if (autoClose && currentWindows.Count != 0)
                        {
                            // automatically end the previous window
                            WindowClause.Window w0 = currentWindows[0];
                            w0.End(previous, position - 1, previousPrevious, current);
                            Despatch(w0, context);
                            currentWindows.Clear();
                        }

                        currentWindows.Add(new WindowClause.Window(current, position, previous, next));
                    }

                    foreach (WindowClause.Window active in currentWindows)
                    {
                        if (!active.IsFinished())
                        {
                            active.contents.Add(current);
                            WindowClause.Added(currentWindows, ref added);
                        }
                    }

                    if (windowClause.EndCondition != null)
                    {
                        List<WindowClause.Window> removals = new List<WindowClause.Window>();
                        foreach (WindowClause.Window w in currentWindows)
                        {
                            if (!w.IsFinished() && windowClause.MatchesEnd(w, previous, current, next, position, context))
                            {
                                w.End(current, position, previous, next);
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
                    w.End(current, position, previous, null);
                    Despatch(w, context);
                }
            }
        }

        private void Despatch(WindowClause.Window w, IXPathContext context)
        {
            windowClause.CheckWindowContents(w);

            for (WindowClause.Window earliest; (earliest = WindowClause.EarliestFinished(currentWindows, ref visits)) != null;)
            {
                windowClause.BindWindow(earliest, context);
                destination.ProcessTuple(context);
                earliest.despatched = true;
            }
        }

        public override void Dispose()
        {
            StackGuard.Probe();
            currentWindows = null;
            destination.Dispose();
        }
    }
}
