# OutSmart DAXon — a native C# port of Saxon-HE 12.9

[![build](https://github.com/outbridge-apps/DAXon/actions/workflows/build.yml/badge.svg)](https://github.com/outbridge-apps/DAXon/actions/workflows/build.yml)

**OutSmart DAXon** is a pure-managed XSLT 3.0 / XPath 3.1 / XQuery 3.1 engine for the classic
**.NET Framework 4.7.2+ and .NET 8+** — a complete C# port of **Saxon-HE 12.9** (© Saxonica, MPL 2.0).
No Java, no IKVM at runtime: one self-contained assembly with no third-party dependencies,
for hosts that are stuck on the old Framework where current XSLT 3.0 engines are not an option.

> OutSmart DAXon is an independent derivative work. It is **not affiliated with, endorsed by, or
> supported by Saxonica Limited**. "Saxon" and "Saxonica" are trademarks of Saxonica Limited and are
> used here only descriptively, to identify the upstream project this port derives from.

**Base version**: the port derives from the Saxon-HE **12.9** Java source release, published by
Saxonica at <https://github.com/Saxonica/Saxon-HE> (see also <https://www.saxonica.com/>).

## Build

Prebuilt assembly: [latest release](https://github.com/outbridge-apps/DAXon/releases/latest).

Or build it yourself:

```
dotnet build OutSmart.DAXon.sln -c Release
```

builds `OutSmart.DAXon.dll` — the whole engine, one self-contained assembly
(`src/OutSmart.DAXon/`).

Note: `.gitattributes` pins `* -text` (no CRLF conversion) — the tree contains byte-sensitive
embedded data files. If your clone predates that file, run
`git config core.autocrlf false; git checkout -- .` once.

## Usage

```csharp
using System.IO;
using OutSmart.DAXon.Api;

// One Processor per process — reusable and thread-safe. Time limit, input cap and what a
// stylesheet may reach: new Processor(new ProcessorOptions { ... }) — see Security below.
var proc = new Processor();

// Compile once, reuse for any number of transformations (thread-safe).
XsltExecutable exe = proc.NewXsltCompiler()
    .Compile(new StringReader(xsltText), "urn:stylesheet");

// Parse the input document (or Build(filePath) for a file).
XdmNode input = proc.NewDocumentBuilder()
    .Build(new StringReader(inputXml), "urn:input");

// One transformer per transformation — cheap to create, never share between calls.
Xslt30Transformer tr = exe.Load30();
var output = new StringWriter();
tr.SetGlobalContextItem(input, true);
tr.ApplyTemplates(input, proc.NewSerializer(output));
string result = output.ToString();
```

Expected failures (bad stylesheet, bad input, resource limit hit) arrive as
`DAXonApiException` with standard XSLT/XPath error codes — one `try/catch` around the
transform call is the whole error-handling contract.

## Security

A stylesheet or query can reach outside the transformation through the engine's built-in
resolvers:

- read local files and fetch URLs — `doc`, `document`, `xsl:source-document`, `unparsed-text`,
  `json-doc`, `collection`, `xsl:include` / `xsl:import`, `fn:transform`, `load-xquery-module`,
  and external entities / DTDs in any XML it parses;
- read environment variables — `environment-variable`, `available-environment-variables`,
  `system-property`;
- write files — `xsl:result-document`.

**Everything is allowed by default**, as in every earlier version. When stylesheets come from
people you do not fully trust, restrict it when you create the `Processor`:

```csharp
var proc = new Processor(new ProcessorOptions
{
    TransformTimeout = TimeSpan.FromSeconds(30),
    MaxInputBytes = 50L * 1024 * 1024,
    AllowFileRead = false,
    AllowFileWrite = false,
    AllowEnvironmentVariables = false,
    AllowedHosts = { HostRule.Exact("api.contoso.com"), HostRule.Wildcard("*.contoso.net") },   // *. = subdomains only
    BlockedHosts = { HostRule.IpRange("10.0.0.0/8") },
});
```

- A denied read fails the way a missing resource fails: the function's usual error code
  (`FODC0002`, `FOUT1170`, `XTSE0165`, ...) with a message naming the missing permission, before
  any file or network access. `doc-available` / `unparsed-text-available` return `false`, a
  denied environment variable reads as unset, a denied `xsl:result-document` raises `SXRD0004`.
- Network hosts: `BlockedHosts` is checked first, then — if it is not empty — `AllowedHosts`.
  Rules are `Exact`, `Wildcard`, `Regex` (anchored, case-insensitive, with a match timeout) and
  `IpRange` (CIDR); hosts compare in canonical form (punycode, lower case, IPv4-mapped addresses
  as IPv4). Under a policy, HTTP redirects are followed by the engine and every hop is checked.
  Prefer host-name rules for allow-lists: an IP range is checked against the addresses resolved
  before the request, and the HTTP stack resolves the name again when it connects.
- Your own code is trusted and not gated: a resolver or `xsl:result-document` handler you
  install, and calls with an explicit path such as `DocumentBuilder.Build(file)`.
  `MaxInputBytes` caps input you pass in directly too (`DocumentBuilder`, `XsltCompiler`, ...).
- The `Processor` takes the options: after `new Processor(options)` a setter throws, and
  `fn:transform` / `xsl:evaluate` inherit them. For decisions of your own, set `ReadFilter`,
  `WriteFilter` or `EnvironmentVariableFilter`: they are asked after the flags and host rules,
  can only deny more, and the error names the filter.
- Entity expansion in any XML the engine parses stops at 10,000,000 characters per document,
  under every policy and on every host. .NET sets this limit itself only for applications that
  target .NET Framework 4.5.2 or later; the engine sets it for the rest too.

## Status

The port is verified against the full W3C QT3 (XPath/XQuery 3.1) + XSLT 3.0 test corpora and
matches Java Saxon-HE verdict-for-verdict, except 17 cases requiring XML 1.1 input documents,
which the .NET `XmlReader` cannot parse. Hostile inputs cannot kill the process: transformation
and compile-time deadlines, input-size caps and adaptive stack guards turn deep recursion / deep
JSON / deep regex nesting into catchable coded errors; `ProcessorOptions.StackSizeThreshold`
sets how much of the thread's stack stays free for that (128 KB by default, and at least).

The conformance runner lives in [`tests/QT3Test`](tests/QT3Test); it downloads the W3C
corpora at pinned revisions, so the result above can be reproduced locally.

## How this port was produced

The translation from the Saxon-HE 12.9 Java sources, and the subsequent refactoring,
hardening and performance work, were carried out with AI assistance — Anthropic's Claude
(Opus 4.8 and Fable 5) — under human direction and review.

Nothing was taken on the model's word. Every change to the engine had to pass:

- the **W3C QT3 (XPath/XQuery 3.1) and XSLT 3.0 conformance corpora** — 38 554 cases
  passing against a fixed, documented set of 17 known failures (XML 1.1 input documents,
  which the .NET `XmlReader` cannot parse);
- **byte-identity gates** — selected transform outputs compared byte-for-byte against
  Java Saxon-HE running the same inputs;
- a spec-derived suite of 489 cases, a multi-threaded equality battery on one shared
  `Processor`, and a set of robustness probes (deadlines, caches, stack guards).

## License & attribution

Licensed under the **Mozilla Public License, Version 2.0** — see [`LICENSE`](LICENSE).

This is a derivative work of **Saxon-HE 12.9**, © Saxonica Limited, which is itself distributed under
the MPL 2.0 (source: <https://github.com/Saxonica/Saxon-HE>). Per-file Saxonica copyright notices are
retained as required by the license. Files modified in the course of this port carry the same MPL 2.0
terms; the modified source is published here in full. The engine has no third-party dependencies.

OutSmart DAXon is maintained by Outbridge (<https://outbridge.app/>) and is independent of Saxonica.
