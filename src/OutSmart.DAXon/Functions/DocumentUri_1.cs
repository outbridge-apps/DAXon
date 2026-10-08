////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the document-uri() function
    /// </summary>
    internal sealed class DocumentUri_1 : ScalarSystemFunction
    {
        public override AtomicValue Evaluate(IItem item, IXPathContext context)
        {
            return GetDocumentURI((NodeInfo)item, context);
        }

        public static AnyURIValue GetDocumentURI(NodeInfo node, IXPathContext c)
        {
            if (node.GetNodeKind() == Types.Type.DOCUMENT)
            {
                Controller controller = c.GetController();
                DocumentPool pool = controller.GetDocumentPool();
                string docURI = pool.GetDocumentURI(node);
                if (docURI == null)
                {
                    return null;
                }
                else if (docURI == "")
                {
                    return null;
                }
                else
                {
                    return new AnyURIValue(docURI);
                }
            }
            else
            {
                return null;
            }
        }
    }
}