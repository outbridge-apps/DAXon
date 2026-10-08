////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Threading;
namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// An integer that can be incremented atomically with thread safety
    /// </summary>
    internal sealed class AtomicCounter
    {
        // Note, this class is extracted into a separate module to allow different Java and C# implementations
        private long counter;

        public long AndIncrement => Interlocked.Increment(ref counter) - 1;
        public AtomicCounter(int initialValue)
        {
            Init(initialValue);
        }

        private void Init(int initialValue)
        {
            counter = initialValue;
        }
    }
}