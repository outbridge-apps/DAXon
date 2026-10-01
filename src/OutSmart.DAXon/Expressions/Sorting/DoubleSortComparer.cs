////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class DoubleSortComparer : IAtomicComparer
    {
        private static readonly DoubleSortComparer THE_INSTANCE = new DoubleSortComparer();

        public IStringCollator Collator => null;

        private DoubleSortComparer()
        {
        }
        public static DoubleSortComparer GetInstance()
        {
            return THE_INSTANCE;
        }

        public IAtomicComparer ProvideContext(IXPathContext context)
        {
            return this;
        }

        public int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {
            if (a == null)
            {
                if (b == null)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            else if (b == null)
            {
                return +1;
            }

            NumericValue an = (NumericValue)a;
            NumericValue bn = (NumericValue)b;
            if (an.IsNaN())
            {
                return bn.IsNaN() ? 0 : -1;
            }
            else if (bn.IsNaN())
            {
                return +1;
            }

            return an.CompareTo(bn);
        }

        /// <summary>
        /// Test whether two values compare equal. Note that for this comparer, NaN is considered equal to itself
        /// </summary>
        public bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return CompareAtomicValues(a, b) == 0;
        }

        /// <summary>
        /// Test whether two values compare equal. Note that for this comparer, NaN is considered equal to itself
        /// </summary>
        public string Save()
        {
            return "DblSC";
        }
    }
}