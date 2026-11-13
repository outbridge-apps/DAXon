////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// An exception thrown by the Saxon s9api API. This is always a wrapper for some other underlying exception
    /// </summary>
    public class DAXonApiException : Exception
    {
        public DAXonApiException(Exception cause) : base(ArgumentCheck.Checked(cause, nameof(cause)).Message, cause)
        {
        }

        public DAXonApiException(UncheckedXPathException cause) : base(ArgumentCheck.Checked(cause, nameof(cause)).GetXPathException()?.Message, cause.GetXPathException())
        {
        }

        public DAXonApiException(string message) : base(message, new XPathException(message))
        {
        }

        public DAXonApiException(string message, Exception cause) : base(message, new XPathException(message, cause))
        {
        }

        internal static bool IsIO(Exception e)
        {
            // UriFormatException: a name that is no path either - a device ("CON"), "c:relative", "\.\pipe\x".
            // ObjectDisposedException: a reader or stream the host had closed (SXXP0003 as Java's, not raw).
            return e is System.IO.IOException || e is UnauthorizedAccessException || e is UriFormatException || e is ObjectDisposedException
                || IsPathRefused(e);
        }

        // .NET Framework refusing a path by its form ("Illegal characters in path.", "The given path's format is not
        // supported.") from System.IO or its FileIOPermission check: a file that cannot be read, not a defect.
        private static bool IsPathRefused(Exception e)
        {
            string thrower = e is ArgumentException || e is NotSupportedException ? e.TargetSite?.DeclaringType?.FullName : null;
            return thrower != null && (thrower.StartsWith("System.IO.", StringComparison.Ordinal) || thrower.StartsWith("Microsoft.Win32.", StringComparison.Ordinal) || thrower == "System.Security.Permissions.FileIOPermission");
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