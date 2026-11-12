// Spec-derived test cases: numeric functions/operators (F&O 4.x) and math: namespace.
// Expected values derived by hand from XPath 3.1 / F&O 3.1; doubles restricted to
// exactly-representable results so serialization is unambiguous.
namespace OutSmart.DAXon.SpecTests.Cases;

public static class B07_NumericMath
{
    public static TestCase[] Cases() => new[]
    {
        new TestCase
        {
            Id = "num-001",
            Area = "xpath: numeric+math",
            Title = "abs on negative decimal and on negative-zero double",
            SpecRef = "F&O 4.4.1",
            XPathExpr = @"(abs(-10.5), abs(-0e0))",
            Expected = "10.5|0"
        },
        new TestCase
        {
            Id = "num-002",
            Area = "xpath: numeric+math",
            Title = "abs(-INF) returns positive INF",
            SpecRef = "F&O 4.4.1",
            XPathExpr = @"abs(xs:double('-INF'))",
            Expected = "INF"
        },
        new TestCase
        {
            Id = "num-003",
            Area = "xpath: numeric+math",
            Title = "ceiling and floor on negative decimal fractions",
            SpecRef = "F&O 4.4.2, 4.4.3",
            XPathExpr = @"(ceiling(-1.5), floor(-1.1))",
            Expected = "-1|-2"
        },
        new TestCase
        {
            Id = "num-004",
            Area = "xpath: numeric+math",
            Title = "round: exact halves round toward positive infinity",
            SpecRef = "F&O 4.4.4",
            XPathExpr = @"(round(2.5), round(-2.5))",
            Expected = "3|-2"
        },
        new TestCase
        {
            Id = "num-005",
            Area = "xpath: numeric+math",
            Title = "ceiling of double strictly between -1 and 0 is negative zero",
            SpecRef = "F&O 4.4.2",
            XPathExpr = @"string(ceiling(-0.5e0))",
            Expected = "-0"
        },
        new TestCase
        {
            Id = "num-006",
            Area = "xpath: numeric+math",
            Title = "round with negative precision on xs:integer",
            SpecRef = "F&O 4.4.4",
            XPathExpr = @"round(1234, -2)",
            Expected = "1200"
        },
        new TestCase
        {
            Id = "num-007",
            Area = "xpath: numeric+math",
            Title = "round 2-arg on decimal: tie at third digit rounds up",
            SpecRef = "F&O 4.4.4",
            XPathExpr = @"round(1.125, 2)",
            Expected = "1.13"
        },
        new TestCase
        {
            Id = "num-008",
            Area = "xpath: numeric+math",
            Title = "round-half-to-even banker's rounding at 2.5 and 3.5",
            SpecRef = "F&O 4.4.5",
            XPathExpr = @"(round-half-to-even(2.5), round-half-to-even(3.5))",
            Expected = "2|4"
        },
        new TestCase
        {
            Id = "num-009",
            Area = "xpath: numeric+math",
            Title = "round-half-to-even with positive precision on decimal",
            SpecRef = "F&O 4.4.5",
            XPathExpr = @"round-half-to-even(0.125, 2)",
            Expected = "0.12"
        },
        new TestCase
        {
            Id = "num-010",
            Area = "xpath: numeric+math",
            Title = "round-half-to-even negative precision tie picks even multiple",
            SpecRef = "F&O 4.4.5",
            XPathExpr = @"round-half-to-even(150, -2)",
            Expected = "200"
        },
        new TestCase
        {
            Id = "num-011",
            Area = "xpath: numeric+math",
            Title = "number() yields NaN for unparsable string and for empty sequence",
            SpecRef = "F&O 4.5.1",
            XPathExpr = @"(number('abc'), number(()))",
            Expected = "NaN|NaN"
        },
        new TestCase
        {
            Id = "num-012",
            Area = "xpath: numeric+math",
            Title = "number(true()) is 1",
            SpecRef = "F&O 4.5.1",
            XPathExpr = @"number(true())",
            Expected = "1"
        },
        new TestCase
        {
            Id = "num-013",
            Area = "xpath: numeric+math",
            Title = "number() trims surrounding whitespace before parsing",
            SpecRef = "F&O 4.5.1",
            XPathExpr = @"number('  42  ')",
            Expected = "42"
        },
        new TestCase
        {
            Id = "num-014",
            Area = "xpath: numeric+math",
            Title = "integer div integer yields xs:decimal",
            SpecRef = "F&O 4.2.4",
            XPathExpr = @"5 div 4",
            Expected = "1.25"
        },
        new TestCase
        {
            Id = "num-015",
            Area = "xpath: numeric+math",
            Title = "idiv truncates toward zero for both signs",
            SpecRef = "F&O 4.2.5",
            XPathExpr = @"(7 idiv 2, (-7) idiv 2)",
            Expected = "3|-3"
        },
        new TestCase
        {
            Id = "num-016",
            Area = "xpath: numeric+math",
            Title = "mod result takes the sign of the dividend",
            SpecRef = "F&O 4.2.6",
            XPathExpr = @"(-10) mod 3",
            Expected = "-1"
        },
        new TestCase
        {
            Id = "num-017",
            Area = "xpath: numeric+math",
            Title = "double division by zero yields INF and -INF",
            SpecRef = "F&O 4.2.4",
            XPathExpr = @"(1e0 div 0, (-1e0) div 0)",
            Expected = "INF|-INF"
        },
        new TestCase
        {
            Id = "num-018",
            Area = "xpath: numeric+math",
            Title = "zero double divided by zero double is NaN",
            SpecRef = "F&O 4.2.4",
            XPathExpr = @"0e0 div 0e0",
            Expected = "NaN"
        },
        new TestCase
        {
            Id = "num-019",
            Area = "xpath: numeric+math",
            Title = "finite double mod INF returns the dividend",
            SpecRef = "F&O 4.2.6",
            XPathExpr = @"5e0 mod xs:double('INF')",
            Expected = "5"
        },
        new TestCase
        {
            Id = "num-020",
            Area = "xpath: numeric+math",
            Title = "math:pi() approximation probed via round",
            SpecRef = "F&O 4.7.1",
            XPathExpr = @"round(math:pi() * 100)",
            Expected = "314"
        },
        new TestCase
        {
            Id = "num-021",
            Area = "xpath: numeric+math",
            Title = "math:sqrt of 4 is 2; sqrt of negative is NaN",
            SpecRef = "F&O 4.7.7",
            XPathExpr = @"(math:sqrt(4), math:sqrt(-1))",
            Expected = "2|NaN"
        },
        new TestCase
        {
            Id = "num-022",
            Area = "xpath: numeric+math",
            Title = "math:pow exact integer power; zero exponent yields 1 even for 0 base",
            SpecRef = "F&O 4.7.6",
            XPathExpr = @"(math:pow(2, 10), math:pow(0e0, 0))",
            Expected = "1024|1"
        },
        new TestCase
        {
            Id = "num-023",
            Area = "xpath: numeric+math",
            Title = "math:exp(0) is 1 and math:log(1) is 0",
            SpecRef = "F&O 4.7.2, 4.7.4",
            XPathExpr = @"(math:exp(0), math:log(1))",
            Expected = "1|0"
        },
        new TestCase
        {
            Id = "num-024",
            Area = "xpath: numeric+math",
            Title = "math:sin(0), math:cos(0), math:atan2(0,1) exact values",
            SpecRef = "F&O 4.7.8, 4.7.9, 4.7.14",
            XPathExpr = @"(math:sin(0), math:cos(0), math:atan2(0, 1))",
            Expected = "0|1|0"
        },
        new TestCase
        {
            Id = "num-025",
            Area = "xpath: numeric+math",
            Title = "NaN eq NaN is false, NaN ne NaN is true",
            SpecRef = "F&O 4.3.1",
            XPathExpr = @"(xs:double('NaN') eq xs:double('NaN'), xs:double('NaN') ne xs:double('NaN'))",
            Expected = "false|true"
        },
        new TestCase
        {
            Id = "num-026",
            Area = "xpath: numeric+math",
            Title = "deep-equal treats NaN as equal to NaN",
            SpecRef = "F&O 14.2.3",
            XPathExpr = @"deep-equal(xs:double('NaN'), xs:double('NaN'))",
            Expected = "true"
        },
        new TestCase
        {
            Id = "num-027",
            Area = "xpath: numeric+math",
            Title = "INF and -INF serialize as 'INF' and '-INF'",
            SpecRef = "F&O 19.1.2",
            XPathExpr = @"(string(xs:double('INF')), string(xs:double('-INF')))",
            Expected = "INF|-INF"
        },
        new TestCase
        {
            Id = "num-028",
            Area = "xpath: numeric+math",
            Title = "integer idiv zero raises FOAR0001",
            SpecRef = "F&O 4.2.5",
            XPathExpr = @"1 idiv 0",
            ExpectedErrorCode = "FOAR0001"
        },
        new TestCase
        {
            Id = "num-029",
            Area = "xpath: numeric+math",
            Title = "integer div zero (decimal division) raises FOAR0001",
            SpecRef = "F&O 4.2.4",
            XPathExpr = @"1 div 0",
            ExpectedErrorCode = "FOAR0001"
        },
        new TestCase
        {
            Id = "num-030",
            Area = "xpath: numeric+math",
            Title = "NaN operand to idiv raises FOAR0002",
            SpecRef = "F&O 4.2.5",
            XPathExpr = @"xs:double('NaN') idiv 1",
            ExpectedErrorCode = "FOAR0002"
        }
    };
}
