# Changelog

## 1.5.0 — 2026-11-13

1.5.0 brings the fixes of a further series of reviews of the port, against the specifications, against Saxon-HE 12.9
and against hostile input, made by Claude like the port itself.

**If your host runs stylesheets, queries or documents it does not control, upgrade.** In 1.4.x and earlier some of
them could crash the process past any `try`, or run past the time and memory limits; 1.5.0 stops each with a coded
error. Most other fixes are invisible to a working host. Read **Upgrading from 1.4** first.

### Upgrading from 1.4

Every change an existing host can observe.

**Assembly and API**

- `AssemblyVersion` is `1.5.0.0`. Exported engine members are gone since 1.4, so a host compiled against 1.4 that
  used them must recompile.
- Gone, reached through the Api's `Underlying*` properties: `XQueryExpression.Evaluate`, `EvaluateSingle`,
  `SetAllowDocumentProjection`, `IsDocumentProjectionAllowed`, `ExplainPathMap`; `Configuration.IsJITEnabled`,
  `MakeStreamingTransformer`, `MakeDocumentProjector`, the static `IsAssertionsEnabled`;
  `Controller.ApplyStreamingTemplates`, `GetStreamingReceiver`, `SetUseDocumentProjection`;
  `PipelineConfiguration.XPathContext`.
- Gone too, deeper in the engine: `Expressions.Parsing.PathMap`, its four nested types and `AddToPathMap` of every
  expression; `DeepEquals` of `IFunctionItem`, of maps and of arrays; `NamePool.AllocateClarkName`, `SuggestPrefix`;
  `NamespaceMap.ApplyDifferences`; `IRegularExpression.ReplaceWith`; `XMLEmitter.IsStarted`, `SetCharacterReferenceGenerator`;
  `UnicodeString.Tidy` and seven members of `UnicodeBuilder`; `CheckValidInJavascript`; `Transformation.Timer`;
  `IsJustInTimeCompilation`, `IsPreScan` and their setters; `PrepareForStreaming`, `InvertStreamableTemplates`;
  `Export(ExpressionPresenter)` of templates, attribute sets, modes and keys. `SequenceType.ExplainMismatch` takes a value
  and a type hierarchy. With the members named above, 155 lines of 1.4's member list and 5 types.
- `Lib.Logger.INFO`, `WARNING`, `ERROR`, `DISASTER` are now `SeverityInfo` and so on: a break in source. Binaries
  built against 1.4 still run, the constants being inlined.
- `Lib.FeatureData.featureList` is an `IReadOnlyList`, `Init()` is gone and its fields are readonly: a host
  built against 1.4 that reads them gets `MissingMethodException` or `MissingFieldException`.
- Thirteen exported statics are readonly (`ItemType.ANY_ITEM`, `ANY_FUNCTION`, `ParameterSet.EMPTY_PARAMETER_SET`
  and others), and five tables are internal.
- New: `XdmValue(IEnumerable<XdmItem>)`, `Size`, `IsEmpty`, `ItemAt`, `Subsequence`, `Append`, `MakeValue`, `MakeSequence`;
  `XdmAtomicValue(decimal)`, `(ulong)`, `(DateTime)`, `(DateTimeOffset)`, `(TimeSpan)`, `(Uri)`, `GetDateTime`, `GetDateTimeOffset`,
  `GetTimeSpan`; `MakeAtomicValue` also takes `decimal`, `sbyte`, `ushort`, `uint`, `ulong`, `DateTime`, `DateTimeOffset`,
  `TimeSpan`, `Uri`. `Value` of an `xs:dateTime` or `xs:date` stays its lexical form. `new XdmAtomicValue(5UL)` is an
  `xs:unsignedLong` when recompiled; built against 1.4 it bound to the `float` constructor and still does. `ItemAt` and
  `Size` index into the value; `MakeSequence` takes nesting of any depth and refuses a list that holds itself.
- New: `ProcessorOptions.MaxStackBytes` caps the stack a call may use, whatever its thread has (null, the default: the
  whole thread), and `ProcessorOptions.JsonNumbersAsDecimal` reads a JSON number without an exponent as `xs:integer` or
  `xs:decimal` (false, the default: `xs:double`, as the standard says). See [HOSTING](docs/HOSTING.md).
- `new XdmAtomicValue(text, ItemType.ANY_URI)` checks the text as Java's s9api does: `03:04:05`, `1a:b`, `%zz` are
  `DAXonApiException`, where 1.4 took them. The cast `xs:anyURI(...)` in a query still takes them, as in Java.

