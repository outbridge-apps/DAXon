// B08_DateTime.cs
// Spec-derived test cases: XPath 3.1 dates, times, durations (F&O 3.1 sections 8, 9, 18).
// Expected values reasoned from the W3C specs; no engine access. All timezones explicit.

namespace OutSmart.DAXon.SpecTests.Cases;

public static class B08_DateTime
{
    public static TestCase[] Cases() => new[]
    {
        new TestCase
        {
            Id = "dt-001",
            Area = "xpath: dates+durations",
            Title = "xs:date and xs:time constructors yield canonical lexical forms",
            SpecRef = "F&O 18.1",
            XPathExpr = @"concat(xs:date('2024-01-15'), '|', xs:time('13:20:00+05:00'))",
            Expected = "2024-01-15|13:20:00+05:00"
        },
        new TestCase
        {
            Id = "dt-002",
            Area = "xpath: dates+durations",
            Title = "xs:date with month 13 raises FORG0001",
            SpecRef = "F&O 18.1",
            XPathExpr = @"xs:date('2024-13-01')",
            ExpectedErrorCode = "FORG0001"
        },
        new TestCase
        {
            Id = "dt-003",
            Area = "xpath: dates+durations",
            Title = "xs:dateTime accepts Feb 29 in leap year 2024",
            SpecRef = "F&O 18.1",
            XPathExpr = @"string(xs:dateTime('2024-02-29T12:00:00Z'))",
            Expected = "2024-02-29T12:00:00Z"
        },
        new TestCase
        {
            Id = "dt-004",
            Area = "xpath: dates+durations",
            Title = "xs:dateTime Feb 29 in non-leap year 2023 raises FORG0001",
            SpecRef = "F&O 18.1",
            XPathExpr = @"xs:dateTime('2023-02-29T00:00:00')",
            ExpectedErrorCode = "FORG0001"
        },
        new TestCase
        {
            Id = "dt-005",
            Area = "xpath: dates+durations",
            Title = "duration constructors canonicalize: P20M -> P1Y8M, PT90M -> PT1H30M",
            SpecRef = "F&O 18.1",
            XPathExpr = @"(xs:duration('P1Y2M3DT4H5M6S'), xs:yearMonthDuration('P20M'), xs:dayTimeDuration('PT90M'))",
            Expected = "P1Y2M3DT4H5M6S|P1Y8M|PT1H30M"
        },
        new TestCase
        {
            Id = "dt-006",
            Area = "xpath: dates+durations",
            Title = "Gregorian type constructors gYear/gMonth/gDay/gYearMonth/gMonthDay",
            SpecRef = "F&O 18.1",
            XPathExpr = @"(xs:gYear('2024'), xs:gMonth('--12'), xs:gDay('---15'), xs:gYearMonth('2024-07'), xs:gMonthDay('--02-29'))",
            Expected = "2024|--12|---15|2024-07|--02-29"
        },
        new TestCase
        {
            Id = "dt-007",
            Area = "xpath: dates+durations",
            Title = "component extractors from dateTime; seconds returns decimal 45.5",
            SpecRef = "F&O 9.5",
            XPathExpr = @"let $d := xs:dateTime('2024-01-15T10:30:45.5Z') return (year-from-dateTime($d), month-from-dateTime($d), day-from-dateTime($d), hours-from-dateTime($d), minutes-from-dateTime($d), seconds-from-dateTime($d))",
            Expected = "2024|1|15|10|30|45.5"
        },
        new TestCase
        {
            Id = "dt-008",
            Area = "xpath: dates+durations",
            Title = "timezone-from-*: Z gives PT0S, absent gives empty, -05:00 gives -PT5H",
            SpecRef = "F&O 9.5",
            XPathExpr = @"(string(timezone-from-dateTime(xs:dateTime('2024-01-15T10:00:00Z'))), empty(timezone-from-date(xs:date('2024-01-15'))), string(timezone-from-time(xs:time('10:00:00-05:00'))))",
            Expected = "PT0S|true|-PT5H"
        },
        new TestCase
        {
            Id = "dt-009",
            Area = "xpath: dates+durations",
            Title = "fn:dateTime(date, time) with no timezones yields timezoneless dateTime",
            SpecRef = "F&O 9.3.1",
            XPathExpr = @"string(dateTime(xs:date('2024-01-15'), xs:time('10:30:00')))",
            Expected = "2024-01-15T10:30:00"
        },
        new TestCase
        {
            Id = "dt-010",
            Area = "xpath: dates+durations",
            Title = "fn:dateTime takes the timezone of the one argument that has one",
            SpecRef = "F&O 9.3.1",
            XPathExpr = @"string(dateTime(xs:date('2024-01-15Z'), xs:time('10:30:00')))",
            Expected = "2024-01-15T10:30:00Z"
        },
        new TestCase
        {
            Id = "dt-011",
            Area = "xpath: dates+durations",
            Title = "fn:dateTime with conflicting timezones raises FORG0008",
            SpecRef = "F&O 9.3.1",
            XPathExpr = @"dateTime(xs:date('2024-01-15+01:00'), xs:time('10:00:00+02:00'))",
            ExpectedErrorCode = "FORG0008"
        },
        new TestCase
        {
            Id = "dt-012",
            Area = "xpath: dates+durations",
            Title = "date plus dayTimeDuration P3D",
            SpecRef = "F&O 9.7",
            XPathExpr = @"string(xs:date('2024-01-15') + xs:dayTimeDuration('P3D'))",
            Expected = "2024-01-18"
        },
        new TestCase
        {
            Id = "dt-013",
            Area = "xpath: dates+durations",
            Title = "dateTime plus PT45M crosses midnight, timezone preserved",
            SpecRef = "F&O 9.7",
            XPathExpr = @"string(xs:dateTime('2024-01-15T23:30:00Z') + xs:dayTimeDuration('PT45M'))",
            Expected = "2024-01-16T00:15:00Z"
        },
        new TestCase
        {
            Id = "dt-014",
            Area = "xpath: dates+durations",
            Title = "time plus duration wraps modulo 24 hours",
            SpecRef = "F&O 9.7",
            XPathExpr = @"string(xs:time('23:30:00') + xs:dayTimeDuration('PT2H'))",
            Expected = "01:30:00"
        },
        new TestCase
        {
            Id = "dt-015",
            Area = "xpath: dates+durations",
            Title = "dateTime minus dateTime (same timezone) yields dayTimeDuration",
            SpecRef = "F&O 9.7",
            XPathExpr = @"string(xs:dateTime('2024-01-15T12:00:00Z') - xs:dateTime('2024-01-14T06:30:00Z'))",
            Expected = "P1DT5H30M"
        },
        new TestCase
        {
            Id = "dt-016",
            Area = "xpath: dates+durations",
            Title = "date minus date across leap-year Feb 29 yields P2D",
            SpecRef = "F&O 9.7",
            XPathExpr = @"string(xs:date('2024-03-01Z') - xs:date('2024-02-28Z'))",
            Expected = "P2D"
        },
        new TestCase
        {
            Id = "dt-017",
            Area = "xpath: dates+durations",
            Title = "date minus P1M pins day to last day of February (leap year)",
            SpecRef = "F&O 9.7",
            XPathExpr = @"string(xs:date('2024-03-31') - xs:yearMonthDuration('P1M'))",
            Expected = "2024-02-29"
        },
        new TestCase
        {
            Id = "dt-018",
            Area = "xpath: dates+durations",
            Title = "yearMonthDuration times 2.3 rounds months: P2Y11M * 2.3 = P6Y9M",
            SpecRef = "F&O 8.3",
            XPathExpr = @"string(xs:yearMonthDuration('P2Y11M') * 2.3)",
            Expected = "P6Y9M"
        },
        new TestCase
        {
            Id = "dt-019",
            Area = "xpath: dates+durations",
            Title = "dayTimeDuration div dayTimeDuration yields exact decimal 4",
            SpecRef = "F&O 8.3",
            XPathExpr = @"string(xs:dayTimeDuration('P1D') div xs:dayTimeDuration('PT6H'))",
            Expected = "4"
        },
        new TestCase
        {
            Id = "dt-020",
            Area = "xpath: dates+durations",
            Title = "dayTimeDuration div 2: P1DT4H halves to PT14H",
            SpecRef = "F&O 8.3",
            XPathExpr = @"string(xs:dayTimeDuration('P1DT4H') div 2)",
            Expected = "PT14H"
        },
        new TestCase
        {
            Id = "dt-021",
            Area = "xpath: dates+durations",
            Title = "years/months-from-duration on P20M give 1 and 8",
            SpecRef = "F&O 8.2",
            XPathExpr = @"(years-from-duration(xs:yearMonthDuration('P20M')), months-from-duration(xs:yearMonthDuration('P20M')))",
            Expected = "1|8"
        },
        new TestCase
        {
            Id = "dt-022",
            Area = "xpath: dates+durations",
            Title = "days/hours/minutes/seconds-from-duration incl. negative and fractional",
            SpecRef = "F&O 8.2",
            XPathExpr = @"(days-from-duration(xs:dayTimeDuration('P3DT10H')), hours-from-duration(xs:dayTimeDuration('P3DT10H')), minutes-from-duration(xs:dayTimeDuration('-P5DT12H30M')), seconds-from-duration(xs:dayTimeDuration('P3DT10H12.5S')))",
            Expected = "3|10|-30|12.5"
        },
        new TestCase
        {
            Id = "dt-023",
            Area = "xpath: dates+durations",
            Title = "duration equality: P1Y eq P12M and P1D eq PT24H",
            SpecRef = "F&O 8.1",
            XPathExpr = @"(xs:duration('P1Y') eq xs:duration('P12M'), xs:dayTimeDuration('P1D') eq xs:dayTimeDuration('PT24H'))",
            Expected = "true|true"
        },
        new TestCase
        {
            Id = "dt-024",
            Area = "xpath: dates+durations",
            Title = "dateTime eq across timezones (same instant); yearMonthDuration lt",
            SpecRef = "F&O 9.4; F&O 8.1",
            XPathExpr = @"(xs:dateTime('2024-01-15T10:00:00+02:00') eq xs:dateTime('2024-01-15T08:00:00Z'), xs:yearMonthDuration('P1Y') lt xs:yearMonthDuration('P13M'))",
            Expected = "true|true"
        },
        new TestCase
        {
            Id = "dt-025",
            Area = "xpath: dates+durations",
            Title = "format-date numeric picture [Y0001]-[M01]-[D01]",
            SpecRef = "F&O 9.8",
            XPathExpr = @"format-date(xs:date('2024-01-15'), '[Y0001]-[M01]-[D01]')",
            Expected = "2024-01-15"
        },
        new TestCase
        {
            Id = "dt-026",
            Area = "xpath: dates+durations",
            Title = "format-date names with lang en: [FNn], [D1] [MNn] [Y]",
            SpecRef = "F&O 9.8",
            XPathExpr = @"format-date(xs:date('2024-03-05'), '[FNn], [D1] [MNn] [Y]', 'en', (), ())",
            Expected = "Tuesday, 5 March 2024"
        },
        new TestCase
        {
            Id = "dt-027",
            Area = "xpath: dates+durations",
            Title = "format-dateTime zero-padded time components [H01]:[m01]:[s01]",
            SpecRef = "F&O 9.8",
            XPathExpr = @"format-dateTime(xs:dateTime('2024-01-15T09:05:03Z'), '[H01]:[m01]:[s01]')",
            Expected = "09:05:03"
        },
        new TestCase
        {
            Id = "dt-028",
            Area = "xpath: dates+durations",
            Title = "format-date doubled brackets escape to literal [ and ]",
            SpecRef = "F&O 9.8",
            XPathExpr = @"format-date(xs:date('2024-01-15'), '[[[Y0001]]]')",
            Expected = "[2024]"
        },
        new TestCase
        {
            Id = "dt-029",
            Area = "xpath: dates+durations",
            Title = "format-date with hours component [H01] raises FOFD1350",
            SpecRef = "F&O 9.8",
            XPathExpr = @"format-date(xs:date('2024-01-15'), '[H01]')",
            ExpectedErrorCode = "FOFD1350"
        },
        new TestCase
        {
            Id = "dt-030",
            Area = "xpath: dates+durations",
            Title = "adjust-dateTime-to-timezone with explicit -PT5H shifts wall clock",
            SpecRef = "F&O 9.6",
            XPathExpr = @"string(adjust-dateTime-to-timezone(xs:dateTime('2024-01-15T10:00:00Z'), xs:dayTimeDuration('-PT5H')))",
            Expected = "2024-01-15T05:00:00-05:00"
        },
        new TestCase
        {
            Id = "dt-031",
            Area = "xpath: dates+durations",
            Title = "adjust-dateTime-to-timezone with empty sequence strips timezone",
            SpecRef = "F&O 9.6",
            XPathExpr = @"string(adjust-dateTime-to-timezone(xs:dateTime('2024-01-15T10:00:00-05:00'), ()))",
            Expected = "2024-01-15T10:00:00"
        },
        new TestCase
        {
            Id = "dt-032",
            Area = "xpath: dates+durations",
            Title = "adjust-date-to-timezone on zoned date can shift to previous day",
            SpecRef = "F&O 9.6",
            XPathExpr = @"string(adjust-date-to-timezone(xs:date('2024-01-15Z'), xs:dayTimeDuration('-PT5H')))",
            Expected = "2024-01-14-05:00"
        }
    };
}
