////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the nilled() function
    /// </summary>
    internal sealed class Nilled_1 : SystemFunction, ICallable
    {
        private static BooleanValue GetNilledProperty(NodeInfo node)
        {
            if (node == null || node.GetNodeKind() != Types.Type.ELEMENT)
            {
                return null;
            }

            return BooleanValue.Get(node.IsNilled());
        }

        public static bool IsNilled(NodeInfo node)
        {
            BooleanValue b = GetNilledProperty(node);
            return b != null && b.GetBooleanValue();
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NodeInfo node = (NodeInfo)arguments[0].Head();
            if (node == null || node.GetNodeKind() != Types.Type.ELEMENT)
            {
                return EmptySequence.GetInstance();
            }

            return BooleanValue.Get(IsNilled(node));
        }
    }
}
