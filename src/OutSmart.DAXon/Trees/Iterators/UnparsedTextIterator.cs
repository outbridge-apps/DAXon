////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Trees.Iterators
{
    /// <summary>
    /// Iterates over a text resource line by line for fn:unparsed-text-lines (the port of the
    /// upstream TextLinesIterator/UnparsedTextIterator pair). Lines are read LAZILY from the
    /// reader: the resource is never materialized as one big string and the lines are never
    /// materialized as one big list. Each line is checked for XML-valid characters (FOUT1190).
    /// Line endings #xA, #xD, #xD#xA separate lines (TextReader.ReadLine matches the upstream
    /// LineNumberReader definition); a trailing line-ending produces no final empty line.
    /// </summary>
    internal sealed class UnparsedTextIterator : ISequenceIterator
    {
        private TextReader reader;
        private readonly RunResources scope;   // the API call that closes the reader if this iterator does not
        private readonly WeakReference<IDisposable> ticket;
        private readonly IIntPredicateProxy checker;
        private readonly URI uri;
        private int position = 0;   // lines delivered so far; -1 after end

        /// <summary>Streaming form: resolve the URI to a reader and iterate straight off the file.</summary>
        public UnparsedTextIterator(URI absoluteURI, IXPathContext context, string encoding)
        {
            Configuration config = context.GetConfiguration();
            TextReader r = UnparsedTextFunction.OpenText(absoluteURI, encoding, context, "unparsed-text-lines()");
            this.reader = r;
            this.scope = RunResources.Track(this, out ticket);
            this.uri = absoluteURI;
            this.checker = config.ValidCharacterChecker;
        }

        /// <summary>Stable form: iterate over already-fetched (cached) content.</summary>
        public UnparsedTextIterator(TextReader reader, URI absoluteURI, IXPathContext context)
        {
            this.reader = reader;
            this.uri = absoluteURI;
            this.checker = context.GetConfiguration().ValidCharacterChecker;
        }

        public IItem Next()
        {
            if (position < 0)
            {
                Dispose();
                return null;
            }

            if (reader == null)
            {
                throw new XPathException("unparsed-text-lines(): " + uri + " was closed when the evaluation that opened it ended", "FOUT1170");
            }

            string s;
            try
            {
                s = reader.ReadLine();
            }
            catch (InvalidOperationException e)
            {
                // Proxy for a decode failure (see UnparsedTextFunction.ReadFile)
                Dispose();
                throw UnparsedTextFunction.HandleIOError(uri, new IOException(e.Message, e));
            }
            catch (IOException err)
            {
                Dispose();
                throw UnparsedTextFunction.HandleIOError(uri, err);
            }

            if (s == null)
            {
                position = -1;
                Dispose();
                return null;
            }

            if (position == 0 && s.Length > 0 && s[0] == '﻿')
            {
                s = s.Substring(1);
            }

            CheckLine(s);
            position++;
            return new StringValue(s);
        }

        private void CheckLine(string buffer)
        {
            // Fast scan: chars in [0x20..0xD7FF] plus TAB are valid in both XML 1.0 and 1.1,
            // so a clean line needs no checker call at all (avoids an interface call per char).
            int n = buffer.Length;
            int start = 0;
            while (start < n)
            {
                char fc = buffer[start];
                if ((fc >= 0x20 && fc < 0xD800) || fc == '\t')
                {
                    start++;
                    continue;
                }

                break;
            }

            if (start == n)
            {
                return;
            }

            for (int c = start; c < buffer.Length;)
            {
                int ch32 = buffer[c++];
                if (UTF16CharacterSet.IsHighSurrogate(ch32))
                {
                    // Half a pair with no other half after it - only a reader of the host's delivers one: the line
                    // ended there (an index out of range), or the next character was taken for the half and passed
                    // the test with it. The same error unparsed-text() has for it.
                    if (c >= buffer.Length || !UTF16CharacterSet.IsLowSurrogate(buffer[c]))
                    {
                        Dispose();
                        throw new XPathException("The unparsed-text file contains an unpaired surrogate (line=" + position + " column=" + c + " value=hex " + (ch32).ToString("x", CultureInfo.InvariantCulture) + ')').WithErrorCode("FOUT1190");
                    }

                    char low = buffer[c++];
                    ch32 = UTF16CharacterSet.CombinePair((char)ch32, low);
                }

                if (!checker.Test(ch32))
                {
                    Dispose();
                    throw new XPathException("The unparsed-text file contains a character that is illegal in XML (line=" + position + " column=" + (c + 1) + " value=hex " + (ch32).ToString("x", CultureInfo.InvariantCulture) + ')').WithErrorCode("FOUT1190");
                }
            }
        }

        // The scope's long weak ticket still reaches an iterator that awaits its finalizer, so CloseAll and the
        // finalizer can close it at once; whoever takes the reader closes it - two Disposes of a StreamReader race.
        public void Dispose()
        {
            TextReader r = Interlocked.Exchange(ref reader, null);
            if (r != null)
            {
                r.Dispose();
                scope?.Untrack(ticket);
            }

            GC.SuppressFinalize(this);
        }

        ~UnparsedTextIterator()
        {
            Interlocked.Exchange(ref reader, null)?.Dispose();
        }
    }
}
