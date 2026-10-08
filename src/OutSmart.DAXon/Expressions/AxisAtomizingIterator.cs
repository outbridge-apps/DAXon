////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class AxisAtomizingIterator : ISequenceIterator
    {
        private readonly IAtomizedValueIterator @base;
        private IAtomicSequence results;
        private int atomicPosition;
        public AxisAtomizingIterator(IAtomizedValueIterator @base)
        {
            this.@base = @base;
        }

        public AtomicValue Next()
        {
            while (true)
            {
                if (results != null)
                {
                    if (atomicPosition < results.GetLength())
                    {
                        return results.ItemAt(atomicPosition++);
                    }
                    else
                    {
                        results = null;
                        continue;
                    }
                }

                try
                {
                    IAtomicSequence atomized = @base.NextAtomizedValue();
                    if (atomized == null)
                    {
                        results = null;
                        return null;
                    }

                    if (atomized is AtomicValue)
                    {

                        // common case (the atomized value of the node is a single atomic value)
                        results = null;
                        return (AtomicValue)atomized;
                    }
                    else
                    {
                        results = atomized;
                        atomicPosition = 0; // continue
                    }
                }
                catch (XPathException e)
                {
                    throw new UncheckedXPathException(e);
                }
            }
        }

        public void Dispose()
        {
            @base.Dispose();
        }
        IItem ISequenceIterator.Next() => Next();
    }
}
