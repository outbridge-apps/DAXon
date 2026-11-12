// B03_SequencesFlow.cs
// Spec-derived test cases: XPath 3.1 sequence machinery (for/let/quantifiers)
// and F&O 3.1 sequence functions. Expected values derived by hand from the specs.

namespace OutSmart.DAXon.SpecTests.Cases;

public static class B03_SequencesFlow
{
    public static TestCase[] Cases() => new[]
    {
        // ---------- for expressions (XPath 3.1 3.9) ----------

        new TestCase
        {
            Id = "seq-001",
            Area = "xpath: sequences+flwor",
            Title = "for-return maps each item of the binding sequence",
            SpecRef = "XPath 3.1 3.9",
            XPathExpr = @"for $i in 1 to 4 return $i * $i",
            Expected = "1|4|9|16"
        },
        new TestCase
        {
            Id = "seq-002",
            Area = "xpath: sequences+flwor",
            Title = "for with two clauses is a cartesian product, leftmost varies slowest",
            SpecRef = "XPath 3.1 3.9",
            XPathExpr = @"for $i in (1, 2), $j in (10, 20) return $i + $j",
            Expected = "11|21|12|22"
        },
        new TestCase
        {
            Id = "seq-003",
            Area = "xpath: sequences+flwor",
            Title = "for over an empty binding sequence yields the empty sequence",
            SpecRef = "XPath 3.1 3.9",
            XPathExpr = @"empty(for $x in () return $x + 1)",
            Expected = "true"
        },

        // ---------- let expressions (XPath 3.1 3.10) ----------

        new TestCase
        {
            Id = "seq-004",
            Area = "xpath: sequences+flwor",
            Title = "let: a later binding sees an earlier one",
            SpecRef = "XPath 3.1 3.10",
            XPathExpr = @"let $x := 5, $y := $x + 2 return $x * $y",
            Expected = "35"
        },
        new TestCase
        {
            Id = "seq-005",
            Area = "xpath: sequences+flwor",
            Title = "nested let shadows the outer variable; inner init sees outer value",
            SpecRef = "XPath 3.1 3.10",
            XPathExpr = @"let $x := 2 return (let $x := $x + 1 return $x * $x)",
            Expected = "9"
        },

        // ---------- quantified expressions (XPath 3.1 3.13) ----------

        new TestCase
        {
            Id = "seq-006",
            Area = "xpath: sequences+flwor",
            Title = "some: single-var match, two-var match, and no match",
            SpecRef = "XPath 3.1 3.13",
            XPathExpr = @"(some $x in (1, 2, 3) satisfies $x mod 2 = 0, some $x in (1, 2), $y in (2, 3) satisfies $x = $y, some $x in (1, 3, 5) satisfies $x mod 2 = 0)",
            Expected = "true|true|false"
        },
        new TestCase
        {
            Id = "seq-007",
            Area = "xpath: sequences+flwor",
            Title = "every: all items pass vs one item fails",
            SpecRef = "XPath 3.1 3.13",
            XPathExpr = @"(every $x in (1, 2, 3) satisfies $x <= 3, every $x in (1, 2, 3) satisfies $x < 3)",
            Expected = "true|false"
        },
        new TestCase
        {
            Id = "seq-008",
            Area = "xpath: sequences+flwor",
            Title = "quantifiers over empty sequence: some is false, every is true",
            SpecRef = "XPath 3.1 3.13",
            XPathExpr = @"(some $x in () satisfies true(), every $x in () satisfies false())",
            Expected = "false|true"
        },

        // ---------- fn:empty / fn:exists (F&O 14.1.1, 14.1.2) ----------

        new TestCase
        {
            Id = "seq-009",
            Area = "xpath: sequences+flwor",
            Title = "empty/exists: () vs single items 0 and empty string",
            SpecRef = "F&O 14.1.1, 14.1.2",
            XPathExpr = @"(empty(()), empty(0), exists(()), exists(''))",
            Expected = "true|false|false|true"
        },

        // ---------- fn:head / fn:tail (F&O 14.1.3, 14.1.4) ----------

        new TestCase
        {
            Id = "seq-010",
            Area = "xpath: sequences+flwor",
            Title = "head/tail basics; head(()) empty, tail of singleton empty",
            SpecRef = "F&O 14.1.3, 14.1.4",
            XPathExpr = @"(head(1 to 5), string-join(tail(('a', 'b', 'c')), ''), empty(head(())), empty(tail(('only'))))",
            Expected = "1|bc|true|true"
        },

        // ---------- fn:insert-before (F&O 14.1.5) ----------

        new TestCase
        {
            Id = "seq-011",
            Area = "xpath: sequences+flwor",
            Title = "insert-before at middle; position below 1 clamps to start, past end appends",
            SpecRef = "F&O 14.1.5",
            XPathExpr = @"(string-join(insert-before(('a', 'b', 'c'), 2, 'X'), ''), string-join(insert-before(('a', 'b'), 0, 'X'), ''), string-join(insert-before(('a', 'b'), 9, 'Y'), ''))",
            Expected = "aXbc|Xab|abY"
        },

        // ---------- fn:remove (F&O 14.1.6) ----------

        new TestCase
        {
            Id = "seq-012",
            Area = "xpath: sequences+flwor",
            Title = "remove middle item; out-of-range position is a no-op",
            SpecRef = "F&O 14.1.6",
            XPathExpr = @"(string-join(remove(('a', 'b', 'c'), 2), ''), string-join(remove(('a', 'b', 'c'), 0), ''), string-join(remove(('a', 'b', 'c'), 4), ''))",
            Expected = "ac|abc|abc"
        },

        // ---------- fn:reverse (F&O 14.1.7) ----------

        new TestCase
        {
            Id = "seq-013",
            Area = "xpath: sequences+flwor",
            Title = "reverse reverses order; reverse of empty is empty",
            SpecRef = "F&O 14.1.7",
            XPathExpr = @"(reverse(1 to 4), empty(reverse(())))",
            Expected = "4|3|2|1|true"
        },

        // ---------- fn:subsequence (F&O 14.1.8) ----------

        new TestCase
        {
            Id = "seq-014",
            Area = "xpath: sequences+flwor",
            Title = "subsequence: 2-arg takes to end; length past end clips",
            SpecRef = "F&O 14.1.8",
            XPathExpr = @"(string-join(subsequence(('a', 'b', 'c', 'd', 'e'), 3), ''), string-join(subsequence(('a', 'b', 'c'), 2, 100), ''))",
            Expected = "cde|bc"
        },
        new TestCase
        {
            Id = "seq-015",
            Area = "xpath: sequences+flwor",
            // round(1.5)=2, round(2.5)=3 (half toward +INF), so positions p with 2 <= p < 5.
            Title = "subsequence rounds fractional start and length per fn:round",
            SpecRef = "F&O 14.1.8",
            XPathExpr = @"subsequence((1, 2, 3, 4, 5), 1.5, 2.5)",
            Expected = "2|3|4"
        },

        // ---------- fn:unordered (F&O 14.1.9) ----------

        new TestCase
        {
            Id = "seq-016",
            Area = "xpath: sequences+flwor",
            Title = "unordered preserves the items (probed via sort, order-independent)",
            SpecRef = "F&O 14.1.9",
            XPathExpr = @"sort(unordered((3, 1, 2)))",
            Expected = "1|2|3"
        },

        // ---------- fn:distinct-values (F&O 14.2.1) ----------

        new TestCase
        {
            Id = "seq-017",
            Area = "xpath: sequences+flwor",
            Title = "distinct-values removes duplicates (result order probed via sort)",
            SpecRef = "F&O 14.2.1",
            XPathExpr = @"sort(distinct-values((3, 1, 2, 3, 1, 2)))",
            Expected = "1|2|3"
        },
        new TestCase
        {
            Id = "seq-018",
            Area = "xpath: sequences+flwor",
            Title = "distinct-values equates equal values across numeric types",
            SpecRef = "F&O 14.2.1",
            XPathExpr = @"count(distinct-values((1, 1.0, xs:double('1'), 2)))",
            Expected = "2"
        },

        // ---------- fn:index-of (F&O 14.2.2) ----------

        new TestCase
        {
            Id = "seq-019",
            Area = "xpath: sequences+flwor",
            Title = "index-of returns all match positions in order; no match yields empty",
            SpecRef = "F&O 14.2.2",
            XPathExpr = @"(index-of((10, 20, 30, 20), 20), count(index-of((1, 2, 3), 9)))",
            Expected = "2|4|0"
        },

        // ---------- fn:deep-equal (F&O 14.2.3) ----------

        new TestCase
        {
            Id = "seq-020",
            Area = "xpath: sequences+flwor",
            Title = "deep-equal atomics: cross-type numeric eq, NaN equals NaN, order matters",
            SpecRef = "F&O 14.2.3",
            XPathExpr = @"(deep-equal((1, 2), (1.0, 2.0)), deep-equal(xs:double('NaN'), xs:double('NaN')), deep-equal((1, 2), (2, 1)))",
            Expected = "true|true|false"
        },
        new TestCase
        {
            Id = "seq-021",
            Area = "xpath: sequences+flwor",
            Title = "deep-equal on element nodes compares names and content recursively",
            SpecRef = "F&O 14.2.3",
            XPathExpr = @"(deep-equal(/r/a[1], /r/a[2]), deep-equal(/r/a[1], /r/a[3]))",
            InputXml = @"<r><a><b>1</b></a><a><b>1</b></a><a><b>2</b></a></r>",
            Expected = "true|false"
        },

        // ---------- fn:count (F&O 14.4.1) ----------

        new TestCase
        {
            Id = "seq-022",
            Area = "xpath: sequences+flwor",
            Title = "count: empty is 0, nested sequences flatten, reversed range is empty",
            SpecRef = "F&O 14.4.1",
            XPathExpr = @"(count(()), count((1, (2, 3), ())), count(3 to 1), count(1 to 100))",
            Expected = "0|3|0|100"
        },

        // ---------- fn:sum (F&O 14.4.5) ----------

        new TestCase
        {
            Id = "seq-023",
            Area = "xpath: sequences+flwor",
            Title = "sum: empty is 0, range sums, explicit zero for empty, exact decimals",
            SpecRef = "F&O 14.4.5",
            XPathExpr = @"(sum(()), sum(1 to 10), sum((), -1), sum((1.5, 2.5)))",
            Expected = "0|55|-1|4"
        },

        // ---------- fn:avg (F&O 14.4.2) ----------

        new TestCase
        {
            Id = "seq-024",
            Area = "xpath: sequences+flwor",
            Title = "avg of integers yields exact decimal; avg of empty is empty",
            SpecRef = "F&O 14.4.2",
            XPathExpr = @"(string(avg((1, 2, 3, 4))), empty(avg(())))",
            Expected = "2.5|true"
        },

        // ---------- fn:min / fn:max (F&O 14.4.3, 14.4.4) ----------

        new TestCase
        {
            Id = "seq-025",
            Area = "xpath: sequences+flwor",
            Title = "min/max with numeric promotion (decimal, double) and strings",
            SpecRef = "F&O 14.4.3, 14.4.4",
            XPathExpr = @"(string(min((2, 1.5))), string(max((2, 3.5e0))), min(('b', 'a', 'c')), max((2, 7, 5)))",
            Expected = "1.5|3.5|a|7"
        },
        new TestCase
        {
            Id = "seq-026",
            Area = "xpath: sequences+flwor",
            Title = "min/max of empty is empty; a NaN in the input yields NaN",
            SpecRef = "F&O 14.4.3, 14.4.4",
            XPathExpr = @"(empty(min(())), empty(max(())), string(min((1, xs:double('NaN')))), string(max((1, xs:double('NaN')))))",
            Expected = "true|true|NaN|NaN"
        },

        // ---------- cardinality functions (F&O 14.3) ----------

        new TestCase
        {
            Id = "seq-027",
            Area = "xpath: sequences+flwor",
            Title = "zero-or-one/one-or-more/exactly-one success paths return the input",
            SpecRef = "F&O 14.3",
            XPathExpr = @"(zero-or-one(()), zero-or-one(7), one-or-more((8, 9)), exactly-one(3))",
            Expected = "7|8|9|3"
        },
        new TestCase
        {
            Id = "seq-028",
            Area = "xpath: sequences+flwor",
            Title = "zero-or-one with two items raises FORG0003",
            SpecRef = "F&O 14.3.1",
            XPathExpr = @"zero-or-one((1, 2))",
            ExpectedErrorCode = "FORG0003"
        },
        new TestCase
        {
            Id = "seq-029",
            Area = "xpath: sequences+flwor",
            Title = "one-or-more with empty sequence raises FORG0004",
            SpecRef = "F&O 14.3.2",
            XPathExpr = @"one-or-more(())",
            ExpectedErrorCode = "FORG0004"
        },
        new TestCase
        {
            Id = "seq-030",
            Area = "xpath: sequences+flwor",
            Title = "exactly-one with two items raises FORG0005",
            SpecRef = "F&O 14.3.3",
            XPathExpr = @"exactly-one((1, 2))",
            ExpectedErrorCode = "FORG0005"
        }
    };
}
