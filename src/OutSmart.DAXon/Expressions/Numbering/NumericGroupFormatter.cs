////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
namespace OutSmart.DAXon.Expressions.Numbering
{
    public abstract class NumericGroupFormatter
    {
        protected UnicodeString adjustedPicture;
        public virtual UnicodeString AdjustedPicture => adjustedPicture;
        public abstract string Separator { get; }

        public abstract string Format(string value);
    }
}