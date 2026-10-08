////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the function fn:sort#2, according to the new XPath 3.1 spec in bug 29792
    /// </summary>
    internal class Sort_2 : Sort_1
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            List<ItemToBeSorted> inputList = GetItemsToBeSorted(arguments[0]);
            return DoSort(inputList, GetCollation(context, arguments[1]), context);
        }

        protected virtual IStringCollator GetCollation(IXPathContext context, ISequence collationArg)
        {
            StringValue secondArg = (StringValue)collationArg.Head();
            if (secondArg == null)
            {
                return context.GetConfiguration().GetCollation(GetRetainedStaticContext().DefaultCollationName);
            }
            else
            {
                return context.GetConfiguration().GetCollation(secondArg.GetStringValue(), StaticBaseUriString);
            }
        }
    }
}