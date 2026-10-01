////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class CompositeAtomicKey
    {
        // This is extracted as a separate class primarily to allow different implementations for Java and C#.
        // This is because <code>List.equals()</code> has the desired semantics on Java, but not on C#.
        private IList<IAtomicMatchKey> keys;
        public CompositeAtomicKey(IList<IAtomicMatchKey> keys)
        {
            this.keys = keys;
        }

        public override bool Equals(object obj)
        {
            return obj is CompositeAtomicKey other && keys.SequenceEqual(other.keys);
        }

        public override int GetHashCode()
        {
            int h = 17;
            foreach (var k in keys)
            {
                h = h * 31 + (k?.GetHashCode() ?? 0);
            }
            return h;
        }
    }
}