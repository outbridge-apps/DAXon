////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Text;
namespace OutSmart.DAXon.Types
{
    public class ValidationFailure : ILocation, IConversionResult, IInvalidity
    {
        private string message;
        private string systemId;
        private string publicId;
        private int lineNumber = -1;
        private int columnNumber = -1;
        private AbsolutePath path;
        private AbsolutePath contextPath;
        private NodeInfo invalidNode;
        private IList<NodeInfo> offendingNodes;
        private int schemaPart = -1;
        private string constraintName;
        private string clause;
        private ISchemaType schemaType;
        private StructuredQName errorCode;
        private ValidationException exception;
        private bool errorHasBeenReported;

        public virtual int SchemaPart => schemaPart;

        public virtual string ConstraintName => constraintName;

        public virtual string ConstraintClauseNumber => clause;

        public virtual string ConstraintReferenceMessage
        {
            get
            {
                if (schemaPart == -1)
                {
                    return null;
                }

                return "See http://www.w3.org/TR/xmlschema11-" + schemaPart + "/#" + constraintName + " clause " + clause;
            }
        }

        public virtual IList<NodeInfo> OffendingNodes
        {
            get
            {
                if (offendingNodes == null)
                {
                    return new List<NodeInfo>();
                }
                else
                {
                    return offendingNodes;
                }
            }
        }

        public virtual ILocation Locator
        {
            get => this; set
            {
                if (value != null)
                {
                    SetPublicId(value.GetPublicId());
                    SetSystemId(value.GetSystemId());
                    SetLineNumber(value.GetLineNumber());
                    SetColumnNumber(value.GetColumnNumber());
                }
            }
        }

        public virtual StructuredQName ErrorCodeQName
        {
            get => errorCode; set
            {
                this.errorCode = value;
            }
        }

        public virtual ISchemaType SchemaType
        {
            get => schemaType; set
            {
                schemaType = value;
            }
        }

        public virtual string ValidationLocationText
        {
            get
            {
                StringBuilder fsb = new StringBuilder(256);
                AbsolutePath valPath = GetAbsolutePath();
                if (valPath != null)
                {
                    fsb.Append("Validating ");
                    fsb.Append(valPath.PathUsingPrefixes);
                    if (valPath.SystemId != null)
                    {
                        fsb.Append(" in ");
                        fsb.Append(valPath.SystemId);
                    }
                }

                return fsb.ToString();
            }
        }

        public virtual string ContextLocationText
        {
            get
            {
                StringBuilder fsb = new StringBuilder(256);
                AbsolutePath contextPath = GetContextPath();
                if (contextPath != null)
                {
                    fsb.Append("Currently processing ");
                    fsb.Append(contextPath.PathUsingPrefixes);
                    if (contextPath.SystemId != null)
                    {
                        fsb.Append(" in ");
                        fsb.Append(contextPath.SystemId);
                    }
                }

                return fsb.ToString();
            }
        }
        public ValidationFailure(string message)
        {
            this.message = message;
            SetErrorCode("FORG0001");
        }

        public ValidationFailure(string message, string errorCode)
        {
            this.message = message;
            SetErrorCode(errorCode);
        }

        public static ValidationFailure FromException(Exception exception)
        {
            if (exception is ValidationException)
            {
                return ((ValidationException)exception).GetValidationFailure();
            }
            else if (exception is XPathException)
            {
                ValidationFailure failure = new ValidationFailure(exception.Message);
                if (((XPathException)exception).ErrorCodeQName == null)
                {
                    failure.SetErrorCode("FORG0001");
                }
                else
                {
                    failure.ErrorCodeQName = ((XPathException)exception).ErrorCodeQName;
                }

                failure.Locator = ((XPathException)exception).GetLocator();
                return failure;
            }
            else
            {
                return new ValidationFailure(exception.Message);
            }
        }

        public virtual void SetConstraintReference(int schemaPart, string constraintName, string clause)
        {
            this.schemaPart = schemaPart;
            this.constraintName = constraintName;
            this.clause = clause;
        }

        public virtual void SetConstraintReference(ValidationFailure e)
        {
            schemaPart = e.schemaPart;
            constraintName = e.constraintName;
            clause = e.clause;
        }

