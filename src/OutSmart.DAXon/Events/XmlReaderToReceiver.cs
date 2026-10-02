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
using OutSmart.DAXon.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

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

        // The XML declaration is ASCII in every byte-oriented XML encoding, so matching the first bytes
        // directly finds version="1.1" regardless of the real encoding (UTF-16 is not peeked, as before).
        private const int XmlDeclPeekBytes = 256;
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
            return CreateXmlReader(charStream, byteStream, systemId, resolver, false);
        }

        // config: the configuration whose resource policy gates the default resolver's file reads
        // (external DTD subsets and entities); null for the engine's own embedded resources.
        public static XmlReader CreateXmlReader(TextReader charStream, Stream byteStream, string systemId, XmlResolver resolver, bool dtdValidate, bool suppressValidationErrors = false, Configuration config = null)
        {
            var settings = new XmlReaderSettings
            {
                // File-relative DTD/external-entity fetch by default (Java SAX parity); an internal subset
                // is processed for entity expansion. When a resolver is supplied, external entities/DTD
                // resolve through it instead.
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = resolver ?? new FileOnlyXmlResolver(config, charStream == null && byteStream == null ? systemId : null),
                ValidationType = dtdValidate ? ValidationType.DTD : ValidationType.None,
                IgnoreComments = false,
                IgnoreProcessingInstructions = false,
                IgnoreWhitespace = false,
                CheckCharacters = true,
                CloseInput = true,
                ConformanceLevel = ConformanceLevel.Document,
            };
            if (dtdValidate && suppressValidationErrors)
            {
                // ValidationType.DTD here is used only so .NET classifies element-content whitespace as
                // ignorable (XmlNodeType.Whitespace vs SignificantWhitespace — see Parse()); we do not want a
                // DTD-invalid but well-formed document to abort, so swallow validity events.
                settings.ValidationEventHandler += (sender, e) => { };
            }

            // Ownership note: CloseInput=true above means this factory OWNS the supplied stream/reader
            // - the returned XmlReader's Dispose closes it, on the success path and the mid-parse-error
            // path alike. The same ownership must hold when construction itself throws, or the input
            // (engine-opened for doc()/includes; wrapped in a deadline guard for http) leaks to the
            // finalizer holding its file handle or pooled socket. The declaration peek is a real throw
            // site: a guarded network stream raises SXTO0001 from Read when the run's deadline expires.
            string baseUri = systemId ?? string.Empty;
            if (charStream != null)
            {
                try
                {
                    return XmlReader.Create(charStream, settings, baseUri);
                }
                catch
                {
                    charStream.Dispose();
                    throw;
                }
            }

            if (byteStream != null)
            {
                // XML 1.1: .NET's XmlReader rejects version="1.1". If the declaration announces 1.1, downgrade
                // it to 1.0 in a pass-through wrapper and relax character checking so the C0 controls that are
                // well-formed in 1.1 (but not 1.0) pass. A 1.0 / declaration-less document is untouched.
                try
                {
                    byteStream = MaybeDowngradeXml11(byteStream, settings);
                    return XmlReader.Create(byteStream, settings, baseUri);
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
                return XmlReader.Create(systemId, settings);
            }

            throw new XPathException("ActiveStreamSource supplies neither a stream nor a system identifier");
        }

        private static Stream MaybeDowngradeXml11(Stream input, XmlReaderSettings settings)
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
                    bufSize = (int)Math.Min(bufSize, Math.Max(4096, input.Length));
                }

                input = new BufferedStream(input, bufSize);
            }

            byte[] head = new byte[XmlDeclPeekBytes];
            int n = 0, r;
            while (n < XmlDeclPeekBytes && (r = input.Read(head, n, XmlDeclPeekBytes - n)) > 0)
            {
                n += r;
            }

            int digit = Xml11VersionDigit(head, n);
            if (digit >= 0)
            {
                settings.CheckCharacters = false;
                // '1.1' -> '1.0' is length-preserving, so the byte offsets the parser sees are unchanged
                head[digit] = (byte)'0';
            }

            return new PrefixedStream(head, n, input);
        }

        // Offset of the last digit of version="1.1" (or '1.1') in the document's XML declaration, else
        // -1. Checked on the bytes: decoding the peek to a Latin-1 string cost ~1 KB and 3.6k cycles
        // per document.
        private static int Xml11VersionDigit(byte[] head, int n)
        {
            int decl = -1;
            for (int i = 0; i <= 3; i++)   // a BOM may precede the declaration
            {
                if (BytesAt(head, n, i, "<?xml"))
                {
                    decl = i;
                    break;
                }
            }

            if (decl < 0)
            {
                return -1;
            }

            for (int i = decl + 5; i < n; i++)
            {
                if (BytesAt(head, n, i, "?>"))
                {
                    return -1;
                }

                if (BytesAt(head, n, i, "version=") && i + 12 < n)
                {
                    byte quote = head[i + 8];
                    if ((quote == '"' || quote == '\'') && head[i + 9] == '1' && head[i + 10] == '.' && head[i + 11] == '1' && head[i + 12] == quote)
                    {
                        return i + 11;
                    }
                }
            }

            return -1;
        }

        private static bool BytesAt(byte[] bytes, int n, int at, string ascii)
        {
            if (at + ascii.Length > n)
            {
                return false;
            }

            for (int k = 0; k < ascii.Length; k++)
            {
                if (bytes[at + k] != ascii[k])
                {
                    return false;
                }
            }

            return true;
        }

        public void Parse()
        {
            localLocator = new XmlPullLocation(reader);
            lastTextNodeLocator = localLocator;
            StartDocument();
            // When the reader validates against a DTD, .NET reports whitespace in element-only content as
            // XmlNodeType.Whitespace and whitespace in mixed content as SignificantWhitespace — the signal
            // needed to drop ignorable whitespace (number-4501). Without validation every inter-element
            // whitespace is Whitespace, so this stays off and such nodes are preserved.
            bool dtdWhitespaceClassification = reader.Settings != null && reader.Settings.ValidationType == ValidationType.DTD;
            // Cooperative deadline: a doc()/document() call mid-run parses here with no other
            // check site, so a large document must not outrun the transformation limit. Called
            // per node event - the active token throttles clock sampling itself.
            while (reader.Read())
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();

                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
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
                        // DTD-less document (every inter-element node is Whitespace) preserves it.
                        if (hasDtd && dtdWhitespaceClassification)
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
                        ParseExternalDtd(reader.GetAttribute("SYSTEM"));
                        break;

                        // XmlDeclaration, expanded EntityReference: nothing to emit.
                }
            }

            EndDocument();
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

                string publicId = null, systemId = null;
                var m = System.Text.RegularExpressions.Regex.Match(body, "^PUBLIC\\s+(?:\"([^\"]*)\"|'([^']*)')\\s+(?:\"([^\"]*)\"|'([^']*)')");
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
        // Best-effort and file-only: parameter-entity indirection and includes are not expanded (adequate
        // for the flat FOTS test DTDs); any failure silently leaves ID typing to the internal subset.
        private void ParseExternalDtd(string systemId)
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

                if (OutSmart.DAXon.Internal.ResourceGate.IsRestricted(pipe.GetConfiguration()) && OutSmart.DAXon.Internal.ResourceGate.CheckRead(pipe.GetConfiguration(), abs.AbsoluteUri, OutSmart.DAXon.Api.ResourceKind.ExternalEntity) != null)
                {
                    return;
                }

                if (abs.IsFile && System.IO.File.Exists(abs.LocalPath))
                {
                    ParseDtdAttTypes(System.IO.File.ReadAllText(abs.LocalPath));
                    ParseDtdUnparsedEntities(System.IO.File.ReadAllText(abs.LocalPath));
                }
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

        private void ProcessingInstruction(string target, string data)
        {
            Flush(true);
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
            private bool measurePrincipal;

            // Of the document the parser opened by system id - its first fetch, before anything it
            // references - read from the open handle, so a tree can be sized from it; else -1.
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
                measurePrincipal = !string.IsNullOrEmpty(principalSystemId);
                if (OutSmart.DAXon.Internal.ResourceGate.IsRestricted(config) && !string.IsNullOrEmpty(principalSystemId))
                {
                    try
                    {
                        principal = ResolveUri(null, principalSystemId);
                    }
                    catch (Exception)
                    {
                        principal = null;
                    }
                }
            }

            public override object GetEntity(Uri absoluteUri, string role, System.Type ofObjectToReturn)
            {
                if (absoluteUri != null && absoluteUri.IsFile)
                {
                    if (OutSmart.DAXon.Internal.ResourceGate.IsRestricted(config) && !absoluteUri.Equals(principal))
                    {
                        string denied = OutSmart.DAXon.Internal.ResourceGate.CheckRead(config, absoluteUri.AbsoluteUri, OutSmart.DAXon.Api.ResourceKind.ExternalEntity);
                        if (denied != null)
                        {
                            throw new System.IO.IOException(denied);
                        }
                    }

                    object entity = base.GetEntity(absoluteUri, role, ofObjectToReturn);
                    if (measurePrincipal)
                    {
                        measurePrincipal = false;
                        PrincipalLength = OutSmart.DAXon.Resources.ActiveStreamSource.RemainingLength(entity as Stream);
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
