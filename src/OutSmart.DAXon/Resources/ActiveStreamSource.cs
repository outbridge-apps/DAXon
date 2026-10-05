////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using System.IO;
namespace OutSmart.DAXon.Resources
{
    /// <summary>
    /// A native <see cref="IActiveSource"/> over a byte <see cref="System.IO.Stream"/> or character
    /// <see cref="System.IO.TextReader"/> (or a bare systemId URL). Phase 5 replaced the JAXP
    /// StreamSource/SAXSource hierarchy: this parses straight through <see cref="XmlReaderToReceiver"/>
    /// (a System.Xml.XmlReader pumped into the Receiver pipeline), with no SAX round-trip. External
    /// entities / an external DTD subset resolve through the Configuration's ResourceResolver (wrapped as
    /// a System.Xml.XmlResolver); DTD-STRICT validation maps to <c>ValidationType.DTD</c>.
    /// </summary>
    internal sealed class ActiveStreamSource : IActiveSource
    {
        private readonly Stream byteStream;
        private readonly TextReader charStream;
        private string systemId;

        /// <summary>True when this source carries only a systemId (no byte/char stream) — i.e. it will be opened
        /// from its URL. Used to short-circuit to the document pool when a transformer is reused (bug 4837).</summary>
        public bool IsStreamless
        {
            get { return byteStream == null && charStream == null; }
        }

        public ActiveStreamSource(Stream byteStream, TextReader charStream, string systemId)
        {
            this.byteStream = byteStream;
            this.charStream = charStream;
            this.systemId = systemId;
        }

        // What is left to parse when the input knows it (a file, a buffer, a string), else -1: the tree is
        // sized from it.
        internal long InputLength => byteStream != null ? RemainingLength(byteStream) : RemainingLength(charStream);

        internal static long RemainingLength(Stream stream)
        {
            try
            {
                return stream != null && stream.CanSeek ? stream.Length - stream.Position : -1;
            }
            catch (System.NotSupportedException)
            {
                return -1;
            }
        }

        // StringReader keeps its length to itself, and it is how hosts pass a document they hold as text
        // (the D365 one does). Both runtimes name its fields _s and _pos; without them, no length.
        private static readonly System.Reflection.FieldInfo StringReaderText = typeof(StringReader).GetField("_s", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo StringReaderPosition = typeof(StringReader).GetField("_pos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        internal static long RemainingLength(TextReader reader)
        {
            if (reader is StringReader && StringReaderText != null && StringReaderPosition != null
                && StringReaderText.GetValue(reader) is string text && StringReaderPosition.GetValue(reader) is int position)
            {
                return text.Length - position;
            }

            return -1;
        }

        public void SetSystemId(string systemId)
        {
            this.systemId = systemId;
        }

        public string GetSystemId()
        {
            return systemId;
        }

        public void Deliver(IReceiver receiver, ParseOptions options)
        {
            Configuration config = receiver.GetPipelineConfiguration().GetConfiguration();
            string url = systemId;

            // The whitespace a DTD makes ignorable is told apart only where both the parse and its pipeline leave it
            // out: a stylesheet module is parsed keeping all of it.
            XmlReaderToReceiver.DtdUse dtd = XmlReaderToReceiver.DtdUseFor(options);
            if (dtd == XmlReaderToReceiver.DtdUse.Whitespace && !XmlReaderToReceiver.LeavesOutIgnorable(receiver.GetPipelineConfiguration().GetParseOptions()))
            {
                dtd = XmlReaderToReceiver.DtdUse.None;
            }

            bool dtdValidate = dtd == XmlReaderToReceiver.DtdUse.Validate || dtd == XmlReaderToReceiver.DtdUse.ValidateLax;

            // External entities / an external DTD subset resolve through the config's ResourceResolver. A bare
            // non-validating parse with no external references needs no resolver (null = no external fetch).
            System.Xml.XmlResolver resolver = (dtdValidate || options.EntityResolverClass != null)
                ? new ResourceResolverXmlResolver(config.GetResourceResolver(), config, IsStreamless ? url : null)
                : null;

            try
            {
                using (System.Xml.XmlReader xr = XmlReaderToReceiver.CreateXmlReader(charStream, byteStream, url, resolver, dtd, config, 0, options.GetErrorReporter()))
                {
                    XmlReaderToReceiver.Send(xr, receiver);
                }
            }
            // No XPathException clause: it is not one of the types below, so catching it here only
            // to rethrow was pure cost - and this method sits on every nested include level, where
            // a catch-and-rethrow re-enters exception dispatch and eats stack on the unwind (AW).
            catch (UncheckedXPathException uxpe)
            {
                throw uxpe.GetXPathException();
            }
            catch (System.Exception err) when (Api.DAXonApiException.IsIO(err) || err is System.Net.WebException)
            {
                // An I/O failure (a missing file, a locked one, a directory, a path refused by its form) becomes
                // SXXP0003, which doc-available() turns into false.
                // A malformed-document XmlException is NOT caught here -- it propagates unchanged.
                throw IOFailure(err, url);
            }
        }

        // The reader's reason stays in the message: a host that logs only the message could not tell a missing
        // file from a held one or a directory.
        internal static XPathException IOFailure(System.Exception err, string systemId)
        {
            return new XPathException("I/O error reported by XML parser processing " + systemId + ": " + err.Message, err).WithErrorCode(DAXonErrorCode.SXXP0003);
        }
    }
}
