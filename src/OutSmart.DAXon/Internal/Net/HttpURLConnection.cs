////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal.Net
{

    // HTTP flavor for ResourceLoader's redirect loop. The platform follows redirects itself unless
    // the resource-policy path turns that off; request headers come from HttpRequestDefaults.
    internal sealed class HttpURLConnection : URLConnection
    {
        public const int HTTP_MOVED_PERM = 301;
        public const int HTTP_MOVED_TEMP = 302;
        public const int HTTP_SEE_OTHER = 303;
        public const int HTTP_TEMP_REDIRECT = 307;
        public const int HTTP_PERM_REDIRECT = 308;
        public int ResponseCode { get { var r = Response() as global::System.Net.HttpWebResponse; return r == null ? 200 : (int)r.StatusCode; } }
        public HttpURLConnection(global::System.Uri url) : base(url) { }
        public void SetInstanceFollowRedirects(bool follow) { followRedirects = follow; }
        public string GetHeaderField(string name) { try { return Response()?.Headers?[name]; } catch { return null; } }
    }
}