**Exceptions**

- `Xslt30Transformer.ApplyTemplates((Stream)null, ...)` is `DAXonApiException` XTDE0044, no initial match selection, as in s9api; a null
  `XdmValue` selection stays `ArgumentNullException`.
- `ArgumentNullException` for a null where the API wants a value, in about 145 members (`XdmNode.Attribute(null)`,
  `DocumentBuilder.SetTreeModel(null)`, `Processor.DeclareCollation(uri, null)`). 1.4 gave `NullReferenceException`,
  or went on silently.
- A relative base URI is `ArgumentException` at the setter: `XsltCompiler.BaseUri`, `DocumentBuilder.BaseUri`,
  `XPathCompiler.BaseURI`, `XQueryCompiler.SetBaseURI`.
- `new XdmValue(object)` and `XdmValue.Wrap` of an `XdmValue` are `ArgumentException`. 1.4 made an empty value.
- `new XdmFunctionItem()` is `NotSupportedException`. 1.4 made an item that held no function.
- `DAXonApiException` in place of a raw exception: a closed reader or stream (`SXXP0003`),
  `XdmAtomicValue.LongValue` or `GetDecimalValue` of a string that is no number, a lone surrogate in an expression.
- `DocumentBuilder.DocumentProjectionQuery` refuses a query when it is set, `NotSupportedException`, instead of
  failing every `Build`.
- `XPathSelector.EvaluateSingle` and `EffectiveBooleanValue` want a value only for the variables the expression reads.

**Files and URIs**

- A path is the name of a file: `%41` in a path no longer reads or writes the file with an `A`. Base URIs of such
  files are `...%2541...`. See [HOSTING](docs/HOSTING.md), "Paths, file names and encodings".
- `xsl:result-document href="x%2541.xml"` writes `x%41.xml`. 1.4 decoded it twice and wrote `xA.xml`.
- What the engine reads it opens sharing writing and deletion, so a file open for writing elsewhere can be read.
- A `TextReader` a resource resolver hands `unparsed-text` and its kin is closed after it is read when
  `PleaseCloseAfterUse` is set, as its `Stream` was. 1.4 left it open.
- `resolve-uri` and a relative `xml:base` against an opaque base (`urn:sheet`) are `FORG0002`, as in Java. 1.4 gave
  `urn:x.xml`. So is a base that is a path with backslashes, `C:\dir\`.
- `resolve-uri` encodes nothing and decodes nothing, as F&O says: `resolve-uri('g<h', 'http://a/b%20c/')` is
  `http://a/b%20c/g<h`, where 1.4 gave `g%3Ch`. Java refuses these characters with `FORG0002`; the spec does not.
- `resolve-uri` keeps a `%20` next to a space, each as it was given: `resolve-uri('a b%20c', 'http://x/')` is
  `http://x/a b%20c`, where 1.4 and Java give `a b c`. An absolute reference with a space comes back with the space, as F&O says;
  1.4 and Java give `%20`.
- `resolve-uri` of a URI past 65519 characters resolves, and past-the-root `../` go as RFC 3986 says. 1.4 gave `FORG0002`
  on .NET Framework and .NET 8. `doc()` of one is `FODC0002`, nothing being there, not `FODC0005`.
- A file past 260 characters reads on .NET Framework and in the .NET 4.5 mode: `doc`, `unparsed-text`, `-available`, `Build`.
  `collection()` of such a directory gave a raw exception there; it reads, or in the 4.5 mode is `FODC0002`.
- URIs a host resolver or result-document handler receives keep their escapes: `file:///C:/my%20sheets/x.xsl`, not
  `.../my sheets/...`.
- `resolve-uri` against a `jar:` or `file:////` base keeps the escapes of the reference and the four slashes, as Java:
  `resolve-uri('a%2Fb', 'file:////srv/x/c')` is `file:////srv/x/a%2Fb`, where 1.4 gave `file://srv/x/a/b`.
- `file://localhost/C:/x.xml` is the file `C:\x.xml` (RFC 8089, as Java). 1.4 looked for a share `C:` on a server localhost.
  `file://localhost/c$/x.xml` is the share `\\localhost\c$`, a network path: refused when `AllowNetwork` is false.
