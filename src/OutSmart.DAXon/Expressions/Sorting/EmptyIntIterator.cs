////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Expressions.Sorting
{
    /// <summary>
    /// An iterator over a zero-length sequence of integers
    /// </summary>
    internal sealed class EmptyIntIterator : AbstractIntIterator
    {
        private static readonly EmptyIntIterator THE_INSTANCE = new EmptyIntIterator();

        private EmptyIntIterator()
        {
        }
        public static EmptyIntIterator GetInstance()
        {
            return THE_INSTANCE;
        }

        public override bool HasNext()
        {
            return false;
        }

        public override int Next()
        {
            return 0;
        }
    }
}