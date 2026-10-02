////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Internal
{

    /// <summary>
    /// Recursion-depth-exceeded error (the stack guard's signal; upstream Saxon raised
    /// java.lang.StackOverflowError here).
    /// <para>
    /// Deliberately NOT an XPathException. The engine has ~185 sites on the recursion cycle that
    /// catch an XPathException only to re-decorate and rethrow it, and on .NET Framework each such
    /// handler re-enters exception dispatch from the still-unwound deep stack at ~20KB a level - so
    /// a signal those handlers can see costs more stack to report than the descent cost to reach,
    /// and the process dies anyway. Being a foreign type makes every one of them transparent by
    /// construction, which is the only version of this that stays true when someone adds the 186th.
    /// </para>
    /// <para>
    /// The recursion site nearest the overflow says WHICH recursion blew (message, error code,
    /// location) via <see cref="Describe"/>; outer sites of the same recursion filter on
    /// <see cref="Described"/> and stand aside, so exactly one handler runs however deep the stack
    /// is. The API boundary converts to the XPathException.StackOverflow the host expects.
    /// </para>
    /// </summary>
    internal sealed class RecursionDepthError : Exception
    {
        private const string NestingMessage = "Too many nested instructions or template/function calls for the thread's stack. "
            + "The stylesheet may be looping, or be nested too deeply for this stack size.";

        private string description;
        private string errorCode;
        private ILocation location;
        private bool nesting;
        private ILocation nestingLocation;

        public RecursionDepthError() : base("") { }

        /// <summary>Raised at a level of stylesheet nesting (an instruction's evaluation), not at a call.</summary>
        internal static RecursionDepthError AtNesting(ILocation location)
        {
            bool known = location != null && (location.GetLineNumber() > 0 || location.GetSystemId() != null);
            return new RecursionDepthError { nesting = true, nestingLocation = known ? location : null };
        }

        public override string Message => description ?? base.Message;

        /// <summary>True once a recursion site has claimed this abort.</summary>
        public bool Described => errorCode != null;

        /// <summary>Records which recursion overflowed; returns this, for `throw e.Describe(...)`.</summary>
        public RecursionDepthError Describe(string message, string code, ILocation loc)
        {
            if (nesting && code == DAXonErrorCode.SXLM0001)
            {
                // The claiming call sees a recursion, but the stack may have gone to nesting below it.
                message = NestingMessage;
                loc = nestingLocation ?? loc;
            }

            description = message;
            errorCode = code;
            location = loc;
            return this;
        }

        /// <summary>The host-visible form. Built at the API boundary, once, on an unwound stack.</summary>
        public XPathException.StackOverflow ToXPathException()
        {
            return new XPathException.StackOverflow(
                description ?? (nesting ? NestingMessage : "Too many nested function or template calls. May be due to infinite recursion"),
                errorCode ?? DAXonErrorCode.SXLM0001,
                location ?? nestingLocation);
        }
    }
}
