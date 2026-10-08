////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Collections
{
    /// <summary>
    /// An iterator over a single integer
    /// </summary>
    internal sealed class IntSingletonIterator : AbstractIntIterator
    {
        private readonly int value;
        bool gone;
        public IntSingletonIterator(int value)
        {
            this.value = value;
        }

        public override bool HasNext()
        {
            return !gone;
        }

        public override int Next()
        {
            gone = true;
            return value;
        }
    }
}