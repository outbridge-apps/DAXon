////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Schema;

namespace OutSmart.DAXon.Events
{
    /// <summary>
    /// Pumps a <see cref="System.Xml.XmlReader"/> (a .NET pull parser) directly into a Saxon
    /// <see cref="IReceiver"/>, building the tree without the intermediate SAX round-trip
    /// (DotNetXmlReader -&gt; ReceivingContentHandler). It reproduces the Receiver-event semantics of
    /// <see cref="ReceivingContentHandler"/>: immutable namespace-map maintenance, attribute-map
    /// construction (all attributes untyped, matching a non-validating parser), name caching, character
    /// buffering with <c>StringTool.Compress</c>, and the <see cref="ISourceLocator"/>/<c>levelInEntity</c>
    /// contract the tree builders (TinyBuilder / LinkedTreeBuilder) rely on for base-URI and entity tracking.
    /// Unlike the SAX path it carries no SAX types: its locator reads the XmlReader directly.
    /// </summary>
    internal sealed class XmlReaderToReceiver
    {

        // The head of an input read ahead for its XML declaration: room for one in any encoding the parser tells
        // by itself, UTF-32 included.
        private const int XmlDeclPeekBytes = 256;
        // .NET's own default since 4.5.2, set explicitly: in .NET Framework's legacy XML mode (an IIS app whose httpRuntime
        // targetFramework is below 4.5.2) the reader has no limit: a billion-laughs DTD expands until time or memory runs out.
        private const long MaxEntityCharacters = 10_000_000;
        // JAXP disable/enable-output-escaping PI targets (javax.xml.transform.Result.PI_*).
        private const string PI_DISABLE_OUTPUT_ESCAPING = "javax.xml.transform.disable-output-escaping";
        private const string PI_ENABLE_OUTPUT_ESCAPING = "javax.xml.transform.enable-output-escaping";
        private readonly IReceiver receiver;
        private readonly PipelineConfiguration pipe;
        private readonly XmlReader reader;

        // Exact-type check: the fused text lane replicates TinyBuilder's Characters/MakeTextNode
        // behavior, which a subclass could override — anything else keeps the generic route.
        private readonly Trees.Tiny.TinyBuilder directBuilder;

        // Text values are chunked straight into the accumulation buffer, skipping the per-node
        // reader.Value string. Chunked reads advance the reader's reported line position past the
        // node, so a line-numbered parse keeps the Value route to preserve text-node locators.
        private readonly bool chunkValues;

        // The reader does not check character references (an XML 1.1 label has that switched off, a host's own reader
        // may never do it), so what no XML has is refused here: U+0000 above all, which the serializer reads as its own mark.
        private bool vetCharacters;

        private XmlPullLocation localLocator;
        private readonly Stack<string> entityBaseStack = new Stack<string>();   // reader.BaseURI per open element
        private ILocation lastTextNodeLocator;
        private readonly bool lineNumbering;
        private readonly bool allowDisableOutputEscaping;
        private bool escapingDisabled;

        // DTD-declared attribute types: key "elementQName\tattrQName" -> ID/IDREF/IDREFS/NMTOKEN/NMTOKENS/
        // ENTITY/ENTITIES. .NET's XmlReader reports every attribute as CDATA even with DtdProcessing.Parse,
        // so ID typing (needed by fn:id/fn:idref) is recovered by parsing the DOCTYPE internal subset's
        // ATTLIST declarations. Null until a DOCTYPE with an internal subset is seen.
        private Dictionary<string, string> dtdAttTypes;

        // Buffer accumulating character data until the next markup event, mirroring ReceivingContentHandler.
        private char[] buffer = new char[512];
        private int charsUsed;

        // false once an end tag has been seen; controls whitespace-compression of the next text node.
        private bool afterStartTag = true;

        // Set on the DocumentType event (always before the root element). Without a DTD no entity can
        // exist, so BaseURI is constant and the per-element entity-boundary tracking (BaseURI read +
        // stack + compare) is skipped entirely.
        private bool hasDtd;

        // Element-nesting depth (0 in the prolog/epilog). XmlReader reports boundary whitespace that a
        // Java SAX parser never would, so it is suppressed at depth 0.
        private int elementDepth;

        // For each open element, whether the DTD declares it: only there can whitespace be ignorable.
        private bool[] declaredAt;

        // Stack of in-scope namespace maps; the bottom entry is the empty document-level map.
        private readonly Stack<NamespaceMap> namespaceStack = new Stack<NamespaceMap>();

        // Name cache keyed on the reader's ATOMIZED name parts: XmlReader.NameTable returns one string
        // instance per lexical name for the whole parse, so a hit is three pointer compares. A flat
        // 2-way table instead of a Dictionary because net472 never devirtualizes IEqualityComparer —
        // a dictionary lookup paid two interface calls plus two identity hashes per element (~9% of a
        // full parse). A non-atomized string (or a slot conflict) can only cause a miss that creates a
        // duplicate but equivalent INodeName — never a wrong hit.
        private readonly NameEntry[] nameCache = new NameEntry[256];

        private struct NameEntry
        {
            public string Local;
            public string Prefix;
            public string Uri;
            public INodeName Name;
        }

        public XmlReaderToReceiver(XmlReader reader, IReceiver receiver)
        {
            this.reader = reader;
            this.receiver = receiver;
            this.pipe = receiver.GetPipelineConfiguration();
            Configuration config = pipe.GetConfiguration();
            this.lineNumbering = pipe.GetParseOptions().IsLineNumbering();
            this.allowDisableOutputEscaping = config.GetConfigurationProperty(Feature<bool>.USE_PI_DISABLE_OUTPUT_ESCAPING);
            this.directBuilder = receiver.GetType() == typeof(Trees.Tiny.TinyBuilder) ? (Trees.Tiny.TinyBuilder)receiver : null;
            this.chunkValues = reader.CanReadValueChunk && !lineNumbering;
        }

        /// <summary>
        /// Parse the whole document, sending events to the receiver.
        /// </summary>
        public static void Send(XmlReader reader, IReceiver receiver)
        {
            new XmlReaderToReceiver(reader, receiver).Parse();
        }

        /// <summary>
        /// Build a <see cref="System.Xml.XmlReader"/> with the settings used by the (non-validating, no
        /// external-fetch) input pipeline — identical to DotNetXmlReader.CreateReader so parsing is byte-equivalent.
        /// </summary>
        public static XmlReader CreateXmlReader(TextReader charStream, Stream byteStream, string systemId)
        {
            return CreateXmlReader(charStream, byteStream, systemId, null);
        }

        /// <summary>
        /// As above, but with an explicit <see cref="System.Xml.XmlResolver"/> for external entities / DTD
        /// subsets (e.g. one backed by Saxon's ResourceResolver). Pass null for no external fetch.
        /// </summary>
        public static XmlReader CreateXmlReader(TextReader charStream, Stream byteStream, string systemId, XmlResolver resolver)
        {
            return CreateXmlReader(charStream, byteStream, systemId, resolver, DtdUse.None);
        }

        // What the parser does with a document's DTD besides reading its entities and attribute defaults.
        internal enum DtdUse
        {
            None,
            // Tells the whitespace of element-only content apart, for the tree to leave it out.
            Whitespace,
            // Reports every validity error as well: the parse fails at its end if there was one.
            Validate,
            // As Validate, the errors being warnings the parse survives.
            ValidateLax
        }

        // The whitespace a DTD makes ignorable stays out of the tree unless the parse keeps all whitespace, as with
        // Saxon's SAX handler.
        internal static bool LeavesOutIgnorable(ParseOptions options)
        {
            return !(options?.SpaceStrippingRule is NoElementsSpaceStrippingRule);
        }

