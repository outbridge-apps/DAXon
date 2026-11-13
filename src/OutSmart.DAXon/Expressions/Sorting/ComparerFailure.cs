////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Runtime.ExceptionServices;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Expressions.Sorting
{
    // .NET's sorts wrap what a comparer throws in InvalidOperationException, where Java's let it through.
    internal static class ComparerFailure
    {
        /// <summary>
        /// What the comparer threw, as the sort reports it: an engine error as thrown (the time or memory limit among
        /// them), two values that do not compare (InvalidCastException, upstream's ClassCastException) as the type
        /// error XPTY0004 (the dynamic error XTDE1030 of xsl:sort). A host collation's own exception is rethrown here,
        /// as thrown.
        /// </summary>
        internal static XPathException Reported(Exception thrown, bool xslSort = false)
        {
            Exception inner = thrown is InvalidOperationException && thrown.InnerException != null ? thrown.InnerException : thrown;
            switch (inner)
            {
                case UncheckedXPathException wrapped:
                    return wrapped.GetXPathException();
                case XPathException engine:
                    return engine;
                case InvalidCastException cast:
                    XPathException e = new XPathException("Non-comparable types found while sorting: " + cast.Message, xslSort ? "XTDE1030" : "XPTY0004");
                    return xslSort ? e : e.AsTypeError();
                default:
                    ExceptionDispatchInfo.Capture(inner).Throw();
                    return null;
            }
        }
    }
}
