////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
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
    internal sealed class DescendingComparer : IAtomicComparer
    {
        private readonly IAtomicComparer baseComparer;

        public IAtomicComparer BaseComparer => baseComparer;

        public IStringCollator Collator => baseComparer.Collator;
        public DescendingComparer(IAtomicComparer @base)
        {
            baseComparer = @base;
        }

        public IAtomicComparer ProvideContext(IXPathContext context)
        {
            IAtomicComparer newBase = baseComparer.ProvideContext(context);
            if (newBase != baseComparer)
            {
                return new DescendingComparer(newBase);
            }
            else
            {
                return this;
            }
        }

        public int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {
            return 0 - baseComparer.CompareAtomicValues(a, b);
        }

        public bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return baseComparer.ComparesEqual(a, b);
        }

        public string Save()
        {
            return "DESC|" + baseComparer.Save();
        }
    }
}