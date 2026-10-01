////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Expressions.Accumulators
{
    /// <summary>
    /// Represents the values of an accumulator whose evaluation has failed. The error is retained
    /// until referenced using accumulator-before() or accumulator-after().
    /// </summary>
    internal sealed class FailedAccumulatorData : IIAccumulatorData
    {
        private readonly Accumulator acc;
        private readonly XPathException error;

        public FailedAccumulatorData(Accumulator acc, XPathException error)
        {
            this.acc = acc;
            this.error = error;
        }

        public Accumulator GetAccumulator()
        {
            return acc;
        }

        public ISequence GetValue(NodeInfo node, bool postDescent)
        {
            throw error;
        }
    }
}
