////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Expressions.Flwor
{
    // The initial tuple source of a FLWOR pull pipeline: delivers exactly ONE (empty) tuple, then reports
    // exhaustion. The previous stub always returned false, so every FLWOR tuple stream yielded ZERO tuples.
    internal sealed class SingularityPull : TuplePull
    {
        private bool done;
        public SingularityPull() { }
        public override bool NextTuple(IXPathContext context)
        {
            if (done)
            {
                return false;
            }
            done = true;
            return true;
        }
    }
}