- `resolve-uri` of a path that begins with a drive takes it in place of the base's path, as RFC 3986 and Java:
  `resolve-uri('/C:/x.xml', 'file:///C:/a/b/')` is `file:///C:/x.xml`. 1.4 gave `file:///C:/C:/x.xml`, and `FORG0002` for
  `/C:` against `file:///`. `/x.xml` against that base stays `file:///C:/x.xml`, on the base's drive (Java and RFC 3986:
  `file:/x.xml`).
- `collection()` and `uri-collection()` with `recurse=yes` find what lies past 260 characters below an ordinary directory on .NET
  Framework, in the .NET 4.5 mode too. 1.4 left it out with no word. A directory named with a trailing dot or space (`dir.`)
  is listed as itself on every runtime; on .NET Framework 1.4 gave a raw `DirectoryNotFoundException` there.
- A directory or file of a collection that may not be read is `FODC0002`. 1.4 gave a raw `UnauthorizedAccessException`, whatever
  `on-error` said. A directory below the one named that cannot be listed is left out under `on-error=ignore` and `warning`
  (with a warning); the directory named fails the collection. A member file that cannot be read is left out under
  `on-error=ignore` and `warning`, as Java leaves out one that cannot be parsed; with no `on-error`, or `fail`, it fails.
- The walk of `recurse=yes` takes no stack of the thread, and each directory it lists keeps the time limit. In 1.4 a deep
  enough directory tree killed the process.
- An output file may be past 260 characters on .NET Framework, and its path may hold more than one `#`. 1.4 gave `SXRD0004`.

**Text**

- `encoding="UTF-16"` writes big-endian with the mark `FE FF`, as Java. 1.4 wrote `FF FE` and little-endian. A host
  that reads the bytes itself, or a consumer that takes only `FF FE`, asks for `UTF-16LE` and `byte-order-mark=yes`.
- Bytes labelled `UTF-16` with no mark are read as big-endian (RFC 2781, as Java). 1.4 read little-endian. A mark wins.
- `unparsed-text`, `unparsed-text-lines` and `json-doc` refuse bytes that are not text in the encoding, also after a
  mark and at the end of the file, `FOUT1200` or `FOUT1190`; `unparsed-text-available` says false. 1.4 gave U+FFFD
  after a mark, and on .NET Framework dropped an incomplete sequence at the end.
- Under `InvariantGlobalization` `normalize-unicode` and the `normalization-form` of a serializer refuse text past
  ASCII, `FOCH0003` and `SESU0011`. 1.4 returned it unnormalized.
- `byte-order-mark=yes` on the adaptive method with UTF-8 writes the mark only when a node opens the output.
- `unparsed-text` with an encoding decodes the bytes as they are, as F&O and Java do: `windows-1252` over `EF BB BF` is three
  characters, `UTF-8` over `FE FF` is `FOUT1190`. 1.4 let the mark win. The marks of `UTF-16` and `UTF-32` still tell their order.
- `encoding="UTF-32"` with `byte-order-mark=no` writes big-endian, which is what UTF-32 with no mark means. 1.4 wrote little-endian.
  `System.Xml` itself does not read such a document; `doc()` and `Build` do. Bytes labelled UTF-32 with no mark read as big-endian.
  `UTF-32LE` and `UTF-32BE` with nothing said have no mark, as `UTF-16LE` and `UTF-16BE`. 1.4 wrote one.
- A byte a code page leaves undefined is an error in `unparsed-text` and its kin, where 1.4 read a C1 control or a
  private-use character: `FOUT1190` or `FOUT1200`. In a document it reads as U+FFFD, as Java's parser reads it; 1.4 read
  the C1 control. See [HOSTING](docs/HOSTING.md), "Paths, file names and encodings".
- Java's names of UTF-16 and UTF-32 (`unicode`, `UTF16`, `UTF_16`, `UTF32`, `UTF_32`) read as `UTF-16` and `UTF-32` do: the
  mark tells the order, none is big-endian. To .NET `unicode` is little-endian only.
- `iso-8859-7` has the euro at 0xA4, `iso-8859-8` its two marks at 0xFD and 0xFE, `koi8-u` box drawing at 0xAE and 0xBE, on
  output and input. A document's declaration may say `cp1252`, `ms932`, `macroman`.
- The adaptive method writes one `?` for a character the encoding cannot hold. 1.4 wrote two for a supplementary character,
  and a look-alike for others (`A` for U+0100).
