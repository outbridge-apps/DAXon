namespace OutSmart.DAXon.SpecTests.Cases;

// Spec-derived tests for XPath 3.1 path expressions and axes (XPath 3.1 sect. 3.3),
// node/kind tests, positional and reverse-axis predicate semantics, and the
// node-sequence combinators union/intersect/except (sect. 3.4.2).
// All expected values derived by hand from the W3C XPath 3.1 and F&O 3.1 specs.
//
// Primary input document (no insignificant whitespace; document order):
//   document-node
//     doc
//       a id="a1"  text "one"
//       b id="b1"
//         c id="c1"  text "two"
//         c id="c2"  text "three"
//         comment "note"
//         PI target pi1, content "data"
//         text "tail"
//       a id="a2"  text "four"
public static class B01_XPathAxes
{
    public static TestCase[] Cases() => new[]
    {
        new TestCase
        {
            Id = "axes-001",
            Area = "xpath: axes+paths",
            Title = "child axis name test selects both a children in document order",
            SpecRef = "XPath 3.1 3.3.2.1",
            XPathExpr = @"/doc/a",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "one|four"
        },
        new TestCase
        {
            Id = "axes-002",
            // descendant::* from doc node: doc,a1,b,c1,c2,a2 = 6; b/descendant-or-self::*: b,c1,c2 = 3.
            Area = "xpath: axes+paths",
            Title = "descendant and descendant-or-self element counts",
            SpecRef = "XPath 3.1 3.3.2.1",
            XPathExpr = @"concat(count(/descendant::*), ':', count(/doc/b/descendant-or-self::*))",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "6:3"
        },
        new TestCase
        {
            Id = "axes-003",
            Area = "xpath: axes+paths",
            Title = "abbreviated .. selects the parent element",
            SpecRef = "XPath 3.1 3.3.5",
            XPathExpr = @"string(/doc/b/c[1]/../@id)",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "b1"
        },
        new TestCase
        {
            Id = "axes-004",
            // ancestor axis is a reverse axis, but the path result is returned in document order: doc then b.
            Area = "xpath: axes+paths",
            Title = "ancestor axis path result is in document order",
            SpecRef = "XPath 3.1 3.3.2.1",
            XPathExpr = @"string-join(//c[@id='c2']/ancestor::*/name(), ',')",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "doc,b"
        },
        new TestCase
        {
            Id = "axes-005",
            // Siblings after c1: element c2, comment, PI, text 'tail' = 4 nodes.
            Area = "xpath: axes+paths",
            Title = "following-sibling::node() includes element, comment, PI and text siblings",
            SpecRef = "XPath 3.1 3.3.2.1",
            XPathExpr = @"count(//c[@id='c1']/following-sibling::node())",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "4"
        },
        new TestCase
        {
            Id = "axes-006",
            Area = "xpath: axes+paths",
            Title = "preceding-sibling element results delivered in document order",
            SpecRef = "XPath 3.1 3.3.2.1",
            XPathExpr = @"string-join(//a[@id='a2']/preceding-sibling::*/@id, ',')",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "a1,b1"
        },
        new TestCase
        {
            Id = "axes-007",
            // Following of c1 (excludes its descendant text 'two' and all ancestors):
            // c2, "three", comment, PI, "tail", a2, "four" = 7 nodes.
            Area = "xpath: axes+paths",
            Title = "following axis excludes descendants and ancestors",
            SpecRef = "XPath 3.1 3.3.2.1",
            XPathExpr = @"count(//c[@id='c1']/following::node())",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "7"
        },
        new TestCase
        {
            Id = "axes-008",
            // Preceding of c2 excludes ancestors (document node, doc, b): a1, "one", c1, "two" = 4.
            Area = "xpath: axes+paths",
            Title = "preceding axis excludes ancestors but keeps earlier subtrees",
            SpecRef = "XPath 3.1 3.3.2.1",
            XPathExpr = @"count(//c[@id='c2']/preceding::node())",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "4"
        },
        new TestCase
        {
            Id = "axes-009",
            Area = "xpath: axes+paths",
            Title = "self axis with name test filters a node sequence by name",
            SpecRef = "XPath 3.1 3.3.2.1",
            XPathExpr = @"string-join(/doc/*/self::a/@id, ',')",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "a1,a2"
        },
        new TestCase
        {
            Id = "axes-010",
            // Context item is the document node; document-node(element(doc)) matches a document
            // whose single element child is named doc; element(nope) does not match.
            Area = "xpath: axes+paths",
            Title = "document-node() kind tests with and without an element test",
            SpecRef = "XPath 3.1 3.3.2.2",
            XPathExpr = @"concat(exists(self::document-node()), ',', exists(self::document-node(element(doc))), ',', exists(self::document-node(element(nope))))",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "true,true,false"
        },
        new TestCase
        {
            Id = "axes-011",
            Area = "xpath: axes+paths",
            Title = "name() of the document node is the zero-length string",
            SpecRef = "F&O 14.1",
            XPathExpr = @"concat('[', name(/), ']')",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "[]"
        },
        new TestCase
        {
            Id = "axes-012",
            // //i[1] means descendant-or-self::node()/child::i[1]: the first i child of EACH parent.
            Area = "xpath: axes+paths",
            Title = "//i[1] applies the predicate per step, not to the whole result",
            SpecRef = "XPath 3.1 3.3.3",
            XPathExpr = @"//i[1]",
            InputXml = @"<r><s><i>1</i><i>2</i></s><s><i>3</i></s></r>",
            Expected = "1|3"
        },
        new TestCase
        {
            Id = "axes-013",
            Area = "xpath: axes+paths",
            Title = "//i[last()] selects the last i child of each parent",
            SpecRef = "XPath 3.1 3.3.3",
            XPathExpr = @"//i[last()]",
            InputXml = @"<r><s><i>1</i><i>2</i></s><s><i>3</i></s></r>",
            Expected = "2|3"
        },
        new TestCase
        {
            Id = "axes-014",
            // Parenthesizing first collects all three i elements; the filter then takes the last one.
            Area = "xpath: axes+paths",
            Title = "(//i)[last()] filters the whole document-order sequence",
            SpecRef = "XPath 3.1 3.2.1",
            XPathExpr = @"(//i)[last()]",
            InputXml = @"<r><s><i>1</i><i>2</i></s><s><i>3</i></s></r>",
            Expected = "3"
        },
        new TestCase
        {
            Id = "axes-015",
            Area = "xpath: axes+paths",
            Title = "position() predicate on a forward child step",
            SpecRef = "XPath 3.1 3.3.3",
            XPathExpr = @"string-join(/doc/*[position() gt 1]/@id, ',')",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "b1,a2"
        },
        new TestCase
        {
            Id = "axes-016",
            // First predicate keeps (b,a2); positions renumber, so [1] then yields b.
            Area = "xpath: axes+paths",
            Title = "multiple predicates apply left to right with renumbered positions",
            SpecRef = "XPath 3.1 3.3.3",
            XPathExpr = @"string(/doc/*[position() gt 1][1]/@id)",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "b1"
        },
        new TestCase
        {
            Id = "axes-017",
            // ancestor is a reverse axis: position 1 is the NEAREST ancestor, i.e. the parent b.
            Area = "xpath: axes+paths",
            Title = "ancestor::*[1] is the parent (reverse-axis positional semantics)",
            SpecRef = "XPath 3.1 3.3.3",
            XPathExpr = @"name(//c[@id='c2']/ancestor::*[1])",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "b"
        },
        new TestCase
        {
            Id = "axes-018",
            Area = "xpath: axes+paths",
            Title = "preceding-sibling::*[1] is the immediately preceding sibling",
            SpecRef = "XPath 3.1 3.3.3",
            XPathExpr = @"string(//a[@id='a2']/preceding-sibling::*[1]/@id)",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "b1"
        },
        new TestCase
        {
            Id = "axes-019",
            // b has exactly one child text node ("two"/"three" belong to the c elements).
            Area = "xpath: axes+paths",
            Title = "text() kind test selects only child text nodes",
            SpecRef = "XPath 3.1 3.3.2.2",
            XPathExpr = @"string(/doc/b/text())",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "tail"
        },
        new TestCase
        {
            Id = "axes-020",
            Area = "xpath: axes+paths",
            Title = "comment() kind test selects the comment child",
            SpecRef = "XPath 3.1 3.3.2.2",
            XPathExpr = @"string(/doc/b/comment())",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "note"
        },
        new TestCase
        {
            Id = "axes-021",
            // PI name is its target; PI string value excludes the target and leading whitespace.
            Area = "xpath: axes+paths",
            Title = "processing-instruction() and processing-instruction(name) kind tests",
            SpecRef = "XPath 3.1 3.3.2.2",
            XPathExpr = @"concat(name(/doc/b/processing-instruction()), '=', string(/doc/b/processing-instruction('pi1')))",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "pi1=data"
        },
        new TestCase
        {
            Id = "axes-022",
            // Children of b: c1, c2, comment, PI, text = 5; attributes are NOT children. a1 has one attribute.
            Area = "xpath: axes+paths",
            Title = "node() matches all children but not attributes; @* counts attributes",
            SpecRef = "XPath 3.1 3.3.2.2",
            XPathExpr = @"concat(count(/doc/b/node()), ':', count(/doc/a[1]/@*))",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "5:1"
        },
        new TestCase
        {
            Id = "axes-023",
            // element(a) matches the two a elements; bare element() matches all 6 elements.
            Area = "xpath: axes+paths",
            Title = "element(name) and bare element() kind tests",
            SpecRef = "XPath 3.1 3.3.2.2",
            XPathExpr = @"concat(string-join(//element(a)/@id, ','), ':', count(//element()))",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "a1,a2:6"
        },
        new TestCase
        {
            Id = "axes-024",
            // A step whose node test is an AttributeTest defaults to the attribute axis,
            // so //attribute(id) finds all five id attributes.
            Area = "xpath: axes+paths",
            Title = "attribute(id) kind test defaults to the attribute axis",
            SpecRef = "XPath 3.1 3.3.5",
            XPathExpr = @"count(//attribute(id))",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "5"
        },
        new TestCase
        {
            Id = "axes-025",
            // Union removes the duplicate c2 and sorts into document order: a1 before c2.
            Area = "xpath: axes+paths",
            Title = "union eliminates duplicates and yields document order",
            SpecRef = "XPath 3.1 3.4.2",
            XPathExpr = @"string-join((//c[@id='c2'] | //a[@id='a1'] | //c[@id='c2'])/@id, ',')",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "a1,c2"
        },
        new TestCase
        {
            Id = "axes-026",
            Area = "xpath: axes+paths",
            Title = "intersect keeps only nodes present in both operands",
            SpecRef = "XPath 3.1 3.4.2",
            XPathExpr = @"string-join((/doc/* intersect //a)/@id, ',')",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "a1,a2"
        },
        new TestCase
        {
            Id = "axes-027",
            Area = "xpath: axes+paths",
            Title = "except removes nodes present in the second operand",
            SpecRef = "XPath 3.1 3.4.2",
            XPathExpr = @"string-join((/doc/* except //a)/@id, ',')",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "b1"
        },
        new TestCase
        {
            Id = "axes-028",
            // Both c elements contribute (doc,b,self); the path operator de-duplicates doc and b
            // and returns document order: doc, b, c1, c2.
            Area = "xpath: axes+paths",
            Title = "path operator de-duplicates and document-orders step results",
            SpecRef = "XPath 3.1 3.3.1.1",
            XPathExpr = @"string-join(//c/ancestor-or-self::*/name(), ',')",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            Expected = "doc,b,c,c"
        },
        new TestCase
        {
            Id = "axes-029",
            // *:item matches elements with local name item in any namespace or none.
            Area = "xpath: axes+paths",
            Title = "*:localname wildcard matches across namespaces",
            SpecRef = "XPath 3.1 3.3.2.2",
            XPathExpr = @"//*:item",
            InputXml = @"<r xmlns:p=""urn:x""><p:item>A</p:item><item>B</item><p:other>C</p:other></r>",
            Expected = "A|B"
        },
        new TestCase
        {
            Id = "axes-030",
            // E1 of E1/E2 evaluates to atomic values, which is a type error.
            Area = "xpath: axes+paths",
            Title = "path step applied to atomic values raises XPTY0019",
            SpecRef = "XPath 3.1 3.3.1.1",
            XPathExpr = @"(1 to 3)/child::x",
            ExpectedErrorCode = "XPTY0019"
        },
        new TestCase
        {
            Id = "axes-031",
            // The final step yields both element nodes and the atomic value 1.
            Area = "xpath: axes+paths",
            Title = "final path step mixing nodes and atomics raises XPTY0018",
            SpecRef = "XPath 3.1 3.3.1.1",
            XPathExpr = @"/doc/(*, 1)",
            InputXml = @"<doc><a id=""a1"">one</a><b id=""b1""><c id=""c1"">two</c><c id=""c2"">three</c><!--note--><?pi1 data?>tail</b><a id=""a2"">four</a></doc>",
            ExpectedErrorCode = "XPTY0018"
        },
        new TestCase
        {
            Id = "axes-032",
            // Inside the predicate the context item is the atomic 1/2; an axis step then errors.
            Area = "xpath: axes+paths",
            Title = "axis step with non-node context item raises XPTY0020",
            SpecRef = "XPath 3.1 3.3.2",
            XPathExpr = @"(1, 2)[self::node()]",
            ExpectedErrorCode = "XPTY0020"
        }
    };
}