        public virtual string GetConstraintReference()
        {
            return constraintName + '.' + clause;
        }

        public virtual void AddOffendingNode(NodeInfo node)
        {
            if (offendingNodes == null)
            {
                offendingNodes = new List<NodeInfo>();
            }

            offendingNodes.Add(node);
        }

        public virtual AbsolutePath GetPath()
        {
            return path;
        }

        public virtual void SetPath(AbsolutePath path)
        {
            this.path = path;
        }

        public virtual AbsolutePath GetContextPath()
        {
            return contextPath;
        }

        public virtual void SetContextPath(AbsolutePath contextPath)
        {
            this.contextPath = contextPath;
        }

        public virtual NodeInfo GetInvalidNode()
        {
            return invalidNode;
        }

        public virtual void SetInvalidNode(NodeInfo invalidNode)
        {
            this.invalidNode = invalidNode;
        }

        public virtual string GetMessage()
        {
            return message;
        }

        public virtual void SetMessage(string message)
        {
            this.message = message;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("ValidationException: ");
            string message = GetMessage();
            if (message != null)
            {
                sb.Append(message);
            }

            return sb.ToString();
        }

        public virtual string GetPublicId()
        {
            ILocation loc = Locator;
            if (publicId == null && loc != null && loc != this)
            {
                return loc.GetPublicId();
            }
            else
            {
                return publicId;
            }
        }

        public virtual string GetSystemId()
        {
            ILocation loc = Locator;
            if (systemId == null && loc != null && loc != this)
            {
                return loc.GetSystemId();
            }
            else
            {
                return systemId;
            }
        }

        public virtual int GetLineNumber()
        {
            ILocation loc = Locator;
            if (lineNumber == -1 && loc != null && loc != this)
            {
                return loc.GetLineNumber();
            }
            else
            {
                return lineNumber;
            }
        }

        public virtual int GetColumnNumber()
        {
            ILocation loc = Locator;
            if (columnNumber == -1 && loc != null && loc != this)
            {
                return loc.GetColumnNumber();
            }
            else
            {
                return columnNumber;
            }
        }

        public virtual ILocation SaveLocation()
        {
            return new Loc(this);
        }

        public virtual void SetPublicId(string id)
        {
            publicId = id;
        }

        public virtual void SetSystemId(string id)
        {
            systemId = id;
        }

        public virtual void SetLineNumber(int line)
        {
            lineNumber = line;
        }

        public virtual void SetColumnNumber(int column)
        {
            columnNumber = column;
        }

        public virtual void SetSourceLocator(ILocation locator)
        {
            if (locator != null)
            {
                SetPublicId(locator.GetPublicId());
                SetSystemId(locator.GetSystemId());
                SetLineNumber(locator.GetLineNumber());
                SetColumnNumber(locator.GetColumnNumber());
            }
        }

        public virtual void SetErrorCode(string errorCode)
        {
            if (errorCode == null)
            {
                this.errorCode = null;
            }
            else
            {
                this.errorCode = new StructuredQName("err", NamespaceUri.ERR, errorCode);
            }
        }

        public virtual string GetErrorCode()
        {
            if (errorCode == null)
            {
                return null;
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

        public virtual ValidationException MakeException()
        {
            if (exception != null)
            {
                exception.MaybeSetLocation(this);
                return exception;
            }

            ValidationException ve = new ValidationException(this);
            if (errorCode == null)
            {
                ve.SetErrorCode("FORG0001");
            }
            else
            {
                ve.ErrorCodeQName = errorCode;
            }

            ve.SetHasBeenReported(errorHasBeenReported);
            exception = ve;
            return ve;
        }

        public virtual AtomicValue AsAtomic()
        {
            throw MakeException();
        }

        public virtual bool HasBeenReported()
        {
            return errorHasBeenReported;
        }

        public virtual void SetHasBeenReported(bool reported)
        {
            errorHasBeenReported = reported;
            if (exception != null)
            {
                exception.SetHasBeenReported(reported);
            }
        }

        public virtual AbsolutePath GetAbsolutePath()
        {
            if (path != null)
            {
                return path; //        } else if (node != null) {
            }
            else
            {
                return null;
            }
        }
    }
}
