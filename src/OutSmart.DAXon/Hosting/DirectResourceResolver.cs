////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;

using OutSmart.DAXon.Resources;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using System;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Lib
{
    internal sealed class DirectResourceResolver : IResourceResolver
    {
        private readonly Configuration config;
        public DirectResourceResolver(Configuration config)
        {
            this.config = config;
        }

        public ResolvedResource Resolve(ResourceRequest request)
        {
            if (request.uriIsNamespace)
            {
                return null; // bug 5266
            }

            // The Processor's input-size cap applies to every resource this default resolver
            // fetches (doc/document/collection/unparsed-text/json-doc, compile-time includes).
            // A host-supplied resolver takes precedence over this one and is the host's own
            // code - capping what it returns is its own responsibility.
            long maxInput = OutSmart.DAXon.Internal.Streams.InputSizeLimit.MaxFor(config);

            ProtocolRestrictor restrictor = config.GetProtocolRestrictor();
            if (restrictor.ToString() != "all")
            {
                try
                {
                    URI u = new URI(request.uri);
                    if (!restrictor.Test(u))
                    {
                        throw new XPathException("URIs using protocol " + u.Scheme + " are not permitted");
                    }
                }
                catch (URISyntaxException)
                {
                    throw new XPathException("Unknown URI scheme requested " + request.uri);
                }
            }

            // The Processor's resource policy, before any file or network access.
            OutSmart.DAXon.Api.ResourceKind kind = ResourceGate.KindOfNature(request.nature);
            string denied = ResourceGate.IsRestricted(config) ? ResourceGate.CheckRead(config, AbsoluteTarget(request), kind) : null;
            if (denied != null)
            {
                throw ResourceGate.Denied(denied, kind);
            }

            System.IO.Stream stream;
            if (ResourceRequest.BINARY_NATURE.Equals(request.nature))
            {

                // This path is used by the unparsed-text resolver when no encoding is supplied;
                // it returns (where possible) the binary input stream together with the content type
                // from the HTTP headers.
                string uri;
                try
                {
                    if (request.baseUri == null)
                    {
                        uri = request.uri;
                    }
                    else
                    {
                        uri = new URI(request.baseUri).Resolve(request.uri).ToString();
                    }

                    ResolvedResource typed = ResourceLoader.TypedResource(config, uri);
                    if (typed != null)
                    {
                        typed.Stream = InputSizeLimit.Apply(typed.Stream, maxInput, uri, "FOUT1170");
                    }

                    return typed;
                }
                catch (IOException e)
                {
                    throw new XPathException("Cannot read " + request.uri, e);
                }
                catch (URISyntaxException e)
                {
                    throw new XPathException("Cannot read " + request.uri, e);
                }
            }

            try
            {

                // Get an input stream from the request URI
                stream = ResourceLoader.UrlStream(config, request.uri, kind);
            }
            catch (IOException)
            {
                stream = null; // Carry on, the XML parser might know what to do with it.
            }

            if (ResourceRequest.TEXT_NATURE.Equals(request.nature))
            {

                // Typically happens when using unparsed-text() with an explicit encoding
                return new ResolvedResource { Stream = InputSizeLimit.Apply(stream, maxInput, request.uri, "FOUT1170"), SystemId = request.uri };
            }

            // Default: an XML resource. Return the raw byte stream + systemId. It is delivered through the
            // direct System.Xml.XmlReader path (StreamSource -> ActiveStreamSource -> XmlReaderToReceiver),
            // no longer via a SAXSource + SAX round-trip. We opened the stream, so it is closed after the parse.
            // NOTE the size cap only holds when we obtained the stream: if UrlStream failed above, the XML
            // parser opens the URI itself and the fallback is uncapped (exotic URI schemes only).
            ResolvedResource xml = new ResolvedResource();
            xml.Stream = InputSizeLimit.Apply(stream, maxInput, request.uri, "FODC0002");
            xml.SystemId = request.uri;
            xml.PleaseCloseAfterUse = stream != null;
            return xml;
        }

        // What this resolver will actually open: the URI itself, or resolved against the base.
        private static string AbsoluteTarget(ResourceRequest request)
        {
            Uri absolute;
            if (Uri.TryCreate(request.uri, UriKind.Absolute, out _))
            {
                return request.uri;
            }

            Uri baseUri;
            if (request.uri != null && request.baseUri != null
                && Uri.TryCreate(request.baseUri, UriKind.Absolute, out baseUri)
                && Uri.TryCreate(baseUri, request.uri, out absolute))
            {
                return absolute.AbsoluteUri;
            }

            return request.uri;
        }
    }
}