////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Flwor
{
    internal sealed class CountClausePush : TuplePush
    {
        private readonly TuplePush destination;
        private readonly int slot;
        private int count = 0;
        public CountClausePush(Outputter outputter, TuplePush destination, CountClause countClause) : base(outputter)
        {
            this.destination = destination;
            this.slot = countClause.RangeVariable.LocalSlotNumber;
        }

        public override void ProcessTuple(IXPathContext context)
        {
            context.SetLocalVariable(slot, new Int64Value(++count));
            destination.ProcessTuple(context);
        }

        public override void Dispose()
        {
            destination.Dispose();
        }
    }
}
