////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Json
{
    internal sealed class JsonReceiver : IReceiver
    {
        private static readonly string ERR_INPUT = "FOJS0006";

        private static readonly Func<int, bool> isControlChar = (c) => c < 31 || (c >= 127 && c <= 159);

        // Shared immutable JSON punctuation tokens: the emitter used to allocate a fresh BMPString for
        // every ",", quote, brace etc. — 6-8 allocations per element on large documents.
        private static readonly UnicodeString TOK_COMMA = BMPString.Of(",");
        private static readonly UnicodeString TOK_QUOTE = BMPString.Of("\"");
        private static readonly UnicodeString TOK_COLON = BMPString.Of(":");
        private static readonly UnicodeString TOK_COLON_SPACED = BMPString.Of(" : ");
        private static readonly UnicodeString TOK_LSQB = BMPString.Of("[");
        private static readonly UnicodeString TOK_LSQB_SP = BMPString.Of("[ ");
        private static readonly UnicodeString TOK_RSQB = BMPString.Of("]");
        private static readonly UnicodeString TOK_RSQB_SP = BMPString.Of(" ]");
        private static readonly UnicodeString TOK_LCURLY = BMPString.Of("{");
        private static readonly UnicodeString TOK_LCURLY_SP = BMPString.Of("{ ");
        private static readonly UnicodeString TOK_RCURLY = BMPString.Of("}");
        private static readonly UnicodeString TOK_RCURLY_SP = BMPString.Of(" }");
        private static readonly UnicodeString TOK_NULL = BMPString.Of("null");
        private static readonly UnicodeString TOK_TRUE = BMPString.Of("true");
        private static readonly UnicodeString TOK_FALSE = BMPString.Of("false");
        private static readonly UnicodeString TOK_NEWLINE = BMPString.Of("\n");
        private readonly IXPathContext context;
        private PipelineConfiguration pipe;
        private IUniStringConsumer output;
        private readonly StringBuilder textBuffer = new StringBuilder(128);

        // Single-text-chunk fast path: the first Characters event of an element is retained as-is and
        // only spilled into textBuffer when a second chunk arrives. Retention is safe because this
        // receiver is only ever fed by a walk over an already-materialized (immutable) tree — the
        // xml-to-json argument is a node, never a live push pipeline with reusable buffers.
        private UnicodeString pendingChunk;
        private readonly Stack<string> stack = new Stack<string>();   // local names of open elements
        private bool atStart = true;
        private bool indenting = false;
        private bool escaped = false;
        private readonly Stack<KeyChecker> keyChecker = new Stack<KeyChecker>();
        private readonly List<KeyChecker> spareKeyCheckers = new List<KeyChecker>();
        private IFunctionItem numberFormatter = null;

        public IFunctionItem NumberFormatter
        {
            get => this.numberFormatter; set
            {
                this.numberFormatter = value;
            }
        }
        public JsonReceiver(PipelineConfiguration pipe, IXPathContext context, IUniStringConsumer output)
        {
            if (pipe == null)
                throw new NullReferenceException();
            if (output == null)
                throw new NullReferenceException();
            SetPipelineConfiguration(pipe);
            this.output = output;
            this.context = context;
        }

        public void SetPipelineConfiguration(PipelineConfiguration pipe)
        {
            this.pipe = pipe;
        }

        public PipelineConfiguration GetPipelineConfiguration()
        {
            return pipe;
        }

        public void SetSystemId(string systemId)
        {
        }

        public void SetIndenting(bool indenting)
        {
            this.indenting = indenting;
        }

        public void Open()
        {
            output.Open();
        }

        public void StartDocument(int properties)
        {
        }

        public void EndDocument()
        {
        }

        public void SetUnparsedEntity(string name, string systemID, string publicID)
        {
        }

        public void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            string local = elemName.GetLocalPart();
            string parent = stack.Count == 0 ? null : stack.Peek();
            bool inMap = "map".Equals(parent) || stack.Count == 0;
            stack.Push(local);

            if (!elemName.HasURI(NamespaceUri.FN))
            {
                throw new XPathException("xml-to-json: element found in wrong namespace: " + elemName.GetStructuredQName().EQName, ERR_INPUT);
            }

            string key = null;
            string escapedAtt = null;
            string escapedKey = null;
            foreach (AttributeInfo att in attributes)
            {
                INodeName attName = att.GetNodeName();
                if (attName.HasURI(NamespaceUri.NULL))
                {
                    switch (attName.GetLocalPart())
                    {
                        case "key":
                            if (!inMap)
                            {
                                throw new XPathException("xml-to-json: The key attribute is allowed only on elements within a map", ERR_INPUT);
                            }

                            key = att.Value;
                            break;
                        case "escaped-key":
                            if (!inMap)
                            {
                                throw new XPathException("xml-to-json: The escaped-key attribute is allowed only on elements within a map", ERR_INPUT);
                            }

                            escapedKey = att.Value;
                            break;
                        case "escaped":
                            bool allowed = stack.Count == 1 || local.Equals("string");

                            // See bugs 29917 and 30077: at the top level, the escaped attribute is ignored
                            // whatever element it appears on
                            if (!allowed)
                            {
                                throw new XPathException("xml-to-json: The escaped attribute is allowed only on the <string> element", ERR_INPUT);
                            }

                            escapedAtt = att.Value;
                            break;
                        default:
                            throw new XPathException("xml-to-json: Disallowed attribute in input: " + attName.DisplayName, ERR_INPUT);
                    }
                }
                else if (attName.HasURI(NamespaceUri.FN))
                {
                    throw new XPathException("xml-to-json: Disallowed attribute in input: " + attName.DisplayName, ERR_INPUT);
                } // Attributes in other namespaces are ignored
            }

            StartEntry(local, parent, inMap, key, escapedAtt, escapedKey);
        }

        // Direct-walk entry (JsonTreeWalker): same contract as StartElement with the name and the
        // three recognised attributes already extracted - including the same placement validation
        // the attribute loop above performs.
        internal void StartEntryDirect(string local, string key, string escapedAtt, string escapedKey)
        {
            string parent = stack.Count == 0 ? null : stack.Peek();
            bool inMap = "map".Equals(parent) || stack.Count == 0;
            stack.Push(local);
            if (key != null && !inMap)
            {
                throw new XPathException("xml-to-json: The key attribute is allowed only on elements within a map", ERR_INPUT);
            }

            if (escapedKey != null && !inMap)
            {
                throw new XPathException("xml-to-json: The escaped-key attribute is allowed only on elements within a map", ERR_INPUT);
            }

            if (escapedAtt != null && !(stack.Count == 1 || local.Equals("string")))
            {
                throw new XPathException("xml-to-json: The escaped attribute is allowed only on the <string> element", ERR_INPUT);
            }

            StartEntry(local, parent, inMap, key, escapedAtt, escapedKey);
        }

        // Shared core of StartElement/StartEntryDirect: emits the separator and key, validates the
        // parent/child relationship and dispatches on the element kind. The caller has already
        // pushed `local` onto the stack.
        private void StartEntry(string local, string parent, bool inMap, string key, string escapedAtt, string escapedKey)
        {
            if (!atStart)
            {
                output.Accept(TOK_COMMA);
                if (indenting)
                {
                    Indent(stack.Count);
                }
            }

            if (inMap && keyChecker.Count > 0)
            {
                if (key == null)
                {
                    throw new XPathException("xml-to-json: Child elements of <map> must have a key attribute", ERR_INPUT);
                }

                bool alreadyEscaped = false;
                if (escapedKey != null)
                {
                    try
                    {
                        alreadyEscaped = StringConverter.StringToBoolean.INSTANCE.ConvertString(StringView.Tidy(escapedKey)).AsAtomic().EffectiveBooleanValue();
                    }
                    catch (XPathException e)
                    {
                        throw new XPathException("xml-to-json: Value of escaped-key attribute '" + Err.Wrap(escapedKey) + "' is not a valid xs:boolean", ERR_INPUT);
                    }
                }

                key = (alreadyEscaped ? HandleEscapedString(key) : EscapeText(key));
                string normalizedKey = alreadyEscaped ? Unescape(key) : key;
                bool added = keyChecker.Peek().Add(normalizedKey);
                if (!added)
                {
                    throw new XPathException("xml-to-json: duplicate key value " + Err.Wrap(key), ERR_INPUT);
                }

                output.Accept(TOK_QUOTE).Accept(StringView.Of(key)).Accept(TOK_QUOTE).Accept(indenting ? TOK_COLON_SPACED : TOK_COLON);
            }

            CheckParent(local, parent);
            switch (local)
            {
                case "array":
                    if (indenting)
                    {
                        Indent(stack.Count);
                        output.Accept(TOK_LSQB_SP);
                    }
                    else
                    {
                        output.Accept(TOK_LSQB);
                    }

                    atStart = true;
                    break;
                case "map":
                    if (indenting)
                    {
                        Indent(stack.Count);
                        output.Accept(TOK_LCURLY_SP);
                    }
                    else
                    {
                        output.Accept(TOK_LCURLY);
                    }

                    atStart = true;
                    keyChecker.Push(TakeKeyChecker());
                    break;
                case "null":

                    output.Accept(TOK_NULL);
                    atStart = false;
                    break;
                case "string":
                    if (escapedAtt != null)
                    {
                        try
                        {
                            escaped = StringConverter.StringToBoolean.INSTANCE.ConvertString(StringView.Tidy(escapedAtt)).AsAtomic().EffectiveBooleanValue();
                        }
                        catch (XPathException e)
                        {
                            throw new XPathException("xml-to-json: value of escaped attribute (" + escaped + ") is not a valid xs:boolean", ERR_INPUT);
                        }
                    }


                    atStart = false;
                    break;
                case "boolean":
                case "number":

                    atStart = false;
                    break;
                default:
                    throw new XPathException("xml-to-json: unknown element <" + local + ">", ERR_INPUT);
            }

            textBuffer.Length = 0;
            pendingChunk = null;
        }

        // The one "Escape(s, false, false, isControlChar) leaves it alone" rule behind both fast paths.
        // CONSERVATIVE: 0x1F counts as special though Escape keeps it raw - it only takes the slow path.
        // Past the Framework JIT's inlining size limit, so it showed as its own 0.9% frame without the hint.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static bool IsPlainJsonChar(int c)
        {
            return c >= 32 && c != '\\' && c != '"' && c != '/' && (c < 127 || c > 159);
        }

        // 8-bit strings are scanned on their bytes: a virtual CodePointAt per character was 1.5% of Trans
        private static bool IsCleanString(UnicodeString s)
        {
            if (s is Slice8 slice)
            {
                return IsCleanBytes(slice.ByteArray, slice.Start, slice.End);
            }

            if (s is Twine8 twine)
            {
                return IsCleanBytes(twine.ByteArray, 0, twine.ByteArray.Length);
            }

            long len = s.Length();
            for (long i = 0; i < len; i++)
            {
                if (!IsPlainJsonChar(s.CodePointAt(i)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCleanBytes(byte[] bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (!IsPlainJsonChar(bytes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        // Escape(s, false, false, isControlChar) without a delegate call per character on a clean string
        private static string EscapeText(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (!IsPlainJsonChar(s[i]))
                {
                    return Escape(s, false, false, isControlChar);
                }
            }

            return s;
        }

        private void CheckParent(string child, string parent)
        {
            if ("null".Equals(parent) || "string".Equals(parent) || "number".Equals(parent) || "boolean".Equals(parent))
            {
                throw new XPathException("xml-to-json: " + Err.IndefiniteArticleFor(child, true) + " " + Err.Wrap(child, Err.ELEMENT) + " element cannot appear as a child of " + Err.Wrap(parent, Err.ELEMENT), ERR_INPUT);
            }
        }

        public void EndElement()
        {
            string local = stack.Pop();
            // Single-text-chunk fast path: the usual element carries exactly one Characters event, kept
            // in pendingChunk as its original UnicodeString — no StringBuilder round-trip, and a clean
            // <string> value is emitted below without ever materializing a System.String.
            UnicodeString uContent = pendingChunk ?? StringView.Tidy(textBuffer.ToString());
            pendingChunk = null;
            if (local.Equals("string") && !escaped && IsCleanString(uContent))
            {
                output.Accept(TOK_QUOTE).Accept(uContent).Accept(TOK_QUOTE);
                textBuffer.Length = 0;
                escaped = false;
                atStart = false;
                return;
            }

            string content = uContent.ToString();
            if (local.Equals("boolean"))
            {
                try
                {
                    bool b = StringConverter.StringToBoolean.INSTANCE.ConvertString(uContent).AsAtomic().EffectiveBooleanValue();
                    output.Accept(b ? TOK_TRUE : TOK_FALSE);
                }
                catch (XPathException e)
                {
                    throw new XPathException("xml-to-json: Value of <boolean> element is not a valid xs:boolean", ERR_INPUT);
                }
            }
            else if (local.Equals("number"))
            {
                if (numberFormatter == null)
                {
                    try
                    {
                        double d = StringToDouble11.GetInstance().StringToNumber(uContent);
                        if (double.IsNaN(d) || double.IsInfinity(d))
                        {
                            throw new XPathException("xml-to-json: Infinity and NaN are not allowed", ERR_INPUT);
                        }

                        output.Accept(new DoubleValue(d).UnicodeStringValue);
                    }
                    catch (FormatException e)
                    {
                        throw new XPathException("xml-to-json: Invalid number: " + uContent, ERR_INPUT);
                    }
                }
                else
                {
                    ISequence result = SystemFunction.DynamicCall(numberFormatter, context, new StringValue(uContent));
                    output.Accept(result.Head().UnicodeStringValue);
                }
            }
            else if (local.Equals("string"))
            {
                output.Accept(TOK_QUOTE);
                if (escaped)
                {
                    output.Accept(StringView.Of(HandleEscapedString(content)));
                }
                else
                {
                    output.Accept(StringView.Of(EscapeText(content)));
                }

                output.Accept(TOK_QUOTE);
            }
            else if (!Whitespace.IsAllWhite(uContent))
            {
                throw new XPathException("xml-to-json: Element " + local + " must have no text content", ERR_INPUT);
            }

            textBuffer.Length = 0;
            escaped = false;
            if (local.Equals("array"))
            {
                output.Accept(indenting ? TOK_RSQB_SP : TOK_RSQB);
            }
            else if (local.Equals("map"))
            {
                spareKeyCheckers.Add(keyChecker.Pop());
                output.Accept(indenting ? TOK_RCURLY_SP : TOK_RCURLY);
            }

            atStart = false;
        }

        private static string HandleEscapedString(string str)
        {

            // check that escape sequences are valid
            Unescape(str);
            StringBuilder @out = new StringBuilder(str.Length * 2);
            bool afterEscapeChar = false;
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (c == '"' && !afterEscapeChar)
                {
                    @out.Append("\\\"");
                }
                else if (c < 32 || (c >= 127 && c < 160))
                {
                    if (c == '\b')
                    {
                        @out.Append("\\b");
                    }
                    else if (c == '\f')
                    {
                        @out.Append("\\f");
                    }
                    else if (c == '\n')
                    {
                        @out.Append("\\n");
                    }
                    else if (c == '\r')
                    {
                        @out.Append("\\r");
                    }
                    else if (c == '\t')
                    {
                        @out.Append("\\t");
                    }
                    else
                    {
                        @out.Append("\\u");
                        @out.Append(Hex4(c));
                    }
                }
                else if (c == '/' && !afterEscapeChar)
                {
                    @out.Append("\\/");
                }
                else
                {
                    @out.AppendCodePoint(c);
                }

                afterEscapeChar = c == '\\' && !afterEscapeChar;
            }

            return @out.ToString();
        }

        public static string Escape(string @in, bool retainQuot, bool retainSlash, Func<int, bool> hexEscapes)
        {
            // Fast path: most keys/values need no escaping at all — return the input unchanged
            // instead of copying it through a StringBuilder.
            bool clean = true;
            for (int i = 0; i < @in.Length; i++)
            {
                char c = @in[i];
                if (c == '\\' || c == '\b' || c == '\f' || c == '\n' || c == '\r' || c == '\t'
                    || (c == '"' && !retainQuot) || (c == '/' && !retainSlash) || hexEscapes(c))
                {
                    clean = false;
                    break;
                }
            }

            if (clean)
            {
                return @in;
            }

            StringBuilder @out = new StringBuilder(@in.Length);
            for (int i = 0; i < @in.Length; i++)
            {
                int c = @in[i];
                switch (c)
                {
                    case '"':
                        @out.Append(retainQuot ? "\"" : "\\\"");
                        break;
                    case '\b':
                        @out.Append("\\b");
                        break;
                    case '\f':
                        @out.Append("\\f");
                        break;
                    case '\n':
                        @out.Append("\\n");
                        break;
                    case '\r':
                        @out.Append("\\r");
                        break;
                    case '\t':
                        @out.Append("\\t");
                        break;
                    case '/':
                        @out.Append(retainSlash ? "/" : "\\/"); // spec bug 29665, saxon bug 2849
                        break;
                    case '\\':
                        @out.Append("\\\\");
                        break;
                    default:
                        if (hexEscapes(c))
                        {
                            @out.Append("\\u");
                            @out.Append(Hex4(c));
                        }
                        else
                        {
                            @out.AppendCodePoint(c);
                        }

                        break;
                }
            }

            return @out.ToString();
        }

        private static string Hex4(int c)
        {
            return c.ToString("X4"); // uppercase, zero-padded to 4 — same as the old pad loop
        }
        public void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (stack.Count > 0 && !Whitespace.IsAllWhite(chars))
            {
                string local = stack.Peek();
                if (local.Equals("map") || local.Equals("array"))
                {
                    throw new XPathException("xml-to-json: Element " + local + " must have no text content", ERR_INPUT);
                }
            }

            if (pendingChunk == null && textBuffer.Length == 0)
            {
                pendingChunk = chars;
            }
            else
            {
                if (pendingChunk != null)
                {
                    textBuffer.Append(pendingChunk.ToString());
                    pendingChunk = null;
                }

                textBuffer.Append(chars.ToString());
            }
        }

        public void ProcessingInstruction(string name, UnicodeString data, ILocation locationId, int properties)
        {
        }

        public void Comment(UnicodeString content, ILocation locationId, int properties)
        {
        }

        public void Close()
        {
            if (output != null)
            {
                output.Close();
                output = null;
            }
        }

        public void Dispose()
        {
            if (output != null)
            {
                output.Dispose();
                output = null;
            }
        }

        public bool UsesTypeAnnotations()
        {
            return false;
        }

        public string GetSystemId()
        {
            return null;
        }

        private void Indent(int depth)
        {
            output.Accept(TOK_NEWLINE);
            for (int i = 0; i < depth; i++)
            {
                output.Accept(StringConstants.SINGLE_SPACE);
            }
        }

        private static string Unescape(string literal)
        {
            if (literal.IndexOf('\\') < 0)
            {
                return literal;
            }

            StringBuilder buffer = new StringBuilder(literal.Length);
            for (int i = 0; i < literal.Length; i++)
            {
                char c = literal[i];
                if (c == '\\')
                {
                    if (i++ == literal.Length - 1)
                    {
                        throw new XPathException("String '" + Err.Wrap(literal) + "' ends in backslash ", "FOJS0007");
                    }

                    switch (literal[i])
                    {
                        case '"':
                            buffer.Append('"');
                            break;
                        case '\\':
                            buffer.Append('\\');
                            break;
                        case '/':
                            buffer.Append('/');
                            break;
                        case 'b':
                            buffer.Append('\b');
                            break;
                        case 'f':
                            buffer.Append('\f');
                            break;
                        case 'n':
                            buffer.Append('\n');
                            break;
                        case 'r':
                            buffer.Append('\r');
                            break;
                        case 't':
                            buffer.Append('\t');
                            break;
                        case 'u':
                            try
                            {
                                string hex = literal.Substring(i + 1, 4) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                                int code = Convert.ToInt32(hex, 16);
                                buffer.Append((char)code);
                                i += 4;
                            }
                            catch (Exception e)
                            {
                                throw new XPathException("Invalid hex escape sequence in string '" + Err.Wrap(literal) + "'", "FOJS0007");
                            }

                            break;
                        default:
                            int next = literal[i];
                            string xx = next < 256 ? next + "" : "x" + (next).ToString("x");
                            throw new XPathException("Unknown escape sequence \\" + xx, "FOJS0007");
                    }
                }
                else
                {
                    buffer.Append(c);
                }
            }

            return buffer.ToString();
        }

        private KeyChecker TakeKeyChecker()
        {
            int n = spareKeyCheckers.Count;
            if (n == 0)
            {
                return new KeyChecker();
            }

            KeyChecker kc = spareKeyCheckers[n - 1];
            spareKeyCheckers.RemoveAt(n - 1);
            kc.Reset();
            return kc;
        }

        // Duplicate-key detector. Real-document maps hold a handful of keys, where a linear scan
        // over a small array beats hashing every key; larger maps spill into a HashSet. Finished
        // checkers go on a free list (spareKeyCheckers), so a run allocates one checker per map
        // NESTING LEVEL rather than one HashSet per map.
        private sealed class KeyChecker
        {
            private const int SpillAt = 16;
            private readonly string[] small = new string[SpillAt];
            private int count;
            private HashSet<string> spill;

            public bool Add(string key)
            {
                if (spill != null)
                {
                    return spill.Add(key);
                }

                string[] s = small;
                for (int i = 0; i < count; i++)
                {
                    if (string.Equals(s[i], key))
                    {
                        return false;
                    }
                }

                if (count == SpillAt)
                {
                    spill = new HashSet<string>(small);
                    return spill.Add(key);
                }

                s[count++] = key;
                return true;
            }

            public void Reset()
            {
                count = 0;
                spill = null;
            }
        }

        // Upstream Receiver defaults: this receiver takes the xml-to-json element vocabulary as
        // events, never composed items.
        public void Append(IItem item, ILocation locationId, int properties) => throw new InvalidOperationException("The xml-to-json receiver does not accept composed items");
        public void Append(IItem item) => throw new InvalidOperationException("The xml-to-json receiver does not accept composed items");
        public bool HandlesAppend() => false;
    }
}