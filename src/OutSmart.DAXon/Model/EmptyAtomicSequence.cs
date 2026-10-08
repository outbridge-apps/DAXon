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

        public EmptyAtomicSequence Subsequence(int start, int length)
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
        IGroundedValue IGroundedValue.Subsequence(int arg0, int arg1) => Subsequence(arg0, arg1);
        IItem ISequence.Head() => Head();
        ISequenceIterator ISequence.Iterate() => Iterate();
        public IEnumerator<AtomicValue> GetEnumerator() => System.Linq.Enumerable.Empty<AtomicValue>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        IGroundedValue IGroundedValue.Reduce() => Reduce();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public IGroundedValue Materialize() => this; // upstream GroundedValue default
        public string ToShortString() => OutSmart.DAXon.Transformation.Err.DepictSequence(this); // upstream GroundedValue default
        public IEnumerable<IItem> AsIterable() => new IItem[0]; // empty (upstream GroundedValue.asIterable default over an empty sequence)
        public bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public ISequence MakeRepeatable() => this; // upstream Sequence.makeRepeatable default
    }
}


