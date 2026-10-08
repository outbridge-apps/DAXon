////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Numbering
{
    internal sealed class NamedTimeZone
    {
        static readonly HashSet<string> knownTimeZones = new HashSet<string>();
        static readonly Dictionary<string, IList<string>> idForCountry = new Dictionary<string, IList<string>>(50);
        static readonly IList<string> worldTimeZones = new List<string>(20);

        static NamedTimeZone()
        {

            knownTimeZones.Add("UTC");
            foreach (TimeZoneInfo tz in TimeZoneInfo.GetSystemTimeZones())
            {
                knownTimeZones.Add(tz.Id);
            }
            // The table starts with countries that use multiple timezones, then proceeds in alphabetical order
            Tz("us", "America/New_York", true);
            Tz("us", "America/Chicago", true);
            Tz("us", "America/Denver", true);
            Tz("us", "America/Los_Angeles", true);
            Tz("us", "America/Anchorage", true);
            Tz("us", "America/Halifax", true);
            Tz("us", "Pacific/Honolulu", true);
            Tz("ca", "Canada/Pacific");
            Tz("ca", "Canada/Mountain");
            Tz("ca", "Canada/Central");
            Tz("ca", "Canada/Eastern");
            Tz("ca", "Canada/Atlantic");
            Tz("au", "Australia/Sydney", true);
            Tz("au", "Australia/Darwin", true);
            Tz("au", "Australia/Perth", true);
            Tz("ru", "Europe/Moscow", true);
            Tz("ru", "Europe/Samara");
            Tz("ru", "Asia/Yekaterinburg");
            Tz("ru", "Asia/Novosibirsk");
            Tz("ru", "Asia/Krasnoyarsk");
            Tz("ru", "Asia/Irkutsk");
            Tz("ru", "Asia/Chita");
            Tz("ru", "Asia/Vladivostok");
            Tz("an", "Europe/Andorra");
            Tz("ae", "Asia/Abu_Dhabi");
            Tz("af", "Asia/Kabul");
            Tz("al", "Europe/Tirana");
            Tz("am", "Asia/Yerevan");
            Tz("ao", "Africa/Luanda");
            Tz("ar", "America/Buenos_Aires");
            Tz("as", "Pacific/Samoa");
            Tz("at", "Europe/Vienna");
            Tz("aw", "America/Aruba");
            Tz("az", "Asia/Baku");
            Tz("ba", "Europe/Sarajevo");
            Tz("bb", "America/Barbados");
            Tz("bd", "Asia/Dhaka");
            Tz("be", "Europe/Brussels", true);
            Tz("bf", "Africa/Ouagadougou");
            Tz("bg", "Europe/Sofia");
            Tz("bh", "Asia/Bahrain");
            Tz("bi", "Africa/Bujumbura");
            Tz("bm", "Atlantic/Bermuda");
            Tz("bn", "Asia/Brunei");
            Tz("bo", "America/La_Paz");
            Tz("br", "America/Sao_Paulo");
            Tz("bs", "America/Nassau");
            Tz("bw", "Gaborone");
            Tz("by", "Europe/Minsk");
            Tz("bz", "America/Belize");
            Tz("cd", "Africa/Kinshasa");
            Tz("ch", "Europe/Zurich");
            Tz("ci", "Africa/Abidjan");
            Tz("cl", "America/Santiago");
            Tz("cn", "Asia/Shanghai");
            Tz("co", "America/Bogota");
            Tz("cr", "America/Costa_Rica");
            Tz("cu", "America/Cuba");
            Tz("cv", "Atlantic/Cape_Verde");
            Tz("cy", "Asia/Nicosia");
            Tz("cz", "Europe/Prague");
            Tz("de", "Europe/Berlin");
            Tz("dj", "Africa/Djibouti");
            Tz("dk", "Europe/Copenhagen");
            Tz("do", "America/Santo_Domingo");
            Tz("dz", "Africa/Algiers");
            Tz("ec", "America/Quito");
            Tz("ee", "Europe/Tallinn");
            Tz("eg", "Africa/Cairo");
            Tz("er", "Africa/Asmara");
            Tz("es", "Europe/Madrid");
            Tz("fi", "Europe/Helsinki");
            Tz("fj", "Pacific/Fiji");
            Tz("fk", "America/Stanley");
            Tz("fr", "Europe/Paris");
            Tz("ga", "Africa/Libreville");
            Tz("gb", "Europe/London");
            Tz("gd", "America/Grenada");
            Tz("ge", "Asia/Tbilisi");
            Tz("gh", "Africa/Accra");
            Tz("gm", "Africa/Banjul");
            Tz("gn", "Africa/Conakry");
            Tz("gr", "Europe/Athens");
            Tz("gy", "America/Guyana");
            Tz("hk", "Asia/Hong_Kong");
            Tz("hn", "America/Tegucigalpa");
            Tz("hr", "Europe/Zagreb");
            Tz("ht", "America/Port-au-Prince");
            Tz("hu", "Europe/Budapest");
            Tz("id", "Asia/Jakarta");
            Tz("ie", "Europe/Dublin");
            Tz("il", "Asia/Tel_Aviv", true);
            Tz("in", "Asia/Calcutta", true);
            Tz("iq", "Asia/Baghdad");
            Tz("ir", "Asia/Tehran");
            Tz("is", "Atlantic/Reykjavik");
            Tz("it", "Europe/Rome");
            Tz("jm", "America/Jamaica");
            Tz("jo", "Asia/Amman");
            Tz("jp", "Asia/Tokyo", true);
            Tz("ke", "Africa/Nairobi");
            Tz("kg", "Asia/Bishkek");
            Tz("kh", "Asia/Phnom_Penh");
            Tz("kp", "Asia/Pyongyang");
            Tz("kr", "Asia/Seoul");
            Tz("kw", "Asia/Kuwait");
            Tz("lb", "Asia/Beirut");
            Tz("li", "Europe/Liechtenstein");
            Tz("lk", "Asia/Colombo");
            Tz("lr", "Africa/Monrovia");
            Tz("ls", "Africa/Maseru");
            Tz("lt", "Europe/Vilnius");
            Tz("lu", "Europe/Luxembourg");
            Tz("lv", "Europe/Riga");
            Tz("ly", "Africa/Tripoli");
            Tz("ma", "Africa/Rabat");
            Tz("mc", "Europe/Monaco");
            Tz("md", "Europe/Chisinau");
            Tz("mg", "Indian/Antananarivo");
            Tz("mk", "Europe/Skopje");
            Tz("ml", "Africa/Bamako");
            Tz("mm", "Asia/Rangoon");
            Tz("mn", "Asia/Ulaanbaatar");
            Tz("mo", "Asia/Macao");
            Tz("mq", "America/Martinique");
            Tz("mt", "Europe/Malta");
            Tz("mu", "Indian/Mauritius");
            Tz("mv", "Indian/Maldives");
            Tz("mw", "Africa/Lilongwe");
            Tz("mx", "America/Mexico_City");
            Tz("my", "Asia/Kuala_Lumpur");
            Tz("na", "Africa/Windhoek");
            Tz("ne", "Africa/Niamey");
            Tz("ng", "Africa/Lagos");
            Tz("ni", "America/Managua");
            Tz("nl", "Europe/Amsterdam");
            Tz("no", "Europe/Oslo");
            Tz("np", "Asia/Kathmandu");
            Tz("nz", "Pacific/Aukland");
            Tz("om", "Asia/Muscat");
            Tz("pa", "America/Panama");
            Tz("pe", "America/Lima");
            Tz("pg", "Pacific/Port_Moresby");
            Tz("ph", "Asia/Manila");
            Tz("pk", "Asia/Karachi");
            Tz("pl", "Europe/Warsaw");
            Tz("pr", "America/Puerto_Rico");
            Tz("pt", "Europe/Lisbon");
            Tz("py", "America/Asuncion");
            Tz("qa", "Asia/Qatar");
            Tz("ro", "Europe/Bucharest");
            Tz("rs", "Europe/Belgrade");
            Tz("rw", "Africa/Kigali");
            Tz("sa", "Asia/Riyadh");
            Tz("sd", "Africa/Khartoum");
            Tz("se", "Europe/Stockholm");
            Tz("sg", "Asia/Singapore");
            Tz("si", "Europe/Ljubljana");
            Tz("sk", "Europe/Bratislava");
            Tz("sl", "Africa/Freetown");
            Tz("so", "Africa/Mogadishu");
            Tz("sr", "America/Paramaribo");
            Tz("sv", "America/El_Salvador");
            Tz("sy", "Asia/Damascus");
            Tz("sz", "Africa/Mbabane");
            Tz("td", "Africa/Ndjamena");
            Tz("tg", "Africa/Lome");
            Tz("th", "Asia/Bangkok");
            Tz("tj", "Asia/Dushanbe");
            Tz("tm", "Asia/Ashgabat");
            Tz("tn", "Africa/Tunis");
            Tz("to", "Pacific/Tongatapu");
            Tz("tr", "Asia/Istanbul");
            Tz("tw", "Asia/Taipei");
            Tz("tz", "Africa/Dar_es_Salaam");
            Tz("ua", "Europe/Kiev");
            Tz("ug", "Africa/Kampala");
            Tz("uk", "Europe/London", true);
            Tz("uy", "America/Montevideo");
            Tz("uz", "Asia/Tashkent");
            Tz("ve", "America/Caracas");
            Tz("vn", "Asia/Hanoi");
            Tz("za", "Africa/Johannesburg");
            Tz("zm", "Africa/Lusaka");
            Tz("zw", "Africa/Harare");
        }
        // (small static block merged into the table static ctor below - CS0111 dual static ctor)

        static void Tz(string country, string zoneId)
        {
            IList<string> list = idForCountry.GetOrDefault(country);
            if (list == null)
            {
                list = new List<string>(4);
            }

            list.Add(zoneId);
            idForCountry[country] = list;
        }

        static void Tz(string country, string zoneId, bool major)
        {
            Tz(country, zoneId);
            if (major)
            {
                worldTimeZones.Add(zoneId);
            }
        }

        //@CSharpReplaceBody(code="return formatTimeZoneOffset(date);")
        public static string GetTimeZoneNameForDate(DateTimeValue date, string place)
        {
            if (!date.HasTimezone())
            {
                return "";
            }

            TimeZoneInfo referenceTimezone = null;
            if (place.StartsWith("America/", StringComparison.Ordinal))
            {
                place = "us";
            }

            switch (place)
            {
                case "us":
                    referenceTimezone = ZoneOf("America/New_York");
                    break;
                case "uk":
                case "gb":
                    referenceTimezone = ZoneOf("Europe/London");
                    break;
            }

            if (referenceTimezone == null && place.StartsWith("Europe/", StringComparison.Ordinal))
            {
                referenceTimezone = ZoneOf(place);
            }

            if (referenceTimezone == null)
            {
                return FormatTimeZoneOffset(date);
            }

            bool summerTime = InDaylightTime(referenceTimezone, date.SecondsSinceEpoch().LongValue());
            int tzMinutes = date.TimezoneInMinutes;
            if (summerTime)
            {
                switch (tzMinutes)
                {
                    case 330:
                        return "IST";
                    case 120:
                        return "CEST";
                    case 60:
                        return "BST";
                    case 0:
                        return "GMT";
                    case -240:
                        return "EDT";
                    case -300:
                        return "CDT";
                    case -360:
                        return "MDT";
                    case -420:
                        return "PDT";
                    case -480:
                        return "AKDT";
                    case -540:
                        return "HDT";
                    default:
                        return FormatTimeZoneOffset(date);
                }
            }
            else
            {
                switch (tzMinutes)
                {
                    case 330:
                        return "IST";
                    case 60:
                        return "CET";
                    case 0:
                        return "GMT";
                    case -300:
                        return "EST";
                    case -360:
                        return "CST";
                    case -420:
                        return "MST";
                    case -480:
                        return "PST";
                    case -540:
                        return "AKST";
                    case -600:
                        return "HST";
                    default:
                        return FormatTimeZoneOffset(date);
                }
            }
        }

        public static string FormatTimeZoneOffset(DateTimeValue timeValue)
        {
            UnicodeBuilder sb = new UnicodeBuilder(16);
            CalendarValue.AppendTimezone(timeValue.TimezoneInMinutes, sb);
            return sb.ToString();
        }

        public static string GetOlsonTimeZoneName(DateTimeValue date, string country)
        {
            if (!date.HasTimezone())
            {
                return "";
            }

            IList<string> possibleIds = idForCountry.GetOrDefault(country.ToLowerInvariant());
            string exampleId;
            if (possibleIds == null)
            {
                return FormatTimeZoneOffset(date);
            }
            else
            {
                exampleId = possibleIds[0];
            }

            TimeZoneInfo exampleZone = ZoneOf(exampleId);
            bool inSummerTime = InDaylightTime(exampleZone, date.SecondsSinceEpoch().LongValue());
            int tzMinutes = date.TimezoneInMinutes;
            foreach (string olson in possibleIds)
            {
                TimeZoneInfo possibleTimeZone = ZoneOf(olson);
                int offsetSeconds = GetOffsetInSecondsAtDateTime(possibleTimeZone);
                if (offsetSeconds == tzMinutes * 60)
                {
                    return inSummerTime ? olson + "*" : olson;
                }
            }

            return FormatTimeZoneOffset(date);
        }

        public static bool? InSummerTime(DateTimeValue date, string region)
        {
            string olsonName;
            if (region.Length == 2)
            {
                IList<string> possibleIds = idForCountry.GetOrDefault(region.ToLowerInvariant());
                if (possibleIds == null)
                {
                    return null;
                }
                else
                {
                    olsonName = possibleIds[0];
                }
            }
            else
            {
                olsonName = region;
            }

            TimeZoneInfo zone = OlsonZoneOrUtc(olsonName);
            return (InDaylightTime(zone, date.SecondsSinceEpoch().LongValue()));
        }

        private static TimeZoneInfo OlsonZoneOrUtc(string olsonName)
        {
            try
            {
                return ZoneOf(olsonName);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }

        private static bool InDaylightTime(TimeZoneInfo zone, long secondsSinceEpoch)
        {
            try
            {
                return zone.IsDaylightSavingTime(DateTimeOffset.FromUnixTimeSeconds(secondsSinceEpoch));
            }
            catch (ArgumentOutOfRangeException)
            {
                // A year .NET's calendar does not reach - below 1, above 9999: standard time, as TimeZone.GetOffset has it.
                return false;
            }
        }

        public static TimeZoneInfo GetNamedTimeZone(string olsonName)
        {
            if (knownTimeZones.Contains(olsonName))
            {
                return ZoneOf(olsonName);
            }
            else
            {
                return null;
            }
        }

        // Preserves the legacy offset-at-now behaviour: the old DateTimeValue.ToJavaInstant() returned
        // Instant.now() (a stub that ignored the value), so the offset was taken at the current instant,
        // not at dateTime. Reached only from GetOlsonTimeZoneName.
        private static int GetOffsetInSecondsAtDateTime(TimeZoneInfo zone)
        {
            return (int)zone.GetUtcOffset(DateTimeOffset.UtcNow).TotalSeconds;
        }

        // Java ZoneId.of(): resolve via the shared platform (TimeZoneInfo) resolver; an unknown id
        // throws (the sole catcher is OlsonZoneOrUtc).
        private static TimeZoneInfo ZoneOf(string id)
        {
            TimeZoneInfo tzi = OutSmart.DAXon.Internal.Collections.TimeZone.Resolve(id);
            if (tzi == null)
                throw new TimeZoneNotFoundException("Unknown time-zone ID: " + id);
            return tzi;
        }
    }
}