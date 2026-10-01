////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;

namespace OutSmart.DAXon.Internal
{
    // Readers an evaluation opened and never closed: one that stops early - head(unparsed-text-lines(..))
    // - or fails part-way leaves its reader to the finalizer, and on Windows the file stays locked
    // until a GC. Each API entry point runs inside a scope; leaving the scope closes what is still open.
    internal sealed class RunResources : IDisposable
    {
        [ThreadStatic]
        private static RunResources current;

        private readonly RunResources outer;
        private readonly bool entered;
        private List<IDisposable> open;

        private RunResources(RunResources outer, bool entered)
        {
            this.outer = outer;
            this.entered = entered;
        }

        // A scope for the duration of a call: using (RunResources.Enter()) { ... }
        public static RunResources Enter()
        {
            return current = new RunResources(current, true);
        }

        // A scope a lazy iterator keeps across calls; it is current only between Activate and Restore.
        public static RunResources Detached()
        {
            return new RunResources(null, false);
        }

        public RunResources Activate()
        {
            RunResources saved = current;
            current = this;
            return saved;
        }

        public static void Restore(RunResources saved)
        {
            current = saved;
        }

        // Called by the opener; null when no API call is on the stack (nothing to close it then).
        public static RunResources Track(IDisposable resource)
        {
            RunResources scope = current;
            if (scope != null)
            {
                lock (scope)
                {
                    (scope.open ?? (scope.open = new List<IDisposable>())).Add(resource);
                }
            }

            return scope;
        }

        public void Untrack(IDisposable resource)
        {
            lock (this)
            {
                open?.Remove(resource);
            }
        }

        public void CloseAll()
        {
            List<IDisposable> left;
            lock (this)
            {
                left = open;
                open = null;
            }

            if (left == null)
            {
                return;
            }

            foreach (IDisposable resource in left)
            {
                try
                {
                    resource.Dispose();
                }
                catch (Exception)
                {
                    // closing is best effort; the call's own outcome stands
                }
            }
        }

        public void Dispose()
        {
            if (entered && current == this)
            {
                current = outer;
            }

            CloseAll();
        }
    }
}
