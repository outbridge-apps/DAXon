////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values
{
    internal abstract class SequenceExtent : IGroundedValue
    {
        public abstract UnicodeString UnicodeStringValue { get; }
        public static Of<IItem> From(ISequenceIterator iter)
        {
            List<IItem> list = new List<IItem>(!SequenceTool.SupportsGetLength(iter) ? 20 : ((ILastPositionFinder)iter).GetLength());
            for (IItem item; (item = iter.Next()) != null;)
            {
                list.Add(item);
                if (list.Count == MemoryLedger.BigCount)
                {
                    MemoryLedger.HoldOnce(list, -1, MemoryLedger.ItemListSizer);   // summed as it grows
                }
            }

            return new Of<IItem>(list);
        }

        public static IGroundedValue MakeResidue(ISequenceIterator iter)
        {
            if (iter is IGroundedIterator && ((IGroundedIterator)iter).IsActuallyGrounded())
            {
                return ((IGroundedIterator)iter).GetResidue();
            }

            SequenceExtent extent = From(iter);
            return extent.Reduce();
        }

        public static IGroundedValue MakeSequenceExtent<T>(IList<T> input) where T : IItem
        {
            int len = input.Count;
            if (len == 0)
            {
                return EmptySequence.GetInstance();
            }
            else if (len == 1)
            {
                return (IGroundedValue)input[0];
            }
            else
            {
                // No (IList<IItem>) cast: .NET generic lists are invariant, a List<NodeInfo> is not an
                // IList<IItem> — Of<T> handles the derived-item list directly.
                return new Of<T>(input);
            }
        }
        public abstract ISequenceIterator Iterate();
        public abstract IItem ItemAt(int arg0);
        public abstract IItem Head();
        public abstract IGroundedValue Subsequence(int arg0, int arg1);
        public abstract int GetLength();
        public abstract string GetStringValue();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual bool EffectiveBooleanValue() => ExpressionTool.EffectiveBooleanValue(Iterate()); // upstream GroundedValue default method
        public virtual IGroundedValue Reduce() => this; // upstream GroundedValue default method
        public virtual IGroundedValue Materialize() => this; // upstream GroundedValue default method
        public virtual string ToShortString() => OutSmart.DAXon.Transformation.Err.DepictSequence(this); // upstream GroundedValue default
        public virtual IEnumerable<IItem> AsIterable() { var list = new List<IItem>(); var it = Iterate(); for (IItem i = it.Next(); i != null; i = it.Next()) { list.Add(i); } return list; }
        public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        // A SequenceExtent is an in-memory grounded value - already repeatable (upstream default).
        public virtual ISequence MakeRepeatable() => this;
        internal sealed class Of<T> : SequenceExtent, IEnumerable<T> where T : IItem
        {
            private IList<T> items;

            public override UnicodeString UnicodeStringValue
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
                            foreach (T item in items)
                            {
                                builder.Append(separator);
                                separator = StringConstants.SINGLE_SPACE;
                                builder.Append(item.GetStringValue());
                            }

                            return builder.ToUnicodeString();
                    }
                }
            }
            public Of(IList<T> list)
            {
                this.items = list;
                if (list.Count >= MemoryLedger.BigCount)
                {
                    MemoryLedger.HoldOnce(list, -1, ListSizer);
                }
            }

            private static readonly Func<object, long> ListSizer = o => MemoryLedger.ListBytes((IList<T>)o);

            // Java stores Arrays.asList(items) — a view; a T[] already implements IList<T>
            public Of(T[] items) : this((IList<T>)items)
            {
            }

            public override ISequenceIterator Iterate()
            {
                return new ListIterator.Of<T>(items);
            }

            public override bool EffectiveBooleanValue()
            {
                int len = GetLength();
                if (len == 0)
                {
                    return false;
                }
                else
                {
                    IItem first = (IItem)items[0];
                    if (first is NodeInfo)
                    {
                        return true;
                    }
                    else if (len == 1 && first is AtomicValue)
                    {
                        return first.EffectiveBooleanValue();
                    }
                    else
                    {

                        // this will fail - reuse the error messages
                        return ExpressionTool.EffectiveBooleanValue(Iterate());
                    }
                }
            }

            public override IItem ItemAt(int n)
            {
                if (n >= 0 && n < items.Count)
                {
                    return (IItem)items[n];
                }
                else
                {
                    return null;
                }
            }

            public override IItem Head()
            {
                if (items.Count == 0)
                {
                    return null;
                }
                else
                {
                    return (IItem)items[0];
                }
            }

            public override int GetLength()
            {
                return items.Count;
            }

            public override string GetStringValue()
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
                        foreach (T item in items)
                        {
                            builder.Append(separator);
                            separator = " ";
                            builder.Append(item.GetStringValue());
                        }

                        return builder.ToString();
                }
            }

            public override IGroundedValue Subsequence(int start, int length)
            {
                if (start < 0)
                {
                    start = 0;
                }

                int size = GetLength();
                if (start > size)
                {
                    return EmptySequence.GetInstance();
                }

                int limit = ((long)start + (long)length > size) ? size : start + length;
                return new Of<T>(items.GetRange(start, (limit) - (start))).Reduce();
            }

            public override string ToString()
            {
                StringBuilder fsb = new StringBuilder(64);
                fsb.Append('(');
                for (int i = 0; i < GetLength(); i++)
                {
                    fsb.Append(i == 0 ? "" : ", ");
                    fsb.Append(items[i].ToString());
                }

                fsb.Append(')');
                return fsb.ToString();
            }

            public override IGroundedValue Reduce()
            {
                int len = GetLength();
                if (len == 0)
                {
                    return EmptySequence.GetInstance();
                }
                else if (len == 1)
                {
                    return this.ItemAt(0);
                }
                else
                {
                    return this;
                }
            }
            public IEnumerator<T> GetEnumerator() => items.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}