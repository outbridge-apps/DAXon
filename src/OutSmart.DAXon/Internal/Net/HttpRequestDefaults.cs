////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Net;

namespace OutSmart.DAXon.Internal.Net
{
    /// <summary>
    /// Set on every HTTP request the engine makes. gzip is asked for and decoded by
    /// HttpContentDecoding, never by the platform. Cookies set along a redirect chain reach the next
    /// hop under the cookie rules (domain, path, Secure) and live for one fetch only.
    /// </summary>
    internal static class HttpRequestDefaults
    {
        internal static void Apply(WebRequest request, CookieContainer cookies)
        {
            if (request is HttpWebRequest http)
            {
                // Platform decompression would sit beneath NetworkDeadline's guard, out of its reach.
                http.Headers[HttpRequestHeader.AcceptEncoding] = "gzip";
                http.CookieContainer = cookies ?? new CookieContainer();
            }
        }
    }
}
