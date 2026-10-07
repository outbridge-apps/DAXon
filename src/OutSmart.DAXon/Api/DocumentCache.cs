////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.IO;
using OutSmart.DAXon.Internal.Caching;

namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// Thread-safe, bounded cache of parsed documents, for hosts that transform the same
    /// input repeatedly. A cached XdmNode is immutable and safe to share across threads;
    /// each transformation must still use its own Xslt30Transformer (Load30 per call).
    ///
    /// The cache is bound to a Processor: cached nodes carry that Processor's name pool and
    /// may only be fed to transformations created from the same Processor.
    ///
    /// Correctness of the keys:
    ///  - file entries are keyed by full path + last-write time + length, so a modified
    ///    file is re-parsed on the next call; the superseded tree ages out by LFU eviction;
    ///  - content entries hold the document text itself in the key, so a hash collision
    ///    can never serve the wrong document (equality is a full comparison).
    ///
    /// A concurrent miss on the same key may parse twice; the trees are equivalent and the
    /// last one wins - correct, at the cost of transiently duplicated work.
    ///
    /// Inputs larger than the Processor's memory limit (ProcessorOptions.MaxMemoryBytes, 500 MB
    /// by default) are rejected with DAXonApiException before parsing, so one oversized
    /// document cannot exhaust the host's memory.
    /// </summary>
    public sealed class DocumentCache
    {
        private readonly Processor processor;
        private readonly ClockCache<Key, XdmNode> cache;
        private readonly long maxInputBytes;

        public DocumentCache(Processor processor, int capacity = 8)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            this.processor = processor;
            this.cache = new ClockCache<Key, XdmNode>(capacity);
            this.maxInputBytes = processor.Options.InputCap;
        }

        /// <summary>
        /// Parse an XML file, or return the cached tree if the file (by full path,
        /// last-write time and length) has been parsed before.
        /// </summary>
        public XdmNode GetOrParseFile(string path)
        {
            try
            {
                string full = Path.GetFullPath(path);
                var info = new FileInfo(full);
                if (!info.Exists)
                {
                    throw new FileNotFoundException("Document not found: " + full, full);
                }

                if (info.Length > maxInputBytes)
                {
                    throw new DAXonApiException(
                        $"Input document too large: {full} is {info.Length} bytes, exceeds the memory limit of {maxInputBytes} bytes (ProcessorOptions.MaxMemoryBytes)");
                }

                // Windows paths: normalize case in the key so the same file hits the same entry
                var key = Key.ForFile(full.ToUpperInvariant(), info.LastWriteTimeUtc.Ticks, info.Length);
                return cache.GetOrAdd(key, _ =>
                {
                    using (var s = OutSmart.DAXon.Internal.Streams.FileNames.OpenRead(full))
                    {
                        return processor.NewDocumentBuilder().Build(s, new Uri(full).AbsoluteUri);
                    }
                });
            }
            catch (Exception e) when (DAXonApiException.IsIO(e))
            {
                // A file that cannot be read - missing, held, named as the file system will not have it - as
                // DocumentBuilder.Build(path) reports it: SXXP0003, the reason in the message, the cause inside.
                throw DAXonApiException.FromIO(e, path);
            }
        }

        /// <summary>
        /// Parse an XML document supplied as a string, or return the cached tree for
        /// identical content and base URI.
        /// </summary>
        public XdmNode GetOrParseContent(string content, string baseUri)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            // Round B2: the limit is in BYTES. Comparing content.Length (characters) against it
            // let multi-byte UTF-8 through at 2-3x the declared cap.
            long bytes = content.Length > maxInputBytes
                ? content.Length
                : System.Text.Encoding.UTF8.GetByteCount(content);
            if (bytes > maxInputBytes)
            {
                throw new DAXonApiException(
                    $"Input document too large: content is {bytes} bytes, exceeds the memory limit of {maxInputBytes} bytes (ProcessorOptions.MaxMemoryBytes)");
            }

            var key = Key.ForContent(content, baseUri);
            return cache.GetOrAdd(key, _ =>
            {
                using (var r = new StringReader(content))
                {
                    return processor.NewDocumentBuilder().Build(r, baseUri);
                }
            });
        }

        private sealed class Key
        {
            private readonly string text;      // full path (file) or document content
            private readonly string baseUri;   // content entries only
            private readonly long ticks;       // file entries only
            private readonly long length;
            private readonly bool isFile;
            private readonly int hash;

            private Key(string text, string baseUri, long ticks, long length, bool isFile)
            {
                this.text = text;
                this.baseUri = baseUri;
                this.ticks = ticks;
                this.length = length;
                this.isFile = isFile;
                this.hash = ComputeHash();
            }

            internal static Key ForFile(string fullPath, long ticks, long length)
            {
                return new Key(fullPath, null, ticks, length, true);
            }

            internal static Key ForContent(string content, string baseUri)
            {
                return new Key(content, baseUri, 0, content.Length, false);
            }

            // For content keys the hash samples the ends of the text plus its length; a
            // colliding sample only makes Equals run its full comparison, never a wrong hit
            private int ComputeHash()
            {
                unchecked
                {
                    int h = isFile ? 17 : 23;
                    h = h * 31 + length.GetHashCode();
                    h = h * 31 + ticks.GetHashCode();
                    int n = text.Length;
                    int sample = Math.Min(n, 4096);
                    for (int i = 0; i < sample; i++)
                    {
                        h = h * 31 + text[i];
                    }

                    for (int i = Math.Max(sample, n - 4096); i < n; i++)
                    {
                        h = h * 31 + text[i];
                    }

                    return h;
                }
            }

            public override int GetHashCode() => hash;

            public override bool Equals(object obj)
            {
                var other = obj as Key;
                if (other == null || isFile != other.isFile || ticks != other.ticks || length != other.length || hash != other.hash)
                {
                    return false;
                }

                return string.Equals(baseUri, other.baseUri, StringComparison.Ordinal)
                    && string.Equals(text, other.text, StringComparison.Ordinal);
            }
        }
    }
}
