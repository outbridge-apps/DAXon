// B05_Strings2.cs
// Spec-derived test cases: F&O string functions beyond the core set.
// Covers codepoint functions, codepoint-equal/compare/contains-token,
// URI escaping functions, one-arg tokenize, format-integer pictures,
// and format-number with xsl:decimal-format declarations.
// All expected values derived by hand from XPath 3.1 / F&O 3.1 / XSLT 3.0.

namespace OutSmart.DAXon.SpecTests.Cases;

public static class B05_Strings2
{
    public static TestCase[] Cases() => new[]
    {
        // ---------- codepoints-to-string / string-to-codepoints ----------

        new TestCase
        {
            Id = "str2-001",
            Area = "xpath: strings extra",
            Title = "codepoints-to-string builds string; empty sequence gives zero-length string",
            SpecRef = "F&O 5.2.1",
            // (72,101,108,108,111) = "Hello"; codepoints-to-string(()) = "" so string-length = 0
            XPathExpr = @"(codepoints-to-string((72, 101, 108, 108, 111)), string-length(codepoints-to-string(())))",
            Expected = "Hello|0"
        },
        new TestCase
        {
            Id = "str2-002",
            Area = "xpath: strings extra",
            Title = "codepoints-to-string with codepoint 0 raises FOCH0001",
            SpecRef = "F&O 5.2.1",
            XPathExpr = @"codepoints-to-string(0)",
            ExpectedErrorCode = "FOCH0001"
        },
        new TestCase
        {
            Id = "str2-003",
            Area = "xpath: strings extra",
            Title = "string-to-codepoints returns codepoint sequence; empty string gives empty sequence",
            SpecRef = "F&O 5.2.2",
            // 'a'=97 'b'=98 'c'=99; string-to-codepoints('') is () so count = 0
            XPathExpr = @"(string-to-codepoints('abc'), count(string-to-codepoints('')))",
            Expected = "97|98|99|0"
        },

        // ---------- codepoint-equal / compare / contains-token ----------

        new TestCase
        {
            Id = "str2-004",
            Area = "xpath: strings extra",
            Title = "codepoint-equal: equal, unequal, empty argument yields empty sequence",
            SpecRef = "F&O 5.3.7",
            XPathExpr = @"(codepoint-equal('abc', 'abc'), codepoint-equal('abc', 'abcd'), count(codepoint-equal((), 'a')))",
            Expected = "true|false|0"
        },
        new TestCase
        {
            Id = "str2-005",
            Area = "xpath: strings extra",
            Title = "compare under codepoint collation returns -1/0/1; proper prefix is smaller",
            SpecRef = "F&O 5.3.6",
            XPathExpr = @"(compare('a', 'b'), compare('abc', 'abc'), compare('b', 'a'), compare('a', 'aa'))",
            Expected = "-1|0|1|-1"
        },
        new TestCase
        {
            Id = "str2-006",
            Area = "xpath: strings extra",
            Title = "contains-token: exact token match, token arg is whitespace-trimmed, sequence input",
            SpecRef = "F&O 5.3.9",
            XPathExpr = @"(contains-token('red green blue', 'green'), contains-token('red green blue', ' green '), contains-token(('red', 'green', 'blue'), 'red'))",
            Expected = "true|true|true"
        },
        new TestCase
        {
            Id = "str2-007",
            Area = "xpath: strings extra",
            Title = "contains-token: no substring match, comma-glued tokens differ, empty token is false",
            SpecRef = "F&O 5.3.9",
            // 'red, green, blue' splits on whitespace into 'red,' 'green,' 'blue' - none equals 'green'
            XPathExpr = @"(contains-token('red green blue', 'gre'), contains-token('red, green, blue', 'green'), contains-token('red green blue', ''))",
            Expected = "false|false|false"
        },

        // ---------- URI escaping functions ----------

        new TestCase
        {
            Id = "str2-008",
            Area = "xpath: strings extra",
            Title = "encode-for-uri keeps unreserved chars, percent-encodes space and reserved chars",
            SpecRef = "F&O 6.2",
            XPathExpr = @"encode-for-uri('AZaz09-_.~ /?')",
            Expected = "AZaz09-_.~%20%2F%3F"
        },
        new TestCase
        {
            Id = "str2-009",
            Area = "xpath: strings extra",
            Title = "encode-for-uri percent-encodes non-ASCII as UTF-8 octets in uppercase hex",
            SpecRef = "F&O 6.2",
            // codepoint 233 = e-acute (U+00E9), UTF-8 C3 A9; spec example '~bÃ©bÃ©'
            XPathExpr = @"encode-for-uri(concat('~b', codepoints-to-string(233), 'b', codepoints-to-string(233)))",
            Expected = "~b%C3%A9b%C3%A9"
        },
        new TestCase
        {
            Id = "str2-010",
            Area = "xpath: strings extra",
            Title = "iri-to-uri escapes space and non-ASCII but keeps reserved URI chars",
            SpecRef = "F&O 6.3",
            // ':' '/' '?' '=' '#' are retained; space -> %20; U+00E9 -> %C3%A9
            XPathExpr = @"iri-to-uri(concat('http://a/b c', codepoints-to-string(233), '?x=1#f'))",
            Expected = "http://a/b%20c%C3%A9?x=1#f"
        },
        new TestCase
        {
            Id = "str2-011",
            Area = "xpath: strings extra",
            Title = "escape-html-uri escapes only chars outside codepoints 32-126; space kept",
            SpecRef = "F&O 6.4",
            XPathExpr = @"escape-html-uri(concat('http://a/b c?x=1', codepoints-to-string(233)))",
            Expected = "http://a/b c?x=1%C3%A9"
        },

        // ---------- tokenize one-argument form ----------

        new TestCase
        {
            Id = "str2-012",
            Area = "xpath: strings extra",
            Title = "one-arg tokenize splits on whitespace and trims leading/trailing whitespace",
            SpecRef = "F&O 5.6.5",
            XPathExpr = @"tokenize(' red    green  blue ')",
            Expected = "red|green|blue"
        },
        new TestCase
        {
            Id = "str2-013",
            Area = "xpath: strings extra",
            Title = "one-arg tokenize of whitespace-only string returns empty sequence",
            SpecRef = "F&O 5.6.5",
            XPathExpr = @"count(tokenize('   '))",
            Expected = "0"
        },

        // ---------- format-integer ----------

        new TestCase
        {
            Id = "str2-014",
            Area = "xpath: strings extra",
            Title = "format-integer decimal-digit pictures: '1', '01' zero-padding, no truncation, grouping",
            SpecRef = "F&O 4.6.1",
            XPathExpr = @"(format-integer(7, '1'), format-integer(7, '01'), format-integer(123, '01'), format-integer(1234, '#,##0'))",
            Expected = "7|07|123|1,234"
        },
        new TestCase
        {
            Id = "str2-015",
            Area = "xpath: strings extra",
            Title = "format-integer alphabetic sequence a..z aa ab..., uppercase variant",
            SpecRef = "F&O 4.6.1",
            XPathExpr = @"(format-integer(1, 'a'), format-integer(4, 'a'), format-integer(26, 'a'), format-integer(27, 'a'), format-integer(4, 'A'))",
            Expected = "a|d|z|aa|D"
        },
        new TestCase
        {
            Id = "str2-016",
            Area = "xpath: strings extra",
            Title = "format-integer roman numerals lower and upper case, subtractive forms",
            SpecRef = "F&O 4.6.1",
            XPathExpr = @"(format-integer(4, 'i'), format-integer(9, 'i'), format-integer(14, 'I'), format-integer(2024, 'I'))",
            Expected = "iv|ix|XIV|MMXXIV"
        },
        new TestCase
        {
            Id = "str2-017",
            Area = "xpath: strings extra",
            Title = "format-integer words in English: w lowercase, W uppercase, Ww title case",
            SpecRef = "F&O 4.6.1",
            // single-word values chosen to avoid hyphenation/'and' variation
            XPathExpr = @"(format-integer(4, 'w', 'en'), format-integer(4, 'W', 'en'), format-integer(12, 'Ww', 'en'))",
            Expected = "four|FOUR|Twelve"
        },
        new TestCase
        {
            Id = "str2-018",
            Area = "xpath: strings extra",
            Title = "format-integer English ordinal modifier '1;o' produces st/nd/rd/th suffixes",
            SpecRef = "F&O 4.6.1",
            XPathExpr = @"(format-integer(1, '1;o', 'en'), format-integer(2, '1;o', 'en'), format-integer(3, '1;o', 'en'), format-integer(4, '1;o', 'en'), format-integer(11, '1;o', 'en'), format-integer(21, '1;o', 'en'))",
            Expected = "1st|2nd|3rd|4th|11th|21st"
        },
        new TestCase
        {
            Id = "str2-019",
            Area = "xpath: strings extra",
            Title = "format-integer negative gets minus sign, zero formats, empty sequence gives zero-length string",
            SpecRef = "F&O 4.6.1",
            XPathExpr = @"(format-integer(-15, '1'), format-integer(0, '1'), string-length(format-integer((), '1')))",
            Expected = "-15|0|0"
        },
        new TestCase
        {
            Id = "str2-020",
            Area = "xpath: strings extra",
            Title = "format-integer with zero-length picture raises FODF1310",
            SpecRef = "F&O 4.6.1",
            XPathExpr = @"format-integer(1, '')",
            ExpectedErrorCode = "FODF1310"
        },

        // ---------- format-number with xsl:decimal-format (full XSLT) ----------

        new TestCase
        {
            Id = "str2-021",
            Area = "xpath: strings extra",
            Title = "format-number '#,##0.00': grouping, forced fraction digits, zero, rounding",
            SpecRef = "F&O 4.7.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:value-of select=""format-number(1234.5, '#,##0.00'), format-number(0, '#,##0.00'), format-number(1234567.891, '#,##0.00')"" separator=""|""/>
  </xsl:template>
</xsl:stylesheet>",
            Expected = "1,234.50|0.00|1,234,567.89"
        },
        new TestCase
        {
            Id = "str2-022",
            Area = "xpath: strings extra",
            Title = "format-number percent multiplies by 100; custom per-mille sign multiplies by 1000",
            SpecRef = "F&O 4.7.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:decimal-format name=""pm"" per-mille=""m""/>
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:value-of select=""format-number(0.25, '0%'), format-number(0.006, '0m', 'pm')"" separator=""|""/>
  </xsl:template>
</xsl:stylesheet>",
            Expected = "25%|6m"
        },
        new TestCase
        {
            Id = "str2-023",
            Area = "xpath: strings extra",
            Title = "format-number negative subpattern used for negatives; default is minus-sign prefix",
            SpecRef = "F&O 4.7.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:value-of select=""format-number(-1234.5, '#,##0.00;(#,##0.00)'), format-number(-1.5, '0.0')"" separator=""|""/>
  </xsl:template>
</xsl:stylesheet>",
            Expected = "(1,234.50)|-1.5"
        },
        new TestCase
        {
            Id = "str2-024",
            Area = "xpath: strings extra",
            Title = "unnamed xsl:decimal-format swaps decimal and grouping separators (European style)",
            SpecRef = "F&O 4.7.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:decimal-format decimal-separator="","" grouping-separator="".""/>
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:value-of select=""format-number(1234.5, '#.##0,00')""/>
  </xsl:template>
</xsl:stylesheet>",
            Expected = "1.234,50"
        },
        new TestCase
        {
            Id = "str2-025",
            Area = "xpath: strings extra",
            Title = "format-number uses decimal-format NaN and infinity strings; -INF gets minus prefix",
            SpecRef = "F&O 4.7.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:decimal-format name=""df"" NaN=""[nan]"" infinity=""[inf]""/>
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:value-of select=""format-number(number('xyz'), '0.0', 'df'), format-number(1 div 0e0, '0.0', 'df'), format-number(-1 div 0e0, '0.0', 'df')"" separator=""|""/>
  </xsl:template>
</xsl:stylesheet>",
            Expected = "[nan]|[inf]|-[inf]"
        },
        new TestCase
        {
            Id = "str2-026",
            Area = "xpath: strings extra",
            Title = "custom pattern-separator character splits positive and negative subpictures",
            SpecRef = "F&O 4.7.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:decimal-format name=""ps"" pattern-separator=""!""/>
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:value-of select=""format-number(7, '0!(0)', 'ps'), format-number(-7, '0!(0)', 'ps')"" separator=""|""/>
  </xsl:template>
</xsl:stylesheet>",
            Expected = "7|(7)"
        },
        new TestCase
        {
            Id = "str2-027",
            Area = "xpath: strings extra",
            Title = "format-number with undeclared decimal format name raises FODF1280",
            SpecRef = "F&O 4.7.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:value-of select=""format-number(1, '0', 'nosuch')""/>
  </xsl:template>
</xsl:stylesheet>",
            ExpectedErrorCode = "FODF1280"
        }
    };
}
