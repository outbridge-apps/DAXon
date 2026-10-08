////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Text;
using System.IO;
namespace OutSmart.DAXon.Serialization
{
    public class ExpandedStreamResult
    {
        private readonly Configuration config;
        private Properties outputProperties;
        private readonly string systemId;
        // The StreamResult this was expanded from. Kept so that a stream opened HERE (from the
        // system ID) can be published back to it - Serializer.Dispose closes result.GetOutputStream()
        // and says so in as many words ("relies on the fact that the SerializerFactory sets the
        // Stream"), but the port never set it, so a run that failed before the normal pipeline close
        // left its output file open and locked until finalization. Round BG's probe caught it.
        private readonly StreamResult originatingResult;
        private TextWriter writer;
        private System.IO.Stream outputStream;
        private ICharacterSet characterSet;
        private string encoding;
        private bool mustCloseAfterUse = false;
        // set when the writer made here over a stream begins the bytes with their mark itself (see Marked)
        private bool marksItself;

        public virtual TextWriter Writer
        {
            get => writer; set
            {
                this.writer = value;

                // If the writer uses a known encoding, change the encoding in the XML declaration
                // to match. Any encoding actually specified in xsl:output is ignored, because encoding
                // is being done by the user-supplied TextWriter, and not by Saxon itself.
                if (value is StreamWriter && outputProperties != null)
                {
                    string enc = ((StreamWriter)value).Encoding.WebName;
                    outputProperties.SetProperty(DAXonOutputKeys.ENCODING, enc);
                    characterSet = config.GetCharacterSetFactory().GetCharacterSet(outputProperties);
                }
            }
        }

        public virtual ICharacterSet CharacterSet => characterSet;
        public ExpandedStreamResult(Configuration config, StreamResult result, Properties outputProperties)
        {
            this.config = config;
            this.originatingResult = result;
            this.systemId = result.GetSystemId();
            this.writer = result.GetWriter();
            this.outputStream = result.GetOutputStream();
            this.outputProperties = outputProperties;
            this.encoding = outputProperties.GetProperty(DAXonOutputKeys.ENCODING);
            if (encoding == null)
            {
                encoding = "UTF8";
            }
            else if (encoding.Equals("UTF-8", StringComparison.OrdinalIgnoreCase))
            {
                encoding = "UTF8";
            }
            else if (encoding.Equals("UTF-16", StringComparison.OrdinalIgnoreCase))
            {
                encoding = "UTF16";
            }

            if (characterSet == null)
            {
                characterSet = config.GetCharacterSetFactory().GetCharacterSet(encoding);
            }

            string byteOrderMark = outputProperties.GetProperty(DAXonOutputKeys.BYTE_ORDER_MARK);
            if (byteOrderMark == "no" && encoding == "UTF16")
            {

                // UTF-16 that begins with no mark is read as big-endian (RFC 2781), and so it is written
                encoding = "UTF-16BE";
            }
            else if (!(characterSet is UTF8CharacterSet))
            {

                //if (characterSet instanceof PluggableCharacterSet) {
                encoding = characterSet.CanonicalName;
            }
        }

        public virtual IUnicodeWriter ObtainUnicodeWriter()
        {
            if (writer != null)
            {
                // a StreamWriter of the host's whose encoding has a preamble begins its bytes with that mark itself
                return new UnicodeWriterToWriter(writer, writer is StreamWriter own && own.Encoding.GetPreamble().Length > 0);
            }
            else
            {
                System.IO.Stream os = ObtainOutputStream();
                return MakeUnicodeWriterFromOutputStream(os);
            }
        }

        protected virtual System.IO.Stream ObtainOutputStream()
        {
            if (outputStream != null)
            {
                return outputStream;
            }

            string uriString = systemId;
            if (uriString == null)
            {
                throw new XPathException("Result has no system ID, writer, or output stream defined");
            }

            try
            {
                string file = MakeWritableOutputFile(uriString);
                mustCloseAfterUse = true;
                outputStream = OutSmart.DAXon.Internal.Streams.FileNames.Create(file);

                // Publish the opened stream back to the StreamResult this was expanded from, so a
                // failure-path Dispose can actually reach it. The normal close (pipeline completes)
                // closes the same stream first; FileStream.Dispose is idempotent, so both paths are
                // safe in either order.
                originatingResult.SetOutputStream(outputStream);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException
                || e is FormatException || e is System.Security.SecurityException || e is URISyntaxException)
            {
                // Held by another process, refused by the file system for its form, out of reach: one error for a
                // destination that cannot be written, with the system's own reason. A held file was a raw IOException.
                throw new XPathException("Cannot write to " + uriString + ": " + e.Message, e).WithErrorCode(DAXonErrorCode.SXRD0004);
            }

            return outputStream;
        }

        public virtual bool IsMustCloseAfterUse()
        {
            return mustCloseAfterUse;
        }

