# Tests

Test projects you can build and run against the engine in `src/`. None of them is part of the solution build;
build the one you want on its own. Each references `src/OutSmart.DAXon` as a project, so it tests the source in
this tree.

| project | what it checks | time |
|---|---|---|
| [`QT3Test`](QT3Test) | the W3C QT3 (XPath/XQuery 3.1) and XSLT 3.0 conformance corpora, against a fixed baseline of known failures | minutes |
| [`OutSmart.DAXon.SpecTests`](OutSmart.DAXon.SpecTests) | 499 cases whose expected results come from the XPath 3.1, XSLT 3.0 and F&O 3.1 specifications, not from the engine's own output | ~10 s |
| [`OutSmart.DAXon.TwinsTest`](OutSmart.DAXon.TwinsTest) | one shared `Processor` under concurrency: near-identical stylesheets run interleaved and from 16-32 threads, every output compared byte for byte with a fresh `Processor`'s | seconds |
| [`Smoke`](Smoke) | one transformation against a built DLL, as CI runs it | seconds |

Building the `net472` targets needs Windows with the .NET Framework 4.7.2 targeting pack (Visual Studio or the
Developer Pack); the `net10.0` targets need the .NET 10 SDK. Pick one target with `-f` when you lack the others.
The test executables are x64.

## SpecTests

```
dotnet build tests/OutSmart.DAXon.SpecTests -c Release
tests/OutSmart.DAXon.SpecTests/bin/Release/net472/OutSmart.DAXon.SpecTests.exe
```

The same runs under `net8.0/` and `net10.0/` (`dotnet OutSmart.DAXon.SpecTests.dll` on other systems).
It prints a summary per area and a `TOTAL` line, lists every failure as `[FAIL]`, `[WRONG-ERROR]`, `[ERROR]` or
`[TIMEOUT]` with the expected and actual result, and writes every case to `results.tsv` in the current directory.

- `--filter <text>`: only the cases whose id or area contains the text (`--filter rex-`, `--filter axes-001`).
- `--list`: list the cases instead of running them.
- `--out <file>`: write the results there instead of `results.tsv`.
- `SPEC_STACK=1`: add exception stacks to the report.

Each case has a 15 s budget. The exit code is 0 whenever the run completes, failures or not (99 means the
harness itself failed), so read the `TOTAL` line: all 499 pass on every target. About 50 cases expect a
dynamic error; the engine reports each to stderr as it would to any host, so `Error at ...` lines on the console
are not failures.

Cases live in `Cases/*.cs`, one class per area, each case naming the section of the specification it comes
from (`SpecRef`). Id prefixes: `axes ops seq str1 str2 rex num dt cast map arr hof node tmpl grp ser spec`.

## TwinsTest

```
dotnet build tests/OutSmart.DAXon.TwinsTest -c Release
tests/OutSmart.DAXon.TwinsTest/bin/Release/net472/OutSmart.DAXon.TwinsTest.exe
```

Prints `TWINS-OK` and exits 0, or prints each differing output as `TWINS-FAIL`, `TWINS-MT-FAIL` or
`TWINS-BURST-FAIL` (single thread, staggered threads, bursts) with the expected and actual text, and exits 1.
`TWINS_DUMP=<dir>` writes the stylesheets, the input and the reference outputs there.
