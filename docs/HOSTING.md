# Hosting OutSmart.DAXon

How to embed the engine in a host (a service, a batch worker, a CLI) correctly and at full speed.
The rules below are the same contract Java Saxon-HE has. Where a number is given, it was measured;
the last section says how these claims are tested.

## Object lifecycle: what to share, what to create per call

| Object | Cost to create | Sharing rule |
|---|---|---|
| `Processor` | expensive | ONE per process, share freely across threads |
| `XsltExecutable` (compiled stylesheet) | ~10 ms compile | one per stylesheet, compile once, share freely |
| `XdmNode` (parsed input document) | ~360 ms for a 7.5 MB input | immutable after build - share freely (see DocumentCache) |
| `Xslt30Transformer` (from `Load30()`) | microseconds | ONE PER TRANSFORMATION - never share, never reuse concurrently |

The only hard rule: objects belong to their `Processor`. A node or executable created from
one Processor must not be fed to transformations created from another (the name pools would
disagree). With the static-singleton pattern below this cannot happen by construction.

### Reclaiming engine state

There is no API that empties a cache, and that is deliberate: every cache the engine keeps is
either bounded with eviction or vocabulary-bound (see *Memory behavior* below), so the normal
answer is that you do not need to reclaim anything. The measured shape is a plateau, not a
slope - a host with stable schemas and a stable set of stylesheets reaches steady state and
stays there.

**When you do need it, the unit of reclamation is the `Processor` — and it is a PAIR.** Drop
the `Processor` and every `XsltExecutable`, `XdmNode` and `DocumentCache` built from it **in
the same step**, then build a fresh set. A dropped Processor is fully collectable: measured
over 5 build/use/drop cycles, retention returns to 0 KB.

Splitting that step is the one way to get this wrong. Keeping a cached executable across a
Processor swap does not corrupt anything silently - the engine catches the mismatch and throws
`DAXonApiException: Source document and stylesheet must use the same or compatible
Configurations` - but the failure is intermittent and lands on whichever request happens to be
in the swap window. Hold the Processor and its stylesheet cache in ONE immutable object and
replace the reference atomically:

```csharp
private sealed class Generation
{
    public readonly Processor Proc;
    public readonly ConcurrentDictionary<string, XsltExecutable> Sheets;
    public Generation() { Proc = new Processor(); Sheets = new ConcurrentDictionary<string, XsltExecutable>(); }
}
private static Generation current = new Generation();

// per call: take ONE snapshot and use it throughout - a request that started on the old
// generation finishes on it, and that generation is collected when its last caller returns.
Generation gen = Volatile.Read(ref current);
```

Two costs to weigh before recycling on a timer: rebuilding the working set of stylesheets is
~55 ms for a realistic 14 KB / 30-template sheet. What recycling reclaims is the
per-Processor NamePool - the only structure with a hard ceiling - and with it the namespace
URIs only that Processor held (below).

Do NOT try to do this by swapping the name pool. `Configuration.SetNamePool` is `internal` and
not callable from a host, but the same swap is still reachable through
`SetConfigurationProperty(FeatureCode.NAME_POOL, ...)`; that path throws
`InvalidOperationException` on a Configuration that has already allocated user-defined names,
naming the count and pointing at a new Processor. It used to succeed silently and leave every
compiled template rule matching nothing. Reclamation is a Processor operation, not a pool
operation.

## The canonical host wrapper

```csharp
public static class XsltHost
{
    // process-wide singletons: created once, shared by every thread.
    // Resource limits are set here, on the Processor, and everything created from it
    // inherits them; both are optional (defaults: 1 minute and 500 MB a call, the memory limit also the largest input).
    private static readonly Processor Proc = new Processor(new ProcessorOptions
    {
        TransformTimeout = TimeSpan.FromMinutes(1),   // TimeSpan.Zero = no limit
        MaxMemoryBytes = 500L * 1024 * 1024,          // per call; null = no limit
    });
    private static readonly DocumentCache Docs = new DocumentCache(Proc, capacity: 8);
    private static readonly ConcurrentDictionary<string, XsltExecutable> Sheets
        = new ConcurrentDictionary<string, XsltExecutable>();

    public static string Transform(string xsltPath, string inputXmlPath)
    {
        XsltExecutable exe = Sheets.GetOrAdd(Path.GetFullPath(xsltPath), p => Proc.NewXsltCompiler().Compile(p));

        XdmNode input = Docs.GetOrParseFile(inputXmlPath);   // cached parse (see below)

        var tr = exe.Load30();                               // per call - inside the method!
        var w = new StringWriter();
        tr.SetGlobalContextItem(input, true);
        tr.ApplyTemplates(input, Proc.NewSerializer(w));
        return w.ToString();
    }
}
```

Do NOT hoist `Load30()` into a static field: the transformer holds all execution state
(variable stack, output pipeline) and is strictly one-per-transformation. Creating it costs
microseconds - just call it every time.

The second argument to `SetGlobalContextItem` says "this tree has already had
`xsl:strip-space` applied". It is a hint, not a contract the engine takes on trust: whether a
tree still needs stripping is read off the tree itself, so passing `true` for a tree built by
`DocumentBuilder` (which does no stripping - it knows no stylesheet) is harmless. It did not
use to be. In early versions that claim suppressed stripping for the whole transform, so a
stylesheet declaring `xsl:strip-space` silently saw every whitespace-only text node. Hosts on
an older engine should pass `false`.

### Hosting from PowerShell, VB.NET or a DI container

Anything that binds by name at run time - PowerShell, VB.NET, a container resolving
constructors by reflection - needs two things the C# compiler does not.

`new Processor()` maps to a real zero-parameter constructor, so `New-Object` and
`Activator.CreateInstance` find it. In early versions the only no-argument form was a
constructor whose parameters were all optional, which neither of them fills in:
`New-Object OutSmart.DAXon.Api.Processor` failed with *"Cannot find an appropriate
constructor"*. The parameterised overloads still exist and still win when you pass
arguments.

No exported type has two public members differing only by letter case. PowerShell's type
adapter rejects such a type outright - not at construction, but on every member access
afterwards, with *"The field or property: BaseUri ... differs only in letter casing from the
field or property: BaseURI. The type must be CLS compliant."* There were 25 such pairs, and they
are gone: the two in `Api` (`DocumentBuilder.BaseURI`, `QName.GetNamespaceURI`) were removed, and
16 of the rest by no longer exporting the type at all. A test fails the build if one comes back,
a data member against a method included: `Logger.INFO` and `Logger.Info` were such a pair until 1.5, and the constants are now
`Logger.SeverityInfo`, `SeverityWarning`, `SeverityError` and `SeverityDisaster`.

### One way to make a Processor

Every constructor ends in `Processor(ProcessorOptions)`: `new Processor()` is the default options,
and the obsolete ones fill options in from their arguments. The engine core a Processor runs on
(`Configuration`) is new unless the options name one: `ProcessorOptions.Configuration` runs it on an
existing core, and `ProcessorOptions.ConfigurationFile` builds the core from a Saxon configuration
file, read once by the first Processor made with those options (the later ones share that core).
A core that already serves a Processor keeps that Processor's options - the new one reports and
applies them, so a Processor made over the core of a running transformation cannot widen its limits.
Options that ask for less than those allow (a shorter limit, a closed gate, a filter, a host rule) would
be dropped unseen, so that Processor is refused with an `InvalidOperationException`.
`Processor(Configuration)` and `Processor(ResolvedResource)` of 1.3.3 stay, obsolete, as these.

### What the assembly exports

`XPathSelector.Iterator()`, `XQueryEvaluator.Iterator()` and `XdmNode.AxisIterator(...)` are the
names of s9api's `iterator()` and `axisIterator()`; the `IIterator()` / `IAxisIterator()` of 1.3.3
stay, obsolete.

`OutSmart.DAXon.Api` plus what its signatures reach: 531 types, down from 2018. Everything
else is `internal`. If you were reaching into `Trees.Tiny`, `Regex`, `Expressions` or any
other engine namespace, that code no longer compiles - and it was never a contract this
assembly kept. The list is not curated: it is whatever the `Api` signatures drag in
(`NodeInfo`, `AtomicValue`, `ResolvedResource`, `IReceiver`, `Configuration` and their own
closures), so treat anything outside `Api` as an implementation detail that happens to be
visible. A test holds the count under 704 and spot-checks named internals. The test projects
reach past the line through `InternalsVisibleTo`.

## DocumentCache: skipping the input parse

`OutSmart.DAXon.Api.DocumentCache` is a thread-safe, bounded cache of parsed documents
(CLOCK second-chance eviction; hits are lock-free), bound to a Processor. For hosts that transform the same input repeatedly it
removes the parse - the single largest per-call cost - from the hot path.

```csharp
var docs = new DocumentCache(processor, capacity: 8);

XdmNode a = docs.GetOrParseFile(@"C:\data\in.xml");          // file-based input
XdmNode b = docs.GetOrParseContent(xmlString, "urn:my-doc"); // string-based input
```

### Input size limit

The cache rejects oversized inputs BEFORE parsing with a `DAXonApiException`
("Input document too large"). The limit is `ProcessorOptions.MaxMemoryBytes` of the next section -
no input can be larger than the memory of the call that reads it -, **500 MB** unless set, and none
when it is set to null (file length in bytes; UTF-8 bytes for content entries). 1.3.3 capped inputs
at 150 MB and had no memory limit. A parsed tree retains roughly 3x the source text - the guard keeps
one oversized document from exhausting the host.

The guard counts the bytes of the file, not what the call makes of them. Text of one byte a character
is a string of two, and a function that answers with a string as long builds it in one step, which the
limit cannot come between ("Where it is checked", below): under a 200 MB limit a text file of 190 MB was
read by `unparsed-text` with 420 MB held at the peak, `upper-case` of it peaked at 610 MB and
`normalize-unicode` at 1.2 GB, and none of the three stopped. A host that takes text files near its
limit gives the process several times the limit, or sets the limit to a fraction of what it can hold.

```csharp
// tighten or relax per host at the singleton: one number for inputs and the memory of a call
var proc = new Processor(new ProcessorOptions
{
    MaxMemoryBytes = 50L * 1024 * 1024,
});
var docs = new DocumentCache(proc, capacity: 8);
```

`ProcessorOptions` has the one memory option, not a separate input cap. The obsolete
`Processor.MaxInputBytes` reports it; the obsolete `Processor(TimeSpan?, long)` constructors take their
`maxInputBytes` as the memory limit - their default, 150 MB, stands for the default limit, and
`long.MaxValue` for none.

