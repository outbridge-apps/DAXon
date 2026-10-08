////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Wrappers;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Model
{
    internal sealed class SequenceTool
    {
        /// <summary>
        /// Constant returned by compareTo() method to indicate an indeterminate ordering between two values
        /// </summary>
        public const int INDETERMINATE_ORDERING = int.MinValue;
        public static IGroundedValue ToGroundedValue(ISequenceIterator iterator)
        {
            if (iterator is IGroundedIterator && ((IGroundedIterator)iterator).IsActuallyGrounded())
            {
                return ((IGroundedIterator)iterator).Materialize();
            }
            else
            {
                return SequenceExtent.From(iterator).Reduce();
            }
        }

        public static ISequence ToMemoSequence(ISequenceIterator iterator)
        {
            if (iterator is EmptyIterator)
            {
                return EmptySequence.GetInstance();
            }
            else if (iterator is IGroundedIterator && ((IGroundedIterator)iterator).IsActuallyGrounded())
            {
                try
                {
                    return ToGroundedValue(iterator);
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }
            else
            {
                return new MemoSequence(iterator);
            }
        }

        public static ISequence ToLazySequence(ISequenceIterator iterator)
        {
            if (iterator is IGroundedIterator && ((IGroundedIterator)iterator).IsActuallyGrounded() && !(iterator is AscendingRangeIterator) && !(iterator is DescendingRangeIterator))
            {
                try
                {
                    return ToGroundedValue(iterator);
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }
            else
            {
                return new LazySequence(iterator);
            }
        }

        public static bool SupportsGetLength(ISequenceIterator iterator)
        {
            return iterator is ILastPositionFinder && ((ILastPositionFinder)iterator).SupportsGetLength();
        }

        public static int GetLength(ISequenceIterator iterator)
        {
            try
            {
                return ((ILastPositionFinder)iterator).GetLength();
            }
            catch (InvalidCastException)
            {
                throw new NotSupportedException("getLength() not available in " + iterator.GetType());
            }
        }

        public static void Supply(ISequenceIterator iter, IItemConsumer<IItem> consumer)
        {
            try
            {
                for (IItem item; (item = iter.Next()) != null;)
                {
                    consumer.Accept(item);
                }
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }

        public static int GetLength(ISequence sequence)
        {
            if (sequence is IGroundedValue)
            {
                return ((IGroundedValue)sequence).GetLength();
            }

            return Count.CountFn(sequence.Iterate());
        }

        public static bool SameLength(ISequenceIterator a, ISequenceIterator b)
        {
            if (SequenceTool.SupportsGetLength(a) && SequenceTool.SupportsGetLength(b))
            {
                return ((ILastPositionFinder)a).GetLength() == ((ILastPositionFinder)b).GetLength();
            }
            else
            {
                while (true)
                {
                    IItem itA = a.Next();
                    IItem itB = b.Next();
                    if (itA == null || itB == null)
                    {
                        if (itA != null)
                        {
                            a.Dispose();
                        }

                        if (itB != null)
                        {
                            b.Dispose();
                        }

                        return itA == null && itB == null;
                    }
                }
            }
        }

        public static IItem AsItem(ISequence sequence)
        {
            if (sequence is IItem)
            {
                return (IItem)sequence;
            }

            ISequenceIterator iter = sequence.Iterate();
            IItem first = iter.Next();
            if (first == null)
            {
                return null;
            }

            if (iter.Next() != null)
            {
                throw new XPathException("Sequence contains more than one item");
            }

            return first;
        }

        public static IFocusIterator FocusTracker(ISequenceIterator basis)
        {
            if (basis is IFocusIterator)
            {
                return (IFocusIterator)basis;
            }
            else
            {
                return new FocusTrackingIterator(basis);
            }
        }

        public static object ConvertToJava(IItem item)
        {
            switch (item.GetGenre())
            {
                case Genre.NODE:
                    object node = item;
                    while (node is IVirtualNode)
                    {

                        // strip off any layers of wrapping
                        node = ((IVirtualNode)node).RealNode;
                    }

                    return node;
                case Genre.FUNCTION:
                case Genre.ARRAY:
                case Genre.MAP:
                    return item;
                case Genre.EXTERNAL:
                    return ((IAnyExternalObject)item).WrappedObject;
                case Genre.ATOMIC:
                    AtomicValue value = (AtomicValue)item;
                    switch (value.GetItemType().PrimitiveType)
                    {
                        case StandardNames.XS_STRING:
                        case StandardNames.XS_UNTYPED_ATOMIC:
                        case StandardNames.XS_ANY_URI:
                        case StandardNames.XS_DURATION:
                        case StandardNames.XS_TIME:
                            return value.GetStringValue();
                        case StandardNames.XS_BOOLEAN:
                            return ((BooleanValue)value).GetBooleanValue() ? true : false;
                        case StandardNames.XS_DECIMAL:
                            return ((DecimalValue)value).GetDecimalValue();
                        case StandardNames.XS_INTEGER:
                            return ((NumericValue)value).LongValue();
                        case StandardNames.XS_DOUBLE:
                            return ((DoubleValue)value).GetDoubleValue();
                        case StandardNames.XS_FLOAT:
                            return ((FloatValue)value).GetFloatValue();
                        case StandardNames.XS_DATE_TIME:
                            return ((DateTimeValue)value).ToSystemDateTimeUtc();
                        case StandardNames.XS_DATE:
                            return ((DateValue)value).ToSystemDateTimeUtc();
                        case StandardNames.XS_BASE64_BINARY:
                            return ((Base64BinaryValue)value).BinaryValue;
                        case StandardNames.XS_HEX_BINARY:
                            return ((HexBinaryValue)value).BinaryValue;
                        default:
                            return item;
                    }

                default:
                    return item;
            }
        }

        public static UnicodeString GetStringValue(ISequence sequence)
        {
            UnicodeBuilder ub = new UnicodeBuilder();
            Supply(sequence.Iterate(), (item) =>
            {
                if (!ub.IsEmpty())
                {
                    ub.Append(' ');
                }

                ub.Accept(item.UnicodeStringValue);
            });
            return ub.ToUnicodeString();
        }

        public static string Stringify(ISequence sequence)
        {
            StringBuilder sb = new StringBuilder(64);
            Supply(sequence.Iterate(), (item) =>
            {
                if (sb.Length != 0)
                {
                    sb.Append(' ');
                }

                sb.Append(item.GetStringValue());
            });
            return sb.ToString();
        }

        public static Types.ItemType GetItemType(ISequence sequence, TypeHierarchy th)
        {
            // Arrays and maps type their members through here, so the depth is the value's (12 KB of JSON).
            StackGuard.Probe();
            try
            {
                if (sequence is IItem)
                {
                    return Types.Type.GetItemType((IItem)sequence, th);
                }
                else if (sequence is IntegerRange)
                {
                    return BuiltInAtomicType.INTEGER;
                }
                else if (sequence is IGroundedValue)
                {
                    try
                    {
                        Types.ItemType type = null;
                        ISequenceIterator iter = sequence.Iterate();
                        for (IItem item; (item = iter.Next()) != null;)
                        {
                            if (type == null)
                            {
                                type = Types.Type.GetItemType(item, th);
                            }
                            else
                            {
                                type = Types.Type.GetCommonSuperType(type, Types.Type.GetItemType(item, th), th);
                            }

                            if (type == AnyItemType.GetInstance())
                            {
                                break;
                            }
                        }

                        return type == null ? ErrorType.GetInstance() : type;
                    }
                    catch (UncheckedXPathException)
                    {
                        return AnyItemType.GetInstance();
                    }
                }
                else
                {
                    return AnyItemType.GetInstance();
                }
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                throw e.Describe("Too many nested arrays or maps to compute the type of a value. The value is too deeply nested.", DAXonErrorCode.SXLM0001, null);
            }
        }

        public static UType GetUType(ISequence sequence)
        {
            if (sequence is IItem)
            {
                return UType.GetUType((IItem)sequence);
            }
            else if (sequence is IGroundedValue)
            {
                UType type = UType.VOID;
                ISequenceIterator iter = sequence.Iterate();
                for (IItem item; (item = iter.Next()) != null;)
                {
                    type = type.Union(UType.GetUType(item));
                    if (type == UType.ANY)
                    {
                        break;
                    }
                }

                return type;
            }
            else
            {
                return UType.ANY;
            }
        }

        public static int GetCardinality(ISequence sequence)
        {
            if (sequence is IItem)
            {
                return StaticProperty.EXACTLY_ONE;
            }

            if (sequence is IGroundedValue)
            {
                int len = ((IGroundedValue)sequence).GetLength();
                switch (len)
                {
                    case 0:
                        return StaticProperty.ALLOWS_ZERO;
                    case 1:
                        return StaticProperty.EXACTLY_ONE;
                    default:
                        return StaticProperty.ALLOWS_ONE_OR_MORE;
                }
            }

            try
            {
                ISequenceIterator iter = sequence.Iterate();
                IItem item = iter.Next();
                if (item == null)
                {
                    return StaticProperty.ALLOWS_ZERO;
                }

                item = iter.Next();
                return item == null ? StaticProperty.EXACTLY_ONE : StaticProperty.ALLOWS_ONE_OR_MORE;
            }
            catch (UncheckedXPathException)
            {
                return StaticProperty.ALLOWS_ONE_OR_MORE;
            }
        }

        public static void Process(ISequence value, Outputter output, ILocation locationId)
        {
            try
            {
                Supply(value.Iterate(), (it) => output.Append(it, locationId, ReceiverOption.ALL_NAMESPACES));
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException().MaybeWithLocation(locationId);
            }
        }

        public static ISequence[] MakeSequenceArray(int length)
        {
            return new ISequence[length];
        }

        public static IAttributeMap AttributeMapFromList(IList<AttributeInfo> list)
        {
            int n = list.Count;
            if (n == 0)
            {
                return EmptyAttributeMap.GetInstance();
            }
            else if (n == 1)
            {
                return SingletonAttributeMap.Of(list[0]);
            }
            else
            {
                // Upstream uses LargeAttributeMap (immutable hash trie) above SmallAttributeMap.LIMIT — a
                // PERFORMANCE structure only; the port's LargeAttributeMap is an empty shell not implementing
                // IAttributeMap, so the cast crashed any element with >8 attributes (fn-doc-33). Linear
                // SmallAttributeMap is semantically identical at any size; revisit only if a perf trace
                // shows attribute lookup on wide elements.
                return new SmallAttributeMap(list);
            }
        }

        public static IGroundedValue ItemOrEmpty(IItem item)
        {
            if (item == null)
            {
                return EmptySequence.GetInstance();
            }
            else
            {
                return item;
            }
        }
    }
}