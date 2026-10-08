////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Events
{
    // A System.Xml.XmlWriter that feeds the Receiver pipeline (push-building of documents).
    // Per the port's Dispose/Close contract: Close() is the normal finish (implicitly ends the
    // document, may throw XPathException); Dispose without Close releases quietly and DISCARDS.
    // XPathException from the pipeline propagates as-is — no checked-exception wrapper.
    // Names and prefixes mean what they mean to System.Xml's writer: a null namespace is the one the prefix has in
    // scope, an attribute in a namespace gets a prefix, and a prefix means one namespace in a start tag.
    public class StreamWriterToReceiver : XmlWriter
    {
        private StartTag pendingTag;
        private readonly Stack<NamespaceMap> namespaceStack = new Stack<NamespaceMap>();
        private readonly IReceiver receiver;
        private readonly IIntPredicateProxy charChecker;
        private bool isChecking;
        private int depth = -1;
        private bool closed;

        // Attribute values stream in between WriteStartAttribute and WriteEndAttribute.
        private Triple pendingAttribute;
        private bool pendingAttributeIsNamespaceDecl;
        private readonly StringBuilder attributeValue = new StringBuilder();

        // The first half of a surrogate pair that a piece of text ended with, or zero. Text may come in pieces cut
        // anywhere - WriteChars by the buffer - and the other half may open the next piece.
        private char heldHalf;

        public virtual IReceiver Receiver => receiver;

        public StreamWriterToReceiver(IReceiver receiver)
        {
            PipelineConfiguration pipe = receiver.GetPipelineConfiguration();
            this.namespaceStack.Push(NamespaceMap.EmptyMap());
            this.receiver = new NamespaceReducer(receiver);
            this.charChecker = pipe.GetConfiguration().ValidCharacterChecker;
        }

        public virtual void SetCheckValues(bool check)
        {
            this.isChecking = check;
        }

        // Text of the host's: half a surrogate pair is U+FFFD, as wherever text comes in - but for one that ends the
        // piece, which waits for what follows it.
        private string Paired(string text)
        {
            if (heldHalf != '\0')
            {
                text = heldHalf + text;
                heldHalf = '\0';
            }

            int last = text.Length - 1;
            if (last >= 0 && char.IsHighSurrogate(text[last]))
            {
                heldHalf = text[last];
                text = text.Substring(0, last);
            }

            return StringTool.WithoutHalfPairs(text);
        }

        // Nothing followed the half that was held: it is U+FFFD, in the text it ended.
        private void ReleaseHalf()
        {
            if (heldHalf != '\0')
            {
                heldHalf = '\0';
                receiver.Characters(StringView.Of(((char)0xFFFD).ToString()), Loc.NONE, ReceiverOption.NONE);
            }
        }

        public virtual bool IsCheckValues()
        {
            return this.isChecking;
        }

        public override WriteState WriteState
        {
            get
            {
                if (closed)
                {
                    return WriteState.Closed;
                }

                if (pendingAttribute != null)
                {
                    return WriteState.Attribute;
                }

                if (pendingTag != null)
                {
                    return WriteState.Element;
                }

                return depth == -1 ? WriteState.Start : WriteState.Content;
            }
        }

        private void FlushStartTag()
        {
            if (depth == -1)
            {
                WriteStartDocument();
            }

            if (pendingAttribute != null)
            {
                WriteEndAttribute();
            }

            if (pendingTag != null)
            {
                Triple e = pendingTag.elementName;
                INodeName elemName = e.uri.IsEmpty() ? new NoNamespaceName(e.local) : (INodeName)new FingerprintedQName(e.prefix, e.uri, e.local);
                NamespaceMap nsMap = namespaceStack.Peek();
                foreach (KeyValuePair<string, NamespaceUri> b in pendingTag.bindings)
                {
                    nsMap = nsMap.Bind(b.Key, b.Value);
                }

                IAttributeMap attributes = EmptyAttributeMap.GetInstance();
                foreach (Triple t in pendingTag.attributes)
                {
                    INodeName attName = t.uri.IsEmpty() ? new NoNamespaceName(t.local) : (INodeName)new FingerprintedQName(t.prefix, t.uri, t.local);
                    attributes = attributes.Put(new AttributeInfo(attName, BuiltInAtomicType.UNTYPED_ATOMIC, t.value, Loc.NONE, ReceiverOption.NONE));
                }

                receiver.StartElement(elemName, Untyped.INSTANCE, attributes, nsMap, Loc.NONE, ReceiverOption.NONE);
                pendingTag = null;
                namespaceStack.Push(nsMap);
            }
        }

        // The namespace a prefix means where the pending tag is (its own bindings first), or where the content is.
        private NamespaceUri InScope(string prefix)
        {
            if (prefix == "xml")
            {
                return NamespaceUri.XML;
            }

            NamespaceUri own = pendingTag == null ? null : BoundInTag(prefix);
            if (own != null)
            {
                return own;
            }

            NamespaceUri outer = namespaceStack.Peek().GetNamespaceUri(prefix);
            return outer == null && prefix.Length == 0 ? NamespaceUri.NULL : outer;
        }

        // A prefix that means uri where the pending tag is; "" only for an element, which takes the default namespace.
        private string PrefixInScope(NamespaceUri uri, bool forAttribute)
        {
            if (uri == NamespaceUri.XML)
            {
                return "xml";
            }

            if (!forAttribute && InScope("") == uri)
            {
                return "";
            }

            if (pendingTag != null)
            {
                foreach (KeyValuePair<string, NamespaceUri> b in pendingTag.bindings)
                {
                    if (b.Key.Length != 0 && b.Value == uri)
                    {
                        return b.Key;
                    }
                }
            }

            IEnumerator<string> prefixes = namespaceStack.Peek().IteratePrefixes();
            while (prefixes.MoveNext())
            {
                string p = prefixes.Current;
                if (p.Length != 0 && InScope(p) == uri)
                {
                    return p;
                }
            }

            return null;
        }

        private NamespaceUri BoundInTag(string prefix)
        {
            foreach (KeyValuePair<string, NamespaceUri> b in pendingTag.bindings)
            {
                if (b.Key == prefix)
                {
                    return b.Value;
                }
            }

            return null;
        }

        // A binding the pending tag makes. In one start tag a prefix means one namespace, or the tag would say two things.
        private void BindInTag(string prefix, NamespaceUri uri)
        {
            NamespaceUri bound = BoundInTag(prefix);
            if (bound == null)
            {
                pendingTag.bindings.Add(new KeyValuePair<string, NamespaceUri>(prefix, uri));
            }
            else if (bound != uri)
            {
                throw new ArgumentException("The prefix '" + prefix + "' cannot be bound to " + Err.Wrap(uri.ToString()) + " and to " + Err.Wrap(bound.ToString()) + " in one start tag");
            }
        }

        // The prefixes XML Namespaces reserves: xml for its namespace alone, xmlns for none.
        private static void CheckReserved(string prefix, NamespaceUri uri)
        {
            if (prefix == "xmlns" || uri == NamespaceUri.XMLNS)
            {
                throw new ArgumentException("The prefix xmlns and its namespace are reserved for namespace declarations");
            }

            if ((prefix == "xml") != (uri == NamespaceUri.XML))
            {
                throw new ArgumentException("The prefix xml and the namespace " + NamespaceUri.XML + " belong to each other");
            }
        }

        private static void CheckName(string name, string what)
        {
            if (name == null)
            {
                throw new ArgumentNullException(what);
            }

            if (!NameChecker.IsValidNCName(name))
            {
                throw new ArgumentException("Invalid " + what + Err.Wrap(name));
            }
        }

        private void CheckUri(NamespaceUri uri)
        {
            if (isChecking && !uri.IsEmpty() && !StandardURIChecker.GetInstance().IsValidURI(uri.ToString()))
            {
                throw new ArgumentException("Namespace URI " + Err.Wrap(uri.ToString()) + " is invalid");
            }
        }

        public override void WriteStartDocument()
        {
            if (depth != -1)
            {
                throw new InvalidOperationException("WriteStartDocument must be the first call");
            }

            receiver.Open();
            receiver.StartDocument(ReceiverOption.NONE);
            depth = 0;
        }

        public override void WriteStartDocument(bool standalone)
        {
            WriteStartDocument();
        }

        public override void WriteEndDocument()
        {
            if (depth == -1)
            {
                throw new InvalidOperationException("WriteEndDocument with no matching WriteStartDocument");
            }

            FlushStartTag();
            ReleaseHalf();
            while (depth > 0)
            {
                WriteEndElement();
            }

            receiver.EndDocument();
            depth = -1;
        }

        // The Receiver pipeline has no DTD event; the document type declaration is ignored.
        public override void WriteDocType(string name, string pubid, string sysid, string subset)
        {
        }

        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            CheckName(localName, "element name");
            if (!string.IsNullOrEmpty(prefix))
            {
                CheckName(prefix, "prefix");
            }

            FlushStartTag();
            ReleaseHalf();
            NamespaceUri uri;
            if (ns == null)
            {
                prefix = prefix ?? "";
                uri = InScope(prefix) ?? throw new ArgumentException("The prefix '" + prefix + "' is not declared");
            }
            else
            {
                uri = NamespaceUri.Of(ns);
                prefix = prefix ?? PrefixInScope(uri, false) ?? "";
                if (prefix.Length != 0 && uri.IsEmpty())
                {
                    throw new ArgumentException("Cannot use a prefix with an empty namespace");
                }
            }

            CheckUri(uri);
            if (prefix.Length != 0 || !uri.IsEmpty())
            {
                CheckReserved(prefix, uri);
            }

            depth++;
            pendingTag = new StartTag();
            pendingTag.elementName.local = localName;
            pendingTag.elementName.uri = uri;
            pendingTag.elementName.prefix = prefix;
            BindInTag(prefix, uri);
        }

        public override void WriteEndElement()
        {
            if (depth <= 0)
            {
                throw new InvalidOperationException("WriteEndElement with no matching WriteStartElement");
            }

            FlushStartTag();
            ReleaseHalf();
            namespaceStack.Pop();
            receiver.EndElement();
            depth--;
        }

        // Tree events carry no empty-tag/full-tag distinction.
        public override void WriteFullEndElement()
        {
            WriteEndElement();
        }

        public override void WriteStartAttribute(string prefix, string localName, string ns)
        {
            CheckName(localName, "attribute name");
            if (pendingTag == null)
            {
                throw new InvalidOperationException("Cannot write attribute when not in a start tag");
            }

            if (pendingAttribute != null)
            {
                throw new InvalidOperationException("WriteStartAttribute while already inside an attribute");
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                CheckName(prefix, "prefix");
            }

            attributeValue.Length = 0;
            bool xmlnsNamespace = ns == NamespaceUri.XMLNS.ToString();
            // xmlns declarations arrive through the attribute API, as System.Xml takes them: xmlns:p="uri" (prefix "xmlns",
            // or no prefix in the xmlns namespace) and xmlns="uri"; an attribute named so otherwise is refused.
            if (prefix == "xmlns" || (prefix == null && xmlnsNamespace) || (string.IsNullOrEmpty(prefix) && localName == "xmlns" && (string.IsNullOrEmpty(ns) || xmlnsNamespace)))
            {
                if (!string.IsNullOrEmpty(ns) && !xmlnsNamespace)
                {
                    throw new ArgumentException("The prefix and the name xmlns are reserved for namespace declarations");
                }

                pendingAttributeIsNamespaceDecl = true;
                pendingAttribute = new Triple { prefix = prefix == "xmlns" || localName != "xmlns" ? localName : "" };
                return;
            }

            NamespaceUri uri;
            if (ns == null)
            {
                // as System.Xml: an undeclared prefix with no namespace given is an attribute in no namespace
                uri = string.IsNullOrEmpty(prefix) ? NamespaceUri.NULL : InScope(prefix) ?? NamespaceUri.NULL;
            }
            else
            {
                uri = NamespaceUri.Of(ns);
            }

            // an attribute takes no default namespace: in a namespace it has a prefix, one of its own if need be
            if (uri.IsEmpty())
            {
                if (localName == "xmlns")
                {
                    throw new ArgumentException("An attribute named xmlns in no namespace is a namespace declaration");
                }

                prefix = "";
            }
            else
            {
                CheckUri(uri);
                if (!string.IsNullOrEmpty(prefix))
                {
                    CheckReserved(prefix, uri);
                }

                NamespaceUri inTag = string.IsNullOrEmpty(prefix) ? null : BoundInTag(prefix);
                if (string.IsNullOrEmpty(prefix) || (inTag != null && inTag != uri))
                {
                    prefix = PrefixInScope(uri, true) ?? FreshPrefix();
                }

                CheckReserved(prefix, uri);
            }

            foreach (Triple t in pendingTag.attributes)
            {
                if (t.local == localName && t.uri == uri)
                {
                    throw new XmlException("'" + localName + "' is a duplicate attribute name");
                }
            }

            if (!uri.IsEmpty())
            {
                BindInTag(prefix, uri);
            }

            pendingAttributeIsNamespaceDecl = false;
            pendingAttribute = new Triple { prefix = prefix, uri = uri, local = localName };
        }

        // A prefix nothing in scope uses, for an attribute in a namespace that has none.
        private string FreshPrefix()
        {
            for (int i = 0; ; i++)
            {
                string p = "ns" + i;
                if (InScope(p) == null)
                {
                    return p;
                }
            }
        }

        public override void WriteEndAttribute()
        {
            if (pendingAttribute == null)
            {
                throw new InvalidOperationException("WriteEndAttribute with no matching WriteStartAttribute");
            }

            // the pieces of the value are together here: what is half a pair now has no other half
            string value = StringTool.WithoutHalfPairs(attributeValue.ToString());
            Triple attribute = pendingAttribute;
            pendingAttribute = null;
            if (pendingAttributeIsNamespaceDecl)
            {
                NamespaceUri uri = NamespaceUri.Of(value);
                if (attribute.prefix.Length != 0 && uri.IsEmpty())
                {
                    throw new ArgumentException("The prefix '" + attribute.prefix + "' cannot be undeclared");
                }

                if (attribute.prefix.Length != 0 || !uri.IsEmpty())
                {
                    CheckReserved(attribute.prefix, uri);
                }

                CheckUri(uri);
                BindInTag(attribute.prefix, uri);
            }
            else
            {
                attribute.value = value;
                pendingTag.attributes.Add(attribute);
            }
        }

        public override void WriteString(string text)
        {
            if (text == null)
            {
                return;
            }

            if (pendingAttribute != null)
            {
                attributeValue.Append(text);
                return;
            }

            FlushStartTag();
            text = Paired(text);
            if (text.Length == 0)
            {
                return;
            }

            UnicodeString uData = StringView.Of(text);
            if (!IsValidChars(uData))
            {
                throw new ArgumentException("illegal XML character: " + text);
            }

            receiver.Characters(uData, Loc.NONE, ReceiverOption.NONE);
        }

        public override void WriteChars(char[] buffer, int index, int count)
        {
            WriteString(new string(buffer ?? throw new ArgumentNullException(nameof(buffer)), index, count));
        }

        // CDATA is just characters to a tree pipeline.
        public override void WriteCData(string text)
        {
            WriteString(text);
        }

        public override void WriteWhitespace(string ws)
        {
            WriteString(ws);
        }

        public override void WriteCharEntity(char ch)
        {
            WriteString(ch.ToString());
        }

        public override void WriteSurrogateCharEntity(char lowChar, char highChar)
        {
            WriteString(new string(new[] { highChar, lowChar }));
        }

        // No raw passthrough exists in a tree pipeline; raw text is treated as character content.
        public override void WriteRaw(string data)
        {
            WriteString(data);
        }

        public override void WriteRaw(char[] buffer, int index, int count)
        {
            WriteChars(buffer, index, count);
        }

        public override void WriteBase64(byte[] buffer, int index, int count)
        {
            WriteString(Convert.ToBase64String(buffer ?? throw new ArgumentNullException(nameof(buffer)), index, count));
        }

        public override void WriteEntityRef(string name)
        {
            throw new NotSupportedException("WriteEntityRef");
        }

        public override void WriteComment(string text)
        {
            FlushStartTag();
            ReleaseHalf();
            text = text == null ? "" : StringTool.WithoutHalfPairs(text);
            UnicodeString uData = StringView.Of(text);
            if (!IsValidChars(uData))
            {
                throw new ArgumentException("Invalid XML character in comment: " + text);
            }

            // a comment or an instruction from data cannot end itself and go on as markup
            if (text.Contains("--") || text.EndsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException("A comment cannot contain '--' or end with '-'");
            }

            receiver.Comment(uData, Loc.NONE, ReceiverOption.NONE);
        }

        public override void WriteProcessingInstruction(string name, string text)
        {
            CheckName(name, "processing instruction name");
            if ("xml".Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid processing instruction name" + Err.Wrap(name));
            }

            text = text == null ? "" : StringTool.WithoutHalfPairs(text);
            if (text.Contains("?>"))
            {
                throw new ArgumentException("A processing instruction cannot contain '?>'");
            }

            FlushStartTag();
            ReleaseHalf();
            UnicodeString uData = StringView.Of(text);
            if (!IsValidChars(uData))
            {
                throw new ArgumentException("Invalid character in PI data: " + text);
            }

            receiver.ProcessingInstruction(name, uData, Loc.NONE, ReceiverOption.NONE);
        }

        public override string LookupPrefix(string ns)
        {
            NamespaceUri uri = NamespaceUri.Of(ns ?? throw new ArgumentNullException(nameof(ns)));
            return uri == NamespaceUri.XMLNS ? "xmlns" : PrefixInScope(uri, false);
        }

        // Normal finish: implicitly ends the document and closes the pipeline. May throw.
        public override void Close()
        {
            if (closed)
            {
                return;
            }

            ReleaseHalf();
            if (depth >= 0)
            {
                WriteEndDocument();
            }

            closed = true;
            receiver.Close();
        }

        // Dispose without Close releases quietly (no final events — an exception path must not
        // look like a successful finish).
        protected override void Dispose(bool disposing)
        {
            if (disposing && !closed)
            {
                closed = true;
                receiver.Dispose();
            }
        }

        public override void Flush()
        {
        }

        private bool IsValidChars(UnicodeString text)
        {
            return !isChecking || (UTF16CharacterSet.FirstInvalidChar(text.CodePoints(), charChecker) == -1);
        }

        private sealed class Triple
        {
            public string prefix;
            public NamespaceUri uri;
            public string local;
            public string value;
        }

        private sealed class StartTag
        {
            public readonly Triple elementName = new Triple();
            public readonly List<Triple> attributes = new List<Triple>();
            public readonly List<KeyValuePair<string, NamespaceUri>> bindings = new List<KeyValuePair<string, NamespaceUri>>();   // prefix -> namespace, as this tag declares them
        }
    }
}
