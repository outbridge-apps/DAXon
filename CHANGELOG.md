# Changelog

## 1.4.0 — 2026-10-09

1.4.0 adds .NET 8 and .NET 10 builds, per-call time and memory limits, and control over what a stylesheet
may reach outside the transformation. It also brings a long list of fixes from three reviews of the port
against the specifications and against Saxon-HE 12.9. Most fixes are invisible to a working host. Read
**Upgrading from 1.3** first: it lists every change an existing host can observe.

### Upgrading from 1.3

**Assembly and API**

- `AssemblyVersion` is `1.4.0.0`. The assembly is not strong-named, so a host built against 1.3 loads 1.4
  without a binding redirect.
- No public member was removed. A host compiled against 1.3.3 runs on 1.4 without recompiling.
- Deprecated with `[Obsolete]`, still working:
  - `Processor(bool)`: the edition flag has no effect;
  - the constructors that take limits;
  - `Processor(Configuration)`, `Processor(ResolvedResource)`;
  - `Processor.MaxInputBytes`, `Processor.DefaultMaxInputBytes`;
  - `XPathSelector.IIterator()`, `XQueryEvaluator.IIterator()`, `XdmNode.IAxisIterator(...)`;
  - `IPush.IDocument(bool)`;
  - the parameterless `XdmFunctionItem()`.

  Use `new Processor(new ProcessorOptions { ... })`, `Iterator()`, `AxisIterator(...)` and `Document(bool)`
  instead.
- `DAXonApiUncheckedException` now derives from `DAXonApiException`, so one `catch (DAXonApiException)` also
  catches errors raised while a lazy result is iterated. On recompiling, a `catch` of the derived type must
  come first (CS0160).

**Limits**

- **Memory:** every API call (a compile, a document build, a transformation, a query, an XPath evaluation)
  may hold at most `ProcessorOptions.MaxMemoryBytes`. The default is 500 MB (`Processor.DefaultMaxMemoryBytes`);
  `null` means no limit.
  - What the call keeps counts: its trees, strings, sequences, maps, arrays and sort buffers, plus the trees
    the host hands it. What it allocates and drops does not.
  - Over the limit the call stops with `SXLM0003`, which `xsl:try` does not catch.
  - The same value is the largest input accepted. 1.3.3 capped input at 150 MB by a separate setting.
- **Time:** `TransformTimeout` keeps its default of one minute, and it now applies to every call on its own.
  - Each evaluation of a reused `XPathSelector` and each `XQueryEvaluator.CallFunction` gets its own budget.
    Before, they lived on the first call's deadline and failed with `SXTO0001` a minute after it.
  - Compilation watches the clock in all its phases (1.3.3 only during constant folding). A huge stylesheet
    that compiled in minutes now stops with `SXTO0001`.
  - A compile and each run have a full budget each, counted from their own start.
- The cold start of a process (JIT, first use) is spent inside the limit of the first call.

**Errors**

- Parse errors from `Build`, `Parse`, `Compile` and `ApplyTemplates(Stream)` arrive as `DAXonApiException`
  with code `SXXP0003`. 1.3.3 threw a raw `XmlException`, which is now the inner exception's inner
  exception.
- A file that cannot be read gives `DAXonApiException` `SXXP0003` with the I/O cause, instead of
  `FileNotFoundException`, `IOException` or `UnauthorizedAccessException`. Messages name the cause:
  "I/O error reported by XML parser processing <uri>: <reason>".
- Time-limit messages name the work that stopped: "Compilation / Parsing / XPath evaluation / Query exceeded
  the time limit of Ns". The code stays `SXTO0001`.
- A host exception thrown from an `IExtensionFunction` reaches the host as the same object. A
  `DAXonApiException` it throws keeps its code, which `xsl:try` sees.
- `SetOutputFile` and `NewSerializer(file)` no longer throw themselves for a file that cannot be written. The
  error, `SXRD0004` with the cause, comes when the file is opened.
- `OutOfMemoryException` reaches the host as itself. 1.3.3 wrapped it in `InvalidOperationException`
  "Internal error evaluating ...".
