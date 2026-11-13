////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Sorting
{
    /// <summary>
    /// Class to do a sorted iteration
    /// </summary>
    internal class SortedIterator : ISequenceIterator, ILastPositionFinder, ILookaheadIterator
    {
        private readonly ISequenceIterator @base;
        protected ISortKeyEvaluator sortKeyEvaluator;
        protected IAtomicComparer[] comparators;
        protected ObjectToBeSorted[] values;
        protected int count = -1;
        protected int position;
        protected readonly IXPathContext context;
        private HostLanguage hostLanguage;
        protected virtual ISequenceIterator BaseIterator => @base;

        public virtual bool HasNext
        {
            get
            {
                if (position < 0)
                {
                    return false;
                }

                if (count < 0)
                {

                    // haven't started sorting yet
                    if (@base is ILookaheadIterator && ((ILookaheadIterator)@base).SupportsHasNext())
                    {
                        return ((ILookaheadIterator)@base).HasNext;
                    }
                    else
                    {
                        try
                        {
                            DoSort();
                            return count > 0;
                        }
                        catch (XPathException err)
                        {
                            throw new UncheckedXPathException(err);
                        }
                    }
                }
                else
                {
                    return position < count;
                }
            }
        }

        public SortedIterator(IXPathContext context, ISequenceIterator @base, ISortKeyEvaluator sortKeyEvaluator, IAtomicComparer[] comparators, bool createNewContext)
        {
            if (createNewContext)
            {
                this.context = context.NewMinorContext();
                this.context.TemporaryOutputState = StandardNames.XSL_SORT;
                this.@base = this.context.TrackFocus(@base);
            }
            else
            {
                this.context = context;
                this.@base = @base;
            }

            this.sortKeyEvaluator = sortKeyEvaluator;
            this.comparators = new IAtomicComparer[comparators.Length];
            for (int n = 0; n < comparators.Length; n++)
            {
                this.comparators[n] = comparators[n].ProvideContext(context);
            } // Avoid doing the sort until the user wants the first item. This is because
            // sometimes the user only wants to know whether the collection is empty.
        }

        public virtual void SetHostLanguage(HostLanguage language)
        {
            hostLanguage = language;
        }

        public virtual bool SupportsHasNext()
        {
            return true;
        }

        public virtual IItem Next()
        {
            if (position < 0)
            {
                return null;
            }

            if (count < 0)
            {
                try
                {
                    DoSort();
                }
                catch (XPathException e)
                {
                    throw new UncheckedXPathException(e);
                }
            }

            if (position < count)
            {
                return (IItem)values[position++].value;
            }
            else
            {
                position = -1;
                return null;
            }
        }

        public virtual bool SupportsGetLength()
        {
            return true;
        }

        public virtual int GetLength()
        {
            if (count < 0)
            {
                try
                {
                    DoSort();
                }
                catch (XPathException e)
                {
                    throw new UncheckedXPathException(e);
                }
            }

            return count;
        }

        // The items being sorted: the array, a record each with its key, and the items as a few of them tell.
        protected static readonly Func<object, long> Sizer = o =>
        {
            var s = (SortedIterator)o;
            ObjectToBeSorted[] v = s.values;
            int n = s.count;
            return v == null || n == 0 ? 0 : 24 + 8L * v.Length + (72 + Core.MemoryLedger.MemberBytes(v[n / 2]?.value)) * n;
        };

        private long weight;   // of medium strings, which few can make heavy (MemoryLedger.Weighs)
        private bool held;

        protected virtual void BuildArray()
        {
            int allocated = SequenceTool.SupportsGetLength(@base) ? SequenceTool.GetLength(@base) : 100;
            values = new ObjectToBeSorted[allocated];
            count = 0;

            bool[] asStrings = StringKeys();

            // initialise the array with data
            IItem item;
            while ((item = @base.Next()) != null)
            {
                Core.Controller.CheckActiveTimeout();   // a key can cost microseconds: 400 000 of them take seconds
                if (count == allocated)
                {
                    allocated *= 2;
                    ObjectToBeSorted[] nk2 = new ObjectToBeSorted[allocated];
                    Array.Copy(values, 0, nk2, 0, count);
                    values = nk2;
                }

                // Single-key: store the key inline in key0, skipping the 1-element array alloc.
                ObjectToBeSorted itbs;
                if (comparators.Length == 1)
                {
                    itbs = new ObjectToBeSorted();
                    itbs.value = item;
                    itbs.key0 = sortKeyEvaluator.EvaluateSortKey(0, context);
                    if (asStrings != null)
                    {
                        itbs.key0 = AsString(itbs.key0);
                    }
                }
                else
                {
                    itbs = new ObjectToBeSorted(comparators.Length);
                    itbs.value = item;

                    // TODO: delay evaluating the sort keys until we know they are needed. Often the 2nd and subsequent
                    // sort key values will never be used. The only problem is with sort keys that depend on position().
                    for (int n = 0; n < comparators.Length; n++)
                    {
                        AtomicValue key = sortKeyEvaluator.EvaluateSortKey(n, context);
                        NumericComparer numeric = NumericKey(n);
                        // A numeric key that is a string converts once, not on each comparison.
                        itbs.sortKeyValues[n] = numeric != null && key != null && !(key is NumericValue) ? new DoubleValue(numeric.ToDouble(key)) : key;
                        if (asStrings != null && asStrings[n])
                        {
                            itbs.sortKeyValues[n] = AsString(key);
                        }
                    }
                }

                values[count] = itbs;


                // make the sort stable by adding the record number
                itbs.originalPosition = count++;
                if (!held && (count == Core.MemoryLedger.BigCount || Core.MemoryLedger.Weighs(item, ref weight)))
                {
                    held = true;
                    Core.MemoryLedger.Hold(this, Sizer);   // summed as it grows
                }
            }


            // If there's lots of unused space, reclaim it
            if (allocated * 2 < count || (allocated - count) > 2000)
            {
                ObjectToBeSorted[] nk2 = new ObjectToBeSorted[count];
                Array.Copy(values, 0, nk2, 0, count);
                values = nk2;
            }
        }

        // The keys a collation compares as .NET strings (a language's, case-order): each is made a string once here, where
        // a key backed by bytes was copied into a new string on each of the n log n comparisons. Null when there is none.
        private bool[] StringKeys()
        {
            bool[] keys = null;
            for (int n = 0; n < comparators.Length; n++)
            {
                IAtomicComparer c = comparators[n] is DescendingComparer dc ? dc.BaseComparer : comparators[n];
                if (c is CollatingAtomicComparer cac && (cac.Collator is SimpleCollation || cac.Collator is CaseFirstCollator))
                {
                    (keys ??= new bool[comparators.Length])[n] = true;
                }
            }

            return keys;
        }

        private static AtomicValue AsString(AtomicValue key)
        {
            return key is StringValue sv ? sv.StringBacked() : key;
        }

        // The numeric comparer of key n (data-type="number"), descending or not; null for any other.
        private NumericComparer NumericKey(int n)
        {
            IAtomicComparer c = comparators[n] is DescendingComparer dc ? dc.BaseComparer : comparators[n];
            return c != null && (c.GetType() == typeof(NumericComparer) || c.GetType() == typeof(NumericComparer11)) ? (NumericComparer)c : null;
        }

        private void DoSort()
        {
            BuildArray();
            if (count < 2)
            {
                return;
            }

            if (TryOrdinalSort())
            {
                return;
            }


            // sort the array; SortComparer looks at the deadline, as DeadlineSort's comparisons do
            XPathException failure;
            try
            {
                Array.Sort(values, 0, count, new SortComparer(comparators));
                return;
            }
            catch (InvalidCastException e)
            {
                failure = ComparerFailure.Reported(e, hostLanguage == HostLanguage.XSLT);
            }
            catch (InvalidOperationException e)
            {
                failure = ComparerFailure.Reported(e, hostLanguage == HostLanguage.XSLT);
            }

            throw failure;
        }
        // ---- Codepoint-collation radix sort -------------------------------------------------------
        // A string key under the default (codepoint) collation makes every comparison UnicodeString.CompareTo:
        // codepoint-by-codepoint lexicographic order, shorter string first, originalPosition as the final tie-break
        // (SortComparer, above). For the surrogate-free string reps -- BMPString/BMPSlice (BMP chars) and Slice8/Twine8
        // (Latin1 bytes) -- a radix sort realises that exact total order with no key-to-key comparisons and no
        // per-comparison virtual dispatch, so the result is byte-identical to Array.Sort + SortComparer. An astral /
        // StringView / wide rep or a non-string key leaves TryOrdinalSort, and the unchanged Array.Sort path runs.
        private const int RadixMinCount = 64;
        private const int RadixInsertionCutoff = 16;

        // A string key's backing characters or Latin-1 bytes, with their span, for the radix sorts; false for a
        // representation they do not read flat.
        private static bool KeySpan(StringValue sv, int e, string[] sChars, byte[][] bBytes, int[] koff, int[] klen)
        {
            UnicodeString u = sv.UnicodeStringValue;
            if (BMPSlice.CharSpan(u, out string chars, out int start, out int end))
            {
                sChars[e] = chars;
                koff[e] = start;
                klen[e] = end - start;
            }
            else if (Slice8.Latin1Span(u, out byte[] bytes, out start, out end))
            {
                bBytes[e] = bytes;
                koff[e] = start;
                klen[e] = end - start;
            }
            else if (u is EmptyUnicodeString)
            {
                sChars[e] = "";
                klen[e] = 0;
            }
            else
            {
                return false;
            }

            return true;
        }

        // Layered string sort: pack the first 8 codepoints of every key into an order-preserving
        // big-endian ulong (0x00-padded; XML text never contains U+0000, so shorter-first order is
        // preserved), introsort the packed keys with their element ids — one sequential comparison
        // per compare instead of per-codepoint scattered loads across four arrays — then restore
        // original order inside equal-prefix runs (ties) and finish keys longer than 8 from depth 8.
        // Returns false (caller falls back to the counting radix) if any key holds a codepoint
        // > 0xFF among its first 8 — such keys are not byte-packable.
        private static bool TryPrefix8Sort(int[] idx, int lo, int hi, string[] sChars, byte[][] bBytes, int[] koff, int[] klen)
        {
            int n = hi - lo;
            // The 8 codepoints after the prefix all keys share: ids of one series differ only at the end.
            int d = CommonPrefix(idx, lo, hi, sChars, bBytes, koff, klen);
            ulong[] keys = new ulong[n];
            for (int i = lo; i < hi; i++)
            {
                int e = idx[i];
                int len = klen[e] - d;
                int m = len < 8 ? len : 8;
                ulong k = 0;
                string s = sChars[e];
                if (s != null)
                {
                    int off = koff[e] + d;
                    for (int t = 0; t < m; t++)
                    {
                        int c = s[off + t];
                        if (c > 0xff)
                        {
                            return false;
                        }

                        k = (k << 8) | (uint)c;
                    }
                }
                else
                {
                    byte[] b = bBytes[e];
                    int off = koff[e] + d;
                    for (int t = 0; t < m; t++)
                    {
                        k = (k << 8) | b[off + t];
                    }
                }

                keys[i - lo] = k << (8 * (8 - m));
            }

            int[] ids = new int[n];
            Array.Copy(idx, lo, ids, 0, n);
            Array.Sort(keys, ids);

            Array.Copy(ids, 0, idx, lo, n);

            // equal-prefix runs: restore original (stable) order, then sort beyond depth 8 if any
            // key in the run is longer than 8 codepoints
            int rs = 0;
            while (rs < n)
            {
                int re = rs + 1;
                while (re < n && keys[re] == keys[rs])
                {
                    re++;
                }

                if (re - rs > 1)
                {
                    Array.Sort(idx, lo + rs, re - rs);
                    bool longer = false;
                    for (int i = rs; i < re; i++)
                    {
                        if (klen[idx[lo + i]] > d + 8)
                        {
                            longer = true;
                            break;
                        }
                    }

                    if (longer)
                    {
                        RadixSort(idx, lo + rs, lo + re, sChars, bBytes, koff, klen, d + 8);
                    }
                }

                rs = re;
            }

            return true;
        }

        // The length of the prefix all keys of idx[lo, hi) share.
        private static int CommonPrefix(int[] idx, int lo, int hi, string[] sChars, byte[][] bBytes, int[] koff, int[] klen)
        {
            if (hi - lo < 2)
            {
                return 0;
            }

            int e0 = idx[lo];
            int l = klen[e0];
            for (int i = lo + 1; i < hi && l > 0; i++)
            {
                Core.Controller.CheckActiveTimeout();   // each key is compared over the whole prefix
                int e = idx[i];
                l = Math.Min(l, klen[e]);
                int t = 0;
                while (t < l && CharAt(e, t, sChars, bBytes, koff, klen) == CharAt(e0, t, sChars, bBytes, koff, klen))
                {
                    t++;
                }

                l = t;
            }

            return l;
        }

        // Any number of keys, each a number (data-type="number", xs:double, xs:decimal) or a string under the codepoint
        // collation: each key becomes once a ulong in the comparer's order (a decimal exactly, a string its rank), and an
        // index array sorts on those, key by key within the runs of equal earlier keys, ties by index (= original order). A
        // first key that is an ascending string is ordered by the radix sort itself. The comparator chain cost ~100 ns a
        // comparison; anything else returns false. One path for every shape: the one-string, string-and-number and
        // one-number sorts had a copy each.
        private bool TryOrdinalSort()
        {
            if (count < RadixMinCount)
            {
                return false;
            }

            bool radixFirst = comparators[0] is CodepointCollatingComparer;
            ulong[][] cols = new ulong[comparators.Length][];   // each key of each item, by item
            for (int c = radixFirst ? 1 : 0; c < cols.Length; c++)
            {
                cols[c] = OrdinalColumn(c);
                if (cols[c] == null)
                {
                    return false;
                }
            }

            int[] idx;
            ulong[] buf = null;   // one key by place, for the Array.Sort at hand
            if (radixFirst)
            {
                // stable: the order itself for one key; the ranks show the runs of equal strings to the next
                cols[0] = cols.Length > 1 ? new ulong[count] : null;
                idx = StringOrder(0, cols[0]);
                if (idx == null)
                {
                    return false;
                }
            }
            else
            {
                idx = new int[count];
                for (int e = 0; e < count; e++)
                {
                    idx[e] = e;
                }

                buf = cols.Length > 1 ? (ulong[])cols[0].Clone() : cols[0];
                Array.Sort(buf, idx);
                RestoreTies(idx, buf, 0, count);
            }

            if (cols.Length > 1)
            {
                SortRuns(idx, 0, count, cols, 1, buf ?? new ulong[count]);
            }

            ObjectToBeSorted[] sorted = new ObjectToBeSorted[count];
            for (int r = 0; r < count; r++)
            {
                sorted[r] = values[idx[r]];
            }

            values = sorted;
            return true;
        }

        // Each run of items equal on key c - 1 in idx[lo, hi), in original order, sorted on key c and the keys after it,
        // ties in original order. A run of the previous key is one whatever order its items take, so the runs are read
        // through idx while buf, by place, holds key c for one Array.Sort at a time.
        private static void SortRuns(int[] idx, int lo, int hi, ulong[][] cols, int c, ulong[] buf)
        {
            ulong[] prev = cols[c - 1];
            ulong[] col = cols[c];
            int runStart = lo;
            for (int i = lo + 1; i <= hi; i++)
            {
                if (i == hi || prev[idx[i]] != prev[idx[i - 1]])
                {
                    if (i - runStart > 1)
                    {
                        Core.Controller.CheckActiveTimeout();
                        for (int r = runStart; r < i; r++)
                        {
                            buf[r] = col[idx[r]];
                        }

                        Array.Sort(buf, idx, runStart, i - runStart);
                        RestoreTies(idx, buf, runStart, i);
                        if (c + 1 < cols.Length)
                        {
                            SortRuns(idx, runStart, i, cols, c + 1, buf);
                        }
                    }

                    runStart = i;
                }
            }
        }

        // Array.Sort is not stable: the items of each run of equal keys in idx[lo, hi) go back to original order.
        private static void RestoreTies(int[] idx, ulong[] keys, int lo, int hi)
        {
            int tieStart = lo;
            for (int i = lo + 1; i <= hi; i++)
            {
                if (i == hi || keys[i] != keys[i - 1])
                {
                    if (i - tieStart > 1)
                    {
                        Array.Sort(idx, tieStart, i - tieStart);
                    }

                    tieStart = i;
                }
            }
        }

        // Key c of each item as a ulong in the order of comparator c, descending folded in; null for another comparer or
        // a key it does not map.
        private ulong[] OrdinalColumn(int c)
        {
            IAtomicComparer comp = comparators[c];
            bool descending = false;
            if (comp is DescendingComparer dc)
            {
                descending = true;
                comp = dc.BaseComparer;
            }

            System.Type type = comp?.GetType();
            ulong[] col;
            if (type == typeof(CodepointCollatingComparer))
            {
                col = StringRanks(c);
            }
            else if (type == typeof(DecimalSortComparer))
            {
                col = DecimalOrdinals(c);
            }
            else if (type == typeof(DoubleSortComparer))
            {
                col = DoubleOrdinals(c);
            }
            else if (type == typeof(NumericComparer) || type == typeof(NumericComparer11))
            {
                var numeric = (NumericComparer)comp;
                col = new ulong[count];
                for (int e = 0; e < count; e++)
                {
                    col[e] = MapDoubleToOrdinal(numeric.ToDouble(KeyOf(e, c)), false);
                }
            }
            else
            {
                return null;
            }

            if (col != null && descending)
            {
                for (int e = 0; e < count; e++)
                {
                    col[e] = ~col[e];
                }
            }

            return col;
        }

        private AtomicValue KeyOf(int e, int c)
        {
            ObjectToBeSorted obj = values[e];
            return obj.sortKeyValues == null ? obj.key0 : obj.sortKeyValues[c];
        }

        // DoubleSortComparer: the empty key first, then NaN, then the numbers (the zeros equal). xs:double and xs:float only.
        private ulong[] DoubleOrdinals(int c)
        {
            var col = new ulong[count];
            for (int e = 0; e < count; e++)
            {
                AtomicValue k = KeyOf(e, c);
                if (k == null)
                {
                    col[e] = 0;
                }
                else if (k is DoubleValue || k is FloatValue)
                {
                    double d = ((NumericValue)k).GetDoubleValue();
                    col[e] = double.IsNaN(d) ? 1 : MapDoubleToOrdinal(d, false);
                }
                else
                {
                    return null;
                }
            }

            return col;
        }

        // DecimalSortComparer: the empty key first, then the exact value. Each decimal is scaled to the column's largest
        // scale as a long under 2^62; a larger value, a negative scale or another type returns null.
        private ulong[] DecimalOrdinals(int c)
        {
            var unscaled = new long[count];
            var scales = new int[count];
            var col = new ulong[count];
            int maxScale = 0;
            for (int e = 0; e < count; e++)
            {
                AtomicValue k = KeyOf(e, c);
                if (k == null)
                {
                    scales[e] = -1;
                    continue;
                }

                long u;
                int s;
                if (k is Int64Value i)
                {
                    u = i.LongValue();
                    s = 0;
                }
                else if (!(k is BigDecimalValue bd) || !bd.GetDecimalValue().TryGetCompactParts(out u, out s) || s < 0 || s > 18)
                {
                    return null;
                }

                unscaled[e] = u;
                scales[e] = s;
                maxScale = Math.Max(maxScale, s);
            }

            const long Limit = (1L << 62) - 1;
            for (int e = 0; e < count; e++)
            {
                if (scales[e] < 0)
                {
                    col[e] = 0;
                    continue;
                }

                long p = Powers10[maxScale - scales[e]];
                long u = unscaled[e];
                if (u > Limit / p || u < -Limit / p)
                {
                    return null;
                }

                col[e] = (ulong)(u * p + (1L << 62)) + 1;
            }

            return col;
        }

        private static readonly long[] Powers10 =
        {
            1L, 10L, 100L, 1000L, 10000L, 100000L, 1000000L, 10000000L, 100000000L, 1000000000L, 10000000000L,
            100000000000L, 1000000000000L, 10000000000000L, 100000000000000L, 1000000000000000L, 10000000000000000L,
            100000000000000000L, 1000000000000000000L
        };

        // CodepointCollatingComparer: each key's rank among the distinct keys, the empty key first, from the radix sort.
        private ulong[] StringRanks(int c)
        {
            var col = new ulong[count];
            return StringOrder(c, col) == null ? null : col;
        }

        // The items by string key c under the codepoint collation (the radix sort), the empty key first and ties in
        // original order; ranks, unless null, gets each item's rank among the distinct keys. Null for a key the radix
        // sort does not read flat: another type, or a representation that may hold surrogates.
        private int[] StringOrder(int c, ulong[] ranks)
        {
            string[] sChars = new string[count];
            byte[][] bBytes = new byte[count][];
            int[] koff = new int[count];
            int[] klen = new int[count];
            int nullCount = 0;
            for (int e = 0; e < count; e++)
            {
                AtomicValue k = KeyOf(e, c);
                if (k == null)
                {
                    klen[e] = -1;
                    nullCount++;
                }
                else if (!(k is StringValue sv) || !KeySpan(sv, e, sChars, bBytes, koff, klen))
                {
                    return null;
                }
            }

            int[] idx = new int[count];
            int p = 0;
            for (int e = 0; e < count && p < nullCount; e++)
            {
                if (klen[e] == -1)
                {
                    idx[p++] = e;
                }
            }

            int nonNullStart = p;
            for (int e = 0; e < count; e++)
            {
                if (klen[e] != -1)
                {
                    idx[p++] = e;
                }
            }

            if (!TryPrefix8Sort(idx, nonNullStart, count, sChars, bBytes, koff, klen))
            {
                RadixSort(idx, nonNullStart, count, sChars, bBytes, koff, klen, 0);
            }

            if (ranks != null)
            {
                ulong rank = 0;
                for (int i = 0; i < count; i++)
                {
                    if (i > 0 && !FirstKeyEqual(idx[i - 1], idx[i], sChars, bBytes, koff, klen))
                    {
                        rank++;
                    }

                    ranks[idx[i]] = rank;
                }
            }

            return idx;
        }

        private static ulong MapDoubleToOrdinal(double d, bool descending)
        {
            ulong u;
            if (double.IsNaN(d))
            {
                u = 0UL;   // strictly below every real number's image (all >= 0x000FFFFFFFFFFFFF)
            }
            else
            {
                if (d == 0.0)
                {
                    d = 0.0;   // -0 == +0 for the comparer: normalise so both map identically
                }

                u = (ulong)BitConverter.DoubleToInt64Bits(d);
                u = (u >> 63) != 0 ? ~u : u | 0x8000000000000000UL;
            }

            return descending ? ~u : u;
        }

        private static bool FirstKeyEqual(int e1, int e2, string[] sChars, byte[][] bBytes, int[] koff, int[] klen)
        {
            int len = klen[e1];
            if (len != klen[e2])
            {
                return false;
            }

            if (len <= 0)
            {
                return true;   // both null (-1) or both empty
            }

            byte[] b1 = bBytes[e1], b2 = bBytes[e2];
            int o1 = koff[e1], o2 = koff[e2];
            if (b1 != null && b2 != null)
            {
                for (int i = 0; i < len; i++)
                {
                    if (b1[o1 + i] != b2[o2 + i])
                    {
                        return false;
                    }
                }

                return true;
            }

            string s1 = sChars[e1], s2 = sChars[e2];
            for (int i = 0; i < len; i++)
            {
                int c1 = b1 != null ? b1[o1 + i] & 0xff : s1[o1 + i];
                int c2 = b2 != null ? b2[o2 + i] & 0xff : s2[o2 + i];
                if (c1 != c2)
                {
                    return false;
                }
            }

            return true;
        }

        // 3-way radix quicksort of idx[start,end) at codepoint depth 0. The ==pivot run advances
        // depth by iteration (a shared prefix costs no stack), and the two outer partitions go on an
        // explicit heap stack -- bounded by the input, never the call stack -- so no arrangement of
        // keys can overflow. Fully-equal keys are ordered by index (== originalPosition).
        private static void RadixSort(int[] idx, int start, int end, string[] sChars, byte[][] bBytes, int[] koff, int[] klen, int d0 = 0)
        {
            if (end - start < 2)
            {
                return;
            }

            // MSD counting radix. MKQS partitioning re-passes low-cardinality runs once per pivot
            // at every depth (e.g. 184 distinct 9-codepoint keys over 312k items -> millions of
            // scattered swap-loop visits); a stable 258-bucket counting pass visits each element
            // exactly twice per depth and never swaps. All scratch is reference-free. Stability is
            // free (idx starts in original order, stable permutes preserve it), so identical-key
            // runs need no leaf sort. A run holding a codepoint > 0xFF at the current depth cannot
            // bucket by byte and falls back to the 3-way MKQS range (same order, incl. tie-breaks).
            int n = end - start;
            int[] scratch = new int[n];
            ushort[] charBuf = new ushort[n];   // codepoint+1 at depth d per run element (0 = key exhausted)
            int[] count = new int[258];
            int[] bstart = new int[258];

            int[] stackLo = new int[64];
            int[] stackHi = new int[64];
            int[] stackD = new int[64];
            stackLo[0] = start;
            stackHi[0] = end;
            stackD[0] = d0;
            int sp = 1;

            while (sp > 0)
            {
                sp--;
                int lo = stackLo[sp];
                int hi = stackHi[sp];
                int d = stackD[sp];

                for (; ; )
                {
                    // A pass per depth over the run: keys sharing a long prefix (substrings of one big string) cost n x
                    // its length, so the clock is read per pass.
                    Core.Controller.CheckActiveTimeout();
                    if (hi - lo <= RadixInsertionCutoff)
                    {
                        if (hi - lo > 1)
                        {
                            RadixInsertion(idx, lo, hi, d, sChars, bBytes, koff, klen);
                        }

                        break;
                    }

                    Array.Clear(count, 0, 258);
                    bool wide = false;
                    for (int i = lo; i < hi; i++)
                    {
                        int c = CharAt(idx[i], d, sChars, bBytes, koff, klen);
                        if (c > 0xff)
                        {
                            wide = true;
                            break;
                        }

                        int b = c + 1;
                        charBuf[i - start] = (ushort)b;
                        count[b]++;
                    }

                    if (wide)
                    {
                        MkqsRange(idx, lo, hi, d, sChars, bBytes, koff, klen);
                        break;
                    }

                    if (count[0] == hi - lo)
                    {
                        break;   // identical keys: stable order == original order already
                    }

                    int occupied = 0;
                    for (int b = 0; b < 258 && occupied < 2; b++)
                    {
                        if (count[b] != 0)
                        {
                            occupied++;
                        }
                    }

                    if (occupied == 1)
                    {
                        // shared codepoint at this depth: descend without moving anything
                        d++;
                        continue;
                    }

                    int pos = lo;
                    for (int b = 0; b < 258; b++)
                    {
                        int cnt = count[b];
                        count[b] = pos;
                        pos += cnt;
                    }

                    Array.Copy(count, bstart, 258);
                    for (int i = lo; i < hi; i++)
                    {
                        scratch[count[charBuf[i - start]]++ - start] = idx[i];
                    }

                    Array.Copy(scratch, lo - start, idx, lo, hi - lo);

                    // bucket 0 (exhausted keys) is complete and stable; recurse into the rest
                    for (int b = 1; b < 258; b++)
                    {
                        int bLo = bstart[b];
                        int bHi = count[b];
                        if (bHi - bLo > 1)
                        {
                            sp = RadixPush(ref stackLo, ref stackHi, ref stackD, sp, bLo, bHi, d + 1);
                        }
                    }

                    break;
                }
            }
        }

        // 3-way MKQS over [start,end) from depth d0 — fallback for runs containing codepoints
        // > 0xFF (counting radix buckets by byte). Order identical to the counting path.
        private static void MkqsRange(int[] idx, int start, int end, int d0, string[] sChars, byte[][] bBytes, int[] koff, int[] klen)
        {
            if (end - start < 2)
            {
                return;
            }

            int[] stackLo = new int[64];
            int[] stackHi = new int[64];
            int[] stackD = new int[64];
            stackLo[0] = start;
            stackHi[0] = end;
            stackD[0] = d0;
            int sp = 1;

            while (sp > 0)
            {
                sp--;
                int lo = stackLo[sp];
                int hi = stackHi[sp];
                int d = stackD[sp];

                for (; ; )
                {
                    Core.Controller.CheckActiveTimeout();   // a pass per depth, as RadixSort
                    int n = hi - lo;
                    if (n <= RadixInsertionCutoff)
                    {
                        if (n > 1)
                        {
                            RadixInsertion(idx, lo, hi, d, sChars, bBytes, koff, klen);
                        }

                        break;
                    }

                    int v = MedianPivot(idx, lo, hi, d, sChars, bBytes, koff, klen);
                    int lt = lo;
                    int gt = hi;
                    int i = lo;
                    while (i < gt)
                    {
                        int e = idx[i];
                        int c;
                        if (d >= klen[e])
                        {
                            c = -1;
                        }
                        else
                        {
                            string s = sChars[e];
                            c = s != null ? s[koff[e] + d] : (bBytes[e][koff[e] + d] & 0xff);
                        }

                        if (c < v)
                        {
                            int t = idx[lt]; idx[lt] = idx[i]; idx[i] = t;
                            lt++;
                            i++;
                        }
                        else if (c > v)
                        {
                            gt--;
                            int t = idx[i]; idx[i] = idx[gt]; idx[gt] = t;
                        }
                        else
                        {
                            i++;
                        }
                    }

                    // [lo,lt) < v ; [lt,gt) == v ; [gt,hi) > v
                    if (lt - lo > 1)
                    {
                        sp = RadixPush(ref stackLo, ref stackHi, ref stackD, sp, lo, lt, d);
                    }

                    if (hi - gt > 1)
                    {
                        sp = RadixPush(ref stackLo, ref stackHi, ref stackD, sp, gt, hi, d);
                    }

                    if (v < 0)
                    {
                        // every ==v key ends at depth d -> identical strings -> order by index
                        if (gt - lt > 1)
                        {
                            Array.Sort(idx, lt, gt - lt);
                        }

                        break;
                    }

                    // tail-iterate on the ==v run at the next depth (no stack growth)
                    lo = lt;
                    hi = gt;
                    d++;
                }
            }
        }

        private static int RadixPush(ref int[] stackLo, ref int[] stackHi, ref int[] stackD, int sp, int lo, int hi, int d)
        {
            if (sp == stackLo.Length)
            {
                Array.Resize(ref stackLo, sp * 2);
                Array.Resize(ref stackHi, sp * 2);
                Array.Resize(ref stackD, sp * 2);
            }

            stackLo[sp] = lo;
            stackHi[sp] = hi;
            stackD[sp] = d;
            return sp + 1;
        }

        // Codepoint at depth d of key e, or -1 past the end (surrogate-free rep: a BMP char / a
        // Latin1 byte IS its codepoint).
        private static int CharAt(int e, int d, string[] sChars, byte[][] bBytes, int[] koff, int[] klen)
        {
            if (d >= klen[e])
            {
                return -1;
            }

            string s = sChars[e];
            if (s != null)
            {
                return s[koff[e] + d];
            }

            return bBytes[e][koff[e] + d] & 0xff;
        }

        private static int MedianPivot(int[] idx, int lo, int hi, int d, string[] sChars, byte[][] bBytes, int[] koff, int[] klen)
        {
            int a = CharAt(idx[lo], d, sChars, bBytes, koff, klen);
            int b = CharAt(idx[lo + (hi - lo) / 2], d, sChars, bBytes, koff, klen);
            int c = CharAt(idx[hi - 1], d, sChars, bBytes, koff, klen);
            if (a < b)
            {
                return b < c ? b : (a < c ? c : a);
            }

            return a < c ? a : (b < c ? c : b);
        }

        private static void RadixInsertion(int[] idx, int lo, int hi, int d, string[] sChars, byte[][] bBytes, int[] koff, int[] klen)
        {
            for (int i = lo + 1; i < hi; i++)
            {
                int x = idx[i];
                int j = i - 1;
                while (j >= lo && CompareFrom(idx[j], x, d, sChars, bBytes, koff, klen) > 0)
                {
                    idx[j + 1] = idx[j];
                    j--;
                }

                idx[j + 1] = x;
            }
        }

        // Full order of keys ea, eb from codepoint depth d (the range shares prefix [0,d)):
        // lexicographic by codepoint, shorter first, index (== originalPosition) as the final
        // tie-break -- identical to SortComparer.
        private static int CompareFrom(int ea, int eb, int d, string[] sChars, byte[][] bBytes, int[] koff, int[] klen)
        {
            int la = klen[ea];
            int lb = klen[eb];
            int m = la < lb ? la : lb;
            string sa = sChars[ea];
            byte[] ba = bBytes[ea];
            int oa = koff[ea];
            string sb = sChars[eb];
            byte[] bb = bBytes[eb];
            int ob = koff[eb];
            for (int t = d; t < m; t++)
            {
                int ca = sa != null ? sa[oa + t] : ba[oa + t] & 0xff;
                int cb = sb != null ? sb[ob + t] : bb[ob + t] & 0xff;
                int diff = ca - cb;
                if (diff != 0)
                {
                    return diff;
                }
            }

            if (la != lb)
            {
                return la - lb;
            }

            return ea - eb;
        }

        public virtual void Dispose() { }

        // Items by the ordinal columns after the first, then by index (== originalPosition).
        private sealed class SortComparer : IComparer<ObjectToBeSorted>
        {
            private readonly IAtomicComparer[] comparators;
            private int comparisons;
            public SortComparer(IAtomicComparer[] comparators)
            {
                this.comparators = comparators;
            }

            public int Compare(ObjectToBeSorted a, ObjectToBeSorted b)
            {
                // Nothing else stops an Array.Sort: 1.5M keys under lang=de make 30M comparisons, 15 s on .NET Framework.
                if ((++comparisons & 1023) == 0)
                {
                    Core.Controller.CheckActiveTimeoutNow();
                }

                try
                {
                    if (comparators.Length == 1)
                    {
                        // Single-key: the key is inline in key0 when sortKeyValues is null (SortedIterator
                        // fast path; key0 itself may legitimately be null for an empty sort key), else in
                        // the array (SortedGroupIterator / multi-key builders).
                        AtomicValue ak = a.sortKeyValues == null ? a.key0 : a.sortKeyValues[0];
                        AtomicValue bk = b.sortKeyValues == null ? b.key0 : b.sortKeyValues[0];
                        int comp = comparators[0].CompareAtomicValues(ak, bk);
                        if (comp != 0)
                        {
                            return comp;
                        }
                    }
                    else
                    {
                        IAtomicComparer[] comps = comparators;
                        AtomicValue[] ak = a.sortKeyValues;
                        AtomicValue[] bk = b.sortKeyValues;
                        for (int i = 0; i < comps.Length; i++)
                        {
                            int comp = comps[i].CompareAtomicValues(ak[i], bk[i]);
                            if (comp != 0)
                            {

                                // we have found a difference, so we can return
                                return comp;
                            }
                        }
                    }
                }
                catch (NoDynamicContextException e)
                {
                    throw new InvalidOperationException("Sorting without dynamic context: " + e.Message);
                }


                // all sort keys equal: the original order decides, as the sort routines are not all stable
                return a.originalPosition - b.originalPosition;
            }
        }
    }
}