- `indent` adds no more spaces past 256 levels of nesting: 400 nested elements are 199 918 characters, where 1.4 wrote
  481 199. An atomic item at the top is written under `cdata-section-elements`; 1.4 lost it, and
  `serialize(('a','b'), map{'method':'xml','cdata-section-elements':QName('urn:p','z')})` was empty. The HTML method writes
  U+007F as `&#x7f;`, and for HTML 4.01 refuses it, `SERE0014`. 1.4 dropped the character.
- `normalization-form` with spaces in it is `SESU0011`, as any other that is no form. 1.4 gave `SEPM0016`.
- A document labelled `US-ASCII` (or any name of it) with a byte past 7 bits is `FODC0002`, as in Java. 1.4 read each such byte as `?`.
- `windows-874`, `TIS-620` and `ISO-8859-11` are three tables, as in Java: an undefined byte is an error in `unparsed-text`, TIS-620
  has nothing at 0x80-0x9F, ISO-8859-11 has the C1 controls there. 1.4 read all three as Windows' 874, with C1 and private-use
  characters for undefined bytes. Java's names `tis620`, `x-windows-874`, `ms874`, `x-iso-8859-11` are known.
- `UTF-7` is refused on .NET Framework as on .NET 8 and in Java, under any of its names: `FOUT1190`, `SESU0007`, `FODC0002`
  for a document, an external entity or an external DTD that declares it, and `XQST0087` for a query module on every runtime.
  1.4 decoded such a document or entity there, and markup hidden in its text became markup; .NET 8 and 10 gave a raw
  `NotSupportedException` for the module.
- The encoding a document declares counts however far from the start the name stands. 1.4 looked at the first 100 bytes and
  past them left the document to System.Xml's own tables. A declaration not closed within 64 KB is `FODC0002`.
- A UTF-8 mark before a declaration of another encoding: the declaration wins, as in Java and on .NET 8. On .NET Framework 1.4
  refused the document when the name was one System.Xml did not know (`cp1252`) and read it with its own table when it knew it.
- An XQuery module declared `us-ascii` reads a byte past 7 bits as U+FFFD, as Java. 1.4 read `?`.
- An output encoding is known by its name only: `windows1252` is `SESU0007` whatever a Processor serialized before. 1.4, as
  Saxon in Java, took it, and wrote it in the declaration, once `windows-1252` had been asked for. The names of the built-in
  sets (`utf_8`, `iso_8859_1`) are taken as before.

**Answers**

- An untyped value that casts to NaN compares with nothing: `xs:untypedAtomic('NaN') > 1` is false, and
  `xs:untypedAtomic('-0') = 0e0` is true, as XPath 3.1 says. 1.4 and Java use a total order here.
- A seeded `random-number-generator()?permute` gives another order than 1.4. The order was already not Java's.
- `upper-case` and `lower-case` map 128 more codepoints as Java does (ı, ſ, U+0345, Kelvin, Ohm, Angstrom, ẞ, Greek
  with ypogegrammeni), on every runtime.
- A `group by` after a `group by` binds the latest variable of each name. Saxon 12.9 bound the oldest and gave wrong groups.
- Numerals of more than 800 digits, and decimal text past 15 digits, are correctly rounded on .NET Framework. So is a short
  numeral with an exponent: `xs:double('4.91e-6')` is `0.00000491`, where 1.4 gave `0.0000049100000000000004` there.
- `idiv` of doubles is exact: `1e23 idiv 1` is 99999999999999991611392, where 1.4 gave 100000000000000000000000.
  `format-integer` of a number past 2^64 writes it (1.4 wrote it modulo 2^64), and a sum of durations past the range of a
  64-bit count is the sum (100 000 times `P99999999D` was `P1316034912D`). On .NET Framework `math:sin`, `cos` and `tan` of
  a large argument are what they are on .NET 8: `math:sin(18512.40297e27)` was 1.85e31.
- An exact quotient of two integers is an `xs:integer` only when both are positive, as in Java: `-6 div 3` and `avg(-1)`
  are `xs:decimal`, and `-6 div 3 to 5` is `XPTY0004`. 1.4 gave an integer for every exact quotient, `-2, -1 ... 5` here.
- `xs:date('2020-01-01+14:30')` is `FORG0001`: a timezone ends at 14:00. `resolve-uri('g', 'file:/x/y')` is `file:/x/g`,
  where 1.4 gave `FORG0002`. `xs:NCName('n') cast as xs:QName` is the name; 1.4 gave a raw `NotSupportedException`.