- Some codes changed:
  - a time limit inside `parse-xml()`, `parse-xml-fragment()` or `load-xquery-module()` stays `SXTO0001`;
  - a module a policy denies is `XQST0059`;
  - `collection()` of a URI that cannot be read is `SXXP0003` (as Saxon).
- Many raw .NET exceptions (`NullReferenceException`, `InvalidCastException`, `IndexOutOfRangeException`,
  `FormatException`, ...) became errors with a standard code (see Fixed).

**Values and API results**

- `JsonBuilder` returns the characters, as `parse-json()` does: `"a\nb"` has a newline. 1.3.3 kept escapes as
  written: `C:\\dir` came back with two backslashes. `SetEscaped(true)` restores the old form. Characters XML 1.0
  cannot hold become U+FFFD.
- `XdmValue.ToString()` and `XdmNode.ToString()` return the serialized value instead of the class name.
- `new XdmAtomicValue(true)` reads as 1 through `LongValue`, `GetDoubleValue()` and `GetDecimalValue()`.
- `XdmValue.Matches`, `SequenceType.Matches` and `ItemType.Matches` answer for declared item types (they always
  answered false). `XdmMap.AsMap()` returns the dictionary. `new XdmValue()`, `new XdmMap()` and `new XdmArray()`
  are the empty sequence, map and array.
- Half a surrogate pair in a string from the host becomes U+FFFD (1.3.3 threw or lost the next character). A URI
  with half a pair is refused.
- Numbers are parsed and formatted the same under every culture. Under de-DE 1.3.3 read "-1.5" as -15; under
  ru-RU and fr-FR it read NaN; on .NET 8/10 some cultures printed their own minus sign.

**Language and output**

- A function value called with the wrong arity or cardinality is an error: `$f((1, 2))` where the parameter is
  `item()?` raises `XPTY0004`. This covers inline functions, `string#1`, functions passed to `for-each`, `sort`,
  `array:*`, and members of arrays and maps in type checks. 1.3.3 silently took the first item.
- `fn:transform` with a `post-process` value that is not a function raises `XPTY0004`; 1.3.3 ignored it.
- `fn:serialize` reads its parameter map by the table in F&O 3.1:
  - a value of the wrong type is `XPTY0004`;
  - an invalid value is `SEPM0016`;
  - `normalization-form`, `include-content-type`, `escape-uri-attributes`, `undeclare-prefixes`, `html-version`,
    `json-node-output-method` and `escape-solidus` take effect.

  In the element form, `indent="yes"` works, and no XML declaration is written unless asked for.
- Byte order mark:
  - `UTF-16LE` and `UTF-16BE` without `byte-order-mark` are written without one (as Saxon);
  - `byte-order-mark="yes"` writes exactly one;
  - `no` writes none.
- XML and XHTML output write U+FFFD for characters XML cannot hold (U+0000, U+FFFE, U+FFFF, C0 controls in
  XML 1.0). Before, it wrote an unreadable reference or the raw character. JSON escapes U+001F.
- `method="json"` with `indent="yes"` indents by 2 (as Saxon 12.9; 1.3.3 indented by 3).
- `format-date` and its relatives prefix `[Language: en]` when they fall back to English words. Arabic, Persian
  and Pashto cultures name Gregorian months, not those of another calendar.
- Collation `strength=primary` (and `ignore-modifiers=yes`) ignores only spaces, hyphens, zero-width and control
  characters, as Saxon-HE's Java collator does. 1.3.3 ignored all punctuation, so "C#" equalled "C". New: UCA
  `alternate=shifted|blanked` ignores punctuation and symbols.
- `system-property('xsl:vendor')` is "Outbridge" and `xsl:vendor-url` is <https://outbridge.app/>.

**Documents and resources**

- DTD validation validates:
  - `SetDTDValidation(true)` and the `DTD_VALIDATION` feature report each error and fail with `SXXP0003`
    (`FODC0002` from `doc()`);
  - a document without a DOCTYPE fails validation.
- Whitespace in elements declared element-only by a DTD is stripped on every path (`doc()`, `parse-xml()`,
  `ApplyTemplates(Stream)`, `Parse`), and only there. `Build` no longer strips undeclared elements, which lost
  data.
- The configuration's resource resolver is asked for a document's external DTD and external entities on every
  path.
