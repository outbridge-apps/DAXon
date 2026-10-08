////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Text;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    public interface IGroundedValue : ISequence
    {
        ISequenceIterator Iterate();
        IItem ItemAt(int n);
        IItem Head();
        IGroundedValue Subsequence(int start, int length);
        int GetLength();
        bool EffectiveBooleanValue();



        UnicodeString UnicodeStringValue { get; }
        string GetStringValue();
        IGroundedValue Reduce();



        IGroundedValue Materialize();



        string ToShortString();



        IEnumerable<IItem> AsIterable();



        bool ContainsNode(NodeInfo sought);



        IGroundedValue Concatenate(params IGroundedValue[] others);









    }
}
