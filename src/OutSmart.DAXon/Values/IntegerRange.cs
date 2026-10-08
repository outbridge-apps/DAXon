////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Values
{
    internal sealed class IntegerRange : IAtomicSequence
    {
        public long start;
        public long step;
        public long end; // the adjusted end, so it is actually the last number returned

        public long Start => start;

        public long End => end;

        public UnicodeString CanonicalLexicalRepresentation => UnicodeStringValue;

        public UnicodeString UnicodeStringValue
        {
            get
            {
                try
                {
                    return SequenceTool.GetStringValue(this);
                }
                catch (XPathException err)
                {
                    throw new InvalidOperationException(err.Message, err);
                }
            }
        }
        public IntegerRange(long start, long step, long end)
        {
            if (step == 0)
            {
                throw new ArgumentException("step = 0 in IntegerRange");
            }

            if (end != start && (end > start != step > 0))
            {
                throw new ArgumentException("end before start in IntegerRange");
            }

            if (CountExceedsLimit(start, step, end))
            {
                throw new XPathException("Maximum length of sequence in Saxon is " + int.MaxValue, "XPDY0130");
            }

            this.start = start;
            this.step = step;
            this.end = start + (end - start) / step * step;
        }

        public long GetStep()
        {
            return step;
        }

        // True if the range start..end by step holds more than int.MaxValue items (the Saxon
        // sequence-length limit, XPDY0130). The item count is |end-start|/|step| + 1, and both
        // magnitudes can reach 2^63, which no long holds — so they are computed unsigned, where
        // the subtraction of the larger from the smaller is exact for every input.
        // The old guards computed Math.Abs(end-start) or a signed (end-start)/step and compared
        // with '>': the first threw a raw OverflowException from Math.Abs(long.MinValue) instead
        // of a clean XPDY0130, the second wrapped negative and let a 2^63-item range through.
        internal static bool CountExceedsLimit(long start, long step, long end)
        {
            ulong span = end >= start ? (ulong)end - (ulong)start : (ulong)start - (ulong)end;
            ulong absStep = step < 0 ? (ulong)(-(step + 1)) + 1 : (ulong)step;
            // count = span/absStep + 1 > int.MaxValue  <=>  span/absStep >= int.MaxValue
            return span / absStep >= int.MaxValue;
        }

        // Closed-form total of the arithmetic series start, start+step, ..., last (inclusive),
        // all in checked long arithmetic: count*start + step*(0+1+...+(count-1)). False on
        // overflow — callers then iterate, which reproduces the generic behaviour (including
        // BigInteger promotion). Shared by fn:sum's fast paths and the range iterators.
        internal static bool TrySum(long start, long step, long last, out long total)
        {
            try
            {
                checked
                {
                    long count = (last - start) / step + 1;
                    long tri = (count % 2 == 0) ? (count / 2) * (count - 1) : count * ((count - 1) / 2);
                    total = count * start + step * tri;
                    return true;
                }
            }
            catch (OverflowException)
            {
                total = 0;
                return false;
            }
        }

        public IAtomicIterator Iterate()
        {

            // Written this way for C# conversion
            if (step > 0)
            {
                return new AscendingRangeIterator(start, step, end);
            }
            else
            {
                return new DescendingRangeIterator(start, -step, end);
            }
        }

        public IntegerValue ItemAt(int n)
        {
            if (n < 0 || n >= GetLength())
            {
                return null;
            }

            return Int64Value.MakeIntegerValue(start + (n * step));
        }

        public IGroundedValue Subsequence(int start, int length)
        {
            if (length <= 0)
            {
                return EmptySequence.GetInstance();
            }

            // Counted in items, not values: Saxon 12.9's value arithmetic held only for step 1, so
            // tail(reverse(1 to 10)) was empty
            long from = Math.Max(start, 0);
            long count = Math.Min(length, GetLength() - from);
            if (count <= 0)
            {
                return EmptySequence.GetInstance();
            }

            long newStart = this.start + from * step;
            return new IntegerRange(newStart, step, newStart + (count - 1) * step);
        }

        public int GetLength()
        {
            return (int)((end - start) / step) + 1;
        }

        public IntegerValue Head()
        {
            return new Int64Value(start);
        }

        public string GetStringValue()
        {
            try
            {
                return SequenceTool.Stringify(this);
            }
            catch (XPathException err)
            {
                throw new InvalidOperationException(err.Message, err);
            }
        }

        public bool EffectiveBooleanValue()
        {
            return ExpressionTool.EffectiveBooleanValue(Iterate());
        }

        public IGroundedValue Reduce()
        {
            if (start == end)
            {
                return ItemAt(0);
            }
            else
            {
                return this;
            }
        }

        public override string ToString()
        {
            return "(" + start + (step == 1 ? "" : (" by " + step)) + " to " + end + ")";
        }

        AtomicValue IAtomicSequence.Head() => Head();
        AtomicValue IAtomicSequence.ItemAt(int arg0) => ItemAt(arg0);
        ISequenceIterator IGroundedValue.Iterate() => Iterate();
        IItem IGroundedValue.ItemAt(int arg0) => ItemAt(arg0);
        IItem IGroundedValue.Head() => Head();
        IItem ISequence.Head() => Head();
        ISequenceIterator ISequence.Iterate() => Iterate();
        public IEnumerator<AtomicValue> GetEnumerator()
        {
            // Count-bounded: a value-bounded loop (v <= end) would spin forever when end
            // sits at the long boundary and v += step wraps.
            long v = start;
            for (int i = GetLength(); i > 0; i--)
            {
                yield return new Int64Value(v);
                if (i > 1)
                {
                    v += step;
                }
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        // IntegerRange is already an in-memory grounded value, so materialize/makeRepeatable return itself,
        // and an integer range contains no nodes. (Materialize was a throwing stub, so `1 to 5` blew up wherever
        // it had to be grounded, e.g. map:entry("k", 1 to 5).)
        public IGroundedValue Materialize() => this;
        public string ToShortString() => "(" + Start + " to " + End + ")";
        // Streaming, not a materialized list: a range can be huge (bounded only by the
        // int.MaxValue sequence cap), and eager buffering turned every whole-range consumer
        // (Literal type checks, foreach bridges) into an O(N)-memory walk — sum(1 to 1e9)
        // exhausted memory at compile time through Literal.IsInstance.
        public IEnumerable<IItem> AsIterable()
        {
            var it = Iterate();
            for (IItem i = it.Next(); i != null; i = it.Next())
            {
                yield return i;
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
