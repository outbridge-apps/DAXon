////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Lib
{
    public class ResourceRequest
    {
        public const string TEXT_NATURE = "https://www.iana.org/assignments/media-types/text/plain";
        public const string BINARY_NATURE = "https://www.iana.org/assignments/media-types/application/binary";
        public const string XQUERY_NATURE = "https://www.iana.org/assignments/media-types/application/xquery";
        public const string XML_NATURE = "https://www.iana.org/assignments/media-types/application/xml";
        public const string DTD_NATURE = "https://www.iana.org/assignments/media-types/application/xml-dtd";
        public const string EXTERNAL_ENTITY_NATURE = "https://www.iana.org/assignments/media-types/application/xml-external-parsed-entity";
        public const string SCHEMA_NATURE = NamespaceConstant.SCHEMA;
        public const string VALIDATION_PURPOSE = "http://www.rddl.org/purposes#validation";
        public static readonly string XSLT_NATURE = NamespaceConstant.XSLT;
        public static readonly string XSD_NATURE = NamespaceConstant.SCHEMA;
        // The whole nature/purpose thing never got fleshed out as completely as it might have. For things
        // where there isn't a defined purpose, just use any purpose...the catalog lookup allows any
        // value to match null.
        public static readonly string ANY_PURPOSE = null;
        public static readonly string ANY_NATURE = null;
        public string uri;
        /// <summary>
        ///  The base URI that was used to resolve any relative URI, if known.
        /// </summary>
        public string baseUri;
        /// <summary>
        ///  The relative URI that was actually requested, where applicable.
        /// </summary>
        public string relativeUri;
        /// <summary>
        ///  The public ID of the requested resource, where applicable
        /// </summary>
        public string publicId;
        /// <summary>
        /// The name of the requested resource, used when resolving entity references
        /// </summary>
        public string entityName;
        public string nature;
        public string purpose;
        public bool uriIsNamespace;
        public bool streamable;
        public string requestedEncoding;
        public virtual ResourceRequest Copy()
        {
            ResourceRequest rr = new ResourceRequest();
            rr.relativeUri = relativeUri;
            rr.baseUri = baseUri;
            rr.uri = uri;
            rr.uriIsNamespace = uriIsNamespace;
            rr.publicId = publicId;
            rr.purpose = purpose;
            rr.nature = nature;
            rr.entityName = entityName;
            rr.streamable = streamable;
            rr.requestedEncoding = requestedEncoding;
            return rr;
        }

        public virtual ResolvedResource Resolve(params IResourceResolver[] resolvers)
        {
            string requestedUri = relativeUri;
            if (requestedUri == null)
            {
                requestedUri = uri;
            }

            string id = null;

            // Extract any fragment identifier. Note, this code is no longer used to
            // resolve fragment identifiers in URI references passed to the document()
            // function: the code of the document() function handles these itself.
            ResourceRequest adjustedRequest = this;
            int hash = requestedUri.IndexOf('#');
            if (hash >= 0)
            {
                adjustedRequest = Copy();
                adjustedRequest.relativeUri = requestedUri.Substring(0, hash);
                id = requestedUri.Substring(hash + 1);
            }

            ResolvedResource resolved = null;
            foreach (IResourceResolver resolver in resolvers)
            {
                if (resolver != null)
                {
                    ResolvedResource s = null;
                    try
                    {
                        s = resolver.Resolve(this);
                    }
                    catch (XPathException e)
                    {
                        Exception cause = e.InnerException as Exception;
                        if (cause is ArgumentException)
                        {
                            ArgumentException iae = (ArgumentException)(object)cause;
                            if (iae.InnerException is URISyntaxException)
                            {
                                throw new XPathException("Invalid URI " + uri, (Exception)(iae.InnerException));
                            }

                            throw;
                        }
                        else
                        {
                            throw;
                        }
                    }

                    if (s != null)
                    {
                        resolved = s;
                        break;
                    }
                }
            }

            if (resolved != null && !resolved.IsEmpty && id != null)
            {
                string idFinal = id;
                IFilterFactory factory = (next) => (IReceiver)(new IDFilter(next, idFinal));
                if (resolved.Filters == null)
                {
                    resolved.Filters = new List<IFilterFactory>();
                }

                resolved.Filters.Add(factory);
            }

            return resolved;
        }
    }
}