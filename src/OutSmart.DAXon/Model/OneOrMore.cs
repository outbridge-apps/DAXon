////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
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