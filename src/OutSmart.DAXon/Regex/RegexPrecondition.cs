////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// A precondition that must be true if a regular expression is to match
    /// </summary>
    internal sealed class RegexPrecondition
    {
        public Operation operation;
        public int fixedPosition;
        public int minPosition;
        public RegexPrecondition(Operation op, int fixedPos, int minPos)
        {
            this.operation = op;
            this.fixedPosition = fixedPos;
            this.minPosition = minPos;
        }
    }
}