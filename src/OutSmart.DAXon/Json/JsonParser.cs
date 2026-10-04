////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Json
{
    /// <summary>
    /// Parser for JSON, which notifies parsing events to a JsonHandler
    /// </summary>
    internal sealed class JsonParser
    {
        public const int ESCAPE = 1;
        public const int ALLOW_ANY_TOP_LEVEL = 2;
        public const int LIBERAL = 4;
        public const int VALIDATE = 8;
        public const int DEBUG = 16;
        public const int DUPLICATES_RETAINED = 32;
        public const int DUPLICATES_LAST = 64;
        public const int DUPLICATES_FIRST = 128;
        public const int DUPLICATES_REJECTED = 256;
        public const int DUPLICATES_SPECIFIED = DUPLICATES_FIRST | DUPLICATES_LAST | DUPLICATES_RETAINED | DUPLICATES_REJECTED;
        private static readonly string ERR_GRAMMAR = "FOJS0001";
        private static readonly string ERR_DUPLICATE = "FOJS0003";
        private static readonly string ERR_SCHEMA = "FOJS0004";
        private static readonly string ERR_OPTIONS = "FOJS0005";
        private static readonly string ERR_LIMITS = "FOJS0001"; // No specific code in spec
        private IFunctionItem numberParser = null;
        /// <summary>
        /// Create a JSON parser
        /// </summary>
        public JsonParser()
        {
        }

        public void Parse(string input, int flags, JsonHandler handler, IXPathContext context)
        {
            if ((input.Length == 0))
            {
                InvalidJSON("An empty string is not valid JSON", ERR_GRAMMAR, 1);
            }

            JsonTokenizer t = new JsonTokenizer(input);
            t.Next();
            try
            {
                ParseConstruct(handler, t, flags, context);
            }
            catch (InvalidOperationException e)
            {

                // e.g. unmatched surrogate pairs
                InvalidJSON(e.Message, ERR_GRAMMAR, t.lineNumber);
            }

            if (t.Next() != JsonToken.EOF)
            {
                InvalidJSON("Unexpected token beyond end of JSON input", ERR_GRAMMAR, t.lineNumber);
            }
        }

        public static int GetFlags(Dictionary<string, IGroundedValue> options, bool allowValidate, bool isSchemaAware)
        {
            int flags = 0;
            BooleanValue debug = options.ContainsKey("debug") ? (BooleanValue)options.GetOrDefault("debug") : null;
            if (debug != null && debug.GetBooleanValue())
            {
                flags |= DEBUG;
            }

            BooleanValue escape = options.ContainsKey("escape") ? (BooleanValue)options.GetOrDefault("escape") : null;
            if (escape != null && escape.GetBooleanValue())
            {
                flags |= ESCAPE;
                if (options.ContainsKey("fallback") && options.GetOrDefault("fallback") != null)
                {
                    throw new XPathException("Cannot specify a fallback function when escape=true", "FOJS0005");
                }
            }

            BooleanValue liberal = options.ContainsKey("liberal") ? (BooleanValue)options.GetOrDefault("liberal") : null;
            if (liberal != null && liberal.GetBooleanValue())
            {
                flags |= LIBERAL;
                flags |= ALLOW_ANY_TOP_LEVEL;
            }

            bool validate = false;
            if (allowValidate)
            {
                validate = options.ContainsKey("validate") && ((BooleanValue)options.GetOrDefault("validate")).GetBooleanValue();
                if (validate)
                {
                    if (!isSchemaAware)
                    {
                        Error("Requiring validation on non-schema-aware processor", ERR_SCHEMA);
                    }

                    flags |= VALIDATE;
                }
            }

            if (options.ContainsKey("duplicates"))
            {
                string duplicates = ((StringValue)options.GetOrDefault("duplicates")).GetStringValue();
                switch (duplicates)
                {
                    case "reject":
                        flags |= DUPLICATES_REJECTED;
                        break;
                    case "use-last":
                        flags |= DUPLICATES_LAST;
                        break;
                    case "use-first":
                        flags |= DUPLICATES_FIRST;
                        break;
                    case "retain":
                        flags |= DUPLICATES_RETAINED;
                        break;
                    default:
                        Error("Invalid value for 'duplicates' option", ERR_OPTIONS);
                        break;
                }

                if (validate && duplicates == "retain")
                {
                    Error("The options validate:true and duplicates:retain cannot be used together", ERR_OPTIONS);
                }
            }

            return flags;
        }

        private void ParseConstruct(JsonHandler handler, JsonTokenizer tokenizer, int flags, IXPathContext context)
        {
            bool debug = (flags & DEBUG) != 0;
            if (debug)
            {
                Console.Error.WriteLine("token:" + tokenizer.currentToken + " :" + tokenizer.TokenValue());
            }

            JsonToken tok = tokenizer.currentToken;
            switch (tok)
            {
                case JsonToken.LCURLY:
                    ParseObject(handler, tokenizer, flags, context);
                    break;
                case JsonToken.LSQB:
                    ParseArray(handler, tokenizer, flags, context);
                    break;
                case JsonToken.NUMERIC_LITERAL:
                    string lexical = tokenizer.TokenValue();
                    AtomicValue d = ParseNumericLiteral(lexical, flags, tokenizer.lineNumber, context);
                    handler.WriteNumeric(lexical, d);
                    break;
                case JsonToken.TRUE:
                    handler.WriteBoolean(true);
                    break;
                case JsonToken.FALSE:
                    handler.WriteBoolean(false);
                    break;
                case JsonToken.NULL:
                    handler.WriteNull();
                    break;
                case JsonToken.STRING_LITERAL:
                    string literal = tokenizer.TokenValue();
                    handler.WriteString(Unescape(literal, flags, ERR_GRAMMAR, tokenizer.lineNumber));
                    break;
                default:
                    InvalidJSON("Unexpected symbol: " + tokenizer.TokenValue(), ERR_GRAMMAR, tokenizer.lineNumber);
                    break;
            }
        }

        private void ParseObject(JsonHandler handler, JsonTokenizer tokenizer, int flags, IXPathContext context)
        {
            // Depth grows only through container recursion; the guard adapts to the executing
            // thread's stack (Java relies on catching StackOverflowError instead). The parent's
            // catch converts to the "too deeply nested" FOJS0001 that Java reports.
            StackGuard.Probe();
            bool liberal = (flags & LIBERAL) != 0;
            handler.StartMap();
            JsonToken tok = tokenizer.Next();
            while (tok != JsonToken.RCURLY)
            {
                if (tok != JsonToken.STRING_LITERAL && !(tok == JsonToken.UNQUOTED_STRING && liberal))
                {
                    InvalidJSON("Property name must be a string literal (found " + ShowToken(tok, tokenizer.TokenValue() + ")"), ERR_GRAMMAR, tokenizer.lineNumber);
                }

                string key = tokenizer.TokenValue();
                key = Unescape(key, flags, ERR_GRAMMAR, tokenizer.lineNumber);
                string reEscaped = handler.ReEscape(key);
                tok = tokenizer.Next();
                if (tok != JsonToken.COLON)
                {
                    InvalidJSON("Missing colon after \"" + Err.Wrap(key) + "\"", ERR_GRAMMAR, tokenizer.lineNumber);
                }

                tokenizer.Next();
                bool duplicate = handler.SetKey(key, reEscaped);
                if (duplicate && ((flags & DUPLICATES_REJECTED) != 0))
                {
                    InvalidJSON("Duplicate key value \"" + Err.Wrap(key) + "\"", ERR_DUPLICATE, tokenizer.lineNumber);
                }

                try
                {
                    if (!duplicate || ((flags & (DUPLICATES_LAST | DUPLICATES_RETAINED)) != 0))
                    {
                        ParseConstruct(handler, tokenizer, flags, context);
                    }
                    else
                    {

                        // retain first: parse the duplicate value but discard it
                        JsonHandler h2 = new JsonHandler();
                        h2.Context = context;
                        ParseConstruct(h2, tokenizer, flags, context);
                    }
                }
                catch (RecursionDepthError e) when (!e.Described)
                {
                    // Described, not thrown as FOJS0001 here: parse-json can be called from inside a
                    // recursive template, and an XPathException would unwind through the engine
                    // stack above it. The code still reaches the host (round BC).
                    throw e.Describe("Objects are too deeply nested", ERR_LIMITS, null);
                }

                tok = tokenizer.Next();
                if (tok == JsonToken.COMMA)
                {
                    tok = tokenizer.Next();
                    if (tok == JsonToken.RCURLY)
                    {
                        if (liberal)
                        {
                            break; // tolerate the trailing comma
                        }
                        else
                        {
                            InvalidJSON("Trailing comma after entry in object", ERR_GRAMMAR, tokenizer.lineNumber);
                        }
                    }
                }
                else if (tok == JsonToken.RCURLY)
                {
                    break;
                }
                else
                {
                    InvalidJSON("Unexpected token after value of \"" + Err.Wrap(key) + "\" property", ERR_GRAMMAR, tokenizer.lineNumber);
                }
            }

            handler.EndMap();
        }

        private void ParseArray(JsonHandler handler, JsonTokenizer tokenizer, int flags, IXPathContext context)
        {
            // Same stack-adaptive depth guard as ParseObject.
            StackGuard.Probe();
            bool liberal = (flags & LIBERAL) != 0;
            handler.StartArray();
            JsonToken tok = tokenizer.Next();
            if (tok == JsonToken.RSQB)
            {
                handler.EndArray();
                return;
            }

            while (true)
            {
                try
                {
                    ParseConstruct(handler, tokenizer, flags, context);
                }
                catch (RecursionDepthError e) when (!e.Described)
                {
                    // Described, not thrown as FOJS0001 here — see ParseObject.
                    throw e.Describe("Arrays are too deeply nested", ERR_LIMITS, null);
                }

                tok = tokenizer.Next();
                if (tok == JsonToken.COMMA)
                {
                    tok = tokenizer.Next();
                    if (tok == JsonToken.RSQB)
                    {
                        if (liberal)
                        {
                            break; // tolerate the trailing comma
                        }
                        else
                        {
                            InvalidJSON("Trailing comma after entry in array", ERR_GRAMMAR, tokenizer.lineNumber);
                        }
                    }
                }
                else if (tok == JsonToken.RSQB)
                {
                    break;
                }
                else
                {
                    InvalidJSON("Unexpected token (" + ShowToken(tok, tokenizer.TokenValue()) + ") after entry in array", ERR_GRAMMAR, tokenizer.lineNumber);
                }
            }

            handler.EndArray();
        }

        private AtomicValue ParseNumericLiteral(string token, int flags, int lineNumber, IXPathContext context)
        {
            try
            {
                if ((flags & LIBERAL) == 0)
                {

                    // extra checks on the number disabled by choosing spec="liberal"
                    if (token.StartsWith("+", StringComparison.Ordinal))
                    {
                        InvalidJSON("Leading + sign not allowed: " + token, ERR_GRAMMAR, lineNumber);
                    }
                    else
                    {
                        string t = token;
                        if (t.StartsWith("-", StringComparison.Ordinal))
                        {
                            t = t.Substring(1);
                        }

                        if (t.StartsWith("0", StringComparison.Ordinal) && !(t.Equals("0") || t.StartsWith("0.", StringComparison.Ordinal) || t.StartsWith("0e", StringComparison.Ordinal) || t.StartsWith("0E", StringComparison.Ordinal)))
                        {
                            InvalidJSON("Redundant leading zeroes not allowed: " + token, ERR_GRAMMAR, lineNumber);
                        }

                        if (t.EndsWith(".", StringComparison.Ordinal) || t.Contains(".e") || t.Contains(".E"))
                        {
                            InvalidJSON("Empty fractional part not allowed", ERR_GRAMMAR, lineNumber);
                        }

                        if (t.StartsWith(".", StringComparison.Ordinal))
                        {
                            InvalidJSON("Empty integer part not allowed", ERR_GRAMMAR, lineNumber);
                        }
                    }
                }

                if (numberParser != null)
                {
                    ISequence[] args = new ISequence[1];
                    args[0] = new StringValue(token);
                    ISequence result = SystemFunction.DynamicCall(numberParser, context, args).Head();
                    return (AtomicValue)result.Head();
                }
                else
                {
                    return new DoubleValue(StringToDouble.GetInstance().StringToNumber(StringView.Tidy(token)));
                }
            }
            catch (FormatException e)
            {
                InvalidJSON("Invalid numeric literal: " + e.Message, ERR_GRAMMAR, lineNumber);
                return DoubleValue.NaN;
            }
        }

        public static string Unescape(string literal, int flags, string errorCode, int lineNumber)
        {
            if (literal.IndexOf('\\') < 0)
            {
                return literal;
            }

            bool liberal = (flags & LIBERAL) != 0;
            StringBuilder buffer = new StringBuilder(literal.Length);
            for (int i = 0; i < literal.Length; i++)
            {
                char c = literal[i];
                if (c == '\\')
                {
                    if (i++ == literal.Length - 1)
                    {
                        throw new XPathException("Invalid JSON escape: String " + Err.Wrap(literal) + " ends in backslash", errorCode);
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
                                string hex = literal.Substring(i + 1, 4);
                                int code = Convert.ToInt32(hex, 16);
                                buffer.Append((char)code);
                                i += 4;
                            }
                            catch (Exception e)
                            {
                                if (liberal)
                                {
                                    buffer.Append("\\u");
                                }
                                else
                                {
                                    throw new XPathException("Invalid JSON escape: \\u must be followed by four hex characters", errorCode);
                                }
                            }

                            break;
                        default:
                            if (liberal)
                            {
                                buffer.Append(literal[i]);
                            }
                            else
                            {
                                char next = literal[i];
                                string xx = next < 256 ? next + "" : "x" + ((int)(next)).ToString("x", CultureInfo.InvariantCulture);
                                throw new XPathException("Unknown escape sequence \\" + xx, errorCode);
                            }

                            break;
                    }
                }
                else
                {
                    buffer.Append(c);
                }
            }

            return buffer.ToString();
        }

        private static void Error(string message, string code)
        {
            throw new XPathException(message, code);
        }

        private static void InvalidJSON(string message, string code, int lineNumber)
        {
            Error("Invalid JSON input on line " + lineNumber + ": " + message, code);
        }

        public static string ShowToken(JsonToken token, string currentTokenValue)
        {
            switch (token)
            {
                case JsonToken.LSQB:
                    return "[";
                case JsonToken.RSQB:
                    return "]";
                case JsonToken.LCURLY:
                    return "{";
                case JsonToken.RCURLY:
                    return "}";
                case JsonToken.STRING_LITERAL:
                    return "string (\"" + currentTokenValue + "\")";
                case JsonToken.NUMERIC_LITERAL:
                    return "number (" + currentTokenValue + ")";
                case JsonToken.TRUE:
                    return "true";
                case JsonToken.FALSE:
                    return "false";
                case JsonToken.NULL:
                    return "null";
                case JsonToken.COLON:
                    return ":";
                case JsonToken.COMMA:
                    return ",";
                case JsonToken.EOF:
                    return "<eof>";
                default:
                    return "<" + token + ">";
            }
        }

        public void SetNumberParser(Dictionary<string, IGroundedValue> options, IXPathContext context)
        {
            ISequence val = options.ContainsKey("number-parser") ? options.GetOrDefault("number-parser") : null;
            if (val != null)
            {
                IItem fn = val.Head();
                if (fn is IFunctionItem)
                {
                    numberParser = (IFunctionItem)fn;
                    if (numberParser.GetArity() != 1)
                    {
                        throw new XPathException("Number-parser function must have arity=1", "FOJS0005");
                    }

                    SpecificFunctionType required = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_STRING }, SequenceType.SINGLE_ATOMIC);
                    if (!required.Matches(numberParser, context.GetConfiguration().GetTypeHierarchy()))
                    {
                        throw new XPathException("Number-parser function does not match the required type", "FOJS0005");
                    }
                }
                else
                {
                    throw new XPathException("Value of option 'number-parser' is not a function", "FOJS0005");
                }
            }
        }

        internal enum JsonToken
        {
            LSQB,
            RSQB,
            LCURLY,
            RCURLY,
            STRING_LITERAL,
            NUMERIC_LITERAL,
            TRUE,
            FALSE,
            NULL,
            COLON,
            COMMA,
            UNQUOTED_STRING,
            EOF
        }

        /// <summary>
        /// Inner class to do the tokenization
        /// </summary>
        private sealed class JsonTokenizer
        {
            public readonly string input;
            public int position;
            public int lineNumber = 1;
            public JsonToken currentToken;
            public StringBuilder currentTokenValue = new StringBuilder(64);
            // Zero-copy token value: string/number/name tokens without escapes are delivered as a
            // substring of the input (no per-char StringBuilder); null = value lives in the builder.
            public string currentTokenString;

            public string TokenValue()
            {
                return currentTokenString ?? currentTokenValue.ToString();
            }
            public JsonTokenizer(string input)
            {
                this.input = input;
                this.position = 0;

                // Ignore a leading BOM
                if (!(input.Length == 0) && input[0] == 65279)
                {
                    position++;
                }
            }

            public JsonToken Next()
            {
                currentToken = ReadToken();
                return currentToken;
            }

            private JsonToken ReadToken()
            {
                // Invariant D3: the number of tokens is chosen by the INPUT, and this is the single
                // funnel every one of them passes through. Nothing else on the JSON path looked at
                // the clock, so a large document ran to completion regardless of the deadline -
                // both through JsonBuilder and through parse-json()/json-doc() inside a transform.
                // The check is self-throttling (Controller samples the clock once per stride).
                Controller.CheckActiveTimeout();
                if (position >= input.Length)
                {
                    return JsonToken.EOF;
                }

                bool breakLoop = false;
                do
                {
                    char c = input[position];
                    switch (c)
                    {
                        case '\n':
                        case '\r':
                            if (!(c == '\n' && position > 0 && input[position] == '\n'))
                            {
                                lineNumber++;
                            }


                            // drop through
                            goto case ' ';
                        case ' ':
                        case '\t':
                            if (++position >= input.Length)
                            {
                                return JsonToken.EOF;
                            }

                            break;
                        default:
                            breakLoop = true;
                            break;
                    }
                }
                while (!breakLoop);
                char ch = input[position++];
                switch (ch)
                {
                    case '[':
                        return JsonToken.LSQB;
                    case '{':
                        return JsonToken.LCURLY;
                    case ']':
                        return JsonToken.RSQB;
                    case '}':
                        return JsonToken.RCURLY;
                    case '"':
                        // Fast scan: most literals contain no escapes and no control characters,
                        // so the token is just a substring of the input -- no StringBuilder at all.
                        int litStart = position;
                        while (true)
                        {
                            if (position >= input.Length)
                            {
                                InvalidJSON("Unclosed quotes in string literal", ERR_GRAMMAR, lineNumber);
                            }

                            char fc = input[position];
                            if (fc == '"')
                            {
                                currentTokenString = input.Substring(litStart, position - litStart);
                                position++;
                                return JsonToken.STRING_LITERAL;
                            }

                            if (fc == '\\' || fc < 32)
                            {
                                break;   // rare: escape or control char -> the general loop below
                            }

                            position++;
                        }

                        currentTokenString = null;
                        currentTokenValue.Length = 0;
                        currentTokenValue.Append(input, litStart, position - litStart);
                        bool afterBackslash = false;
                        while (true)
                        {
                            if (position >= input.Length)
                            {
                                InvalidJSON("Unclosed quotes in string literal", ERR_GRAMMAR, lineNumber);
                            }

                            char c = input[position++];
                            if (c < 32)
                            {
                                InvalidJSON("Unescaped control character (x" + ((int)(c)).ToString("x", CultureInfo.InvariantCulture) + ")", ERR_GRAMMAR, lineNumber);
                            }

                            if (afterBackslash && c == 'u')
                            {
                                try
                                {
                                    string hex = input.Substring(position, 4);
                                    Convert.ToInt32(hex, 16);
                                }
                                catch (Exception e)
                                {
                                    InvalidJSON("\\u must be followed by four hex characters", ERR_GRAMMAR, lineNumber);
                                }
                            }

                            if (c == '"' && !afterBackslash)
                            {
                                break;
                            }
                            else
                            {
                                currentTokenValue.Append(c);
                                afterBackslash = c == '\\' && !afterBackslash;
                            }
                        }

                        return JsonToken.STRING_LITERAL;
                    case ':':
                        return JsonToken.COLON;
                    case ',':
                        return JsonToken.COMMA;
                    case '-':
                    case '+':
                    case '.':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        int numStart = position - 1;
                        if (position < input.Length)
                        {

                            // We could be in ECMA mode when there is a single digit
                            while (true)
                            {
                                char c = input[position];
                                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                                {
                                    if (++position >= input.Length)
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }

                        currentTokenString = input.Substring(numStart, position - numStart);
                        return JsonToken.NUMERIC_LITERAL;
                    default:
                        {

                            // Allow unquoted strings in liberal mode
                            if (NameChecker.IsNCNameChar(ch))
                            {
                                int nameStart = position - 1;
                                while (position < input.Length)
                                {
                                    char c = input[position];
                                    if (NameChecker.IsNCNameChar(c))
                                    {
                                        position++;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                string val = input.Substring(nameStart, position - nameStart);
                                currentTokenString = val;
                                switch (val)
                                {
                                    case "true":
                                        return JsonToken.TRUE;
                                    case "false":
                                        return JsonToken.FALSE;
                                    case "null":
                                        return JsonToken.NULL;
                                    default:
                                        return JsonToken.UNQUOTED_STRING;
                                }
                            }
                            else
                            {
                                char c = input[--position];
                                string s = UTF16CharacterSet.IsSurrogate(c) ? "" : " '" + c + "'";
                                InvalidJSON("Unexpected character" + s + " (\\u" + ((int)(c)).ToString("x", CultureInfo.InvariantCulture) + ") at position " + position, ERR_GRAMMAR, lineNumber);
                                return JsonToken.EOF;
                            }
                        }

                        break;
                }
            }
        }
    }
}