The same limit also guards every resource a STYLESHEET fetches at run time or
compile time through the engine's default resolvers: `doc()`, `document()`,
`collection()` members (each member a ZIP collection inflates counts on its own), `unparsed-text()`/`unparsed-text-lines()`, `json-doc()`, and
`xsl:include`/`xsl:import`/`fn:transform` sources. An oversized resource raises the
requesting function's own retrieval error (`FOUT1170` for text/JSON, `FODC0005` for
documents, message names the size and the limit), so `xsl:try` can handle it like any
other fetch failure. Seekable sources (files) are rejected up front by exact length;
non-seekable ones (HTTP) abort as soon as the byte count crosses the cap. The direct API
entries are capped too (`DocumentBuilder`, `JsonBuilder`, `XsltCompiler`), and
since 1.4 what a stylesheet pulls in - its external DTD subset, its external entities - counts
like a source document's (until then the compiler read them whole), so the only remaining
uncapped path is a host-registered custom resolver - the host's own code returns that stream,
and capping it is the host's job.
Oversized documents that must be processed anyway belong on a separate Processor with a
raised limit and a low-concurrency queue: peak memory is roughly `concurrency x
per-transform working set`, and one 300 MB input costs ~1 GB of tree alone.

Measured effect on a 7.5 MB input: a warm call drops from
~1250 ms (parse 361 + transform 889) to ~889 ms, i.e. **the entire parse, 20-30% of the
end-to-end call** depending on machine state. The first call with a new input still pays
the parse and populates the cache.

### Why the cache can never serve a wrong or stale document

- **File entries** are keyed by case-normalized full path + last-write time + length.
  A modified file has a different key and is re-parsed on the next call; the superseded
  tree simply ages out of the cache. There is no invalidation to get wrong.
- **Content entries** carry the document text itself in the key. The hash only samples the
  ends of the text; on a hash match, equality is a full character-by-character comparison -
  a collision can trigger an extra compare, never a wrong hit. The base URI is part of the
  key too.
- **Concurrency**: hits are lock-free dictionary reads; inserts and evictions serialize on
  one lock. A concurrent miss on the same key may parse twice (the parse runs outside the
  lock); the trees are equivalent and the last one wins. Cached nodes are immutable and
  safe to read from any number of threads.

### Sizing

Each cached 7.5 MB document holds roughly 25 MB of tree. `capacity` bounds the number of
retained trees (default 8); pick it from your input-repetition profile, not from RAM
optimism. Entries for superseded file versions count against capacity until evicted.

## Memory limit per call

