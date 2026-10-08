////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// The XPath 2.0 remove() function
    /// </summary>
    internal sealed class Remove : SystemFunction
    {

        //
        public override string StreamerName => "Remove";
        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            if (Literal.IsAtomic(arguments[1]))
            {
                ISequence index = ((Literal)arguments[1]).GroundedValue;
                if (index is IntegerValue)
                {
                    try
                    {
                        long value = ((IntegerValue)index).LongValue();
                        if (value <= 0)
                        {
                            return arguments[0];
                        }
                        else if (value == 1)
                        {
                            return new TailExpression(arguments[0], 2);
                        }
                    }
                    catch (XPathException)
                    {
                    }
                }
            }

            return base.MakeFunctionCall(arguments);
        }

        //
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IntSet removePositions;
            if (arguments[1] is AtomicValue)
            {
                NumericValue n = (NumericValue)arguments[1].Head();
                int pos = (int)n.LongValue();
                if (pos < 1)
                {
                    return arguments[0];
                }

                removePositions = new IntSingletonSet(pos);
            }
            else
            {
                IntHashSet positions = new IntHashSet();
                NumericValue n;
                ISequenceIterator iter = arguments[1].Iterate();
                while ((n = (NumericValue)iter.Next()) != null)
                {
                    int pos = (int)n.LongValue();
                    if (pos >= 1)
                    {
                        positions.Add(pos);
                    }
                }

                if (positions.IsEmpty())
                {
                    return arguments[0];
                }

                removePositions = positions;
            }

            return SequenceTool.ToLazySequence(new RemoveIterator(arguments[0].Iterate(), removePositions));
        }

        //
        internal sealed class RemoveIterator : ISequenceIterator, ILastPositionFinder
        {
            readonly ISequenceIterator @base;
            readonly IntSet removePositions;
            int basePosition;
            IItem current;
            public RemoveIterator(ISequenceIterator @base, IntSet removePosition)
            {
                this.@base = @base;
                this.removePositions = removePosition;
            }

            public IItem Next()
            {
                current = @base.Next();
                basePosition++;
                while (current != null && removePositions.Contains(basePosition))
                {
                    current = @base.Next();
                    basePosition++;
                }

                return current;
            }

            public void Dispose()
            {
                @base.Dispose();
            }

            public bool SupportsGetLength()
            {
                return SequenceTool.SupportsGetLength(@base);
            }

            public int GetLength()
            {
                int x = SequenceTool.GetLength(@base);
                int result = x;
                IIntIterator iter = removePositions.IIterator();
                while (iter.MoveNext())
                {
                    int i = iter.Current;
                    if (i >= 1 && i <= x)
                    {
                        result--;
                    }
                }

                return result;
            }
        }
    }
}
