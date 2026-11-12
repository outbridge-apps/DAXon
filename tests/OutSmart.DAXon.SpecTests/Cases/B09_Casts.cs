namespace OutSmart.DAXon.SpecTests.Cases;

// Spec-derived test cases: XPath 3.1 cast / castable expressions and XSD constructor
// functions (F&O section 18/19). Expected values reasoned from XPath 3.1, XSLT 3.0 and
// F&O 3.1 spec text; ambiguous serializations avoided via string()/count()/instance of.
public static class B09_Casts
{
    public static TestCase[] Cases() => new[]
    {
        new TestCase
        {
            Id = "cast-001",
            Area = "xpath: casts+constructors",
            Title = "xs:integer constructor collapses surrounding whitespace",
            SpecRef = "F&O 19.2",
            XPathExpr = @"xs:integer('  12  ')",
            Expected = "12"
        },
        new TestCase
        {
            Id = "cast-002",
            Area = "xpath: casts+constructors",
            Title = "numeric to string casts use canonical forms",
            SpecRef = "F&O 19.1.1",
            XPathExpr = @"(xs:string(42), xs:string(3.5), xs:string(2.5e0))",
            Expected = "42|3.5|2.5"
        },
        new TestCase
        {
            Id = "cast-003",
            Area = "xpath: casts+constructors",
            Title = "xs:integer from '12.5' raises FORG0001",
            SpecRef = "F&O 19.2",
            XPathExpr = @"xs:integer('12.5')",
            ExpectedErrorCode = "FORG0001"
        },
        new TestCase
        {
            Id = "cast-004",
            Area = "xpath: casts+constructors",
            Title = "xs:boolean from '1','0','true','false'",
            SpecRef = "F&O 19.2",
            XPathExpr = @"(xs:boolean('1'), xs:boolean('0'), xs:boolean('true'), xs:boolean('false'))",
            Expected = "true|false|true|false"
        },
        new TestCase
        {
            Id = "cast-005",
            Area = "xpath: casts+constructors",
            Title = "xs:boolean('TRUE') is not a valid lexical form",
            SpecRef = "F&O 19.2",
            XPathExpr = @"xs:boolean('TRUE')",
            ExpectedErrorCode = "FORG0001"
        },
        new TestCase
        {
            Id = "cast-006",
            Area = "xpath: casts+constructors",
            Title = "boolean casts to string and to numerics (1/0)",
            SpecRef = "F&O 19.1.2",
            XPathExpr = @"(xs:string(true()), xs:string(false()), xs:integer(true()), xs:integer(false()), string(xs:double(true())))",
            Expected = "true|false|1|0|1"
        },
        new TestCase
        {
            Id = "cast-007",
            Area = "xpath: casts+constructors",
            Title = "double to integer truncates toward zero",
            SpecRef = "F&O 19.1.2",
            XPathExpr = @"(xs:integer(2.9e0), xs:integer(-2.9e0))",
            Expected = "2|-2"
        },
        new TestCase
        {
            Id = "cast-008",
            Area = "xpath: casts+constructors",
            Title = "decimal to integer truncates toward zero",
            SpecRef = "F&O 19.1.2",
            XPathExpr = @"(xs:integer(7.99), xs:integer(-7.99))",
            Expected = "7|-7"
        },
        new TestCase
        {
            Id = "cast-009",
            Area = "xpath: casts+constructors",
            Title = "exact decimal/double round trips preserve value",
            SpecRef = "F&O 19.1.2",
            XPathExpr = @"(string(xs:decimal(1.5e0)), string(xs:double(xs:decimal('2.5'))))",
            Expected = "1.5|2.5"
        },
        new TestCase
        {
            Id = "cast-010",
            Area = "xpath: casts+constructors",
            Title = "untypedAtomic attribute in arithmetic is cast to xs:double",
            SpecRef = "XPath 3.1 3.5",
            XPathExpr = @"(/r/@a + 1, (/r/@a + 1) instance of xs:double)",
            InputXml = @"<r a=""5""/>",
            Expected = "6|true"
        },
        new TestCase
        {
            Id = "cast-011",
            Area = "xpath: casts+constructors",
            Title = "decimal operand promoted to double in mixed arithmetic",
            SpecRef = "XPath 3.1 B.1",
            XPathExpr = @"((1.5 + 0.25e0) instance of xs:double, string(1.5 + 0.25e0))",
            Expected = "true|1.75"
        },
        new TestCase
        {
            Id = "cast-012",
            Area = "xpath: casts+constructors",
            Title = "decimal plus float yields xs:float, not xs:double",
            SpecRef = "XPath 3.1 B.1",
            XPathExpr = @"((1.5 + xs:float('0.25')) instance of xs:float, (1.5 + xs:float('0.25')) instance of xs:double)",
            Expected = "true|false"
        },
        new TestCase
        {
            Id = "cast-013",
            Area = "xpath: casts+constructors",
            Title = "float operand promoted to double in mixed arithmetic",
            SpecRef = "XPath 3.1 B.1",
            XPathExpr = @"((xs:float('1.5') + 1.5e0) instance of xs:double, string(xs:float('1.5') + 1.5e0))",
            Expected = "true|3"
        },
        new TestCase
        {
            Id = "cast-014",
            Area = "xpath: casts+constructors",
            Title = "xs:anyURI compares equal to xs:string via URI promotion",
            SpecRef = "XPath 3.1 B.1",
            XPathExpr = @"xs:anyURI('http://example.org/') eq 'http://example.org/'",
            Expected = "true"
        },
        new TestCase
        {
            Id = "cast-015",
            Area = "xpath: casts+constructors",
            Title = "xs:anyURI cast collapses surrounding whitespace",
            SpecRef = "F&O 19.1.7",
            XPathExpr = @"string(xs:anyURI('  http://example.org/x  '))",
            Expected = "http://example.org/x"
        },
        new TestCase
        {
            Id = "cast-016",
            Area = "xpath: casts+constructors",
            Title = "xs:QName constructor resolves prefix from static context",
            SpecRef = "F&O 19.3",
            XPathExpr = @"(prefix-from-QName(xs:QName('xs:integer')), local-name-from-QName(xs:QName('xs:integer')), namespace-uri-from-QName(xs:QName('xs:integer')))",
            Expected = "xs|integer|http://www.w3.org/2001/XMLSchema"
        },
        new TestCase
        {
            Id = "cast-017",
            Area = "xpath: casts+constructors",
            Title = "castable as xs:QName: bound prefix true, malformed lexical false",
            SpecRef = "XPath 3.1 3.14.3",
            XPathExpr = @"('xs:integer' castable as xs:QName, 'not a qname' castable as xs:QName)",
            Expected = "true|false"
        },
        new TestCase
        {
            Id = "cast-018",
            Area = "xpath: casts+constructors",
            Title = "hexBinary 4142 casts to base64Binary QUI=",
            SpecRef = "F&O 19.1.6",
            XPathExpr = @"string(xs:base64Binary(xs:hexBinary('4142')))",
            Expected = "QUI="
        },
        new TestCase
        {
            Id = "cast-019",
            Area = "xpath: casts+constructors",
            Title = "base64Binary QUJD casts to hexBinary 414243",
            SpecRef = "F&O 19.1.6",
            XPathExpr = @"string(xs:hexBinary(xs:base64Binary('QUJD')))",
            Expected = "414243"
        },
        new TestCase
        {
            Id = "cast-020",
            Area = "xpath: casts+constructors",
            Title = "hexBinary canonical string form is upper-case",
            SpecRef = "F&O 19.1.1",
            XPathExpr = @"string(xs:hexBinary('4a'))",
            Expected = "4A"
        },
        new TestCase
        {
            Id = "cast-021",
            Area = "xpath: casts+constructors",
            Title = "castable as xs:integer over valid and invalid lexicals",
            SpecRef = "XPath 3.1 3.14.3",
            XPathExpr = @"('12' castable as xs:integer, 'abc' castable as xs:integer, '12.5' castable as xs:integer)",
            Expected = "true|false|false"
        },
        new TestCase
        {
            Id = "cast-022",
            Area = "xpath: casts+constructors",
            Title = "empty sequence castable as xs:integer? true, without ? false",
            SpecRef = "XPath 3.1 3.14.3",
            XPathExpr = @"(() castable as xs:integer?, () castable as xs:integer)",
            Expected = "true|false"
        },
        new TestCase
        {
            Id = "cast-023",
            Area = "xpath: casts+constructors",
            Title = "cast of empty sequence without ? is a type error",
            SpecRef = "XPath 3.1 3.14.2",
            XPathExpr = @"() cast as xs:integer",
            ExpectedErrorCode = "XPTY0004"
        },
        new TestCase
        {
            Id = "cast-024",
            Area = "xpath: casts+constructors",
            Title = "cast of empty sequence with ? yields empty sequence",
            SpecRef = "XPath 3.1 3.14.2",
            XPathExpr = @"count(() cast as xs:integer?)",
            Expected = "0"
        },
        new TestCase
        {
            Id = "cast-025",
            Area = "xpath: casts+constructors",
            Title = "cast of non-numeric string to xs:double raises FORG0001",
            SpecRef = "F&O 19.2",
            XPathExpr = @"'abc' cast as xs:double",
            ExpectedErrorCode = "FORG0001"
        },
        new TestCase
        {
            Id = "cast-026",
            Area = "xpath: casts+constructors",
            Title = "xs:untypedAtomic cast preserves string verbatim (no ws processing)",
            SpecRef = "F&O 19.1.1",
            XPathExpr = @"string-length(string(xs:untypedAtomic('  a b  ')))",
            Expected = "7"
        },
        new TestCase
        {
            Id = "cast-027",
            Area = "xpath: casts+constructors",
            Title = "xs:normalizedString replaces tab with space (no collapse)",
            SpecRef = "F&O 19.2",
            XPathExpr = @"(string-length(xs:normalizedString(codepoints-to-string((97, 9, 98)))), xs:normalizedString(codepoints-to-string((97, 9, 98))) eq 'a b')",
            Expected = "3|true"
        },
        new TestCase
        {
            Id = "cast-028",
            Area = "xpath: casts+constructors",
            Title = "xs:token collapses internal and surrounding whitespace",
            SpecRef = "F&O 19.2",
            XPathExpr = @"string(xs:token('  a   b  '))",
            Expected = "a b"
        },
        new TestCase
        {
            Id = "cast-029",
            Area = "xpath: casts+constructors",
            Title = "INF/-INF/NaN round-trip through xs:double and xs:float",
            SpecRef = "F&O 19.2",
            XPathExpr = @"(string(xs:double('INF')), string(xs:double('-INF')), string(xs:double('NaN')), string(xs:float('INF')))",
            Expected = "INF|-INF|NaN|INF"
        },
        new TestCase
        {
            Id = "cast-030",
            Area = "xpath: casts+constructors",
            Title = "numeric to boolean: zero and NaN false, non-zero true",
            SpecRef = "F&O 19.1.5",
            XPathExpr = @"(xs:boolean(0), xs:boolean(number('NaN')), xs:boolean(5), xs:boolean(0.0e0))",
            Expected = "false|false|true|false"
        }
    };
}
