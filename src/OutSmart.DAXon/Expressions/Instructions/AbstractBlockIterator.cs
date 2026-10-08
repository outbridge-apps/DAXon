////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal abstract class AbstractBlockIterator : ISequenceIterator
    {
        protected int size;
        protected int currentOperand;
        protected ISequenceIterator currentIter;
        protected IXPathContext context;
        public AbstractBlockIterator()
        {
        }

        public AbstractBlockIterator(int size, IXPathContext context)
        {
            this.size = size;
            this.context = context;
            this.currentOperand = 0;
        }

        public virtual void Init(int size, IXPathContext context)
        {
            this.size = size;
            this.context = context;
            this.currentOperand = 0;
        }

        public virtual IItem Next()
        {
            if (currentOperand < 0)
            {
                return null;
            }

            while (true)
            {
                if (currentIter == null)
                {
                    try
                    {
                        currentIter = GetNthChildIterator(currentOperand++);
                    }
                    catch (XPathException e)
                    {
                        throw new UncheckedXPathException(e);
                    }
                }

                IItem current = currentIter.Next();
                if (current != null)
                {
                    return current;
                }

                currentIter = null;
                if (currentOperand >= size)
                {
                    currentOperand = -1;
                    return null;
                }
            }
        }

        public abstract ISequenceIterator GetNthChildIterator(int n);
        public virtual void Dispose()
        {
            if (currentIter != null)
            {
                currentIter.Dispose();
            }

            currentOperand = -1;
        }
    }
}