////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Resources
{
    // Port of net.sf.saxon.resource.UnknownResource (Saxon 12.9): a member whose file extension maps to
    // no media type. Its first bytes decide: XML or HTML markup is parsed, anything else is binary.
    internal sealed class UnknownResource : IResource
    {
        public static readonly IResourceFactory FACTORY = new GenericResourceFactory((context, details) => new UnknownResource(context, details));

        private readonly IXPathContext context;
        private readonly Configuration config;
        private readonly AbstractResourceCollection.InputDetails details;

        public UnknownResource(IXPathContext context, AbstractResourceCollection.InputDetails details)
        {
            this.context = context;
            this.config = context.GetConfiguration();
            this.details = details;
        }

        public string ResourceURI => details.resourceUri;

        public string ContentType => "application/xml";

        public IItem Item
        {
            get
            {
                byte[] bytes;
                try
                {
                    bytes = details.ObtainBinaryContent(config);
                }
                catch (XPathException e)
                {
                    return details.Skip(context, e);
                }

                string mediaType = URLConnection.GuessContentTypeFromBytes(bytes, bytes.Length)
                    ?? config.GetMediaTypeForFileExtension("");
                if (mediaType == null || mediaType == "application/unknown")
                {
                    mediaType = "application/binary";
                }

                details.contentType = mediaType;
                details.binaryContent = bytes;

                // Upstream dereferences a null factory here; a type nobody registered is binary.
                IResourceFactory delegee = config.GetResourceFactoryForMediaType(mediaType) ?? BinaryResource.FACTORY;
                return delegee.MakeResource(context, details).Item;
            }
        }
    }
}