        // The use of its DTD a parse under these options asks of the reader.
        internal static DtdUse DtdUseFor(ParseOptions options)
        {
            int validation = options == null ? Validation.SKIP : options.DTDValidationMode;
            return validation == Validation.STRICT ? DtdUse.Validate
                : validation == Validation.LAX ? DtdUse.ValidateLax
                : LeavesOutIgnorable(options) ? DtdUse.Whitespace : DtdUse.None;
        }

        // The validity errors of the readers made here that read a DTD (see DtdEvents).
        private static readonly ConditionalWeakTable<XmlReader, DtdEvents> DtdEventsOf = new ConditionalWeakTable<XmlReader, DtdEvents>();

        // config: the configuration whose resource policy gates the default resolver's file reads
        // (external DTD subsets and entities); null for the engine's own embedded resources.
        // inputInEntity: characters of the input itself that arrive as an external entity (parse-xml-fragment),
        // which the reader would otherwise count against the expansion limit.
        // reporter: receives the validity errors of a validating parse; the configuration's when null.
        // mayBeXml11: false when the caller has seen that the characters it passes are not labelled XML 1.1 (see
        // Xml11Label), which spares them the read-ahead.
        public static XmlReader CreateXmlReader(TextReader charStream, Stream byteStream, string systemId, XmlResolver resolver, DtdUse dtd, Configuration config = null, long inputInEntity = 0, IErrorReporter reporter = null, bool mayBeXml11 = true)
        {
            XmlResolver entities = resolver ?? new FileOnlyXmlResolver(config, charStream == null && byteStream == null ? systemId : null);
            var settings = new XmlReaderSettings
            {
                // File-relative DTD/external-entity fetch by default (Java SAX parity); an internal subset
                // is processed for entity expansion. When a resolver is supplied, external entities/DTD
                // resolve through it instead.
                DtdProcessing = DtdProcessing.Parse,
                MaxCharactersFromEntities = MaxEntityCharacters + inputInEntity,
                XmlResolver = entities,
                ValidationType = ValidationType.None,
                IgnoreComments = false,
                IgnoreProcessingInstructions = false,
                IgnoreWhitespace = false,
                CheckCharacters = true,
                CloseInput = true,
                ConformanceLevel = ConformanceLevel.Document,
            };

            // Ownership note: CloseInput=true above means this factory OWNS the supplied stream/reader
            // - the returned XmlReader's Dispose closes it, on the success path and the mid-parse-error
            // path alike. The same ownership must hold when construction itself throws, or the input
            // (engine-opened for doc()/includes; wrapped in a deadline guard for http) leaks to the
            // finalizer holding its file handle or pooled socket. The declaration peek is a real throw
            // site: a guarded network stream raises SXTO0001 from Read when the run's deadline expires.

            // The head of every input is read first: for an XML 1.1 label, which the parser refuses (see Xml11Label), and
            // for a DOCTYPE - without one the reader need not validate to tell a DTD's whitespace, and keeps its fast lane.
            bool mayHaveDoctype = true;
            string baseUri = systemId ?? string.Empty;
            var use = new DtdSetup { Dtd = dtd, SystemId = baseUri, Reporter = reporter, Config = config };
            if (charStream != null)
            {
                try
                {
                    if (dtd == DtdUse.Whitespace || mayBeXml11)
                    {
                        charStream = PeekProlog(charStream, settings, dtd == DtdUse.Whitespace, out mayHaveDoctype);
                        EntitiesOf(settings, entities);
                    }

                    return Tracked(XmlReader.Create(charStream, UseDtd(settings, mayHaveDoctype, ref use), baseUri), use.Events);
                }
                catch
                {
                    charStream.Dispose();
                    throw;
                }
            }

            if (byteStream != null)
            {
                try
                {
                    byteStream = PeekHead(byteStream, settings, dtd == DtdUse.Whitespace, out mayHaveDoctype);
                    EntitiesOf(settings, entities);
                    return Tracked(XmlReader.Create(byteStream, UseDtd(settings, mayHaveDoctype, ref use), baseUri), use.Events);
                }
                catch
                {
                    // byteStream is either the original or the PrefixedStream wrapping it, whose
                    // Dispose cascades to the original - correct at every point of the sequence.
                    byteStream.Dispose();
                    throw;
                }
            }

            if (!string.IsNullOrEmpty(systemId))
            {
                // Opened here the way XmlReader.Create(systemId) opens it, for its head to be read first.
                Uri uri = entities.ResolveUri(null, systemId);
                Stream principal = (Stream)entities.GetEntity(uri, string.Empty, typeof(Stream));
                if (principal == null)
                {
                    // Nothing serves it: the parser's own failure for that, in its own words.
                    settings.XmlResolver = new ServesNothing();
                    return XmlReader.Create(systemId, settings);
                }

                try
                {
                    principal = PeekHead(principal, settings, dtd == DtdUse.Whitespace, out mayHaveDoctype);
                    EntitiesOf(settings, entities);
                    return Tracked(XmlReader.Create(principal, UseDtd(settings, mayHaveDoctype, ref use), uri.ToString()), use.Events);
                }
                catch
                {
                    principal.Dispose();
                    throw;
                }
            }

            throw new XPathException("ActiveStreamSource supplies neither a stream nor a system identifier");
        }

        // A document labelled XML 1.1 (the peek has switched the check of character references off for it) may pull
        // in a DTD and entities labelled 1.1 themselves; one labelled 1.0 may not, and the parser goes on refusing them.
        private static void EntitiesOf(XmlReaderSettings settings, XmlResolver entities)
        {
            if (!settings.CheckCharacters)
            {
                settings.XmlResolver = new Xml11Entities(entities);
            }
        }

        // Serves what the resolver serves, the XML 1.1 label of its text declaration read as the document's own was.
        private sealed class Xml11Entities : XmlResolver
        {
            private readonly XmlResolver served;

            public Xml11Entities(XmlResolver served)
            {
                this.served = served;
            }

            public override System.Net.ICredentials Credentials
            {
                set { served.Credentials = value; }
            }

            public override Uri ResolveUri(Uri baseUri, string relativeUri)
            {
                return served.ResolveUri(baseUri, relativeUri);
            }

            public override bool SupportsType(Uri absoluteUri, System.Type type)
            {
                return served.SupportsType(absoluteUri, type);
            }

            public override object GetEntity(Uri absoluteUri, string role, System.Type ofObjectToReturn)
            {
                object entity = served.GetEntity(absoluteUri, role, ofObjectToReturn);
                try
                {
                    return entity is Stream bytes ? PeekHead(bytes, new XmlReaderSettings(), false, out _)
                        : entity is TextReader characters ? PeekProlog(characters, new XmlReaderSettings(), false, out _)
                        : entity;
                }
                catch
                {
                    (entity as IDisposable)?.Dispose();
                    throw;
                }
            }
        }

        // Has the parser say that an input given by system id cannot be resolved.
        private sealed class ServesNothing : XmlResolver
        {
            public override System.Net.ICredentials Credentials
            {
                set { }
            }

            public override object GetEntity(Uri absoluteUri, string role, System.Type ofObjectToReturn)
            {
                return null;
            }
        }

        // What CreateXmlReader was asked about the DTD, and the events of the reader it then made.
        private struct DtdSetup
        {
            public DtdUse Dtd;
            public string SystemId;
            public IErrorReporter Reporter;
            public Configuration Config;
            public DtdEvents Events;
        }

