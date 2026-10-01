////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System.IO;
using System.Text;

namespace OutSmart.DAXon.Resources
{
    // Port of net.sf.saxon.resource.UnparsedTextResource (Saxon 12.9): a collection member delivered
    // as an xs:string. Undecodable bytes become U+FFFD, as Java's String constructor makes them.
    internal sealed class UnparsedTextResource : IResource
    {
        public static readonly IResourceFactory FACTORY = new GenericResourceFactory((context, details) => new UnparsedTextResource(context, details));

        private readonly IXPathContext context;
        private readonly Configuration config;
        private readonly AbstractResourceCollection.InputDetails details;
        private readonly string contentType;
        private readonly string href;
        private string encoding;
        private string unparsedText;

        private UnparsedTextResource(IXPathContext context, AbstractResourceCollection.InputDetails details)
        {
            this.context = context;
            this.config = context?.GetConfiguration();
            this.details = details;
            this.href = details.resourceUri;
            this.contentType = details.contentType;
            this.encoding = details.encoding;
            if (details.characterContent != null)
            {
                unparsedText = details.characterContent;
            }
            else if (details.binaryContent != null)
            {
                unparsedText = MakeString(details.binaryContent);
            }
        }

        public UnparsedTextResource(string uri, string content)
        {
            this.href = uri;
            this.unparsedText = content;
        }

        public string ResourceURI => href;

        public string Encoding => encoding;

        public string ContentType => contentType ?? "text/plain";

        public string Content
        {
            get
            {
                if (unparsedText == null && details != null)
                {
                    try
                    {
                        using (Stream stream = details.GetInputStream(config))
                        {
                            unparsedText = MakeString(BinaryResource.ReadBinaryFromStream(stream, href));
                        }
                    }
                    catch (IOException e)
                    {
                        throw new XPathException(e.Message, "FODC0002");
                    }
                }

                return unparsedText;
            }
        }

        public IItem Item
        {
            get
            {
                string text;
                try
                {
                    text = Content;
                }
                catch (XPathException e)
                {
                    return details.Skip(context, e);
                }

                return text == null ? null : new StringValue(text);
            }
        }

        private string MakeString(byte[] bytes)
        {
            if (encoding == null)
            {
                encoding = EncodingDetector.InferEncoding(bytes, bytes.Length, "UTF-8");
            }

            Encoding enc = EncodingDetector.Lookup(encoding, false);
            int bom = EncodingDetector.BomLength(bytes, 0, bytes.Length, enc);
            return enc.GetString(bytes, bom, bytes.Length - bom);
        }
    }
}
