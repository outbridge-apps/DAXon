// Spec-derived test cases: XSLT 3.0 grouping (xsl:for-each-group with group-by /
// group-adjacent / group-starting-with / group-ending-with, composite keys, sorting
// of groups, nested grouping), keys (xsl:key declarations + fn:key) and xsl:number
// (value= formatting with format tokens, grouping separators, level single/any/multiple,
// count= and from= patterns). Expected values derived by hand from XSLT 3.0
// ch 12 (Numbering), 13.1 (xsl:sort), 14 (Grouping) and 20.2 (Keys).

namespace OutSmart.DAXon.SpecTests.Cases;

public static class B17_GroupingKeys
{
    public static TestCase[] Cases() => new[]
    {
        new TestCase
        {
            Id = "grp-001",
            // Groups: key 'a' -> items 1,3; key 'b' -> items 2,4 (population order kept).
            Area = "xslt: grouping+keys+number",
            Title = "group-by string key: current-grouping-key and members per group",
            SpecRef = "XSLT 3.0 14.1, 14.2.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/i"" group-by=""@c"">
<xsl:value-of select=""current-grouping-key()""/>
<xsl:text>:</xsl:text>
<xsl:value-of select=""current-group()"" separator="",""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i c=""a"">1</i><i c=""b"">2</i><i c=""a"">3</i><i c=""b"">4</i></r>",
            Expected = "a:1,3;b:2,4;"
        },
        new TestCase
        {
            Id = "grp-002",
            // Keys compare as integers: "01" and "1" are one group, "2" and " 2 " another.
            SpecRef = "XSLT 3.0 14.3",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/i"" group-by=""xs:integer(@n)"">
<xsl:value-of select=""current-grouping-key()""/>
<xsl:text>:</xsl:text>
<xsl:value-of select=""current-group()"" separator="",""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i n=""2"">a</i><i n=""01"">b</i><i n=""1"">c</i><i n="" 2 "">d</i></r>",
            Expected = "2:a,d;1:b,c;"
        },
        new TestCase
        {
            Id = "grp-002b",
            // The same keys sorted, and each is still an xs:integer.
            SpecRef = "XSLT 3.0 14.3",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/i"" group-by=""xs:integer(@n)"">
<xsl:sort select=""current-grouping-key()"" data-type=""number""/>
<xsl:value-of select=""current-grouping-key() instance of xs:integer, current-grouping-key(), count(current-group())"" separator=""/""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i n=""2"">a</i><i n=""01"">b</i><i n=""1"">c</i><i n="" 2 "">d</i></r>",
            Expected = "true/1/2;true/2/2;"
        },
        new TestCase
        {
            Id = "grp-003",
            // Group 'x' holds one,three,four in population order; string-join proves membership.
            Area = "xslt: grouping+keys+number",
            Title = "current-group() carries all members: string-join proof",
            SpecRef = "XSLT 3.0 14.2.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/w"" group-by=""@t"">
<xsl:value-of select=""string-join(current-group(), '+')""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><w t=""x"">one</w><w t=""y"">two</w><w t=""x"">three</w><w t=""x"">four</w></r>",
            Expected = "one+three+four;two;"
        },
        new TestCase
        {
            Id = "grp-004",
            // Without xsl:sort, groups are processed in order of first appearance: c, a, b.
            Area = "xslt: grouping+keys+number",
            Title = "groups ordered by first appearance of each key",
            SpecRef = "XSLT 3.0 14.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/i"" group-by=""@k"">
<xsl:value-of select=""current-grouping-key()""/>
<xsl:value-of select=""count(current-group())""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i k=""c""/><i k=""a""/><i k=""b""/><i k=""a""/><i k=""c""/></r>",
            Expected = "c2;a2;b1;"
        },
        new TestCase
        {
            Id = "grp-005",
            // Composite keys pair-wise: (1,x)->P,R; (1,y)->Q; (2,x)->S.
            Area = "xslt: grouping+keys+number",
            Title = "composite='yes': two-value keys group pair-wise",
            SpecRef = "XSLT 3.0 14.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/i"" group-by=""@a, @b"" composite=""yes"">
<xsl:value-of select=""current-grouping-key()"" separator=""-""/>
<xsl:text>:</xsl:text>
<xsl:value-of select=""count(current-group())""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i a=""1"" b=""x"">P</i><i a=""1"" b=""y"">Q</i><i a=""1"" b=""x"">R</i><i a=""2"" b=""x"">S</i></r>",
            Expected = "1-x:2;1-y:1;2-x:1;"
        },
        new TestCase
        {
            Id = "grp-006",
            // Runs of equal adjacent values: aa | b | aaa. The 'b' forms a single-item group.
            Area = "xslt: grouping+keys+number",
            Title = "group-adjacent breaks on each value change, incl. single-item group",
            SpecRef = "XSLT 3.0 14.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/i"" group-adjacent=""."">
<xsl:value-of select=""current-grouping-key()""/>
<xsl:value-of select=""count(current-group())""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i>a</i><i>a</i><i>b</i><i>a</i><i>a</i><i>a</i></r>",
            Expected = "a2;b1;a3;"
        },
        new TestCase
        {
            Id = "grp-007",
            // First item x(0) does not match the pattern yet starts the first group;
            // each g starts a new group: [x0] [g1,x2] [g3,x4].
            Area = "xslt: grouping+keys+number",
            Title = "group-starting-with: first group starts at first item even if unmatched",
            SpecRef = "XSLT 3.0 14.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/*"" group-starting-with=""g"">
<xsl:value-of select=""current-group()"" separator=""""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><x>0</x><g>1</g><x>2</x><g>3</g><x>4</x></r>",
            Expected = "0;12;34;"
        },
        new TestCase
        {
            Id = "grp-008",
            // Groups end AT each e; trailing unmatched item forms the last group:
            // [1,2] [3,4,5] [6].
            Area = "xslt: grouping+keys+number",
            Title = "group-ending-with: groups end at matching items, trailing rest is last group",
            SpecRef = "XSLT 3.0 14.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/*"" group-ending-with=""e"">
<xsl:value-of select=""current-group()"" separator=""""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i>1</i><e>2</e><i>3</i><i>4</i><e>5</e><i>6</i></r>",
            Expected = "12;345;6;"
        },
        new TestCase
        {
            Id = "grp-009",
            // First-appearance order is b,c,a; xsl:sort on the grouping key reorders to a,b,c.
            Area = "xslt: grouping+keys+number",
            Title = "xsl:sort inside for-each-group orders groups by current-grouping-key()",
            SpecRef = "XSLT 3.0 14.1; 13.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/i"" group-by=""@k"">
<xsl:sort select=""current-grouping-key()""/>
<xsl:value-of select=""current-grouping-key()""/>
<xsl:value-of select=""count(current-group())""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i k=""b"">1</i><i k=""c"">2</i><i k=""a"">3</i><i k=""b"">4</i></r>",
            Expected = "a1;b2;c1;"
        },
        new TestCase
        {
            Id = "grp-010",
            // Group sizes x=1, y=3, z=2; descending numeric sort on count -> y,z,x.
            Area = "xslt: grouping+keys+number",
            Title = "sort groups by aggregate count(current-group()) descending",
            SpecRef = "XSLT 3.0 14.1; 13.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/i"" group-by=""@k"">
<xsl:sort select=""count(current-group())"" order=""descending""/>
<xsl:value-of select=""current-grouping-key()""/>
<xsl:value-of select=""count(current-group())""/>
<xsl:text>;</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i k=""x""/><i k=""y""/><i k=""y""/><i k=""z""/><i k=""y""/><i k=""z""/></r>",
            Expected = "y3;z2;x1;"
        },
        new TestCase
        {
            Id = "grp-011",
            // Outer groups by @a: 1->(P1,P2,P4), 2->(P3); inner regroups each by @b.
            Area = "xslt: grouping+keys+number",
            Title = "nested for-each-group over current-group()",
            SpecRef = "XSLT 3.0 14.1, 14.2.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/i"" group-by=""@a"">
<xsl:value-of select=""current-grouping-key()""/>
<xsl:text>[</xsl:text>
<xsl:for-each-group select=""current-group()"" group-by=""@b"">
<xsl:value-of select=""current-grouping-key()""/>
<xsl:value-of select=""count(current-group())""/>
</xsl:for-each-group>
<xsl:text>]</xsl:text>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i a=""1"" b=""x""/><i a=""1"" b=""y""/><i a=""2"" b=""x""/><i a=""1"" b=""x""/></r>",
            Expected = "1[x2y1]2[x1]"
        },
        new TestCase
        {
            Id = "grp-012",
            Area = "xslt: grouping+keys+number",
            Title = "xsl:key use='@attr': key() finds the matching element",
            SpecRef = "XSLT 3.0 20.2.1, 20.2.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:key name=""byId"" match=""item"" use=""@id""/>
<xsl:template match=""/"">
<xsl:value-of select=""key('byId','b')""/>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><item id=""a"">Alpha</item><item id=""b"">Beta</item><item id=""c"">Gamma</item></r>",
            Expected = "Beta"
        },
        new TestCase
        {
            Id = "grp-013",
            // Three items share cat='f'; fn:key returns them in document order.
            Area = "xslt: grouping+keys+number",
            Title = "key() returning multiple nodes yields document order",
            SpecRef = "XSLT 3.0 20.2.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:key name=""byCat"" match=""item"" use=""@cat""/>
<xsl:template match=""/"">
<xsl:value-of select=""key('byCat','f')"" separator="",""/>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><item cat=""f"">apple</item><item cat=""v"">carrot</item><item cat=""f"">banana</item><item cat=""f"">cherry</item></r>",
            Expected = "apple,banana,cherry"
        },
        new TestCase
        {
            Id = "grp-014",
            // use='name' indexes p by its child element's typed (string) value.
            Area = "xslt: grouping+keys+number",
            Title = "xsl:key use='child element' form; navigate from the keyed node",
            SpecRef = "XSLT 3.0 20.2.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:key name=""byName"" match=""p"" use=""name""/>
<xsl:template match=""/"">
<xsl:value-of select=""key('byName','bob')/age""/>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><p><name>ann</name><age>30</age></p><p><name>bob</name><age>25</age></p></r>",
            Expected = "25"
        },
        new TestCase
        {
            Id = "grp-015",
            Area = "xslt: grouping+keys+number",
            Title = "key() with no matching value returns the empty sequence",
            SpecRef = "XSLT 3.0 20.2.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:key name=""byId"" match=""item"" use=""@id""/>
<xsl:template match=""/"">
<xsl:value-of select=""count(key('byId','zzz'))""/>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><item id=""a"">Alpha</item></r>",
            Expected = "0"
        },
        new TestCase
        {
            Id = "grp-016",
            // Two declarations named kk (match='a' and match='b') act as a union index.
            Area = "xslt: grouping+keys+number",
            Title = "two xsl:key declarations with the same name act as a union",
            SpecRef = "XSLT 3.0 20.2.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:key name=""kk"" match=""a"" use=""@x""/>
<xsl:key name=""kk"" match=""b"" use=""@x""/>
<xsl:template match=""/"">
<xsl:value-of select=""key('kk','2')"" separator="",""/>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a x=""1"">A1</a><b x=""1"">B1</b><a x=""2"">A2</a><b x=""2"">B2</b></r>",
            Expected = "A2,B2"
        },
        new TestCase
        {
            Id = "grp-017",
            // use='.' keys each w on its text (string) value; w[1] and w[3] are 'cat'.
            Area = "xslt: grouping+keys+number",
            Title = "key on text value (use='.')",
            SpecRef = "XSLT 3.0 20.2.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:key name=""byText"" match=""w"" use="".""/>
<xsl:template match=""/"">
<xsl:value-of select=""key('byText','cat')/@id"" separator="",""/>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><w id=""1"">cat</w><w id=""2"">dog</w><w id=""3"">cat</w></r>",
            Expected = "1,3"
        },
        new TestCase
        {
            Id = "grp-018",
            // Bare xsl:number: level=single, count defaults to pattern 'item' -> 1,2,3.
            Area = "xslt: grouping+keys+number",
            Title = "xsl:number default (level=single, default count) numbers siblings",
            SpecRef = "XSLT 3.0 12.3",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each select=""r/item"">
<xsl:number/>
<xsl:value-of select="".""/>
</xsl:for-each>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><item>a</item><item>b</item><item>c</item></r>",
            Expected = "1a2b3c"
        },
        new TestCase
        {
            Id = "grp-019",
            // format='01' pads to width 2, '001' to width 3; width never truncates: 1234 stays.
            Area = "xslt: grouping+keys+number",
            Title = "xsl:number value= with decimal format tokens '1', '01', '001'",
            SpecRef = "XSLT 3.0 12.2, 12.4",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:number value=""7""/>
<xsl:text>|</xsl:text>
<xsl:number value=""7"" format=""01""/>
<xsl:text>|</xsl:text>
<xsl:number value=""7"" format=""001""/>
<xsl:text>|</xsl:text>
<xsl:number value=""1234"" format=""01""/>
</xsl:template>
</xsl:stylesheet>",
            Expected = "7|07|007|1234"
        },
        new TestCase
        {
            Id = "grp-020",
            // Sequence a..z then aa,ab,...: 26->z, 27->aa, 28->ab; 'A' gives B and AA.
            Area = "xslt: grouping+keys+number",
            Title = "xsl:number alphabetic format tokens 'a' and 'A'",
            SpecRef = "XSLT 3.0 12.2, 12.4",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:number value=""1"" format=""a""/>
<xsl:text>|</xsl:text>
<xsl:number value=""26"" format=""a""/>
<xsl:text>|</xsl:text>
<xsl:number value=""27"" format=""a""/>
<xsl:text>|</xsl:text>
<xsl:number value=""28"" format=""a""/>
<xsl:text>|</xsl:text>
<xsl:number value=""2"" format=""A""/>
<xsl:text>|</xsl:text>
<xsl:number value=""27"" format=""A""/>
</xsl:template>
</xsl:stylesheet>",
            Expected = "a|z|aa|ab|B|AA"
        },
        new TestCase
        {
            Id = "grp-021",
            // Standard subtractive Roman forms: 4=iv, 9=ix, 14=XIV, 40=XL, 2024=MMXXIV.
            Area = "xslt: grouping+keys+number",
            Title = "xsl:number roman format tokens 'i' and 'I'",
            SpecRef = "XSLT 3.0 12.2, 12.4",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:number value=""4"" format=""i""/>
<xsl:text>|</xsl:text>
<xsl:number value=""9"" format=""i""/>
<xsl:text>|</xsl:text>
<xsl:number value=""14"" format=""I""/>
<xsl:text>|</xsl:text>
<xsl:number value=""40"" format=""I""/>
<xsl:text>|</xsl:text>
<xsl:number value=""2024"" format=""I""/>
</xsl:template>
</xsl:stylesheet>",
            Expected = "iv|ix|XIV|XL|MMXXIV"
        },
        new TestCase
        {
            Id = "grp-022",
            Area = "xslt: grouping+keys+number",
            Title = "xsl:number grouping-separator=',' grouping-size='3'",
            SpecRef = "XSLT 3.0 12.4",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:number value=""1234567"" grouping-separator="","" grouping-size=""3""/>
</xsl:template>
</xsl:stylesheet>",
            Expected = "1,234,567"
        },
        new TestCase
        {
            Id = "grp-023",
            // Context is the 3rd b (5th element child). Default count pattern ('b') -> 3;
            // count='*' counts all element siblings -> 5.
            Area = "xslt: grouping+keys+number",
            Title = "level='single': count= pattern decides which siblings are counted",
            SpecRef = "XSLT 3.0 12.3",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each select=""r/b[3]"">
<xsl:number level=""single""/>
<xsl:text>|</xsl:text>
<xsl:number level=""single"" count=""*""/>
</xsl:for-each>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><a/><b/><a/><b/><b/></r>",
            Expected = "3|5"
        },
        new TestCase
        {
            Id = "grp-024",
            // level='any' counts every n at or before the context in document order:
            // s1/n1, s1/n2, s2/n1 precede + context itself = 4.
            Area = "xslt: grouping+keys+number",
            Title = "level='any' counts matching nodes anywhere before, plus self",
            SpecRef = "XSLT 3.0 12.3",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each select=""doc/s[2]/n[2]"">
<xsl:number level=""any"" count=""n""/>
</xsl:for-each>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<doc><s><n/><n/></s><s><n/><n/></s></doc>",
            Expected = "4"
        },
        new TestCase
        {
            Id = "grp-025",
            // Whole doc holds 4 f's up to the context; from='ch' restarts counting after
            // the most recent ch (the second one), leaving only 2.
            Area = "xslt: grouping+keys+number",
            Title = "level='any' with from= pattern restarts the count",
            SpecRef = "XSLT 3.0 12.3",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each select=""doc/ch[2]/f[2]"">
<xsl:number level=""any"" count=""f""/>
<xsl:text>|</xsl:text>
<xsl:number level=""any"" count=""f"" from=""ch""/>
</xsl:for-each>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<doc><ch><f/><f/></ch><ch><f/><f/><f/></ch></doc>",
            Expected = "4|2"
        },
        new TestCase
        {
            Id = "grp-026",
            // Ancestor-or-self nodes matching chap|sec: chap[2] (2nd chap) and sec[3]
            // (3rd sec) -> (2,3); picture '1.1' -> 2.3, picture '1.a' -> 2.c.
            Area = "xslt: grouping+keys+number",
            Title = "level='multiple' with format='1.1' produces hierarchical numbering",
            SpecRef = "XSLT 3.0 12.3, 12.4",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each select=""doc/chap[2]/sec[3]"">
<xsl:number level=""multiple"" count=""chap|sec"" format=""1.1""/>
<xsl:text>|</xsl:text>
<xsl:number level=""multiple"" count=""chap|sec"" format=""1.a""/>
</xsl:for-each>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<doc><chap><sec/><sec/></chap><chap><sec/><sec/><sec/></chap></doc>",
            Expected = "2.3|2.c"
        },
        new TestCase
        {
            Id = "grp-027",
            // Static error: two of the four grouping attributes on one xsl:for-each-group.
            Area = "xslt: grouping+keys+number",
            Title = "group-by together with group-adjacent raises XTSE1080",
            SpecRef = "XSLT 3.0 14.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/*"" group-by=""."" group-adjacent=""."">
<xsl:value-of select=""current-grouping-key()""/>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i>a</i></r>",
            ExpectedErrorCode = "XTSE1080"
        },
        new TestCase
        {
            Id = "grp-028",
            // Without composite='yes' a group-adjacent key must be exactly one item;
            // @missing atomizes to the empty sequence.
            Area = "xslt: grouping+keys+number",
            Title = "group-adjacent key evaluating to empty sequence raises XTTE1100",
            SpecRef = "XSLT 3.0 14.1",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:for-each-group select=""r/i"" group-adjacent=""@missing"">
<xsl:value-of select=""count(current-group())""/>
</xsl:for-each-group>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i>a</i><i>b</i></r>",
            ExpectedErrorCode = "XTTE1100"
        },
        new TestCase
        {
            Id = "grp-029",
            // fn:key with a name matching no xsl:key declaration.
            Area = "xslt: grouping+keys+number",
            Title = "key() naming an undeclared key raises XTDE1260",
            SpecRef = "XSLT 3.0 20.2.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:value-of select=""count(key('nosuch','x'))""/>
</xsl:template>
</xsl:stylesheet>",
            InputXml = @"<r><i>a</i></r>",
            ExpectedErrorCode = "XTDE1260"
        },
        new TestCase
        {
            Id = "grp-030",
            // xsl:number value must convert to an integer >= 0.
            Area = "xslt: grouping+keys+number",
            Title = "xsl:number value='-1' raises XTDE0980",
            SpecRef = "XSLT 3.0 12.2",
            Xslt = @"<xsl:stylesheet version=""3.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
<xsl:output method=""text""/>
<xsl:template match=""/"">
<xsl:number value=""-1""/>
</xsl:template>
</xsl:stylesheet>",
            ExpectedErrorCode = "XTDE0980"
        }
    };
}
