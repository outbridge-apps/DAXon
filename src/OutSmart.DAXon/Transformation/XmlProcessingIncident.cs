////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Utilities;
using System;
namespace OutSmart.DAXon.Transformation
{
    public class XmlProcessingIncident : IXmlProcessingError
    {
        private readonly string message;
        private string errorCode;
        private Exception cause;
        private ILocation locator;
        private bool _isWarning;
        private bool _isTypeError;
        private string fatalErrorMessage;
        private bool _hasBeenReported;
        private HostLanguage hostLanguage = HostLanguage.UNKNOWN;
        private bool _isStaticError;
        private Expression failingExpression;

        public virtual string TerminationMessage
        {
            get => fatalErrorMessage; set
            {
                fatalErrorMessage = value;
            }
        }

        public virtual string ModuleUri => GetLocation().GetSystemId();

        public virtual string InstructionName => ((NodeInfo)locator).DisplayName;
        public XmlProcessingIncident(string message, string errorCode, ILocation location)
        {
            if (message == null)
                throw new NullReferenceException();
            if (errorCode == null)
                throw new NullReferenceException();
            // location nullable in this port (hollow XPathException locator plumbing -> err.GetLocator()==null); coalesced at this.locator below instead of asserting (was Objects.RequireNonNull(location) -> masking NRE)
            this.message = message;
            SetErrorCodeAsEQName(errorCode);
            this.locator = location ?? Loc.NONE;
            this._isWarning = false;
        }

        public XmlProcessingIncident(string message)
        {
            this.message = message;
        }

        public XmlProcessingIncident(string message, string errorCode)
        {
            this.message = message;
            SetErrorCodeAsEQName(errorCode);
        }

        public XmlProcessingIncident(XPathException err, bool isWarning)
        {
            XPathException exception = XPathException.MakeXPathException(err);
            message = exception.Message;
            errorCode = exception.ErrorCodeQName.EQName;
            locator = exception.GetLocator();
            this._isWarning = isWarning;
        }

        public virtual void SetWarning(bool warning)
        {
            _isWarning = warning;
        }

        public virtual XmlProcessingIncident AsWarning()
        {
            _isWarning = true;
            return this;
        }

        public virtual bool IsAlreadyReported()
        {
            return _hasBeenReported;
        }

        public virtual void SetAlreadyReported(bool reported)
        {
            this._hasBeenReported = reported;
        }

        public virtual HostLanguage GetHostLanguage()
        {
            return hostLanguage;
        }

        public virtual void SetHostLanguage(HostLanguage language)
        {
            this.hostLanguage = language;
        }

        public virtual bool IsTypeError()
        {
            return _isTypeError;
        }

        public virtual void SetTypeError(bool isTypeError)
        {
            this._isTypeError = isTypeError;
        }

        public virtual bool IsStaticError()
        {
            return _isStaticError;
        }

        public virtual void SetStaticError(bool isStaticError)
        {
            this._isStaticError = isStaticError;
        }

        public virtual QName GetErrorCode()
        {
            if (errorCode == null)
            {
                return null;
            }

            return new QName(StructuredQName.FromEQName((errorCode)));
        }

        public virtual void SetErrorCodeAsEQName(string code)
        {
            if (code.StartsWith("Q{", StringComparison.Ordinal))
            {
                this.errorCode = code;
            }
            else if (NameChecker.IsValidNCName(StringTool.CodePoints(code)))
            {
                this.errorCode = "Q{" + NamespaceConstant.ERR + "}" + code;
            }
            else
            {
                this.errorCode = "Q{" + NamespaceConstant.SAXON + "}invalid-error-code";
            }
        }

        public virtual string GetMessage()
        {
            return message;
        }

        public virtual Expression GetFailingExpression()
        {
            return failingExpression;
        }

        public virtual void SetFailingExpression(Expression expr)
        {
            this.failingExpression = expr;
        }

        public virtual ILocation GetLocation()
        {
            return locator == null ? Loc.NONE : locator;
        }

        public virtual void SetLocation(ILocation loc)
        {
            this.locator = loc;
        }

        public virtual int GetColumnNumber()
        {
            ILocation locator = GetLocation();
            if (locator != null)
            {
                return locator.GetColumnNumber();
            }

            return -1;
        }

        public virtual int GetLineNumber()
        {
            ILocation locator = GetLocation();
            if (locator != null)
            {
                return locator.GetLineNumber();
            }

            return -1;
        }

        public virtual bool IsWarning()
        {
            return _isWarning;
        }

        public virtual string GetPath()
        {
            if (locator is NodeInfo)
            {
                return Navigator.GetPath((NodeInfo)locator);
            }
            else
            {
                return null;
            }
        }

        public virtual Exception GetCause()
        {
            return cause;
        }

        public virtual void SetCause(Exception cause)
        {
            this.cause = cause;
        }

        public static void MaybeSetHostLanguage(IXmlProcessingError error, HostLanguage lang)
        {
            if (error.GetHostLanguage() == HostLanguage.UNKNOWN)
            {
                if (error is XmlProcessingIncident)
                {
                    ((XmlProcessingIncident)error).SetHostLanguage(lang);
                }
                else if (error is XmlProcessingException)
                {
                    ((XmlProcessingException)error).GetXPathException().SetHostLanguage(lang);
                }
            }
        }

        public static void MaybeSetLocation(IXmlProcessingError error, ILocation loc)
        {
            if (error.GetLocation() == null || error.GetLocation() == Loc.NONE)
            {
                if (error is XmlProcessingIncident)
                {
                    ((XmlProcessingIncident)error).SetLocation(loc);
                }
                else if (error is XmlProcessingException)
                {
                    ((XmlProcessingException)error).GetXPathException().SetLocation(loc);
                }
            }
        }
        IXmlProcessingError IXmlProcessingError.AsWarning() => AsWarning();
    }
}
