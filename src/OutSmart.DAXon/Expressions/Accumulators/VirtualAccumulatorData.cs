////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Wrappers;
namespace OutSmart.DAXon.Expressions.Accumulators
{
    internal sealed class VirtualAccumulatorData : IIAccumulatorData
    {
        private readonly IIAccumulatorData realData;
        public VirtualAccumulatorData(IIAccumulatorData realData)
        {
            this.realData = realData;
        }

        public Accumulator GetAccumulator()
        {
            return realData.GetAccumulator();
        }

        public ISequence GetValue(NodeInfo node, bool postDescent)
        {
            NodeInfo original = ((VirtualCopy)node).OriginalNode;
            return realData.GetValue(original, postDescent);
        }
    }
}