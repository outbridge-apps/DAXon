////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;

namespace OutSmart.DAXon.Serialization
{
    // Runtime: the real SequenceNormalizerWithSpaceSeparator.cs (namespace OutSmart.DAXon.Events) is excluded; this stub
    // stands in (namespace OutSmart.DAXon.Serialization, where SerializationProperties constructs it). It previously derived
    // straight from SequenceReceiver, FLATTENING the real ': SequenceNormalizer : ProxyReceiver' chain -- so its
    // Dispose()/EndDocument() resolved to the SequenceReceiver NIE base instead of SequenceNormalizer's real overrides
    // (the receiver chain's final close at end of XsltController.ApplyTemplates). Reparent it onto the real
    // OutSmart.DAXon.Events.SequenceNormalizer and carry the two real overrides (Append -> Decompose, error code SENR0001),
    // exactly as the excluded file does. The bogus implicit operator (=> null) is dropped: it IS-A SequenceNormalizer now.
    internal sealed class SequenceNormalizerWithSpaceSeparator : SequenceNormalizer
    {
        protected override string ErrorCodeForDecomposingFunctionItems => "SENR0001";
        public SequenceNormalizerWithSpaceSeparator(IReceiver next) : base(next) { }
        public override void Append(IItem item, ILocation l, int p) { Decompose(item, l, p); }
    }
}
