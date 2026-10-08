////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
namespace OutSmart.DAXon.Transformation
{
    public class UncheckedXPathException : Exception
    {
        public UncheckedXPathException(XPathException cause) : base(cause?.Message, cause)
        {
        }

        public UncheckedXPathException(string message) : base(new XPathException(message).Message, new XPathException(message))
        {
        }

        public UncheckedXPathException(string message, string errorCode) : base(new XPathException(message, errorCode).Message, new XPathException(message, errorCode))
        {
        }

        public UncheckedXPathException(Exception cause) : base(new XPathException(cause).Message, new XPathException(cause))
        {
        }

        public virtual XPathException GetXPathException()
        {
            return (XPathException)InnerException;
        }

        public string GetMessage()
        {
            return InnerException.Message;
        }
    }
}