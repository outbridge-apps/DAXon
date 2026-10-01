////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values.Arrays
{
    /// <summary>
    /// Implementation of the extension function array:sort(array, function) =&gt; array
    /// </summary>
    internal sealed class ArraySort : ArrayFunctionSet.ArrayGeneratingFunction
    {

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            ArrayItem array = (ArrayItem)arguments[0].Head();
            List<MemberToBeSorted> inputList = new List<MemberToBeSorted>(array.ArrayLength());
            int i = 0;
            IStringCollator collation;
            if (arguments.Length == 1)
            {
                collation = context.GetConfiguration().GetCollation(GetRetainedStaticContext().DefaultCollationName);
            }
            else
            {
                StringValue collName = (StringValue)arguments[1].Head();
                if (collName == null)
                {
                    collation = context.GetConfiguration().GetCollation(GetRetainedStaticContext().DefaultCollationName);
                }
                else
                {
                    collation = context.GetConfiguration().GetCollation(collName.GetStringValue(), StaticBaseUriString);
                }
            }

            IFunctionItem key = null;
            if (arguments.Length == 3)
            {
                key = (IFunctionItem)arguments[2].Head();
            }

            foreach (IGroundedValue seq in array.Members())
            {
                MemberToBeSorted member = new MemberToBeSorted();
                member.value = seq;
                member.originalPosition = i++;
                if (key != null)
                {
                    member.sortKey = DynamicCall(key, context, new ISequence[] { seq }).Materialize();
                }
                else
                {
                    member.sortKey = Atomize(seq);
                }

                inputList.Add(member);
            }

            IAtomicComparer atomicComparer = AtomicSortComparer.MakeSortComparer(collation, StandardNames.XS_ANY_ATOMIC_TYPE, context);
            try
            {
                inputList.Sort((a, b) =>
                {
                    int result = CompareSortKeys(a.sortKey, b.sortKey, atomicComparer);
                    if (result == 0)
                    {

                        // TODO: unnecessary, we are now using a stable sort routine
                        return a.originalPosition - b.originalPosition;
                    }
                    else
                    {
                        return result;
                    }
                }); //GenericSorter.quickSort(0, array.arrayLength(), sortable);
            }
            catch (InvalidOperationException e) when (e.InnerException != null)
            {
                // List<T>.Sort wraps any comparer exception in InvalidOperationException, so the plain
                // catch below never saw it. A real XPathException (e.g. from key evaluation) must propagate
                // as-is; a type-comparison failure (InvalidCastException, upstream ClassCastException) is the
                // XPTY0004 "non-comparable types" type error.
                if (e.InnerException is XPathException xe)
                {
                    throw xe;
                }
                if (e.InnerException is InvalidCastException)
                {
                    throw new XPathException("Non-comparable types found while sorting: " + e.InnerException.Message).WithErrorCode("XPTY0004").AsTypeError();
                }
                throw;
            }
            catch (InvalidCastException e)
            {
                throw new XPathException("Non-comparable types found while sorting: " + e.Message).WithErrorCode("XPTY0004").AsTypeError();
            }

            IList<IGroundedValue> outputList = new List<IGroundedValue>(array.ArrayLength());
            foreach (MemberToBeSorted member in inputList)
            {
                outputList.Add(member.value);
            }

            return MakeArray(outputList);
        }

        public static int CompareSortKeys(IGroundedValue a, IGroundedValue b, IAtomicComparer comparer)
        {
            // Singleton-atomic keys (the common case): identical to the loop below — one compare, and on 0
            // both iterators would be exhausted → 0 — without allocating two iterators per comparison.
            if (a is AtomicValue av && b is AtomicValue bv)
            {
                try
                {
                    return comparer.CompareAtomicValues(av, bv);
                }
                catch (NoDynamicContextException e)
                {
                    throw new InvalidOperationException(e.Message, e);
                }
            }

            ISequenceIterator iteratora = a.Iterate();
            ISequenceIterator iteratorb = b.Iterate();
            while (true)
            {
                AtomicValue firsta = (AtomicValue)iteratora.Next();
                AtomicValue firstb = (AtomicValue)iteratorb.Next();
                if (firsta == null)
                {
                    if (firstb == null)
                    {
                        return 0;
                    }
                    else
                    {
                        return -1;
                    }
                }
                else if (firstb == null)
                {
                    return +1;
                }
                else
                {
                    try
                    {
                        int first = comparer.CompareAtomicValues(firsta, firstb);
                        if (first == 0)
                        {
                            continue;
                        }
                        else
                        {
                            return first;
                        }
                    }
                    catch (NoDynamicContextException e)
                    {
                        throw new InvalidOperationException(e.Message, e);
                    }
                }
            }
        }

        private static IGroundedValue Atomize(ISequence input)
        {
            try
            {
                ISequenceIterator iterator = input.Iterate();
                ISequenceIterator mapper = Atomizer.GetAtomizingIterator(iterator, false);
                return SequenceTool.ToGroundedValue(mapper);
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }
        private sealed class MemberToBeSorted
        {
            public IGroundedValue value;
            public IGroundedValue sortKey;
            public int originalPosition;
        }
    }
}