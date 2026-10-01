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
    internal sealed class WhereClausePull : TuplePull
    {
        private readonly TuplePull @base;
        private readonly IBooleanEvaluator predicate;
        public WhereClausePull(TuplePull @base, IBooleanEvaluator predicate)
        {
            this.@base = @base;
            this.predicate = predicate;
        }

        public override bool NextTuple(IXPathContext context)
        {
            while (@base.NextTuple(context))
            {
                if (predicate(context))
                {
                    return true;
                }
            }
            return false;
        }

        public override void Dispose()
        {
            @base.Dispose();
        }
    }
}
