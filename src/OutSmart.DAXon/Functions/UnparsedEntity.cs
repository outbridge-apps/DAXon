////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{
    // Faithful port of net.sf.saxon.functions.UnparsedEntity (Saxon 12.9). Was a hollow stub, so
    // unparsed-entity-uri()/unparsed-entity-public-id() were unregistered (XPST0017 at stylesheet compile).
    // Implements unparsed-entity-uri() (XSLT 1.0) and unparsed-entity-public-id() (XSLT 2.0).
    internal abstract class UnparsedEntity : SystemFunction, ICallable
    {
        public const int URI = 0;
        public const int PUBLIC_ID = 1;

        public abstract int Op { get; }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            int operation = Op;
            string arg0 = arguments[0].Head().GetStringValue();
            NodeInfo doc = null;
            if (GetArity() == 1)
            {
                IItem it = context.GetContextItem();
                if (it is NodeInfo)
                {
                    doc = ((NodeInfo)it).Root;
                }

                if (doc == null || doc.GetNodeKind() != OutSmart.DAXon.Types.Type.DOCUMENT)
                {
                    string code = operation == URI ? "XTDE1370" : "XTDE1380";
                    throw new XPathException("In function " + GetFunctionName().DisplayName +
                        ", the context item must be a node in a tree whose root is a document node", code, context);
                }
            }
            else
            {
                doc = (NodeInfo)arguments[1].Head();
                if (doc != null)
                {
                    doc = doc.Root;
                }

                if (doc == null || doc.GetNodeKind() != OutSmart.DAXon.Types.Type.DOCUMENT)
                {
                    string code = operation == URI ? "XTDE1370" : "XTDE1380";
                    throw new XPathException("In function " + GetFunctionName().DisplayName +
                        ", the second argument must be a document node", code, context);
                }
            }

            string[] ids = doc.GetTreeInfo().GetUnparsedEntity(arg0);
            string result = ids == null ? "" : ids[operation];
            if (result == null)
            {
                result = "";
            }

            return operation == URI ? (ISequence)new AnyURIValue(result) : new StringValue(result);
        }

        internal sealed class UnparsedEntityUri : UnparsedEntity
        {
            public override int Op => URI;
        }

        internal sealed class UnparsedEntityPublicId : UnparsedEntity
        {
            public override int Op => PUBLIC_ID;
        }
    }
}
