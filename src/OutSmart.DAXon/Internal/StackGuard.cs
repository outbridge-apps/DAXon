////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using OutSmart.DAXon.Api;

namespace OutSmart.DAXon.Internal
{
    /// <summary>
    /// Proactive stack-overflow guard. On .NET Framework a real StackOverflowException is
    /// uncatchable and kills the process, so the Java behavior (catch StackOverflowError at
    /// the recursion sites and report SXLM0001) cannot be reproduced reactively. Instead the
    /// recursion entry points call Probe(), which raises the catchable RecursionDepthError
    /// while there is still guaranteed headroom to unwind — the ported catch sites then
    /// convert it to the same errors Java reports (SXLM0001 / XTDE3400).
    /// The remaining stack is measured directly (thread stack bounds are fixed for the
    /// thread's lifetime, so the low bound is cached per thread) because
    /// RuntimeHelpers.EnsureSufficientExecutionStack demands 512 KB on 64-bit Framework —
    /// half of a default 1 MB thread — which would reject recursion depths that in fact
    /// complete comfortably.
    /// </summary>
    internal static class StackGuard
    {
        // Headroom kept in reserve when RecursionDepthError is raised. The original x64 value was
        // 256KB, measured while the abort was still converted to XPathException inside the deep
        // engine stack. That conversion made ~185 decorating catches re-enter exception dispatch
        // during the unwind. RecursionDepthError is now deliberately foreign until the API boundary,
        // so that depth-proportional cascade no longer exists. Keeping its obsolete reserve made every
        // probe fail immediately on a 256KB D365 Batch thread, even at logical depth zero.
        //
        // Re-calibrated against every hostile recursion shape after the foreign-exception change:
        // 64KB still allowed a real StackOverflowException on a 256KB thread, while 96KB survived.
        // Keep the next 32KB tier as safety margin. This is a fixed abort/unwind reserve, not a
        // percentage of the host stack; depth-proportional error paths add their own extraMargin.
        // The default of ProcessorOptions.StackSizeThreshold, and the least it may be.
        internal const int MinThreshold = 128 * 1024;

        [ThreadStatic]
        private static ulong stackLow;   // low bound of this thread's reserved stack region

        // stackLow plus the threshold of the engine call that owns this thread: a probe below it throws. 0 until the
        // bounds are read, and always where they cannot be (the runtime's own check stands in there).
        [ThreadStatic]
        private static ulong stackFloor;

        [ThreadStatic]
        private static int threshold;   // of the call that owns this thread; MinThreshold when below it (0: none set)

        private static volatile bool noApi;   // GetCurrentThreadStackLimits needs Windows 8/Server 2012+; absent off Windows

        [DllImport("kernel32.dll")]
        private static extern void GetCurrentThreadStackLimits(out UIntPtr lowLimit, out UIntPtr highLimit);

        /// <summary>Throws RecursionDepthError if the remaining stack is too small to safely
        /// descend another recursion level. Adapts to the executing thread's stack size.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]   // sits on every guarded recursion entry
        public static void Probe()
        {
            Probe(0);
        }

        /// <summary>
        /// As <see cref="Probe()"/>, plus caller-supplied headroom. For recursions whose ERROR path
        /// costs far more stack than the descent: on .NET Framework every level that catches and
        /// re-codes an exception re-enters exception dispatch from inside its catch, so the stack
        /// grows while unwinding instead of shrinking. A caller that pays that per level must
        /// reserve for it here - a fixed margin cannot, since the shortfall scales with depth.
        /// NoInlining is load-bearing: every caller is a recursion entry whose frame PERSISTS down
        /// the descent, and inlining moves the address-taken probe local into that frame — the
        /// few bytes per level break the depth-2000-on-1MB contract (sof_depth_fused_hof).
        /// This dedicated frame is transient: it pops before the level descends. The EH-carrying
        /// init stays split out in ProbeSlow so this body remains one TLS read + compare.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Probe(ulong extraMargin)
        {
            ulong floor = stackFloor;
            if (floor == 0)
            {
                ProbeSlow(extraMargin);
                return;
            }

            unsafe
            {
                byte probe;
                if ((ulong)&probe < floor + extraMargin)
                {
                    throw new RecursionDepthError();
                }
            }
        }

        /// <summary>
        /// The thread now runs a call of a Processor whose options ask this many bytes of stack to stay free
        /// (<see cref="OutSmart.DAXon.Api.ProcessorOptions.StackSizeThreshold"/>); 0 for none, which is the default.
        /// Set wherever the thread's deadline is (Controller), so it follows the same owner.
        /// </summary>
        internal static void UseThreshold(int bytes)
        {
            threshold = bytes;
            ulong low = stackLow;
            stackFloor = low == 0 ? 0 : low + Effective(bytes);
        }

        private static ulong Effective(int bytes)
        {
            return (ulong)(bytes < MinThreshold ? MinThreshold : bytes);
        }

        /// <summary>
        /// As <see cref="Probe()"/>, at a level of nesting in the stylesheet rather than of recursion.
        /// The recursion site that reports the error cannot tell the two apart, so it names both.
        /// Its own test: Probe stays exactly as calibrated.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ProbeNesting(ILocation location)
        {
            ulong floor = stackFloor;
            if (floor == 0)
            {
                ReadBounds();
                if (noApi)
                {
                    FallbackProbeNesting(location);
                    return;
                }

                floor = stackFloor;
            }

            unsafe
            {
                byte probe;
                if ((ulong)&probe < floor)
                {
                    throw RecursionDepthError.AtNesting(location);
                }
            }
        }

        // Once-per-thread init plus the pre-Windows-8 route (noApi leaves stackFloor at 0, so
        // those threads land here on every probe, as before). Holds the EH that must not sit
        // in the inlined hot body.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ProbeSlow(ulong extraMargin)
        {
            ReadBounds();
            if (noApi)
            {
                FallbackProbe();
                return;
            }

            Probe(extraMargin);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReadBounds()
        {
            if (!noApi)
            {
                try
                {
                    GetCurrentThreadStackLimits(out UIntPtr lo, out _);
                    stackLow = (ulong)lo;
                    stackFloor = stackLow + Effective(threshold);
                }
                catch (EntryPointNotFoundException)
                {
                    noApi = true;
                }
                catch (DllNotFoundException)
                {
                    noApi = true;   // no kernel32 at all: the .NET builds also run on Linux and macOS
                }
            }
        }

        private static void FallbackProbeNesting(ILocation location)
        {
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }
            catch (InsufficientExecutionStackException)
            {
                throw RecursionDepthError.AtNesting(location);
            }
        }

        // Pre-Windows-8 and non-Windows fallback: the BCL probe (conservative — 512 KB on 64-bit Framework, 128 KB on .NET).
        private static void FallbackProbe()
        {
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }
            catch (InsufficientExecutionStackException)
            {
                throw new RecursionDepthError();
            }
        }
    }
}
