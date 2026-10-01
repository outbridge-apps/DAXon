////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;

namespace OutSmart.DAXon.Internal.Collections
{
    /// <summary>
    /// Java collection-surface extension methods over the BCL list/set/linked-list types.
    ///
    /// CollectionsModernizer (tools/CollectionsModernizer) rewrites the transpiled tree's
    /// OutSmart.DAXon.Internal.Collections.ArrayList/HashSet/LinkedHashSet/LinkedList wrapper types to their BCL
    /// equivalents; the Java-named members the wrappers used to carry live here instead so
    /// the (unchanged) call sites keep compiling AND keep Java semantics.
    ///
    /// Lives in namespace OutSmart.DAXon.Internal.Collections because every transpiled file has `using OutSmart.DAXon.Internal.Collections;`.
    /// Complements (never duplicates - duplicate signatures would be CS0121 at call sites)
    /// the older OutSmart.DAXon.Internal.JavaApiExtensions, which already provides:
    ///   IsEmpty/Size/AddAll/ToArray/ContainsAll on ICollection&lt;T&gt;,
    ///   Add(IList,int,T) [java.util.List.add(int,E) insert],
    ///   Remove(IList,int) [java.util.List.remove(int) returning the removed element],
    ///   SubList / IndexOf(from) on IList&lt;T&gt;.
    ///
    /// Methods whose name collides with a BCL INSTANCE member (instance always beats
    /// extension) carry an "AndGet"/"AndReturnTrue" suffix; CollectionsModernizer rewrites
    /// the affected call sites (only where the result is actually consumed) to these names.
    /// </summary>
    internal static class CollectionExtensions
    {
        // ------------------------------------------------------------------ IList<T>

        /// <summary>
        /// java.util.List.get(int) - positional read. Java throws IndexOutOfBoundsException
        /// on a bad index; the BCL indexer throws ArgumentOutOfRangeException - equivalent
        /// failure semantics for a faithful port.
        /// </summary>
        public static T Get<T>(this IList<T> list, int index) => list[index];

        /// <summary>
        /// java.util.List.set(int, E) - replaces the element at the index and RETURNS THE
        /// PREVIOUS element (the BCL indexer setter returns nothing - this preserves the
        /// Java return-old contract).
        /// </summary>
        public static T Set<T>(this IList<T> list, int index, T element)
        {
            T prev = list[index];
            list[index] = element;
            return prev;
        }

        /// <summary>
        /// java.util.List.remove(int) - removes the element at the index and RETURNS IT.
        /// Named RemoveAtAndGet because List&lt;T&gt;.RemoveAt(int) is a void INSTANCE method,
        /// so an extension named RemoveAt could never be selected. CollectionsModernizer
        /// rewrites result-consuming RemoveAt call sites to this; statement-position calls
        /// keep the BCL RemoveAt (identical removal effect).
        /// </summary>
        public static T RemoveAtAndGet<T>(this IList<T> list, int index)
        {
            T v = list[index];
            list.RemoveAt(index);
            return v;
        }

        /// <summary>
        /// java.util.Collection.add(E) for lists - ALWAYS returns true (Java's contract for
        /// List.add). Named AddAndReturnTrue because List&lt;T&gt;.Add(T) is a void INSTANCE
        /// method; CollectionsModernizer rewrites only result-consuming Add call sites here.
        /// </summary>
        public static bool AddAndReturnTrue<T>(this ICollection<T> c, T item)
        {
            c.Add(item);
            return true;
        }

        // ----------------------------------------- System.Collections.Generic.LinkedList<T>

        /// <summary>
        /// java.util.LinkedList.add(E) - appends at the tail and returns true (Java's
        /// Collection.add contract). The BCL LinkedList&lt;T&gt; has NO public Add method
        /// (only explicit ICollection&lt;T&gt;.Add), so this extension binds for both
        /// statement-position and result-consuming call sites.
        /// </summary>
        public static bool Add<T>(this LinkedList<T> list, T item)
        {
            list.AddLast(item);
            return true;
        }

        /// <summary>
        /// java.util.LinkedList.getFirst() - first element; throws on an empty list
        /// (Java: NoSuchElementException; here: InvalidOperationException - equivalent
        /// fail-fast semantics).
        /// </summary>
        public static T GetFirst<T>(this LinkedList<T> list)
        {
            var node = list.First;
            if (node == null)
                throw new System.InvalidOperationException("LinkedList is empty (java.util.NoSuchElementException)");
            return node.Value;
        }

        /// <summary>
        /// java.util.LinkedList.getLast() - last element; throws on an empty list
        /// (Java: NoSuchElementException; here: InvalidOperationException).
        /// </summary>
        public static T GetLast<T>(this LinkedList<T> list)
        {
            var node = list.Last;
            if (node == null)
                throw new System.InvalidOperationException("LinkedList is empty (java.util.NoSuchElementException)");
            return node.Value;
        }

        /// <summary>
        /// java.util.LinkedList.removeFirst() - removes AND RETURNS the first element;
        /// throws on empty. Named RemoveFirstAndGet because the BCL RemoveFirst() is a void
        /// INSTANCE method; CollectionsModernizer rewrites result-consuming sites here.
        /// </summary>
        public static T RemoveFirstAndGet<T>(this LinkedList<T> list)
        {
            T v = list.GetFirst();
            list.RemoveFirst();
            return v;
        }

        /// <summary>
        /// java.util.LinkedList.removeLast() - removes AND RETURNS the last element;
        /// throws on empty. Named RemoveLastAndGet for the same instance-collision reason.
        /// </summary>
        // java.util.Collection.containsAll.
        public static bool ContainsAll<T>(this ICollection<T> c, IEnumerable<T> other)
        {
            if (other == null)
                return true;
            foreach (var x in other)
            {
                if (!c.Contains(x))
                    return false;
            }
            return true;
        }

        // List<T>.AddRange for arbitrary ICollection receivers (HashSet, IList, ...).
        public static void AddRange<T>(this ICollection<T> dest, IEnumerable<T> src)
        {
            if (src == null)
                return;
            foreach (var x in src)
                dest.Add(x);
        }

        // List<T>.GetRange for IList receivers (returns a copy, as List.GetRange does).
        public static List<T> GetRange<T>(this IList<T> list, int index, int count)
        {
            var result = new List<T>(count);
            for (int i = 0; i < count; i++)
                result.Add(list[index + i]);
            return result;
        }

        public static T RemoveLastAndGet<T>(this LinkedList<T> list)
        {
            T v = list.GetLast();
            list.RemoveLast();
            return v;
        }
    }
}
