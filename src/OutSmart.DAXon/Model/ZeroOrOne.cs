////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;

using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// A value that is a sequence containing zero or one items.
    /// </summary>
    internal class ZeroOrOne<T> : IGroundedValue
    {
        private static readonly ZeroOrOne<object> EMPTY = new ZeroOrOne<object>(null);
        private readonly T item; // may be null, to represent an empty sequence

        public virtual UnicodeString UnicodeStringValue => item == null ? EmptyUnicodeString.GetInstance() : ((IItem)(object)item).UnicodeStringValue;

        public ZeroOrOne(T item)
        {
            this.item = item;
        }
        public static ZeroOrOne<TItem> Empty<TItem>() => (ZeroOrOne<TItem>)(object)EMPTY;

        public virtual string GetStringValue()
        {
            return item == null ? "" : ((IItem)(object)item).GetStringValue();
        }

        public virtual T Head()
        {
            return item;
        }

        public virtual int GetLength()
        {
            return item == null ? 0 : 1;
        }

        public virtual T ItemAt(int n)
        {
            if (n == 0 && item != null)
            {
                return item;
            }
            else
            {
                return default(T);
            }
        }

        public virtual IGroundedValue Subsequence(int start, int length)
        {
            if (item != null && start <= 0 && start + length > 0)
            {
                return this;
            }
            else
            {
                return EmptySequence.GetInstance();
            }
        }

        /// <summary>
        /// Return an iterator over this value.
        /// </summary>
        public virtual ISequenceIterator Iterate()
        {
            return SingletonIterator.MakeIterator((IItem)item);
        }

        public virtual bool EffectiveBooleanValue()
        {
            return ExpressionTool.EffectiveBooleanValue((ISequenceIterator)(item));
        }

        public override string ToString()
        {
            return item == null ? "null" : item.ToString();
        }

        public virtual IGroundedValue Reduce()
        {
            if (item == null)
            {
                return EmptySequence.GetInstance();
            }
            else
            {
                return (IGroundedValue)item;
            }
        }
        IItem IGroundedValue.ItemAt(int arg0) => (IItem)(object)ItemAt(arg0);
        IItem IGroundedValue.Head() => (IItem)(object)Head();
        IItem ISequence.Head() => (IItem)(object)Head();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual IGroundedValue Materialize() => this; // upstream GroundedValue default
        public virtual string ToShortString() => OutSmart.DAXon.Transformation.Err.DepictSequence(this); // upstream GroundedValue default
        public virtual IEnumerable<IItem> AsIterable() => item == null ? new IItem[0] : new IItem[] { (IItem)(object)item }; // 0-or-1 items (upstream GroundedValue.asIterable)
        public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public virtual ISequence MakeRepeatable() => this; // upstream Sequence.makeRepeatable default
    }
}
