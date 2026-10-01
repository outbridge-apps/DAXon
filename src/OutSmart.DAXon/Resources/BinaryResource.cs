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
using System;
using System.IO;
using System.Text;

namespace OutSmart.DAXon.Resources
{
    // Port of net.sf.saxon.resource.BinaryResource (Saxon 12.9): a collection member delivered as
    // xs:base64Binary. Currently limited to 2G octets.
    internal sealed class BinaryResource : IResource
    {
        public static readonly IResourceFactory FACTORY = new GenericResourceFactory((context, details) => new BinaryResource(context, details));

        private readonly IXPathContext context;
        private readonly Configuration config;
        private readonly AbstractResourceCollection.InputDetails details;
        private readonly string href;
        private readonly string contentType;
        private byte[] data;

        public BinaryResource(IXPathContext context, AbstractResourceCollection.InputDetails details)
        {
            this.context = context;
            this.config = context?.GetConfiguration();
            this.details = details;
            this.href = details.resourceUri;
            this.contentType = details.contentType;
            this.data = details.binaryContent;
        }

        public BinaryResource(string href, string contentType, byte[] content)
        {
            this.href = href;
            this.contentType = contentType;
            this.data = content;
        }

        public string ResourceURI => href;

        public string ContentType => contentType;

        public byte[] Data
        {
            get
            {
                if (data == null && details != null)
                {
                    data = details.ObtainBinaryContent(config);
                }

                return data;
            }
        }

        public IItem Item
        {
            get
            {
                byte[] bytes;
                try
                {
                    bytes = Data;
                }
                catch (XPathException e)
                {
                    return details.Skip(context, e);
                }

                return bytes == null ? null : new Base64BinaryValue(bytes);
            }
        }

        public static byte[] Encode(string s, string encoding)
        {
            try
            {
                return EncodingDetector.Lookup(encoding, true).GetBytes(s);
            }
            catch (EncoderFallbackException e)
            {
                throw new XPathException("Unmappable input in encoding: " + e.Message, "FODC0002");
            }
        }

        public static string Decode(byte[] value, string encoding)
        {
            return Decode(value, 0, value.Length, encoding);
        }

        public static string Decode(byte[] value, int offset, int len, string encoding)
        {
            Encoding enc = EncodingDetector.Lookup(encoding, true);
            int bom = EncodingDetector.BomLength(value, offset, len, enc);
            try
            {
                return enc.GetString(value, offset + bom, len - bom);
            }
            catch (DecoderFallbackException)
            {
                throw new XPathException("Malformed input found when decoding binary resource", "FODC0002");
            }
        }

        // Consumes the stream but does not close it; path is for diagnostics only.
        public static byte[] ReadBinaryFromStream(Stream input, string path)
        {
            var buffer = new MemoryStream();
            byte[] chunk = new byte[16384];
            try
            {
                int n;
                while ((n = input.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + n > 0x7FFFFFC7)
                    {
                        throw new XPathException("Cannot handle binary resources longer than 2G octets: " + path, "FODC0002");
                    }

                    buffer.Write(chunk, 0, n);
                }

                return buffer.ToArray();
            }
            catch (IOException e)
            {
                throw new XPathException("Failed to read: " + path + " " + e.Message, "FODC0002");
            }
        }
    }
}
