////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions.Accumulators
{
    internal sealed class PathMappedAccumulatorData : IIAccumulatorData
    {
        public PathMappedAccumulatorData(object a, object b) { }
        public Accumulator GetAccumulator() => throw new NotImplementedException("STUB: PathMappedAccumulatorData.GetAccumulator not ported (excluded stub)");
        public ISequence GetValue(NodeInfo node, bool postDescent) => throw new NotImplementedException("STUB: PathMappedAccumulatorData.GetValue not ported (excluded stub)");
    }
}
