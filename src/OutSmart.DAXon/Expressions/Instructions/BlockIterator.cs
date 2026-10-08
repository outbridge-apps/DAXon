////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Iterate over the instructions in a sequence of instructions (or an XPath comma expression),
    /// concatenating the result of each instruction into a single combined sequence.
    /// </summary>
    internal sealed class BlockIterator : AbstractBlockIterator
    {
        private readonly Operand[] operanda;

        public BlockIterator(Operand[] operanda, IXPathContext context) : base(operanda.Length, context)
        {
            this.operanda = operanda;
        }

        public override ISequenceIterator GetNthChildIterator(int n)
        {
            return operanda[n].GetChildExpression().Iterate(context);
        }
    }
}
