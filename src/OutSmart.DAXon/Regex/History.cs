////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Regex
{
    internal sealed class History
    {
        private readonly Dictionary<Operation, IntSet> zeroLengthMatches = new Dictionary<Operation, IntSet>();
        public bool IsDuplicateZeroLengthMatch(Operation op, int position)
        {
            IntSet positions = zeroLengthMatches.GetOrDefault(op);
            if (positions == null)
            {
                positions = new IntHashSet(position);
                positions.Add(position);
                zeroLengthMatches[op] = positions;
                return false;
            }
            else
            {

                // return true if the position was already present in the list
                return !positions.Add(position);
            }
        }
    }
}