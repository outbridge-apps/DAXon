////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;
using System.Text;

namespace OutSmart.DAXon.Functions
{

    // fn:generate-id#1: Call-only impl — the full GenerateId_1 port would drag the StringElaborator cluster.
    internal sealed class GenerateIdFn1 : ScalarSystemFunction
    {
        public GenerateIdFn1() { }
        public static Func<GenerateIdFn1> New() => () => new GenerateIdFn1();

        // Ids are short ASCII; GenerateId implementations only append (no reentrancy), so one
        // per-thread builder serves every call instead of a StringBuilder+char[] pair per node.
        [ThreadStatic]
        private static StringBuilder tlsBuffer;

        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            StringBuilder buffer = tlsBuffer;
            if (buffer == null || buffer.Capacity > 256)
            {
                tlsBuffer = buffer = new StringBuilder(24);
            }

            buffer.Length = 0;
            ((NodeInfo)arg).GenerateId(buffer);
            return new StringValue(buffer.ToString());
        }
        public override ISequence ResultWhenEmpty()
        {
            return StringValue.EMPTY_STRING;
        }
    }
}
