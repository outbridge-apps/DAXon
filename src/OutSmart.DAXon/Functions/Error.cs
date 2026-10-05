////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement XPath function fn:error()
    /// </summary>
    internal sealed class Error : SystemFunction, ICallable
    {
        public override int GetSpecialProperties(Expression[] arguments)
        {
            return base.GetSpecialProperties(arguments) & ~StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        public IItem ErrorFn(IXPathContext context, QNameValue errorCode, StringValue desc, ISequenceIterator errObject)
        {
            QNameValue qname = null;
            if (GetArity() > 0)
            {
                qname = errorCode;
            }

            if (qname == null)
            {
                qname = new QNameValue("err", NamespaceUri.ERR, GetArity() == 1 ? "FOTY0004" : "FOER0000", BuiltInAtomicType.QNAME, false);
            }

            string description;
            if (GetArity() > 1)
            {
                description = desc == null ? "" : desc.GetStringValue();
            }
            else
            {
                description = "Error signalled by application call on error()";
            }

            XPathException e = new UserDefinedXPathException(description).WithErrorCode(qname.GetStructuredQName()).WithXPathContext(context);
            if (GetArity() > 2 && errObject != null)
            {
                IGroundedValue errorObject = SequenceTool.ToGroundedValue(errObject);
                if (errorObject.GetLength() == 1)
                {
                    IItem root = errorObject.Head();
                    if ((root is NodeInfo) && ((NodeInfo)root).GetNodeKind() == Types.Type.DOCUMENT)
                    {
                        IAxisIterator iter = ((NodeInfo)root).IterateAxis(AxisInfo.CHILD, new NameTest(Types.Type.ELEMENT, NamespaceUri.NULL, "error", context.GetConfiguration().GetNamePool()));
                        NodeInfo errorElement = iter.Next();
                        if (errorElement != null)
                        {
                            string module = errorElement.GetAttributeValue(NamespaceUri.NULL, "module");
                            string lineVal = errorElement.GetAttributeValue(NamespaceUri.NULL, "line");
                            int line;
                            try
                            {
                                line = lineVal == null ? -1 : int.Parse(lineVal, CultureInfo.InvariantCulture);
                            }
                            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                            {
                                line = -1;
                            }

                            string columnVal = errorElement.GetAttributeValue(NamespaceUri.NULL, "column");
                            int col;
                            try
                            {
                                col = columnVal == null ? -1 : int.Parse(columnVal, CultureInfo.InvariantCulture);
                            }
                            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                            {
                                col = -1;
                            }

                            Loc locator = new Loc(module, line, col);
                            e.SetLocator(locator);
                        }
                    }
                }

                e.ErrorObject = errorObject;
            }

            throw e;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            int len = arguments.Length;
            switch (len)
            {
                case 0:
                    return ErrorFn(context, null, null, null);
                case 1:

                    // Note: in XPath 3.1 first arg may be an empty sequence, and then error code is FOER0000.
                    // Previously in XPath 3.0 error#1 does not allow the first argument to be an empty sequence. So we took
                    // care to raise XPTY0004 in this case. But we still report a generic error message, rather
                    // than complaining specifically about the missing error code
                    QNameValue arg0 = (QNameValue)arguments[0].Head();
                    if (arg0 == null)
                    {
                        arg0 = new QNameValue("err", NamespaceUri.ERR, "FOER0000");
                    }

                    return ErrorFn(context, arg0, null, null);
                case 2:
                    return ErrorFn(context, (QNameValue)arguments[0].Head(), (StringValue)arguments[1].Head(), null);
                case 3:
                    return ErrorFn(context, (QNameValue)arguments[0].Head(), (StringValue)arguments[1].Head(), arguments[2].Iterate());
                default:
                    return null;
            }
        }

        internal sealed class UserDefinedXPathException : XPathException
        {
            public UserDefinedXPathException(string message) : base(message)
            {
            }
        }
    }
}