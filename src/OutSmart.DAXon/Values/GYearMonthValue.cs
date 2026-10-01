////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Regex;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// Implementation of the xs:gYearMonth data type
    /// </summary>
    internal sealed class GYearMonthValue : GDateValue
    {
        private static readonly OutSmart.DAXon.Internal.Regex.Pattern regex = OutSmart.DAXon.Internal.Regex.Pattern.Compile("(-?[0-9]+-[0-9][0-9])(Z|[+-][0-9][0-9]:[0-9][0-9])?");

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.G_YEAR_MONTH;

        public override UnicodeString PrimitiveStringValue
        {
            get
            {
                UnicodeBuilder sb = new UnicodeBuilder(16);
                int yr = year;
                if (year <= 0)
                {
                    yr = -yr + (hasNoYearZero ? 1 : 0); // no year zero in lexical space for XSD 1.0
                    if (yr != 0)
                    {
                        sb.Append('-');
                    }
                }

                AppendString(sb, yr, (yr > 9999 ? (yr + "").Length : 4));
                sb.Append('-');
                AppendTwoDigits(sb, month);
                if (HasTimezone())
                {
                    AppendTimezone(sb);
                }

                return sb.ToUnicodeString();
            }
        }
        private GYearMonthValue(MutableGDateValue m) : base(m)
        {
        }

        public GYearMonthValue(int year, byte month, int tz, bool xsd10) : this(new MutableGDateValue(year, month, 1, xsd10, tz, BuiltInAtomicType.G_YEAR_MONTH))
        {
        }

        public static IConversionResult MakeGYearMonthValue(UnicodeString value, ConversionRules rules)
        {
            UnicodeString trimmed = Whitespace.Trim(value);
            Matcher m = regex.Matcher(trimmed.ToString());
            if (!m.Matches())
            {
                return new ValidationFailure("Cannot convert '" + value + "' to a gYearMonth");
            }

            MutableGDateValue g = new MutableGDateValue();
            string @base = m.Group(1);
            string tz = m.Group(2);
            string date = @base + "-01" + (tz == null ? "" : tz);
            g.typeLabel = BuiltInAtomicType.G_YEAR_MONTH;
            SetLexicalValue(g, BMPString.Of(date), rules.IsAllowYearZero());
            return g.error == null ? new GYearMonthValue(g) : g.error;
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            MutableGDateValue m = MakeMutableCopy();
            m.typeLabel = typeLabel;
            return new GYearMonthValue(m);
        }

        public override CalendarValue Add(DurationValue duration)
        {
            throw new XPathException("Cannot add a duration to an xs:gYearMonth", "XPTY0004").AsTypeError();
        }

        public override CalendarValue AdjustTimezone(int tz)
        {
            DateTimeValue dt = (DateTimeValue)ToDateTime().AdjustTimezone(tz);
            return new GYearMonthValue(dt.Year, dt.Month, dt.TimezoneInMinutes, hasNoYearZero);
        }
    }
}