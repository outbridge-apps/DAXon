////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// The XPath 2.0 insert-before() function
    /// </summary>
    internal sealed class InsertBefore : SystemFunction
    {

        public override string StreamerName => "InsertBefore";
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NumericValue n = (NumericValue)arguments[1].Head();
            int pos = (int)n.LongValue();
            return SequenceTool.ToLazySequence(new InsertIterator(arguments[0].Iterate(), arguments[2].Iterate(), pos));
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            return new AnonymousSystemFunctionCall(this, arguments);
        }

        private sealed class AnonymousSystemFunctionCall : SystemFunctionCall
        {

            public AnonymousSystemFunctionCall(InsertBefore parent, Expression[] arguments) : base(parent, arguments)
            {
            }
            public override ItemType GetItemType()
            {
                return Types.Type.GetCommonSuperType(GetArg(0).GetItemType(), GetArg(2).GetItemType());
            }
        }

        internal sealed class InsertIterator : ISequenceIterator
        {
            private readonly ISequenceIterator @base;
            private readonly ISequenceIterator insert;
            private readonly int insertPosition;
            private int position;
            private bool inserting;
            public InsertIterator(ISequenceIterator @base, ISequenceIterator insert, int insertPosition)
            {
                this.@base = @base;
                this.insert = insert;
                this.insertPosition = Math.Max(insertPosition, 1);
                this.inserting = insertPosition == 1;
            }

            public IItem Next()
            {
                IItem nextItem;
                if (inserting)
                {
                    nextItem = insert.Next();
                    if (nextItem == null)
                    {
                        inserting = false;
                        nextItem = @base.Next();
                    }
                }
                else
                {
                    if (position == insertPosition - 1)
                    {
                        nextItem = insert.Next();
                        if (nextItem == null)
                        {
                            nextItem = @base.Next();
                        }
                        else
                        {
                            inserting = true;
                        }
                    }
                    else
                    {
                        nextItem = @base.Next();
                        if (nextItem == null && position < insertPosition - 1)
                        {
                            inserting = true;
                            nextItem = insert.Next();
                        }
                    }
                }

                if (nextItem == null)
                {
                    position = -1;
                    return null;
                }
                else
                {
                    position++;
                    return nextItem;
                }
            }

            public void Dispose()
            {
                @base.Dispose();
                insert.Dispose();
            }
        }
    }
}