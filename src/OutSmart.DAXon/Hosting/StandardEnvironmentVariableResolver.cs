////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections;

namespace OutSmart.DAXon.Lib
{
    // ActiveSAXSource — used inline in Configuration.cs.
    // ActiveSAXSource stub removed 2026-06-01: real OutSmart.DAXon.Events.ActiveSAXSource re-included (drives parser.Parse).
    // I5 B4b-1 (2026-06-12): the compat HashSet wrapper is RETIRED, so BOTH sides of this interface+impl
    // pair are now System.Collections.Generic.HashSet<string>. Crucially the divergence the run-31
    // post-mortem warned about (impl BCL vs poc interface compat -> mid-pipeline CS0738 suppressor that
    // blinded probe rounds r3-r29) is GONE: the TypeMapper Collections-prefix qualifier-strip collapses
    // the RAW poc interface's FQ OutSmart...HashSet<string> to bare -> BCL at STAGE-0, before r3, so the
    // interface is BCL at every stage in lockstep with this BCL impl. (The modernizer still skips both by
    // design; harmless now that neither references the deleted compat type.)
    // TIER-2 2026-06-17: faithful .NET equivalent of upstream StandardEnvironmentVariableResolver (Java System.getenv()).
    // Backs fn:environment-variable / fn:available-environment-variables when Feature.ALLOW_EXTERNAL_FUNCTIONS is on.
    // Snapshot frozen at first use per resolver (= per Configuration): upstream parity — Java's
    // System.getenv() snapshots once per JVM — and O(1) lookups instead of a native block copy per call.
    internal sealed class StandardEnvironmentVariableResolver : IEnvironmentVariableResolver
    {
        private Dictionary<string, string> snapshot;

        public StandardEnvironmentVariableResolver() { }

        private Dictionary<string, string> Snapshot()
        {
            Dictionary<string, string> s = System.Threading.Volatile.Read(ref snapshot);
            if (s == null)
            {
                s = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (DictionaryEntry e in Environment.GetEnvironmentVariables())
                {
                    s[(string)e.Key] = (string)e.Value;
                }
                s = System.Threading.Interlocked.CompareExchange(ref snapshot, s, null) ?? s;
            }
            return s;
        }

        public HashSet<string> GetAvailableEnvironmentVariables()
        {
            return new HashSet<string>(Snapshot().Keys);
        }

        // Ordinal lookup over the same snapshot the availability list is built from, not
        // Environment.GetEnvironmentVariable: on Windows that API is case-INSENSITIVE, so
        // environment-variable('PATH') would return a value while available-environment-variables()
        // reports only 'Path' — the two functions must agree (F&O function-1501).
        public string GetEnvironmentVariable(string name)
        {
            string value;
            return Snapshot().TryGetValue(name, out value) ? value : null;
        }
    }
}