- A regex flag that is none of `s`, `m`, `i`, `x`, `q` is `FORX0001`, as F&O says: `replace('abc', 'b', 'x', '!')`,
  `tokenize('a;b', ';', ';')`. 1.4, as Java, passed over `!` and `;`.
- The default language is `en`, not the culture of the thread that built the Processor. Nor does a UCA or `saxon:` collation
  with no `lang` follow that thread: under sv-SE `sort(('a','ä','z','å'), '...UCA')` gave `a z å ä` in 1.4 and gives the root
  order now, `z` last. A host that wants a language's order says `lang=sv`.
- `format-date` and its kin in another language than English: a picture that asks for what the engine has in English only -
  an ordinal `[D1o]`, a number in words `[Dw]`, an era `[E]` - is written in English throughout after `[Language: en]`.
  1.4 mixed the two: `[Language: en]Donnerstag, 29th Februar 2024`. `[FNn]` and `[MNn]` begin with a capital in every
  language, `Samedi 5 Octobre 2024` for `fr` where 1.4 gave `samedi 5 octobre 2024`, and `[MN]` for `tr` is `ARALIK`, where
  1.4 gave `ARALiK`.
- `math:pow` and `math:exp10` are correctly rounded on every runtime: `math:pow(10e0, 23) eq 1e23`. One answer in some
  hundreds changes by one unit in the last place. A power that is not a multiple of 1/8 costs about 0.3 microseconds, some
  four times the platform's pow.
- `format-dateTime` and its kin take the offset of a named place from the IANA database in the engine, release 2024b, not the
  Windows registry: Asia/Kathmandu in 1980 is +05:30, Africa/Casablanca in 2026 is +01:00, Antarctica/Troll is known.
  Summer time and the name of a zone follow a change of the standard offset alone (Turkey 2016, Lithuania 2000).