        // Sets the reader up for the use it makes of a DTD: none at all when the parse only tells whitespace apart
        // and the document has no DOCTYPE.
        private static XmlReaderSettings UseDtd(XmlReaderSettings settings, bool mayHaveDoctype, ref DtdSetup use)
        {
            if (use.Dtd != DtdUse.None && (mayHaveDoctype || use.Dtd != DtdUse.Whitespace))
            {
                // ValidationType.DTD has .NET classify element-content whitespace (see Parse()). A document that
                // is well-formed but not valid must not abort there, so the validity errors go to the handler.
                use.Events = new DtdEvents(use.Dtd, use.SystemId, use.Dtd == DtdUse.Whitespace ? null : use.Reporter ?? use.Config?.MakeErrorReporter());
                settings.ValidationType = ValidationType.DTD;
                settings.ValidationEventHandler += use.Events.OnEvent;
            }

            return settings;
        }

        private static XmlReader Tracked(XmlReader reader, DtdEvents events)
        {
            if (events != null)
            {
                DtdEventsOf.Add(reader, events);
            }

            return reader;
        }

        // A prolog longer than this is not read through: the reader then tells whitespace apart whether or not a
        // DOCTYPE follows, which is right either way and only slower.
        private const int MaxPrologPeek = 64 * 1024;

        // Reads the head of the stream: for the label of a document that says it is XML 1.1, which it takes off,
        // and for whether the prolog has a DOCTYPE. What was read is served again.
        private static Stream PeekHead(Stream input, XmlReaderSettings settings, bool findDoctype, out bool mayHaveDoctype)
        {
            // XmlReader refills its internal buffer in small chunks, so an unbuffered input
            // (File.OpenRead's 4KB default, raw network streams) pays a syscall per refill —
            // measured ~15ms on a 7.5MB file. 64KB stays under the LOH threshold; a seekable
            // stream caps the buffer at its length so small documents don't pay for it.
            if (!(input is MemoryStream))
            {
                int bufSize = 64 * 1024;
                if (input.CanSeek)
                {
                    bufSize = (int)Math.Min(bufSize, Math.Max(XmlDeclPeekBytes, input.Length));
                }

                input = new BufferedStream(input, bufSize);
            }

            long start = input.CanSeek ? input.Position : -1;
            byte[] head = new byte[XmlDeclPeekBytes];
            int n = 0, r;
            while (n < XmlDeclPeekBytes && (r = input.Read(head, n, XmlDeclPeekBytes - n)) > 0)
            {
                n += r;
            }

            ByteUnits units = UnitsOf(head, n);
            int digit = Xml11Label(units, true);
            if (digit >= 0)
            {
                settings.CheckCharacters = false;
                // '1.1' -> '1.0' is length-preserving, so the byte offsets the parser sees are unchanged
                head[units.LowByte(digit)] = (byte)'0';
            }

            mayHaveDoctype = true;
            while (findDoctype)
            {
                Doctype found = FindDoctype(head, n, n < head.Length);
                if (found != Doctype.NeedsMore || head.Length >= MaxPrologPeek)
                {
                    mayHaveDoctype = found != Doctype.Absent;
                    break;
                }

                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
                Array.Resize(ref head, head.Length * 16);
                while (n < head.Length && (r = input.Read(head, n, head.Length - n)) > 0)
                {
                    n += r;
                }
            }

            if (start >= 0 && digit < 0)
            {
                // Nothing was rewritten and the stream can go back: the parser reads it from where it stood, and
                // sizes its buffers by its length - some 10 KB less for a small document.
                input.Position = start;
                return input;
            }

            return new PrefixedStream(head, n, input);
        }

        // Whether a document held as text may have a DOCTYPE: a caller that holds it can ask for the plain reader
        // outright, and spare a small document the read-ahead.
        internal static bool MayHaveDoctype(string xml)
        {
            return FindDoctype(new StringUnits(xml), true) != Doctype.Absent;
        }

        // Whether a document held as text says it is XML 1.1: one that does not is spared the read-ahead.
        internal static bool IsLabelledXml11(string xml)
        {
            return Xml11Label(new StringUnits(xml), true) >= 0;
        }

        // As PeekHead, for an input that arrives as characters.
        private static TextReader PeekProlog(TextReader input, XmlReaderSettings settings, bool findDoctype, out bool mayHaveDoctype)
        {
            char[] head = new char[XmlDeclPeekBytes];
            int n = 0;
            bool ended = false;
            int label = LabelNeedsMore;
            mayHaveDoctype = true;
            while (true)
            {
                int r = n < head.Length ? input.Read(head, n, head.Length - n) : 0;
                if (r > 0)
                {
                    n += r;
                }
                else if (n < head.Length)
                {
                    ended = true;
                }

                if (label == LabelNeedsMore)
                {
                    // A declaration that does not end within the first head is not looked at further.
                    label = Xml11Label(new CharUnits(head, n), ended || n == head.Length);
                    if (label >= 0)
                    {
                        settings.CheckCharacters = false;
                        head[label] = '0';
                    }
                }

                if (findDoctype)
                {
                    Doctype found = FindDoctype(new CharUnits(head, n), ended);
                    if (found != Doctype.NeedsMore || (n == head.Length && n >= MaxPrologPeek))
                    {
                        mayHaveDoctype = found != Doctype.Absent;
                        findDoctype = false;
                    }
                }

                if (label != LabelNeedsMore && !findDoctype)
                {
                    break;
                }

                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
                if (n == head.Length)
                {
                    Array.Resize(ref head, n * 16);
                }
            }

            return new PrefixedTextReader(head, n, input);
        }

        private enum Doctype
        {
            Unknown,     // the prolog does not show it: an encoding not read here, or not XML at all
            Present,
            Absent,
            NeedsMore    // the prolog goes on beyond what was read
        }

        // The code units of a prolog, whatever they arrive as.
        private interface IUnits
        {
            int Count { get; }

            int this[int index] { get; }
        }

        private readonly struct CharUnits : IUnits
        {
            private readonly char[] chars;
            private readonly int count;

            public CharUnits(char[] chars, int count)
            {
                this.chars = chars;
                this.count = count;
            }

            public int Count => count;

            public int this[int index] => chars[index];
        }

        private readonly struct StringUnits : IUnits
        {
            private readonly string text;

            public StringUnits(string text)
            {
                this.text = text;
            }

            public int Count => text.Length;

            public int this[int index] => text[index];
        }

        // Bytes of an encoding that keeps ASCII (size 1), of UTF-16 (size 2) or of UTF-32 (size 4).
        private readonly struct ByteUnits : IUnits
        {
            private readonly byte[] bytes;
            private readonly int start;
            private readonly int count;
            private readonly int size;
            private readonly bool bigEndian;

            public ByteUnits(byte[] bytes, int start, int end, int size, bool bigEndian)
            {
                this.bytes = bytes;
                this.start = start;
                this.count = end > start ? (end - start) / size : 0;
                this.size = size;
                this.bigEndian = bigEndian;
            }

            public int Count => count;

            public int this[int index]
            {
                get
                {
                    if (size == 1)
                    {
                        return bytes[start + index];
                    }

                    int at = start + size * index;
                    if (size == 2)
                    {
                        return bigEndian ? (bytes[at] << 8) | bytes[at + 1] : (bytes[at + 1] << 8) | bytes[at];
                    }

                    return bigEndian
                        ? (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3]
                        : (bytes[at + 3] << 24) | (bytes[at + 2] << 16) | (bytes[at + 1] << 8) | bytes[at];
                }
            }

            // Where the low-order byte of a unit lies.
            public int LowByte(int index)
            {
                return start + size * index + (bigEndian ? size - 1 : 0);
            }
        }

