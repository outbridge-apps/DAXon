////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Charsets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Functions
{
    internal abstract class UnparsedTextFunction : SystemFunction
    {
        public override int GetSpecialProperties(Expression[] arguments)
        {
            int p = base.GetSpecialProperties(arguments);
            if (GetRetainedStaticContext().GetConfiguration().GetBooleanProperty(Feature<bool>.STABLE_UNPARSED_TEXT))
            {
                return p;
            }
            else
            {
                return p & ~StaticProperty.NO_NODES_NEWLY_CREATED; // Pretend the function is creative to prevent the result going into a global variable,
                // which takes excessive memory. Unless we're caching anyway, for stability reasons.
            }
        }

        public static void ReadFile(URI absoluteURI, string encoding, IUniStringConsumer output, IXPathContext context)
        {
            Configuration config = context.GetConfiguration();
            IIntPredicateProxy checker = config.ValidCharacterChecker;

            // Use the URI machinery to validate and resolve the URIs
            TextReader reader;
            try
            {
                reader = context.GetController().UnparsedTextURIResolver.Resolve(absoluteURI, encoding, config);
            }
            catch (XPathException err)
            {
                err.MaybeSetErrorCode("FOUT1170");
                throw;
            }

            if (reader == null)
            {
                ResourceRequest request = new ResourceRequest();
                request.uri = absoluteURI.ToString();
                request.nature = ResourceRequest.TEXT_NATURE;
                ResolvedResource src = request.Resolve(config.GetResourceResolver(), new DirectResourceResolver(config));
                if (src != null)
                {
                    reader = StandardUnparsedTextResolver.GetReaderFromResolvedResource(src, encoding, config, false);
                }
                else
                {
                    throw new XPathException("unparsed-text(): resolver returned no resource");
                }
            }

            try
            {
                ReadFile(checker, reader, output);
            }
            catch (ArgumentException encErr)
            {
                throw new XPathException("Unknown encoding " + Err.Wrap(encoding), encErr).WithErrorCode("FOUT1190");
            }
            catch (IOException ioErr)
            {

                throw HandleIOError(absoluteURI, ioErr);
            }
        }

        public static URI GetAbsoluteURI(string href, string baseURI, IXPathContext context)
        {
            URI absoluteURI;
            try
            {
                absoluteURI = ResolveURI.MakeAbsolute(href, baseURI);
            }
            catch (URISyntaxException err)
            {
                HandleURISyntaxException(href, baseURI, err);
                return null;
            }

            if (absoluteURI.Fragment != null)
            {
                throw new XPathException("URI for unparsed-text() must not contain a fragment identifier", "FOUT1170");
            }


            // The URL dereferencing classes throw all kinds of strange exceptions if given
            // ill-formed sequences of %hh escape characters. So we do a sanity check that the
            // escaping is well-formed according to UTF-8 rules
            EncodeForUri.CheckPercentEncoding(absoluteURI.ToString());
            return absoluteURI;
        }

        private static void HandleURISyntaxException(string href, string baseURI, URISyntaxException err)
        {
            throw new XPathException(err.GetReason() + ": " + err.GetInput(), err).WithErrorCode("FOUT1170");
        }

        public static XPathException HandleIOError(URI absoluteURI, IOException ioErr)
        {
            string message = "Failed to read input file";
            if (absoluteURI != null && !ioErr.Message.Equals(absoluteURI.ToString()))
            {
                message += ' ' + absoluteURI.ToString();
            }

            message += " (" + ioErr.GetType().FullName + ')';
            return new XPathException(message, ioErr).WithErrorCode(GetErrorCode(ioErr));
        }

        public static string GetErrorCode(IOException ioErr)
        {

            // FOUT1200 should be used when the encoding was inferred, FOUT1190 when it was explicit. We rely on the
            // caller to change FOUT1200 to FOUT1190 when necessary
            if (ioErr is MalformedInputException)
            {
                return "FOUT1200";
            }
            else if (ioErr is UnmappableCharacterException)
            {
                return "FOUT1200";
            }
            else if (ioErr is CharacterCodingException)
            {
                return "FOUT1200";
            }
            else
            {
                return "FOUT1170";
            }
        }

        public static UnicodeString ReadFile(IIntPredicateProxy checker, TextReader reader)
        {
            UnicodeBuilder buffer = new UnicodeBuilder();
            ReadFile(checker, reader, new AnonymousAbstractUniStringConsumer(buffer));
            return buffer.ToUnicodeString();
        }

        /// <summary>
        /// Read the whole reader into a .NET string, for consumers (json-doc) that (a) apply no
        /// XML-character validation and (b) want a string anyway. Mirrors ReadFile's chunked read
        /// loop -- same reader.Read calls, same decode-error wrapping, same leading-BOM strip -- but
        /// appends the raw UTF-16 chars to a StringBuilder instead of building a codepoint
        /// UnicodeString and converting it back. That removes the per-char validity test and the
        /// UnicodeString->string round-trip (int[] fill + Array.ConvertAll on the whole file).
        /// The result is character-identical to ReadFile(...).ToString() for valid input.
        /// </summary>
        public static string ReadFileToString(TextReader reader)
        {
            StringBuilder sb = new StringBuilder();
            char[] buffer = new char[8192];
            bool first = true;
            while (true)
            {
                int actual;
                try
                {
                    actual = reader.Read(buffer, 0, buffer.Length);
                }
                catch (InvalidOperationException e)
                {
                    // Proxy for a decode failure (System.Text.DecoderFallbackException), as in ReadFile
                    throw new IOException(e.Message, e);
                }

                if (IsEndOfFile(actual))
                {
                    break;
                }

                int start = 0;
                if (first)
                {
                    if (buffer[0] == '﻿')
                    {
                        start = 1;
                    }

                    first = false;
                }

                sb.Append(buffer, start, actual - start);
            }

            reader.Dispose();
            return sb.ToString();
        }

        public static void ReadFile(IIntPredicateProxy checker, TextReader reader, IUniStringConsumer output)
        {
            char[] buffer = new char[2048];
            bool first = true;
            int actual;
            int line = 1;
            int column = 1;
            int mask = 0;
            while (true)
            {
                try
                {
                    actual = reader.Read(buffer, 0, buffer.Length);
                }
                catch (InvalidOperationException e)
                {

                    // Proxy for C# System.Text.DecoderFallbackException
                    throw new IOException(e.Message, e);
                }

                if (IsEndOfFile(actual))
                {
                    break;
                }

                for (int c = 0; c < actual;)
                {
                    int ch32 = buffer[c++];
                    if (ch32 == '\n')
                    {
                        line++;
                        column = 0;
                    }

                    column++;
                    mask |= ch32;
                    // [#x20-#xD7FF] is always valid XML 1.0 and holds no surrogate: the common case
                    // (all of ASCII/Latin1-BMP text) skips the per-char virtual checker.Test and the
                    // surrogate probe. Only C0 controls and #xD800+ (surrogates/astral/specials) take
                    // the slow paths; error line/column tracking is unchanged, so messages are identical.
                    if (ch32 >= 0x20 && ch32 < 0xD800)
                    {
                        continue;
                    }

                    if (ch32 < 0x20)
                    {
                        if (ch32 != 0x9 && ch32 != 0xA && ch32 != 0xD)
                        {
                            throw new XPathException("The text file contains a character that is illegal in XML (line=" + line + " column=" + column + " value=hex " + (ch32).ToString("x") + ')').WithErrorCode("FOUT1190");
                        }

                        continue;
                    }

                    if (UTF16CharacterSet.IsHighSurrogate(ch32))
                    {
                        if (c == actual)
                        {

                            // bug 3785, test case fn-unparsed-text-055
                            // We've got a high surrogate right at the end of the buffer.
                            // The path of least resistance is to extend the buffer.
                            char[] buffer2 = new char[2048];
                            int actual2 = reader.Read(buffer2, 0, 2048);
                            if (actual2 <= 0)
                            {
                                // The resource ENDS with the high surrogate: the low-surrogate fetch
                                // below used to read past the array. (Only a custom resolver's reader
                                // can deliver a lone surrogate - the platform decoders emit U+FFFD.)
                                throw new XPathException("The text file contains an unpaired surrogate (line=" + line + " column=" + column + " value=hex " + (ch32).ToString("x") + ')').WithErrorCode("FOUT1190");
                            }

                            char[] buffer3 = new char[actual + actual2];
                            Array.Copy(buffer, 0, buffer3, 0, actual);
                            Array.Copy(buffer2, 0, buffer3, actual, actual2);
                            buffer = buffer3;
                            actual = actual + actual2;
                        }

                        char low = buffer[c++];
                        if (!UTF16CharacterSet.IsLowSurrogate(low))
                        {
                            // Unpaired high surrogate mid-stream: CombinePair would swallow the next
                            // character into a garbage astral codepoint and the checker would pass it.
                            throw new XPathException("The text file contains an unpaired surrogate (line=" + line + " column=" + column + " value=hex " + (ch32).ToString("x") + ')').WithErrorCode("FOUT1190");
                        }

                        ch32 = UTF16CharacterSet.CombinePair((char)ch32, low);
                        mask |= ch32;
                    }

                    if (!checker.Test(ch32))
                    {
                        throw new XPathException("The text file contains a character that is illegal in XML (line=" + line + " column=" + column + " value=hex " + (ch32).ToString("x") + ')').WithErrorCode("FOUT1190");
                    }
                }

                int start = 0;
                if (first)
                {
                    if (buffer[0] == '﻿')
                    {
                        start = 1;
                        actual--;
                    }

                    first = false;
                }

                if (mask <= 0xff)
                {
                    output.Accept(new Twine8(buffer, start, actual));
                }
                else if (mask <= 0xffff)
                {
                    output.Accept(new Twine16(buffer, start, actual));
                }
                else
                {
                    // Astral content: the port's StringView is BMP-only (Length()=UTF-16 units,
                    // CodePointAt splits surrogate pairs), so string-length(unparsed-text(...)) counted
                    // units (2052) instead of codepoints (2048, fn-unparsed-text-055). FromCharSequence
                    // builds a real 24-bit UnicodeString with pairs combined.
                    output.Accept(StringTool.FromCharSequence(new string(buffer, start, actual)));
                }
            }

            reader.Dispose();
        }

        private static bool IsEndOfFile(int bytesRead)
        {
            return bytesRead <= 0;
        }

        private sealed class AnonymousAbstractUniStringConsumer : AbstractUniStringConsumer
        {

            private readonly UnicodeBuilder buffer;
            public AnonymousAbstractUniStringConsumer(UnicodeBuilder buffer)
            {
                this.buffer = buffer;
            }
            public override IUniStringConsumer Accept(UnicodeString chars)
            {
                return buffer.Accept(chars);
            }
        }
    }
}