////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Regex;
using OutSmart.DAXon.Text;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// Implementation of the xs:gDay data type
    /// </summary>
    internal sealed class GDayValue : GDateValue
    {
        private static readonly OutSmart.DAXon.Internal.Regex.Pattern regex = OutSmart.DAXon.Internal.Regex.Pattern.Compile("---([0-9][0-9])(Z|[+-][0-9][0-9]:[0-9][0-9])?");

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.G_DAY;

        public override UnicodeString PrimitiveStringValue
        {
            get
            {
                UnicodeBuilder sb = new UnicodeBuilder(16);
                sb.AppendLatin("---");
                AppendTwoDigits(sb, day);
                if (HasTimezone())
                {
                    AppendTimezone(sb);
                }

                return sb.ToUnicodeString();
            }
        }
        protected GDayValue(MutableGDateValue m) : base(m)
        {
        }

        public GDayValue(byte day, int tz) : this(day, tz, BuiltInAtomicType.G_DAY)
        {
        }

        public GDayValue(byte day, int tz, IAtomicType type) : this(new MutableGDateValue(2000, 1, day, false, tz, type))
        {
        }

        public static IConversionResult MakeGDayValue(UnicodeString value)
        {
            UnicodeString trimmed = Whitespace.Trim(value);
            Matcher m = regex.Matcher(trimmed.ToString());
            if (!m.Matches())
            {
                return new ValidationFailure("Cannot convert '" + value + "' to a gDay");
            }

            MutableGDateValue g = new MutableGDateValue();
            string @base = m.Group(1);
            string tz = m.Group(2);
            string date = "2000-01-" + @base + (tz == null ? "" : tz);
            g.typeLabel = BuiltInAtomicType.G_DAY;
            SetLexicalValue(g, BMPString.Of(date), true);
            return g.error == null ? new GDayValue(g) : g.error;
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            MutableGDateValue m = MakeMutableCopy();
            m.typeLabel = typeLabel;
            return new GDayValue(m);
        }

        public override CalendarValue Add(DurationValue duration)
        {
            throw new XPathException("Cannot add a duration to an xs:gDay", "XPTY0004").AsTypeError();
        }

        public override CalendarValue AdjustTimezone(int tz)
        {
            DateTimeValue dt = (DateTimeValue)ToDateTime().AdjustTimezone(tz);
            return new GDayValue(dt.Day, dt.TimezoneInMinutes);
        }
    }
}
