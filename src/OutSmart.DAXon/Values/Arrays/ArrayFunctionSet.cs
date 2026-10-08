////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.functions.qt4.Slice;
//import com.saxonica.functions.qt4.UnparcelFn;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Collections.Zeno;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values.Arrays
{
    /// <summary>
    /// Function signatures (and pointers to implementations) of the functions defined in XPath 3.1
    /// </summary>
    internal sealed class ArrayFunctionSet : BuiltInFunctionSet
    {
        private static readonly ArrayFunctionSet instance31 = new ArrayFunctionSet(31);
        private static readonly ArrayFunctionSet instance40 = new ArrayFunctionSet(40);

        public override string ConventionalPrefix => "array";
        private ArrayFunctionSet(int version)
        {
            Init(version);
        }

        public static ArrayFunctionSet GetInstance(int version)
        {
            return version >= 40 ? instance40 : instance31;
        }

        private void Init(int version)
        {
            Register("append", 2, (e) => e.Populate(() => new ArrayAppend(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, AnyItemType.GetInstance(), STAR | NAV, null));
            ItemType filterFunctionType = new SpecificFunctionType(new SequenceType[] { SequenceType.ANY_SEQUENCE }, SequenceType.SINGLE_BOOLEAN);
            Register("filter", 2, (e) => e.Populate(() => new ArrayFilter(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, filterFunctionType, ONE | INS, null));
            Register("flatten", 1, (e) => e.Populate(() => new ArrayFlatten(), AnyItemType.GetInstance(), STAR, 0).Arg(0, AnyItemType.GetInstance(), STAR | ABS, null));
            ItemType foldFunctionType = new SpecificFunctionType(new SequenceType[] { SequenceType.ANY_SEQUENCE, SequenceType.ANY_SEQUENCE }, SequenceType.ANY_SEQUENCE);
            Register("fold-left", 3, (e) => e.Populate(() => new ArrayFoldLeft(), AnyItemType.GetInstance(), STAR, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, AnyItemType.GetInstance(), STAR | NAV, null).Arg(2, foldFunctionType, ONE | INS, null));
            Register("fold-right", 3, (e) => e.Populate(() => new ArrayFoldRight(), AnyItemType.GetInstance(), STAR, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, AnyItemType.GetInstance(), STAR | NAV, null).Arg(2, foldFunctionType, ONE | INS, null));
            ItemType forEachFunctionType = new SpecificFunctionType(new SequenceType[] { SequenceType.ANY_SEQUENCE }, SequenceType.ANY_SEQUENCE);
            Register("for-each", 2, (e) => e.Populate(() => new ArrayForEach(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, forEachFunctionType, ONE | INS, null));
            Register("for-each-pair", 3, (e) => e.Populate(() => new ArrayForEachPair(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(2, foldFunctionType, ONE | INS, null));
            Register("get", 2, (e) => e.Populate(() => new ArrayGet(), AnyItemType.GetInstance(), STAR, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.INTEGER, ONE | ABS, null));
            Register("head", 1, (e) => e.Populate(() => new ArrayHead(), AnyItemType.GetInstance(), STAR, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null));
            Register("insert-before", 3, (e) => e.Populate(() => new ArrayInsertBefore(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.INTEGER, ONE | ABS, null).Arg(2, AnyItemType.GetInstance(), STAR | NAV, null));
            Register("join", 1, (e) => e.Populate(() => new ArrayJoin(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, STAR | INS, null));
            Register("put", 3, (e) => e.Populate(() => new ArrayPut(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.INTEGER, ONE | INS, null).Arg(2, AnyItemType.GetInstance(), STAR | NAV, null));
            Register("remove", 2, (e) => e.Populate(() => new ArrayRemove(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.INTEGER, STAR | ABS, null));
            Register("reverse", 1, (e) => e.Populate(() => new ArrayReverse(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null));
            Register("size", 1, (e) => e.Populate(() => new ArraySize(), BuiltInAtomicType.INTEGER, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null));
            ItemType sortFunctionType = new SpecificFunctionType(new SequenceType[] { SequenceType.ANY_SEQUENCE }, SequenceType.ATOMIC_SEQUENCE);
            Register("sort", 1, (e) => e.Populate(() => new ArraySort(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null));
            Register("sort", 2, (e) => e.Populate(() => new ArraySort(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.STRING, OPT | ABS, null));
            Register("sort", 3, (e) => e.Populate(() => new ArraySort(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.STRING, OPT | ABS, null).Arg(2, sortFunctionType, ONE | INS, null));
            Register("subarray", 2, (e) => e.Populate(() => new ArraySubarray(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.INTEGER, ONE | ABS, null));
            Register("subarray", 3, (e) => e.Populate(() => new ArraySubarray(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.INTEGER, ONE | ABS, null).Arg(2, BuiltInAtomicType.INTEGER, (version >= 40 ? OPT : ONE) | ABS, null));
            Register("tail", 1, (e) => e.Populate(() => new ArrayTail(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null));

            if (version >= 40)
            {
                ItemType replaceActionType = new SpecificFunctionType(new SequenceType[] { SequenceType.ANY_SEQUENCE }, SequenceType.ANY_SEQUENCE);
                Register("empty", 1, (e) => e.Populate(() => new ArrayEmpty(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null));
                Register("exists", 1, (e) => e.Populate(() => new ArrayExists(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null));
                Register("foot", 1, (e) => e.Populate(() => new ArrayFoot(), AnyItemType.GetInstance(), STAR, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null));
                Register("replace", 3, (e) => e.Populate(() => new ArrayReplace(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.INTEGER, ONE | ABS, null).Arg(2, replaceActionType, ONE | INS, null));
            }

            // TODO: the following functions should be private
            Register("_to-sequence", 1, (e) => e.Populate(() => new ArrayToSequence(), AnyItemType.GetInstance(), STAR, 0).Arg(0, ArrayItemType.ANY_ARRAY_TYPE, ONE | INS, null));
            Register("_from-sequence", 1, (e) => e.Populate(() => new ArrayFromSequence(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, AnyItemType.GetInstance(), STAR | INS, null));
        }

        public override NamespaceUri GetNamespace()
        {
            return NamespaceUri.ARRAY_FUNCTIONS;
        }

        public static int CheckSubscript(IntegerValue subscript, int limit)
        {
            int index = subscript.AsSubscript();
            if (index <= 0)
            {
                throw new XPathException("Array subscript " + subscript.UnicodeStringValue + " is out of range", "FOAY0001");
            }

            if (index > limit)
            {
                throw new XPathException("Array subscript " + subscript.UnicodeStringValue + " exceeds limit (" + limit + ")", "FOAY0001");
            }

            return index;
        }

        internal abstract class ArrayGeneratingFunction : SystemFunction, IPingable
        {
            private double numberOfCalls = 0;
            private double numberOfConversions = 0;
            private double totalSize = 0;
            public void Ping()
            {
                numberOfConversions++;
            }

            protected virtual int ExpectedSize()
            {
                return numberOfCalls < 10 ? 10 : (int)(totalSize / numberOfCalls * 1.05); // allow a little leeway
            }

            protected virtual ArrayItem MakeArray(IList<IGroundedValue> members)
            {
                if (numberOfConversions > Math.Max(10, numberOfCalls * 0.5))
                {

                    // More than half the calls result in the array being converted...
                    return new ImmutableArrayItem(members);
                }
                else
                {
                    numberOfCalls++;
                    totalSize += members.Count;
                    SimpleArrayItem result = new SimpleArrayItem(members);
                    result.RequestNotification(this);
                    return result;
                }
            }
        }

        /// <summary>
        /// Implementation of the function array:append(array, item()*) =&gt; array
        /// </summary>
        internal sealed class ArrayAppend : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                return array.Append(arguments[1].Materialize());
            }
        }

        /// <summary>
        /// Implementation of the function array:filter(array, function) =&gt; array
        /// </summary>
        internal sealed class ArrayFilter : ArrayGeneratingFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                IFunctionItem fn = (IFunctionItem)arguments[1].Head();
                IList<IGroundedValue> list = new List<IGroundedValue>(ExpectedSize());
                foreach (IGroundedValue gv in array.Members())
                {
                    Core.Controller.CheckActiveTimeoutPerStep();
                    if (((BooleanValue)DynamicCall(fn, context, new ISequence[] { gv }).Head()).GetBooleanValue())
                    {
                        list.Add(gv);
                    }
                }

                return MakeArray(list);
            }
        }

        /// <summary>
        /// Implementation of the function array:flatten =&gt; item()*
        /// </summary>
        internal sealed class ArrayFlatten : SystemFunction
        {
            private void Flatten(ISequence arg, IList<IItem> @out)
            {
                // one level of recursion per level of array nesting, so the depth is the input's
                StackGuard.Probe();
                SequenceTool.Supply(arg.Iterate(), (item) =>
                {
                    if (item is ArrayItem)
                    {
                        foreach (IGroundedValue member in ((ArrayItem)item).Members())
                        {
                            Flatten(member, @out);
                        }
                    }
                    else
                    {
                        @out.Add(item);
                    }
                });
            }

            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                IList<IItem> @out = new List<IItem>();
                Flatten(arguments[0], @out);
                return SequenceExtent.MakeSequenceExtent(@out);
            }
        }

        /// <summary>
        /// Implementation of the function array:fold-left(array, item()*, function) =&gt; array
        /// </summary>
        internal sealed class ArrayFoldLeft : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                int arraySize = array.ArrayLength();
                ISequence zero = arguments[1];
                IFunctionItem fn = (IFunctionItem)arguments[2].Head();
                int i;
                for (i = 0; i < arraySize; i++)
                {
                    Core.Controller.CheckActiveTimeoutPerStep();
                    zero = DynamicCall(fn, context, new ISequence[] { zero, array[i] });
                }

                return zero;
            }
        }

        /// <summary>
        /// Implementation of the function array:fold-left(array, item()*, function) =&gt; array
        /// </summary>
        internal sealed class ArrayFoldRight : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                ISequence zero = arguments[1];
                IFunctionItem fn = (IFunctionItem)arguments[2].Head();
                int i;
                for (i = array.ArrayLength() - 1; i >= 0; i--)
                {
                    Core.Controller.CheckActiveTimeoutPerStep();
                    zero = DynamicCall(fn, context, new ISequence[] { array[i], zero });
                }

                return zero;
            }
        }

        /// <summary>
        /// Implementation of the proposed 4.0 function array:exists(array)
        /// </summary>
        internal sealed class ArrayExists : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                int len = array.ArrayLength();
                return BooleanValue.Get(len > 0);
            }
        }

        /// <summary>
        /// Implementation of the proposed 4.0 function array:empty(array)
        /// </summary>
        internal sealed class ArrayEmpty : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                int len = array.ArrayLength();
                return BooleanValue.Get(len == 0);
            }
        }

        /// <summary>
        /// Implementation of the proposed 4.0 function array:foot(array) =&gt; item()*
        /// </summary>
        internal sealed class ArrayFoot : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                int len = array.ArrayLength();
                if (len == 0)
                {
                    throw new XPathException("Argument to array:foot is an empty array", "FOAY0001");
                }

                return array[len - 1];
            }
        }

        /// <summary>
        /// Implementation of the function array:for-each(array, function) =&gt; array
        /// </summary>
        internal sealed class ArrayForEach : ArrayGeneratingFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                IFunctionItem fn = (IFunctionItem)arguments[1].Head();
                IList<IGroundedValue> list = new List<IGroundedValue>(ExpectedSize());
                foreach (IGroundedValue gv in array.Members())
                {
                    Core.Controller.CheckActiveTimeoutPerStep();
                    list.Add(DynamicCall(fn, context, new IGroundedValue[] { gv }).Materialize());
                }

                return MakeArray(list);
            }
        }

        /// <summary>
        /// Implementation of the function array:for-each-pair(array, array, function) =&gt; array
        /// </summary>
        internal sealed class ArrayForEachPair : ArrayGeneratingFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array1 = (ArrayItem)arguments[0].Head();
                ArrayItem array2 = (ArrayItem)arguments[1].Head();
                IFunctionItem fn = (IFunctionItem)arguments[2].Head();
                IList<IGroundedValue> list = new List<IGroundedValue>(ExpectedSize());
                int i;
                for (i = 0; i < array1.ArrayLength() && i < array2.ArrayLength(); i++)
                {
                    Core.Controller.CheckActiveTimeoutPerStep();
                    list.Add(DynamicCall(fn, context, new ISequence[] { array1[i], array2[i] }).Materialize());
                }

                return MakeArray(list);
            }
        }

        /// <summary>
        /// Implementation of the function array:get(array, xs:integer) =&gt; item()*
        /// </summary>
        internal sealed class ArrayGet : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                IntegerValue index = (IntegerValue)arguments[1].Head();
                if (arguments.Length <= 2)
                {
                    return array[CheckSubscript(index, array.ArrayLength()) - 1];
                }
                else
                {
                    int i = index.AsSubscript();
                    if (i <= 0 || i > array.ArrayLength())
                    {
                        IFunctionItem fn = (IFunctionItem)arguments[2].Head();
                        return DynamicCall(fn, context, index);
                    }
                    else
                    {
                        return array[i - 1];
                    }
                }
            }
        }

        /// <summary>
        /// Implementation of the function array:head(array) =&gt; item()*
        /// </summary>
        internal sealed class ArrayHead : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                if (array.ArrayLength() == 0)
                {
                    throw new XPathException("Argument to array:head is an empty array", "FOAY0001");
                }

                return array[0];
            }
        }

        /// <summary>
        /// Implementation of the function array:insert-before(array, xs:integer, item()*) =&gt; array
        /// </summary>
        internal sealed class ArrayInsertBefore : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                int index = CheckSubscript((IntegerValue)arguments[1].Head(), array.ArrayLength() + 1) - 1;
                if (index < 0 || index > array.ArrayLength())
                {
                    throw new XPathException("Specified position is not in range", "FOAY0001");
                }

                ISequence newMember = arguments[2];
                return array.Insert(index, newMember.Materialize());
            }
        }

        /// <summary>
        /// Implementation of the function array:join(arrays) =&gt; array
        /// </summary>
        internal sealed class ArrayJoin : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ISequenceIterator iterator = arguments[0].Iterate();
                ArrayItem array = SimpleArrayItem.EMPTY_ARRAY;
                ArrayItem nextArray;
                while ((nextArray = (ArrayItem)iterator.Next()) != null)
                {
                    array = array.Concat(nextArray);
                }

                return array;
            }
        }

        /// <summary>
        /// Implementation of the function array:put(arrays, index, newValue) =&gt; array
        /// </summary>
        internal sealed class ArrayPut : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                int index = CheckSubscript((IntegerValue)arguments[1].Head(), array.ArrayLength()) - 1;
                IGroundedValue newVal = arguments[2].Materialize();
                return array.Put(index, newVal);
            }
        }

        /// <summary>
        /// Implementation of the function array:remove(array, xs:integer) =&gt; array
        /// </summary>
        internal sealed class ArrayRemove : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                IGroundedValue offsets = arguments[1].Materialize();
                if (offsets is IntegerValue)
                {
                    int index = CheckSubscript((IntegerValue)offsets, array.ArrayLength()) - 1;
                    return array.Remove(index);
                }

                IntSet positions = new IntHashSet();
                ISequenceIterator arg1 = offsets.Iterate();
                SequenceTool.Supply(arg1, (pos) =>
                {
                    int index = CheckSubscript((IntegerValue)pos, array.ArrayLength()) - 1;
                    positions.Add(index);
                });
                return array.RemoveSeveral(positions);
            }
        }

        /// <summary>
        /// Implementation of the function array:replace(array, position, action) =&gt; array
        /// </summary>
        internal sealed class ArrayReplace : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                IntegerValue index = (IntegerValue)arguments[1].Head();
                int pos = CheckSubscript(index, array.ArrayLength()) - 1;
                IGroundedValue oldVal = array[pos];
                IFunctionItem fn = (IFunctionItem)arguments[2].Head();
                IGroundedValue newVal = DynamicCall(fn, context, oldVal).Materialize();
                return array.Put(pos, newVal);
            }
        }

        /// <summary>
        /// Implementation of the function array:reverse(array, xs:integer, xs:integer) =&gt; array
        /// </summary>
        internal sealed class ArrayReverse : ArrayGeneratingFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                IList<IGroundedValue> list = new List<IGroundedValue>(array.ArrayLength());
                int i;
                for (i = 0; i < array.ArrayLength(); i++)
                {
                    list.Add(array[array.ArrayLength() - i - 1]);
                }

                return MakeArray(list);
            }
        }

        /// <summary>
        /// Implementation of the function array:size(array) =&gt; integer
        /// </summary>
        internal sealed class ArraySize : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                return new Int64Value(array.ArrayLength());
            }
        }

        /// <summary>
        /// Implementation of the function array:subarray(array, xs:integer, xs:integer) =&gt; array
        /// </summary>
        internal sealed class ArraySubarray : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                int start = CheckSubscript((IntegerValue)arguments[1].Head(), array.ArrayLength() + 1);
                int length;
                if (arguments.Length == 3)
                {
                    IntegerValue len = (IntegerValue)arguments[2].Head();
                    if (len == null)
                    {
                        length = array.ArrayLength() - start + 1;
                    }
                    else
                    {
                        int signum = len.Signum();
                        if (signum < 0)
                        {
                            throw new XPathException("Specified length of subarray is less than zero", "FOAY0002");
                        }

                        length = signum == 0 ? 0 : CheckSubscript(len, array.ArrayLength());
                    }
                }
                else
                {
                    length = array.ArrayLength() - start + 1;
                }

                if (start < 1)
                {
                    throw new XPathException("Start position is less than one", "FOAY0001");
                }

                if (start > array.ArrayLength() + 1)
                {
                    throw new XPathException("Start position is out of bounds", "FOAY0001");
                }

                if (start + length > array.ArrayLength() + 1)
                {
                    throw new XPathException("Specified length of subarray is too great for start position given", "FOAY0001");
                }

                return array.SubArray(start - 1, start + length - 1);
            }
        }

        internal sealed class ArrayTail : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                if (array.ArrayLength() < 1)
                {
                    throw new XPathException("Argument to array:tail is an empty array", "FOAY0001");
                }

                return array.Remove(0);
            }
        }

        internal sealed class ArrayToSequence : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ArrayItem array = (ArrayItem)arguments[0].Head();
                return ToSequence(array);
            }

            public static ISequence ToSequence(ArrayItem array)
            {
                ZenoSequence results = new ZenoSequence();
                foreach (IGroundedValue seq in array.Members())
                {
                    results = results.AppendSequence(seq);
                }

                return results;
            }
        }

        internal sealed class ArrayFromSequence : FoldingFunction, IPingable
        {
            private double numberOfCalls = 0;
            private double numberOfConversions = 0;
            public void Ping()
            {
                numberOfConversions++;
            }

            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                if (numberOfConversions > Math.Max(10, numberOfCalls * 0.5))
                {
                    return ImmutableArrayItem.From(arguments[0].Iterate());
                }
                else
                {
                    SimpleArrayItem result = SimpleArrayItem.MakeSimpleArrayItem(arguments[0].Iterate());
                    result.RequestNotification(this);
                    numberOfCalls++;
                    return result;
                }
            }

            public override IFold GetFold(IXPathContext context, params ISequence[] additionalArguments)
            {
                return new AnonymousIFold(this);
            }

            private sealed class AnonymousIFold : IFold
            {

                readonly IList<IGroundedValue> members = new List<IGroundedValue>();
                public AnonymousIFold(ArrayFromSequence parent)
                {
                }
                public void ProcessItem(IItem item)
                {
                    members.Add(item);
                }

                public bool IsFinished()
                {
                    return false;
                }

                public ISequence Result()
                {
                    return new SimpleArrayItem(members);
                }
            }
        }
    }
}
