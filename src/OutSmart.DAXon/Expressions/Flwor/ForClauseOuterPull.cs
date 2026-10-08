////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Flwor
{
    // "for ... allowing empty" (outer-join semantics): an empty binding sequence still delivers one tuple
    // with the range variable bound to the empty sequence.
    internal sealed class ForClauseOuterPull : ForClausePull
    {
        public ForClauseOuterPull(TuplePull @base, ForClause forClause) : base(@base, forClause)
        {
        }

        public override bool NextTuple(IXPathContext context)
        {
            while (true)
            {
                // Same per-tuple deadline check as ForClausePull (see there): the outer-join
                // variant overrides NextTuple in full, so it needs its own.
                context.GetController().CheckTimeoutPerStep();
                IItem next;
                if (currentIteration == null)
                {
                    if (!@base.NextTuple(context))
                    {
                        return false;
                    }
                    currentIteration = SequenceTool.FocusTracker(forClause.GetIterator(context));
                    next = currentIteration.Next();
                    if (next == null)
                    {
                        context.SetLocalVariable(forClause.RangeVariable.LocalSlotNumber, EmptySequence.GetInstance());
                        if (forClause.PositionVariable != null)
                        {
                            context.SetLocalVariable(forClause.PositionVariable.LocalSlotNumber, Int64Value.ZERO);
                        }
                        currentIteration = null;
                        return true;
                    }
                }
                else
                {
                    next = currentIteration.Next();
                }
                if (next != null)
                {
                    context.SetLocalVariable(forClause.RangeVariable.LocalSlotNumber, next);
                    if (forClause.PositionVariable != null)
                    {
                        context.SetLocalVariable(forClause.PositionVariable.LocalSlotNumber, new Int64Value(currentIteration.Position()));
                    }
                    return true;
                }
                currentIteration = null;
            }
        }
    }
}
