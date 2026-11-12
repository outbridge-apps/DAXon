// Spec-derived serialization tests for the Saxon-HE 12.9 C# port.
// Expected values derived by hand from XSLT 3.0 ch 26, the XSLT/XQuery
// Serialization 3.1 REC, and F&O 3.1 (fn:serialize). Constructs whose exact
// byte form is implementation-defined (whitespace after the XML declaration,
// whitespace inside a DOCTYPE declaration, HTML doctype emission/casing) are
// probed via substring-before/contains so the expectation is unambiguous.

namespace OutSmart.DAXon.SpecTests.Cases;

public static class B19_Serialization
{
    public static TestCase[] Cases() => new[]
    {
        // --- text output method ---
        new TestCase
        {
            Id = "ser-001",
            Area = "xslt: serialization",
            Title = "method=text outputs only text nodes; comments and PIs contribute nothing",
            SpecRef = "Serialization 3.1 sec 8 (text output method)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <e>A<xsl:comment>zap</xsl:comment><f>B</f><xsl:processing-instruction name=""pp"">zap</xsl:processing-instruction>C</e>
  </xsl:template>
</xsl:stylesheet>",
            Expected = "ABC"
        },
        new TestCase
        {
            Id = "ser-002",
            Area = "xslt: serialization",
            Title = "method=text performs no escaping of < and &",
            SpecRef = "Serialization 3.1 sec 8 (text output method)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text""/>
  <xsl:template match=""/""><xsl:text>a &lt; b &amp; c</xsl:text></xsl:template>
</xsl:stylesheet>",
            Expected = "a < b & c"
        },

        // --- xml output method: exact markup ---
        new TestCase
        {
            Id = "ser-003",
            Area = "xslt: serialization",
            Title = "method=xml omit-xml-declaration=yes indent=no gives exact bare markup",
            SpecRef = "XSLT 3.0 sec 26; Serialization 3.1 sec 5 (xml output method)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""xml"" omit-xml-declaration=""yes"" indent=""no""/>
  <xsl:template match=""/""><a><b>1</b><c>2</c></a></xsl:template>
</xsl:stylesheet>",
            Expected = @"<a><b>1</b><c>2</c></a>"
        },
        new TestCase
        {
            Id = "ser-004",
            Area = "xslt: serialization",
            Title = "element content escaping: & becomes &amp; and < becomes &lt;",
            SpecRef = "Serialization 3.1 sec 5 (xml output method)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""xml"" omit-xml-declaration=""yes"" indent=""no""/>
  <xsl:template match=""/""><x>a &amp; b &lt; c</x></xsl:template>
</xsl:stylesheet>",
            Expected = @"<x>a &amp; b &lt; c</x>"
        },
        new TestCase
        {
            Id = "ser-005",
            Area = "xslt: serialization",
            Title = "empty element serialized in minimized form <a/>; encoding=UTF-8 declared on xsl:output",
            SpecRef = "Serialization 3.1 sec 5 (xml output method)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""xml"" encoding=""UTF-8"" omit-xml-declaration=""yes"" indent=""no""/>
  <xsl:template match=""/""><a/></xsl:template>
</xsl:stylesheet>",
            Expected = @"<a/>"
        },
        new TestCase
        {
            Id = "ser-006",
            Area = "xslt: serialization",
            Title = "attribute values are delimited with double quotes",
            SpecRef = "Serialization 3.1 sec 5 (xml output method)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""xml"" omit-xml-declaration=""yes"" indent=""no""/>
  <xsl:template match=""/""><e a=""v""/></xsl:template>
</xsl:stylesheet>",
            Expected = @"<e a=""v""/>"
        },
        new TestCase
        {
            Id = "ser-007",
            Area = "xslt: serialization",
            Title = "& is escaped as &amp; inside an attribute value",
            SpecRef = "Serialization 3.1 sec 5 (xml output method)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""xml"" omit-xml-declaration=""yes"" indent=""no""/>
  <xsl:template match=""/""><e a=""x&amp;y""/></xsl:template>
</xsl:stylesheet>",
            Expected = @"<e a=""x&amp;y""/>"
        },
        new TestCase
        {
            Id = "ser-008",
            Area = "xslt: serialization",
            Title = "cdata-section-elements wraps text child in a CDATA section, content unescaped",
            SpecRef = "Serialization 3.1 sec 5 (cdata-section-elements)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""xml"" omit-xml-declaration=""yes"" indent=""no"" cdata-section-elements=""x""/>
  <xsl:template match=""/""><x>a&lt;b</x></xsl:template>
</xsl:stylesheet>",
            Expected = @"<x><![CDATA[a<b]]></x>"
        },

        // --- character maps ---
        new TestCase
        {
            Id = "ser-009",
            Area = "xslt: serialization",
            Title = "character map replaces * with [star] under method=text",
            SpecRef = "XSLT 3.0 sec 26; Serialization 3.1 sec 11 (character maps)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text"" use-character-maps=""m""/>
  <xsl:character-map name=""m""><xsl:output-character character=""*"" string=""[star]""/></xsl:character-map>
  <xsl:template match=""/""><xsl:text>a*b</xsl:text></xsl:template>
</xsl:stylesheet>",
            Expected = "a[star]b"
        },
        new TestCase
        {
            Id = "ser-010",
            Area = "xslt: serialization",
            Title = "character map on & takes precedence over escaping; replacement output raw",
            SpecRef = "XSLT 3.0 sec 26; Serialization 3.1 sec 11 (character maps)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""xml"" omit-xml-declaration=""yes"" indent=""no"" use-character-maps=""m""/>
  <xsl:character-map name=""m""><xsl:output-character character=""&amp;"" string=""[amp]""/></xsl:character-map>
  <xsl:template match=""/""><e>x&amp;y</e></xsl:template>
</xsl:stylesheet>",
            Expected = @"<e>x[amp]y</e>"
        },

        // --- item-separator and raw output ---
        new TestCase
        {
            Id = "ser-011",
            Area = "xslt: serialization",
            Title = "item-separator=| joins atomic items of a raw sequence under method=xml build-tree=no",
            SpecRef = "XSLT 3.0 sec 26; Serialization 3.1 sec 2 (sequence normalization)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""xml"" omit-xml-declaration=""yes"" build-tree=""no"" item-separator=""|""/>
  <xsl:template match=""/""><xsl:sequence select=""1 to 4""/></xsl:template>
</xsl:stylesheet>",
            Expected = "1|2|3|4"
        },

        // --- json output method via xsl:output ---
        new TestCase
        {
            Id = "ser-012",
            Area = "xslt: serialization",
            Title = "method=json serializes a single-entry map as a JSON object",
            SpecRef = "XSLT 3.0 sec 26; Serialization 3.1 sec 9 (json output method)",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""json"" indent=""no""/>
  <xsl:template match=""/""><xsl:sequence select=""map{'a':1}""/></xsl:template>
</xsl:stylesheet>",
            Expected = @"{""a"":1}"
        },

        // --- fn:serialize ---
        new TestCase
        {
            Id = "ser-013",
            Area = "xslt: serialization",
            Title = "serialize() with method=text joins adjacent atomic values with a single space",
            SpecRef = "F&O 3.1 sec 14.3 fn:serialize; Serialization 3.1 sec 2",
            XPathExpr = @"serialize((1,2,3), map{'method':'text'})",
            Expected = "1 2 3"
        },
        new TestCase
        {
            Id = "ser-014",
            Area = "xslt: serialization",
            Title = "serialize() of the empty sequence with method=text yields the empty string",
            SpecRef = "F&O 3.1 sec 14.3 fn:serialize; Serialization 3.1 sec 2",
            XPathExpr = @"serialize((), map{'method':'text'})",
            Expected = ""
        },
        new TestCase
        {
            Id = "ser-015",
            Area = "xslt: serialization",
            Title = "serialize() of an element node with method=xml and declaration omitted",
            SpecRef = "F&O 3.1 sec 14.3 fn:serialize; Serialization 3.1 sec 5",
            XPathExpr = @"serialize(/r/a, map{'method':'xml', 'omit-xml-declaration':true()})",
            InputXml = @"<r><a>x</a></r>",
            Expected = @"<a>x</a>"
        },
        new TestCase
        {
            Id = "ser-016",
            Area = "xslt: serialization",
            Title = "omit-xml-declaration=no emits XML declaration with version 1.0 and encoding UTF-8",
            SpecRef = "F&O 3.1 sec 14.3 fn:serialize; Serialization 3.1 sec 5 (XML declaration)",
            XPathExpr = @"substring-before(serialize(., map{'omit-xml-declaration':false()}), '<r')",
            Expected = @"<?xml version=""1.0"" encoding=""UTF-8""?>"
        },
        new TestCase
        {
            Id = "ser-017",
            Area = "xslt: serialization",
            Title = "standalone=yes adds standalone attribute after the encoding declaration",
            SpecRef = "Serialization 3.1 sec 5 (standalone parameter)",
            XPathExpr = @"substring-before(serialize(., map{'omit-xml-declaration':false(), 'standalone':true()}), '<r')",
            Expected = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>"
        },
        new TestCase
        {
            Id = "ser-018",
            Area = "xslt: serialization",
            Title = "doctype-system emits a DOCTYPE for the root element with the SYSTEM identifier",
            SpecRef = "Serialization 3.1 sec 5 (doctype-system parameter)",
            XPathExpr = @"let $s := serialize(., map{'omit-xml-declaration':true(), 'doctype-system':'u.dtd'}) return contains($s, '<!DOCTYPE r') and contains($s, 'SYSTEM ""u.dtd""')",
            Expected = "true"
        },

        // --- html output method ---
        new TestCase
        {
            Id = "ser-019",
            Area = "xslt: serialization",
            Title = "html method serializes br as void <br> with no end tag",
            SpecRef = "Serialization 3.1 sec 7 (html output method)",
            XPathExpr = @"contains(serialize(/p, map{'method':'html', 'indent':false()}), '<br>b</p>')",
            InputXml = @"<p>a<br/>b</p>",
            Expected = "true"
        },
        new TestCase
        {
            Id = "ser-020",
            Area = "xslt: serialization",
            Title = "html method gives empty non-void element an explicit end tag, never minimized",
            SpecRef = "Serialization 3.1 sec 7 (html output method)",
            XPathExpr = @"contains(serialize(/div, map{'method':'html', 'indent':false()}), '<div></div>')",
            InputXml = @"<div/>",
            Expected = "true"
        },

        // --- json output method via fn:serialize ---
        new TestCase
        {
            Id = "ser-021",
            Area = "xslt: serialization",
            Title = "json method serializes xs:boolean map value as bare true",
            SpecRef = "Serialization 3.1 sec 9 (json output method)",
            XPathExpr = @"serialize(map{'b':true()}, map{'method':'json'})",
            Expected = @"{""b"":true}"
        },
        new TestCase
        {
            Id = "ser-022",
            Area = "xslt: serialization",
            Title = "json method serializes an array in member order as [1,2]",
            SpecRef = "Serialization 3.1 sec 9 (json output method)",
            XPathExpr = @"serialize([1,2], map{'method':'json'})",
            Expected = "[1,2]"
        },
        new TestCase
        {
            Id = "ser-023",
            Area = "xslt: serialization",
            Title = "json method serializes a single string as a quoted JSON string",
            SpecRef = "Serialization 3.1 sec 9 (json output method)",
            XPathExpr = @"serialize('hi', map{'method':'json'})",
            Expected = @"""hi"""
        },

        // --- spec-mandated serialization errors ---
        new TestCase
        {
            Id = "ser-024",
            Area = "xslt: serialization",
            Title = "json method with a sequence of more than one item raises SERE0023",
            SpecRef = "Serialization 3.1 sec 9 err:SERE0023",
            XPathExpr = @"serialize((1,2), map{'method':'json'})",
            ExpectedErrorCode = "SERE0023"
        },
        new TestCase
        {
            Id = "ser-025",
            Area = "xslt: serialization",
            Title = "json method with NaN raises SERE0020 (number not representable in JSON)",
            SpecRef = "Serialization 3.1 sec 9 err:SERE0020",
            XPathExpr = @"serialize(xs:double('NaN'), map{'method':'json'})",
            ExpectedErrorCode = "SERE0020"
        },
        new TestCase
        {
            Id = "ser-026",
            Area = "xslt: serialization",
            Title = "json method with two keys sharing one string form raises SERE0022",
            SpecRef = "Serialization 3.1 sec 9 err:SERE0022",
            XPathExpr = @"serialize(map{1:'x', '1':'y'}, map{'method':'json'})",
            ExpectedErrorCode = "SERE0022"
        },

        // --- json escaping fast paths: what is written raw and what is \u-escaped ---
        new TestCase
        {
            Id = "ser-027",
            Area = "xslt: serialization",
            Title = "json method in UTF-8 writes non-ASCII raw and hex-escapes a C1 control",
            SpecRef = "Serialization 3.1 sec 9.1.1 (json string escaping)",
            XPathExpr = @"serialize(map{'k': codepoints-to-string((1055,1088,133))}, map{'method':'json'}) eq '{""k"":""' || codepoints-to-string((1055,1088)) || '\u0085""}'",
            Expected = "true"
        },
        new TestCase
        {
            Id = "ser-028",
            Area = "xslt: serialization",
            Title = "json method in us-ascii hex-escapes a character outside the encoding",
            SpecRef = "Serialization 3.1 sec 9.1.1",
            XPathExpr = @"serialize(map{'k': codepoints-to-string(1055)}, map{'method':'json','encoding':'us-ascii'})",
            Expected = @"{""k"":""\u041F""}"
        },
        new TestCase
        {
            Id = "ser-029",
            Area = "xpath: json",
            Title = "xml-to-json escapes quote, backslash and solidus in keys and values, leaves plain text alone",
            SpecRef = "F&O 3.1 17.5.4 fn:xml-to-json",
            XPathExpr = @"xml-to-json(parse-xml('<map xmlns=""http://www.w3.org/2005/xpath-functions""><string key=""a/b&quot;"">x\y/z</string><string key=""plain"">plain</string></map>'))",
            Expected = @"{""a\/b\"""":""x\\y\/z"",""plain"":""plain""}"
        },
        new TestCase
        {
            Id = "ser-030",
            Area = "xpath: json",
            Title = "xml-to-json hex-escapes a C1 control and keeps non-ASCII raw in keys and values",
            SpecRef = "F&O 3.1 17.5.4 fn:xml-to-json",
            XPathExpr = @"xml-to-json(parse-xml('<map xmlns=""http://www.w3.org/2005/xpath-functions""><string key=""k&#x85;"">v&#x85;' || codepoints-to-string(1055) || '</string></map>')) eq '{""k\u0085"":""v\u0085' || codepoints-to-string(1055) || '""}'",
            Expected = "true"
        }
    };
}
