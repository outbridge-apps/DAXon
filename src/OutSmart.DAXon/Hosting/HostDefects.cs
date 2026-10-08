////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Runtime.CompilerServices;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Lib
{
    // An exception an extension function's host code threw of its own reaches the host as thrown (HOSTING.md): it is
    // marked where IExtensionFunction.Call returns, and the catch-alls that wrap a stray exception as an internal error let it by.
    internal static class HostDefects
    {
        private static readonly ConditionalWeakTable<Exception, object> marked = new ConditionalWeakTable<Exception, object>();
        private static readonly object Marker = new object();

        // True, marking it, for an exception that is not one of the errors a run raises or handles.
        internal static bool Mark(Exception e)
        {
            if (e is XPathException || e is UncheckedXPathException || e is DAXonApiException || e is RecursionDepthError || e is OutOfMemoryException)
            {
                return false;
            }

            marked.GetValue(e, _ => Marker);
            return true;
        }

        internal static bool IsHostDefect(Exception e)
        {
            return marked.TryGetValue(e, out _);
        }
    }
}
