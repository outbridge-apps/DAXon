////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
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
using OutSmart.DAXon.Collections.Trie;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Model
{
    internal sealed class AtomicArray : IAtomicSequence
    {
        private static readonly IList<AtomicValue> emptyAtomicList = new List<AtomicValue>();
        public static AtomicArray EMPTY_ATOMIC_ARRAY = new AtomicArray(emptyAtomicList);
        private readonly IList<AtomicValue> content;

        public UnicodeString CanonicalLexicalRepresentation => UnicodeStringValue;

        public UnicodeString UnicodeStringValue
        {
            get
            {
                UnicodeBuilder ub = new UnicodeBuilder();
                bool first = true;
                foreach (AtomicValue av in content)
                {
                    if (!first)
                    {
                        ub.Append(' ');
                    }
                    else
                    {
                        first = false;
                    }

                    ub.Accept(av.UnicodeStringValue);
                }

                return ub.ToUnicodeString();
            }
        }
        public AtomicArray(IList<AtomicValue> content)
        {
            this.content = content;
        }

        public AtomicArray(ISequenceIterator iter)
        {
            List<AtomicValue> list = new List<AtomicValue>(10);
            SequenceTool.Supply(iter, (item) => list.Add((AtomicValue)item));
            content = list;
        }

        public AtomicValue Head()
        {
            return content.Count == 0 ? null : content[0];
        }

        public IAtomicIterator Iterate()
        {

            return new ListIterator.OfAtomic<AtomicValue>(content);
        }

        public AtomicValue ItemAt(int n)
        {
            if (n >= 0 && n < content.Count)
            {
                return content[n];
            }
            else
            {
                return null;
            }
        }

        public int GetLength()
        {
            return content.Count;
        }

        public AtomicArray Subsequence(int start, int length)
        {
            if (start < 0)
            {
                start = 0;
            }

            if (start + length > content.Count)
            {
                length = content.Count - start;
            }

            return new AtomicArray(content.GetRange(start, (start + length) - (start)));
        }

        public string GetStringValue()
        {
            StringBuilder sb = new StringBuilder(64);
            bool first = true;
            foreach (AtomicValue av in content)
            {
                if (!first)
                {
                    sb.Append(' ');
                }
                else
                {
                    first = false;
                }

                sb.Append(av.GetStringValue());
            }

            return sb.ToString();
        }

        public bool EffectiveBooleanValue()
        {
            return ExpressionTool.EffectiveBooleanValue(Iterate());
        }

        public IGroundedValue Reduce()
        {
            int len = GetLength();
            if (len == 0)
            {
                return EmptySequence.GetInstance();
            }
            else if (len == 1)
            {
                return ItemAt(0);
            }
            else
            {
                return this;
            }
        }
        ISequenceIterator IGroundedValue.Iterate() => Iterate();
        IItem IGroundedValue.ItemAt(int arg0) => ItemAt(arg0);
        IItem IGroundedValue.Head() => Head();
        IGroundedValue IGroundedValue.Subsequence(int arg0, int arg1) => Subsequence(arg0, arg1);
        IItem ISequence.Head() => Head();
        ISequenceIterator ISequence.Iterate() => Iterate();
        public IEnumerator<AtomicValue> GetEnumerator() => content.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => content.GetEnumerator();

        // AtomicArray is an immutable, already-grounded atomic sequence, so the grounded-value defaults are
        // trivial. These were auto-generated NotImplementedException stubs: Materialize() threw during
        // ApplyFunctionConversionRules when e.g. xs:IDREFS('a b c') (an AtomicArray) was passed through a
        // dynamic function call (function-lookup(...)(...)). It contains no nodes.
        public IGroundedValue Materialize() => this;
        public string ToShortString() => "atomic sequence of length " + GetLength();
        public IEnumerable<IItem> AsIterable()
        {
            foreach (AtomicValue __v in content)
            {
                yield return __v;
            }
        }
        public bool ContainsNode(NodeInfo sought) => false;
        public IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public ISequence MakeRepeatable() => this;
    }
}
