////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.IO;
using System.IO.Compression;
using OutSmart.DAXon.Internal.Streams;

namespace OutSmart.DAXon.Internal.Net
{
    /// <summary>
    /// Decodes a gzip response body above NetworkDeadline's guard, so every read of compressed bytes
    /// is checked against the run's deadline. The compressed bytes count against the input cap here;
    /// the callers cap the decoded bytes as before.
    /// </summary>
    internal static class HttpContentDecoding
    {
        internal static Stream Decode(Stream guarded, string contentEncoding, long maxInput, string systemId, string errorCode)
        {
            if (guarded == null || !IsGzip(contentEncoding))
            {
                return guarded;
            }

            return new GZipStream(InputSizeLimit.Apply(guarded, maxInput, systemId, errorCode), CompressionMode.Decompress);
        }

        private static bool IsGzip(string contentEncoding)
        {
            string e = contentEncoding?.Trim();
            return string.Equals(e, "gzip", StringComparison.OrdinalIgnoreCase) || string.Equals(e, "x-gzip", StringComparison.OrdinalIgnoreCase);
        }
    }
}
