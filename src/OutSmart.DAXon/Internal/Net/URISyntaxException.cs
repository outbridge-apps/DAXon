////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal.Net
{
    /// <summary>
    /// Java URISyntaxException shim. Thrown by URI constructors when the input
    /// is not a valid URI per RFC 2396. In our compat layer, URI constructors
    /// delegate to System.Uri and may throw UriFormatException -- callers that
    /// need to catch URISyntaxException specifically should catch this type
    /// (we throw it where Saxon code expects it).
    /// </summary>
    // Extend Throwable so callers passing this to XPathException(Throwable err) ctor bind.
    internal sealed class URISyntaxException : Exception
    {
        public string Input { get; }
        public string Reason { get; }
        public int Index { get; }

        public URISyntaxException(string input, string reason)
            : this(input, reason, -1) { }

        public URISyntaxException(string input, string reason, int index)
            : base(BuildMessage(input, reason, index))
        {
            Input = input;
            Reason = reason;
            Index = index;
        }

        public string GetInput() => Input;
        public string GetReason() => Reason;
        public int GetIndex() => Index;

        private static string BuildMessage(string input, string reason, int index)
        {
            if (index >= 0)
                return $"{reason} at index {index}: {input}";
            return $"{reason}: {input}";
        }
    }
}
