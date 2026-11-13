////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.XQuery;

namespace OutSmart.DAXon.Api
{
    // A wrapped value: XdmNode and XdmItem keep their NodeInfo or item here.
    public class XdmValue
    {
        private readonly object _value;
        public virtual object UnderlyingValue => _value;
        // The empty sequence.
        public XdmValue() { _value = EmptySequence.GetInstance(); }
        // null is the empty sequence; an item subclass keeps what it is given.
        public XdmValue(object value)
        {
            // Anything else failed later as a raw cast, or enumerated as nothing.
            if (value != null && !(value is ISequence))
            {
                throw new ArgumentException("An XdmValue holds an engine sequence (ISequence), not a " + value.GetType().FullName, nameof(value));
            }

            _value = value ?? (this is XdmItem ? null : EmptySequence.GetInstance());
        }

        /// <summary>The sequence of these items, in this order. Null is the empty sequence, as for the other constructor.</summary>
        public XdmValue(IEnumerable<XdmItem> items)
        {
            _value = items == null ? EmptySequence.GetInstance() : SequenceOf(items);
        }

        private static IGroundedValue SequenceOf(IEnumerable<XdmItem> items)
        {
            var list = new List<IItem>();
            foreach (XdmItem item in items)
            {
                ArgumentCheck.NotNull(item, nameof(items));
                list.Add(item.UnderlyingValue);
            }

            return SequenceExtent.MakeSequenceExtent(list);
        }

        private IGroundedValue _grounded;

        // The items with a length and an index, so that a loop over ItemAt is linear: an engine result is grounded
        // already, a lazy value is read once. Null for an item, which is its own one item.
        private IGroundedValue Grounded()
        {
            if (this is XdmItem || !(_value is ISequence sequence))
            {
                return null;
            }

            if (_grounded == null)
            {
                try
                {
                    _grounded = sequence as IGroundedValue ?? sequence.Materialize();
                }
                catch (Exception e) when (ApiErrors.IsEngineError(e))
                {
                    throw ApiErrors.WrapUnchecked(e);
                }
            }

            return _grounded;
        }

        /// <summary>The number of items.</summary>
        public int Size()
        {
            IGroundedValue grounded = Grounded();
            return grounded != null ? grounded.GetLength() : this is XdmItem ? 1 : 0;
        }

        /// <summary>Whether this is the empty sequence.</summary>
        public bool IsEmpty()
        {
            return Size() == 0;
        }

        /// <summary>The item at this position, counting from zero.</summary>
        public XdmItem ItemAt(int n)
        {
            if (n >= 0)
            {
                IGroundedValue grounded = Grounded();
                if (grounded == null)
                {
                    if (n == 0 && this is XdmItem self)
                    {
                        return self;
                    }
                }
                else if (n < grounded.GetLength())
                {
                    return (XdmItem)Wrap(grounded.ItemAt(n));
                }
            }

            throw new ArgumentOutOfRangeException(nameof(n), n, "The value has no item at this position");
        }

