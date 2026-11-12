namespace OutSmart.DAXon.SpecTests.Cases;

// Spec-derived tests for F&O node, QName and URI functions plus EBV.
// Expected values reasoned from XPath 3.1 / F&O 3.1 spec text; all
// constructs chosen so serialization is deterministic (codepoint collation,
// no timezone/base-uri dependence, no map ordering).
public static class B14_NodeFns
{
    public static TestCase[] Cases() => new[]
    {
        new TestCase
        {
            Id = "node-001",
            Area = "xpath: node+qname+uri fns",
            Title = "name() on prefixed element returns lexical QName",
            SpecRef = "F&O 13.1",
            XPathExpr = @"name(/r/*[1])",
            InputXml = @"<r xmlns:p=""http://ex""><p:a/></r>",
            Expected = "p:a"
        },
        new TestCase
        {
            Id = "node-002",
            Area = "xpath: node+qname+uri fns",
            Title = "name() on no-namespace element and attribute",
            SpecRef = "F&O 13.1",
            XPathExpr = @"(name(/r/a), name(/r/a/@id))",
            InputXml = @"<r><a id=""1""/></r>",
            Expected = "a|id"
        },
        new TestCase
        {
            Id = "node-003",
            Area = "xpath: node+qname+uri fns",
            Title = "name() of document node and of empty sequence is zero-length string",
            SpecRef = "F&O 13.1",
            XPathExpr = @"concat('[', name(/), ']', '[', name(()), ']')",
            Expected = "[][]"
        },
        new TestCase
        {
            Id = "node-004",
            Area = "xpath: node+qname+uri fns",
            Title = "local-name() and namespace-uri() on prefixed element; empty uri on no-ns element",
            SpecRef = "F&O 13.2, 13.3",
            XPathExpr = @"(local-name(/r/*[1]), namespace-uri(/r/*[1]), concat('[', namespace-uri(/r), ']'))",
            InputXml = @"<r xmlns:p=""http://ex""><p:a/></r>",
            Expected = "a|http://ex|[]"
        },
        new TestCase
        {
            Id = "node-005",
            Area = "xpath: node+qname+uri fns",
            Title = "node-name() yields xs:QName with prefix; empty for document node",
            SpecRef = "F&O 2.1",
            XPathExpr = @"(string(node-name(/r/*[1])), count(node-name(/)))",
            InputXml = @"<r xmlns:p=""http://ex""><p:a/></r>",
            Expected = "p:a|0"
        },
        new TestCase
        {
            Id = "node-006",
            Area = "xpath: node+qname+uri fns",
            Title = "root() without arg and root(attribute) both return the document node",
            SpecRef = "F&O 13.5",
            XPathExpr = @"(root() is /, root(/r/a/@id) is /)",
            InputXml = @"<r><a id=""1""/></r>",
            Expected = "true|true"
        },
        new TestCase
        {
            Id = "node-007",
            Area = "xpath: node+qname+uri fns",
            Title = "has-children() on document, non-empty element, empty element",
            SpecRef = "F&O 13.7",
            XPathExpr = @"(has-children(/), has-children(/r), has-children(/r/a))",
            InputXml = @"<r><a/></r>",
            Expected = "true|true|false"
        },
        new TestCase
        {
            Id = "node-008",
            Area = "xpath: node+qname+uri fns",
            Title = "innermost() keeps only nodes with no descendant in the input",
            SpecRef = "F&O 13.8",
            XPathExpr = @"string-join(innermost((/r, /r/a, /r/a/b, /r/c))/name(), ',')",
            InputXml = @"<r><a><b/></a><c/></r>",
            Expected = "b,c"
        },
        new TestCase
        {
            Id = "node-009",
            Area = "xpath: node+qname+uri fns",
            Title = "outermost() keeps only nodes with no ancestor in the input",
            SpecRef = "F&O 13.9",
            XPathExpr = @"string-join(outermost((/r/a, /r/a/b, /r/c))/name(), ',')",
            InputXml = @"<r><a><b/></a><c/></r>",
            Expected = "a,c"
        },
        new TestCase
        {
            Id = "node-010",
            Area = "xpath: node+qname+uri fns",
            Title = "path() of second same-named element uses Q{} notation and sibling position",
            SpecRef = "F&O 13.6",
            XPathExpr = @"path(/r/a[2])",
            InputXml = @"<r><a id=""5"">t</a><a/></r>",
            Expected = "/Q{}r[1]/Q{}a[2]"
        },
        new TestCase
        {
            Id = "node-011",
            Area = "xpath: node+qname+uri fns",
            Title = "path() of document node, no-ns attribute, and text node",
            SpecRef = "F&O 13.6",
            XPathExpr = @"(path(/), path(/r/a[1]/@id), path(/r/a[1]/text()))",
            InputXml = @"<r><a id=""5"">t</a><a/></r>",
            Expected = "/|/Q{}r[1]/Q{}a[1]/@id|/Q{}r[1]/Q{}a[1]/text()[1]"
        },
        new TestCase
        {
            Id = "node-012",
            Area = "xpath: node+qname+uri fns",
            Title = "data() on untyped attribute yields xs:untypedAtomic; on element yields string value",
            SpecRef = "F&O 2.4",
            XPathExpr = @"let $d := data(/r/a/@id) return ($d instance of xs:untypedAtomic, $d, data(/r))",
            InputXml = @"<r><a id=""5"">1</a><b>2</b></r>",
            Expected = "true|5|12"
        },
        new TestCase
        {
            Id = "node-013",
            Area = "xpath: node+qname+uri fns",
            Title = "string(element) concatenates descendant text nodes",
            SpecRef = "F&O 2.3",
            XPathExpr = @"string(/r)",
            InputXml = @"<r>a<b>c</b>d</r>",
            Expected = "acd"
        },
        new TestCase
        {
            Id = "node-014",
            Area = "xpath: node+qname+uri fns",
            Title = "number() on numeric attribute and NaN for non-numeric element content",
            SpecRef = "F&O 4.5.1",
            XPathExpr = @"(number(/r/@a), number(/r))",
            InputXml = @"<r a=""3.5"">abc</r>",
            Expected = "3.5|NaN"
        },
        new TestCase
        {
            Id = "node-015",
            Area = "xpath: node+qname+uri fns",
            Title = "lang(): inherited xml:lang, range match en~en-US, case-insensitive exact match",
            SpecRef = "F&O 13.4",
            XPathExpr = @"(lang('en', /r/a), lang('en-us', /r/a), lang('fr', /r/a))",
            InputXml = @"<r xml:lang=""en-US""><a/></r>",
            Expected = "true|true|false"
        },
        new TestCase
        {
            Id = "node-016",
            Area = "xpath: node+qname+uri fns",
            Title = "lang(): nearer xml:lang ancestor overrides outer one",
            SpecRef = "F&O 13.4",
            XPathExpr = @"(lang('fr', /r), lang('fr', /r/a), lang('en', /r/a))",
            InputXml = @"<r xml:lang=""fr""><a xml:lang=""en""/></r>",
            Expected = "true|false|true"
        },
        new TestCase
        {
            Id = "node-017",
            Area = "xpath: node+qname+uri fns",
            Title = "resolve-QName() uses element in-scope binding for the prefix",
            SpecRef = "F&O 10.1.1",
            XPathExpr = @"let $q := resolve-QName('p:x', /*) return (string($q), namespace-uri-from-QName($q))",
            InputXml = @"<r xmlns:p=""http://ex""/>",
            Expected = "p:x|http://ex"
        },
        new TestCase
        {
            Id = "node-018",
            Area = "xpath: node+qname+uri fns",
            Title = "resolve-QName() of unprefixed name uses default element namespace",
            SpecRef = "F&O 10.1.1",
            XPathExpr = @"namespace-uri-from-QName(resolve-QName('x', /*))",
            InputXml = @"<r xmlns=""http://d""/>",
            Expected = "http://d"
        },
        new TestCase
        {
            Id = "node-019",
            Area = "xpath: node+qname+uri fns",
            Title = "QName() components via prefix/local-name/namespace-uri-from-QName; no prefix yields empty",
            SpecRef = "F&O 10.1.2, 10.2",
            XPathExpr = @"let $q := QName('http://ex', 'p:local') return (prefix-from-QName($q), local-name-from-QName($q), namespace-uri-from-QName($q), count(prefix-from-QName(QName('http://ex', 'plain'))))",
            Expected = "p|local|http://ex|0"
        },
        new TestCase
        {
            Id = "node-020",
            Area = "xpath: node+qname+uri fns",
            Title = "in-scope-prefixes() contains declared prefix and xml (sorted)",
            SpecRef = "F&O 10.2.6",
            XPathExpr = @"string-join(sort(in-scope-prefixes(/*)), ',')",
            InputXml = @"<r xmlns:p=""http://ex""/>",
            Expected = "p,xml"
        },
        new TestCase
        {
            Id = "node-021",
            Area = "xpath: node+qname+uri fns",
            Title = "namespace-uri-for-prefix(): inherited prefix, default namespace via '', and xml",
            SpecRef = "F&O 10.2.5",
            XPathExpr = @"(namespace-uri-for-prefix('p', /r/*), namespace-uri-for-prefix('', /r/*), namespace-uri-for-prefix('xml', /r))",
            InputXml = @"<r xmlns:p=""http://ex""><a xmlns=""http://d""/></r>",
            Expected = "http://ex|http://d|http://www.w3.org/XML/1998/namespace"
        },
        new TestCase
        {
            Id = "node-022",
            Area = "xpath: node+qname+uri fns",
            Title = "resolve-uri() two-arg: relative merge, dot-dot segment removal, absolute passthrough",
            SpecRef = "F&O 6.1",
            XPathExpr = @"(resolve-uri('b/c', 'http://ex/a/'), resolve-uri('../c', 'http://ex/a/b/'), resolve-uri('http://other/x', 'http://ex/'))",
            Expected = "http://ex/a/b/c|http://ex/a/c|http://other/x"
        },
        new TestCase
        {
            Id = "node-023",
            Area = "xpath: node+qname+uri fns",
            Title = "boolean() EBV: empty string, '0' string, zero, NaN, node present, empty node seq",
            SpecRef = "F&O 7.3.1, XPath 3.1 2.4.3",
            XPathExpr = @"(boolean(''), boolean('0'), boolean(0), boolean(number('NaN')), boolean(/r), boolean(/r/nope))",
            Expected = "false|true|false|false|true|false"
        },
        new TestCase
        {
            Id = "node-024",
            Area = "xpath: node+qname+uri fns",
            Title = "not() over empty seq / string / zero double; true() and false()",
            SpecRef = "F&O 7.3.2, 7.1",
            XPathExpr = @"(not(()), not('x'), not(0e0), true(), false())",
            Expected = "true|false|true|true|false"
        },
        new TestCase
        {
            Id = "node-025",
            Area = "xpath: node+qname+uri fns",
            Title = "name() applied to an integer is a type error",
            SpecRef = "XPath 3.1 3.1.5, F&O 13.1",
            XPathExpr = @"name(3)",
            ExpectedErrorCode = "XPTY0004"
        },
        new TestCase
        {
            Id = "node-026",
            Area = "xpath: node+qname+uri fns",
            Title = "resolve-QName() with unbound prefix raises FONS0004",
            SpecRef = "F&O 10.1.1",
            XPathExpr = @"resolve-QName('undecl:x', /r)",
            ExpectedErrorCode = "FONS0004"
        },
        new TestCase
        {
            Id = "node-027",
            Area = "xpath: node+qname+uri fns",
            Title = "QName() with prefix but zero-length namespace URI raises FOCA0002",
            SpecRef = "F&O 10.1.2",
            XPathExpr = @"QName('', 'p:x')",
            ExpectedErrorCode = "FOCA0002"
        },
        new TestCase
        {
            Id = "node-028",
            Area = "xpath: node+qname+uri fns",
            Title = "boolean() of a two-item atomic sequence has no EBV: FORG0006",
            SpecRef = "F&O 7.3.1, XPath 3.1 2.4.3",
            XPathExpr = @"boolean((1, 2))",
            ExpectedErrorCode = "FORG0006"
        }
    };
}
