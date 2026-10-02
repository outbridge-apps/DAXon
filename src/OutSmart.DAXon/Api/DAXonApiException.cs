////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// An exception thrown by the Saxon s9api API. This is always a wrapper for some other underlying exception
    /// </summary>
    public class DAXonApiException : Exception
    {
        public DAXonApiException(Exception cause) : base(cause?.Message, cause)
        {
        }

        public DAXonApiException(UncheckedXPathException cause) : base(cause.GetXPathException()?.Message, cause.GetXPathException())
        {
        }

        public DAXonApiException(string message) : base(message, new XPathException(message))
        {
        }

        public DAXonApiException(string message, Exception cause) : base(message, new XPathException(message, cause))
        {
        }

        // .NET's XmlReader reports malformed input as a raw XmlException; Java's parser surfaced it as
        // SXXP0003 inside the API exception. The XmlException stays reachable as the inner cause.
        internal static DAXonApiException FromXmlParser(System.Xml.XmlException e, string systemId)
        {
            XPathException err = new XPathException("Error reported by XML parser: " + e.Message, e)
                .WithErrorCode(DAXonErrorCode.SXXP0003)
                .WithLocation(new Expressions.Parsing.Loc(string.IsNullOrEmpty(e.SourceUri) ? systemId : e.SourceUri, e.LineNumber, e.LinePosition));
            return new DAXonApiException(err);
        }

        public string GetMessage()
        {
            return InnerException.Message;
        }

        public virtual QName GetErrorCode()
        {
            Exception cause = (Exception)InnerException;
            if (cause is XPathException)
            {
                StructuredQName code = ((XPathException)cause).ErrorCodeQName;
                return code == null ? null : new QName(code);
            }
            else
            {
                return null;
            }
        }

        public virtual int GetLineNumber()
        {
            Exception cause = (Exception)InnerException;
            if (cause is XPathException)
            {
                ILocation loc = ((XPathException)cause).GetLocator();
                return loc == null ? -1 : loc.GetLineNumber();
            }
            else
            {
                return -1;
            }
        }

        public virtual string GetSystemId()
        {
            Exception cause = (Exception)InnerException;
            if (cause is XPathException)
            {
                ILocation loc = ((XPathException)cause).GetLocator();
                return loc == null ? null : loc.GetSystemId();
            }
            else
            {
                return null;
            }
        }

        public virtual int GetColumnNumber()
        {
            Exception cause = (Exception)InnerException;
            if (cause is XPathException)
            {
                ILocation loc = ((XPathException)cause).GetLocator();
                return loc == null ? -1 : loc.GetColumnNumber();
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// The diagnostics collected during a failed stylesheet compilation, oldest first
        /// (empty for any other failure). Capped - GetErrorCount is the true total. This is
        /// what a host gets without installing a reporter; SetErrorList remains the opt-in
        /// for capturing warnings and successful-compile diagnostics too.
        /// </summary>
        public virtual IList<IXmlProcessingError> GetErrors()
        {
            return InnerException is Xslt.XsltCompilationFailure failure
                ? failure.Errors
                : (IList<IXmlProcessingError>)new List<IXmlProcessingError>();
        }

        public virtual int GetErrorCount()
        {
            return InnerException is Xslt.XsltCompilationFailure failure ? failure.TotalErrorCount : 0;
        }
    }
}