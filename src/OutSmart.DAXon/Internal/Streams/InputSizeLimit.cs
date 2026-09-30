using System;
using System.IO;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Internal.Streams
{
    /// <summary>
    /// Enforces the Processor's MaxInputBytes on resolver-fetched resources (doc/document/
    /// collection/unparsed-text/json-doc and compile-time includes): a seekable source is
    /// rejected up front by its exact length; an unseekable one (e.g. an HTTP response)
    /// aborts as soon as the running byte count crosses the cap. The error carries the
    /// retrieval-failure code of the requesting function (FODC0002 for documents,
    /// FOUT1170 for text), so xsl:try can catch it like any other fetch failure.
    /// </summary>
    internal static class InputSizeLimit
    {
        /// <summary>
        /// The cap of the configuration's resource policy, or no cap when the configuration was
        /// not built by a Processor (the engine's own internal configurations).
        /// </summary>
        public static long MaxFor(OutSmart.DAXon.Core.Configuration config)
        {
            return config?.ResourcePolicy?.MaxInputBytes ?? long.MaxValue;
        }

        /// <summary>
        /// Reject a string that is over the cap. The cap is in BYTES, so a character count is
        /// only a lower bound: measure UTF-8 once the cheap test passes (round B2 - comparing
        /// chars against a byte limit let multi-byte text through at 2-3x the declared cap).
        /// </summary>
        public static void CheckString(string content, long max, string uri, string errorCode)
        {
            if (content == null || max == long.MaxValue)
            {
                return;
            }

            // UTF-8 never uses fewer bytes than chars, so over the limit in chars is over it in bytes
            long bytes = content.Length > max ? content.Length : System.Text.Encoding.UTF8.GetByteCount(content);
            if (bytes > max)
            {
                throw Oversized(bytes, max, uri, errorCode);
            }
        }

        public static System.IO.TextReader Apply(System.IO.TextReader reader, long max, string uri, string errorCode)
        {
            return reader == null || max == long.MaxValue ? reader : new CappedTextReader(reader, max, uri, errorCode);
        }

        public static System.IO.Stream Apply(System.IO.Stream stream, long max, string uri, string errorCode)
        {
            if (stream == null || max == long.MaxValue)
            {
                return stream;
            }

            long length = -1;
            if (stream.CanSeek)
            {
                try
                {
                    length = stream.Length;
                }
                catch (NotSupportedException)
                {
                }
            }

            if (length >= 0)
            {
                if (length > max)
                {
                    stream.Dispose();
                    throw Oversized(length, max, uri, errorCode);
                }

                return stream;   // exact length known and under the cap - no wrapper needed
            }

            return new CappedStream(stream, max, uri, errorCode);
        }

        internal static XPathException Oversized(long size, long max, string uri, string errorCode)
        {
            string sizePart = size >= 0 ? " (" + size + " bytes)" : "";
            return new XPathException("Input resource" + sizePart + " exceeds the Processor's MaxInputBytes limit of "
                + max + " bytes: " + uri).WithErrorCode(errorCode);
        }

        // Read-only pass-through counting wrapper for sources whose length is unknown up front.
        private sealed class CappedStream : System.IO.Stream
        {
            private readonly System.IO.Stream inner;
            private readonly long max;
            private readonly string uri;
            private readonly string errorCode;
            private long count;

            internal CappedStream(System.IO.Stream inner, long max, string uri, string errorCode)
            {
                this.inner = inner;
                this.max = max;
                this.uri = uri;
                this.errorCode = errorCode;
            }

            public override int Read(byte[] buffer, int offset, int length)
            {
                int n = inner.Read(buffer, offset, length);
                if (n > 0 && (count += n) > max)
                {
                    throw Oversized(-1, max, uri, errorCode);
                }

                return n;
            }

            public override int ReadByte()
            {
                int b = inner.ReadByte();
                if (b >= 0 && ++count > max)
                {
                    throw Oversized(-1, max, uri, errorCode);
                }

                return b;
            }

            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => count; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int length) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        // A TextReader has no length, so the cap is enforced as it is consumed. The budget is in
        // bytes, so each char is charged its UTF-8 width; a surrogate pair is one 4-byte unit,
        // charged when the low half arrives.
        private sealed class CappedTextReader : System.IO.TextReader
        {
            private readonly System.IO.TextReader inner;
            private readonly long max;
            private readonly string uri;
            private readonly string errorCode;
            private long count;
            private bool pendingHighSurrogate;

            internal CappedTextReader(System.IO.TextReader inner, long max, string uri, string errorCode)
            {
                this.inner = inner;
                this.max = max;
                this.uri = uri;
                this.errorCode = errorCode;
            }

            public override int Peek()
            {
                return inner.Peek();
            }

            public override int Read()
            {
                int c = inner.Read();
                if (c >= 0)
                {
                    Charge((char)c);
                }

                return c;
            }

            public override int Read(char[] buffer, int index, int count)
            {
                int n = inner.Read(buffer, index, count);
                for (int i = 0; i < n; i++)
                {
                    Charge(buffer[index + i]);
                }

                return n;
            }

            private void Charge(char c)
            {
                if (pendingHighSurrogate)
                {
                    pendingHighSurrogate = false;
                    if (char.IsLowSurrogate(c))
                    {
                        // the pair costs 4 bytes; 3 were charged for the high half
                        count += 1;
                        Verify();
                        return;
                    }
                }

                if (char.IsHighSurrogate(c))
                {
                    pendingHighSurrogate = true;
                }

                count += c < 0x80 ? 1 : c < 0x800 ? 2 : 3;
                Verify();
            }

            private void Verify()
            {
                if (count > max)
                {
                    throw Oversized(count, max, uri, errorCode);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