`ProcessorOptions.MaxMemoryBytes` bounds the memory one engine call holds - a compile, a document
build, a transformation, a query, an XPath evaluation: 500 MB unless set
(`Processor.DefaultMaxMemoryBytes`), and null for no limit at all - neither memory nor input size.
Over it the call
stops with `SXLM0003` ("Transformation exceeded its memory limit of 500.0 MB
(ProcessorOptions.MaxMemoryBytes): 512.4 MB held, 4.9 MB of it the trees it was handed").
Like the time limit it is the host's authority: `xsl:try` and XQuery `try/catch` do not catch
it, and `parse-xml()` and its kin keep its code.

```csharp
var proc = new Processor(new ProcessorOptions { MaxMemoryBytes = 500L * 1024 * 1024 });
```

**What counts.** What the call holds, not what it allocates and drops. .NET cannot tell how much of
the heap one call holds - the heap belongs to the whole process - so each call keeps a ledger of the
structures it builds: trees (with their namespace maps), strings, sequences, maps and arrays, and the
buffers of sorting, grouping, `distinct-values()` and `reverse()` while they gather. Each is entered
weakly, so one the call drops leaves the ledger with the collection that frees it. Large structures
are entered one by one, small trees, maps and arrays by sampling, each sample standing for the bytes
around it. The trees the host hands the call - global context item, selection, parameters, function
arguments - count once each, by their size, however many calls share them (of a sequence, the trees
among its first 1024 items).

The sum is an estimate. On fourteen workloads - a 40 MB document and a copy of it, 300000 small
trees, millions of strings or integers, maps made by merge and by put, arrays, JSON objects, deep
namespace maps, a long string, the result of `serialize()` - it came within 0.9 to 1.12 of what the
heap held for the call. What a call allocates and drops does not count: the W3C suites pass under the
default limit, where a limit on allocations stopped four tests that hold tens of MB and allocate
gigabytes - catalog-007 holds 75 MB and allocates 2.6 GB.

**Per call, not per process.** Each call sums its own ledger, so calls running at once do not count
each other: four transformations holding 190 MB each ran together under the default, the process
holding 1.7 GB, while memory bombs started beside them stopped. N concurrent calls can therefore hold
N times the limit; bound the process yourself (concurrency, a Job Object, a container). A nested
transformation (`fn:transform`, `load-xquery-module()`) is part of its caller and enters what it
builds in the caller's ledger. Each evaluation of an `XPathSelector` and each direct function call of
an `XQueryEvaluator` is a call of its own. What an extension function builds through the engine
counts; what it keeps in its own .NET objects does not.

**Where it is checked.** The ledger is summed as the call allocates - after every sixteenth of the
limit (256 KB to 32 MB), at the points where the time limit is checked and on every buffer an input
stream reads. A sum over the limit may still count what the call dropped and no collection has freed
yet, so the call stops only if the sum is still over after a garbage collection - the one step that
reaches the whole process, taken only by a call over its limit by its own count. A collection that
finds the call under its limit makes the next wait for half a limit of allocations, doubling up to
four. A call is seen about a sixteenth of the limit after it goes over, later when one step builds a
large structure at once; one huge allocation (a 2 GB string) cannot be stopped in advance and ends as
`OutOfMemoryException`, as without the limit. What .NET builds for the engine is not in the ledger -
the buffer an XML reader fills with a text node is gone with the node; the tree it goes to counts.

**Lazy results.** A lazy result the host iterates - `XPathSelector.Iterator()`, `foreach` over a
selector, `XQueryEvaluator.Iterator()` - runs each step under the limits of the call that made it,
whatever ran on the thread since. What the host builds between steps, in the body of its loop, is its
own, and a limit that fires in a step reaches the host as `DAXonApiUncheckedException`, not as an
engine exception.

**Inputs.** `MaxMemoryBytes` is also the largest input accepted: a file or stream larger than
the limit is refused before it is read, with the fetch's own error code (see *Input size limit*).

**Runtimes.** The ledger works on every runtime. A 4.7.2 runtime has no per-thread allocation
counter to say when to sum it, so there it is summed at every check instead.

## Transformation time limit

A stylesheet can run for a very long time - or forever - without using any extra memory: a
legal `<xsl:for-each select="1 to 2000000000">`, nested loops, or an infinite tail-recursive
function or template. Neither this engine nor Java Saxon-HE has historically imposed a runtime
clock on such work, so a single runaway (or hostile) input can pin a thread indefinitely.

The engine therefore applies a cooperative wall-clock deadline per transformation, taken from
the Processor's `TransformTimeout` (set at Processor construction, **default one minute**).
When it is exceeded the run aborts with a dynamic error (`SXTO0001`, "Transformation exceeded
its time limit"), unwinding the stack cleanly - every lock is released, so the shared
`Processor` and name pool stay intact (unlike `Thread.Abort`, which cannot be used safely here).

```csharp
var proc = new Processor(new ProcessorOptions { TransformTimeout = TimeSpan.FromMinutes(5) });
// TransformTimeout = TimeSpan.Zero  => no limit (Zero or negative disables)
```

**It is a per-CALL limit, not a per-request one.** Each engine entry point claims the full
budget for its own scope, so a host that compiles a stylesheet, builds an input tree, parses a
JSON parameter and then transforms has a worst case of roughly **4x `TransformTimeout`** for
that one message. This is deliberate: a single budget shared across the phases would abort
legitimate work on a cold cache, where the compile is the expensive part and the transform is
not. A host that needs a bound on its whole request must impose that itself - the engine limit
bounds runaway *engine* work, not host orchestration.

Every execution created from the Processor arms the deadline at the start of its run:
`Xslt30Transformer` (`Load30()`), the older `XsltTransformer` (`Load()`), XQuery
(`XQueryEvaluator`), the XPath API (`XPathCompiler`/`XPathSelector`), `DocumentBuilder`,
and `JsonBuilder`. An `XPathSelector` arms at each evaluation and an `XQueryEvaluator` at each
direct function call; until 1.4 both armed once, so a reused one ran every call on the first
call's deadline. A selector's `EvaluateSingle`, `EffectiveBooleanValue` and `foreach` report a
limit raised inside an iterator as `DAXonApiException`, as `Evaluate()` does; until 1.4 it reached
the host as a raw `UncheckedXPathException`. Compile-time `use-when` and static-variable
evaluation shares its compile's deadline (until 1.4 it armed a fresh one per expression, so N of
them compiled for N limits).
`JsonBuilder` was, in early versions, the one entry point
that armed nothing - and arming it was only half the fix, because no loop on the
JSON path sampled the clock at all, so `parse-json()` / `json-doc()` inside a *transform* also
ran to completion past a deadline that was armed and being ignored. The tokenizer now checks
it at the single funnel every token passes through. Arming is also what makes the deadline *per-run*: each run claims the
thread's active-deadline slot afresh, so a deadline left over from an earlier, finished run on
the same thread can never abort later work - and gives the slot back when it ends, so the host's own
work on the thread afterwards (`GetStringValue`, iterating a value a call returned, `ToString` of a
node) runs under no call's limits. Until 1.4 a finished call's deadline, and under `MaxMemoryBytes`
its count, could stop that work with nothing running. An XQuery library module pulled in from XSLT or XQuery via
`fn:load-xquery-module` runs on its own controller but *inherits* the caller's deadline, so a
runaway module function cannot outlive - nor reset the clock of - the run that loaded it. The
same holds for `fn:transform`: the nested transformation builds its own controller from its own
Processor, and is capped at the calling run's absolute deadline rather than starting a fresh
budget - including when the nested Processor has no limit of its own.

Two properties the deadline deliberately has, both wider than upstream Saxon (which has no such
limit to protect):

* **A stylesheet cannot catch it.** `xsl:try` intercepts dynamic errors, but not `SXTO0001` -
  a limit the host set on the run is not something the run may decline. (Nor is `SXLM0001`,
  the recursion-depth error: there is no stack left to run a handler on. Upstream's `xsl:try`
  and XQuery `try/catch` let it through as well; it reaches the host as a coded error.)
* **It is checked per item AND per step.** Hot iterators sample the clock on a stride, but
  instruction-level loops - one `xsl:for-each` iteration, one apply-templates dispatch, one
  group, one tail call - are throttled separately, so a workload of few-but-very-expensive steps
  is bounded just as tightly as a tight numeric loop. Detection is cooperative, so the abort
  lands at the end of the step in progress: a single step that blocks for minutes inside one
  BCL call is still only interrupted when it returns. One such call is known: on .NET Framework
  `contains`, `substring-before` and `substring-after` under a UCA or `lang` collation are one
  NLS search, whose cost can be the length of the string times that of the pattern - a string of
  hundreds of KB and a pattern of a few KB can run many times past a limit of seconds (net8 and
  net10: the limit holds). A host
  that takes such strings from outside bounds their length itself, or uses the codepoint
  collation, whose search is checked.

A limit met while a document is read - also inside an external entity, which System.Xml wraps
in an error of its own - is `SXTO0001` or `SXLM0003` too: `doc-available` does not take it for a
document that cannot be had, and `on-error` of a collection does not take it for a member that
failed (until 1.5 it did, and the run went on).

Jobs
that legitimately need a different budget (for example a slow low-concurrency queue) belong on
their own Processor with its own limit. The check is cooperative: the hot evaluation loops
(`xsl:for-each`, `for`-expressions, filters, mapped sequences, integer ranges, the tail-call
trampoline) test the deadline, so abort latency is bounded by one loop iteration rather than
instantaneous. Since 1.4 so do the loops over a value already in memory, whose own iterator
looks at no clock - the array functions, `fold-left`/`fold-right`, `filter`, `for-each`,
`for-each-pair`, `some`/`every`, `index-of`, `string-join`, `deep-equal` - and the sort into
document order (a union of a million nodes); over a few million items each ran a limit two to
three times over. A limit that fires inside `parse-xml()`, `parse-xml-fragment()` or
`load-xquery-module()` keeps its code `SXTO0001` (until 1.4 they re-coded it as FODC0006, a
malformed document, and FOQM0003, a broken module).
This covers unbounded-time-but-bounded-memory loops; deep non-tail recursion is
already capped by the `SXLM0001` stack guard, and wide input iteration by the input-size limit
above. A run with no limit (or one that is never reached) pays a single boolean test per loop
iteration and its output is byte-for-byte identical.

*Compilation* runs under the same limit: `XsltCompiler.Compile`/`CompilePackage`,
`XQueryCompiler.Compile`, and `XPathCompiler.Compile`/`CompilePattern` each arm the Processor's
deadline for the duration of the compile, so hostile *text* whose constant folding is
attacker-sized work - `sum(1 to 2000000000)`, `count((1 to 2000000000)[…])` - is bounded too.
A compile the limit stops fails with `SXTO0001` ("Compilation exceeded the time limit of ...
(ProcessorOptions.TransformTimeout)"), wherever the optimizer was. Before 1.4 it could also
succeed: a fold the limit interrupted was taken for the expression's own dynamic error and
compiled in, so every run of the stylesheet then failed at once, having spent no time at all;
or the fold was dropped and the compile went on past its limit. Stacked bombs share one
budget: once the compile deadline has passed, every further check aborts immediately, so N
bombs cost one timeout, not N. The compile's own phases look at the clock as well - the parser
once per sub-expression, the type checker and optimizer once per level of descent, the
stylesheet-tree and expression-tree walks once per node - so a text whose *compilation* is the
attacker-sized work stops too: 20000 chained `let` clauses or 100000 nested `xsl:if` are
quadratic to compile and ran minutes past the limit while only the folds were watched. The
checks are throttled (the clock is sampled on a stride the deadline tunes), so they cost a
compile nothing measurable.
Compile and run each count the full budget from their own start: a 10 s limit is 10 s for the
compile and another 10 s for every transformation.

Together with the input-size limit, this bounds the two ways a single input can exhaust the
host: memory (oversized document) and time (runaway loop).

**Coverage** (verified with a 2 s limit against adversarial-but-legal workloads): the evaluation loops above, plus the regex engine (every `matches`/`replace`/
`tokenize`/`analyze-string` candidate attempt and every backtracking step), plus the XML
parse loop itself (a `doc()` of a large document mid-run, and the source parse of a
transformation - the deadline is armed *before* the source parse, so parse time counts
against the budget). All abort within a whisker of the limit. Since 1.4 so do the functions
that go over one long string, every 64K characters: `string-to-codepoints` and `tokenize` gave
millions of items that `count()`, `sum()` or `[last()]` pulled with no look at the clock, a
regex of a fast shape (`\s+`) scanned the subject in one call, and `upper-case`/`lower-case`,
`normalize-space`, `escape-html-uri`, the `hexBinary`/`base64Binary` conversions, JSON parsing,
serialization, `xml-to-json` and a text node built from a long string ran their loops over it
unchecked - 200 million characters went 1 to 17 s past a limit of 0.5 s. What
remains is a step that is one call into .NET, linear in the string and run to its end: a
comparison or search under a collation other than the codepoint one (`compare`, `contains`,
`starts-with`, `ends-with`, `substring-before`/`-after`, `collation-key`), `double.Parse` of a
numeral cast to `xs:double`, the copy that joins two long strings. Unicode normalization is not
on the list: `String.Normalize` sorts a run of combining marks in quadratic time, so from 4 096
characters a string goes to it a stretch of some 4 096 at a time, the clock looked at between
them. A stretch ends before a character that combines with nothing before it - an ASCII one, a
CJK ideograph, a kana letter, a Hangul syllable, or any character of the Basic Multilingual Plane
that is no combining mark or conjoining jamo and whose decomposition begins with neither, which
the platform is asked once a character -, so the stretches normalize apart as they do together;
one with more than 32 combining marks in a row, or with no such place for 8 192 characters (marks,
jamo, characters past the plane), the engine decomposes and puts in canonical order itself, and
the platform composes it (the answers are the platform's). For a run of
marks that is what keeps the call under the clock. Text that is its own normal form comes back
as it went.
Over 200 million characters
on .NET Framework 4.7.2 a collation search takes 6 to 8 s, the cast 3 s, the join 2 s; on
.NET 10 the search about 1.7 s and the other two as much. Strings a call builds count against
`MaxMemoryBytes`, which bounds them; a string the host hands in is the host's to size.

## Paths, file names and encodings

A string given as a path is the name of a file, whatever it looks like. `Build(path)`, `Compile(path)`,
`DocumentCache`, `NewSerializer(path)`, the current directory as a base and the directories a collection
lists keep `%41` or `%20` in a name as those characters: the base URI of `C:\pc%41\x.xml` is
`file:///C:/pc%2541/x.xml`, as Java's `File.toURI()` writes it. Up to 1.4 such a name read or wrote another
file, `C:\pcA\x.xml`, without an error. A URI is decoded once: `xsl:result-document href="x%2541.xml"` writes
`x%41.xml`, as Java does, and `href="x%41.xml"` writes `xA.xml`.

A base URI that is a Windows path with backslashes, `C:\dir\`, is not a URI: `resolve-uri` says so with
`FORG0002`, as Java does. Use the entries that take a path. `new Uri(path).AbsoluteUri` makes a file: URI, but only
when the path has no `%`: that call itself reads `C:pc%41` as `C:pcA`.

The engine reads a file that another process still has open for writing, as Java does, and opens what it
reads with `FileShare.ReadWrite | FileShare.Delete`. A file it writes others can only read until it is closed.

The names Java gives Windows code pages mean the same on every runtime for `output:encoding`, `unparsed-text` and the
declaration of an XML document: `cp1250` to `cp1258`, `ms932` and `windows-31j` (Shift-JIS), `macroman`, `tis620`,
`x-windows-874`, `ms874`, `x-iso-8859-11`. `cesu-8` and `iso-8859-16` have no encoding in .NET and are `SESU0007` or
`FOUT1190`; `UTF-7`, which only .NET Framework has, is refused there too, under any of its names and in the declaration
of a document, an external entity or an external DTD (`FODC0002`), where it would turn markup hidden in text into
markup, and of a query module (`XQST0087`). A declaration counts however long it is,
up to 64 KB; one not closed within that is `FODC0002`.

The single-byte tables the engine uses are not quite the platform's. A byte a code page leaves undefined, which .NET
reads as a C1 control or a private-use character, is an error for `unparsed-text`: `FOUT1190` or `FOUT1200`. In a document
it reads as U+FFFD, as Java's parser reads it, so that a file one undefined byte spoils still reads. `iso-8859-7` is
its 2003 edition, with the euro at 0xA4; `iso-8859-8` its 1999 edition, with the two marks at 0xFD and 0xFE; `koi8-u`
has box drawing at 0xAE and 0xBE, as
RFC 2319. `windows-874`, `TIS-620` and `ISO-8859-11`, one table to .NET, are three as in Java: TIS-620 has nothing at
0x80-0x9F, ISO-8859-11 the C1 controls. A document labelled `US-ASCII` with a byte past 7 bits is `FODC0002`, as in Java,
not `?`. A UTF-8 mark before a declaration of another encoding gives way to the declaration. The other way, a character such a table lacks is not written: a character reference in markup, `SERE0008` in
text, one `?` for the adaptive method. A document's external entities still go to `System.Xml` with the encoding they declare.

A file past 260 characters reads on every runtime, the .NET 4.5 mode included: `doc`, `unparsed-text`, the `-available`
functions, `DocumentBuilder.Build`, and output to a directory that exists; `collection()` with `recurse=yes` walks into such
directories. In that mode `collection()` of a directory past 248 characters and the creation of one stop with an error, as
`System.IO` there takes no long path in any form. `file://localhost/C:/x.xml` is the file `C:\x.xml`;
`file://localhost/c$/x.xml` is the share `\\localhost\c$`, a network path, refused when `AllowNetwork` is false.

`math:pow` and `math:exp10` are correctly rounded and the same on every runtime, which `Math.Pow` is not: `math:pow(10e0, 23)
eq 1e23`. They allocate nothing; a power that is not a multiple of 1/8 takes about 0.3 microseconds a call, some four times
`Math.Pow`, and integer powers about 0.2 more.

The place of `format-dateTime` is looked up in the IANA time zone database that comes with the engine, release 2024b, the
same on every runtime and whatever the machine's updates (`data/tzdb.bin`, made from a JDK's database). A name it does not
have may still be a zone of the platform.

The test projects are x64. A 32-bit process gave the same answers on the spec suite and the conformance
corpus; its stack frames and limbs differ, so the depths and the speed of huge integers quoted here do not
hold there.

## Resources fetched by URI

Anything a stylesheet pulls by URI - `doc()`, `document()`, `collection()`,
`unparsed-text()`, `json-doc()`, `xsl:include`/`xsl:import`, `fn:transform` - reaches a
server (or a file tree) the host may not control. That surface was tested against a
deliberately hostile local server; every axis below ends in a catchable error inside the
run's own limits.

**Recursion in the fetch graph.** A module that includes itself, directly or through a
cycle of URIs, is `XTSE0180`/`XTSE0210` as the spec requires. A chain that hands out a
*fresh* URI per level defeats that check (the URIs never repeat), so include/import nesting
is additionally bounded by the stack the running thread actually has. The bound is
adaptive, not a fixed number: each open module takes ~2 KB of stack on .NET Framework and the
engine reserves 2 KB more per module, which puts the ceiling at ~35 nested modules on a 256 KB
thread (the IIS default), ~250 on a 1 MB worker thread and proportionally more on a larger stack -
about a third of the depth at which an error can still be reported, so the guard has a safety
factor. On .NET 8 and 10, whose first-tier code takes bigger frames, it is ~22 and ~140.
Reporting an error out of a nested include costs no extra stack per level (before 1.5 a cycle
error re-entered exception dispatch on every level, and the ceiling was 7 nested modules on
256 KB). Beyond it the compile fails with `XTSE0010` "include/import
nesting is too deep". Real stylesheet libraries nest a handful of levels, so this is only
ever reached by a generated or hostile chain - but a host that legitimately nests deeply
should give its worker threads a larger stack. A document
chain followed by a *recursive function* (`doc()` of a document naming the next document) is
bounded by the ordinary `SXLM0001` recursion guard, not by this.

**Hostile responses.** An endless body (no `Content-Length`, bytes forever) stops at
the input cap (`MaxMemoryBytes`). A body that trickles - one byte every few seconds, so no single socket read
ever times out - aborts with `SXTO0001`: the run's remaining budget is handed to the socket
layer as its own connect/read timeout, and the response stream re-checks the deadline before
every read. A server that accepts the connection and then says nothing likewise fails inside
the budget instead of parking the thread. Redirect loops end at the platform's redirect cap.
Entity-expansion bombs ("billion laughs") stop at 10,000,000 characters of entity text per
document (`SXXP0003` from the API, `FODC0002` from `doc()`, `FODC0006` from `parse-xml()`; the
message names `MaxCharactersFromEntities`), and a self-referencing external entity is rejected as
not-well-formed; note that the input cap limits the bytes *fetched*, not what a DTD expands
them into, so it is the parser limit that matters there. Until 1.4 the engine left that limit
to .NET, which sets it only for an application that targets .NET Framework 4.5.2 or later: in
the legacy XML mode - an exe built for an earlier version, an IIS application whose
`<httpRuntime targetFramework>` is earlier or absent (some application servers still say `4.5`) - there was none:
a 1 KB document expanding to 10^8 characters ran `TransformTimeout`'s whole budget (10 s, 864 MB),
and one expanding to 10^10 was not stopped by it at all. The value is .NET's own default, so on
.NET and on other .NET Framework hosts nothing changes. `parse-xml-fragment()` passes its input
to the reader as an entity, so a fragment longer than the limit used to fail as not well-formed
wherever the limit was on; its own characters no longer count.

Two caveats. First, a run with **no** deadline (`transformTimeout: TimeSpan.Zero`) keeps the
platform's socket defaults, and a trickling server is then bounded only by those - if the
host disables the time limit it owns this risk. Second, a **host-supplied** resolver does its
own fetching and is the host's own code: neither the size cap nor the deadline plumbing can
apply to it. Restricting *which* schemes a stylesheet may fetch at all (an SSRF question, not
a robustness one) is the `ALLOWED_PROTOCOLS` configuration feature.

**Which resolver answers.** `doc()` and `document()` ask the run's resolver
(`Xslt30Transformer.SetResourceResolver`, `XQueryEvaluator.SetResourceResolver`), then the
configuration's, and only then read the URI themselves. Since 1.4 `unparsed-text()`,
`unparsed-text-lines()`, `unparsed-text-available()` and `json-doc()` ask in the same order, after a
`SetUnparsedTextResolver` the host set, as Saxon 12 does. Before, they read a file or URL themselves
and asked only the configuration's resolver, and only once that read had failed: a host's resolver
could not serve them, and the fallbacks tried a failed read again - a missing file was opened four
times, a URL answering 404 fetched three times. The resource policy and `ALLOWED_PROTOCOLS` guard
the engine's own reads, so a host's resolver may serve a URI the policy refuses, as it may for
`doc()`; an `http:` URI under `ALLOWED_PROTOCOLS=file` is refused before any request (it used to
be fetched, then refused).

**The input cap covers the direct API too.** In early versions the cap applied only to
resolver-routed fetches (above) and to `DocumentCache`; input the host handed straight to
`DocumentBuilder.Build`, `JsonBuilder`, `XsltCompiler.Compile` or (until 1.4) `XQueryCompiler.Compile` was
uncapped. All of those now reject over-cap input with a
`DAXonApiException` naming the actual size and the limit - by path too (`Build(path)`,
`Parse(path, ...)`, `Compile(path)`, `CompilePackage(path)`, a `ResolvedResource` with only a
`SystemId`), which until 1.4 let the reader open the file uncapped. A `Stream` or `TextReader` of
unknown length is enforced as it is consumed, so the abort lands before the whole input is
in memory - `JsonBuilder.ParseJson(TextReader)` too since 1.4, which read the whole reader first.
Since 1.4 the cap also reaches the inputs it had missed: `Xslt30Transformer.ApplyTemplates(Stream, ...)`,
a `ResolvedResource` that carries its own stream or reader (`Compile`, `CompilePackage`, `Parse`,
`Serializer.Serialize`) and the external DTD and external entities a document pulls in.
The limit is in **bytes**: `DocumentCache.GetOrParseContent` used to compare
character count against it, which let multi-byte UTF-8 through at 2-3x the declared cap.

## XML input: DTDs, whitespace, validation

**A document's DTD and entities.** The parser reads an external DTD subset or an external entity
that is a file - under the resource policy and the input cap - and refuses any other URI: a
document whose DOCTYPE names `http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd` does not parse
on its own, and nothing is fetched from the network for it. Since 1.4 the configuration's
resolver (`Configuration.SetResourceResolver`) is asked first, on every input path -
`DocumentBuilder.Build`, `ApplyTemplates(Stream, ...)`, `doc()`, `parse-xml()` - as it already was
for a stylesheet's DTD. `ResourceRequest.nature` is `EXTERNAL_ENTITY_NATURE`, `uri` the absolute
system identifier, `publicId` the PUBLIC identifier when the declaration has one. Answer with a
`Stream` (a `TextReader` is re-encoded as UTF-8, so its text declaration must not name another
encoding), or return null to leave the reference to the parser. What the resolver serves is the
host's own: neither the policy nor the input cap applies to it. Two things to expect. For a
declaration with a PUBLIC identifier System.Xml first tries that identifier as if it were a URI,
so a request for something like `.../-//W3C//DTD XHTML 1.0 Strict//EN` arrives before the real
one: return null for it. And the DTD itself is asked for twice, the second time to learn which
attributes are IDs (`id()`, `idref()`). XML catalogs are not read: `Processor.SetCatalogFiles`
throws `NotSupportedException` (until 1.4 it did nothing, silently) - a resolver does what a
catalog would.

**Whitespace a DTD makes ignorable.** Where the DTD declares an element with element-only
content, the whitespace between its children is left out of the tree, as Saxon leaves out what a
SAX parser reports as ignorable. In mixed or `ANY` content, under `xml:space="preserve"` and in an
element the DTD does not declare, it stays. Since 1.4 that is so on every input path. Before, only
`DocumentBuilder.Build` left it out, so one document was two different trees depending on
whether it came through `Build` or through `doc()`, `ApplyTemplates(Stream, ...)`, `parse-xml()`;
and `Build` also dropped the whitespace of every element the DTD did not declare - with a DOCTYPE
that declares nothing but entities, every whitespace-only text node of the document, the space in
`<b>a</b> <i>b</i>` included. To keep all whitespace: `WhitespaceStrippingPolicy.NONE` on the
builder, or the `STRIP_WHITESPACE` feature set to `none` for the configuration. A document
without a DOCTYPE is not affected, and `Build` now reads it with the plain reader, which takes
about a quarter less memory per parse.

**DTD validation.** `DocumentBuilder.SetDTDValidation(true)` validates what that builder builds.
The `DTD_VALIDATION` feature validates what the engine reads itself - `doc()`, `document()`,
`xsl:source-document`, the input of `ApplyTemplates(Stream, ...)` - but not `parse-xml()`, which
never validates, and not a builder, whose own flag decides. Every validity error goes to the
error reporter, and the parse then fails with a `DAXonApiException` - `SXXP0003`, or `FODC0002`
from `doc()`, which `xsl:try` catches and `doc-available()` answers with false - that says how
many there were and quotes the first. A document with no DOCTYPE has nothing to be valid against
and fails the same way. With `DTD_VALIDATION_RECOVERABLE` the errors are reported, a warning sums
them up and the document is used. Until 1.4 the builder's flag validated nothing, the feature
worked for `doc()` alone and let System.Xml's own `XmlSchemaValidationException` out of the
engine, and the recoverable form did nothing.

**XML 1.1** is not read: the .NET parser implements XML 1.0 only and refuses a declaration that
says `version="1.1"`. A document, a stylesheet or a stylesheet module so labelled is read as the
XML 1.0 document it nearly always is, with one allowance: character references to the C0
controls (`&#x1;` ... `&#x1F;`), which 1.1 permits, are let through. Since 1.4 that is so on
every input path and in every encoding the parser detects. Before, the label was taken off only
for a byte stream in an encoding that keeps ASCII: `Build(Stream, ...)`,
`ApplyTemplates(Stream, ...)` and `doc()` read the document that `Build(TextReader, ...)`,
`Build(path)`, `Parse(path, ...)` and `parse-xml()` refused, nothing read it in UTF-16, and a
stylesheet compiled from a stream but not from text or a path. Nothing else XML 1.1 adds is
there: its additional name characters and the undeclaring of a prefix (`xmlns:p=""`) are errors,
NEL (U+0085) and U+2028 are ordinary characters rather than line ends. An external DTD or
entity whose own text declaration says 1.1 is read the same way under a document labelled 1.1
(since 1.4; it was refused), and refused under any other, as XML has it: no entity is of a later
version than the document that includes it. `parse-xml-fragment()` takes a fragment labelled 1.1
likewise. What XML 1.1 forbids as 1.0 does -
`&#0;`, a reference to half a surrogate pair, U+FFFE, U+FFFF - is refused under the label as
without it. Until 1.4 those came through on the stream paths, and `&#0;` was not harmless: the
serializer uses U+0000 as its own mark for switching output escaping off and on, so a document
labelled 1.1 could have its escaped text (`&lt;script&gt;`) written out as markup.

**A fragment's own declaration.** `parse-xml-fragment()` reads its argument as the characters it
is. Until 1.4 the fragment went to the parser as UTF-8 bytes, so a text declaration that names
another encoding (`<?xml version="1.0" encoding="iso-8859-1"?>`) had every non-ASCII character
read as two - `é` as `Ã©` -, and one that names UTF-16 or an encoding .NET does not know was an
error. `parse-xml()` never had this.

**XInclude** is not available: the .NET parser does not implement it. Asking for it is an error
since 1.4 (it used to be accepted and ignored): the `XINCLUDE` feature set to true
(`ArgumentException`), `Configuration.SetXIncludeAware(true)` (`NotSupportedException`),
`?xinclude=yes` on a document or collection URI and `saxon:xinclude="yes"` on
`xsl:source-document` (a dynamic error, FODC0002).

**What a name on Windows may be.** Windows has names for what is no file: the devices - `CON`,
`NUL`, `COM1`, `\\.\pipe\x` - and, with a colon past the drive, the streams of a file
(`a.xml::$DATA` is the content of `a.xml`). Every file the engine opens by name, to read or to
write, is refused by such a name with the error of the call (FODC0002, FOUT1170, SXXP0003,
XTSE0165, SXRD0004). Until 1.4 the runtime decided: .NET Framework refused a device with an
exception of its own, which from `doc()` and `xsl:include` reached the host as that; .NET 8 and
later open it and read, so `doc('CON')` was a wait no time limit ends, and a stream name read or
wrote the file past any check of how a name ends. A host that writes to `NUL` to discard output
passes `Stream.Null` to `NewSerializer` instead.

## JSON input: `JsonBuilder`

`JsonBuilder.ParseJson` gives what `parse-json()` gives: a string or a key holds the characters
its JSON text stands for. Until 1.4 the builder parsed with the `escape` option on, as Saxon's
does: a backslash, a line break, a tab or a character XML cannot hold arrived as the JSON escape
itself (`C:\\dir`, the two characters `\n`), and a value written back out as JSON came out escaped
twice. `SetEscaped(true)` restores that form for a host that depends on it. The one thing it kept
and the default does not: a character XML 1.0 cannot hold - `\u0000`, `\u0007`, an unpaired
surrogate - becomes U+FFFD, as in `parse-json()`.

### JSON numbers

The standard reads every JSON number as `xs:double`. A stylesheet then prints an identifier
`9007199254740993` as `9.007199254740992E15`, `xs:decimal()` of a price `19.99` gives
`19.989999999999998436805981327779591083526611328125`, and digits past the sixteenth are lost.
`ProcessorOptions.JsonNumbersAsDecimal = true` reads a number as it is written:

| written as | read as | example |
|---|---|---|
| no point, no exponent | `xs:integer` | `9007199254740993` stays `9007199254740993` |
| a point, no exponent | `xs:decimal`, exact | `19.99`; `5.0` prints `5` |
| an exponent | `xs:double`, as before | `1.5e3` prints `1500` |

Up to 1000 digits; a longer number stays `xs:double` (exact reading takes time quadratic in the
digits, and no time limit can stop it). It applies to `JsonBuilder`, `parse-json()` and
`json-doc()`; a `number-parser` option given to `parse-json()` wins, and `json-to-xml()` keeps
the text anyway. A form only `liberal` accepts (`01`, `1.`, `+1`) stays `xs:double`, and `-0`
reads as `0` (neither type has a negative zero).

## HTML input

The engine parses XML, JSON and plain text natively; HTML is not a native input - the same as
Java Saxon-HE, which needs an HTML parser such as TagSoup on its classpath. Parse HTML with a parser of
your choice, and hand the engine its result as XML text, or write it through
`DocumentBuilder.NewBuildingStreamWriter()` (an `XmlWriter`, below). Element names a browser repairs
may be no XML names, and a comment may hold `--`: such a writer refuses them, so leave them out or
replace them first.

## Text and values the host passes in

A .NET string can hold what a document cannot: half a surrogate pair, U+0000, a control
character, U+FFFE. A string the host passes - a stylesheet parameter, a variable, what an
extension function returns, what it writes through the push API - is taken with them, and two
rules decide what becomes of them.

**Half a surrogate pair is U+FFFD.** A string cut through a pair - the first hundred characters
of a text with an emoji in it - has a half no encoding can write. Wherever text of the host's
comes in, U+FFFD stands for it: `new XdmAtomicValue(string)` and the other ways to make a value,
the push API, `Serializer.SetOutputProperty`, the text of an XPath expression or a query, and the
two `XmlWriter` faces, `Serializer.GetXmlWriter()` and `NewBuildingStreamWriter()` - where text
may come in pieces, and a pair cut between two `WriteChars` calls stays the pair it is. Until
1.4 a half at the end of the string was an `IndexOutOfRangeException` out of
`new XdmAtomicValue`, and one inside it took the next character with it (as in Saxon).

What is not a string of the API has its own rule. A resource the host's resolver serves as
characters is the resource: `unparsed-text()` and `unparsed-text-lines()` raise FOUT1190 for half
a pair in it, as for any character XML has not, and the XML parser refuses it. A URI with one is
no URI - `new URI(...)` throws `URISyntaxException`, as for any malformed one. The name of a file
can hold one (NTFS takes any sixteen-bit units): `collection()` and `uri-collection()` list such
a file with U+FFFD in its name, and it is then a file that is not found. A system identifier or
a base URI the host passes is kept as it is given.

**XML output holds the characters of XML.** The `xml` and `xhtml` methods write U+FFFD for what
the XML they write has no character for: U+0000, U+FFFE, U+FFFF, and in XML 1.0 the control
characters other than tab, line feed and carriage return - in text, in an attribute, a comment,
a processing instruction, a CDATA section. Output declared `version="1.1"` keeps a control
character as the reference `&#x1;`, which 1.1 allows. Until 1.4 they were written as they came -
`&#x1;` in a 1.0 document, the rest raw - and no parser read the result. This holds for a
control character a document labelled XML 1.1 brought in as well (see **XML 1.1** above): ask
for `version="1.1"` to carry it through. In a comment or a processing instruction, where no
reference can stand for a character, an encoding that has no U+FFFD gets `?`. `html` writes
U+FFFD for U+0000 wherever it stands - text, an attribute, a comment, a script - which is what an
HTML parser makes of one; the other control characters, and U+007F to U+009F, in text and
attributes are character references from `html-version` 5 (`&#x1;`) and SERE0014 below it, as the
Serialization specification says (up to 1.4 they were left out), and stand as they are in a comment
or a script. `json` escapes them all
(`\u0000`, `\u001F` - until 1.4 U+001F went out raw, which is not JSON).

Where the stylesheet asks for text as it stands, it gets it: the `text` method and
`disable-output-escaping` write every character they are given, and so does the replacement
string of a character map (but for U+0000 in XML or HTML output, which is U+FFFD there too). A
separator that is a control character is passed as a parameter and written by the text method,
as before.

Until 1.4 the serializer also took every U+0000 for the mark it puts around what a character
map wrote, as Saxon does: between two of them the host's own text went out unescaped - `<` as
markup, a quote as the end of an attribute value or of a JSON string - and beside a character
map an odd number of them was an `ArgumentOutOfRangeException`. A host that passed text it did
not write into a transformation should read its 1.3 output with that in mind.

**Values the host builds.** `new XdmValue()` is the empty sequence, `new XdmMap()` the empty map,
`new XdmArray()` the empty array, and an extension function with nothing to return returns
`new XdmValue()` or null. Until 1.4 the three held nothing at all - any use of one was a
`NullReferenceException`, and a host function could not return the empty sequence. `ToString()`
of a value is what the adaptive output method writes for it, indented: the markup of a node,
`map{...}`, the items of a sequence one to a line (before 1.4, the name of the class). It costs
a serialization of the value - a log line that names a document writes the document - and it
throws nothing: a value that cannot be written is the name of its class, as before.

**A document the host writes: the push API.** `processor.NewPush(destination)` works since 1.4
(it was an `InvalidCastException`):

```csharp
using (IDocument doc = processor.NewPush(serializer).Document(true))
{
    IElement top = doc.Element("root").Attribute("version", "1.5");
    foreach (Employee e in employees)
    {
        top.Element("emp").Attribute("ssn", e.Ssn).Text(e.Name);
    }
}
```

`Document(bool)` begins the document; `IDocument(bool)`, the name the method had in 1.3, is the
same method and is marked obsolete.

Starting an element closes the one before it at the same level, and closing the document closes
the destination. Content is escaped as any text is. Names are checked, which s9api leaves to the
implementation: the name of an element, an attribute or a processing instruction must be an
NCName, a comment cannot hold `--` and an instruction cannot hold `?>` - a name or a comment
taken from data cannot turn into markup. The names XML Namespaces reserves are refused too: the
prefix `xmlns`, the namespace `http://www.w3.org/2000/xmlns/`, an attribute named `xmlns` (it
would declare a default namespace; `Namespace()` is the call for that), and a binding of `xml`
or of its namespace to anything but each other. A name in the `xml` namespace is written under
that prefix, and an attribute in a namespace that comes with no prefix is given one. A refused
call is a `DAXonApiException`; nothing more can be written after it, and `Close()` then releases
the destination without finishing the document.

**A document the host writes: the `XmlWriter` faces.** `Serializer.GetXmlWriter()` and
`DocumentBuilder.NewBuildingStreamWriter()` are `System.Xml.XmlWriter`s, and since 1.4 a name means
to them what it means to the writer `XmlWriter.Create` returns: a null namespace is the one the
prefix has in scope (`WriteStartElement("c")` inside `xmlns="urn:a"` is in `urn:a`), `""` is no
namespace, a null prefix takes the one in scope, and an attribute in a namespace that comes with
no prefix gets one - the parent's, or one of its own. A prefix means one namespace in a start tag.
Until 1.4 such an attribute was written with no prefix: it lost its namespace, its binding moved
the element into that namespace, and a second attribute of the same local name made the output
ill-formed. Element, attribute and instruction names, comments and instructions are checked
whatever `SetCheckValues` says, as `System.Xml` checks them: a name or a comment taken from data
cannot end its markup (a comment `x --><b/>` used to write an element `b`). What they refuse is
an `ArgumentException`, or an `XmlException` for an attribute written twice. Where `System.Xml`
itself misreads a call they do not follow it: an attribute `q:xmlns` in no namespace, which
`System.Xml` turns into a default namespace declaration that moves the element, is refused. A
second element at the top level is still written (an external entity, as XSLT output may be).

## Language and the host's culture

The default language - what `default-language()` returns, and what `format-date` and
`format-integer` use when they are given none - is `en`, whatever the machine or the thread
(`xsl:number` with no `lang` has always been English). Up to 1.4 it was the `CurrentCulture` of the
thread that built the Processor, kept for the Processor's
whole life: one made lazily on a request thread fixed that request's language for everybody after
it, a German host wrote `Samstag 31 Oktober 2026`, and the invariant culture answered `iv`. A host
that wants a language says so, once, or per call with the argument of the function:

```csharp
processor.SetConfigurationProperty(Feature<string>.DEFAULT_LANGUAGE, "de");
```

The month and day names of a language other than English come from .NET's culture of that name and
take its case rules: `[MNn]` in French is `Octobre`, `[MN]` in Turkish `ARALIK`. Words, ordinals,
half-days and eras exist in English only, so a picture with one of them is all English and begins
with the marker: `[D1o] [MNn]` in French is `[Language: en]5th October`, as Saxon-HE writes it. A
language .NET does not know is English with the marker too. The names themselves are the runtime's:
NLS on .NET Framework and ICU on .NET 8 and later differ for 28 of 75 languages.

The default country is still read from the host (`RegionInfo.CurrentRegion` of the thread that builds
the Processor, or `Feature<string>.DEFAULT_COUNTRY`), but no output turns on it: `[ZN]` with no place
writes the offset, `-05:00`, on any host, as Java does, and a name takes an explicit place
(`format-dateTime($d, '[ZN]', 'en', (), 'us')` is `EST`). Numbers, dates and comparisons do not read
the culture of any thread.

.NET 8 and later can run with `InvariantGlobalization` (`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`, the
usual setting of small Linux containers), which leaves out the Unicode and culture data. The engine runs,
with three differences. `normalize-unicode` and the `normalization-form` of a serializer refuse text
past ASCII, FOCH0003 and SESU0011, because the platform would return it unnormalized. UCA and `lang=`
collations compare by code point, which `fallback=yes` (their default) allows; ask for `fallback=no` to
get an error instead. Month and day names are English, with the `[Language: en]` marker. A host that
needs any of these sets `InvariantGlobalization` to false and installs ICU.

## Output as bytes: the byte order mark

Whether the bytes of an output begin with a byte order mark is what `byte-order-mark` says -
`xsl:output`, `xsl:result-document`, `Serializer.Property.BYTE_ORDER_MARK` - for every output
method: `yes` is one mark, `no` is none. When nothing is said: UTF-8 has no mark; UTF-16 and UTF-32 have one;
`UTF-16LE`, `UTF-16BE`, `UTF-32LE` and `UTF-32BE` have none - the name says the order of the bytes, and this is
what Saxon writes. `UTF-16` is big-endian, with its mark or without, as Java writes it, which is how bytes labelled
UTF-16 that begin with no mark are read (RFC 2781); up to 1.4 it was little-endian with its mark.

`UTF-32` with a mark is little-endian, without one big-endian, which is what UTF-32 with no mark means, and how bytes
labelled so are read. An XML document labelled `UTF-32` is a choice of parser: `System.Xml` reads only the little-endian
form, Java's parser only the big-endian form with no mark, and the engine's `doc()` and `Build` read both. Up to 1.4 `no`
gave little-endian with no mark, which `System.Xml` read and nothing else should have.

Until 1.4 the parameter did not decide this for UTF-16 and UTF-32: `no` left the mark there,
and `yes` with `UTF-16LE` or `UTF-16BE` wrote two - a document no parser reads. And `UTF-16LE`
and `UTF-16BE` with nothing said began with a mark: an output that relied on it - text or CSV
that a program recognizes by its mark - now needs `byte-order-mark="yes"`.

A mark is the first thing in a stream. Bytes written to a stream that already holds some
(`Position > 0`) get no mark for UTF-16 and UTF-32, whatever is asked.

When the destination is a `TextWriter` of yours, the bytes are yours too. A `StreamWriter`
whose encoding has a preamble (`Encoding.UTF8`, `Encoding.Unicode`) writes its mark itself, and
`byte-order-mark="yes"` adds no second one (until 1.4 it did). Over one that has none
(`new UTF8Encoding(false)`), and into a `StringWriter`, `yes` writes the character U+FEFF first,
for the encodings UTF-8, UTF-16LE and UTF-16BE - as it always has for XML and text, and now for
HTML too.

## Memory behavior on a long-lived Processor

The prod profile - one Processor per process, reused for every transformation, sequentially
and in parallel - is leak-checked empirically (batches of transforms with a full compacting GC
between batches, slope fitted across batches):

- **Steady state is flat.** Same-vocabulary inputs (fresh 640 KB XML parse or 250 KB JSON
  parse per call, cached executable), recompiling the same stylesheet with no host cache, and
  8-thread parallel rounds on the shared Processor all measure **0 B/iteration** retained
  growth over thousands of runs. Everything a run allocates - input tree, variable frames,
  `doc()` documents, key indexes, output buffers - dies with the run. All engine-internal
  caches are bounded (CLOCK eviction, capacities 100-1024).
- **Per-run state is released when the run ends** - including per-THREAD state, which is the
  part a heap total does not reveal. Verified by parking worker threads after their transform
  (a pool thread never dies), dropping every host reference and forcing a compacting GC: a
  4-thread round retains **0 MB**. Two slots caused exactly this and are fixed: the active
  deadline slot held the whole Controller (one input document and its globals pinned per idle
  thread), and the pattern matcher's reusable focus iterator held its last context item (one
  input document pinned per thread that ever matched a predicate rule; 78.6 MB across 8 threads).
  Both now hold value tokens or are cleared on exit. The release is tied to the END of each run rather than to object construction, so it holds even
  for API objects used outside the documented lifecycle (see the reuse caveat below).
- **Swept, not sampled.** A retention matrix ran over 31 feature shapes -
  apply-templates, predicate patterns, modes and `xsl:next-match`, `key()`, `idref()`,
  accumulators, both grouping forms, `xsl:merge`, `xsl:iterate`, sorting, `xsl:number`, global
  variables, tunnel parameters, `copy-of`, user functions, maps and arrays, regex,
  `analyze-string`, date formatting, `xsl:evaluate`, `try/catch`, character maps,
  `xsl:message`, `doc()`, `collection()`, `unparsed-text()`, `json-doc()`, `fn:transform`,
  `xsl:result-document` - in BOTH modes (executable held; threads parked and everything
  dropped), plus the XQuery, XPath and DocumentBuilder APIs. Every one retains 0 B per run.
- **Bounded caches replace, they do not accumulate.** Eight CLOCK caches (capacity 100-1024:
  format-date and format-integer pictures, compiled regexes, XPath executables, converters,
  the optional host DocumentCache, and the per-mode template-dispatch rule memo at 1024)
  evict a second-chance victim before every insert. Driven with distinct keys at 10-20x
  their capacity, the retained heap is flat to **~1 B per further key**. What a cache costs
  is therefore `capacity x value size` — worth thinking about for two of them: the
  DocumentCache, whose values are whole parsed documents (host-chosen capacity), and the
  process-wide compiled-regex cache, whose entry size follows the PATTERN. Dynamic patterns
  are runtime strings, so the ceiling is workload-defined: measured, a full cache of 256
  10 KB alternation patterns holds ~50 MB, of 100 KB patterns ~540 MB (~20 B per pattern
  char). That is a transient high-water mark, not growth: eviction is
  age-ordered (a real clock hand; the newest entry sits behind the hand), and when the
  workload moves back to ordinary patterns the giants leave and the memory returns —
  measured back to ~0 within one capacity's worth of fresh inserts.
- **The type-pair memo is bounded.** `TypeHierarchy` memoizes every `ItemTypePair` it
  is asked about, and it is compile-driven: input names add nothing, ~3 new pairs arrive per
  DISTINCT stylesheet, and dropping the `XsltExecutable` did NOT release them - so a host
  compiling throwaway variants grew this for the Processor's life. It is a memo of a pure
  function, so eviction only ever costs a recompute; capped at `TypeHierarchy.MemoCapacity`
  (4096), which is two orders of magnitude above any realistic working set. Measured with a
  same-session DLL bisect: no transform or compile delta outside the noise band.
- **One structure legitimately only grows** - same design and same bounds as Java Saxon-HE:
  the per-Processor **NamePool** grows ~176 B per *distinct element/attribute QName* ever seen
  (hard cap ~1M names, then a clean `SXLM0004` error, below). It outlives the call, but the call that
  makes a name up answers for it under `MaxMemoryBytes`, for as long as the call lasts: a million new
  attribute names stop with `SXLM0003` under a limit of 100 MB. A later call that meets the same names pays nothing. It is vocabulary-bound: stable
  schemas cost a few KB once. Only machine-GENERATED per-document names (data-derived element
  names, or any name in a namespace embedding a GUID) make it grow per document - avoid that
  pattern, or give such workloads a periodically recycled Processor. Measured:
  50 000 distinct QName+namespace pairs cost 8.9 MB.
- **Namespace URIs live as long as something references them.** The engine compares
  namespaces **by reference** (`NamespaceUri` overrides neither `Equals` nor `GetHashCode` nor
  `==`), so every URI is interned process-wide and has one instance at most. Up to 1.3.3
  that table also kept every URI it ever saw, after the Processor that saw it was gone - in
  Java Saxon-HE it still does. It now holds its instances weakly: a URI that a compiled
  stylesheet, a document, a NamePool entry or the host still holds keeps its instance, and one
  nothing holds leaves the table at the next full collection (the table is swept then, on the
  finalizer thread, so an idle host gets the memory back too). Measured: 50 documents each in
  a 100 000-character namespace held 9.8 MB after their Processor was dropped, and hold 2 KB
  now; 40 000 short URIs leave ~15 B each (the table's bucket array, sized by its peak).
  `NamespaceUri.InternedCount` reports the URIs currently referenced. Evicting a URI something
  still holds would let it be interned twice and compare unequal to itself; two tests pin
  that this cannot happen. Note what does NOT feed the NamePool:
  **JSON and text input are immune** — a JSON key is a map key, not a name, so `json-doc()`,
  `parse-json()` and `JsonBuilder` add nothing to the pool however many distinct keys arrive
  (tested: 3 000 distinct keys, delta 0). Only element/attribute QNames from XML input,
  and names written into stylesheets, allocate fingerprints.

So a long-lived process can only grow in four ways, in decreasing order of likelihood: the
host keeps things alive itself (a DocumentCache sized in documents, executables it never
releases, more Processors than it needs); a workload feeds *generated* vocabulary into the
NamePool; concurrency raises the peak (each in-flight transform holds its own tree
- peak is roughly `concurrency x per-transform working set`); or a single run is simply large,
which is what `MaxMemoryBytes` (500 MB a call unless set) and `TransformTimeout` bound.

Four caveats a per-run measurement cannot express:

- **`doc()` with a LITERAL URI can be pooled for the Configuration's life, not the run's.**
  By default it is not: a document lives as long as the run that read it, which is all the spec asks.
  With `Feature.PRE_EVALUATE_DOC_FUNCTION` on, a literal URI is read at compile time into
  `Configuration.GlobalDocumentPool`, which has no eviction. A per-run slope then reads 0 while the
  pool grows: the cost is one parsed tree per DISTINCT literal URI the Processor ever compiles. For a
  handful of reference documents that is the point of the feature. Do not turn it on when the URI set
  is open-ended; those URIs belong in a stylesheet parameter or the host's own DocumentCache (bounded).

- **The vocabulary pools end in a cliff, not a slow leak.** Past ~1.05 M distinct QNames the
  NamePool refuses every further new name, so the shared Processor is finished, not merely
  fat — and the error says exactly that, naming the occupancy, the ceiling and the remedy,
  because a message like "too many names" reads as a transient failure. The host gets it as a
  `DAXonApiException` with the code `SXLM0004` (up to 1.4 it was an internal exception type
  the host could not catch by type); `xsl:try` and `try/catch` do not catch it, as in Java.
  `NamePool.UserDefinedNameCount` reports the live occupancy, so a host can watch that axis
  instead of meeting it. Swapping in a fresh pool is NOT the remedy and
  is refused: fingerprints are baked into compiled patterns and into each mode's
  rule-chain index, so after a swap the same names get DIFFERENT fingerprints and every compiled
  template rule stops matching — silently, with empty output and no error. Build a new Processor.
  Anything that mints names or namespace URIs per message
  (a GUID in a namespace, data-derived element names) walks towards that wall; give such a
  workload a recycled process. Collation-URI `lang=` tags USED to have the same shape one level
  down — each distinct tag interned a `CultureInfo` (~1.1 KB) in a framework-wide table the
  engine cannot clear, and on Windows 10+ even made-up well-formed tags count, because the OS
  synthesizes a culture instead of rejecting them. Now only tags naming a culture
  the OS knows reach the framework; unknown tags collate as the invariant culture and cost
  nothing, so data-driven tags are safe.
- **Reusing an `Xslt30Transformer` no longer leaks, but is still unsupported.** It used to
  retain every document the transformer ever saw (the source document pool and the accumulator
  index were reset only in the constructor); they are now released when each run ends. The
  one-per-transformation rule above stands for the other reasons - `Load30()` costs
  microseconds, and a transformer holds the whole execution state.
- **A reused `XPathSelector` keeps nothing of its last evaluation.** A selector evaluates in one
  stack frame and one controller from one evaluation to the next. Until 1.4 what its local
  variables held stayed reachable until the next evaluation (a `let` of 2 million strings,
  200 MB), and the documents `doc()` loaded stayed in its pool for as long as the selector:
  three files of 150 MB held 450 MB, as they do in Saxon-HE 12.9. An evaluation now ends as a
  call ends - `Evaluate`, `EvaluateSingle`, `EffectiveBooleanValue`, `foreach`, and an iterator
  once it is done or disposed leave the locals empty and the pool without the documents it
  loaded - the document of the context item you set stays, so `doc()` of its URI is that node
  each time; an iterator abandoned half-way keeps its evaluation's values until the selector is
  evaluated again. A
  transformer, and an `XQueryEvaluator` called for functions, keep the global variables they
  computed for their next call, as Saxon's do: drop the object to free them.

## Multiple processes / batch-server instances

Every process (each service or batch-server instance) has its own `Processor`, caches and
compiled stylesheets - they share nothing and need nothing shared. Each instance pays its
own warmup (stylesheet compile, first parse). Multiple Processors INSIDE one process also
work but waste memory and invite cross-Processor object mixing - keep one per process.

## The first call in a process

.NET Framework compiles the engine's code to machine code (JIT) the first time each method
runs, so the first compile and the first transformation in a fresh process pay for it once.
Measured on .NET Framework 4.8 (a small stylesheet, 3 KB input):

| | new process | warm process |
|---|---|---|
| `new Processor()` | ~110 ms | - |
| first `Compile` of the stylesheet | ~1.7 s | ~6 ms |
| first transformation | ~0.5 s | ~1 ms |

About 2.3 s per process, once; every later call runs at full speed. Each service or batch-server
process pays it after every start or recycle. It is paid inside the time
limit: the first compile under its own `TransformTimeout`, the first run under another, so a
limit of a second or two stops the first call after a start with SXTO0001 where the same call a
moment later passes. Keep the limit above the cold start, or warm up before the first request
(measured on a slow 4-core box: the first compile of a small stylesheet 1.6-1.8 s, the next
ones 1-10 ms). What helps:

- **Keep the objects** of the lifecycle table above: one `Processor` per process and the
  compiled `XsltExecutable` cached. Recompiling per call does not repeat the JIT cost, but it
  does repeat the compile.
- **Warm up when the process starts**, where the host has a start hook: compile and run one
  tiny stylesheet once. That takes ~1.5 s itself and moves most of the cost out of the first
  real request: its compile then takes ~0.4 s and its run ~0.2 s (what remains is code only
  that stylesheet reaches).
- **NGen** (`ngen install OutSmart.DAXon.dll` from an elevated prompt, again after every
  update of the DLL) precompiles the engine: the whole first call drops to ~0.15 s. It works
  only where the host loads the DLL as a file through normal probing (an application's own
  folder). It does not help where the host loads assemblies in a way that never uses a native
  image - some application servers load the assemblies of their packages so - and a host that
  uses `Assembly.LoadFrom` does not get one either.
- **.NET 8+ hosts** can publish their application with ReadyToRun, the equivalent of NGen
  there.

## GC configuration

The engine is allocation-heavy on .NET Framework. In the host's app.config:

```xml
<runtime>
  <gcServer enabled="true"/>
</runtime>
```

On a single-threaded workload this is worth only ~1-3%; under concurrent load it is the
difference between scaling and not scaling. Measured with 8 threads on 8 cores, one
shared Processor and a 7.4 MB input: with workstation GC every allocation-heavy scenario
collapses to 2-4x aggregate throughput (scaling efficiency 0.20-0.45 - hundreds of STW
gen0/gen1 collections per second suspend all transform threads); with server GC the same
scenarios reach 0.9-1.25 efficiency, i.e. near-linear scaling. This is a host-config
switch, not an engine property - an IIS host typically runs server GC already, but a
console or Windows-service host defaults to workstation GC and MUST set it explicitly.

Oversubscription (16 threads on 8 cores) keeps aggregate throughput roughly flat vs 8
threads - requests queue without collapse; there is no benefit to running more transform
threads than cores.

## Crash safety: what the host must and must not handle

Wrap every transform call in ONE `try/catch (Exception)` on the thread that runs it — an
unhandled exception on any thread kills a .NET Framework process. Expected errors (bad
stylesheet, bad input, an input file that cannot be read - SXXP0003 since 1.4, a raw
IOException before, its message naming the reason (missing, in use, a directory, a path the file
system refuses by its form), from `DocumentCache.GetOrParseFile` as well -, an output file that
cannot be written - SXRD0004 since 1.4, with the reason; a file held by another process was a raw
IOException, and a name the file system refuses was .NET's own exception out of `SetOutputFile`
or `NewSerializer(file)`, where nothing is thrown for it any more; a destination that fails while
it is written - a full disk, a stream of the host's that throws an IOException - is SXRD0004 as
well, naming the destination and the reason (it was "Failure writing to " and no code); and a
name for an output file that reads as a URI of another scheme - `report:2024.xml`,
`http://host/out.xml` - is refused, where it used to be written under the local path of that
URI: `2024.xml`, or `out.xml` at the root of the drive -, type
errors, SXTO0001 timeout, size caps) arrive as
`DAXonApiException` with Java-parity error codes; anything else is an engine defect worth
reporting. Do NOT wrap engine internals in additional try/catch — the per-request boundary
is the whole contract.

**A null argument.** A public method given null where it needs a value throws
`ArgumentNullException` naming the parameter, before it does anything; up to 1.4.0 it was a
`NullReferenceException` from somewhere inside, or a value that held nothing and failed later
(`new XdmAtomicValue((string)null)`, `new QName(null, null, null)`). Where null means "none" it
is taken as before: a setter of a handler, a resolver or a listener clears it, a null system
identifier is none, `CallTemplate(null, ...)` calls `xsl:initial-template`, a null namespace is no
namespace. `new XdmValue(null)` is the empty sequence (up to 1.4.0 it held nothing). A null in
`CallFunction`'s argument array is refused like a null array. `ApplyTemplates((Stream)null, ...)` is XTDE0044,
no initial match selection, as in s9api.

**A number that is no value of an enum** - `(OccurrenceIndicator)999` - is refused the same way, with
`ArgumentOutOfRangeException`; up to 1.4.0 it was taken for the nearest thing and changed the behaviour
later (a variable declared with it required an empty sequence).

An error met while a result is read item by item - `Iterator()`, `foreach` over an
`XPathSelector` or an `XQueryEvaluator` - arrives as `DAXonApiUncheckedException`, the type Java
needed for what an iterator throws. Since 1.4 it is a `DAXonApiException`, so the one catch takes
it and `GetErrorCode()` reads its code; before, it was an exception apart, and its `Message` was
the cause's whole `ToString()`, stack trace included.

**An extension function's own exception.** `IExtensionFunction.Call` runs host code inside the
run. A `DAXonApiException` it throws is a dynamic error of the run: `xsl:try` catches it, with the
code of an `XPathException` the host built it on (since 1.4; before, always `saxon:XXXX9999`), and
uncaught it reaches the host as a `DAXonApiException`, the host's own cause in its chain. Any other
exception - a `NullReferenceException`, an `InvalidOperationException` - is the host's defect and
passes through the run unwrapped, past `xsl:try`, as Java-HE lets a `RuntimeException` through; the
per-request catch above is where it lands. It is the very object thrown, from wherever the call
was: until 1.4 one raised under a user function or a template rule came wrapped in an
`InvalidOperationException` "Internal error evaluating ..." (Saxon wraps there too). An extension
whose failure a stylesheet should be able to handle throws `DAXonApiException`. The same holds for
the comparer of a collation the host declared (`Processor.DeclareCollation`): up to 1.4.0 a sort
under it - `xsl:sort`, `fn:sort`, `order by`, `array:sort` - reported the comparer's exception as
"Non-comparable types found while sorting" (XTDE1030 / XPTY0004) or wrapped in .NET's
`InvalidOperationException` "Failed to compare two elements". An exception of a message handler
(`SetMessageHandler`, `SetMessageListener`) is ignored, as Saxon ignores it, except an
`OutOfMemoryException`, which went unnoticed as well up to 1.4.0.

Deep user recursion is guarded: non-tail recursive functions/templates that
exceed the executing thread's stack raise SXLM0001 ("Too many nested ... calls") exactly
like Java-HE, an error the host catches (`xsl:try` and `try/catch` do not, as in Java),
instead of an uncatchable StackOverflowException killing the process.
The guard adapts to the thread's stack size (bigger stack — deeper legal recursion; the
last 128 KB are reserved for the error path). Tail-recursive templates/functions are
optimized and never grow the stack. The same adaptive guard bounds every input-driven
recursion axis: pathologically deep JSON (`parse-json`/`json-doc` →
FOJS0001 "too deeply nested"), deeply nested dynamic regex patterns (compile → FORX0002,
match → SXRE0001), and deeply nested XPath/XQuery expressions (XPST0003) — all clean
errors on any thread size, where Java-HE crashes on the regex case. Since 1.4 so do the walks
over a deeply nested array or map that recursed with no guard - `map:find`, the type of a value
(asked of a sort key or a typed argument), flattening into content or a serializer,
`xsl:on-empty`, `xsl:where-populated` - which a deeply nested JSON value took to a
StackOverflowException: they are SXLM0001 now. A collection walked with `recurse=yes` keeps no
stack of the thread per directory since 1.5, where a deep enough directory tree exhausted it.
OutOfMemoryException
remains catchable but the process should be recycled after one; the input-size cap and
deadlines make it hard to reach. It reaches the host as itself: until 1.4 one thrown inside a
user function or a template rule arrived as an InvalidOperationException ("Internal error
evaluating ...").

**How much stack stays free** is `ProcessorOptions.StackSizeThreshold`: 128 KB by default, which
is also the least it may be (with 64 KB a real StackOverflowException got through on a 256 KB
thread). It does not make recursion deeper - only the stack of the thread does: run the call on
a thread you create with a larger one (`new Thread(work, 16 * 1024 * 1024)`; the engine creates
no threads of its own). Raise it when extension functions of yours need much stack themselves:
the engine can call them at the deepest level. What it costs, measured with a non-tail
recursive XQuery function on .NET Framework: on a 1 MB thread 2375 levels at 128 KB, 2034 at
256 KB, 1351 at 512 KB; on a 4 MB thread 10570 and 9546. A threshold that leaves the thread no
room makes every call fail with a coded error. On Windows; elsewhere the runtime's own check
(`RuntimeHelpers.EnsureSufficientExecutionStack`) stands in and the option has no effect.

**A deep recursion costs the whole process, not only its own call.** Every garbage collection walks
the stack of every thread, so while one call is tens of thousands of frames deep, all the others
slow down, and their own `TransformTimeout` can fire first. Measured with six light workers for
10 s beside one runaway recursive function: 14 330 runs alone; 4 710 with the function on a 1 MB
thread, 2 270 on 4 MB, 1 711 on 16 MB (p99 from 43 ms to 1.1 s). A thread of 16 MB is the remedy
for one deep input, not a default for calls that run side by side. `ProcessorOptions.MaxStackBytes`
(null by default: the whole thread) caps the stack a call may use whatever its thread has: a
recursion then stops with SXLM0001 as on a thread of that size.

**A long run of `xsl:variable` is deep, not long.** Each `xsl:variable` scopes everything after it
in its sequence constructor, so N of them in one template compile to an expression N levels deep,
and the compiler walks it recursively. Measured on .NET Framework: a 256 KB thread (the stack some
hosts and batch frameworks give their workers) compiles about 300 in a row and refuses 600 with
"Stylesheet is too deeply nested (insufficient stack on this thread)"; a 1 MB thread about 2500;
past 3000 the static-analysis limit refuses them on any stack (XPST0003). Compile time grows with
the square of the count, as in Saxon: 2000 take about 7 s. A generated stylesheet of that shape
belongs on a thread with a larger stack (above), or its variables in a map or in separate
templates. XQuery `let` clauses do not nest this way: 2000 compile on a 256 KB thread.

Earlier versions closed the remaining process-death and wrong-answer axes:

- **Every stack-driven abort is one foreign error type internally**, so no
  catch site can accidentally re-decorate it into something a stylesheet swallows; it
  surfaces as SXLM0001 (or the axis-specific codes above) and nothing else. Deep values,
  deep result trees, and deep regex *class bodies* — recursions whose depth is the
  input's, found by matrix rather than by hunt — all land on this path.
- **Catastrophic regex backtracking**: every backtracking step, at every
  quantifier nesting level, is charged against one shared per-attempt budget
  (`Feature<int>.REGEX_BACKTRACKING_LIMIT`, "Regex backtracking limit exceeded") AND the
  run's deadline — nested quantifiers like `((a)*)*` used to slip past both brakes.
  With the limit disabled the deadline alone still binds.
- **Recursive `fn:transform`**: a stylesheet that calls `fn:transform` on
  itself builds a fresh controller per level and no other guard sees the nesting; it now
  raises FOXT0003 ("fn:transform nesting is too deep"), which reaches the host, instead of
  an overflow that killed the process. The ceiling scales with the worker thread's stack — roughly 11
  open nested transforms on a 1 MB thread, ~59 on 4 MB; real stylesheets nest one or two.
- **Tree depth is enforced, not wrapped**: the tree model stores node depth in
  16 bits, and past 32 767 levels the old behavior was not a crash but a silently WRONG
  answer (`string(/)` returned empty; counts stayed correct, which is what made it easy
  to miss). Building deeper now raises SXLM0002 ("Tree depth limit exceeded") at the
  exact node that crosses the line, on every input path.

## When a run fails

A run that ends in an error — including an `SXTO0001` timeout — releases what it held,
deterministically, before the exception reaches the host:

- **The destination is closed.** A `Serializer` over `SetOutputFile` has its file handle
  closed the moment the failure surfaces: the file is immediately deletable and a retry
  can reopen it. (It used to stay open, locked and half-written until a finalizer ran.)
  Treat the file's CONTENT as void — it is whatever had been flushed when the run died.
- **`OnClose` listeners do NOT fire.** They signal a completed result; a failed run has
  none. Host cleanup that must run on both outcomes belongs in the host's own
  try/finally around the transform call, not in an `OnClose`.
- **Nothing per-run outlives the run.** The source document pool, accumulator indexes
  and attribute-set state are released when the run gives up the controller — on the
  failure path too — and HTTP responses opened for redirect hops or content-type probes
  are disconnected rather than left to finalization (an abandoned response holds a
  pooled socket, and the default connection limit is 2 per host).

A second guarantee sits one level deeper: the engine's XML-reader factory OWNS the input
it is given (`CloseInput`), and that ownership holds even when reader construction itself
throws — the XML-declaration peek reads the stream, and a deadline-guarded network stream
raises `SXTO0001` from that read. Before the fix the un-disposed stream was a pooled HTTP
connection with an unread body, and with the default connection limit of 2 per host, two
such failures parked the host's entire pool for that endpoint: the leak's cost landed on
the NEXT run as a stall, not on any memory graph.

**A failed compile now tells the host why.** The default reporter writes its diagnostics
through a `Logger` whose sink is `Console.Error` — a channel an IIS worker cannot read —
and the exception used to carry only the sentence "Errors were reported during stylesheet
compilation". The collected diagnostics now travel on the exception itself:
`DAXonApiException.GetErrorCode()`, `GetLineNumber()`, `GetColumnNumber()` and `GetSystemId()`
describe the first error, `Message` holds the text of up to 20 errors and says how many more there
were, `GetErrors()` returns those 20 and `GetErrorCount()` the true total. No reporter needs to be
installed for this;
`XsltCompiler.SetErrorList`/`SetErrorReporter` remain the opt-in for structured capture and
for warnings on a compile that succeeds.

These guarantees are pinned by two permanent tests: one fails the build if a failed run
leaves its output file locked or leaks OS handles; the other drives every failure SHAPE —
a timeout abort, a failed compile holding an engine-opened include, a run whose input dies
before the reader exists, and the network variant against a live local HTTP server
where the assertion is that the next healthy fetch does not stall.

**Compile diagnostics do not degrade on a reused Processor.** The "too many errors" budget
(1000 by default, `MaximumNumberOfErrors`) counts the errors of ONE compilation. In early versions
it was accumulated by a reporter shared across every compiler made from a Configuration
and never reset, so a long-lived host eventually got `Too many errors reported` - and an
early abort that truncated the diagnostics - for a stylesheet with two mistakes.
A compilation spans its packages: a package loaded through `xsl:use-package` compiles
inside the episode of the compile that asked for it - it neither resets the
budget already spent nor gets a fresh one of its own, the same way an imported query
module counts against the importing query.

## Verification

What you can run yourself is in [`tests/`](../tests/README.md): the W3C QT3 and XSLT 3.0 conformance
corpora (38 693 cases pass; the 17 known failures are XML 1.1 input documents), 499 cases whose expected
results come from the specifications, and a concurrency test that runs near-identical stylesheets on one
shared `Processor` from many threads and compares every output byte for byte with a fresh Processor's.

The claims of this document about limits, failure paths and memory are also held by robustness and soak
suites that are not part of this repository: stack guards on small and large threads, the deadline and
the memory limit on every entry point, regex backtracking, nested transformations and tree depth, the
release of files, handles and memory when a run fails, retention on a long-lived Processor (per run, per
parked thread, per distinct name), the exported surface staying reachable from reflective binders, and a
three-hour soak of one Processor - mixed workloads, failures, concurrent bursts, per-round compiles -
whose retained memory, private bytes and handle count stayed flat and whose output never drifted.