        // The code units of a document's head, told as the parser tells them: UTF-16 and UTF-32 by a byte order mark or
        // a first '<', anything else read as an encoding that keeps ASCII. head has room for four bytes whatever n is.
        private static ByteUnits UnitsOf(byte[] head, int n)
        {
            int b0 = head[0], b1 = head[1], b2 = head[2], b3 = head[3];
            if ((b0 == 0xFF && b1 == 0xFE) || (b0 == 0x3C && b1 == 0))
            {
                // Little-endian: UTF-32 begins as UTF-16 does and goes on with two zero bytes.
                return b2 == 0 && b3 == 0
                    ? new ByteUnits(head, b0 == 0xFF ? 4 : 0, n, 4, false)
                    : new ByteUnits(head, b0 == 0xFF ? 2 : 0, n, 2, false);
            }

            if (b0 == 0 && b1 == 0 && ((b2 == 0xFE && b3 == 0xFF) || (b2 == 0 && b3 == 0x3C)))
            {
                return new ByteUnits(head, b2 == 0xFE ? 4 : 0, n, 4, true);
            }

            if ((b0 == 0xFE && b1 == 0xFF) || (b0 == 0 && b1 == 0x3C))
            {
                return new ByteUnits(head, b0 == 0xFE ? 2 : 0, n, 2, true);
            }

            return new ByteUnits(head, b0 == 0xEF && b1 == 0xBB && b2 == 0xBF ? 3 : 0, n, 1, false);
        }

        // complete: there is nothing after these bytes.
        private static Doctype FindDoctype(byte[] head, int n, bool complete)
        {
            if (n < 4)
            {
                return complete ? Doctype.Unknown : Doctype.NeedsMore;
            }

            return FindDoctype(UnitsOf(head, n), complete);
        }

        // Walks the prolog - XML declaration, comments, processing instructions, whitespace - up to the DOCTYPE or
        // the document element. Anything else, and any control character on the way, is Unknown.
        private static Doctype FindDoctype<T>(T units, bool complete)
            where T : struct, IUnits
        {
            Doctype cut = complete ? Doctype.Unknown : Doctype.NeedsMore;
            int n = units.Count;
            int i = 0;
            while (true)
            {
                while (i < n && (units[i] == ' ' || units[i] == '\t' || units[i] == '\r' || units[i] == '\n' || (i == 0 && units[i] == 0xFEFF)))
                {
                    i++;
                }

                if (i + 1 >= n)
                {
                    return cut;
                }

                if (units[i] != '<')
                {
                    return Doctype.Unknown;
                }

                int c = units[i + 1];
                string end;
                if (c == '?')
                {
                    end = "?>";
                    i += 2;
                }
                else if (c == '!')
                {
                    if (i + 3 >= n)
                    {
                        return cut;
                    }

                    if (units[i + 2] != '-' || units[i + 3] != '-')
                    {
                        const string doctype = "DOCTYPE";
                        for (int k = 0; k < doctype.Length; k++)
                        {
                            if (i + 2 + k >= n)
                            {
                                return cut;
                            }

                            if (units[i + 2 + k] != doctype[k])
                            {
                                return Doctype.Unknown;
                            }
                        }

                        return Doctype.Present;
                    }

                    end = "-->";
                    i += 4;
                }
                else
                {
                    return c == '_' || c == ':' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c >= 0x80 ? Doctype.Absent : Doctype.Unknown;
                }

                // To the end of the comment or processing instruction.
                while (true)
                {
                    if (i + end.Length > n)
                    {
                        return cut;
                    }

                    int u = units[i];
                    if (u < ' ' && u != '\t' && u != '\r' && u != '\n')
                    {
                        return Doctype.Unknown;
                    }

                    if (u == end[0] && units[i + 1] == end[1] && (end.Length == 2 || units[i + 2] == end[2]))
                    {
                        i += end.Length;
                        break;
                    }

                    i++;
                }
            }
        }

        // The units end before the XML declaration tells its version.
        private const int LabelNeedsMore = -2;

        // .NET's parser refuses a document labelled XML 1.1. It is read as the 1.0 document it nearly always is: the last
        // digit of the label - found here, else -1 - becomes 0 in place, and the C0 references 1.1 allows are let through.
        private static int Xml11Label<T>(T units, bool complete)
            where T : struct, IUnits
        {
            // ' ' is white space, '_' white space that may be absent: a declaration and no other processing
            // instruction, with the version first, as the grammar has it.
            const string upToNumber = "<?xml _version_=_";
            int cut = complete ? -1 : LabelNeedsMore;
            int n = units.Count;
            int i = n > 0 && units[0] == 0xFEFF ? 1 : 0;
            foreach (char expected in upToNumber)
            {
                if (expected == '_')
                {
                    while (i < n && IsSpace(units[i]))
                    {
                        i++;
                    }

                    continue;
                }

                if (i >= n)
                {
                    return cut;
                }

                int unit = units[i++];
                if (expected == ' ' ? !IsSpace(unit) : unit != expected)
                {
                    return -1;
                }
            }

            if (i + 5 > n)
            {
                return cut;
            }

            int quote = units[i];
            return (quote == '"' || quote == '\'') && units[i + 1] == '1' && units[i + 2] == '.' && units[i + 3] == '1' && units[i + 4] == quote
                ? i + 3
                : -1;
        }

        private static bool IsSpace(int unit)
        {
            return unit == ' ' || unit == '\t' || unit == '\r' || unit == '\n';
        }

        public void Parse()
        {
            localLocator = new XmlPullLocation(reader);
            lastTextNodeLocator = localLocator;
            StartDocument();
            // When the reader validates against a DTD, .NET reports whitespace in element-only content as
            // XmlNodeType.Whitespace and whitespace in mixed content as SignificantWhitespace — the signal
            // needed to drop ignorable whitespace (number-4501). Without validation every inter-element
            // whitespace is Whitespace, so this stays off and such nodes are preserved. A parse that keeps
            // all whitespace keeps this too, as Saxon's SAX handler does.
            XmlReaderSettings settings = reader.Settings;
            vetCharacters = settings == null || !settings.CheckCharacters;
            bool dtdWhitespaceClassification = false;
            DtdEvents dtd = null;
            if (settings != null && settings.ValidationType == ValidationType.DTD)
            {
                DtdEventsOf.TryGetValue(reader, out dtd);
                dtdWhitespaceClassification = LeavesOutIgnorable(pipe.GetParseOptions());
                declaredAt = new bool[16];
            }

            // Cooperative deadline: a doc()/document() call mid-run parses here with no other
            // check site, so a large document must not outrun the transformation limit. Called
            // per node event - the active token throttles clock sampling itself.
            while (true)
            {
                dtd?.NextNode();
                if (!reader.Read())
                {
                    break;
                }

                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();

                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        // Asked before the attributes are walked: the name is the element's while the reader is on it.
                        bool declared = dtd == null || !dtd.IsUndeclared(reader);
                        StartElement();

                        // An empty element (<x/>) opens and closes in one node and raises no separate
                        // EndElement, so synthesize the close and leave the nesting depth unchanged.
                        if (reader.IsEmptyElement)
                        {
                            EndElement();
                        }
                        else
                        {
                            elementDepth++;
                            if (dtdWhitespaceClassification)
                            {
                                if (elementDepth >= declaredAt.Length)
                                {
                                    Array.Resize(ref declaredAt, elementDepth * 2);
                                }

                                declaredAt[elementDepth] = declared;
                            }
                        }

                        break;
                    case XmlNodeType.EndElement:
                        EndElement();
                        elementDepth--;
                        break;
                    case XmlNodeType.SignificantWhitespace:
                        // Whitespace .NET knows to be significant (mixed content, or xml:space="preserve"):
                        // always retained. Prolog/epilog (depth 0) is still outside the data model.
                        if (elementDepth == 0)
                        {
                            break;
                        }

                        goto case XmlNodeType.Text;
                    case XmlNodeType.Whitespace:
                        // Prolog/epilog whitespace is not part of the XPath data model; a Java SAX parser
                        // never reports it. XmlReader does, at depth 0 -- suppress it.
                        if (elementDepth == 0)
                        {
                            break;
                        }

                        // Element-content (ignorable) whitespace: when validating against a DTD, .NET reports
                        // element-only-content whitespace as Whitespace (mixed content is SignificantWhitespace
                        // above). Drop it — Java's SAX ignorableWhitespace() likewise never enters the XSLT/XDM
                        // source tree (number-4501). Gated on the reader validating against a DTD, so a
                        // DTD-less document (every inter-element node is Whitespace) preserves it; and on the
                        // DTD declaring the element, as .NET reports Whitespace in one it does not describe too.
                        if (hasDtd && dtdWhitespaceClassification && declaredAt[elementDepth])
                        {
                            break;
                        }

                        goto case XmlNodeType.Text;
                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                        AppendReaderValue();
                        break;
                    case XmlNodeType.ProcessingInstruction:
                        ProcessingInstruction(reader.Name, reader.Value);
                        break;
                    case XmlNodeType.Comment:
                        Comment(reader.Value);
                        break;
                    case XmlNodeType.DocumentType:
                        // Nothing to emit into the tree, but harvest ATTLIST declarations from the internal
                        // subset so ID/IDREF attributes can be typed below (fn:id / fn:idref), and NDATA
                        // entity declarations for fn:unparsed-entity-uri / -public-id.
                        hasDtd = true;
                        ParseDtdAttTypes(reader.Value);
                        ParseDtdUnparsedEntities(reader.Value);
                        // .NET surfaces only the internal subset as reader.Value. When the DOCTYPE names an
                        // external DTD (SYSTEM), fetch it and harvest its ATTLIST declarations too — the FOTS
                        // id/idref and xsl:number id() tests declare ID attributes in an external .dtd
                        // (number-4501, id-035). Best-effort: a missing/complex DTD leaves ID typing as-is.
                        ParseExternalDtd(reader.GetAttribute("SYSTEM"), reader.GetAttribute("PUBLIC"));
                        break;

                        // XmlDeclaration, expanded EntityReference: nothing to emit.
                }
            }

