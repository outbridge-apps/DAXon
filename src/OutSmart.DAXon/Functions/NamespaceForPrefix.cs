////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the function namespace-uri-for-prefix()
    /// </summary>
    internal sealed class NamespaceForPrefix : SystemFunction, ICallable
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            AnyURIValue result = NamespaceUriForPrefix((StringValue)arguments[0].Head(), (NodeInfo)arguments[1].Head());
            return SequenceTool.ItemOrEmpty(result);
        }

        private static AnyURIValue NamespaceUriForPrefix(StringValue p, NodeInfo element)
        {
            string prefix;
            if (p == null)
            {
                prefix = "";
            }
            else
            {
                prefix = p.GetStringValue();
            }

            INamespaceResolver resolver = element.AllNamespaces;
            NamespaceUri uri = resolver.GetURIForPrefix(prefix, true);
            if (uri == null || uri.IsEmpty())
            {
                return null;
            }

            return new AnyURIValue(uri.ToUnicodeString());
        }
    }
}
