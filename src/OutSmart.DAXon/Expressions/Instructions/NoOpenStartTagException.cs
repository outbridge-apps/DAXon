////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Exception indicating that an attribute or namespace node has been written when there is no open
    /// element to write it to. (Upstream lives in net.sf.saxon.event; kept here where the stub lived.)
    /// </summary>
    internal sealed class NoOpenStartTagException : XPathException
    {

        public NoOpenStartTagException(string message) : base(message)
        {
        }
        public static NoOpenStartTagException MakeNoOpenStartTagException(
                int nodeKind, string name, int hostLanguage, bool parentIsDocument, ILocation startElementLocationId)
        {
            string message;
            string errorCode;
            bool isXslt = hostLanguage == (int)HostLanguage.XSLT;
            if (parentIsDocument)
            {
                string kind = nodeKind == OutSmart.DAXon.Types.Type.ATTRIBUTE ? "an attribute" : "a namespace";
                message = "Cannot create " + kind + " node (" + name + ") whose parent is a document node";
                errorCode = isXslt ? "XTDE0420" : "XPTY0004";
            }
            else
            {
                string kind = nodeKind == OutSmart.DAXon.Types.Type.ATTRIBUTE ? "An attribute" : "A namespace";
                message = kind + " node (" + name + ") cannot be created after a child of the containing element";
                errorCode = isXslt ? "XTDE0410" : "XQTY0024";
            }

            if (startElementLocationId != null && startElementLocationId.GetLineNumber() != -1)
            {
                message += ". Most recent element start tag was output at line " +
                    startElementLocationId.GetLineNumber() + " of module " +
                        new StandardDiagnostics().AbbreviateLocationURI(startElementLocationId.GetSystemId());
            }

            NoOpenStartTagException err = new NoOpenStartTagException(message);
            err.SetErrorCode(errorCode);
            return err;
        }
    }
}
