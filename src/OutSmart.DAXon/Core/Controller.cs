////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Resources;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Streams;

namespace OutSmart.DAXon.Core
{
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(Action<Saxon.Hej.s9api.IXmlProcessingError> reporter) {"
    //                + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //                + "    }"
    //})
    public class Controller : IContextOriginator
    {
        protected internal readonly object syncLock = new object();
        public const string ANONYMOUS_PRINCIPAL_OUTPUT_URI = "dummy:/anonymous/principal/result";
        private readonly Configuration config;
        protected Executable executable;
        protected IItem globalContextItem;
        private bool globalContextItemPreset;
        private Dictionary<PackageData, Bindery> binderies;
        private GlobalParameterSet globalParameters;
        private bool convertParameters = true;
        private readonly Dictionary<GlobalVariable, HashSet<GlobalVariable>> globalVariableDependencies = new Dictionary<GlobalVariable, HashSet<GlobalVariable>>();
        protected ITraceListener traceListener;
        private bool tracingPaused;
        private Logger traceFunctionDestination;
        private IResourceResolver resourceResolver;
        protected IReceiver principalResult;
        protected string principalResultURI;
        private IUnparsedTextURIResolver unparsedTextResolver;
        private string defaultCollectionURI;
        private IErrorReporter errorReporter;
        private TreeModel treeModel = TreeModel.TINY_TREE;
        private DocumentPool sourceDocumentPool;
        private IntHashMap<Dictionary<long, KeyIndex>> localIndexes;
        private Dictionary<string, object> userDataTable;
        private NodeInfo lastRememberedNode = null;
        private int lastRememberedNumber = -1;
        private DateTimeValue currentDateTime;
        private bool dateTimePreset = false;
        private PathMap pathMap = null;
        protected int validationMode = Validation.DEFAULT;
        protected bool inUse = false;
        private bool stripSourceTrees = true;
        private ICollectionFinder collectionFinder = null;
        private StylesheetCache stylesheetCache = null;
        private Func<ISequenceIterator, FocusTrackingIterator> focusTrackerFactory = (iter => new FocusTrackingIterator(iter));
        private Func<ISequenceIterator, FocusTrackingIterator> multiThreadedFocusTrackerFactory;

        // --- Cooperative transformation deadline ------------------------------------------------
        // Hot evaluation loops (xsl:for-each, the tail-call trampoline, XPath 'for') call
        // CheckTimeout(). When a deadline is armed and passes, a clean SXTO0001 dynamic error
        // unwinds the stack, releasing every lock - unlike Thread.Abort, which would leave the
        // shared Processor/NamePool corrupt. The fast path (no deadline) is a single bool test, so
        // a run with no time limit pays nothing and its output stays byte-for-byte unchanged.
        private bool hasDeadline;
        private long deadlineTimestamp;      // Stopwatch timestamp at which to abort
        private DeadlineToken deadlineToken; // the adaptive throttle shared with the thread slot
        private TimeSpan timeoutSetting;     // remembered for the diagnostic message only
        private bool hasInheritedCap;        // nested run: may not outlive the enclosing run
        private long inheritedDeadline;
        private TimeSpan inheritedSetting;
        private DeadlineToken inheritedMemory;   // nested run: the enclosing run's memory count, which it continues
        private bool chargesInputs;              // this run has a memory limit of its own and counts what it is handed
        private HashSet<long> chargedTrees;      // document numbers of the trees counted already
        private long firstTreeOfRun;             // trees numbered from here on were made by the run itself


        // The deadline active on the current thread, published when a deadline is armed (once per
        // run). Lets loops with no XPathContext at hand - notably iterating a constant-folded
        // integer range - honour the deadline via the static CheckActiveTimeout(). A transformation
        // is single-threaded, so a plain per-thread field is safe; nested runs on one thread
        // (fn:load-xquery-module) inherit the same deadline, so the value stays correct.
        // Deliberately a tiny value-only token, NOT the Controller itself: nothing resets the slot
        // when a run finishes, so whatever it references stays reachable until the thread runs
        // another transformation - a Controller here pinned the last input document, the bindery
        // and the per-run document pool on every idle pool thread.
        [ThreadStatic]
        private static DeadlineToken activeOnThread;

        // Snapshot of one run's deadline, shared between the arming Controller and the thread slot.
        // Holds only value-typed state so a stale slot retains ~40 bytes, never the run's graph.
        internal sealed class DeadlineToken
        {
            internal bool hasDeadline;
            internal long deadlineTimestamp;
            internal TimeSpan setting;
            internal string activity;   // what the limit stopped when not a transformation: "Compilation", "Parsing", ...
            internal int stackThreshold;   // the Processor's ProcessorOptions.StackSizeThreshold; 0 for the default

            // ProcessorOptions.MaxMemoryBytes, 0 for none. The call has taken the thread's allocation counter less
            // memoryBase; counting a tree it was handed lowers the base, as if the call had allocated the tree.
            internal long memoryLimit;
            internal long memoryBase;
            internal long memoryInputs;   // of it the trees the call was handed, for the message
            private bool armed;           // a deadline or a memory limit: whether the throttles sample at all

            // TWO independent clock-sampling throttles, one per class of call site. Reading the
            // clock costs ~25ns, far too much to do on every item of a hot iterator, so each class
            // samples once per stride and retunes the stride from what it measures.
            //
            // They must not share a countdown, which is what round BA measured the hard way. Per-
            // item sites (iterators) can fire millions of times a second and drive any shared
            // stride to the cap; a per-STEP site - one xsl:for-each iteration, one tail call - then
            // inherits that stride and needs thousands of its own steps to work it off. With one
            // step costing a second, that is a deadline the run reaches hours late: deep-equal over
            // two big trees checked 960k times on its first pass and essentially never again
            // (the sequences are materialised by then), leaving the enclosing for-each - one call
            // per second - starved behind a countdown of 4096. Split, each class adapts to its own
            // pace, and no loop decrements more than one of them, so there is no stride^2 blind
            // spot either.
            private Throttle perItem = new Throttle();
            private Throttle perStep = new Throttle();

            internal void Arm(long deadline, TimeSpan limit)
            {
                deadlineTimestamp = deadline;
                setting = limit;
                hasDeadline = true;
                armed = true;
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                perItem.Reset(now);
                perStep.Reset(now);
            }

            // A memory limit counted from now.
            internal void ArmMemory(long limit)
            {
                memoryLimit = limit;
                memoryBase = OutSmart.DAXon.Internal.AllocationMeter.Read();
                memoryInputs = 0;
                StartSampling();
            }

            // A call nested in another continues that one's count: one limit for both.
            internal void ContinueMemory(DeadlineToken outer)
            {
                memoryLimit = outer.memoryLimit;
                memoryBase = outer.memoryBase;
                memoryInputs = outer.memoryInputs;
                StartSampling();
            }

            internal void ChargeInput(long bytes)
            {
                memoryBase -= bytes;
                memoryInputs += bytes;
            }

            private void StartSampling()
            {
                if (!armed)
                {
                    armed = true;
                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    perItem.Reset(now);
                    perStep.Reset(now);
                }
            }

            /// <summary>Per-item sites: iterators, the regex driver, the parse loops.</summary>
            internal void Check()
            {
                if (armed && perItem.Tick())
                {
                    Sample(perItem);
                }
            }

            /// <summary>
            /// Per-step sites: one iteration of an instruction whose body can cost anything at all.
            /// </summary>
            internal void CheckPerStep()
            {
                if (armed && perStep.Tick())
                {
                    Sample(perStep);
                }
            }

            private void Sample(Throttle t)
            {
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                if (hasDeadline && now >= deadlineTimestamp)
                {
                    throw Exceeded();
                }

                CheckMemory();
                t.Retune(now);
            }

            // Reading the counter costs about what reading the clock does, so input reads can afford it on every buffer.
            internal void CheckMemory()
            {
                if (memoryLimit > 0 && OutSmart.DAXon.Internal.AllocationMeter.Read() - memoryBase > memoryLimit)
                {
                    throw MemoryExceeded();
                }
            }

            private XPathException MemoryExceeded()
            {
                long taken = OutSmart.DAXon.Internal.AllocationMeter.Read() - memoryBase;
                string handed = memoryInputs > 0 ? ", " + Size(memoryInputs) + " of it the trees it was handed" : "";
                return new XPathException((activity ?? "Transformation") + " exceeded its memory limit of " + Size(memoryLimit)
                    + " (ProcessorOptions.MaxMemoryBytes): " + Size(taken) + " counted" + handed, DAXonErrorCode.SXLM0003);
            }

            private static string Size(long bytes)
            {
                return bytes < 1048576
                    ? (bytes / 1024.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " KB"
                    : (bytes / 1048576.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
            }

            // The limit is the Processor's TransformTimeout, which also bounds compiling and parsing:
            // "Transformation exceeded" for a compile sent hosts looking at the wrong thing.
            private XPathException Exceeded()
            {
                string limit = setting.TotalSeconds + "s";
                return new XPathException(activity == null
                    ? "Transformation exceeded its time limit of " + limit
                    : activity + " exceeded the time limit of " + limit + " (ProcessorOptions.TransformTimeout)", DAXonErrorCode.SXTO0001);
            }

            // One class's sampling rate. Kept off the token so the two cannot be confused, and out
            // of Sample() so the hot path stays a decrement and a branch.
            private sealed class Throttle
            {
                private const int StrideMax = 4096;
                private static readonly long SampleTargetTicks = System.Diagnostics.Stopwatch.Frequency / 50;   // 20 ms

                private int countdown = 1;
                private int stride = 1;
                private long lastSample;

                internal void Reset(long now)
                {
                    countdown = 1;
                    stride = 1;
                    lastSample = now;
                }

                internal bool Tick()
                {
                    return --countdown <= 0;
                }

                // Retune towards one sample per SampleTargetTicks of work. Shrinking is proportional
                // and immediate - one slow sample drops the stride to 1, so the next step is
                // checked. Growth is capped at a doubling per sample: the first sample of a run
                // lands microseconds after Arm, and a proportional jump off that near-zero elapsed
                // would put the stride straight at the cap - the fixed-stride blind spot this
                // replaces, restored. Ramping 1 -> cap costs 13 samples, i.e. nothing.
                internal void Retune(long now)
                {
                    long elapsed = now - lastSample;
                    lastSample = now;
                    long ceiling = (long)stride * 2;
                    long scaled = elapsed <= 0 ? ceiling : (long)stride * SampleTargetTicks / elapsed;
                    if (scaled > ceiling)
                    {
                        scaled = ceiling;
                    }

                    stride = (int)(scaled < 1 ? 1 : (scaled > StrideMax ? StrideMax : scaled));
                    countdown = stride;
                }
            }

            internal void CheckNow()
            {
                if (hasDeadline && System.Diagnostics.Stopwatch.GetTimestamp() >= deadlineTimestamp)
                {
                    throw Exceeded();
                }

                CheckMemory();
            }
        }

        /// <summary>
        /// Honour the current thread's active transformation deadline from a context-less loop.
        /// </summary>
        public static void CheckActiveTimeout()
        {
            activeOnThread?.Check();
        }

        /// <summary>
        /// As above, but sampling the clock on EVERY call. For slow blocking call sites - a network
        /// read that returns a trickle of bytes - where the strided check would need thousands of
        /// (individually slow) calls before it first looks at the clock.
        /// </summary>
        internal static void CheckActiveTimeoutNow()
        {
            activeOnThread?.CheckNow();
        }

        /// <summary>
        /// The memory limit alone, from every buffer an input stream reads: a huge text node grows inside the XML reader,
        /// where no check of the parse runs until the node is complete.
        /// </summary>
        internal static void CheckActiveMemory()
        {
            activeOnThread?.CheckMemory();
        }

        // Whether the call on this thread has a memory limit: its input streams are then metered even when their length is known.
        internal static bool HasActiveMemoryLimit => activeOnThread?.memoryLimit > 0;

        /// <summary>
        /// <see cref="CheckActiveTimeout"/> for a per-step site: one call of a function the stylesheet supplied,
        /// whose cost can be anything (see <see cref="CheckTimeoutPerStep"/>).
        /// </summary>
        internal static void CheckActiveTimeoutPerStep()
        {
            activeOnThread?.CheckPerStep();
        }

        /// <summary>
        /// Milliseconds left on this thread's active deadline, or -1 when none is armed. Blocking
        /// I/O cannot poll a cooperative deadline, so the socket layer is given this as its own
        /// timeout: a fetch can then never outlive the run that asked for it.
        /// </summary>
        internal static int RemainingMillis()
        {
            DeadlineToken token = activeOnThread;
            if (token == null || !token.hasDeadline)
            {
                return -1;
            }

            long ticks = token.deadlineTimestamp - System.Diagnostics.Stopwatch.GetTimestamp();
            if (ticks <= 0)
            {
                return 1;   // already past: fail on the next check rather than wait
            }

            double ms = ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return ms >= int.MaxValue ? int.MaxValue : (int)ms + 1;
        }

        /// <summary>
        /// Cap this controller's deadline at an enclosing run's. A nested transformation
        /// (fn:transform) builds its own Processor-derived controller and would otherwise arm a
        /// FULL fresh budget, so a 3s run could spawn a 60s one - and, recursively, any total.
        /// The cap survives the re-arming that priming and the streaming path perform, because
        /// <see cref="SetTimeout"/> applies it on every call. Copies out only the parent's absolute
        /// deadline, never the parent controller, so nothing of the outer run is retained.
        /// </summary>
        internal void CapDeadlineTo(Controller parent)
        {
            if (parent != null && parent.hasDeadline)
            {
                hasInheritedCap = true;
                inheritedDeadline = parent.deadlineTimestamp;
                inheritedSetting = parent.timeoutSetting;
            }

            // Nor a fresh memory limit: the nested run continues the enclosing run's count.
            if (parent?.deadlineToken != null && parent.deadlineToken.memoryLimit > 0)
            {
                inheritedMemory = parent.deadlineToken;
            }
        }

        /// <summary>
        /// Arm - or, for a non-positive TimeSpan, clear - a wall-clock deadline for this
        /// transformation. Measured from the moment of this call, so it is armed once per run.
        /// When armed, hot evaluation loops abort with SXTO0001 once the limit is exceeded.
        /// An inherited cap (<see cref="CapDeadlineTo"/>) always wins when it expires first, and
        /// applies even to a nested run whose own Processor has no limit at all.
        /// </summary>
        public virtual void SetTimeout(TimeSpan timeout)
        {
            SetTimeout(timeout, null);
        }

        // activity names what the limit stops when it is not a transformation ("Compilation", "Parsing", ...).
        internal void SetTimeout(TimeSpan timeout, string activity)
        {
            var token = new DeadlineToken { activity = activity, stackThreshold = StackThresholdOf(config) };
            Own(token);   // this run now owns the deadline slot on the running thread
            hasDeadline = false;

            if (timeout > TimeSpan.Zero)
            {
                timeoutSetting = timeout;
                deadlineTimestamp = DeadlineFromNow(timeout);
                hasDeadline = true;
            }

            if (hasInheritedCap && (!hasDeadline || inheritedDeadline < deadlineTimestamp))
            {
                deadlineTimestamp = inheritedDeadline;
                timeoutSetting = inheritedSetting;
                hasDeadline = true;
            }

            ArmMemory(token);
            if (!hasDeadline)
            {
                if (token.memoryLimit > 0)
                {
                    deadlineToken = token;   // the memory limit alone
                    ChargeRunInputs();
                }

                return;               // no deadline: the token holds at most the memory limit
            }

            token.Arm(deadlineTimestamp, timeoutSetting);
            deadlineToken = token;
            ChargeRunInputs();
        }

        // The Processor's memory limit for this call, counted from now - or, for a nested run, the enclosing run's count.
        private void ArmMemory(DeadlineToken token)
        {
            chargedTrees = null;
            chargesInputs = false;
            if (inheritedMemory != null)
            {
                token.ContinueMemory(inheritedMemory);
                return;
            }

            long limit = MemoryLimitOf(config);
            if (limit > 0)
            {
                token.ArmMemory(limit);
                chargesInputs = true;
                firstTreeOfRun = config.DocumentNumberAllocator.AllocateDocumentNumber();
            }
        }

        // ProcessorOptions.MaxMemoryBytes of the Processor a configuration serves; 0 for none.
        private static long MemoryLimitOf(Configuration config)
        {
            return (config?.GetProcessor() as OutSmart.DAXon.Api.Processor)?.Options.MaxMemoryBytes ?? 0;
        }

        /// <summary>
        /// Count the trees the host hands this run - in a context item, parameters, a selection - against its memory limit,
        /// as if the run had allocated them: each once, by its size. Repeating it is harmless. A nested run counts nothing
        /// (the enclosing one did).
        /// </summary>
        internal void ChargeInput(ISequence value)
        {
            if (chargesInputs && value != null && deadlineToken != null)
            {
                deadlineToken.ChargeInput(InputSize.Of(value, chargedTrees ??= new HashSet<long>(), firstTreeOfRun));
            }
        }

        // The trees the run holds: its global context item and the stylesheet's or query's parameters. Run when the limits
        // are armed, and again by an entry point that installs its inputs afterwards.
        internal virtual void ChargeRunInputs()
        {
            if (!chargesInputs)
            {
                return;
            }

            ChargeInput(globalContextItem);
            if (globalParameters != null)
            {
                foreach (StructuredQName name in globalParameters.Keys)
                {
                    ChargeInput(globalParameters.Get(name));
                }
            }
        }

        /// <summary>
        /// Called per ITEM from hot evaluation loops - iterators, the regex driver, parse loops.
        /// Returns immediately unless a deadline is armed and has passed, in which case it throws
        /// SXTO0001. Shares its throttle with the context-less <see cref="CheckActiveTimeout"/>,
        /// which is the same class of site.
        /// </summary>
        public void CheckTimeout()
        {
            deadlineToken?.Check();
        }

        /// <summary>
        /// Called per STEP from instruction-level loops - one xsl:for-each iteration, one XPath
        /// 'for' binding, one tail call - where a single step can cost arbitrarily much. These get
        /// their own throttle, because a per-item throttle driven to its cap by a burst of cheap
        /// iterator calls would starve them for thousands of (second-long) steps.
        /// </summary>
        public void CheckTimeoutPerStep()
        {
            deadlineToken?.CheckPerStep();
        }

        /// <summary>
        /// Adopt an already-armed deadline from a parent controller. Used when a nested execution
        /// is spun up mid-run on its own controller (fn:load-xquery-module): the absolute deadline
        /// is shared, not restarted, so a nested module cannot buy itself a fresh time budget.
        /// A parent with no deadline clears this one.
        /// </summary>
        public void InheritDeadlineFrom(Controller parent)
        {
            var token = new DeadlineToken { activity = parent?.deadlineToken?.activity, stackThreshold = StackThresholdOf(config) };
            Own(token);   // the nested run now owns the deadline slot on the running thread
            chargesInputs = false;   // and continues the parent's memory count, which counted the inputs
            if (parent?.deadlineToken != null && parent.deadlineToken.memoryLimit > 0)
            {
                token.ContinueMemory(parent.deadlineToken);
            }

            if (parent == null || !parent.hasDeadline)
            {
                hasDeadline = false;
                if (token.memoryLimit > 0)
                {
                    deadlineToken = token;
                }

                return;               // no deadline: the token holds at most the memory limit
            }

            deadlineTimestamp = parent.deadlineTimestamp;
            timeoutSetting = parent.timeoutSetting;
            hasDeadline = true;
            token.Arm(deadlineTimestamp, timeoutSetting);
            deadlineToken = token;
        }

        /// <summary>
        /// Share the deadline the enclosing scope armed on this thread - the compile's own, every
        /// compile entry arms one - without claiming the slot. For evaluation inside a compile
        /// ([xsl:]use-when, static variables): a fresh budget per expression multiplied the
        /// compile's limit by their number. Nothing armed (no limit) leaves this one unlimited.
        /// </summary>
        internal void AdoptThreadDeadline()
        {
            DeadlineToken token = activeOnThread;
            if (token != null && token.hasDeadline)
            {
                deadlineTimestamp = token.deadlineTimestamp;
                timeoutSetting = token.setting;
                hasDeadline = true;
                deadlineToken = token;
            }
            else if (token != null && token.memoryLimit > 0)
            {
                deadlineToken = token;   // the compile's memory limit alone
            }
        }

        /// <summary>
        /// Claim the current thread's active-deadline slot for a COMPILE scope: constant folding
        /// can evaluate attacker-sized work (sum(1 to 2000000000)) before any transformation - and
        /// so any run-time deadline - exists. Arms the Processor's limit when the configuration has
        /// one, else leaves the scope unlimited. Returns the previous owner, which the caller MUST
        /// restore (try/finally): a compile nested inside a running transformation (fn:transform)
        /// hands the slot back to that run's deadline on exit.
        /// </summary>
        internal static DeadlineToken ArmThreadDeadline(Configuration config, string activity)
        {
            DeadlineToken previous = activeOnThread;
            if (config.GetProcessor() is OutSmart.DAXon.Api.Processor p)
            {
                // The token alone, as SetTimeout arms it for a fresh Controller: building one to arm it cost
                // ~2.8 KB and ~3 us on every API call.
                var token = new DeadlineToken { activity = activity, stackThreshold = p.Options.StackSizeThreshold };
                TimeSpan timeout = p.TransformTimeout;
                if (timeout > TimeSpan.Zero)
                {
                    token.Arm(DeadlineFromNow(timeout), timeout);
                }

                if (p.Options.MaxMemoryBytes is long memory)
                {
                    token.ArmMemory(memory);
                }

                Own(token);
            }
            else
            {
                Own(null);
            }

            return previous;
        }

        internal static void RestoreThreadDeadline(DeadlineToken previous)
        {
            Own(previous);
        }

        // The thread now runs a call of the token's owner: its deadline for the checks with no context at hand, and the
        // stack its options keep free for the recursion guard. Every change of the slot goes through here.
        private static void Own(DeadlineToken token)
        {
            activeOnThread = token;
            OutSmart.DAXon.Internal.StackGuard.UseThreshold(token?.stackThreshold ?? 0);
        }

        // The StackSizeThreshold of the Processor a configuration serves; 0 (the default) for the engine's own.
        private static int StackThresholdOf(Configuration config)
        {
            return (config?.GetProcessor() as OutSmart.DAXon.Api.Processor)?.Options.StackSizeThreshold ?? 0;
        }

        /// <summary>
        /// Release the thread from its deadline for one-time initialization (the regex tables a type
        /// initializer loads). The runtime caches a type initializer's exception for the life of the
        /// process, so a limit firing there broke regex and format-date for every later Processor.
        /// Returns the previous owner, which the caller MUST restore (try/finally).
        /// </summary>
        internal static DeadlineToken SuspendThreadDeadline()
        {
            DeadlineToken previous = activeOnThread;
            Own(null);
            return previous;
        }

        // Saturates: TimeSpan.MaxValue in Stopwatch ticks overflows a long, and the wrapped deadline
        // was already past, so every call failed at once. Thousands of years stand in for "never".
        private static long DeadlineFromNow(TimeSpan timeout)
        {
            double ticks = timeout.TotalSeconds * System.Diagnostics.Stopwatch.Frequency;
            return ticks >= long.MaxValue / 2 ? long.MaxValue : System.Diagnostics.Stopwatch.GetTimestamp() + (long)ticks;
        }

        public virtual string BaseOutputURI
        {
            get => principalResultURI; set
            {
                principalResultURI = value;
            }
        }

        public virtual IReceiver PrincipalResult => principalResult;

        public virtual IErrorReporter ErrorReporter
        {
            get => errorReporter; set
            {
                errorReporter = value;
            }
        }

        public virtual IItem GlobalContextItem
        {
            get => globalContextItem; set
            {
                SetGlobalContextItem(value, false);
            }
        }

        public virtual IResourceResolver ResourceResolver
        {
            get => resourceResolver; set
            {
                resourceResolver = value;
            }
        }

        public virtual IUnparsedTextURIResolver UnparsedTextURIResolver
        {
            get => unparsedTextResolver; set
            {
                unparsedTextResolver = value;
            }
        }

        public virtual int SchemaValidationMode
        {
            get => validationMode; set
            {
                this.validationMode = value;
            }
        }

        public virtual TreeModel Model
        {
            get => treeModel; set
            {
                treeModel = value;
            }
        }

        public virtual ISpaceStrippingRule SpaceStrippingRule
        {
            get
            {
                if (config.GetParseOptions().SpaceStrippingRule == AllElementsSpaceStrippingRule.GetInstance())
                {
                    return AllElementsSpaceStrippingRule.GetInstance();
                }
                else if (executable is PreparedStylesheet)
                {
                    ISpaceStrippingRule rule = ((PreparedStylesheet)executable).GetTopLevelPackage().SpaceStrippingRule;
                    if (rule != null)
                    {
                        return rule;
                    }
                }

                return NoElementsSpaceStrippingRule.GetInstance();
            }
        }

        public virtual Logger TraceFunctionDestination
        {
            get => traceFunctionDestination; set
            {
                traceFunctionDestination = value;
            }
        }

        public virtual IntHashMap<Dictionary<long, KeyIndex>> LocalIndexes
        {
            get
            {
                lock (syncLock)
                {
                    if (localIndexes == null)
                    {
                        localIndexes = new IntHashMap<Dictionary<long, KeyIndex>>();
                    }

                    return localIndexes;
                }
            }
        }

        public virtual PathMap PathMapForDocumentProjection => pathMap;
        public Controller(Configuration config)
        {
            this.config = config;

            // create a dummy executable
            executable = new Executable(config);
            sourceDocumentPool = new DocumentPool();
            Reset();
        }

        public Controller(Configuration config, Executable executable)
        {
            this.config = config;
            this.executable = executable;
            sourceDocumentPool = new DocumentPool();
            Reset();
        }

        public virtual void Reset()
        {
            globalParameters = new GlobalParameterSet();
            focusTrackerFactory = config.GetFocusTrackerFactory(executable, false);
            multiThreadedFocusTrackerFactory = config.GetFocusTrackerFactory(executable, true);

            resourceResolver = null;
            unparsedTextResolver = config.UnparsedTextURIResolver;
            validationMode = config.SchemaValidationMode;
            errorReporter = config.MakeErrorReporter();
            traceListener = null;
            traceFunctionDestination = config.Logger;
            ITraceListener tracer;
            try
            {
                tracer = config.MakeTraceListener();
            }
            catch (XPathException err)
            {
                throw new InvalidOperationException(err.Message);
            }

            if (tracer != null)
            {
                AddTraceListener(tracer);
            }

            Model = config.GetParseOptions().Model;
            globalContextItem = null;
            currentDateTime = null;
            dateTimePreset = false;
            ClearPerTransformationData();
        }

        protected virtual void ClearPerTransformationData()
        {
            lock (syncLock)
            {
                userDataTable = new Dictionary<string, object>(20);
                principalResult = null;
                tracingPaused = false;
                lastRememberedNode = null;
                lastRememberedNumber = -1;
                stylesheetCache = null;
                localIndexes = null;
                if (!globalContextItemPreset)
                {
                    globalContextItem = null;
                }
            }
        }

        /// <summary>
        /// Release the state a finished run leaves behind. Called when the run gives up `inUse`, NOT
        /// at the start of the next one: the Api pools the principal input tree in MakeSourceTree
        /// BEFORE it enters ApplyTemplates, so clearing on entry would throw away the pool entry that
        /// gives doc($sameUri) its node identity with the input.
        ///   Why it is needed at all: the pool holds the input tree plus every tree doc()/document()
        /// pulled, keyed by URI, with no eviction, and it used to be reset only in Reset() - which is
        /// called ONLY from the constructors. A host that reuses one Xslt30Transformer (the docs
        /// forbid it; the engine allows it, since inUse is released in a finally) therefore
        /// accumulated every document it ever transformed, silently, at megabytes per run.
        /// </summary>
        protected internal virtual void ReleaseRunState()
        {
            lock (syncLock)
            {
                // Nothing pooled means nothing to discard and no reason to allocate a replacement:
                // the common shape (host passes a built XdmNode, stylesheet calls no doc()) never
                // pools anything, so this keeps the per-run cost at one compare.
                if (sourceDocumentPool != null && !sourceDocumentPool.IsEmpty)
                {
                    ClearDocumentPool();
                }
            }
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual ISequence GetParameter(StructuredQName name)
        {
            return globalParameters[name];
        }

        public virtual IGroundedValue GetConvertedParameter(StructuredQName name, Values.SequenceType requiredType, IXPathContext context)
        {
            IGroundedValue val = globalParameters.ConvertParameterValue(name, requiredType, convertParameters, context);
            if (val != null)
            {

                // Check that any nodes belong to the right configuration
                Configuration config = GetConfiguration();
                ISequenceIterator iter = val.Iterate();
                IItem next;
                while ((next = iter.Next()) != null)
                {
                    if (next is NodeInfo && !config.IsCompatible(((NodeInfo)next).GetConfiguration()))
                    {
                        throw new XPathException("A node supplied in a global parameter must be built using the same Configuration " + "that was used to compile the stylesheet or query", DAXonErrorCode.SXXP0004);
                    }
                }


                // If the supplied value is a document node, and the document node has a systemID that is an absolute
                // URI, and the absolute URI does not already exist in the document pool, then register it in the document
                // pool, so that the document-uri() function will find it there, and so that a call on doc() will not
                // reload it.
                if (val is NodeInfo && ((NodeInfo)val).GetNodeKind() == Types.Type.DOCUMENT)
                {
                    string systemId = ((NodeInfo)val).Root.GetSystemId();
                    try
                    {
                        if (systemId != null && new URI(systemId).IsAbsolute())
                        {
                            DocumentKey key = new DocumentKey(systemId);
                            DocumentPool pool = GetDocumentPool();
                            if (pool.Find(key) == null)
                            {
                                pool.Add(((NodeInfo)val).GetTreeInfo(), key);
                            }
                        }
                    }
                    catch (URISyntaxException err)
                    {
                    }
                }

                val = val.Materialize();
            }

            return val;
        }

        public virtual SequenceCollector AllocateSequenceOutputter()
        {
            PipelineConfiguration pipe = MakePipelineConfiguration();
            return new SequenceCollector(pipe, 20);
        }

        public virtual SequenceCollector AllocateSequenceOutputter(int size)
        {
            PipelineConfiguration pipe = MakePipelineConfiguration();
            return new SequenceCollector(pipe, size);
        }

        public virtual PipelineConfiguration MakePipelineConfiguration()
        {
            ParseOptions parseOptions = GetConfiguration().GetParseOptions().WithSchemaValidationMode(validationMode).WithErrorReporter(errorReporter);
            PipelineConfiguration pipe = new PipelineConfiguration(GetConfiguration(), parseOptions);
            pipe.SetController(this);
            return pipe;
        }

        public virtual void ReportFatalError(XPathException err)
        {
            if (!err.HasBeenReported())
            {
                if (err.GetHostLanguage() == null)
                {
                    if (executable.GetHostLanguage() == HostLanguage.XSLT)
                    {
                        err.SetHostLanguage("XSLT");
                    }
                    else if (executable.GetHostLanguage() == HostLanguage.XQUERY)
                    {
                        err.SetHostLanguage("XQuery");
                    }
                }

                ErrorReporter.Report(new XmlProcessingException(err));
                err.SetHasBeenReported(true);
            }
        }

        public virtual void Warning(string message, string errorCode, ILocation locator)
        {
            if (locator == null)
            {
                locator = Loc.NONE;
            }

            if (errorCode == null)
            {
                errorCode = DAXonErrorCode.SXWN9000;
            }

            if (message == null)
            {
                message = "Unspecified warning";
            }

            XmlProcessingIncident warning = new XmlProcessingIncident(message, errorCode, locator).AsWarning();
            errorReporter.Report(warning);
        }

        protected virtual void HandleXPathException(XPathException err)
        {
            // The direct XmlReader parse path never wraps a SAX SAXParseException, so the historic
            // "unwrap a Crimson-wrapped runtime exception" branch is unreachable: always report and rethrow.
            ReportFatalError(err);
            throw err;
        }

        public virtual Executable GetExecutable()
        {
            return executable;
        }

        public virtual DocumentPool GetDocumentPool()
        {
            return sourceDocumentPool;
        }

        public virtual void ClearDocumentPool()
        {
            foreach (PackageData pack in GetExecutable().Packages)
            {
                sourceDocumentPool.DiscardIndexes(pack.GetKeyManager());
            }

            sourceDocumentPool = new DocumentPool();
        }

        public virtual Bindery GetBindery(PackageData packageData)
        {
            lock (syncLock)
            {
                Bindery b = binderies.GetOrDefault(packageData);
                if (b == null)
                {
                    b = new Bindery(packageData);
                    binderies[packageData] = b;
                }

                return b;
            }
        }

        public virtual void SetGlobalContextItem(IItem contextItem, bool alreadyStripped)
        {
            if (!alreadyStripped)
            {

                // Bug 2929 - don't do space-stripping twice
                if (globalContextItem is SpaceStrippedNode && ((SpaceStrippedNode)globalContextItem).UnderlyingNode == contextItem)
                {
                    return;
                }

                if (contextItem is NodeInfo)
                {

                    // In XSLT, apply strip-space and strip-type-annotations options
                    NodeInfo node = (NodeInfo)contextItem;
                    contextItem = PrepareInputTree(node);
                    if (node.GetNodeKind() == Types.Type.DOCUMENT && node.GetSystemId() != null)
                    {
                        DocumentKey key = new DocumentKey(node.GetSystemId());
                        if (GetDocumentPool().Find(key) == null)
                        {
                            GetDocumentPool().Add(node.GetTreeInfo(), key);
                        }
                    }
                }
            }
            else if (contextItem is NodeInfo)
            {
                // The caller asserts the tree is already prepared, but nothing verifies that.
                // PrepareInputTree decides from the tree's own state and is a no-op when the
                // claim is true, so run it either way: an over-claim used to leave the context
                // item unstripped while apply-templates saw a stripped tree, and one document
                // then gave two answers to the same expression in a single transform.
                contextItem = PrepareInputTree((NodeInfo)contextItem);
            }

            if (contextItem is NodeInfo)
            {
                NodeInfo startNode = (NodeInfo)contextItem;
                if (startNode.GetConfiguration() == null)
                {

                    // must be a non-standard document implementation
                    throw new XPathException("The supplied source document must be associated with a Configuration");
                }

                if (!startNode.GetConfiguration().IsCompatible(executable.GetConfiguration()))
                {
                    throw new XPathException("Source document and stylesheet must use the same or compatible Configurations", DAXonErrorCode.SXXP0004);
                }

                if (startNode.GetTreeInfo().IsTyped() && !executable.IsSchemaAware())
                {
                    throw new XPathException("Cannot use a schema-validated source document unless the stylesheet is schema-aware");
                }
            }

            this.globalContextItem = contextItem;
            this.globalContextItemPreset = true;
        }

        public virtual void ClearGlobalContextItem()
        {
            this.globalContextItem = null;
            this.globalContextItemPreset = false;
        }

        public virtual ICollectionFinder GetCollectionFinder()
        {
            if (collectionFinder == null)
            {
                collectionFinder = config.CollectionFinder;
            }

            return collectionFinder;
        }

        public virtual void SetCollectionFinder(ICollectionFinder cf)
        {
            collectionFinder = cf;
        }

        public virtual void SetDefaultCollection(string uri)
        {
            defaultCollectionURI = uri;
        }

        public virtual string GetDefaultCollection()
        {
            return defaultCollectionURI == null ? GetConfiguration().DefaultCollection : defaultCollectionURI;
        }

        public virtual Builder MakeBuilder()
        {
            Builder b = treeModel.MakeBuilder(MakePipelineConfiguration());
            b.SetTiming(config.IsTiming());
            b.SetLineNumbering(config.IsLineNumbering());
            return b;
        }

        public virtual void SetStripSourceTrees(bool strip)
        {
            stripSourceTrees = strip;
        }

        public virtual bool IsStripSourceTree()
        {
            return stripSourceTrees;
        }

        protected virtual bool IsStylesheetContainingStripSpace()
        {
            ISpaceStrippingRule rule;
            return executable is PreparedStylesheet && (rule = ((PreparedStylesheet)executable).GetTopLevelPackage().SpaceStrippingRule) != null && rule != NoElementsSpaceStrippingRule.GetInstance();
        }

        public virtual bool IsStylesheetStrippingTypeAnnotations()
        {
            return executable is PreparedStylesheet && ((PreparedStylesheet)executable).GetTopLevelPackage().IsStripsTypeAnnotations();
        }

        public virtual Stripper MakeStripper(IReceiver next)
        {
            if (next == null)
            {
                next = new Sink(MakePipelineConfiguration());
            }

            return new Stripper(SpaceStrippingRule, next);
        }

        public virtual void RegisterDocument(ITreeInfo doc, DocumentKey uri)
        {
            if (!GetExecutable().IsSchemaAware() && !Untyped.INSTANCE.Equals(doc.GetRootNode().GetSchemaType()))
            {
                bool isXSLT = GetExecutable().GetHostLanguage() == HostLanguage.XSLT;
                string message;
                if (isXSLT)
                {
                    message = "The source document has been schema-validated, but" + " the stylesheet is not schema-aware. A stylesheet is schema-aware if" + " either (a) it contains an xsl:import-schema declaration, or (b) the stylesheet compiler" + " was configured to be schema-aware.";
                }
                else
                {
                    message = "The source document has been schema-validated, but" + " the query is not schema-aware. A query is schema-aware if" + " either (a) it contains an 'import schema' declaration, or (b) the query compiler" + " was configured to be schema-aware.";
                }

                throw new XPathException(message);
            }

            if (uri != null)
            {
                sourceDocumentPool.Add(doc, uri);
            }
        }

        public virtual RuleManager GetRuleManager()
        {
            Executable exec = GetExecutable();
            return exec is PreparedStylesheet ? ((PreparedStylesheet)GetExecutable()).GetRuleManager() : null;
        }

        public virtual void SetTraceListener(ITraceListener listener)
        {
            this.traceListener = listener;
        }

        public virtual ITraceListener GetTraceListener()
        {
            return traceListener;
        }

        public bool IsTracing()
        {
            return traceListener != null && !tracingPaused;
        }

        public void PauseTracing(bool pause)
        {
            tracingPaused = pause;
        }

        public virtual void AddTraceListener(ITraceListener trace)
        {
            if (trace != null)
            {
                traceListener = (ITraceListener)TraceEventMulticaster.Add(traceListener, trace);
            }
        }

        public virtual void OpenTraceEpisode()
        {
            if (traceListener != null)
            {
                traceListener.Open(this);
            }
        }

        public virtual void CloseTraceEpisode()
        {
            if (traceListener != null)
            {
                traceListener.Close();
            }
        }

        public virtual void RemoveTraceListener(ITraceListener trace)
        {
            traceListener = (ITraceListener)TraceEventMulticaster.Remove(traceListener, trace);
        }

        public virtual void InitializeController(GlobalParameterSet @params)
        {

            // get a new bindery, to clear out any variables from previous runs
            binderies = new Dictionary<PackageData, Bindery>();

            // if parameters were supplied, set them up
            try
            {
                executable.CheckSuppliedParameters(@params);
            }
            catch (XPathException e)
            {
                if (!e.HasBeenReported())
                {
                    ErrorReporter.Report(new XmlProcessingException(e));
                    throw;
                }
            }

            globalParameters = @params;

            // Check the global context item
            globalContextItem = executable.CheckInitialContextItem(globalContextItem, NewXPathContext());
            if (traceListener != null)
            {

                PreEvaluateGlobals(NewXPathContext());
            }
        }

        public virtual void SetApplyFunctionConversionRulesToExternalVariables(bool applyConversionRules)
        {
            convertParameters = applyConversionRules; //topLevelBindery.setApplyFunctionConversionRulesToExternalVariables(applyConversionRules);
        }

        public virtual object GetUserData(object key, string name)
        {
            lock (syncLock)
            {
                string keyValue = key.GetHashCode() + " " + name;

                return userDataTable.GetOrDefault(keyValue);
            }
        }

        public virtual void SetUserData(object key, string name, object data)
        {
            lock (syncLock)
            {

                string keyVal = key.GetHashCode() + " " + name;
                if (data == null)
                {
                    userDataTable.Remove(keyVal);
                }
                else
                {
                    userDataTable[keyVal] = data;
                }
            }
        }

        public virtual void SetRememberedNumber(NodeInfo node, int number)
        {
            lock (syncLock)
            {
                lastRememberedNode = node;
                lastRememberedNumber = number;
            }
        }

        public virtual int GetRememberedNumber(NodeInfo node)
        {
            lock (syncLock)
            {
                if (lastRememberedNode == node)
                {
                    return lastRememberedNumber;
                }

                return -1;
            }
        }

        protected virtual void CheckReadiness()
        {
            if (inUse)
            {
                throw new InvalidOperationException("The Controller is being used recursively or concurrently. This is not permitted.");
            }

            if (binderies == null)
            {
                throw new InvalidOperationException("The Controller has not been initialized");
            }

            inUse = true;
            ClearPerTransformationData();
            if (executable == null)
            {
                throw new XPathException("Stylesheet has not been prepared");
            }

            if (!dateTimePreset)
            {
                currentDateTime = null; // reset at start of each transformation
            }
        }

        public virtual NodeInfo MakeSourceTree(IActiveSource source, int validationMode)
        {
            if (source is NodeSource)
            {
                return ((NodeSource)source).Node;
            }
            else if (source is ITreeInfo)
            {
                return ((ITreeInfo)source).GetRootNode();
            }
            else if (source is NodeInfo)
            {
                return ((NodeInfo)source);
            }
            else if (source is ActiveStreamSource ass && source.GetSystemId() != null && ass.IsStreamless)
            {

                // Check to see if the document is already in the document pool. This can happen when a Transformer
                // is reused to perform multiple transformations on the same source document. Bug 4837.
                DocumentKey key = new DocumentKey(source.GetSystemId());
                ITreeInfo existing = sourceDocumentPool.Find(key);
                if (existing != null)
                {
                    return existing.GetRootNode();
                }
            }

            Builder sourceBuilder = MakeBuilder();
            sourceBuilder.SetUseEventLocation(true);
            if (sourceBuilder is TinyBuilder)
            {
                ((TinyBuilder)sourceBuilder).SetStatistics(config.GetTreeStatistics().SOURCE_DOCUMENT_STATISTICS);
                ((TinyBuilder)sourceBuilder).SetInputLength((source as ActiveStreamSource)?.InputLength ?? -1);
            }

            IReceiver r = sourceBuilder;
            ISpaceStrippingRule spaceStrippingRule = NoElementsSpaceStrippingRule.GetInstance();
            if (config.IsStripsAllWhiteSpace() || IsStylesheetContainingStripSpace() || validationMode == Validation.STRICT || validationMode == Validation.LAX)
            {
                r = MakeStripper(sourceBuilder);
                spaceStrippingRule = SpaceStrippingRule;
            }

            if (IsStylesheetStrippingTypeAnnotations())
            {
                r = config.GetAnnotationStripper(r);
            }

            PipelineConfiguration pipe = sourceBuilder.GetPipelineConfiguration();
            pipe.SetParseOptions(pipe.GetParseOptions().WithSchemaValidationMode(validationMode));
            r.SetPipelineConfiguration(pipe);
            Sender.Send(source, r, null);
            NodeInfo doc = sourceBuilder.CurrentRoot;

            //globalContextItem = doc;
            sourceBuilder.Reset();
            if (source.GetSystemId() != null)
            {
                RegisterDocument(doc.GetTreeInfo(), new DocumentKey(source.GetSystemId()));
            }

            doc.GetTreeInfo().SpaceStrippingRule = spaceStrippingRule;
            return doc;
        }

        // Source-free source-tree build (P5): parse a System.Xml.XmlReader into the transform's source tree,
        // applying the stylesheet's strip-space / type-annotation stripping exactly as MakeSourceTree(Source).
        // The stripper is applied manually here (as in the Source path); the pipe parse options carry no
        // stripping rule, so Sender's own wrapping does not strip a second time.
        public virtual NodeInfo MakeSourceTree(System.Xml.XmlReader reader, string systemId, int validationMode)
        {
            return MakeSourceTree(reader, systemId, validationMode, -1);
        }

        // inputLength: of what the reader parses, when known (sizes a tiny tree), else -1.
        internal NodeInfo MakeSourceTree(System.Xml.XmlReader reader, string systemId, int validationMode, long inputLength)
        {
            Builder sourceBuilder = MakeBuilder();
            sourceBuilder.SetUseEventLocation(true);
            if (sourceBuilder is TinyBuilder)
            {
                ((TinyBuilder)sourceBuilder).SetStatistics(config.GetTreeStatistics().SOURCE_DOCUMENT_STATISTICS);
                ((TinyBuilder)sourceBuilder).SetInputLength(inputLength);
            }

            IReceiver r = sourceBuilder;
            ISpaceStrippingRule spaceStrippingRule = NoElementsSpaceStrippingRule.GetInstance();
            if (config.IsStripsAllWhiteSpace() || IsStylesheetContainingStripSpace() || validationMode == Validation.STRICT || validationMode == Validation.LAX)
            {
                r = MakeStripper(sourceBuilder);
                spaceStrippingRule = SpaceStrippingRule;
            }

            if (IsStylesheetStrippingTypeAnnotations())
            {
                r = config.GetAnnotationStripper(r);
            }

            PipelineConfiguration pipe = sourceBuilder.GetPipelineConfiguration();
            pipe.SetParseOptions(pipe.GetParseOptions().WithSchemaValidationMode(validationMode));
            r.SetPipelineConfiguration(pipe);
            Sender.Send(reader, systemId, r, null);
            NodeInfo doc = sourceBuilder.CurrentRoot;
            sourceBuilder.Reset();
            if (systemId != null)
            {
                RegisterDocument(doc.GetTreeInfo(), new DocumentKey(systemId));
            }

            doc.GetTreeInfo().SpaceStrippingRule = spaceStrippingRule;
            return doc;
        }

        public virtual NodeInfo PrepareInputTree(IActiveSource source)
        {
            // P5: PrepareInputTree only ever receives node sources (a NodeInfo, or a NodeSource wrapping one),
            // so extract the node directly rather than routing through Configuration.Unravel(Source).
            NodeInfo start = source is NodeSource ? ((NodeSource)source).Node : (NodeInfo)source;

            // Stripping type annotations happens before stripping of whitespace
            if (IsStylesheetStrippingTypeAnnotations())
            {
                ITreeInfo docInfo = start.GetTreeInfo();
                if (docInfo.IsTyped())
                {
                    TypeStrippedDocument strippedDoc = new TypeStrippedDocument(docInfo);
                    start = strippedDoc.Wrap(start);
                }
            }

            if (stripSourceTrees && IsStylesheetContainingStripSpace())
            {
                ITreeInfo docInfo = start.GetTreeInfo();
                ISpaceStrippingRule spaceStrippingRule = SpaceStrippingRule;
                if (docInfo.SpaceStrippingRule != spaceStrippingRule
                    && !(docInfo is Trees.Tiny.TinyTree noWs && !noWs.ContainsWhitespaceText()))
                {

                    // if not already space-stripped
                    SpaceStrippedDocument strippedDoc = new SpaceStrippedDocument(docInfo, spaceStrippingRule);

                    // Edge case: the global context item might itself be a whitespace text node that is stripped
                    if (!SpaceStrippedNode.IsPreservedNode(start, strippedDoc, start.GetParent()))
                    {
                        return null;
                    }

                    start = strippedDoc.Wrap(start);
                }
            }

            return start;
        }

        public virtual void PreEvaluateGlobals(IXPathContext context)
        {
            foreach (PackageData pack in GetExecutable().Packages)
            {
                foreach (GlobalVariable var in pack.GlobalVariableList)
                {
                    if (!var.IsUnused())
                    {
                        try
                        {
                            var.EvaluateVariable(context, var.DeclaringComponent);
                        }
                        catch (XPathException err)
                        {

                            // Don't report an exception unless the variable is actually evaluated
                            GetBindery(var.GetPackageData()).SetGlobalVariable(var, new FailureValue(err));
                        }
                    }
                }
            }
        }

        public virtual void RegisterGlobalVariableDependency(GlobalVariable one, GlobalVariable two)
        {
            lock (syncLock)
            {
                if (one == two)
                {
                    throw new Circularity("Circular dependency among global variables: " + one.GetVariableQName().DisplayName + " depends on its own value");
                }

                HashSet<GlobalVariable> transitiveDependencies = globalVariableDependencies.GetOrDefault(two);
                if (transitiveDependencies != null)
                {
                    if (transitiveDependencies.Contains(one))
                    {
                        throw new Circularity("Circular dependency among variables: " + one.GetVariableQName().DisplayName + " depends on the value of " + two.GetVariableQName().DisplayName + ", which depends directly or indirectly on the value of " + one.GetVariableQName().DisplayName);
                    }

                    foreach (GlobalVariable var in transitiveDependencies)
                    {

                        // register the transitive dependencies
                        RegisterGlobalVariableDependency(one, var);
                    }
                }

                HashSet<GlobalVariable> existingDependencies = globalVariableDependencies.GetOrDefault(one);
                if (existingDependencies == null)
                {
                    existingDependencies = new HashSet<GlobalVariable>();
                    globalVariableDependencies[one] = existingDependencies;
                }

                existingDependencies.Add(two);
            }
        }

        public virtual void SetCurrentDateTime(DateTimeValue dateTime)
        {
            if (currentDateTime == null)
            {
                if (dateTime.GetComponent(AccessorFn.Component.TIMEZONE) == null)
                {
                    throw new XPathException("No timezone is present in supplied value of current date/time");
                }

                currentDateTime = dateTime;
                dateTimePreset = true;
            }
            else
            {
                throw new InvalidOperationException("Current date and time can only be set once, and cannot subsequently be changed");
            }
        }

        public virtual DateTimeValue GetCurrentDateTime()
        {
            if (currentDateTime == null)
            {
                currentDateTime = DateTimeValue.Now();
            }

            return currentDateTime;
        }

        public virtual int GetImplicitTimezone()
        {
            return GetCurrentDateTime().TimezoneInMinutes;
        }

        public virtual XPathContextMajor NewXPathContext()
        {
            XPathContextMajor c = new XPathContextMajor(this);
            c.CurrentOutputUri = principalResultURI;
            return c;
        }

        public virtual void SetUseDocumentProjection(PathMap pathMap)
        {
            this.pathMap = pathMap;
        }

        public virtual StylesheetCache GetStylesheetCache()
        {
            lock (syncLock)
            {
                if (stylesheetCache == null)
                {
                    this.stylesheetCache = new StylesheetCache();
                }

                return stylesheetCache;
            }
        }

        public virtual Func<ISequenceIterator, FocusTrackingIterator> GetFocusTrackerFactory(bool multithreaded)
        {
            return multithreaded && multiThreadedFocusTrackerFactory != null ? multiThreadedFocusTrackerFactory : focusTrackerFactory;
        }

        public virtual IFocusIterator MakeFocusTracker(ISequenceIterator iter, bool multithreaded)
        {
            if (iter is IFocusIterator)
            {
                return (IFocusIterator)iter;
            }
            else
            {
                return GetFocusTrackerFactory(multithreaded)(iter);
            }
        }

        public virtual void SetFocusTrackerFactory(Func<ISequenceIterator, FocusTrackingIterator> focusTrackerFactory)
        {
            this.focusTrackerFactory = focusTrackerFactory;
        }

        public virtual void SetMultithreadedFocusTrackerFactory(Func<ISequenceIterator, FocusTrackingIterator> focusTrackerFactory)
        {
            this.multiThreadedFocusTrackerFactory = focusTrackerFactory;
        }

        public virtual void SetMemoizingFocusTrackerFactory()
        {
            SetFocusTrackerFactory((@base) =>
            {
                FocusTrackingIterator fti;
                if (!(@base is IGroundedIterator && ((IGroundedIterator)@base).IsActuallyGrounded()) && !(@base is IGroupIterator) && !(@base is IRegexIterator))
                {
                    try
                    {
                        MemoSequence ms = new MemoSequence(@base);
                        fti = FocusTrackingIterator.Track(ms.Iterate());
                    }
                    catch (UncheckedXPathException e)
                    {
                        fti = FocusTrackingIterator.Track(@base);
                    }
                }
                else
                {
                    fti = FocusTrackingIterator.Track(@base);
                }

                return fti;
            });
        }
    }
}