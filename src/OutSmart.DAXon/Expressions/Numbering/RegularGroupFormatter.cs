////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Numbering
{
    internal sealed class RegularGroupFormatter : NumericGroupFormatter
    {
        private readonly int groupSize;
        private readonly string groupSeparator;

        public override string Separator => groupSeparator;
        public RegularGroupFormatter(int grpSize, string grpSep, UnicodeString adjustedPicture)
        {
            groupSize = grpSize;
            groupSeparator = grpSep;
            this.adjustedPicture = adjustedPicture;
        }

        public override string Format(string value)
        {
            if (groupSize > 0 && groupSeparator.Length > 0)
            {
                // Left to right, a separator before each digit that starts a group: an insert at the front per digit made
                // grouping a 200 000-digit number quadratic (33 s under a 3 s limit).
                UnicodeString valueEx = StringView.Tidy(value);
                int n = valueEx.Length32();
                StringBuilder temp = new StringBuilder(n + n / groupSize * groupSeparator.Length + 1);
                for (int i = 0; i < n; i++)
                {
                    if (i != 0 && (n - i) % groupSize == 0)
                    {
                        temp.Append(groupSeparator);
                    }

                    temp.AppendCodePoint(valueEx.CodePointAt(i));
                    Core.Controller.CheckActiveTimeoutEvery64K(i);
                }

                return temp.ToString();
            }
            else
            {
                return value.ToString();
            }
        }
    }
}