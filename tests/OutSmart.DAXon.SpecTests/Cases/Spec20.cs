namespace OutSmart.DAXon.SpecTests.Cases
{
    /// <summary>
    /// 20 spec-derived probes, one per XPath 3.1 / XSLT 3.0 feature area. Expected values come
    /// from the W3C specs, not from observed port behavior — a failing case documents a port gap.
    /// A larger generated bank (387 cases, 13 clusters) lives outside the repo and can be merged
    /// in cluster by cluster as needed.
    /// </summary>
    public static class Spec20
    {
        private const string AxesDoc = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c></b><a id=""a2"">four</a></doc>";

        public static TestCase[] Cases() => new[]
        {
            // ---------------- XPath 3.1 ----------------
            new TestCase { Id = "spec-001", Area = "xpath", Title = "axes: preceding-sibling in document order", SpecRef = "XPath 3.1 3.3.2.1",
                XPathExpr = "string-join(//a[@id='a2']/preceding-sibling::*/@id, ',')",
                InputXml = AxesDoc, Expected = "a1,b1" },

            new TestCase { Id = "spec-002", Area = "xpath", Title = "operators: mod sign, idiv truncation, precedence", SpecRef = "XPath 3.1 3.5 / F&O 4.2",
                XPathExpr = "concat(5 mod -3, ',', -5 idiv 3, ',', 2 + 3 * 4)",
                Expected = "2,-1,14" },

            new TestCase { Id = "spec-003", Area = "xpath", Title = "for: multiple clauses form cartesian product", SpecRef = "XPath 3.1 3.9",
                XPathExpr = "string-join(for $i in (1,2,3), $j in ('a','b') return concat($i,$j), ' ')",
                Expected = "1a 1b 2a 2b 3a 3b" },

            new TestCase { Id = "spec-004", Area = "xpath", Title = "strings: substring rounding, substring-before, translate deletion", SpecRef = "F&O 5.4.5 / 5.4.8 / 5.6.5",
                XPathExpr = "concat(substring('12345', 1.5, 2.6), ',', substring-before('tattoo','ttoo'), ',', translate('abcabc','abc','AB'))",
                Expected = "234,ta,ABAB" },

            new TestCase { Id = "spec-005", Area = "xpath", Title = "format-integer: roman, zero-padded, grouped pictures", SpecRef = "F&O 4.6.1",
                XPathExpr = "concat(format-integer(4, 'I'), ',', format-integer(7, '001'), ',', format-integer(1234567, '#,##0'))",
                Expected = "IV,007,1,234,567" },

            new TestCase { Id = "spec-006", Area = "xpath", Title = "regex: replace, i-flag matches, tokenize keeps empty tokens", SpecRef = "F&O 5.6.3-5.6.5",
                XPathExpr = "concat(replace('banana','an','*'), ',', matches('ABC','abc','i'), ',', string-join(tokenize('a,b,,c', ','), ';'))",
                Expected = "b**a,true,a;b;;c" },

            new TestCase { Id = "spec-007", Area = "xpath", Title = "numeric: round half toward +INF, round-half-to-even, INF serialization", SpecRef = "F&O 4.4.3 / 4.4.4",
                XPathExpr = "concat(round(2.5), ',', round(-2.5), ',', round-half-to-even(2.5), ',', 1e0 div 0)",
                Expected = "3,-2,2,INF" },

            new TestCase { Id = "spec-008", Area = "xpath", Title = "dates: date+duration, time component, dateTime difference", SpecRef = "F&O 8.2 / 9.4 / 9.7",
                XPathExpr = "concat(string(xs:date('2024-03-10') + xs:dayTimeDuration('P5D')), ',', hours-from-time(xs:time('13:20:00')), ',', string(xs:dateTime('2024-01-31T23:00:00Z') - xs:dateTime('2024-01-30T22:00:00Z')))",
                Expected = "2024-03-15,13,P1DT1H" },

            new TestCase { Id = "spec-009", Area = "xpath", Title = "casts: double->integer truncation, hexBinary canonical case, boolean('1'), castable", SpecRef = "XPath 3.1 19 / XSD 1.1",
                XPathExpr = "concat(xs:integer(2.9e0), ',', string(xs:hexBinary('4a')), ',', xs:boolean('1'), ',', '12.5' castable as xs:integer)",
                Expected = "2,4A,true,false" },

            new TestCase { Id = "spec-010", Area = "xpath", Title = "maps: size, lookup, sorted keys", SpecRef = "XPath 3.1 3.11.1 / F&O 17.1",
                XPathExpr = "let $m := map{'a':1,'b':2} return concat(map:size($m), ',', $m?b, ',', string-join(sort(map:keys($m)), '-'))",
                Expected = "2,2,a-b" },

            new TestCase { Id = "spec-011", Area = "xpath", Title = "arrays: size, positional lookup, reverse+flatten", SpecRef = "XPath 3.1 3.11.2 / F&O 17.3",
                XPathExpr = "let $a := [1,2,3] return concat(array:size($a), ',', $a?2, ',', string-join(array:flatten(array:reverse($a)) ! string(.), ''))",
                Expected = "3,2,321" },

            new TestCase { Id = "spec-012", Area = "xpath", Title = "HOF: fold-left is left-associative (inline function, closure)", SpecRef = "XPath 3.1 3.1.6 / F&O 16.2.3",
                XPathExpr = "fold-left((1,2,3), '0', function($a,$b){concat('(',$a,'+',$b,')')})",
                Expected = "(((0+1)+2)+3)" },

            new TestCase { Id = "spec-013", Area = "xpath", Title = "json: parse-json chained lookups into nested array/map", SpecRef = "F&O 17.5.1",
                XPathExpr = "let $j := parse-json('{\"a\":[1,2,{\"b\":true}]}') return concat($j?a?3?b, ',', array:size($j?a))",
                Expected = "true,3" },

            new TestCase { Id = "spec-014", Area = "xpath", Title = "node fns: fn:path canonical Q{} form", SpecRef = "F&O 14.1.4",
                XPathExpr = "path(/r/a[2])",
                InputXml = "<r><a/><a/></r>", Expected = "/Q{}r[1]/Q{}a[2]" },

            // ---------------- XSLT 3.0 ----------------
            new TestCase { Id = "spec-015", Area = "xslt", Title = "templates: explicit priority + next-match", SpecRef = "XSLT 3.0 6.5 / 6.7",
                Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text""/>
  <xsl:template match=""/""><xsl:apply-templates select=""/r/i""/></xsl:template>
  <xsl:template match=""i"" priority=""2"">[hi:<xsl:value-of select="".""/><xsl:next-match/>]</xsl:template>
  <xsl:template match=""i"" priority=""1"">(lo:<xsl:value-of select="".""/>)</xsl:template>
</xsl:stylesheet>",
                InputXml = "<r><i>x</i></r>", Expected = "[hi:x(lo:x)]" },

            new TestCase { Id = "spec-016", Area = "xslt", Title = "for-each + xsl:sort numeric descending", SpecRef = "XSLT 3.0 13.1",
                Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:for-each select=""/r/n"">
      <xsl:sort select=""."" data-type=""number"" order=""descending""/>
      <xsl:value-of select="".""/><xsl:text>;</xsl:text>
    </xsl:for-each>
  </xsl:template>
</xsl:stylesheet>",
                InputXml = "<r><n>2</n><n>10</n><n>1</n></r>", Expected = "10;2;1;" },

            new TestCase { Id = "spec-017", Area = "xslt", Title = "for-each-group group-by: first-appearance order, current-group()", SpecRef = "XSLT 3.0 14.4",
                Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:for-each-group select=""/r/i"" group-by=""@c"">
      <xsl:value-of select=""concat(current-grouping-key(), ':', string-join(current-group(), ''), ';')""/>
    </xsl:for-each-group>
  </xsl:template>
</xsl:stylesheet>",
                InputXml = @"<r><i c=""x"">1</i><i c=""y"">2</i><i c=""x"">3</i></r>", Expected = "x:13;y:2;" },

            new TestCase { Id = "spec-018", Area = "xslt", Title = "xsl:iterate with params and on-completion", SpecRef = "XSLT 3.0 8.8",
                Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:iterate select=""1 to 5"">
      <xsl:param name=""sum"" select=""0""/>
      <xsl:on-completion><xsl:value-of select=""concat('total:', $sum)""/></xsl:on-completion>
      <xsl:next-iteration><xsl:with-param name=""sum"" select=""$sum + .""/></xsl:next-iteration>
    </xsl:iterate>
  </xsl:template>
</xsl:stylesheet>",
                Expected = "total:15" },

            new TestCase { Id = "spec-019", Area = "xslt", Title = "xsl:try/xsl:catch: error() code readable via $err:code", SpecRef = "XSLT 3.0 J / F&O 3.1.1",
                Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform""
    xmlns:xs=""http://www.w3.org/2001/XMLSchema""
    xmlns:my=""urn:my"" xmlns:err=""http://www.w3.org/2005/xqt-errors"">
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:try>
      <xsl:sequence select=""error(xs:QName('my:E123'), 'boom')""/>
      <xsl:catch>
        <xsl:value-of select=""concat('caught:', local-name-from-QName($err:code))""/>
      </xsl:catch>
    </xsl:try>
  </xsl:template>
</xsl:stylesheet>",
                Expected = "caught:E123" },

            new TestCase { Id = "spec-020", Area = "xslt", Title = "xsl:number: value with grouping separator", SpecRef = "XSLT 3.0 12.3-12.4",
                Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:number value=""1234567"" grouping-separator="","" grouping-size=""3""/>
  </xsl:template>
</xsl:stylesheet>",
                Expected = "1,234,567" },

            // Pins a deliberate divergence from Saxon-HE (Java): upstream pairs a nested map that
            // follows a use-first-discarded duplicate key with the DUPLICATE's name (orphan entry
            // on its shared pending-key stack), yielding keys (a,y) here. Per F&O 17.5 the correct
            // result keeps the outer key: (x,y) with x => map{a:1}.
            new TestCase { Id = "spec-021", Area = "xpath", Title = "json: nested map after use-first duplicate key pairs with the OUTER key", SpecRef = "F&O 17.5.1",
                XPathExpr = "let $m := parse-json('{\"x\":{\"a\":1,\"a\":2},\"y\":3}') return string-join((sort(map:keys($m)), string($m?x?a), string($m?y)), '|')",
                Expected = "x|y|1|3" },
        };
    }
}
