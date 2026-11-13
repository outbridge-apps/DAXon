////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Expressions.Flwor
{
    public abstract class TuplePull
    {
        // A clause pulls from the one before it, so a long chain recurses once per clause: each override that does
        // so probes the stack first (5000 where-clauses overflowed a 256KB thread).
        public abstract bool NextTuple(IXPathContext context);
        public virtual void Dispose()
        {
        }
    }
}