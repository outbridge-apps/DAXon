////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Collections.Zeno
{
    internal sealed class ZenoSequence : IGroundedValue
    {
        private readonly ZenoChain<IItem> chain;

        public UnicodeString UnicodeStringValue
        {
            get
            {
                switch (GetLength())
                {
                    case 0:
                        return EmptyUnicodeString.GetInstance();
                    case 1:
                        return this.Head().UnicodeStringValue;
                    default:
                        UnicodeBuilder builder = new UnicodeBuilder();
                        UnicodeString separator = EmptyUnicodeString.GetInstance();
                        foreach (IItem item in chain)
                        {
                            builder.Append(separator);
                            separator = StringConstants.SINGLE_SPACE;
                            builder.Append(item.GetStringValue());
                        }

                        return builder.ToUnicodeString();
                }
            }
        }
        public ZenoSequence()
        {
            chain = new ZenoChain<IItem>();
        }

        public ZenoSequence(ZenoChain<IItem> chain)
        {
            this.chain = chain;
        }

        public ISequenceIterator Iterate()
        {
            return new ZenoSequenceIterator(this);
        }

        public IItem ItemAt(int n)
        {
            return n >= 0 && n < chain.Size() ? chain[n] : null;
        }

        public IItem Head()
        {
            return chain.IsEmpty() ? null : chain[0];
        }

        public IGroundedValue Subsequence(int start, int length)
        {
            if (start < 0)
            {
                start = 0;
            }

            int size = chain.Count();
            if (start >= size || length <= 0)
            {
                return EmptySequence.GetInstance();
            }

            if ((long)start + (long)length > (long)size)
            {
                length = size - start;
            }

            if (length == 1)
            {
                return ItemAt(start);
            }
            else
            {
                return new ZenoSequence(chain.SubList(start, start + length));
            }
        }

        public int GetLength()
        {
            return chain.Count();
        }

        public string GetStringValue()
        {
            switch (GetLength())
            {
                case 0:
                    return "";
                case 1:
                    return this.Head().GetStringValue();
                default:
                    StringBuilder builder = new StringBuilder();
                    string separator = "";
                    foreach (IItem item in chain)
                    {
                        builder.Append(separator);
                        separator = " ";
                        builder.Append(item.GetStringValue());
                    }

                    return builder.ToString();
            }
        }

        public ZenoSequence Append(IItem item)
        {
            return new ZenoSequence(chain.Add(item));
        }

        public ZenoSequence AppendSequence(IGroundedValue items)
        {
            if (chain.IsEmpty() && items is ZenoSequence)
            {
                return (ZenoSequence)items;
            }

            switch (items.GetLength())
            {
                case 0:
                    return this;
                case 1:
                    IItem item = items.Head();
                    return new ZenoSequence(chain.Add(item));
                default:
                    if (items is ZenoSequence)
                    {
                        return new ZenoSequence(chain.Concat(((ZenoSequence)items).chain));
                    }
                    else
                    {
                        return new ZenoSequence(chain.AddAll(items.AsIterable()));
                    }

                    break;
            }
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public bool EffectiveBooleanValue() => OutSmart.DAXon.Expressions.Parsing.ExpressionTool.EffectiveBooleanValue(Iterate()); // upstream GroundedValue default
        public IGroundedValue Reduce() => this;
        // A ZenoSequence is already a GroundedValue, so materialize()/reduce() return itself
        // (GroundedValue defaults). The stubs threw, breaking fold-left etc. whose accumulator is a
        // ZenoSequence sequence value.
        public IGroundedValue Materialize() => this;
        public string ToShortString() => OutSmart.DAXon.Transformation.Err.DepictSequence(this); // upstream GroundedValue default
        public IEnumerable<IItem> AsIterable() { ISequenceIterator it = Iterate(); IItem i; while ((i = it.Next()) != null) yield return i; } // upstream GroundedValue.asIterable
        public bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        // A ZenoSequence is an immutable grounded value (Materialize() => this), so it is already
        // repeatable — matches AtomicValue/EmptySequence/IntegerRange. Was a hollow stub that threw when a
        // `let $x := <zeno-seq>` binding is read more than once (XPathContextMinor.SetLocalVariable).
        public ISequence MakeRepeatable() => this;

        /// <summary>
        /// A ISequenceIterator over a ZenoSequence
        /// </summary>
        internal sealed class ZenoSequenceIterator : IGroundedIterator, ILastPositionFinder, ILookaheadIterator
        {
            // This class is not a LookAheadIterator on C#, because the underlying C# enumerator has no side-effect-free
            // hasNext() operation.
            private readonly ZenoSequence sequence;
            private readonly IEnumerator<IItem> chainIterator;
            private IItem lookahead;
            private bool lookaheadFilled;
            private int position = 0;

            public bool HasNext
            {
                get
                {
                    if (!lookaheadFilled && chainIterator.MoveNext())
                    {
                        lookahead = chainIterator.Current;
                        lookaheadFilled = true;
                    }

                    return lookaheadFilled;
                }
            }
            public ZenoSequenceIterator(ZenoSequence sequence)
            {
                this.sequence = sequence;
                this.chainIterator = sequence.chain.IIterator();
            }

            public IItem Next()
            {
                position++;
                if (lookaheadFilled)
                {
                    lookaheadFilled = false;
                    return lookahead;
                }

                return chainIterator.MoveNext() ? chainIterator.Current : null;
            }

            public bool SupportsGetLength()
            {
                return true;
            }

            public int GetLength()
            {
                return sequence.GetLength();
            }

            public bool IsActuallyGrounded()
            {
                return true;
            }

            public IGroundedValue GetResidue()
            {
                return sequence.Subsequence(position, int.MaxValue);
            }

            public IGroundedValue Materialize()
            {
                return sequence;
            }

            public bool SupportsHasNext()
            {
                return true;
            }
            public void Dispose() { }
        }
    }
}