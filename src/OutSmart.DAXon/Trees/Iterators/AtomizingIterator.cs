////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Trees.Iterators
{
    internal sealed class AtomizingIterator : ISequenceIterator
    {
        private readonly ISequenceIterator @base;
        private IAtomicSequence currentValue;
        private int currentValuePosition = 1;
        private int currentValueSize = 1;
        private readonly RoleDiagnostic roleDiagnostic;
        public AtomizingIterator(ISequenceIterator @base)
        {
            this.@base = @base;
        }

        public AtomicValue Next()
        {
            while (true)
            {
                if (currentValue != null)
                {
                    if (currentValuePosition < currentValueSize)
                    {
                        return currentValue.ItemAt(currentValuePosition++);
                    }
                    else
                    {
                        currentValue = null;
                    }
                }

                IItem nextSource = @base.Next();
                if (nextSource != null)
                {
                    try
                    {
                        IAtomicSequence v = nextSource.Atomize();
                        if (v is AtomicValue)
                        {
                            return (AtomicValue)v;
                        }
                        else
                        {
                            currentValue = v;
                            currentValuePosition = 0;
                            currentValueSize = currentValue.GetLength(); // now go round the loop to get the first item from the atomized value
                        }
                    }
                    catch (XPathException e)
                    {
                        if (roleDiagnostic == null)
                        {
                            throw new UncheckedXPathException(e);
                        }
                        else
                        {
                            string message = e.Message + ". Failed while atomizing the " + roleDiagnostic.GetMessage();
                            throw new UncheckedXPathException(e.WithMessage(message));
                        }
                    }
                }
                else
                {
                    currentValue = null;
                    return null;
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
