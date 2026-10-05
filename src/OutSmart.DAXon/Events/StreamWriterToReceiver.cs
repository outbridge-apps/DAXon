////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Core;
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
    public class StreamWriterToReceiver : XmlWriter
    {
        private StartTag pendingTag = null;
        private readonly Stack<NamespaceMap> namespaceStack = new Stack<NamespaceMap>();
        private readonly IReceiver receiver;
        private readonly IIntPredicateProxy charChecker;
        private bool isChecking = false;
        private int depth = -1;
        private bool closed = false;
        private readonly NamespaceReducer inScopeNamespaces;

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
            // The NamespaceReducer maintains the namespace context, eliminates duplicate
            // declarations, and adds declarations needed by element/attribute prefix-uri pairs.
            PipelineConfiguration pipe = receiver.GetPipelineConfiguration();
            this.inScopeNamespaces = new NamespaceReducer(receiver);
            this.namespaceStack.Push(NamespaceMap.EmptyMap());
            this.receiver = inScopeNamespaces;
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

            if (pendingTag != null)
            {
                CompleteTriple(pendingTag.elementName, false);
                foreach (Triple t in pendingTag.attributes)
                {
                    CompleteTriple(t, true);
                }

                INodeName elemName;
                if (pendingTag.elementName.uri.IsEmpty())
                {
                    elemName = new NoNamespaceName(pendingTag.elementName.local);
                }
                else
                {
                    elemName = new FingerprintedQName(pendingTag.elementName.prefix, pendingTag.elementName.uri, pendingTag.elementName.local);
                }

                NamespaceMap nsMap = namespaceStack.Peek();
                if (!pendingTag.elementName.uri.IsEmpty())
                {
                    nsMap = nsMap.Put(pendingTag.elementName.prefix, pendingTag.elementName.uri);
                }

                foreach (Triple t in pendingTag.namespaces)
                {
                    if (t.prefix == null)
                    {
                        t.prefix = "";
                    }

                    if (t.uri == null)
                    {
                        t.uri = NamespaceUri.NULL;
                    }

                    if (!t.uri.IsEmpty())
                    {
                        nsMap = nsMap.Put(t.prefix, t.uri);
                    }
                }

                IAttributeMap attributes = EmptyAttributeMap.GetInstance();
                foreach (Triple t in pendingTag.attributes)
                {
                    INodeName attName;
                    if (t.uri.IsEmpty())
                    {
                        attName = new NoNamespaceName(t.local);
                    }
                    else
                    {
                        attName = new FingerprintedQName(t.prefix, t.uri, t.local);
                        nsMap = nsMap.Put(t.prefix, t.uri);
                    }

                    attributes = attributes.Put(new AttributeInfo(attName, BuiltInAtomicType.UNTYPED_ATOMIC, t.value, Loc.NONE, ReceiverOption.NONE));
                }

                receiver.StartElement(elemName, Untyped.INSTANCE, attributes, nsMap, Loc.NONE, ReceiverOption.NONE);
                pendingTag = null;
                namespaceStack.Push(nsMap);
            }
        }

        private void CompleteTriple(Triple t, bool isAttribute)
        {
            if (t.local == null)
            {
                throw new InvalidOperationException("Local name of " + (isAttribute ? "Attribute" : "Element") + " is missing");
            }

            if (isChecking && !IsValidNCName(t.local))
            {
                throw new InvalidOperationException("Local name of " + (isAttribute ? "Attribute" : "Element") + Err.Wrap(t.local) + " is invalid");
            }

            if (t.uri == null)
            {
                t.uri = NamespaceUri.NULL;
            }

            if (isChecking && !t.uri.IsEmpty() && IsInvalidURI(t.uri.ToString()))
            {
                throw new InvalidOperationException("Namespace URI " + Err.Wrap(t.local) + " is invalid");
            }

            // Null prefix: derive one. An explicit "" prefix means the default namespace for an
            // element, but an attribute in a namespace always needs a real prefix.
            if (t.prefix == null)
            {
                t.prefix = t.uri.IsEmpty() ? "" : GetPrefixForUri(t.uri);
            }
            else if (t.prefix.Length == 0 && isAttribute && !t.uri.IsEmpty())
            {
                t.prefix = GetPrefixForUri(t.uri);
            }
        }

        private string GetPrefixForUri(NamespaceUri uri)
        {
            if (pendingTag != null)
            {
                foreach (Triple t in pendingTag.namespaces)
                {
                    if (uri.Equals(t.uri))
                    {
                        return t.prefix == null ? "" : t.prefix;
                    }
                }
            }

            IEnumerator<string> prefixes = inScopeNamespaces.IteratePrefixes();
            while (prefixes.MoveNext())
            {
                string p = prefixes.Current;
                if (uri.Equals(inScopeNamespaces.GetURIForPrefix(p, false)))
                {
                    return p;
                }
            }

            return "";
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
            CheckNonNull(localName);
            FlushStartTag();
            ReleaseHalf();
            depth++;
            pendingTag = new StartTag();
            pendingTag.elementName.local = localName;
            pendingTag.elementName.uri = NamespaceUri.Of(ns ?? "");
            pendingTag.elementName.prefix = prefix;
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
            CheckNonNull(localName);
            if (pendingTag == null)
            {
                throw new InvalidOperationException("Cannot write attribute when not in a start tag");
            }

            if (pendingAttribute != null)
            {
                throw new InvalidOperationException("WriteStartAttribute while already inside an attribute");
            }

            attributeValue.Length = 0;
            pendingAttribute = new Triple();
            // xmlns declarations arrive through the attribute API: xmlns:p="uri" (prefix "xmlns")
            // or xmlns="uri" (local name "xmlns", no prefix).
            if (prefix == "xmlns" || NamespaceUri.XMLNS.ToString().Equals(ns))
            {
                pendingAttributeIsNamespaceDecl = true;
                pendingAttribute.prefix = prefix == "xmlns" ? localName : "";
            }
            else if (string.IsNullOrEmpty(prefix) && localName == "xmlns" && string.IsNullOrEmpty(ns))
            {
                pendingAttributeIsNamespaceDecl = true;
                pendingAttribute.prefix = "";
            }
            else
            {
                pendingAttributeIsNamespaceDecl = false;
                pendingAttribute.prefix = prefix;
                pendingAttribute.uri = NamespaceUri.Of(ns ?? "");
                pendingAttribute.local = localName;
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
            if (pendingAttributeIsNamespaceDecl)
            {
                pendingAttribute.uri = NamespaceUri.Of(value);
                pendingTag.namespaces.Add(pendingAttribute);
            }
            else
            {
                pendingAttribute.value = value;
                pendingTag.attributes.Add(pendingAttribute);
            }

            pendingAttribute = null;
        }

        public override void WriteString(string text)
        {
            CheckNonNull(text);
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
            CheckNonNull(buffer);
            WriteString(new string(buffer, index, count));
        }

        // CDATA is just characters to a tree pipeline.
        public override void WriteCData(string text)
        {
            CheckNonNull(text);
            WriteString(text);
        }

        public override void WriteWhitespace(string ws)
        {
            CheckNonNull(ws);
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
            CheckNonNull(buffer);
            WriteString(Convert.ToBase64String(buffer, index, count));
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

            if (isChecking && text.Contains("--"))
            {
                throw new ArgumentException("Comment contains '--'");
            }

            receiver.Comment(uData, Loc.NONE, ReceiverOption.NONE);
        }

        public override void WriteProcessingInstruction(string name, string text)
        {
            CheckNonNull(name);
            text = text == null ? "" : StringTool.WithoutHalfPairs(text);
            FlushStartTag();
            ReleaseHalf();
            UnicodeString uData = StringView.Of(text);
            if (isChecking)
            {
                if (!IsValidNCName(name) || "xml".Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Invalid PITarget: " + name);
                }

                if (!IsValidChars(uData))
                {
                    throw new ArgumentException("Invalid character in PI data: " + text);
                }
            }

            receiver.ProcessingInstruction(name, uData, Loc.NONE, ReceiverOption.NONE);
        }

        public override string LookupPrefix(string ns)
        {
            NamespaceUri uri = NamespaceUri.Of(ns);
            if (pendingTag != null)
            {
                foreach (Triple t in pendingTag.namespaces)
                {
                    if (uri.Equals(t.uri))
                    {
                        return t.prefix == null ? "" : t.prefix;
                    }
                }
            }

            IEnumerator<string> prefixes = inScopeNamespaces.IteratePrefixes();
            while (prefixes.MoveNext())
            {
                string p = prefixes.Current;
                if (uri.Equals(inScopeNamespaces.GetURIForPrefix(p, false)))
                {
                    return p;
                }
            }

            return null;
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

        private bool IsValidNCName(string name)
        {
            return !isChecking || NameChecker.IsValidNCName(name);
        }

        private bool IsValidChars(UnicodeString text)
        {
            return !isChecking || (UTF16CharacterSet.FirstInvalidChar(text.CodePoints(), charChecker) == -1);
        }

        private bool IsInvalidURI(string uri)
        {
            return isChecking && !StandardURIChecker.GetInstance().IsValidURI(uri);
        }

        private void CheckNonNull(object value)
        {
            if (value == null)
            {
                throw new NullReferenceException();
            }
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
            public Triple elementName;
            public IList<Triple> attributes;
            public IList<Triple> namespaces;
            public StartTag()
            {
                elementName = new Triple();
                attributes = new List<Triple>();
                namespaces = new List<Triple>();
            }
        }
    }
}
