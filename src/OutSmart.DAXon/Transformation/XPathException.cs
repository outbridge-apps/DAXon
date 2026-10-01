////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Core;
namespace OutSmart.DAXon.Transformation
{
    public class XPathException : Exception
    {
        private bool _isTypeError = false;
        private bool _isSyntaxError = false;
        private bool _isStaticError = false;
        private bool _isGlobalError = false;
        private string hostLanguage = null;
        private StructuredQName errorCode;
        private ISequence errorObject;
        private Expression failingExpression;
        private bool _hasBeenReported = false;
        IXPathContext context;
        // Native error locus. Replaces routing through the base JAXP TransformerException.SetLocator/GetLocator
        // (a no-op stub); ILocation no longer extends the JAXP SourceLocator.
        private ILocation locator;

        public virtual IXPathContext XPathContext
        {
            get => context; set
            {
                this.context = value;
            }
        }

        public virtual StructuredQName ErrorCodeQName
        {
            get => errorCode; set
            {
                errorCode = value;
            }
        }

        public virtual ISequence ErrorObject
        {
            get => errorObject; set
            {
                errorObject = value;
            }
        }
        public XPathException(string message) : base(message)
        {
            BreakPoint();
        }

        public XPathException(Exception err) : base("", err)
        {
            BreakPoint();
        }

        public XPathException(string message, Exception err) : base(message, err)
        {
            BreakPoint();
        }

        public XPathException(string message, string errorCode, ILocation loc) : this(message, errorCode)
        {
            SetLocator(loc);
            BreakPoint();
        }

        public XPathException(string message, string errorCode) : base(message)
        {
            SetErrorCode(errorCode);
            BreakPoint();
        }

        public XPathException(string message, string errorCode, IXPathContext context) : base(message)
        {
            SetErrorCode(errorCode);
            XPathContext = context;
            BreakPoint();
        }

        private static void BreakPoint()
        {
        }

        public static XPathException MakeXPathException(Exception err)
        {
            if (err is XPathException)
            {
                return (XPathException)err;
            }
            else if (err.InnerException is XPathException)
            {
                return (XPathException)err.InnerException;
            }
            else
            {
                return new XPathException(err?.Message);
            }
        }

        public static XPathException FromXmlProcessingError(IXmlProcessingError error)
        {
            if (error is XmlProcessingException)
            {
                return ((XmlProcessingException)error).GetXPathException();
            }
            else
            {
                XPathException e = new XPathException(error.GetMessage());
                e.SetLocation(error.GetLocation());
                e.SetHostLanguage(error.GetHostLanguage());
                e.SetIsStaticError(error.IsStaticError());
                e.SetIsTypeError(error.IsTypeError());
                QName code = error.GetErrorCode();
                if (code != null)
                {
                    e.ErrorCodeQName = code.GetStructuredQName();
                }

                return e;
            }
        }

        public virtual XPathException WithMessage(string message)
        {
            XPathException e2 = new XPathException(message);
            e2.ErrorCodeQName = ErrorCodeQName;
            e2.SetLocation(GetLocator());
            e2.SetIsSyntaxError(IsSyntaxError());
            e2.SetIsTypeError(IsTypeError());
            e2.SetHostLanguage(GetHostLanguage());
            e2.XPathContext = XPathContext;
            return e2;
        }

        public virtual XPathException WithXPathContext(IXPathContext context)
        {
            this.context = context;
            return this;
        }

        public virtual void SetLocation(ILocation loc)
        {
            if (loc != null)
            {
                SetLocator(loc);
            }
        }

        public virtual XPathException WithLocation(ILocation loc)
        {
            SetLocation(loc);
            return this;
        }

        public virtual Expression GetFailingExpression()
        {
            return failingExpression;
        }

        public virtual XPathException WithFailingExpression(Expression failingExpression)
        {
            if (failingExpression != null)
            {
                this.failingExpression = failingExpression;
                MaybeSetLocation(failingExpression.GetLocation());
            }

            return this;
        }

        public virtual XPathException MaybeWithFailingExpression(Expression failingExpression)
        {
            if (failingExpression != null)
            {
                if (this.failingExpression == null)
                {
                    this.failingExpression = failingExpression;
                }

                MaybeSetLocation(failingExpression.GetLocation());
            }

            return this;
        }

        public ILocation GetLocator()
        {
            return locator;
        }

        // Native SetLocator (ILocation). Hides the base JAXP TransformerException.SetLocator(SourceLocator)
        // no-op; snapshots the location so a live parser locator isn't captured mutably.
        public void SetLocator(ILocation loc)
        {
            locator = loc?.SaveLocation();
        }

        public virtual void SetIsStaticError(bool @is)
        {
            _isStaticError = @is;
        }

        public virtual XPathException AsStaticError()
        {
            SetIsStaticError(true);
            return this;
        }

