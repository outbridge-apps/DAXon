////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Flwor
{
    internal sealed class CountClausePull : TuplePull
    {
        private readonly TuplePull @base;
        private readonly int slot;
        private int count = 0;
        public CountClausePull(TuplePull @base, CountClause countClause)
        {
            this.@base = @base;
            this.slot = countClause.RangeVariable.LocalSlotNumber;
        }

        public override bool NextTuple(IXPathContext context)
        {
            if (!@base.NextTuple(context))
            {
                count = 0;
                context.SetLocalVariable(slot, Int64Value.ZERO);
                return false;
            }
            context.SetLocalVariable(slot, new Int64Value(++count));
            return true;
        }
    }
}
