////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// The XPath 2.0 index-of() function, with the collation already known
    /// </summary>
    internal sealed class IndexOf : CollatingFunctionFixed
    {
        public override IntegerValue[] IntegerBounds => new IntegerValue[]
            {
                Int64Value.PLUS_ONE,
                Expression.MAX_SEQUENCE_LENGTH
            };

        public override string StreamerName => "IndexOf";

        public override void SupplyTypeInformation(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType, Expression[] arguments)
        {
            ItemType type0 = arguments[0].GetItemType();
            ItemType type1 = arguments[1].GetItemType();
            if (type0 is IAtomicType && type1 is IAtomicType)
            {
                PreAllocateComparer((IAtomicType)type0, (IAtomicType)type1, visitor.StaticContext);
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IAtomicComparer comparer = GetAtomicComparer(context);
            ISequenceIterator seq = arguments[0].Iterate();
            AtomicValue val = (AtomicValue)arguments[1].Head();
            BuiltInAtomicType searchType = val.PrimitiveType;
            return SequenceTool.ToLazySequence(new IndexIterator(seq, searchType, val, comparer));
        }

        private sealed class IndexIterator : ISequenceIterator
        {
            private int index;
            private readonly ISequenceIterator @base;
            private readonly BuiltInAtomicType searchType;
            private readonly IAtomicComparer comparer;
            private readonly AtomicValue key;
            public IndexIterator(ISequenceIterator @base, BuiltInAtomicType searchType, AtomicValue key, IAtomicComparer comparer)
            {
                this.@base = @base;
                this.searchType = searchType;
                this.key = key;
                this.comparer = comparer;
            }

            public void Dispose()
            {
                @base.Dispose();
            }

            public Int64Value Next()
            {
                try
                {
                    AtomicValue baseItem;
                    while ((baseItem = (AtomicValue)@base.Next()) != null)
                    {
                        index++;
                        if ((index & 1023) == 0)
                        {
                            Core.Controller.CheckActiveTimeout();
                        }

                        if (Types.Type.IsGuaranteedComparable(searchType, baseItem.PrimitiveType, false) && comparer.ComparesEqual(baseItem, key))
                        {
                            return new Int64Value(index);
                        }
                    }

                    return null;
                }
                catch (NoDynamicContextException e)
                {
                    throw new UncheckedXPathException(e);
                }
            }
            IItem ISequenceIterator.Next() => Next();
        }
    }
}

