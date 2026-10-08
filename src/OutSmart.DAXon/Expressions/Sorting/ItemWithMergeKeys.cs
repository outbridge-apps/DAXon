////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class ItemWithMergeKeys
    {
        internal IItem baseItem;
        public IList<AtomicValue> sortKeyValues;
        internal string sourceName;
        public ItemWithMergeKeys(IItem bItem, SortKeyDefinitionList sKeys, string name, IXPathContext context)
        {
            baseItem = bItem;
            sourceName = name;
            sortKeyValues = new List<AtomicValue>(sKeys.Count);
            foreach (SortKeyDefinition sKey in sKeys)
            {
                sortKeyValues.Add((AtomicValue)sKey.SortKeyEvaluator(context));
            }
        }
    }
}