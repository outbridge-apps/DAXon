////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Flwor
{
    internal sealed class ForClausePush : TuplePush
    {
        protected TuplePush destination;
        protected ForClause forClause;
        public ForClausePush(Outputter outputter, TuplePush destination, ForClause forClause) : base(outputter)
        {
            this.destination = destination;
            this.forClause = forClause;
        }

        public override void ProcessTuple(IXPathContext context)
        {
            ISequenceIterator iter = forClause.GetIterator(context);
            int pos = 0;
            IItem next;
            while ((next = iter.Next()) != null)
            {
                context.SetLocalVariable(forClause.RangeVariable.LocalSlotNumber, next);
                if (forClause.PositionVariable != null)
                {
                    context.SetLocalVariable(forClause.PositionVariable.LocalSlotNumber, new Int64Value(++pos));
                }
                destination.ProcessTuple(context);
            }
        }

        public override void Dispose()
        {
            destination.Dispose();
        }
    }
}
