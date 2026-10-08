////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees;
using System;
namespace OutSmart.DAXon.Transformation
{
    internal sealed class XmlProcessingException : IXmlProcessingError
    {
        private readonly XPathException exception;
        private bool _isWarning;
        private string fatalErrorMessage;

        public string TerminationMessage
        {
            get => this.fatalErrorMessage; set
            {
                this.fatalErrorMessage = value;
            }
        }

        public string ModuleUri => exception.GetLocator()?.GetSystemId(); // module of the error locus
        public XmlProcessingException(XPathException exception)
        {
            this.exception = exception;
        }

        public XPathException GetXPathException()
        {
            return exception;
        }

        public HostLanguage GetHostLanguage()
        {
            ILocation loc = GetLocation();
            if (loc is Instruction || loc is AttributeLocation)
            {
                return HostLanguage.XSLT;
            }
            else
            {
                return HostLanguage.XPATH;
            }
        }

        public bool IsStaticError()
        {
            return exception.IsStaticError();
        }

        public bool IsTypeError()
        {
            return exception.IsTypeError();
        }

        public QName GetErrorCode()
        {
            StructuredQName errorCodeQName = exception.ErrorCodeQName;
            return errorCodeQName == null ? null : new QName(errorCodeQName);
        }

        public string GetMessage()
        {
            return exception.Message;
        }

        public ILocation GetLocation()
        {
            return exception.GetLocator() == null ? Loc.NONE : exception.GetLocator();
        }

        public bool IsWarning()
        {
            return _isWarning;
        }

        public string GetPath()
        {
            return null;
        }

        public Exception GetCause()
        {
            return (Exception)exception.InnerException;
        }

        public Expression GetFailingExpression()
        {
            return exception.GetFailingExpression();
        }

        public void SetWarning(bool warning)
        {
            _isWarning = warning;
        }

        public XmlProcessingException AsWarning()
        {
            XmlProcessingException e2 = new XmlProcessingException(exception);
            e2.SetWarning(true);
            return e2;
        }

        public bool IsAlreadyReported()
        {
            return exception.HasBeenReported();
        }

        public void SetAlreadyReported(bool reported)
        {
            exception.SetHasBeenReported(reported);
        }
        IXmlProcessingError IXmlProcessingError.AsWarning() => AsWarning();
    }
}