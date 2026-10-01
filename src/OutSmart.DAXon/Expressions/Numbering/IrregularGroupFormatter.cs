////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Numbering
{
    internal sealed class IrregularGroupFormatter : NumericGroupFormatter
    {
        private readonly IntSet groupingPositions;
        private readonly IList<int> separators;

        public override string Separator
        {
            get
            {
                if (separators.Count == 0)
                {
                    return null;
                }
                else
                {
                    int sep = separators[separators.Count - 1];
                    StringBuilder fsb = new StringBuilder(16);
                    fsb.AppendCodePoint(sep);
                    return fsb.ToString();
                }
            }
        }
        public IrregularGroupFormatter(IntSet groupingPositions, IList<int> sep, UnicodeString adjustedPicture)
        {
            this.groupingPositions = groupingPositions;
            separators = sep;
            this.adjustedPicture = adjustedPicture;
        }

        public override string Format(string value)
        {
            StringValue @in = new StringValue(value);
            int l, m = 0;
            for (l = 0; l < @in.Length(); l++)
            {
                if (groupingPositions.Contains(l))
                {
                    m++;
                }
            }

            int[] @out = new int[@in.Length32() + m];
            int j = 0;
            int k = @out.Length - 1;
            for (int i = @in.Length32() - 1; i >= 0; i--)
            {
                @out[k--] = @in.Content.CodePointAt(i);
                if ((i > 0) && groupingPositions.Contains(@in.Length32() - i))
                {
                    @out[k--] = separators[j++];
                }
            }

            return StringTool.FromCodePoints(@out, @out.Length).ToString();
        }
    }
}