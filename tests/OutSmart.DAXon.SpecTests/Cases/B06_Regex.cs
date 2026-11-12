// B06_Regex.cs
// Spec-derived tests for XPath 3.1 regular expression functions (F&O 5.6):
// fn:matches, fn:replace, fn:tokenize, plus xsl:analyze-string / regex-group (XSLT 3.0 15.x).
// Expected values worked out by hand from the W3C spec text and its normative examples.

namespace OutSmart.DAXon.SpecTests.Cases;

public static class B06_Regex
{
    public static TestCase[] Cases() => new[]
    {
        // ---------- fn:matches: anchoring and anchors ----------
        new TestCase
        {
            Id = "rex-001",
            Area = "xpath: regex",
            Title = "matches is not implicitly anchored; ^ and $ anchor explicitly",
            SpecRef = "F&O 5.6.3",
            // 'b' found as substring -> true; '^b' no b at start -> false; 'c$' -> true; full anchor -> true
            XPathExpr = @"(matches('abc','b'), matches('abc','^b'), matches('abc','c$'), matches('abc','^abc$'))",
            Expected = "true|false|true|true"
        },

        // ---------- flags ----------
        new TestCase
        {
            Id = "rex-002",
            Area = "xpath: regex",
            Title = "flag i enables case-insensitive matching",
            SpecRef = "F&O 5.6.1.1",
            XPathExpr = @"(matches('ABC','abc','i'), matches('ABC','abc'))",
            Expected = "true|false"
        },
        new TestCase
        {
            Id = "rex-003",
            Area = "xpath: regex",
            Title = "flag m makes ^ match after a newline (multiline)",
            SpecRef = "F&O 5.6.1.1",
            // input is 'a' NL 'b'; with m, ^b matches at start of second line; without m it does not
            XPathExpr = @"(matches(concat('a',codepoints-to-string(10),'b'),'^b','m'), matches(concat('a',codepoints-to-string(10),'b'),'^b'))",
            Expected = "true|false"
        },
        new TestCase
        {
            Id = "rex-004",
            Area = "xpath: regex",
            Title = "flag s makes dot match newline (dot-all)",
            SpecRef = "F&O 5.6.1.1",
            XPathExpr = @"(matches(concat('a',codepoints-to-string(10),'b'),'^a.b$','s'), matches(concat('a',codepoints-to-string(10),'b'),'a.b'))",
            Expected = "true|false"
        },
        new TestCase
        {
            Id = "rex-005",
            Area = "xpath: regex",
            Title = "flag x strips whitespace in pattern except inside character class",
            SpecRef = "F&O 5.6.1.1",
            // 'a b' becomes 'ab' (matches 'ab', not 'a b'); space inside [ ] is preserved
            XPathExpr = @"(matches('ab','a b','x'), matches('a b','a b','x'), matches(' ','[ ]','x'))",
            Expected = "true|false|true"
        },
        new TestCase
        {
            Id = "rex-006",
            Area = "xpath: regex",
            Title = "flag q treats the pattern as a literal string",
            SpecRef = "F&O 5.6.1.1",
            // with q, '.' is a literal dot; without q it matches 'x'
            XPathExpr = @"(matches('a.b','a.b','q'), matches('axb','a.b','q'), matches('axb','a.b'))",
            Expected = "true|false|true"
        },

        // ---------- escapes and empty-string inputs ----------
        new TestCase
        {
            Id = "rex-007",
            Area = "xpath: regex",
            Title = "backslash-escaped metacharacter; matching against the empty string",
            SpecRef = "F&O 5.6.1",
            // a\.b matches only a literal dot; '' contains a zero-length match for a* but not a+
            XPathExpr = @"(matches('a.b','a\.b'), matches('axb','a\.b'), matches('','a*'), matches('','a+'))",
            Expected = "true|false|true|false"
        },

        // ---------- character classes ----------
        new TestCase
        {
            Id = "rex-008",
            Area = "xpath: regex",
            Title = "multi-char escapes \\d \\D \\w \\W \\s \\S",
            SpecRef = "F&O 5.6.1",
            // '7' is \d; 'a' is \D; '-' is punctuation (Pd) so not \w but is \W; tab is \s
            XPathExpr = @"(matches('7','\d'), matches('a','\D'), matches('-','\w'), matches('-','\W'), matches('a','\s'), matches(codepoints-to-string(9),'\s'))",
            Expected = "true|true|false|true|false|true"
        },
        new TestCase
        {
            Id = "rex-009",
            Area = "xpath: regex",
            Title = "unicode category \\p{Lu} and block \\p{IsBasicLatin}",
            SpecRef = "F&O 5.6.1",
            // U+00E9 (e-acute) is in Latin-1 Supplement, not Basic Latin
            XPathExpr = @"(matches('A','\p{Lu}'), matches('a','\p{Lu}'), matches('a','^\p{IsBasicLatin}$'), matches(codepoints-to-string(233),'^\p{IsBasicLatin}$'))",
            Expected = "true|false|true|false"
        },
        new TestCase
        {
            Id = "rex-010",
            Area = "xpath: regex",
            Title = "character class subtraction [a-z-[aeiou]] matches consonants only",
            SpecRef = "F&O 5.6.1",
            XPathExpr = @"(matches('b','^[a-z-[aeiou]]$'), matches('e','^[a-z-[aeiou]]$'))",
            Expected = "true|false"
        },

        // ---------- backreference, quantifiers, alternation ----------
        new TestCase
        {
            Id = "rex-011",
            Area = "xpath: regex",
            Title = "backreference \\1 within pattern requires repeated captured text",
            SpecRef = "F&O 5.6.1",
            XPathExpr = @"(matches('aa','^(a)\1$'), matches('ab','^(a)\1$'))",
            Expected = "true|false"
        },
        new TestCase
        {
            Id = "rex-012",
            Area = "xpath: regex",
            Title = "bounded quantifier {2,3} and alternation with anchors",
            SpecRef = "F&O 5.6.1",
            XPathExpr = @"(matches('aa','^a{2,3}$'), matches('aaaa','^a{2,3}$'), matches('dog','^(cat|dog)$'), matches('cow','^(cat|dog)$'))",
            Expected = "true|false|true|false"
        },

        // ---------- fn:replace ----------
        new TestCase
        {
            Id = "rex-013",
            Area = "xpath: regex",
            Title = "replace: plain substitution and greedy .* consuming whole string",
            SpecRef = "F&O 5.6.5",
            // spec examples: 'a*cada*' and '*'
            XPathExpr = @"concat(replace('abracadabra','bra','*'), '-', replace('abracadabra','a.*a','*'))",
            Expected = "a*cada*-*"
        },
        new TestCase
        {
            Id = "rex-014",
            Area = "xpath: regex",
            Title = "replace: $1 group reference used twice per match",
            SpecRef = "F&O 5.6.5",
            // spec example: matches ab/ac/ad/ab doubled, trailing 'ra' untouched
            XPathExpr = @"replace('abracadabra','a(.)','a$1$1')",
            Expected = "abbraccaddabbra"
        },
        new TestCase
        {
            Id = "rex-015",
            Area = "xpath: regex",
            Title = "replace: group swap $2$1 and whole-match reference $0",
            SpecRef = "F&O 5.6.5",
            XPathExpr = @"concat(replace('abcd','(ab)(cd)','$2$1'), '-', replace('abc','b','[$0]'))",
            Expected = "cdab-a[b]c"
        },
        new TestCase
        {
            Id = "rex-016",
            Area = "xpath: regex",
            Title = "replace: \\$ yields literal dollar, \\\\ yields literal backslash",
            SpecRef = "F&O 5.6.5",
            XPathExpr = @"concat(replace('abc','b','\$'), '-', replace('abc','b','\\'))",
            Expected = @"a$c-a\c"
        },
        new TestCase
        {
            Id = "rex-017",
            Area = "xpath: regex",
            Title = "replace: reference to nonexistent capturing group becomes empty string",
            SpecRef = "F&O 5.6.5",
            // pattern has 1 group; $2 refers to no group -> zero-length string
            XPathExpr = @"replace('abc','(b)','[$2]')",
            Expected = "a[]c"
        },
        new TestCase
        {
            Id = "rex-018",
            Area = "xpath: regex",
            Title = "replace with flag q: literal pattern and literal replacement ($ not special)",
            SpecRef = "F&O 5.6.5",
            XPathExpr = @"concat(replace('a.b.c','.','X','q'), '-', replace('abc','b','$1','q'))",
            Expected = "aXbXc-a$1c"
        },
        new TestCase
        {
            Id = "rex-019",
            Area = "xpath: regex",
            Title = "replace: greedy A+ one match vs reluctant A+? four matches",
            SpecRef = "F&O 5.6.5",
            // spec examples: 'b' and 'bbbb'
            XPathExpr = @"concat(replace('AAAA','A+','b'), '-', replace('AAAA','A+?','b'))",
            Expected = "b-bbbb"
        },
        new TestCase
        {
            Id = "rex-020",
            Area = "xpath: regex",
            Title = "replace: reluctant .*? with anchors and group references",
            SpecRef = "F&O 5.6.5",
            // spec example: (.*?) captures '' before the first 'd' -> 'carted'
            XPathExpr = @"replace('darted','^(.*?)d(.*)$','$1c$2')",
            Expected = "carted"
        },

        // ---------- fn:tokenize ----------
        new TestCase
        {
            Id = "rex-021",
            Area = "xpath: regex",
            Title = "tokenize keeps leading and trailing zero-length tokens",
            SpecRef = "F&O 5.6.4",
            // tokenize(',a,b,', ',') = ('', 'a', 'b', '') -> joined '/a/b/', count 4
            XPathExpr = @"concat('[', string-join(tokenize(',a,b,',','),'/'), ']#', count(tokenize(',a,b,',',')))",
            Expected = "[/a/b/]#4"
        },
        new TestCase
        {
            Id = "rex-022",
            Area = "xpath: regex",
            Title = "tokenize with alternation pattern (ab)|(a): spec example",
            SpecRef = "F&O 5.6.4",
            // ('', 'r', 'c', 'd', 'r', '') joined with '.'
            XPathExpr = @"string-join(tokenize('abracadabra','(ab)|(a)'),'.')",
            Expected = ".r.c.d.r."
        },
        new TestCase
        {
            Id = "rex-023",
            Area = "xpath: regex",
            Title = "tokenize: one-arg whitespace form, zero-length input, flag i",
            SpecRef = "F&O 5.6.4",
            // one-arg = tokenize(normalize-space(.), ' '); '' -> empty sequence; 'i' splits on X and x
            XPathExpr = @"concat(string-join(tokenize(' red   green blue '),'-'), '#', count(tokenize('',',')), '#', string-join(tokenize('aXbxc','x','i'),'-'))",
            Expected = "red-green-blue#0#a-b-c"
        },

        // ---------- xsl:analyze-string ----------
        new TestCase
        {
            Id = "rex-024",
            Area = "xpath: regex",
            Title = "xsl:analyze-string alternates matching and non-matching substrings",
            SpecRef = "XSLT 3.0 15.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform""><xsl:output method=""text""/><xsl:template match=""/""><xsl:analyze-string select=""'a1b22c'"" regex=""\d+""><xsl:matching-substring>[<xsl:value-of select="".""/>]</xsl:matching-substring><xsl:non-matching-substring>(<xsl:value-of select="".""/>)</xsl:non-matching-substring></xsl:analyze-string></xsl:template></xsl:stylesheet>",
            Expected = "(a)[1](b)[22](c)"
        },
        new TestCase
        {
            Id = "rex-025",
            Area = "xpath: regex",
            Title = "regex-group(N) returns captured substrings inside matching-substring",
            SpecRef = "XSLT 3.0 15.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform""><xsl:output method=""text""/><xsl:template match=""/""><xsl:analyze-string select=""'2024-01-15'"" regex=""(\d+)-(\d+)-(\d+)""><xsl:matching-substring><xsl:value-of select=""regex-group(3), regex-group(2), regex-group(1)"" separator=""/""/></xsl:matching-substring></xsl:analyze-string></xsl:template></xsl:stylesheet>",
            Expected = "15/01/2024"
        },
        new TestCase
        {
            Id = "rex-026",
            Area = "xpath: regex",
            Title = "regex-group with out-of-range group number returns zero-length string",
            SpecRef = "XSLT 3.0 15.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform""><xsl:output method=""text""/><xsl:template match=""/""><xsl:analyze-string select=""'ab'"" regex=""(a)(b)""><xsl:matching-substring>[<xsl:value-of select=""regex-group(5)""/>]<xsl:value-of select=""regex-group(1)""/></xsl:matching-substring></xsl:analyze-string></xsl:template></xsl:stylesheet>",
            Expected = "[]a"
        },

        // ---------- spec-mandated errors ----------
        new TestCase
        {
            Id = "rex-027",
            Area = "xpath: regex",
            Title = "invalid regular expression (unclosed paren) raises FORX0002",
            SpecRef = "F&O 5.6.3",
            XPathExpr = @"matches('a','(a')",
            ExpectedErrorCode = "FORX0002"
        },
        new TestCase
        {
            Id = "rex-028",
            Area = "xpath: regex",
            Title = "invalid flag character raises FORX0001",
            SpecRef = "F&O 5.6.3",
            XPathExpr = @"matches('a','a','z')",
            ExpectedErrorCode = "FORX0001"
        },
        new TestCase
        {
            Id = "rex-029",
            Area = "xpath: regex",
            Title = "replace pattern matching a zero-length string raises FORX0003",
            SpecRef = "F&O 5.6.5",
            XPathExpr = @"replace('a','x?','y')",
            ExpectedErrorCode = "FORX0003"
        },
        new TestCase
        {
            Id = "rex-030",
            Area = "xpath: regex",
            Title = "replacement string with $ not followed by digit raises FORX0004",
            SpecRef = "F&O 5.6.5",
            XPathExpr = @"replace('abc','b','x$y')",
            ExpectedErrorCode = "FORX0004"
        },

        // ---------- escaped metacharacters inside a literal run (the compiler merges them into one atom) ----------
        new TestCase
        {
            Id = "rex-031",
            Area = "xpath: regex",
            Title = "escaped metacharacters match literally inside a run of characters",
            SpecRef = "F&O 5.6.1 (XSD 1.1 F.3 SingleCharEsc)",
            XPathExpr = @"(replace('a.b.c','a\.b','X'), replace('a+b(c)','\+b\(c\)','-'), string-join(tokenize('1.2.3','\.'),','))",
            Expected = "X.c|a-|1,2,3"
        },
        new TestCase
        {
            Id = "rex-032",
            Area = "xpath: regex",
            Title = "a quantifier after an escape binds to the escaped character alone",
            SpecRef = "F&O 5.6.1 (XSD 1.1 F.1 piece)",
            XPathExpr = @"(matches('ab...','^ab\.*$'), matches('ab','^ab\.*$'), matches('ab.ab.','^(ab\.)*$'), matches('abb','^ab\.?b$'), matches('ab.b','^ab\.+$'))",
            Expected = "true|true|true|true|false"
        },
        new TestCase
        {
            Id = "rex-033",
            Area = "xpath: regex",
            Title = "escapes next to capturing groups and back-references",
            SpecRef = "F&O 5.6.1, 5.6.2",
            XPathExpr = @"(replace('x(1)y(22)z','\((\d+)\)','[$1]'), matches('a.a.','^(a\.)\1$'), matches('a.ab','^(a\.)\1$'))",
            Expected = "x[1]y[22]z|true|false"
        },
        new TestCase
        {
            Id = "rex-034",
            Area = "xpath: regex",
            Title = "newline, hyphen and backslash escapes in a run, under the i and x flags",
            SpecRef = "F&O 5.6.1, 5.6.1.1",
            XPathExpr = @"(matches('a' || codepoints-to-string(10) || 'b', '^a\nb$'), matches('a-b','^a\-b$'), matches('a\b','^a\\b$'), matches('A.B','^a\.b$','i'), matches('a.b','^a\.b$','x'))",
            Expected = "true|true|true|true|true"
        }
    };
}