- XInclude is refused explicitly: the feature throws `ArgumentException`, `SetXIncludeAware(true)` throws
  `NotSupportedException`. `Processor.SetCatalogFiles` throws `NotSupportedException` (it did nothing).
- A document declared `version="1.1"` is read as XML 1.0 on every input and in every encoding. XML 1.1 itself is
  not supported. `&#0;`, half surrogate pairs, U+FFFE and U+FFFF are refused while parsing.
- `unparsed-text()`, `unparsed-text-lines()`, `unparsed-text-available()` and `json-doc()` ask the run's resource
  resolver, then the configuration's, as `doc()` does and as Saxon 12 does, before reading the resource
  themselves, once.
- Entity expansion stops at 10,000,000 characters per document on every host. .NET applies this limit itself
  only to applications that target .NET Framework 4.5.2 or later.
- Windows device names (`CON`, `NUL`, `COM1`, `\\.\...`) and file stream names (`a.xml::$DATA`, `o.xml:w`) are
  refused as file names for reading and writing. `SetOutputFile("report:2024.xml")` is refused with `SXRD0004`;
  1.3.3 wrote `2024.xml` at the root of the drive.
- On Windows, `file:` URIs that differ only in case are one document, as in Java Saxon.
- HTTP fetches accept gzip and keep cookies across the redirects of one fetch.
- `Serializer.GetXmlWriter()` and `NewBuildingStreamWriter()` mean by a name what `XmlWriter.Create` means:
  - a null namespace is the one in scope;
  - an attribute in a namespace gets a prefix;
  - invalid names throw `ArgumentException`;
  - names, comments and processing instructions are checked always, not only under `SetCheckValues(true)`.

### New

- **Platforms:** one package with `lib/net472`, `lib/net8.0` and `lib/net10.0`. Legacy code pages
  (windows-1251, ...) resolve on .NET 8/10.
- **`ProcessorOptions`**, taken by `new Processor(options)`, which freezes them; `fn:transform` and `xsl:evaluate`
  inherit them:
  - `TransformTimeout` and `MaxMemoryBytes` (see above);
  - `StackSizeThreshold`: how much of the thread's stack a recursion must leave free before it stops with
    `SXLM0001`. 128 KB by default and at least; Windows only;
  - `AllowFileRead`, `AllowFileWrite`, `AllowNetwork`, `AllowEnvironmentVariables`;
  - `AllowedHosts` and `BlockedHosts` with `HostRule.Exact`, `Wildcard`, `Regex` and `IpRange` (CIDR). Under a
    policy, redirects are followed by the engine and every hop is checked;
  - `ReadFilter`, `WriteFilter` and `EnvironmentVariableFilter`, delegates that can only deny more;
  - `Configuration` and `ConfigurationFile`.

  Everything stays allowed by default. A denied resource fails the way a missing one does, with a message that
  names the missing permission.
- `collection()` returns text, JSON, binary and unknown members; reads zip, jar, docx and xlsx archives and XML
  catalogs; and supports `metadata=yes`. Archive members obey `ReadFilter` and the size limit.
- API:
  - `XPathSelector.Iterator()`, `XQueryEvaluator.Iterator()`, `XdmNode.AxisIterator(...)`;
  - `IPush.Document(bool)`; `Processor.NewPush` works;
  - `XdmValue.Matches(SequenceType)`, `XdmMap.AsMap()`;
  - `JsonBuilder.SetEscaped` / `IsEscaped`;
  - `Processor.Options`, `Processor.DefaultMaxMemoryBytes`;
  - `NamespaceUri.InternedCount`.
- `XdmSequenceIterator` works as an `IEnumerator`: `MoveNext()` always answered false.

### Fixed

**Security**

- Text that contained U+0000 switched output escaping off. This covered host strings (parameters, variables,
  extension function results) and `&#0;` in a document marked `version="1.1"`. Escaped input then came out as
  markup: XSS in HTML, injected attributes. U+0000 from data is no longer written (`\u0000` in JSON).
- A comment written through the XmlWriter faces could close itself and inject markup (`x --><b/>`).
- Entity expansion had no limit for hosts that target .NET Framework below 4.5.2. 10^8 characters passed, and
  10^10 ran past the time limit.

