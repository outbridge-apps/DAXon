////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;

namespace OutSmart.DAXon.Resources
{
    // Port of net.sf.saxon.resource.DataURIScheme (Saxon 12.9): the content of an RFC 2397 data: URI as
    // a resource - binary when ;base64, else text. Text is taken as the URI spells it: upstream encoded
    // it to UTF-8 and decoded it with the declared charset (US-ASCII by default), turning non-ASCII into ?.
    internal static class DataURIScheme
    {
        public static IResource Decode(string uri)
        {
            string path = Uri.UnescapeDataString(uri.Substring(uri.IndexOf(':') + 1));
            int comma = path.IndexOf(',');
            if (comma < 0)
            {
                throw new XPathException("Missing comma in data URI", "FODC0002");
            }

            string header = path.Substring(0, comma);
            string content = path.Substring(comma + 1);
            bool isBase64 = header.EndsWith(";base64", StringComparison.Ordinal);
            string contentType = isBase64 ? header.Substring(0, comma - 7) : header;
            if (isBase64)
            {
                byte[] octets = Base64BinaryValue.Decode(StringView.Of(content).Tidy());
                return new BinaryResource(uri, contentType, octets);
            }

            var details = new AbstractResourceCollection.InputDetails
            {
                resourceUri = uri,
                contentType = MediaType(contentType),
                encoding = Charset(contentType) ?? "US-ASCII",
                characterContent = content,
            };
            return UnparsedTextResource.FACTORY.MakeResource(null, details);
        }

        private static string MediaType(string contentType)
        {
            int semicolon = contentType.IndexOf(';');
            return semicolon < 0 ? contentType : contentType.Substring(0, semicolon);
        }

        private static string Charset(string contentType)
        {
            foreach (string part in contentType.Split(';'))
            {
                if (part.StartsWith("charset=", StringComparison.Ordinal))
                {
                    return part.Substring(8);
                }
            }

            return null;
        }
    }
}
