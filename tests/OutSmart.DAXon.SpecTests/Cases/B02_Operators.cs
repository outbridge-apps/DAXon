// B02_Operators.cs
// Spec-derived test cases for XPath 3.1 operators: arithmetic, comparisons,
// logical, range, concatenation, simple map, arrow, conditional, and
// sequence-type expressions (instance of / cast / castable / treat).
// Expected values reasoned by hand from XPath 3.1 and F&O 3.1.

namespace OutSmart.DAXon.SpecTests.Cases;

public static class B02_Operators
{
    public static TestCase[] Cases() => new[]
    {
        // ---------- Arithmetic ----------
        new TestCase
        {
            Id = "ops-001",
            Area = "xpath: operators",
            Title = "Multiplication binds tighter than addition; parentheses override",
            SpecRef = "XPath 3.1 3.5",
            XPathExpr = @"(2 + 3 * 4, (2 + 3) * 4)",
            Expected = "14|20"
        },
        new TestCase
        {
            Id = "ops-002",
            Area = "xpath: operators",
            Title = "Additive and multiplicative operators are left-associative",
            SpecRef = "XPath 3.1 3.5",
            XPathExpr = @"(10 - 3 - 2, 12 div 4 * 3)",
            // (10-3)-2 = 5; (12 div 4)*3 = 3*3 = 9 (decimal)
            Expected = "5|9"
        },
        new TestCase
        {
            Id = "ops-003",
            Area = "xpath: operators",
            Title = "div on integers yields xs:decimal; exact fractional results",
            SpecRef = "F&O 4.2.4",
            XPathExpr = @"(7 div 2, 1 div 4, 5 div 2.5)",
            // 3.5; 0.25; decimal 2 serializes without decimal point
            Expected = "3.5|0.25|2"
        },
        new TestCase
        {
            Id = "ops-004",
            Area = "xpath: operators",
            Title = "idiv truncates toward zero for all sign combinations",
            SpecRef = "F&O 4.2.5",
            XPathExpr = @"(7 idiv 2, -7 idiv 2, 7 idiv -2, -7 idiv -2)",
            // 3.5 -> 3; -3.5 -> -3; -3.5 -> -3; 3.5 -> 3
            Expected = "3|-3|-3|3"
        },
        new TestCase
        {
            Id = "ops-005",
            Area = "xpath: operators",
            Title = "mod result sign follows dividend; works on decimals",
            SpecRef = "F&O 4.2.6",
            XPathExpr = @"(5 mod 3, -5 mod 3, 5 mod -3, -5 mod -3, 5.5 mod 2)",
            // a - (a idiv b)*b: 2; -2; 2; -2; 5.5 - 4 = 1.5
            Expected = "2|-2|2|-2|1.5"
        },
        new TestCase
        {
            Id = "ops-006",
            Area = "xpath: operators",
            Title = "Mixed-operand arithmetic promotes to decimal/double per type promotion",
            SpecRef = "XPath 3.1 3.5; B.1",
            XPathExpr = @"((1 + 0.5) instance of xs:decimal, (1 + 0.5e0) instance of xs:double, (1 + 1) instance of xs:integer, (0.5 + 0.5e0) instance of xs:double)",
            Expected = "true|true|true|true"
        },
        new TestCase
        {
            Id = "ops-007",
            Area = "xpath: operators",
            Title = "Unary minus: binding, repetition, and applied to parenthesized sum",
            SpecRef = "XPath 3.1 3.5",
            XPathExpr = @"(-3 * 2, 2 + -3, - -5, -(2 + 3))",
            Expected = "-6|-1|5|-5"
        },
        new TestCase
        {
            Id = "ops-008",
            Area = "xpath: operators",
            Title = "Integer division by zero raises FOAR0001",
            SpecRef = "F&O 4.2.4",
            XPathExpr = @"1 div 0",
            ExpectedErrorCode = "FOAR0001"
        },
        new TestCase
        {
            Id = "ops-009",
            Area = "xpath: operators",
            Title = "Double division by zero yields INF, -INF, NaN (no error)",
            SpecRef = "F&O 4.2.4",
            XPathExpr = @"(string(1e0 div 0e0), string(-1e0 div 0e0), string(0e0 div 0e0))",
            Expected = "INF|-INF|NaN"
        },

        // ---------- Value comparisons ----------
        new TestCase
        {
            Id = "ops-010",
            Area = "xpath: operators",
            Title = "Value comparisons eq/lt/ge/ne on numbers and codepoint strings",
            SpecRef = "XPath 3.1 3.7.1",
            XPathExpr = @"(1 eq 1, 2 lt 10, 'apple' lt 'banana', 3.5 ge 3.5, 2 ne 2)",
            Expected = "true|true|true|true|false"
        },
        new TestCase
        {
            Id = "ops-011",
            Area = "xpath: operators",
            Title = "Value comparison with an empty-sequence operand yields empty sequence",
            SpecRef = "XPath 3.1 3.7.1",
            XPathExpr = @"(count(() eq 1), count(1 lt ()), exists(() le ()))",
            Expected = "0|0|false"
        },
        new TestCase
        {
            Id = "ops-012",
            Area = "xpath: operators",
            Title = "Value comparison of incomparable types (integer eq string) raises XPTY0004",
            SpecRef = "XPath 3.1 3.7.1",
            XPathExpr = @"1 eq 'one'",
            ExpectedErrorCode = "XPTY0004"
        },

        // ---------- General comparisons ----------
        new TestCase
        {
            Id = "ops-013",
            Area = "xpath: operators",
            Title = "General comparisons are existential over sequences; empty RHS is false",
            SpecRef = "XPath 3.1 3.7.2",
            XPathExpr = @"((1, 2, 3) = (3, 4), (1, 2) != (1, 2), (1, 2) = (), 5 > (3, 8))",
            // 3=3 true; 1 != 2 true; nothing matches empty -> false; 5>3 true
            Expected = "true|true|false|true"
        },
        new TestCase
        {
            Id = "ops-014",
            Area = "xpath: operators",
            Title = "untypedAtomic in general comparison: cast to double vs numeric, to string vs string",
            SpecRef = "XPath 3.1 3.7.2",
            InputXml = @"<r><a>4</a><a>5</a></r>",
            XPathExpr = @"(/r/a = 5, /r/a < 4.5, /r/a = 'x', /r/a > 10)",
            Expected = "true|true|false|false"
        },

        // ---------- Node comparisons ----------
        new TestCase
        {
            Id = "ops-015",
            Area = "xpath: operators",
            Title = "is compares node identity, not value",
            SpecRef = "XPath 3.1 3.7.3",
            InputXml = @"<r><a/><b/></r>",
            XPathExpr = @"(/r/a is /r/a, /r/a is /r/b, /r/*[1] is /r/a)",
            Expected = "true|false|true"
        },
        new TestCase
        {
            Id = "ops-016",
            Area = "xpath: operators",
            Title = "<< and >> test document order; empty operand yields empty sequence",
            SpecRef = "XPath 3.1 3.7.3",
            InputXml = @"<r><a/><b/></r>",
            XPathExpr = @"(/r/a << /r/b, /r/a >> /r/b, /r/b >> /r/a, count(/r/a is ()))",
            Expected = "true|false|true|0"
        },

        // ---------- Logical and / or (EBV) ----------
        new TestCase
        {
            Id = "ops-017",
            Area = "xpath: operators",
            Title = "and/or use effective boolean value: node seq true, '' false, '0' true, 0 false",
            SpecRef = "XPath 3.1 3.8",
            XPathExpr = @"(/r and true(), '' or false(), 'x' and 1, 0 or '0')",
            // EBV(/r)=true (node); EBV('')=false; EBV('x')=EBV(1)=true; EBV(0)=false but EBV('0')=true
            Expected = "true|false|true|true"
        },
        new TestCase
        {
            Id = "ops-018",
            Area = "xpath: operators",
            Title = "EBV of multi-item atomic sequence in and-expression raises FORG0006",
            SpecRef = "F&O 7.3.1",
            XPathExpr = @"(1, 2) and true()",
            ExpectedErrorCode = "FORG0006"
        },

        // ---------- Range expression ----------
        new TestCase
        {
            Id = "ops-019",
            Area = "xpath: operators",
            Title = "Range expression produces increasing consecutive integers",
            SpecRef = "XPath 3.1 3.4.1",
            XPathExpr = @"1 to 4",
            Expected = "1|2|3|4"
        },
        new TestCase
        {
            Id = "ops-020",
            Area = "xpath: operators",
            Title = "Range edge cases: empty when start > end, singleton when equal, negatives",
            SpecRef = "XPath 3.1 3.4.1",
            XPathExpr = @"(count(3 to 1), count(2 to 2), sum(-1 to 1), count(1 to 0))",
            // 3 to 1 -> (); 2 to 2 -> (2); -1+0+1 = 0; 1 to 0 -> ()
            Expected = "0|1|0|0"
        },

        // ---------- String concatenation ----------
        new TestCase
        {
            Id = "ops-021",
            Area = "xpath: operators",
            Title = "|| concatenates with fn:concat semantics; empty sequence becomes ''",
            SpecRef = "XPath 3.1 3.6",
            XPathExpr = @"('ab' || 'cd' || 5, 'a' || (), () || 'b' || '')",
            Expected = "abcd5|a|b"
        },

        // ---------- Simple map operator ----------
        new TestCase
        {
            Id = "ops-022",
            Area = "xpath: operators",
            Title = "Simple map operator applies RHS to each item with . bound",
            SpecRef = "XPath 3.1 3.15",
            XPathExpr = @"(1 to 3) ! (. * .)",
            Expected = "1|4|9"
        },
        new TestCase
        {
            Id = "ops-023",
            Area = "xpath: operators",
            Title = "! preserves order and duplicates; / sorts into document order and dedups",
            SpecRef = "XPath 3.1 3.15",
            InputXml = @"<r><a>1</a><b>2</b></r>",
            XPathExpr = @"((/r/b, /r/a)/., (/r/b, /r/a) ! ., count((/r/a, /r/a)/.), count((/r/a, /r/a) ! .))",
            // /-version reorders to (a,b) -> 1,2; !-version keeps (b,a) -> 2,1; dedup 1 vs 2
            Expected = "1|2|2|1|1|2"
        },

        // ---------- Arrow operator ----------
        new TestCase
        {
            Id = "ops-024",
            Area = "xpath: operators",
            Title = "Arrow operator chains: LHS becomes first argument of each call",
            SpecRef = "XPath 3.1 3.16",
            XPathExpr = @"('hello' => upper-case() => substring(1, 3), (1, 2, 3) => count())",
            Expected = "HEL|3"
        },

        // ---------- Conditional expression ----------
        new TestCase
        {
            Id = "ops-025",
            Area = "xpath: operators",
            Title = "if/then/else selects branch by EBV of condition; () false, '0' true",
            SpecRef = "XPath 3.1 3.12",
            XPathExpr = @"(if (1 lt 2) then 'yes' else 'no', if (()) then 'y' else 'n', if ('0') then 'y' else 'n')",
            Expected = "yes|n|y"
        },

        // ---------- instance of ----------
        new TestCase
        {
            Id = "ops-026",
            Area = "xpath: operators",
            Title = "instance of respects literal types and derivation (integer is-a decimal)",
            SpecRef = "XPath 3.1 3.14.1",
            XPathExpr = @"(5 instance of xs:integer, 5 instance of xs:decimal, 5.0 instance of xs:integer, 5e0 instance of xs:double, 'x' instance of xs:string)",
            // 5.0 is xs:decimal, not xs:integer
            Expected = "true|true|false|true|true"
        },
        new TestCase
        {
            Id = "ops-027",
            Area = "xpath: operators",
            Title = "instance of occurrence indicators: + ? * and empty-sequence()",
            SpecRef = "XPath 3.1 3.14.1",
            XPathExpr = @"((1, 2) instance of xs:integer+, () instance of xs:integer?, () instance of empty-sequence(), (1, 2) instance of item()*, 1 instance of xs:string*, () instance of xs:integer+)",
            // + requires at least one item; integer is not a string
            Expected = "true|true|true|true|false|false"
        },

        // ---------- cast as ----------
        new TestCase
        {
            Id = "ops-028",
            Area = "xpath: operators",
            Title = "cast as converts between atomic types per casting table",
            SpecRef = "XPath 3.1 3.14.2",
            XPathExpr = @"('42' cast as xs:integer, 12 cast as xs:string, 'false' cast as xs:boolean, '1' cast as xs:boolean)",
            Expected = "42|12|false|true"
        },
        new TestCase
        {
            Id = "ops-029",
            Area = "xpath: operators",
            Title = "cast as with ? accepts empty sequence and yields empty",
            SpecRef = "XPath 3.1 3.14.2",
            XPathExpr = @"(count(() cast as xs:integer?), string(('7' cast as xs:integer?) + 1))",
            Expected = "0|8"
        },

        // ---------- castable as ----------
        new TestCase
        {
            Id = "ops-030",
            Area = "xpath: operators",
            Title = "castable as tests lexical validity; ? governs the empty sequence",
            SpecRef = "XPath 3.1 3.14.3",
            XPathExpr = @"('12' castable as xs:integer, 'abc' castable as xs:integer, '1.5' castable as xs:integer, '1.5' castable as xs:decimal, () castable as xs:integer?, () castable as xs:integer)",
            Expected = "true|false|false|true|true|false"
        },

        // ---------- treat as ----------
        new TestCase
        {
            Id = "ops-031",
            Area = "xpath: operators",
            Title = "treat as passes value through unchanged when the type matches",
            SpecRef = "XPath 3.1 3.14.5",
            XPathExpr = @"((5 treat as xs:integer) + 1, count(() treat as xs:integer?), count((1, 2) treat as item()*))",
            Expected = "6|0|2"
        },
        new TestCase
        {
            Id = "ops-032",
            Area = "xpath: operators",
            Title = "treat as raises XPDY0050 when the value does not match the sequence type",
            SpecRef = "XPath 3.1 3.14.5",
            XPathExpr = @"'five' treat as xs:integer",
            ExpectedErrorCode = "XPDY0050"
        }
    };
}
