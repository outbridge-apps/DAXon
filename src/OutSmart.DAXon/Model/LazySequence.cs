////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;using OutSmart.DAXon.Functions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// A sequence that wraps an iterator, without being materialized. It can only be used once.
    /// </summary>
    internal sealed class LazySequence : ISequence
    {
        ISequenceIterator iterator;
        bool used = false;
        public LazySequence(ISequenceIterator iterator)
        {
            this.iterator = iterator;
        }

        public IItem Head()
        {
            return Iterate().Next();
        }

        public ISequenceIterator Iterate()
        {
            lock (this)
            {
                if (used)
                {
                    throw new InvalidOperationException("A LazySequence can only be read once");
                }
                else
                {
                    used = true;
                    return iterator;
                }
            }
        }

        public ISequence MakeRepeatable()
        {
            return Materialize();
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public IGroundedValue Materialize() { var __it = Iterate(); var __l = new List<IItem>(); for (IItem __x; (__x = __it.Next()) != null;) { __l.Add(__x); } return SequenceExtent.MakeSequenceExtent(__l); }
    }
}