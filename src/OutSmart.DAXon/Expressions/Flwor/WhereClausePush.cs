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
    internal sealed class WhereClausePush : TuplePush
    {
        private readonly TuplePush destination;
        private readonly IBooleanEvaluator predicate;
        public WhereClausePush(Outputter outputter, TuplePush destination, IBooleanEvaluator predicate) : base(outputter)
        {
            this.destination = destination;
            this.predicate = predicate;
        }

        public override void ProcessTuple(IXPathContext context)
        {
            if (predicate(context))
            {
                destination.ProcessTuple(context);
            }
        }

        public override void Dispose()
        {
            destination.Dispose();
        }
    }
}
