namespace OutSmart.DAXon.SpecTests.Cases;

public static class B04_Strings1
{
    public static TestCase[] Cases() => new[]
    {
        // ---- fn:string ----
        new TestCase
        {
            Id = "str1-001",
            Area = "xpath: strings core",
            Title = "string() with no argument returns string value of context document node",
            SpecRef = "F&O 2.3",
            XPathExpr = @"string()",
            InputXml = @"<r>hello</r>",
            Expected = "hello"
        },
        new TestCase
        {
            Id = "str1-002",
            Area = "xpath: strings core",
            Title = "string() on element node concatenates descendant text nodes",
            SpecRef = "F&O 2.3",
            XPathExpr = @"string(/r)",
            InputXml = @"<r><a>ab</a><b>cd</b></r>",
            Expected = "abcd"
        },
        new TestCase
        {
            Id = "str1-003",
            Area = "xpath: strings core",
            Title = "string(()) is zero-length string; string on booleans gives 'true'/'false'",
            SpecRef = "F&O 2.3",
            XPathExpr = @"(string-length(string(())), string(true()), string(false()))",
            Expected = "0|true|false"
        },
        new TestCase
        {
            Id = "str1-004",
            Area = "xpath: strings core",
            Title = "string on numbers: integral double no point, fractional double, decimal canonical form",
            SpecRef = "F&O 2.3; XPath 3.1 casting to xs:string",
            XPathExpr = @"(string(1e0), string(2.5e0), string(xs:decimal('1.50')))",
            Expected = "1|2.5|1.5"
        },

        // ---- fn:string-length ----
        new TestCase
        {
            Id = "str1-005",
            Area = "xpath: strings core",
            Title = "string-length of literal counts characters",
            SpecRef = "F&O 5.4.4",
            XPathExpr = @"string-length('Harp not on that string')",
            Expected = "23"
        },
        new TestCase
        {
            Id = "str1-006",
            Area = "xpath: strings core",
            Title = "string-length: no-arg uses context string value; '' and () both give 0",
            SpecRef = "F&O 5.4.4",
            XPathExpr = @"(string-length(), string-length(''), string-length(()))",
            InputXml = @"<r>abcde</r>",
            Expected = "5|0|0"
        },
        new TestCase
        {
            Id = "str1-007",
            Area = "xpath: strings core",
            Title = "string-length counts astral-plane character (U+1F600) as one",
            SpecRef = "F&O 5.4.4",
            XPathExpr = @"(string-length(/r), string-length(codepoints-to-string(128512)))",
            InputXml = @"<r>a&#x1F600;b</r>",
            Expected = "3|1"
        },

        // ---- fn:concat ----
        new TestCase
        {
            Id = "str1-008",
            Area = "xpath: strings core",
            Title = "concat atomizes element nodes to string values",
            SpecRef = "F&O 5.4.1",
            XPathExpr = @"concat(/r/a, '-', /r/b, '!')",
            InputXml = @"<r><a>1</a><b>2</b></r>",
            Expected = "1-2!"
        },
        new TestCase
        {
            Id = "str1-009",
            Area = "xpath: strings core",
            Title = "concat treats empty-sequence argument as '' and casts atomic values to string",
            SpecRef = "F&O 5.4.1",
            XPathExpr = @"concat('x', (), 'y', 1, 2.5)",
            Expected = "xy12.5"
        },

        // ---- fn:string-join ----
        new TestCase
        {
            Id = "str1-010",
            Area = "xpath: strings core",
            Title = "string-join one-argument form joins with no separator",
            SpecRef = "F&O 5.4.2",
            XPathExpr = @"string-join(('a','b','c'))",
            Expected = "abc"
        },
        new TestCase
        {
            Id = "str1-011",
            Area = "xpath: strings core",
            Title = "string-join casts xs:integer items to string before joining",
            SpecRef = "F&O 5.4.2",
            XPathExpr = @"string-join(1 to 4, '.')",
            Expected = "1.2.3.4"
        },
        new TestCase
        {
            Id = "str1-012",
            Area = "xpath: strings core",
            Title = "string-join of empty sequence is zero-length; empty separator concatenates",
            SpecRef = "F&O 5.4.2",
            XPathExpr = @"(string-length(string-join((), '-')), string-join(('a','b'), ''))",
            Expected = "0|ab"
        },

        // ---- fn:substring (rounding rules) ----
        new TestCase
        {
            Id = "str1-013",
            Area = "xpath: strings core",
            Title = "substring rounds start and length: substring('12345',1.5,2.6) is '234'",
            SpecRef = "F&O 5.4.3",
            XPathExpr = @"substring('12345', 1.5, 2.6)",
            Expected = "234"
        },
        new TestCase
        {
            Id = "str1-014",
            Area = "xpath: strings core",
            Title = "substring with start 0 shifts window: substring('12345',0,3) is '12'",
            SpecRef = "F&O 5.4.3",
            XPathExpr = @"substring('12345', 0, 3)",
            Expected = "12"
        },
        new TestCase
        {
            Id = "str1-015",
            Area = "xpath: strings core",
            Title = "substring with negative length selects nothing",
            SpecRef = "F&O 5.4.3",
            XPathExpr = @"substring('12345', 5, -3)",
            Expected = ""
        },
        new TestCase
        {
            Id = "str1-016",
            Area = "xpath: strings core",
            Title = "two-argument substring with negative start returns whole string",
            SpecRef = "F&O 5.4.3",
            XPathExpr = @"substring('12345', -3)",
            Expected = "12345"
        },
        new TestCase
        {
            Id = "str1-017",
            Area = "xpath: strings core",
            Title = "substring with NaN start or NaN length selects nothing",
            SpecRef = "F&O 5.4.3",
            XPathExpr = @"(string-length(substring('12345', 0 div 0e0)), string-length(substring('12345', 1, 0 div 0e0)))",
            Expected = "0|0"
        },
        new TestCase
        {
            Id = "str1-018",
            Area = "xpath: strings core",
            Title = "substring: -42 with +INF length gives whole string; -INF start with +INF length gives empty (sum is NaN)",
            SpecRef = "F&O 5.4.3",
            XPathExpr = @"(substring('12345', -42, 1 div 0e0), string-length(substring('12345', -1 div 0e0, 1 div 0e0)))",
            Expected = "12345|0"
        },

        // ---- fn:substring-before / fn:substring-after ----
        new TestCase
        {
            Id = "str1-019",
            Area = "xpath: strings core",
            Title = "substring-before: match gives prefix; empty needle and no-match both give ''",
            SpecRef = "F&O 5.5.4",
            XPathExpr = @"(substring-before('tattoo','attoo'), string-length(substring-before('tattoo','')), string-length(substring-before('abc','z')))",
            Expected = "t|0|0"
        },
        new TestCase
        {
            Id = "str1-020",
            Area = "xpath: strings core",
            Title = "substring-after: match gives suffix; empty needle returns whole string; no match gives ''",
            SpecRef = "F&O 5.5.5",
            XPathExpr = @"(substring-after('tattoo','tat'), substring-after('tattoo',''), string-length(substring-after('abc','z')))",
            Expected = "too|tattoo|0"
        },

        // ---- fn:contains / fn:starts-with / fn:ends-with ----
        new TestCase
        {
            Id = "str1-021",
            Area = "xpath: strings core",
            Title = "contains/starts-with/ends-with basic true and false results",
            SpecRef = "F&O 5.5.1, 5.5.2, 5.5.3",
            XPathExpr = @"(contains('tattoo','tt'), contains('tattoo','ttt'), starts-with('tattoo','att'), ends-with('tattoo','too'))",
            Expected = "true|false|false|true"
        },
        new TestCase
        {
            Id = "str1-022",
            Area = "xpath: strings core",
            Title = "contains/starts-with/ends-with with zero-length needle all return true",
            SpecRef = "F&O 5.5.1, 5.5.2, 5.5.3",
            XPathExpr = @"(contains('a',''), starts-with('',''), ends-with('abc',''))",
            Expected = "true|true|true"
        },

        // ---- fn:translate ----
        new TestCase
        {
            Id = "str1-023",
            Area = "xpath: strings core",
            Title = "translate deletes characters mapped past end of trans string",
            SpecRef = "F&O 5.6.3",
            XPathExpr = @"translate('--aaa--','abc-','ABC')",
            Expected = "AAA"
        },
        new TestCase
        {
            Id = "str1-024",
            Area = "xpath: strings core",
            Title = "translate: shorter trans deletes 'c'; extra trans chars ignored; first map occurrence wins",
            SpecRef = "F&O 5.6.3",
            XPathExpr = @"(translate('abcdabc','abc','AB'), translate('abc','ab','ABCD'), translate('aba','aa','XY'))",
            Expected = "ABdAB|ABc|XbX"
        },

        // ---- fn:normalize-space ----
        new TestCase
        {
            Id = "str1-025",
            Area = "xpath: strings core",
            Title = "normalize-space strips ends and collapses runs, including tab/LF/CR",
            SpecRef = "F&O 5.4.5",
            XPathExpr = @"(normalize-space('  a   b  c  '), normalize-space(concat('a', codepoints-to-string((9,10,13)), 'b')))",
            Expected = "a b c|a b"
        },
        new TestCase
        {
            Id = "str1-026",
            Area = "xpath: strings core",
            Title = "normalize-space() with no argument normalizes context string value",
            SpecRef = "F&O 5.4.5",
            XPathExpr = @"normalize-space()",
            InputXml = @"<r>  x    y  </r>",
            Expected = "x y"
        },

        // ---- fn:upper-case / fn:lower-case ----
        new TestCase
        {
            Id = "str1-027",
            Area = "xpath: strings core",
            Title = "upper-case/lower-case map letters only; lower-case leaves sharp-s (U+00DF) unchanged",
            SpecRef = "F&O 5.4.7, 5.4.8",
            XPathExpr = @"(upper-case('abCd0'), lower-case('ABc!D'), lower-case(codepoints-to-string(223)) eq codepoints-to-string(223))",
            Expected = "ABCD0|abc!d|true"
        },

        // ---- error cases ----
        new TestCase
        {
            Id = "str1-028",
            Area = "xpath: strings core",
            Title = "string() applied to a function item raises FOTY0014",
            SpecRef = "F&O 2.3",
            XPathExpr = @"string(abs#1)",
            ExpectedErrorCode = "FOTY0014"
        },
        new TestCase
        {
            Id = "str1-029",
            Area = "xpath: strings core",
            Title = "concat with a single argument has no matching signature: XPST0017",
            SpecRef = "F&O 5.4.1; XPath 3.1 3.1.5.1",
            XPathExpr = @"concat('single')",
            ExpectedErrorCode = "XPST0017"
        },
        new TestCase
        {
            Id = "str1-030",
            Area = "xpath: strings core",
            Title = "string-length on a two-item sequence violates xs:string? cardinality: XPTY0004",
            SpecRef = "F&O 5.4.4; XPath 3.1 3.1.5.2",
            XPathExpr = @"string-length(('a','b'))",
            ExpectedErrorCode = "XPTY0004"
        }
    };
}
