////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#if NET
using System.Text;
#endif

namespace OutSmart.DAXon.Internal
{
    /// <summary>
    /// .NET Framework carries ~140 encodings in the BCL; .NET ships seven (UTF-*, ASCII, Latin-1)
    /// and keeps the legacy code pages behind CodePagesEncodingProvider. Registering it is what
    /// makes windows-125x, ISO-8859-x, KOI8-R, Shift_JIS resolve on .NET: for the serializer, for
    /// unparsed-text(), and for System.Xml decoding an input document's declaration, which the
    /// engine never sees. Process-wide by design and purely additive.
    /// </summary>
    internal static class PlatformEncodings
    {
#if NET
        private static readonly object Gate = new object();
        private static bool registered;
#endif

        public static void EnsureRegistered()
        {
#if NET
            lock (Gate)
            {
                if (registered)
                {
                    return;
                }

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                registered = true;
            }
#endif
        }
    }
}
