// Spec-derived test cases: XSLT 3.0 template rules, match patterns, priorities,
// modes (incl. xsl:mode on-no-match), built-in rules, next-match, named templates,
// parameters (default/required/typed/tunnel), apply-templates ordering and sort.
// Expected values derived by hand from XSLT 3.0 ch 5.5, 6, 9, 10, 13.

namespace OutSmart.DAXon.SpecTests.Cases;

public static class B15_XsltTemplates
{
    public static TestCase[] Cases() => new[]
    {
        new TestCase
        {
            Id = "tmpl-001",
            Area = "xslt: templates+modes",
            Title = "Name patterns: each element fires its matching template",
            SpecRef = "XSLT 3.0 6.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/*""/></xsl:template>
<xsl:template match=""a"">[A]</xsl:template>
<xsl:template match=""b"">[B]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/><b/></r>",
            Expected = "[A][B]"
        },
        new TestCase
        {
            Id = "tmpl-002",
            Area = "xslt: templates+modes",
            Title = "Name test (priority 0) beats wildcard * (priority -0.5)",
            SpecRef = "XSLT 3.0 6.5",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/*""/></xsl:template>
<xsl:template match=""*"">[star:<xsl:value-of select=""name()""/>]</xsl:template>
<xsl:template match=""a"">[name]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/><c/></r>",
            Expected = "[name][star:c]"
        },
        new TestCase
        {
            Id = "tmpl-003",
            Area = "xslt: templates+modes",
            Title = "Attribute patterns: @a (priority 0) beats @* (priority -0.5)",
            SpecRef = "XSLT 3.0 5.5",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/@a""/><xsl:apply-templates select=""r/@b""/></xsl:template>
<xsl:template match=""@a"">[a:<xsl:value-of select="".""/>]</xsl:template>
<xsl:template match=""@*"">[any:<xsl:value-of select="".""/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r a=""1"" b=""2""/>",
            Expected = "[a:1][any:2]"
        },
        new TestCase
        {
            Id = "tmpl-004",
            Area = "xslt: templates+modes",
            Title = "text() pattern matches a text node",
            SpecRef = "XSLT 3.0 5.5",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/node()""/></xsl:template>
<xsl:template match=""text()"">[T:<xsl:value-of select="".""/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r>hi</r>",
            Expected = "[T:hi]"
        },
        new TestCase
        {
            Id = "tmpl-005",
            Area = "xslt: templates+modes",
            Title = "node() pattern catches text; loses to name test on elements",
            SpecRef = "XSLT 3.0 6.5",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/node()""/></xsl:template>
<xsl:template match=""node()"">[N]</xsl:template>
<xsl:template match=""c"">[C]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r>x<c/></r>",
            Expected = "[N][C]"
        },
        new TestCase
        {
            Id = "tmpl-006",
            Area = "xslt: templates+modes",
            Title = "Pattern with predicate (priority 0.5) outranks bare name (0)",
            SpecRef = "XSLT 3.0 6.5",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""/></xsl:template>
<xsl:template match=""a"">[N]</xsl:template>
<xsl:template match=""a[@k='1']"">[P]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a k=""1""/><a k=""2""/></r>",
            Expected = "[P][N]"
        },
        new TestCase
        {
            Id = "tmpl-007",
            Area = "xslt: templates+modes",
            Title = "Union pattern alternatives get independent default priorities",
            SpecRef = "XSLT 3.0 6.5",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/*""/></xsl:template>
<xsl:template match=""a|b[@k]"">[U]</xsl:template>
<xsl:template match=""b"">[B]</xsl:template>
<xsl:template match=""*"">[O]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/><b k=""1""/><b/><c/></r>",
            Expected = "[U][U][B][O]"
        },
        new TestCase
        {
            Id = "tmpl-008",
            Area = "xslt: templates+modes",
            Title = "Path pattern r/b matches only b whose parent is r",
            SpecRef = "XSLT 3.0 5.5",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""//b""/></xsl:template>
<xsl:template match=""r/b"">[rb]</xsl:template>
<xsl:template match=""b"">[b]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><b/><x><b/></x></r>",
            Expected = "[rb][b]"
        },
        new TestCase
        {
            Id = "tmpl-009",
            Area = "xslt: templates+modes",
            Title = "Descendant pattern x//b matches only b inside an x",
            SpecRef = "XSLT 3.0 5.5",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""//b""/></xsl:template>
<xsl:template match=""x//b"">[xb]</xsl:template>
<xsl:template match=""b"">[b]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><b/><x><y><b/></y></x></r>",
            Expected = "[b][xb]"
        },
        new TestCase
        {
            Id = "tmpl-010",
            Area = "xslt: templates+modes",
            Title = "Explicit priority attribute overrides default priorities",
            SpecRef = "XSLT 3.0 6.5",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""/></xsl:template>
<xsl:template match=""a"" priority=""2"">[explicit]</xsl:template>
<xsl:template match=""a[@k]"">[pred]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a k=""1""/></r>",
            Expected = "[explicit]"
        },
        new TestCase
        {
            Id = "tmpl-011",
            Area = "xslt: templates+modes",
            Title = "Equal-priority conflict resolved as use-last (last declared wins)",
            SpecRef = "XSLT 3.0 6.5",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""/></xsl:template>
<xsl:template match=""a"">[first]</xsl:template>
<xsl:template match=""a"">[second]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/></r>",
            Expected = "[second]"
        },
        new TestCase
        {
            Id = "tmpl-012",
            Area = "xslt: templates+modes",
            Title = "Built-in rules recurse into elements, copy text, never visit attributes",
            SpecRef = "XSLT 3.0 6.7",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""b"">[B]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r z=""Z"">x<a>y</a><b>w</b></r>",
            Expected = "xy[B]"
        },
        new TestCase
        {
            Id = "tmpl-013",
            Area = "xslt: templates+modes",
            Title = "Named mode selects mode-specific template, default mode unaffected",
            SpecRef = "XSLT 3.0 6.6",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""/><xsl:apply-templates select=""r/a"" mode=""m""/></xsl:template>
<xsl:template match=""a"">[default]</xsl:template>
<xsl:template match=""a"" mode=""m"">[m]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/></r>",
            Expected = "[default][m]"
        },
        new TestCase
        {
            Id = "tmpl-014",
            Area = "xslt: templates+modes",
            Title = "mode=#all template fires in both unnamed and named modes",
            SpecRef = "XSLT 3.0 6.6",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:mode name=""m""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""/><xsl:apply-templates select=""r/a"" mode=""m""/></xsl:template>
<xsl:template match=""a"" mode=""#all"">[all]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/></r>",
            Expected = "[all][all]"
        },
        new TestCase
        {
            Id = "tmpl-015",
            Area = "xslt: templates+modes",
            Title = "xsl:mode on-no-match=shallow-copy copies unmatched elements, replaces matched",
            SpecRef = "XSLT 3.0 6.7",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""xml"" omit-xml-declaration=""yes""/>
<xsl:mode on-no-match=""shallow-copy""/>
<xsl:template match=""a"">[A]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a>x</a><b>y</b></r>",
            Expected = "<r>[A]<b>y</b></r>"
        },
        new TestCase
        {
            Id = "tmpl-016",
            Area = "xslt: templates+modes",
            Title = "xsl:mode on-no-match=deep-skip drops unmatched subtree entirely",
            SpecRef = "XSLT 3.0 6.7",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:mode on-no-match=""deep-skip""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/*""/></xsl:template>
<xsl:template match=""b"">[B:<xsl:value-of select="".""/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a>x</a><b>y</b></r>",
            Expected = "[B:y]"
        },
        new TestCase
        {
            Id = "tmpl-017",
            Area = "xslt: templates+modes",
            Title = "xsl:mode on-no-match=text-only-copy copies text, recurses elements",
            SpecRef = "XSLT 3.0 6.7",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:mode on-no-match=""text-only-copy""/>
<xsl:template match=""b"">[B]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r>a<b>c</b>d</r>",
            Expected = "a[B]d"
        },
        new TestCase
        {
            Id = "tmpl-018",
            Area = "xslt: templates+modes",
            Title = "xsl:mode on-no-match=fail raises XTDE0555 on unmatched node",
            SpecRef = "XSLT 3.0 6.7",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:mode on-no-match=""fail""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/*""/></xsl:template>
<xsl:template match=""a"">[A]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/><b/></r>",
            ExpectedErrorCode = "XTDE0555"
        },
        new TestCase
        {
            Id = "tmpl-019",
            Area = "xslt: templates+modes",
            Title = "xsl:next-match invokes the next lower-priority matching rule",
            SpecRef = "XSLT 3.0 6.8",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""/></xsl:template>
<xsl:template match=""a"" priority=""2"">[H:<xsl:next-match/>]</xsl:template>
<xsl:template match=""a"" priority=""1"">L</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/></r>",
            Expected = "[H:L]"
        },
        new TestCase
        {
            Id = "tmpl-020",
            Area = "xslt: templates+modes",
            Title = "xsl:next-match with no lower rule falls back to built-in rule",
            SpecRef = "XSLT 3.0 6.8",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""/></xsl:template>
<xsl:template match=""a"">[<xsl:next-match/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a>t</a></r>",
            Expected = "[t]"
        },
        new TestCase
        {
            Id = "tmpl-021",
            Area = "xslt: templates+modes",
            Title = "call-template: param default used when absent, with-param overrides",
            SpecRef = "XSLT 3.0 10.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:call-template name=""t""/><xsl:call-template name=""t""><xsl:with-param name=""p"" select=""'x'""/></xsl:call-template></xsl:template>
<xsl:template name=""t""><xsl:param name=""p"" select=""'d'""/>[<xsl:value-of select=""$p""/>]</xsl:template>
</xsl:stylesheet>",
            Expected = "[d][x]"
        },
        new TestCase
        {
            Id = "tmpl-022",
            Area = "xslt: templates+modes",
            Title = "apply-templates without value for required param raises XTDE0700",
            SpecRef = "XSLT 3.0 9.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""/></xsl:template>
<xsl:template match=""a""><xsl:param name=""p"" required=""yes""/>[<xsl:value-of select=""$p""/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/></r>",
            ExpectedErrorCode = "XTDE0700"
        },
        new TestCase
        {
            Id = "tmpl-023",
            Area = "xslt: templates+modes",
            Title = "xsl:param with as=xs:integer usable in arithmetic, default and supplied",
            SpecRef = "XSLT 3.0 9.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:call-template name=""t""/><xsl:call-template name=""t""><xsl:with-param name=""n"" select=""3""/></xsl:call-template></xsl:template>
<xsl:template name=""t""><xsl:param name=""n"" as=""xs:integer"" select=""5""/>[<xsl:value-of select=""$n * 2""/>]</xsl:template>
</xsl:stylesheet>",
            Expected = "[10][6]"
        },
        new TestCase
        {
            Id = "tmpl-024",
            Area = "xslt: templates+modes",
            Title = "Tunnel parameter flows through intermediate template that ignores it",
            SpecRef = "XSLT 3.0 10.1.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""><xsl:with-param name=""t"" tunnel=""yes"" select=""'T'""/></xsl:apply-templates></xsl:template>
<xsl:template match=""a""><xsl:apply-templates select=""b""/></xsl:template>
<xsl:template match=""b""><xsl:param name=""t"" tunnel=""yes"" select=""'none'""/>[b:<xsl:value-of select=""$t""/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a><b/></a></r>",
            Expected = "[b:T]"
        },
        new TestCase
        {
            Id = "tmpl-025",
            Area = "xslt: templates+modes",
            Title = "Non-tunnel parameter is not propagated through intermediate template",
            SpecRef = "XSLT 3.0 10.1.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""><xsl:with-param name=""t"" select=""'T'""/></xsl:apply-templates></xsl:template>
<xsl:template match=""a""><xsl:apply-templates select=""b""/></xsl:template>
<xsl:template match=""b""><xsl:param name=""t"" select=""'none'""/>[b:<xsl:value-of select=""$t""/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a><b/></a></r>",
            Expected = "[b:none]"
        },
        new TestCase
        {
            Id = "tmpl-026",
            Area = "xslt: templates+modes",
            Title = "apply-templates with numeric xsl:sort processes nodes in sorted order",
            SpecRef = "XSLT 3.0 13.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/i""><xsl:sort select=""."" data-type=""number""/></xsl:apply-templates></xsl:template>
<xsl:template match=""i"">[<xsl:value-of select="".""/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i>3</i><i>1</i><i>2</i></r>",
            Expected = "[1][2][3]"
        },
        new TestCase
        {
            Id = "tmpl-027",
            Area = "xslt: templates+modes",
            Title = "xsl:sort order=descending on text keys (codepoint collation)",
            SpecRef = "XSLT 3.0 13.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/i""><xsl:sort select=""."" order=""descending""/></xsl:apply-templates></xsl:template>
<xsl:template match=""i"">[<xsl:value-of select="".""/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i>b</i><i>a</i><i>c</i></r>",
            Expected = "[c][b][a]"
        },
        new TestCase
        {
            Id = "tmpl-028",
            Area = "xslt: templates+modes",
            Title = "apply-templates processes nodes in order of the select sequence",
            SpecRef = "XSLT 3.0 6.4",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""reverse(r/*)""/></xsl:template>
<xsl:template match=""*"">[<xsl:value-of select=""name()""/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><x/><y/><z/></r>",
            Expected = "[z][y][x]"
        },
        new TestCase
        {
            Id = "tmpl-029",
            Area = "xslt: templates+modes",
            Title = "Template with both match and name: invocable both ways, call keeps caller context",
            SpecRef = "XSLT 3.0 6.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:apply-templates select=""r/a""/><xsl:call-template name=""t""/></xsl:template>
<xsl:template match=""a"" name=""t"">[T:<xsl:value-of select=""name(.)""/>]</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/></r>",
            Expected = "[T:a][T:]"
        },
        new TestCase
        {
            Id = "tmpl-030",
            Area = "xslt: templates+modes",
            Title = "call-template supplying undeclared non-tunnel param is static error XTSE0680",
            SpecRef = "XSLT 3.0 10.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/""><xsl:call-template name=""t""><xsl:with-param name=""q"" select=""1""/></xsl:call-template></xsl:template>
<xsl:template name=""t"">x</xsl:template>
</xsl:stylesheet>",
            ExpectedErrorCode = "XTSE0680"
        }
    };
}
