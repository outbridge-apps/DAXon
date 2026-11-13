////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Api
{
    // The one translation of engine failures at the API boundary. Each entry point carried a ladder of catch clauses of
    // its own, and the ladders drifted: a parse error, an iterator's error or an aborting reporter reached the host raw.
    internal static class ApiErrors
    {
        // CallFunction's refusal of a node made under a configuration the call's is not compatible with (SXXP0004).
        internal const string ForeignArgument = "A node supplied in a function argument must be built using the same Configuration "
            + "that was used to compile the stylesheet or query";

        // A type test only: it runs as an exception filter, on the stack of the throw. The stack guard's abort is no
        // XPathException on purpose (see RecursionDepthError) and is converted here, at the boundary, and nowhere deeper.
        internal static bool IsEngineError(Exception e)
        {
            return e is XPathException || e is UncheckedXPathException || e is RecursionDepthError || e is XmlProcessingAbort
                || e is System.Xml.XmlException || e is Model.NamePool.NamePoolLimitException;
        }

        // An engine error, or an input the call opens that could not be read: missing, locked, a directory.
        internal static bool IsInputError(Exception e)
        {
            return IsEngineError(e) || DAXonApiException.IsIO(e);
        }

        // The error the host sees; systemId names the input of a parse or an I/O failure.
        internal static XPathException ErrorOf(Exception e, string systemId = null)
        {
            switch (e)
            {
                case XPathException x:
                    return x;
                case UncheckedXPathException u:
                    return u.GetXPathException();
                case RecursionDepthError r:
                    return r.ToXPathException();
                case XmlProcessingAbort a:
                    return new XPathException(a.Message, a);
                case Model.NamePool.NamePoolLimitException n:
                    // Not an XPathException inside: the pool stays full, so no try/catch of the run could recover.
                    return new XPathException(n.Message, n).WithErrorCode(DAXonErrorCode.SXLM0004);
                case System.Xml.XmlException x:
                    // .NET's XmlReader reports malformed input as a raw XmlException; Java's parser surfaced it as SXXP0003.
                    return new XPathException("Error reported by XML parser: " + x.Message, x)
                        .WithErrorCode(DAXonErrorCode.SXXP0003)
                        .WithLocation(new Expressions.Parsing.Loc(string.IsNullOrEmpty(x.SourceUri) ? systemId : x.SourceUri, x.LineNumber, x.LinePosition));
                default:
                    // SXXP0003 as the resolver path reports it; the reader's reason stays in the message.
                    return Resources.ActiveStreamSource.IOFailure(e, systemId);
            }
        }

        internal static DAXonApiException Wrap(Exception e, string systemId = null)
        {
            // An abort the error reporter asked for stays the cause itself: it carries no error code.
            return e is XmlProcessingAbort ? new DAXonApiException(e) : new DAXonApiException(ErrorOf(e, systemId));
        }

        // What s9api throws unchecked: an error met reading a result item by item, or setting an external variable.
        internal static DAXonApiUncheckedException WrapUnchecked(Exception e)
        {
            return new DAXonApiUncheckedException(ErrorOf(e));
        }

        // A run's failure, given to the run's error reporter unless an inner call already did. A stack overflow is not
        // reported: its handler can run on what the overflow left of the stack, too little for the host's code.
        internal static DAXonApiException Reported(Exception e, IErrorReporter reporter, string systemId = null)
        {
            if (e is RecursionDepthError || e is XmlProcessingAbort)
            {
                return Wrap(e, systemId);
            }

            XPathException err = ErrorOf(e, systemId);
            if (!err.HasBeenReported())
            {
                reporter?.Report(new XmlProcessingException(err));
                err.SetHasBeenReported(true);
            }

            return new DAXonApiException(err);
        }
    }
}
