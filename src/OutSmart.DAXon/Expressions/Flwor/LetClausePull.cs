////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Expressions.Flwor
{
    internal sealed class LetClausePull : TuplePull
    {
        private readonly TuplePull @base;
        private readonly LetClause letClause;
        public LetClausePull(TuplePull @base, LetClause letClause)
        {
            this.@base = @base;
            this.letClause = letClause;
        }

        public override bool NextTuple(IXPathContext context)
        {
            if (!@base.NextTuple(context))
            {
                return false;
            }
            letClause.EvaluateRangeVariable(context);
            return true;
        }

        public override void Dispose()
        {
            @base.Dispose();
        }
    }
}
