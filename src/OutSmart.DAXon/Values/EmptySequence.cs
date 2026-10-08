////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using System.Linq;

// EmptyIterator stub removed -- real source at poc/output/full/EmptyIterator.cs

namespace OutSmart.DAXon.Values
{

    internal sealed class EmptySequence : IGroundedValue
    {
        private static readonly EmptySequence _instance = new EmptySequence();
        public UnicodeString UnicodeStringValue => EmptyUnicodeString.GetInstance();
        public static EmptySequence GetInstance() => _instance;
        public int GetLength() => 0;
        // IGroundedValue surface
        public IItem Head() => null;
        // 2026-06-03: was => null (NRE'd Literal.IsInstance foreach + any empty-seq iteration). Empty sequence iterates as EmptyIterator / an empty enumerable.
        public ISequenceIterator Iterate() => EmptyIterator.GetInstance();
        public IGroundedValue Materialize() => this;
        public IGroundedValue Subsequence(int start, int length) => this;
        // An empty sequence has no item at any position (upstream EmptySequence.itemAt returns null).
        // Was a throwing stub -> NRE'd SubscriptExpression.GetItemAt for `EXPR[n]` whenever EXPR was empty
        // (e.g. //x[2]/y over a doc where the [2] step is empty; app-FunctxFn base-uri/data).
        public IItem ItemAt(int n) => null;
        public IGroundedValue Reduce() => this;
        public string GetStringValue() => "";
        public bool EffectiveBooleanValue() => false;
        public string ToShortString() => "()";
        public IEnumerable<IItem> AsIterable() => Enumerable.Empty<IItem>();
        public bool ContainsNode(NodeInfo node) => false;
        public IGroundedValue Concatenate(params IGroundedValue[] tail) => this;
        public ISequence MakeRepeatable() => this;
    }
}