- `contains` and the other substring functions take a UCA collation with `caseFirst`. 1.4 gave `FOCH0004`.
- A UCA `caseFirst` orders, and changes no equality (UTS #35): at strength primary or secondary case does not count,
  `deep-equal('a', 'A', '...UCA?strength=primary;caseFirst=upper')` is true, `sort` keeps `a A b B`; at tertiary it decides only
  between strings the collation finds unequal, and `distinct-values`, `group by` and `collation-key` find equal what `compare`
  does. On .NET Framework, whose NLS finds strings equal that differ in the number of letters (`ß` `ss`, `æ` `ae`, `ﬁ` `fi`),
  strings equal but for case are ordered by NLS's key, its case weights put in the order asked for: the first letter that
  differs in case decides (`Aä` before `aÄ` upper first), as on .NET 8 and 10. 1.4 made case significant at every
  strength. The `saxon:` collation URI's `case-order` stays as in Java.
- A UCA collation of `strength=identical` or `quaternary` orders as the collation does and by code point only between what
  it finds equal, as in Java: `sort(('b','A','a','B'), '...UCA?strength=identical')` is `a A b B`. 1.4 ordered by code point
  alone, `A B a b`.
- A string never equals the `xs:base64Binary` that `collation-key` gives for it:
  `distinct-values(('abc', collation-key('abc', $uca)), $uca)` is 2 and `deep-equal` of the two is false. 1.4 said 1 and true.
- A recursion that runs out of stack inside a regex is `SXLM0001`, as any other, on every runtime. .NET 8 and 10 gave `SXRE0001`.
- An argument of the wrong kind has the code F&O gives it. A node whose content does not cast is `FORG0001`: `abs(text{'t'})`,
  `1 to <a>t</a>`, `hours-from-dateTime(<a>t</a>)`; 1.4 said `XPTY0004`, as Java does for most functions. A map where an
  atomic value is wanted is `FOTY0013` in every function; 1.4 said `XPTY0004` in some (`timezone-from-dateTime(map{'a':1})`).
  A `catch` that names the code must name the new one.
- A type error says what is wrong with the value ("The supplied value is of type xs:string", "The node has the wrong name"), as
  Saxon does. A static type error on an element constructor with no content gave a raw `IndexOutOfRangeException`; it is `XPTY0004`.
- `xquery version "4.0"` is `SXED0001`; the regex backtracking limit is `SXRE0002`; a `?select=` glob Java refuses
  (`*.{xml,txt}`) is `FODC0004`; a destination's `OnClose` listeners run once per run, and not for a query that failed.

**Limits**

- `MaxMemoryBytes` admits fewer inputs because it counts more: 62 000 JSON object keys under 8 MB (1.4: 500 000; they
  retain 120 bytes a key).
- `format-integer`, `xsl:number`, `string()` and products of huge integers stop at the time limit, within about a second on later
  runtimes. `random-number-generator()?permute` counts against the memory limit.
- New names count against `MaxMemoryBytes` in the call that makes them up, about 250 bytes each: a query that made a
  million attribute names ran under 100 MB in 1.4 and is `SXLM0003` now. So do the attributes of a start tag not yet written,
  about 200 bytes each and their values: 300 000 on one element are past 100 MB.
- What a type error explains of a nested array or map goes four levels down. In 1.4 the message of a deeply nested value
  could grow past every limit.
- A time limit met while a document is read, in an external entity too, is `SXTO0001`: `doc-available` no longer says false for
  it, and `on-error=ignore` or `warning` of a collection no longer leaves the member out and goes on. 1.4 did not stop there at all.
- The key of a long string under a UCA collation counts against `MaxMemoryBytes`, and is made under the time limit: on .NET
  Framework it is several times the string. Under `caseFirst` it carries the case of every character there: 100 000 characters
  make a key of 400 KB in lower case and 700 KB in upper case (1.4: 300 KB), and sorting or grouping under such a collation
  allocates 17 to 50% more than 1.4.
- `normalize-unicode` and the `normalization-form` of a serializer look at the time limit inside a string of 4 096 characters
  or more. In 1.4 a long run of combining marks went to the platform in one call that no limit reached.
- `normalize-unicode` counts what it builds against `MaxMemoryBytes`: an NFKC that makes a string many times longer stops
  with `SXLM0003`, where 1.4 built it past every limit.
- A pattern in parentheses nested past what the thread's stack holds is `XPST0003` ("Expression is too deeply nested").
  In 1.4 the stack overflowed and the process died.

**Speed**

- A FLWOR `group by` under a collation makes each key once and hashes all of it: 100 000 strings group in 1.3 s under
  `numeric=yes` (1.4: 12 s) and in half the time of 1.4 under a UCA collation.
- A string literal of doubled quotes (`""`, `''`) is read in linear time. In 1.4 a long one could take minutes to tokenize,
  outside the time limit.
- `distinct-values` and every other hash of a UCA collation's keys takes the whole key: 20 000 URLs are distinct in
  0.9 s, 1.4 took 27 s (the hash took the first 64 bytes, and keys of strings that share a start collided).
- An `xs:base64Binary` or `xs:hexBinary` hashes every byte. 1.4 shifted a bit a byte over the first 64, and values that differ
  in a few bytes collided: `distinct-values` of 300 000 values of 4 bytes takes 1.1 s (1.4: 26 s on .NET Framework, 15 s on
  .NET 10), of 20 000 that share 70 bytes 1.5 s (1.4: 62 s). `map:keys` and `map:for-each` of a map with binary keys go in
  another order than in 1.4, where keys that count up came out counting up; the order was never promised.
- `normalize-unicode` of a string of 4 096 characters or more looks at the time limit between stretches of it. Text that is
  in the form already comes back as the string it was. Against `String.Normalize` of the whole string, text in its form
  takes a quarter to a half of the time on .NET Framework and 1.1 to 1.5 times it on .NET 10, text that changes 1.0 to
  1.45 times and 1.3 to 1.75. A query that puts text in NFC to NFC takes 1.4's time or less on .NET Framework, up to
  1.25 times it on .NET 8 and .NET 10. There the stretches are copied out for the platform: a call allocates one copy of
  the text more than in 1.4, up to twice 1.4's where the text changes. A script with no space for thousands of
  characters (Cyrillic, Thai, Arabic run together) takes 0.4 to 0.9 times 1.4's time on .NET Framework and 0.7 to 1.35
  times on .NET 8 and .NET 10; NFKC of a long run of compatibility forms, a thirtieth of it on .NET Framework. See
  [HOSTING](docs/HOSTING.md), "Transformation time limit".

## 1.4.0 — 2026-10-09

1.4.0 adds .NET 8 and .NET 10 builds, per-call time and memory limits, and control over what a stylesheet
may reach outside the transformation. It also brings a long list of fixes from three reviews of the port
against the specifications and against Saxon-HE 12.9, made by Claude like the port itself. Most fixes are
invisible to a working host. Read
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
