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

            bool dtdValidate = options.DTDValidationMode == Validation.STRICT;

            // External entities / an external DTD subset resolve through the config's ResourceResolver. A bare
            // non-validating parse with no external references needs no resolver (null = no external fetch).
            System.Xml.XmlResolver resolver = (dtdValidate || options.EntityResolverClass != null)
                ? new ResourceResolverXmlResolver(config.GetResourceResolver(), config, IsStreamless ? url : null)
                : null;

            try
            {
                using (System.Xml.XmlReader xr = XmlReaderToReceiver.CreateXmlReader(charStream, byteStream, url, resolver, dtdValidate, false, config))
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
            catch (System.IO.IOException err)
            {
                // An I/O failure (e.g. missing file) becomes SXXP0003, which doc-available() turns into false.
                // A malformed-document XmlException is NOT caught here -- it propagates unchanged.
                throw new XPathException("I/O error reported by XML parser processing " + url, err).WithErrorCode(DAXonErrorCode.SXXP0003);
            }
            catch (System.Net.WebException err)
            {
                throw new XPathException("I/O error reported by XML parser processing " + url, err).WithErrorCode(DAXonErrorCode.SXXP0003);
            }
        }
    }
}
