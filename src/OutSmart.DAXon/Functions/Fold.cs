////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /* Interface implemented by operations that compute the value of an expression using
 * a "fold" operation: that @is, by applying a function to each item in the input sequence
 * in turn, maintaining working data as the items are processed, and finally computing
 * the final result of the expression from the value of the working data.
 */
    internal interface IFold
    {
        void ProcessItem(IItem item);
        bool IsFinished();
        ISequence Result();
    }
}