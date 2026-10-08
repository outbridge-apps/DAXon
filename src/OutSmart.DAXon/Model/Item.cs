////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Model
{
    public interface IItem : IGroundedValue
    {
        Genre GetGenre();
        IItem Head();



        UnicodeString UnicodeStringValue { get; }
        string GetStringValue();



        IAtomicSequence Atomize();
        string ToShortString();



        IItem ItemAt(int n);



        IGroundedValue Subsequence(int start, int length);










        int GetLength();



        SingletonIterator Iterate();



        IGroundedValue Reduce();



        bool IsStreamed();


    }
}
