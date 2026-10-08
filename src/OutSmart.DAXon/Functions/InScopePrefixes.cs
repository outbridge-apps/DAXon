////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the XPath 2.0 function fn:in-scope-prefixes()
    /// </summary>
    internal sealed class InScopePrefixes : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NodeInfo element = (NodeInfo)arguments[0].Head();
            string[] prefixes = element.AllNamespaces.PrefixArray;
            IList<IItem> result = new List<IItem>();
            foreach (string s in prefixes)
            {
                result.Add(new StringValue(s));
            }

            result.Add(StringValue.Bmp("xml"));
            return SequenceExtent.MakeSequenceExtent(result);
        }
    }
}
