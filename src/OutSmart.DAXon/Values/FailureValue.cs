////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Values
{
    // A memoized FAILED evaluation (global variables/params keep it so a re-read reproduces the
    // original error). The old shell dropped the error in every constructor and threw NIE from
    // half its members while quietly answering "empty" from the other half.
    internal sealed class FailureValue : IGroundedValue
    {
        private readonly XPathException error;
        public FailureValue(XPathException err) { error = err ?? new XPathException("Evaluation failed"); }

        // Every access re-raises the stored error, as upstream: this value IS the failure.
        private UncheckedXPathException Raise() => new UncheckedXPathException(error);

        public UnicodeString UnicodeStringValue => throw Raise();
        public IItem Head() => throw Raise();
        public ISequenceIterator Iterate() => throw Raise();
        public IItem ItemAt(int n) => throw Raise();
        public int GetLength() => throw Raise();
        public string GetStringValue() => throw Raise();
        public bool EffectiveBooleanValue() => throw Raise();
        public IGroundedValue Materialize() => this;
        public IGroundedValue Reduce() => this;
        public IGroundedValue Subsequence(int start, int length) => this;
        public string ToShortString() => "fail(" + (error.Message ?? "") + ")";
        public IEnumerable<IItem> AsIterable() => throw Raise();
        public bool ContainsNode(NodeInfo node) => throw Raise();
        public IGroundedValue Concatenate(params IGroundedValue[] others) => this;
        public ISequence MakeRepeatable() => this;
    }
}
