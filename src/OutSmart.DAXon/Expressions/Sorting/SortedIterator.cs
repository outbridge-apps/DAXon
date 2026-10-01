////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
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
        protected int position = 0;
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

        protected virtual void BuildArray()
        {
            int allocated = SequenceTool.SupportsGetLength(@base) ? SequenceTool.GetLength(@base) : 100;
            values = new ObjectToBeSorted[allocated];
            count = 0;

            // initialise the array with data
            IItem item;
            while ((item = @base.Next()) != null)
            {
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
                }
                else
                {
                    itbs = new ObjectToBeSorted(comparators.Length);
                    itbs.value = item;

                    // TODO: delay evaluating the sort keys until we know they are needed. Often the 2nd and subsequent
                    // sort key values will never be used. The only problem is with sort keys that depend on position().
                    for (int n = 0; n < comparators.Length; n++)
                    {
                        itbs.sortKeyValues[n] = sortKeyEvaluator.EvaluateSortKey(n, context);
                    }
                }

                values[count] = itbs;


                // make the sort stable by adding the record number
                itbs.originalPosition = count++;
            }


            // If there's lots of unused space, reclaim it
            if (allocated * 2 < count || (allocated - count) > 2000)
            {
                ObjectToBeSorted[] nk2 = new ObjectToBeSorted[count];
                Array.Copy(values, 0, nk2, 0, count);
                values = nk2;
            }
        }

        private void DoSort()
        {
            BuildArray();
            if (count < 2)
            {
                return;
            }

            if (TryCodepointRadixSort())
            {
                return;
            }

            if (TryTwoKeyRadixSort())
            {
                return;
            }

            if (TrySingleDoubleKeySort())
            {
                return;
            }


            // sort the array
            try
            {
                Array.Sort(values, 0, count, new SortComparer(comparators));
            }
            catch (InvalidCastException e)
            {
                throw new XPathException("Non-comparable types found while sorting: " + e.Message).WithErrorCode(hostLanguage == HostLanguage.XSLT ? "XTDE1030" : "XPTY0004");
            }
            catch (InvalidOperationException e)
            {
                // .NET Array.Sort wraps comparator exceptions in InvalidOperationException ("Failed to
                // compare two elements"); Java's Arrays.sort lets ComparisonException (a ClassCastException
                // subclass) propagate straight to the catch above. Unwrap to the same XTDE1030/XPTY0004.
                Exception inner = e.InnerException ?? e;
                throw new XPathException("Non-comparable types found while sorting: " + inner.Message).WithErrorCode(hostLanguage == HostLanguage.XSLT ? "XTDE1030" : "XPTY0004");
            }
        }
        // ---- Codepoint-collation radix sort (fast path) ------------------------------------------
        // A single xsl:sort / fn:sort key under the default (codepoint) collation makes every
        // comparison UnicodeString.CompareTo: codepoint-by-codepoint lexicographic order, shorter
        // string first, originalPosition as the final tie-break (SortComparer, above). For the
        // surrogate-free string reps -- BMPString/BMPSlice (BMP chars) and Slice8/Twine8 (Latin1
        // bytes) -- a 3-way radix (multikey) quicksort realises that exact total order with no
        // key-to-key comparisons and no per-comparison virtual dispatch, so the result is
        // byte-identical to Array.Sort + SortComparer. Any unmet condition (multi-key, descending,
        // non-codepoint collation, an astral / StringView / wide rep, a non-string key) returns
        // false and leaves the unchanged Array.Sort path to run.
        private const int RadixMinCount = 64;
        private const int RadixInsertionCutoff = 16;

        private bool TryCodepointRadixSort()
        {
            if (comparators.Length != 1 || !(comparators[0] is CodepointCollatingComparer) || count < RadixMinCount)
            {
                return false;
            }

            // Flat per-key arrays: exactly one of sChars[e] / bBytes[e] is non-null (BMP string vs
            // Latin1 bytes); klen[e] == -1 marks a null key (ranks below all, in original order).
            string[] sChars = new string[count];
            byte[][] bBytes = new byte[count][];
            int[] koff = new int[count];
            int[] klen = new int[count];
            int nullCount = 0;
            for (int e = 0; e < count; e++)
            {
                ObjectToBeSorted obj = values[e];
                AtomicValue k = obj.sortKeyValues == null ? obj.key0 : obj.sortKeyValues[0];
                if (k == null)
                {
                    klen[e] = -1;
                    nullCount++;
                    continue;
                }

                if (!(k is StringValue sv))
                {
                    return false;   // non-string key: let Array.Sort raise the type error
                }

                UnicodeString u = sv.UnicodeStringValue;
                if (u is BMPSlice sl)
                {
                    sChars[e] = sl.Backing;
                    koff[e] = sl.Start;
                    klen[e] = sl.End - sl.Start;
                }
                else if (u is BMPString bstr)
                {
                    string s = bstr.ToString();   // backing string, no copy
                    sChars[e] = s;
                    klen[e] = s.Length;
                }
                else if (u is Slice8 s8)
                {
                    bBytes[e] = s8.ByteArray;
                    koff[e] = s8.Start;
                    klen[e] = s8.End - s8.Start;
                }
                else if (u is Twine8 t8)
                {
                    bBytes[e] = t8.ByteArray;
                    klen[e] = t8.ByteArray.Length;
                }
                else if (u is EmptyUnicodeString)
                {
                    sChars[e] = "";
                    klen[e] = 0;
                }
                else
                {
                    return false;   // StringView (may hold surrogates) / Twine16 / Twine24 / composite
                }
            }

            // Index array: null keys first in original order, then the keys to radix-sort.
            int[] idx = new int[count];
            int p = 0;
            if (nullCount > 0)
            {
                for (int e = 0; e < count; e++)
                {
                    if (klen[e] == -1)
                    {
                        idx[p++] = e;
                    }
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

            ObjectToBeSorted[] sorted = new ObjectToBeSorted[count];
            for (int r = 0; r < count; r++)
            {
                sorted[r] = values[idx[r]];
            }

            values = sorted;
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
            ulong[] keys = new ulong[n];
            for (int i = lo; i < hi; i++)
            {
                int e = idx[i];
                int len = klen[e];
                int m = len < 8 ? len : 8;
                ulong k = 0;
                string s = sChars[e];
                if (s != null)
                {
                    int off = koff[e];
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
                    int off = koff[e];
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
                        if (klen[idx[lo + i]] > 8)
                        {
                            longer = true;
                            break;
                        }
                    }

                    if (longer)
                    {
                        RadixSort(idx, lo + rs, lo + re, sChars, bBytes, koff, klen, 8);
                    }
                }

                rs = re;
            }

            return true;
        }

        // ---- Two-key fast sort: codepoint string key + numeric double key ------------------------
        // xsl:sort pairs of the form (string key, codepoint collation) + (number() key, NumericComparer,
        // optionally descending) realise the SortComparer order lexicographically: radix-sort by key 1
        // (stable by index), then within each run of equal first keys order by the second key using
        // NumericComparer's rules (NaN first, +-0 equal, ascending; descending = exact negation), with
        // originalPosition as the final tie-break. Each double maps to an order-preserving ulong
        // (NaN -> 0, -0 normalised to +0, IEEE sign-flip trick; descending = bitwise complement), so
        // the per-run sorts are primitive-key Array.Sort calls with no comparator dispatch. The result
        // is byte-identical to Array.Sort + SortComparer. Any unmet condition (other comparer kinds,
        // a non-double/non-null second key, an astral/StringView first-key rep) returns false and
        // leaves the general Array.Sort path to run.
        private bool TryTwoKeyRadixSort()
        {
            if (comparators.Length != 2 || !(comparators[0] is CodepointCollatingComparer) || count < RadixMinCount)
            {
                return false;
            }

            IAtomicComparer second = comparators[1];
            bool descending2 = false;
            if (second is DescendingComparer dc)
            {
                descending2 = true;
                second = dc.BaseComparer;
            }

            // NumericComparer11 (data-type="number") differs from NumericComparer only in its
            // string-to-double converter, and string second keys bail below — so both are safe here.
            if (second == null || (second.GetType() != typeof(NumericComparer) && second.GetType() != typeof(NumericComparer11)))
            {
                return false;
            }

            string[] sChars = new string[count];
            byte[][] bBytes = new byte[count][];
            int[] koff = new int[count];
            int[] klen = new int[count];
            ulong[] k2 = new ulong[count];
            int nullCount = 0;
            for (int e = 0; e < count; e++)
            {
                AtomicValue k = values[e].sortKeyValues[0];
                if (k == null)
                {
                    klen[e] = -1;
                    nullCount++;
                }
                else if (!(k is StringValue sv))
                {
                    return false;   // non-string key: let Array.Sort raise the type error
                }
                else
                {
                    UnicodeString u = sv.UnicodeStringValue;
                    if (u is BMPSlice sl)
                    {
                        sChars[e] = sl.Backing;
                        koff[e] = sl.Start;
                        klen[e] = sl.End - sl.Start;
                    }
                    else if (u is BMPString bstr)
                    {
                        string s = bstr.ToString();   // backing string, no copy
                        sChars[e] = s;
                        klen[e] = s.Length;
                    }
                    else if (u is Slice8 s8)
                    {
                        bBytes[e] = s8.ByteArray;
                        koff[e] = s8.Start;
                        klen[e] = s8.End - s8.Start;
                    }
                    else if (u is Twine8 t8)
                    {
                        bBytes[e] = t8.ByteArray;
                        klen[e] = t8.ByteArray.Length;
                    }
                    else if (u is EmptyUnicodeString)
                    {
                        sChars[e] = "";
                        klen[e] = 0;
                    }
                    else
                    {
                        return false;   // StringView (may hold surrogates) / Twine16 / Twine24 / composite
                    }
                }

                AtomicValue v2 = values[e].sortKeyValues[1];
                double d;
                if (v2 == null)
                {
                    d = double.NaN;   // NumericComparer: null compares as NaN
                }
                else if (v2 is DoubleValue dv)
                {
                    d = dv.GetDoubleValue();
                }
                else
                {
                    return false;     // non-double second key: general path
                }

                k2[e] = MapDoubleToOrdinal(d, descending2);
            }

            // Index array: null first keys sort before every string (CompareAtomicValues(null, x) < 0),
            // forming one equal run like any other; the radix pass covers the non-null tail.
            int[] idx = new int[count];
            int p = 0;
            if (nullCount > 0)
            {
                for (int e = 0; e < count; e++)
                {
                    if (klen[e] == -1)
                    {
                        idx[p++] = e;
                    }
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

            RadixSort(idx, nonNullStart, count, sChars, bBytes, koff, klen);

            // Order each run of equal first keys by the mapped second key; exact ties revert to
            // ascending element index == originalPosition (the radix pass is stable by index, but
            // Array.Sort over the ulong keys is not, so equal-key spans are re-sorted by index).
            ulong[] keybuf = new ulong[count];
            int runStart = 0;
            for (int i = 1; i <= count; i++)
            {
                if (i == count || !FirstKeyEqual(idx[i - 1], idx[i], sChars, bBytes, koff, klen))
                {
                    if (i - runStart > 1)
                    {
                        for (int r = runStart; r < i; r++)
                        {
                            keybuf[r] = k2[idx[r]];
                        }

                        Array.Sort(keybuf, idx, runStart, i - runStart);
                        int tieStart = runStart;
                        for (int r = runStart + 1; r <= i; r++)
                        {
                            if (r == i || keybuf[r] != keybuf[r - 1])
                            {
                                if (r - tieStart > 1)
                                {
                                    Array.Sort(idx, tieStart, r - tieStart);
                                }

                                tieStart = r;
                            }
                        }
                    }

                    runStart = i;
                }
            }

            ObjectToBeSorted[] sorted = new ObjectToBeSorted[count];
            for (int r = 0; r < count; r++)
            {
                sorted[r] = values[idx[r]];
            }

            values = sorted;
            return true;
        }

        // Single numeric key under NumericComparer(11) (data-type="number", optionally descending):
        // the same order-preserving ulong mapping as the two-key path, applied to the whole array —
        // one primitive-key Array.Sort plus the index tie-fix, no comparator dispatch. Result is
        // byte-identical to Array.Sort + SortComparer; any unmet condition falls back.
        private bool TrySingleDoubleKeySort()
        {
            if (comparators.Length != 1 || count < RadixMinCount)
            {
                return false;
            }

            IAtomicComparer comp = comparators[0];
            bool descending = false;
            if (comp is DescendingComparer dc)
            {
                descending = true;
                comp = dc.BaseComparer;
            }

            if (comp == null || (comp.GetType() != typeof(NumericComparer) && comp.GetType() != typeof(NumericComparer11)))
            {
                return false;
            }

            ulong[] keys = new ulong[count];
            int[] idx = new int[count];
            for (int e = 0; e < count; e++)
            {
                ObjectToBeSorted obj = values[e];
                AtomicValue k = obj.sortKeyValues == null ? obj.key0 : obj.sortKeyValues[0];
                double d;
                if (k == null)
                {
                    d = double.NaN;   // NumericComparer: null compares as NaN
                }
                else if (k is DoubleValue dv)
                {
                    d = dv.GetDoubleValue();
                }
                else
                {
                    return false;     // non-double key (string conversion etc.): general path
                }

                keys[e] = MapDoubleToOrdinal(d, descending);
                idx[e] = e;
            }

            Array.Sort(keys, idx);
            int tieStart = 0;
            for (int i = 1; i <= count; i++)
            {
                if (i == count || keys[i] != keys[i - 1])
                {
                    if (i - tieStart > 1)
                    {
                        Array.Sort(idx, tieStart, i - tieStart);
                    }

                    tieStart = i;
                }
            }

            ObjectToBeSorted[] sorted = new ObjectToBeSorted[count];
            for (int r = 0; r < count; r++)
            {
                sorted[r] = values[idx[r]];
            }

            values = sorted;
            return true;
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

        private sealed class SortComparer : IComparer<ObjectToBeSorted>
        {
            private IAtomicComparer[] comparators;
            public SortComparer(IAtomicComparer[] comparators)
            {
                this.comparators = comparators;
            }

            public int Compare(ObjectToBeSorted a, ObjectToBeSorted b)
            {
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


                // all sort keys equal: return the items in their original order
                // TODO: unnecessary, we are now using a stable sort routine
                return a.originalPosition - b.originalPosition;
            }
        }
    }
}
