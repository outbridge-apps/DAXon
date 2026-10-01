////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;using OutSmart.DAXon.Functions;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Collections
{
    /// <summary>
    /// An iterator over a sequence of integers with regular steps, e.g. 2, 4, 6, 8...
    /// </summary>
    internal sealed class IntStepIterator : AbstractIntIterator
    {
        private int current;
        private readonly int step;
        private readonly int limit;
        public IntStepIterator(int start, int step, int limit)
        {
            this.current = start;
            this.step = step;
            this.limit = limit;
        }

        public override bool HasNext()
        {
            return step > 0 ? current <= limit : current >= limit;
        }

        public override int Next()
        {
            int n = current;
            current += step;
            return n;
        }
    }
}