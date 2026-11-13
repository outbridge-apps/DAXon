////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// An implementation of IAtomicSequence that contains no items.
    /// </summary>
    internal sealed class EmptyAtomicSequence : IAtomicSequence
    {

        private static readonly EmptyAtomicSequence INSTANCE = new EmptyAtomicSequence();

        public UnicodeString CanonicalLexicalRepresentation => EmptyUnicodeString.GetInstance();

        public UnicodeString UnicodeStringValue => EmptyUnicodeString.GetInstance();
        private EmptyAtomicSequence()
        {
        }
        public static EmptyAtomicSequence GetInstance()
        {
            return INSTANCE;
        }

        public AtomicValue Head()
        {
            return null;
        }

        public IAtomicIterator Iterate()
        {
            return EmptyIterator.OfAtomic();
        }

        public AtomicValue ItemAt(int n)
        {
            return null;
        }

        public int GetLength()
        {
            return 0;
        }

        public string GetStringValue()
        {
            return "";
        }

        public EmptyAtomicSequence Subsequence()
        {
            return this;
        }

        public bool EffectiveBooleanValue()
        {
            return false;
        }

        public EmptyAtomicSequence Reduce()
        {
            return this;
        }
        ISequenceIterator IGroundedValue.Iterate() => Iterate();
        IItem IGroundedValue.ItemAt(int arg0) => ItemAt(arg0);
        IItem IGroundedValue.Head() => Head();
        IGroundedValue IGroundedValue.Subsequence(int arg0, int arg1) => Subsequence();
        IItem ISequence.Head() => Head();
        ISequenceIterator ISequence.Iterate() => Iterate();
        public IEnumerator<AtomicValue> GetEnumerator() => System.Linq.Enumerable.Empty<AtomicValue>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        IGroundedValue IGroundedValue.Reduce() => Reduce();

        public IGroundedValue Materialize() => this; // upstream GroundedValue default
        public string ToShortString() => OutSmart.DAXon.Transformation.Err.DepictSequence(this); // upstream GroundedValue default
        public IEnumerable<IItem> AsIterable() => new IItem[0]; // empty (upstream GroundedValue.asIterable default over an empty sequence)
        public bool ContainsNode(NodeInfo sought) => false; // holds no node
        public IGroundedValue Concatenate(IGroundedValue[] others) => GroundedValueDefaults.Concatenate(this, others);
        public ISequence MakeRepeatable() => this; // upstream Sequence.makeRepeatable default
    }
}