        /// <summary>The items from this position, counting from zero, as many as length says or as there are.</summary>
        public XdmValue Subsequence(int start, int length)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start), start, "A position cannot be negative");
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "A length cannot be negative");
            }

            var items = new List<XdmItem>();
            int at = 0;
            foreach (XdmItem item in this)
            {
                if (at >= start && at - start < length)
                {
                    items.Add(item);
                }

                at++;
            }

            return new XdmValue(items);
        }

        /// <summary>A new value: the items of this one and then those of the other.</summary>
        public XdmValue Append(XdmValue other)
        {
            ArgumentCheck.NotNull(other, nameof(other));
            var items = new List<XdmItem>();
            foreach (XdmItem item in this)
            {
                items.Add(item);
            }

            foreach (XdmItem item in other)
            {
                items.Add(item);
            }

            return new XdmValue(items);
        }

        /// <summary>
        /// The value for an object: an XdmValue as it is, an engine sequence wrapped, a list of objects as a sequence
        /// (see <see cref="MakeSequence"/>), anything else as XdmAtomicValue.MakeAtomicValue takes it.
        /// </summary>
        public static XdmValue MakeValue(object value)
        {
            ArgumentCheck.NotNull(value, nameof(value));
            if (value is XdmValue xdm)
            {
                return xdm;
            }

            if (value is ISequence)
            {
                return Wrap(value);
            }

            if (value is System.Collections.IEnumerable list && !(value is string))
            {
                return MakeSequence(list);
            }

            return XdmAtomicValue.MakeAtomicValue(value);
        }

        /// <summary>The sequence of these objects, each taken as MakeValue takes it. A sequence among them adds its items, as XDM has no nested sequences.</summary>
        public static XdmValue MakeSequence(System.Collections.IEnumerable objects)
        {
            ArgumentCheck.NotNull(objects, nameof(objects));
            return new XdmValue(Flatten(objects));
        }

        // Depth first on a stack of its own, so a deep nesting costs no thread stack; a list met again inside
        // itself would never end and is refused.
        private static List<XdmItem> Flatten(System.Collections.IEnumerable objects)
        {
            var items = new List<XdmItem>();
            var open = new Stack<System.Collections.IEnumerator>();
            var path = new Stack<object>();
            var onPath = new HashSet<object>(SameObject.Instance);
            try
            {
                open.Push(objects.GetEnumerator());
                path.Push(objects);
                onPath.Add(objects);
                while (open.Count > 0)
                {
                    System.Collections.IEnumerator current = open.Peek();
                    if (!current.MoveNext())
                    {
                        (open.Pop() as IDisposable)?.Dispose();
                        onPath.Remove(path.Pop());
                        continue;
                    }

                    object o = current.Current;
                    ArgumentCheck.NotNull(o, nameof(objects));
                    if (o is System.Collections.IEnumerable list && !(o is string) && !(o is XdmValue) && !(o is ISequence))
                    {
                        if (!onPath.Add(list))
                        {
                            throw new ArgumentException("A list that contains itself has no sequence", nameof(objects));
                        }

                        open.Push(list.GetEnumerator());
                        path.Push(list);
                        continue;
                    }

                    foreach (XdmItem item in MakeValue(o))
                    {
                        items.Add(item);
                    }
                }
            }
            finally
            {
                while (open.Count > 0)
                {
                    (open.Pop() as IDisposable)?.Dispose();
                }
            }

            return items;
        }

        private sealed class SameObject : IEqualityComparer<object>
        {
            internal static readonly SameObject Instance = new SameObject();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object o)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
            }
        }

        private static readonly Lazy<Configuration> printing = new Lazy<Configuration>(() => new Configuration());

        // The value as the adaptive output method writes it, indented: markup for a node, map{...} for a map, the items
        // of a sequence one to a line. As in s9api, where a node has no ToString of its own either.
        public override string ToString()
        {
            if (!(_value is ISequence sequence))
            {
                return base.ToString();
            }

            try
            {
                Configuration config = null;
                ISequenceIterator items = sequence.Iterate();
                for (IItem item; config == null && (item = items.Next()) != null;)
                {
                    config = (item as NodeInfo)?.GetConfiguration();
                }

                var properties = new SerializationProperties();
                properties.SetProperty(DAXonOutputKeys.METHOD, "adaptive");
                properties.SetProperty(DAXonOutputKeys.INDENT, "yes");
                properties.SetProperty(DAXonOutputKeys.OMIT_XML_DECLARATION, "yes");
                var written = new System.IO.StringWriter();
                QueryResult.SerializeSequence(sequence.Iterate(), config ?? printing.Value, new StreamResult(written), properties);
                return written.ToString().TrimEnd('\n');
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // A log line or a debugger calls this: a value that cannot be written - too deep for the stack,
                // holding half a surrogate pair - is named by its class, as every value was before 1.4.
                return base.ToString();
            }
        }
        // Type-dispatching Wrap: a NodeInfo must wrap as XdmNode (MessageInstr.MakeMessage casts
        // (XdmNode)XdmNode.Wrap(content)); AtomicValue -> XdmAtomicValue.
        public static XdmValue Wrap(object value)
        {
            if (value is NodeInfo __n)
                return new XdmNode(__n);
            if (value is AtomicValue __a)
                return new XdmAtomicValue(__a);
            // upstream singleton dispatch: a lone map/array/function wraps as its XdmItem subclass
            // (callers cast Wrap(singleItem) to XdmItem, e.g. XPathCompiler.EvaluateSingle)
            if (value is OutSmart.DAXon.Values.Maps.MapItem __m)
                return new XdmMap(__m);
            if (value is OutSmart.DAXon.Values.Arrays.ArrayItem __arr)
                return new XdmArray(__arr);
            if (value is IFunctionItem __f)
                return new XdmFunctionItem(__f);
            return new XdmValue(value);
        }
        // Whether the value is an instance of the sequence type.
        public virtual bool Matches(SequenceType type)
        {
            ArgumentCheck.NotNull(type, nameof(type));
            ItemType itemType = type.GetItemType();
            int size = 0;
            foreach (XdmItem item in this)
            {
                size++;
                if (!itemType.Matches(item))
                {
                    return false;
                }
            }

            return type.GetOccurrenceIndicator().Allows(size);
        }

        // The form callers were compiled against while this answered false to everything: a sequence type, or an
        // item type, which the value matches when it is one such item.
        public bool Matches(object t)
        {
            return t is SequenceType type ? Matches(type) : t is ItemType itemType && Matches(itemType.One());
        }
        // Enumerate the wrapped value's items as XdmItems (was an always-empty stub — any foreach
        // over an XdmValue silently saw nothing, e.g. the driver's context-select narrowing).
        public IEnumerator<XdmItem> GetEnumerator()
        {
            if (this is XdmItem selfItem)
            {
                yield return selfItem;
                yield break;
            }
            if (_value is OutSmart.DAXon.Model.ISequence seq)
            {
                // An engine error met reading the value (a time limit, a lazy failure) is the API's, as for XdmSequenceIterator.
                ISequenceIterator iter;
                try
                {
                    iter = seq.Iterate();
                }
                catch (Exception e) when (ApiErrors.IsEngineError(e))
                {
                    throw ApiErrors.WrapUnchecked(e);
                }

                for (;;)
                {
                    OutSmart.DAXon.Model.IItem it;
                    try
                    {
                        it = iter.Next();
                    }
                    catch (Exception e) when (ApiErrors.IsEngineError(e))
                    {
                        throw ApiErrors.WrapUnchecked(e);
                    }

                    if (it == null)
                    {
                        yield break;
                    }

                    yield return (XdmItem)Wrap(it);
                }
            }
        }
    }
}
