////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the collation-key function defined in the XSLT 3.0 and XPath 3.1 specifications.
    /// </summary>
    internal sealed class CollationKeyFn : CollatingFunctionFixed
    {
        public static Base64BinaryValue GetCollationKey(UnicodeString s, IStringCollator collator)
        {
            AtomicValue val = collator.GetCollationKey(s).AsAtomic();
            if (val is Base64BinaryValue bb)
            {
                return bb;
            }
            else if (val is StringValue sv)
            {
                return sv.CodepointCollationKey;
            }
            else
            {
                throw new InvalidOperationException("Collation key must be Base64Binary");
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue @in = (StringValue)arguments[0].Head();
            IStringCollator collator = StringCollator;
            return GetCollationKey(@in.UnicodeStringValue, collator);
        }
    }
}