            EndDocument();
            dtd?.Finish(hasDtd);
        }

        private void StartDocument()
        {
            charsUsed = 0;
            NamespaceMap empty = NamespaceMap.EmptyMap();
            namespaceStack.Push(empty);
            receiver.SetPipelineConfiguration(pipe);
            string systemId = localLocator.GetSystemId();
            if (systemId != null)
            {
                receiver.SetSystemId(systemId);
            }

            receiver.Open();
            receiver.StartDocument(ReceiverOption.NONE);
        }

        private void EndDocument()
        {
            Flush(true);
            receiver.EndDocument();
            receiver.Close();
        }

        // Harvest ID-family attribute types from a DOCTYPE internal subset's ATTLIST declarations. .NET's
        // XmlReader does not expose DTD attribute types, so fn:id/fn:idref would otherwise never see an ID.
        // Only the internal subset is available here (an external subset is parsed for entities but its text
        // is not surfaced); that covers the FOTS id/idref tests, which declare the DTD inline.
        private void ParseDtdAttTypes(string internalSubset)
        {
            if (string.IsNullOrEmpty(internalSubset))
            {
                return;
            }

            foreach (System.Text.RegularExpressions.Match decl in System.Text.RegularExpressions.Regex.Matches(internalSubset, @"<!ATTLIST\s+(\S+)\s+([\s\S]*?)>"))
            {
                string elem = decl.Groups[1].Value;
                // Each attribute def is `name type default`. Anchoring on the default token that follows the
                // type (#REQUIRED/#IMPLIED/#FIXED or a quoted value) avoids matching a type keyword that
                // appears inside an enumeration or a default value earlier in the same ATTLIST.
                foreach (System.Text.RegularExpressions.Match att in System.Text.RegularExpressions.Regex.Matches(decl.Groups[2].Value,
                    "([^\\s>]+)\\s+(ID|IDREF|IDREFS|NMTOKEN|NMTOKENS|ENTITY|ENTITIES)\\s+(?:#(?:REQUIRED|IMPLIED|FIXED)|\"|')"))
                {
                    if (dtdAttTypes == null)
                    {
                        dtdAttTypes = new Dictionary<string, string>();
                    }

                    dtdAttTypes[elem + "\t" + att.Groups[1].Value] = att.Groups[2].Value;
                }
            }
        }

        // Harvest <!ENTITY name SYSTEM|PUBLIC ... NDATA notation> declarations from the internal
        // subset and report them to the receiver (upstream gets these via SAX unparsedEntityDecl;
        // System.Xml.XmlReader has no unparsed-entity API). The system ID is absolutized against
        // the document URI — SAX parsers differ on this and the suite expects it resolved.
        private void ParseDtdUnparsedEntities(string internalSubset)
        {
            if (string.IsNullOrEmpty(internalSubset))
            {
                return;
            }

            foreach (System.Text.RegularExpressions.Match decl in System.Text.RegularExpressions.Regex.Matches(internalSubset, @"<!ENTITY\s+([^\s%>][^\s>]*)\s+([\s\S]*?)>"))
            {
                string name = decl.Groups[1].Value;
                string body = decl.Groups[2].Value;
                if (!System.Text.RegularExpressions.Regex.IsMatch(body, @"\bNDATA\s+\S+\s*$"))
                {
                    continue;   // parsed (general) entity — not reported
                }

                string publicId = null;
                var m = System.Text.RegularExpressions.Regex.Match(body, "^PUBLIC\\s+(?:\"([^\"]*)\"|'([^']*)')\\s+(?:\"([^\"]*)\"|'([^']*)')");
                string systemId;
                if (m.Success)
                {
                    publicId = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                    systemId = m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
                }
                else
                {
                    m = System.Text.RegularExpressions.Regex.Match(body, "^SYSTEM\\s+(?:\"([^\"]*)\"|'([^']*)')");
                    if (!m.Success)
                    {
                        continue;
                    }
                    systemId = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                }

                string abs = systemId;
                try
                {
                    string baseUri = localLocator.GetSystemId();
                    if (!string.IsNullOrEmpty(baseUri))
                    {
                        abs = new Uri(new Uri(baseUri), systemId).AbsoluteUri;
                    }
                }
                catch { }
                receiver.SetUnparsedEntity(name, abs, publicId);
            }
        }

        // Fetch the external DTD subset (SYSTEM identifier) and harvest its ATTLIST declarations, so that
        // ID/IDREF attributes declared in an external .dtd are typed (fn:id/fn:idref, id() in patterns).
        // Best-effort: parameter-entity indirection and includes are not expanded (adequate for the flat
        // FOTS test DTDs); any failure silently leaves ID typing to the internal subset. Read as the parser
        // read it: from the host's resolver if that serves it, else from the file.
        private void ParseExternalDtd(string systemId, string publicId)
        {
            if (string.IsNullOrEmpty(systemId))
            {
                return;
            }

            try
            {
                Uri abs;
                string baseUri = localLocator.GetSystemId();
                if (!string.IsNullOrEmpty(baseUri) && Uri.TryCreate(new Uri(baseUri), systemId, out abs))
                {
                    // nothing
                }
                else if (!Uri.TryCreate(systemId, UriKind.Absolute, out abs))
                {
                    return;
                }

                string text = null;
                using (Stream served = ResourceResolverXmlResolver.StreamOf(ResourceResolverXmlResolver.Ask(pipe.GetConfiguration().GetResourceResolver(), abs, publicId)))
                {
                    if (served != null)
                    {
                        text = new StreamReader(served).ReadToEnd();
                    }
                }

                if (text == null)
                {
                    if (OutSmart.DAXon.Internal.ResourceGate.IsRestricted(pipe.GetConfiguration()) && OutSmart.DAXon.Internal.ResourceGate.CheckRead(pipe.GetConfiguration(), abs.AbsoluteUri, OutSmart.DAXon.Api.ResourceKind.ExternalEntity) != null)
                    {
                        return;
                    }

                    if (abs.IsFile && System.IO.File.Exists(abs.LocalPath))
                    {
                        using (var dtd = new StreamReader(OutSmart.DAXon.Internal.Streams.FileNames.OpenRead(abs.LocalPath)))
                        {
                            text = dtd.ReadToEnd();
                        }
                    }
                }

                ParseDtdAttTypes(text);
                ParseDtdUnparsedEntities(text);
            }
            catch { }
        }

