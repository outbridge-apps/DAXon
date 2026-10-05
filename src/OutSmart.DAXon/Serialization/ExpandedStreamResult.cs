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
using OutSmart.DAXon.Internal.Charsets;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
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

                // Java always writes a bom for UTF-16, so if the user doesn't want one, use utf16-be
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
                return new UnicodeWriterToWriter(writer);
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
                outputStream = new FileStream(file, FileMode.Create, FileAccess.Write);

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
                // A name the file system refuses fails here, with the reason (the caller reports it)
                uri = new Uri(Path.GetFullPath(uriString)).AbsoluteUri;
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

                    using (File.Create(file)) { }
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
                    writer = new StreamWriter(outputStream, dotnetEncoding);
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
                    return new UnicodeWriterToWriter(writer);
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