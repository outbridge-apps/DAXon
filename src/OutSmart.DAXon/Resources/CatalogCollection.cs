////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using System.IO;
using System.Text;

namespace OutSmart.DAXon.Resources
{
    // Real CatalogCollection.cs (excluded) holds the static utility MakeStringFromStream,
    // called by the compiled base AbstractResourceCollection. The other callers (StandardCollectionFinder,
    // UnparsedTextResource) are excluded, so only this static method is needed -> a static-only stub
    // avoids an AbstractResourceCollection base cascade. Faithful re-impl (compat has no
    // ByteArrayOutputStream, so MemoryStream + Encoding): read the stream, decode with the charset.
    internal sealed class CatalogCollection
    {
        public static string MakeStringFromStream(Stream input, string encoding)
        {
            var ms = new MemoryStream();
            byte[] buffer = new byte[1024];
            // IO-removal: System.IO.Stream.Read(byte[]) has no 1-arg overload and returns 0 (not -1) at EOF.
            for (int length; (length = input.Read(buffer, 0, buffer.Length)) != 0;)
            {
                ms.Write(buffer, 0, length);
            }
            return Encoding.GetEncoding(encoding).GetString(ms.ToArray());
        }
    }
}