**Process stability:** these failures killed the process or hung it; they now stop with a coded error.

- Stack overflow from deeply nested arrays and maps (from 6000 levels of JSON), from expression chains of about
  2000 links on a 256 KB thread, from deep input in `base-uri()`, `document()`, `snapshot()` and copies, and from
  deep adaptive serialization. They now stop with `SXLM0001` or `XPST0003`.
- Runaway work that ignored `TransformTimeout`:
  - comparison sorts;
  - loops over in-memory values (`array:*`, `fold-*`, `filter`, `for-each`, `some`/`every`, `index-of`,
    `string-join`, `deep-equal`);
  - functions over one long string;
  - `xsl:key` indexes and accumulators;
  - deep copies; collections;
  - the whole of compilation.
- A regex hung on a quantified empty group.
- `doc('CON')` hung on .NET 8/10.
- A time limit fired inside the regex tables' static initializer and broke regex for the life of the process.
- A file stayed locked on Windows after an interrupted or failed run (`unparsed-text-lines()` stopped early).
- A race between closing an abandoned `unparsed-text-lines()` and its finalizer could crash the process.

**Memory**

- The process-wide table of namespace URIs kept every URI until the process ended.
- A Processor kept the documents of a failed run.
- A reused `XPathSelector` kept its last evaluation's variables and every document `doc()` loaded.
- A tree built after a large one reserved the large one's arrays.

**Correctness**

- `XdmNode.IAxisIterator` walked the wrong axis for parent, preceding, preceding-sibling, self and namespace.
- Calendar AM in `format-date`.
- `round(x, n)` beyond the int range.
- `xs:integer` of 2^63 as a double.
- `subsequence($s, 1e300)`.
- `reverse()` of a range.
- `ends-with` and `contains` under the HTML collation.
- UCA `numeric=yes` with non-ASCII digits.
- Under lt-LT or lv-LV, attribute values such as `stable="yes"` were rejected with `XTSE0020`.
- "+-1234567890123456" was accepted as an integer.
- Text after an XML 1.1 declaration was corrupted.
- The linked tree model works: attribute and descendant axes, processing instructions, comments,
  `copy-namespaces`, base URIs.
- `string-length` and `substring` counted a surrogate pair as two characters next to a comment in a
  stylesheet.
- `current-dateTime()` on .NET Framework is precise to 100 ns (it moved in 15.6 ms steps).
- `Processor.DistributionVersion` reported "1.0".

**Raw .NET exceptions that became coded errors**

- Serialization method names and user-defined output methods.
- `castable as xs:NMTOKENS` / `IDREFS` / `ENTITIES` on nodes, arrays, maps and functions.
- `xsl:number` `start-at` and `grouping-size` beyond int.
- `priority` beyond double.
- Untyped values passed to `xs:double` parameters ("1,000" read as 1000).
- `format-dateTime` `[ZN]` for years outside 1..9999.
- `xsl:element` and `xsl:attribute` in the xml and xmlns namespaces.
- Output to a locked file or a file with an invalid name.
- `fn:transform` serialization parameters and `post-process`.
- `collation-key` of a very long string.
- `ObtainPackage(name, null)`.
- `GetAssociatedStylesheet` lost the code of the error it met.

### Performance

Measured on .NET Framework 4.7.2 against 1.3.3.

- Compilation is up to 30% faster.
- XSLT 1.0 comparisons are 63% faster, higher-order functions 17%, positional template patterns 32%.
- Regex with escapes is up to 60% faster, with 19–29% less allocation on capturing groups.
- Numeric parsing of strings that are not numbers (`castable`, `number()`) is 5 to 90 times faster.
  `array:size` and growing a sequence are constant time.
- `snapshot()` of deep nodes is linear.
- `Build()` of a document without a DOCTYPE allocates 7–36% less. A large document after small ones allocates
  up to 54% less.
- A small transformation to a `TextWriter` allocates 12 KB instead of 77 KB.
- A small call of a reused `XPathSelector` or `XQueryEvaluator.CallFunction` costs no more than in 1.3.3
  (selector 1.03 µs vs 1.14 µs, allocating a quarter as much).
- `unparsed-text()` reads small files about 38% faster under the default limits.

## 1.3.3

Previous release.
