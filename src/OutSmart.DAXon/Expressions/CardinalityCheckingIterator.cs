////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class CardinalityCheckingIterator : ISequenceIterator
    {
        private readonly ISequenceIterator @base;
        private readonly ILocation locator;
        private IItem first = null;
        private IItem second = null;
        private int position = 0;
        public CardinalityCheckingIterator(ISequenceIterator @base, int requiredCardinality, Func<RoleDiagnostic> roleSupplier, ILocation locator)
        {
            this.@base = @base;
            this.locator = locator;
            try
            {
                first = @base.Next();
                if (first == null)
                {
                    RoleDiagnostic role = roleSupplier();
                    if (!Cardinality.AllowsZero(requiredCardinality))
                    {
                        TypeError("An empty sequence is not allowed as the " + role.GetMessage(), role.ErrorCode);
                    }
                }
                else
                {
                    if (requiredCardinality == StaticProperty.EMPTY)
                    {
                        RoleDiagnostic role = roleSupplier();
                        TypeError("The only value allowed for the " + role.GetMessage() + " is an empty sequence", role.ErrorCode);
                    }

                    second = @base.Next();
                    if (second != null && !Cardinality.AllowsMany(requiredCardinality))
                    {
                        RoleDiagnostic role = roleSupplier();
                        TypeError("A sequence of more than one item {" + CardinalityChecker.DepictSequenceStart(new TwoItemIterator(first, second), 2) + "} is not allowed as the " + role.GetMessage(), role.ErrorCode);
                    }
                }
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        public IItem Next()
        {
            if (position < 2)
            {
                if (position == 0)
                {
                    IItem current = first;
                    position = first == null ? -1 : 1;
                    return current;
                }
                else if (position == 1)
                {
                    IItem current = second;
                    position = second == null ? -1 : 2;
                    return current;
                }
                else
                {

                    // position == -1
                    return null;
                }
            }

            IItem nextBase = @base.Next();
            if (nextBase == null)
            {
                position = -1;
            }
            else
            {
                position++;
            }

            return nextBase;
        }

        public void Dispose()
        {
            @base.Dispose();
        }

        private void TypeError(string message, string errorCode)
        {
            XPathException e = new XPathException(message, errorCode, locator);
            e.SetIsTypeError(!errorCode.StartsWith("FORG", StringComparison.Ordinal));
            throw e;
        }
    }
}