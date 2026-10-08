////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Functions
{
    internal sealed class Count
    {
        // Java's static Count.count(iter) -> static method (same name as class).
        public static int CountFn(ISequenceIterator iter)
        {
            int n = 0;
            if (iter != null)
            {
                while (iter.Next() != null)
                {
                    n++;
                }
            }

            return n;
        }
        public static int CountLocal(ISequenceIterator iter)
        {
            int n = 0; while (iter != null && iter.Next() != null) n++; return n;
        }
        public static int SteppingCount(ISequenceIterator iter) => CountLocal(iter);
        // The Java code calls Count.Count(iter) Ã¢â‚¬â€ provide alias.
    }
}
