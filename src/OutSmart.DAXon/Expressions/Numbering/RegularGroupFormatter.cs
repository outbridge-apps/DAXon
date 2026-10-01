////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;using OutSmart.DAXon.Functions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
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
                UnicodeString valueEx = StringView.Tidy(value);
                StringBuilder temp = new StringBuilder(16);
                for (int i = valueEx.Length32() - 1, j = 0; i >= 0; i--, j++)
                {
                    if (j != 0 && (j % groupSize) == 0)
                    {
                        temp.Insert(0, groupSeparator);
                    }

                    StringTool.PrependWideChar(temp, valueEx.CodePointAt(i));
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