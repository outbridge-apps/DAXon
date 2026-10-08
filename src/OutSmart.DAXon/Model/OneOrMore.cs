////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    internal sealed class OneOrMore<T> : ZeroOrMore<T>
    {

        public OneOrMore(IList<T> content) : base(content)
        {
            if (content.Count == 0)
            {
                throw new ArgumentException();
            }
        }

        public static OneOrMore<IItem> MakeOneOrMore(ISequence sequence)
        {
            IList<IItem> content = new List<IItem>();

            SequenceTool.Supply(sequence.Iterate(), (it) => content.Add(it));
            if (content.Count == 0)
            {
                throw new ArgumentException();
            }

            return new OneOrMore<IItem>(content);
        }
    }
}