        public static string MakeWritableOutputFile(string uriString)
        {
            URI uri = new URI(uriString);
            if (!uri.IsAbsolute())
            {
                // A name the file system refuses fails here, with the reason (the caller reports it). A device first,
                // by what it is: its full path, \\.\NUL, is no URI, and the reason given was that of the URI.
                OutSmart.DAXon.Internal.Streams.FileNames.Check(uriString);
                uri = new Uri(Path.GetFullPath(uriString)).AbsoluteUri;
            }

            // Java's File(URI) takes a file: URI and no other. LocalPath gives a path of this machine for any: a name
            // such as "report:2024.xml" is a URI of the scheme "report" and was written as 2024.xml, and
            // http://host/out.xml at the root of the current drive.
            if (!string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("it is not the name of a file: '" + uri.Scheme + ":' reads as the scheme of a URI");
            }

            string file = new Uri(uri.ToString()).LocalPath;
            try
            {
                if (uri.Scheme == "file" && !(File.Exists(file) || Directory.Exists(file)))
                {
                    string directory = Path.GetDirectoryName(file);
                    if (directory != null && !(File.Exists(directory) || Directory.Exists(directory)))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (OutSmart.DAXon.Internal.Streams.FileNames.Create(file)) { }
                }

                if (Directory.Exists(file))
                {
                    throw new XPathException("Cannot write to a directory: " + uriString, DAXonErrorCode.SXRD0004);
                }

                if (File.Exists(file) && new FileInfo(file).IsReadOnly)
                {
                    throw new XPathException("Cannot write to URI " + uriString, DAXonErrorCode.SXRD0004);
                }
            }
            catch (Exception err) when (err is IOException || err is UnauthorizedAccessException)
            {
                throw new XPathException("Failed to create output file " + uri + ": " + err.Message, err).WithErrorCode(DAXonErrorCode.SXRD0004);
            }

            return file;
        }

        public virtual bool UsesWriter()
        {
            return true;
        }

        // The byte order mark of UTF-16 and UTF-32 bytes is what byte-order-mark says: yes is a mark, no is none. When
        // it says nothing they have one, as .NET writes one for each of them - but for UTF-16LE and UTF-16BE, which
        // have none, as in Saxon: the name says the order. UTF-8 under another of its names has one when the parameter
        // says yes, as UTF-8 has. The StreamWriter writes the mark, at the start of a stream and nowhere else, so that
        // no emitter need add it: with one of its own as well, UTF-16LE with "yes" began with two and did not parse.
        private Encoding Marked(Encoding platform)
        {
            string mark = outputProperties.GetProperty(DAXonOutputKeys.BYTE_ORDER_MARK);
            switch (platform.CodePage)
            {
                case 1200:
                case 1201:
                    marksItself = true;
                    return new UnicodeEncoding(platform.CodePage == 1201, mark == "yes" || (mark != "no" && !NamesItsOrder()));
                case 12000:
                case 12001:
                    marksItself = true;
                    return new UTF32Encoding(platform.CodePage == 12001, mark != "no");
                case 65001:
                    marksItself = true;
                    return new UTF8Encoding(mark == "yes");
                default:
                    return platform;
            }
        }

        // Asked of the name the output properties give: the platform knows UTF-16LE and UTF-16 as one encoding.
        private bool NamesItsOrder()
        {
            string name = outputProperties.GetProperty(DAXonOutputKeys.ENCODING);
            return "UTF-16LE".Equals(name, StringComparison.OrdinalIgnoreCase) || "UTF-16BE".Equals(name, StringComparison.OrdinalIgnoreCase);
        }

        private TextWriter MakeWriterFromOutputStream(System.IO.Stream stream)
        {
            outputStream = stream;

            // If the user supplied an Stream, but the Emitter is written to
            // use a TextWriter (this is the most common case), then we create a TextWriter
            // to wrap the supplied Stream; the complications are to ensure that
            // the character encoding is correct.
            try
            {
                if (encoding.Equals("UTF8", StringComparison.OrdinalIgnoreCase))
                {
                    writer = new UTF8Writer(outputStream);
                }
                else
                {
                    Encoding dotnetEncoding = encoding.Equals("iso-646", StringComparison.OrdinalIgnoreCase) || encoding.Equals("iso646", StringComparison.OrdinalIgnoreCase)
                        ? Encoding.ASCII
                        : Encoding.GetEncoding(encoding);
                    writer = new StreamWriter(outputStream, Marked(dotnetEncoding));
                }

                return writer;
            }
            catch (Exception err)
            {
                if (encoding.Equals("UTF8", StringComparison.OrdinalIgnoreCase))
                {
                    throw new XPathException("Failed to create a UTF8 output writer");
                }

                throw new XPathException("Encoding " + encoding + " is not supported", "SESU0007");
            }
        }

        private IUnicodeWriter MakeUnicodeWriterFromOutputStream(System.IO.Stream stream)
        {
            outputStream = stream;
            try
            {
                if (encoding.Equals("UTF8", StringComparison.OrdinalIgnoreCase))
                {
                    return new UTF8Writer(outputStream);
                }
                else
                {
                    TextWriter writer = MakeWriterFromOutputStream(stream);
                    return new UnicodeWriterToWriter(writer, marksItself);
                }
            }
            catch (Exception err)
            {
                if (encoding.Equals("UTF8", StringComparison.OrdinalIgnoreCase))
                {
                    throw new XPathException("Failed to create a UTF8 output writer");
                }

                throw new XPathException("Encoding " + encoding + " is not supported", "SESU0007");
            }
        }

        public virtual System.IO.Stream GetOutputStream()
        {
            return outputStream;
        }
    }
}