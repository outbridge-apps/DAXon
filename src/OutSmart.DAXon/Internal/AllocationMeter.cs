////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal
{
    /// <summary>
    /// Bytes the current thread has allocated so far: the clock that tells a call when to sum its memory ledger again
    /// (ProcessorOptions.MaxMemoryBytes). A call runs on one thread, so the growth over a call is that call's alone.
    /// </summary>
    internal static class AllocationMeter
    {
#if NET
        internal static bool IsAvailable => true;

        internal static long Read()
        {
            return GC.GetAllocatedBytesForCurrentThread();
        }
#else
        // The runtime of .NET Framework 4.8 has the counter and 4.7.2 does not; the net472 reference assemblies hide it.
        private static readonly Func<long> Counter = Find();

        internal static bool IsAvailable => Counter != null;

        internal static long Read()
        {
            return Counter == null ? 0 : Counter();
        }

        private static Func<long> Find()
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, Type.EmptyTypes, null);
            return method == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
        }
#endif
    }
}
