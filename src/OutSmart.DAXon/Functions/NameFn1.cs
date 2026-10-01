////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Functions
{

    // fn:name#1/local-name#1: Call-only impls — the full Name_1/LocalName_1 ports drag the StringElaborator
    // cluster. Java: StringValue.makeStringValue(node.getDisplayName()/getLocalPart()).
    internal sealed class NameFn1 : ScalarSystemFunction
    {
        private readonly bool _local;
        public NameFn1(bool local) { _local = local; }
        public static Func<NameFn1> NewName() => () => new NameFn1(false);
        public override AtomicValue Evaluate(IItem item, IXPathContext context)
        {
            if (!(item is NodeInfo node))
            {
                throw new XPathException("Argument to " + (_local ? "local-name()" : "name()") + " is not a node", "XPTY0004");
            }
            return StringValue.MakeStringValue(_local ? node.GetLocalPart() : node.DisplayName);
        }
        public override ISequence ResultWhenEmpty()
        {
            return StringValue.EMPTY_STRING;
        }
    }
}