        public virtual bool IsStaticError()
        {
            return _isStaticError;
        }

        public virtual void SetIsSyntaxError(bool @is)
        {
            if (@is)
            {
                _isStaticError = true;
            }

            _isSyntaxError = @is;
        }

        public virtual bool IsSyntaxError()
        {
            return _isSyntaxError;
        }

        public virtual void SetIsTypeError(bool @is)
        {
            _isTypeError = @is;
        }

        public virtual XPathException AsTypeError()
        {
            SetIsTypeError(true);
            return this;
        }

        public virtual XPathException AsTypeErrorIf(bool condition)
        {
            SetIsTypeError(condition);
            return this;
        }

        public virtual bool IsTypeError()
        {
            return _isTypeError;
        }

        public virtual void SetIsGlobalError(bool @is)
        {
            _isGlobalError = @is;
        }

        public virtual bool IsGlobalError()
        {
            return _isGlobalError;
        }

        public virtual void SetHostLanguage(string language)
        {
            this.hostLanguage = language;
        }

        public virtual void SetHostLanguage(HostLanguage language)
        {
            this.hostLanguage = language == HostLanguage.UNKNOWN ? null : language.ToString();
        }

        public virtual string GetHostLanguage()
        {
            return hostLanguage;
        }

        public virtual void SetErrorCode(string code)
        {
            if (code != null)
            {
                errorCode = new StructuredQName("err", NamespaceUri.ERR, code);
            }
        }

        public virtual XPathException WithErrorCode(string code)
        {
            SetErrorCode(code);
            return this;
        }

        public virtual XPathException WithErrorCode(StructuredQName code)
        {
            ErrorCodeQName = code;
            return this;
        }

        public virtual XPathException ReplacingErrorCode(string oldCode, string newCode)
        {
            if (HasErrorCode(oldCode))
            {
                SetErrorCode(newCode);
            }

            return this;
        }

        public virtual void MaybeSetErrorCode(string code)
        {
            if (errorCode == null && code != null)
            {
                errorCode = new StructuredQName("err", NamespaceUri.ERR, code);
            }
        }

        public virtual XPathException MaybeWithErrorCode(string code)
        {
            MaybeSetErrorCode(code);
            return this;
        }

        public virtual string ShowErrorCode()
        {
            if (errorCode == null)
            {
                return "no_error_code";
            }
            else if (errorCode.HasURI(NamespaceUri.ERR))
            {
                return errorCode.GetLocalPart();
            }
            else
            {
                return errorCode.EQName;
            }
        }

        public virtual bool HasErrorCode(params string[] codes)
        {
            if (errorCode != null && errorCode.HasURI(NamespaceUri.ERR))
            {
                foreach (string code in codes)
                {
                    if (errorCode.GetLocalPart().Equals(code))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public virtual void SetHasBeenReported(bool reported)
        {
            _hasBeenReported = reported;
        }

        public virtual bool HasBeenReported()
        {
            return _hasBeenReported;
        }

        public virtual void MaybeSetLocation(ILocation here)
        {
            if (here != null)
            {
                if (GetLocator() == null)
                {
                    SetLocator(here.SaveLocation());
                }
                else if (GetLocator().GetLineNumber() == -1 && !(GetLocator().GetSystemId() != null && here.GetSystemId() != null && !GetLocator().GetSystemId().Equals(here.GetSystemId())))
                {
                    SetLocator(here.SaveLocation());
                }
            }
        }

        public virtual XPathException MaybeWithLocation(ILocation here)
        {
            MaybeSetLocation(here);
            return this;
        }

        public virtual void MaybeSetContext(IXPathContext context)
        {
            if (XPathContext == null)
            {
                XPathContext = context;
            }
        }

        public virtual XPathException MaybeWithContext(IXPathContext context)
        {
            if (XPathContext == null)
            {
                XPathContext = context;
            }

            return this;
        }

        public virtual bool IsReportableStatically()
        {
            if (IsStaticError() || IsTypeError())
            {
                return true;
            }

            StructuredQName err = errorCode;
            if (err != null && err.HasURI(NamespaceUri.ERR))
            {
                string local = err.GetLocalPart();
                return local.Equals("XTDE1260") || local.Equals("XTDE1280") || local.Equals("XTDE1390") || local.Equals("XTDE1400") || local.Equals("XTDE1428") || local.Equals("XTDE1440") || local.Equals("XTDE1460");
            }

            return false;
        }

        /// <summary>
        /// Subclass of XPathException used to report circularities
        /// </summary>
        internal sealed class Circularity : XPathException
        {
            public Circularity(string message) : base(message)
            {
            }
        }

        /// <summary>
        /// Subclass of XPathException used to report stack overflow
        /// </summary>
        internal sealed class StackOverflow : XPathException
        {
            public StackOverflow(string message, string errorCode, ILocation location) : base(message, errorCode, location)
            {
            }
        }
    }
}