        private void StartElement()
        {
            Flush(true);
            int options = ReceiverOption.NAMESPACE_OK | ReceiverOption.ALL_NAMESPACES;

            string uri = reader.NamespaceURI ?? string.Empty;
            string localName = reader.LocalName;
            string elemPrefix = reader.Prefix;
            // The element's lexical QName is only needed for the DTD ATTLIST lookup: reader.Name
            // re-concatenates prefixed names on every call, so keep it off the DTD-less hot path.
            string qName = dtdAttTypes != null ? reader.Name : null;

            NamespaceMap nsMap = namespaceStack.Peek();
            IList<AttributeInfo> attributes = null;

            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    if (vetCharacters)
                    {
                        Vet(new StringUnits(reader.Value));
                    }

                    string aPrefix = reader.Prefix;
                    string aLocal = reader.LocalName;
                    if ((aPrefix.Length == 0 && aLocal == "xmlns") || aPrefix == "xmlns")
                    {
                        // Namespace declaration: fold into the namespace map, not the attribute list.
                        // The binding xmlns:xmlns is never legal and is ignored (matching ReceivingContentHandler).
                        string prefix = aPrefix.Length == 0 ? string.Empty : aLocal;
                        if (!prefix.Equals("xmlns"))
                        {
                            nsMap = nsMap.Bind(prefix, NamespaceUri.Of(reader.Value));
                        }
                    }
                    else
                    {
                        if (attributes == null)
                        {
                            attributes = new List<AttributeInfo>(reader.AttributeCount);
                        }

                        INodeName attName = GetNodeName(reader.NamespaceURI ?? string.Empty, aPrefix, aLocal);

                        // Non-validating parse: attributes are untyped, but recover DTD-declared ID/IDREF
                        // typing (from the ATTLIST harvest above) as IS_ID / IS_IDREF flags so fn:id and
                        // fn:idref work — the TinyTree honours these flags (HandleRootTinyDoc) exactly as it
                        // does for the SAX path. NMTOKEN(S)/ENTITY(IES) have no fn:id/idref effect on an
                        // untyped tree, so they stay plain untyped.
                        int attProps = ReceiverOption.NAMESPACE_OK;
                        if (dtdAttTypes != null && dtdAttTypes.TryGetValue(qName + "\t" + reader.Name, out string dtdType))
                        {
                            if (dtdType == "ID")
                            {
                                attProps |= ReceiverOption.IS_ID;
                            }
                            else if (dtdType == "IDREF" || dtdType == "IDREFS")
                            {
                                attProps |= ReceiverOption.IS_IDREF;
                            }
                        }

                        attributes.Add(new AttributeInfo(attName, BuiltInAtomicType.UNTYPED_ATOMIC, reader.Value, localLocator, attProps));
                    }
                }
                while (reader.MoveToNextAttribute());
                reader.MoveToElement();
            }

            INodeName elementName = GetNodeName(uri, elemPrefix, localName);
            IAttributeMap attributeMap = attributes == null
                ? (IAttributeMap)EmptyAttributeMap.GetInstance()
                : SequenceTool.AttributeMapFromList(attributes);

            // SAX resets levelInEntity via startEntity/endEntity; XmlReader expands entities
            // transparently, so detect the boundary by a BaseURI change vs the enclosing element —
            // the builder marks LevelInEntity==0 elements topWithinEntity (xml:base inside an
            // external entity resolves against the ENTITY's URI, base-uri-051/052). Without a DTD
            // no entity can exist, so the tracking is skipped (levelInEntity still counts depth —
            // the builder's LevelInEntity==0 test must stay false below the root).
            if (hasDtd)
            {
                string curBase = reader.BaseURI;
                localLocator.topOfEntity = entityBaseStack.Count > 0
                    && !string.Equals(curBase, entityBaseStack.Peek(), StringComparison.Ordinal);
                receiver.StartElement(elementName, Untyped.INSTANCE, attributeMap, nsMap, localLocator, options);
                localLocator.topOfEntity = false;
                entityBaseStack.Push(curBase);
            }
            else
            {
                receiver.StartElement(elementName, Untyped.INSTANCE, attributeMap, nsMap, localLocator, options);
            }

            localLocator.levelInEntity++;
            namespaceStack.Push(nsMap);
            afterStartTag = true;
        }

        private void EndElement()
        {
            // Don't attempt whitespace compression if this end tag directly follows a start tag.
            Flush(!afterStartTag);
            localLocator.levelInEntity--;
            if (hasDtd)
            {
                entityBaseStack.Pop();
            }

            receiver.EndElement();
            afterStartTag = false;
            namespaceStack.Pop();
        }

        private void AppendReaderValue()
        {
            if (!chunkValues)
            {
                AppendChars(reader.Value);
                return;
            }

            int read;
            do
            {
                if (buffer.Length - charsUsed < 256)
                {
                    Array.Resize(ref buffer, buffer.Length * 2);
                }

                read = reader.ReadValueChunk(buffer, charsUsed, buffer.Length - charsUsed);
                charsUsed += read;
            }
            while (read > 0);
        }

        private void AppendChars(string s)
        {
            int length = s.Length;
            if (length == 0)
            {
                return;
            }

            while (charsUsed + length > buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
            }

            s.CopyTo(0, buffer, charsUsed, length);
            charsUsed += length;
            if (lineNumbering)
            {
                lastTextNodeLocator = localLocator.SaveLocation();
            }
        }

        private void Flush(bool compress)
        {
            if (charsUsed > 0)
            {
                if (vetCharacters)
                {
                    Vet(new CharUnits(buffer, charsUsed));
                }

                if (directBuilder != null && !escapingDisabled)
                {
                    directBuilder.CharactersDirect(buffer, charsUsed, compress, lastTextNodeLocator);
                }
                else
                {
                    UnicodeString content = StringTool.Compress(buffer, 0, charsUsed, compress);
                    receiver.Characters(content, lastTextNodeLocator, escapingDisabled ? ReceiverOption.DISABLE_ESCAPING : ReceiverOption.WHOLE_TEXT_NODE);
                }

                charsUsed = 0;
                escapingDisabled = false;
            }
        }

        // Fails the parse, as the parser itself does for a 1.0 document, on a code unit that no XML has in 1.1 either:
        // U+0000, half a surrogate pair, U+FFFE, U+FFFF.
        private void Vet<T>(T units)
            where T : struct, IUnits
        {
            int n = units.Count;
            for (int i = 0; i < n; i++)
            {
                int unit = units[i];
                if (unit >= 0xD800 && unit <= 0xDBFF && i + 1 < n && units[i + 1] >= 0xDC00 && units[i + 1] <= 0xDFFF)
                {
                    i++;
                }
                else if (unit == 0 || unit >= 0xFFFE || (unit >= 0xD800 && unit <= 0xDFFF))
                {
                    string code = "U+" + unit.ToString("X4");
                    string what = unit >= 0xD800 && unit <= 0xDFFF ? "half a surrogate pair (" + code + ")" : "the character " + code;
                    throw new XmlException("XML does not allow " + what + ", as a character reference either.",
                        null, Math.Max(0, localLocator.GetLineNumber()), Math.Max(0, localLocator.GetColumnNumber()));
                }
            }
        }

        private void ProcessingInstruction(string target, string data)
        {
            Flush(true);
            if (vetCharacters && data != null)
            {
                Vet(new StringUnits(data));
            }

            if (allowDisableOutputEscaping)
            {
                if (target.Equals(PI_DISABLE_OUTPUT_ESCAPING))
                {
                    escapingDisabled = true;
                    return;
                }
                else if (target.Equals(PI_ENABLE_OUTPUT_ESCAPING))
                {
                    escapingDisabled = false;
                    return;
                }
            }

            UnicodeString ud = string.IsNullOrEmpty(data)
                ? (UnicodeString)EmptyUnicodeString.GetInstance()
                : Whitespace.RemoveLeadingWhitespace(StringView.Tidy(data));
            receiver.ProcessingInstruction(target, ud, localLocator, ReceiverOption.NONE);
        }

        private void Comment(string text)
        {
            Flush(true);
            if (vetCharacters && text != null)
            {
                Vet(new StringUnits(text));
            }

            receiver.Comment(StringView.Of(text), localLocator, ReceiverOption.NONE);
        }

        private INodeName GetNodeName(string uri, string prefix, string localname)
        {
            NameEntry[] cache = nameCache;
            int h = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(localname) & 255;
            if (ReferenceEquals(cache[h].Local, localname)
                && ReferenceEquals(cache[h].Uri, uri)
                && ReferenceEquals(cache[h].Prefix, prefix))
            {
                return cache[h].Name;
            }

            int h2 = h ^ 1;
            if (ReferenceEquals(cache[h2].Local, localname)
                && ReferenceEquals(cache[h2].Uri, uri)
                && ReferenceEquals(cache[h2].Prefix, prefix))
            {
                return cache[h2].Name;
            }

            // The INodeName is shared by all nodes with the same lexical QName; the namecode/fingerprint,
            // if needed, is allocated later by the tree builder. The prefix is part of the key to retain it.
            INodeName created = uri.Length == 0
                ? (INodeName)new NoNamespaceName(localname)
                : new FingerprintedQName(prefix, NamespaceUri.Of(uri), localname);
            int victim = cache[h].Local == null || cache[h2].Local != null ? h : h2;
            cache[victim].Local = localname;
            cache[victim].Prefix = prefix;
            cache[victim].Uri = uri;
            cache[victim].Name = created;
            return created;
        }

        // The validity errors of one parse that reads a DTD. They say which elements the DTD does not declare: .NET
        // reports the whitespace in those as it does that of element-only content. A validating parse reports them
        // too, and fails or warns at its end, as Saxon does with a SAX parser.
        internal sealed class DtdEvents
        {
            private readonly DtdUse use;
            private readonly string systemId;
            private readonly IErrorReporter reporter;
            private int errors;
            private string firstMessage;
            private ILocation firstLocation;

            // Of the node being read. The error naming an undeclared element is its first or second.
            private readonly string[] pending = new string[4];
            private int pendingCount;
            private Dictionary<string, bool> undeclared;
            private UndeclaredMessage wording;

            internal DtdEvents(DtdUse use, string systemId, IErrorReporter reporter)
            {
                this.use = use;
                this.systemId = systemId;
                this.reporter = reporter;
            }

            internal bool Validates => use != DtdUse.Whitespace;

            internal void OnEvent(object sender, ValidationEventArgs e)
            {
                if (e.Severity != XmlSeverityType.Error)
                {
                    return;
                }

                if (pendingCount < pending.Length)
                {
                    pending[pendingCount++] = e.Message;
                }

                if (Validates)
                {
                    XmlSchemaException at = e.Exception;
                    Invalid(e.Message, new Loc(string.IsNullOrEmpty(at.SourceUri) ? systemId : at.SourceUri, at.LineNumber, at.LinePosition));
                }
            }

            private void Invalid(string message, ILocation location)
            {
                if (errors++ == 0)
                {
                    firstMessage = message;
                    firstLocation = location;
                }

                reporter?.Report(new XmlProcessingIncident("Error reported by XML parser: " + message, DAXonErrorCode.SXXP0003, location));
            }

            // At the end of the document. hasDtd: it had a DOCTYPE; without one there is nothing it is valid against.
            internal void Finish(bool hasDtd)
            {
                if (!Validates)
                {
                    return;
                }

                if (!hasDtd)
                {
                    Invalid("The document has no DTD to be validated against", new Loc(systemId, -1, -1));
                }

                if (errors == 0)
                {
                    return;
                }

                string count = "The XML parser reported " + new OutSmart.DAXon.Expressions.Numbering.Numberer_en().ToWords(string.Empty, errors).ToLowerInvariant()
                    + " validation error" + (errors == 1 ? string.Empty : "s");
                if (use == DtdUse.ValidateLax)
                {
                    reporter?.Report(new XmlProcessingIncident(count + ". Processing continues, because recovery from validation errors was requested", DAXonErrorCode.SXXP0003, firstLocation).AsWarning());
                    return;
                }

                throw new XPathException(count + (errors == 1 ? ": " : ". The first: ") + firstMessage).WithErrorCode(DAXonErrorCode.SXXP0003).WithLocation(firstLocation);
            }

            // Before each node is read: what the reader raises while reading it is that node's.
            internal void NextNode()
            {
                pendingCount = 0;
            }

            // Whether the element the reader is on is one the DTD does not declare.
            internal bool IsUndeclared(XmlReader reader)
            {
                if (pendingCount == 0)
                {
                    return false;
                }

                string name = reader.Name;
                undeclared = undeclared ?? new Dictionary<string, bool>();
                if (!undeclared.TryGetValue(name, out bool verdict))
                {
                    wording = wording ?? UndeclaredMessage.Current();
                    for (int i = 0; i < pendingCount && !verdict; i++)
                    {
                        verdict = wording.Names(pending[i], name);
                    }

                    undeclared.Add(name, verdict);
                }

                return verdict;
            }
        }

        // The parser's message for an element its DTD does not declare, around the element's name. Learnt from the
        // parser itself, per UI culture: .NET Framework localizes it, and no public member tells the errors apart.
        private sealed class UndeclaredMessage
        {
            private static readonly ConcurrentDictionary<string, UndeclaredMessage> ByCulture = new ConcurrentDictionary<string, UndeclaredMessage>();
            private readonly string before;
            private readonly string after;

            private UndeclaredMessage(string before, string after)
            {
                this.before = before;
                this.after = after;
            }

            internal static UndeclaredMessage Current()
            {
                return ByCulture.GetOrAdd(System.Globalization.CultureInfo.CurrentUICulture.Name, culture => Learn());
            }

            // Not learnt (before == null): every error then counts, which only keeps more whitespace.
            internal bool Names(string message, string element)
            {
                return before == null
                    || (message.Length == before.Length + element.Length + after.Length
                        && message.StartsWith(before, StringComparison.Ordinal)
                        && message.EndsWith(after, StringComparison.Ordinal)
                        && string.CompareOrdinal(message, before.Length, element, 0, element.Length) == 0);
            }

            private static UndeclaredMessage Learn()
            {
                const string element = "q7z";
                string message = null;
                try
                {
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, ValidationType = ValidationType.DTD, XmlResolver = null };
                    settings.ValidationEventHandler += (sender, e) => message = message ?? e.Message;
                    using (XmlReader probe = XmlReader.Create(new StringReader("<!DOCTYPE a [<!ELEMENT a ANY>]><a><" + element + "/></a>"), settings))
                    {
                        while (probe.Read())
                        {
                        }
                    }
                }
                catch (Exception)
                {
                    message = null;
                }

                int at = message == null ? -1 : message.IndexOf(element, StringComparison.Ordinal);
                return at < 0 ? new UndeclaredMessage(null, null) : new UndeclaredMessage(message.Substring(0, at), message.Substring(at + element.Length));
            }
        }

        /// <summary>
        /// As above, but optionally validating against the document's DTD (<c>ValidationType.DTD</c>) — the
        /// native replacement for the old SAX DTD-STRICT path. DTD validation needs an XmlResolver to fetch an
        /// external DTD subset; the caller supplies one when validating against an external DTD.
        /// </summary>
        // Java's default SAX parser resolves file-relative external DTD subsets/entities; the old null
        // resolver made any <!DOCTYPE x SYSTEM "local.dtd"> fail to parse. Restrict to file: URIs so no
        // network fetch can happen implicitly.
        internal sealed class FileOnlyXmlResolver : XmlUrlResolver
        {
            private readonly Configuration config;
            private readonly Uri principal;
            private bool principalPending;   // the parser opens the document itself first, before anything it references
            private PublicIdProbe publicIds;

            // Of the document the parser opened by system id, read from the open handle, so a tree can be
            // sized from it; else -1.
            internal long PrincipalLength { get; private set; } = -1;

            public FileOnlyXmlResolver()
                : this(null, null)
            {
            }

            // principal: the document itself when the parser opens it by system id. It was asked
            // for by the host or already passed the gate as a document; only what it references is
            // an external entity. Resolved only under a restricted policy, the only one that asks.
            public FileOnlyXmlResolver(Configuration config, string principalSystemId)
            {
                this.config = config;
                principalPending = !string.IsNullOrEmpty(principalSystemId);
                principal = ResourceResolverXmlResolver.PrincipalOf(this, config, principalSystemId);
            }

            public override Uri ResolveUri(Uri baseUri, string relativeUri)
            {
                return publicIds.Resolve(this, baseUri, relativeUri, (r, b, rel) => r.BaseResolveUri(b, rel));
            }

            private Uri BaseResolveUri(Uri baseUri, string relativeUri) => base.ResolveUri(baseUri, relativeUri);

            public override object GetEntity(Uri absoluteUri, string role, System.Type ofObjectToReturn)
            {
                if (principalPending || config == null)
                {
                    return Open(absoluteUri, role, ofObjectToReturn);
                }

                try
                {
                    // What a document pulls in - its external DTD, an external entity - is the host's resolver's to
                    // serve first, as a stylesheet's is: its own answer, neither gated nor capped.
                    Stream served = ResourceResolverXmlResolver.StreamOf(ResourceResolverXmlResolver.Ask(config.GetResourceResolver(), absoluteUri, publicIds.Take()));
                    return served ?? Open(absoluteUri, role, ofObjectToReturn);
                }
                catch (Exception)
                {
                    publicIds.NotOpened(absoluteUri);
                    throw;
                }
            }

            private object Open(Uri absoluteUri, string role, System.Type ofObjectToReturn)
            {
                if (absoluteUri != null && absoluteUri.IsFile)
                {
                    ResourceResolverXmlResolver.CheckEntityRead(config, absoluteUri, principal);

                    // Opened as System.Xml opens it, by the engine: a name that is no file's is refused first.
                    object entity = ofObjectToReturn == null || ofObjectToReturn == typeof(Stream) || ofObjectToReturn == typeof(object)
                        ? OutSmart.DAXon.Internal.Streams.FileNames.OpenRead(absoluteUri.LocalPath, 1)
                        : base.GetEntity(absoluteUri, role, ofObjectToReturn);
                    if (principalPending)
                    {
                        // The input the host asked for by path: capped as a stream it passes would be.
                        principalPending = false;
                        PrincipalLength = OutSmart.DAXon.Resources.ActiveStreamSource.RemainingLength(entity as Stream);
                        entity = OutSmart.DAXon.Internal.Streams.InputSizeLimit.Apply(entity as Stream, PrincipalLength, OutSmart.DAXon.Internal.Streams.InputSizeLimit.MaxFor(config), absoluteUri.OriginalString, "FODC0002") ?? entity;
                    }
                    else
                    {
                        // What the document pulls in - its external DTD, an external entity - is input under the same cap.
                        entity = OutSmart.DAXon.Internal.Streams.InputSizeLimit.Apply(entity as Stream, OutSmart.DAXon.Internal.Streams.InputSizeLimit.MaxFor(config), absoluteUri.OriginalString, "FODC0002") ?? entity;
                    }

                    return entity;
                }

                // IOException, not XmlException: an unfetchable URI is an I/O failure — callers map it
                // to SXXP0003/XTSE0165 (a raw XmlException would escape the compile path uncoded).
                throw new System.IO.IOException("External entity fetch blocked for non-file URI: " + absoluteUri);
            }
        }

        // Serves a buffered prefix, then the rest of the underlying stream — lets the XML declaration be
        // peeked (and optionally rewritten) without a seekable stream. Read-only, forward-only.
        private sealed class PrefixedStream : Stream
        {
            private readonly byte[] prefix;
            private readonly int prefixLen;
            private int pos;
            private readonly Stream rest;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new System.NotSupportedException();
            public override long Position
            {
                get => throw new System.NotSupportedException();
                set => throw new System.NotSupportedException();
            }

            public PrefixedStream(byte[] prefix, int count, Stream rest)
            {
                this.prefix = prefix;
                this.prefixLen = count;
                this.rest = rest;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (pos < prefixLen)
                {
                    int take = Math.Min(count, prefixLen - pos);
                    System.Array.Copy(prefix, pos, buffer, offset, take);
                    pos += take;
                    return take;
                }

                return rest.Read(buffer, offset, count);
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new System.NotSupportedException();
            public override void SetLength(long value) => throw new System.NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    rest.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        // As PrefixedStream, for an input of characters.
        private sealed class PrefixedTextReader : TextReader
        {
            private readonly char[] prefix;
            private readonly int prefixLen;
            private int pos;
            private readonly TextReader rest;

            public PrefixedTextReader(char[] prefix, int count, TextReader rest)
            {
                this.prefix = prefix;
                this.prefixLen = count;
                this.rest = rest;
            }

            public override int Peek()
            {
                return pos < prefixLen ? prefix[pos] : rest.Peek();
            }

            public override int Read()
            {
                return pos < prefixLen ? prefix[pos++] : rest.Read();
            }

            public override int Read(char[] buffer, int index, int count)
            {
                if (pos < prefixLen)
                {
                    int take = Math.Min(count, prefixLen - pos);
                    System.Array.Copy(prefix, pos, buffer, index, take);
                    pos += take;
                    return take;
                }

                return rest.Read(buffer, index, count);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    rest.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        // Native live location backed directly by the XmlReader's line/column information (no SAX Locator).
        // It carries levelInEntity so the tree builders (via ISourceLocator) can mark the top node of each
        // entity, exactly as ReceivingContentHandler.LocalLocator does on the SAX path.
        private sealed class XmlPullLocation : ISourceLocator
        {
            private readonly XmlReader reader;
            private readonly IXmlLineInfo lineInfo;
            public int levelInEntity;
            public bool topOfEntity;   // set for the StartElement call of an element whose BaseURI differs from its parent's

            public int LevelInEntity => topOfEntity ? 0 : levelInEntity;

            public XmlPullLocation(XmlReader reader)
            {
                this.reader = reader;
                this.lineInfo = reader as IXmlLineInfo;
            }
            public string GetPublicId() => null;
            public string GetSystemId() => reader.BaseURI;
            public int GetLineNumber() => lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;
            public int GetColumnNumber() => lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LinePosition : -1;
            public ILocation SaveLocation() => new Loc(GetSystemId(), GetLineNumber(), GetColumnNumber());
        }
    }
}
