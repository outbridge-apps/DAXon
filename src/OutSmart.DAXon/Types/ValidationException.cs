////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using System;
using System.Text;
namespace OutSmart.DAXon.Types
{
    public class ValidationException : XPathException
    {
        private ValidationFailure failure;

        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        public virtual NodeInfo Node
        {
            get
            {
                if (failure != null)
                {
                    return failure.GetInvalidNode();
                }
                else
                {
                    return null;
                }
            }
        }
        public ValidationException(Exception exception) : base((exception).ToString())
        {
        }

        /*setIsTypeError(true);*/
        public ValidationException(string message, Exception exception) : base(message, (Exception)exception)
        {
        }

        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        public ValidationException(string message, ILocation locator) : base(message, null, locator)
        {
        }

        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        public ValidationException(ValidationFailure failure) : base(failure.GetMessage(), failure.GetErrorCode(), failure.Locator)
        {
            this.failure = failure;
        }

        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        public string GetMessage()
        {

            // The message held in the ValidationFailure is sometimes updated, so we use that one in preference.
            // The message in the exception can't be updated, it can only be set from the constructor.
            if (failure != null)
            {
                return failure.GetMessage();
            }
            else
            {
                return base.Message;
            }
        }

        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        public virtual ValidationFailure GetValidationFailure()
        {
            if (failure != null)
            {
                return failure;
            }
            else
            {
                ValidationFailure failure = new ValidationFailure(Message);
                failure.ErrorCodeQName = ErrorCodeQName;
                failure.Locator = GetLocator();
                return failure;
            }
        }

        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("ValidationException: ");
            string message = Message;
            if (message != null)
            {
                sb.Append(message);
            }

            return sb.ToString();
        }

        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        public virtual string GetPath()
        {
            AbsolutePath ap = GetAbsolutePath();
            if (ap == null)
            {
                NodeInfo node = Node;
                if (node != null)
                {
                    return Navigator.GetPath(node);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return ap.PathUsingAbbreviatedUris;
            }
        }

        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        /*setIsTypeError(true);*/
        public virtual AbsolutePath GetAbsolutePath()
        {
            if (failure != null)
            {
                return failure.GetPath();
            }
            else
            {
                return null;
            }
        }
    }
}