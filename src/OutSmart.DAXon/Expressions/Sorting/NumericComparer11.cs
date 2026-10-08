////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class NumericComparer11 : NumericComparer
    {
        private static readonly NumericComparer11 THE_INSTANCE = new NumericComparer11();

        protected NumericComparer11()
        {
            converter = StringToDouble11.GetInstance();
        }
        public static NumericComparer GetInstance()
        {
            return THE_INSTANCE;
        }

        public override string Save()
        {
            return "NC11";
        }
    }
}