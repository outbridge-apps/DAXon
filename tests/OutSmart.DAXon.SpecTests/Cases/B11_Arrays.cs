namespace OutSmart.DAXon.SpecTests.Cases;

// Spec-derived test cases for XPath 3.1 arrays (constructors, array:* functions,
// lookup operators, atomization). Expected values derived by hand from
// XPath 3.1 sections 2.4.2, 3.4, 3.11.2, 3.11.3 and F&O 3.1 section 17.3.
public static class B11_Arrays
{
    public static TestCase[] Cases() => new[]
    {
        // --- Constructors -------------------------------------------------

        new TestCase
        {
            Id = "arr-001",
            Area = "xpath: arrays",
            Title = "Square constructor: one member per ExprSingle; curly constructor: one member per item",
            SpecRef = "XPath 3.1 3.11.2.1",
            // Square: members are (1,2) and 3 -> size 2. Curly: sequence (1,2,3) -> size 3.
            XPathExpr = @"(array:size([ (1, 2), 3 ]), array:size(array { (1, 2), 3 }))",
            Expected = @"2|3"
        },

        // --- array:get / array:put / array:append -------------------------

        new TestCase
        {
            Id = "arr-002",
            Area = "xpath: arrays",
            Title = "array:get uses 1-based member positions",
            SpecRef = "F&O 17.3.2",
            XPathExpr = @"array:get(['a', 'b', 'c'], 2)",
            Expected = @"b"
        },
        new TestCase
        {
            Id = "arr-003",
            Area = "xpath: arrays",
            Title = "array:get with position 0 raises FOAY0001",
            SpecRef = "F&O 17.3.2",
            XPathExpr = @"array:get([1, 2], 0)",
            ExpectedErrorCode = "FOAY0001"
        },
        new TestCase
        {
            Id = "arr-004",
            Area = "xpath: arrays",
            Title = "array:put replaces the member at the given position",
            SpecRef = "F&O 17.3.3",
            XPathExpr = @"string-join(array:put(['a', 'b', 'c'], 2, 'X')?*, '')",
            Expected = @"aXc"
        },
        new TestCase
        {
            Id = "arr-005",
            Area = "xpath: arrays",
            Title = "array:append adds the whole appended sequence as a single member",
            SpecRef = "F&O 17.3.4",
            // Size becomes 3 and member 3 is the two-item sequence (3,4).
            XPathExpr = @"(array:size(array:append([1, 2], (3, 4))), array:get(array:append([1, 2], (3, 4)), 3))",
            Expected = @"3|3|4"
        },

        // --- array:subarray ------------------------------------------------

        new TestCase
        {
            Id = "arr-006",
            Area = "xpath: arrays",
            Title = "array:subarray two-arg takes to end, three-arg takes given length",
            SpecRef = "F&O 17.3.5",
            XPathExpr = @"(string-join(array:subarray([1, 2, 3, 4, 5], 3)?*, ''), string-join(array:subarray(['a', 'b', 'c', 'd'], 2, 2)?*, ''))",
            Expected = @"345|bc"
        },
        new TestCase
        {
            Id = "arr-007",
            Area = "xpath: arrays",
            Title = "array:subarray with start+length past the end raises FOAY0001",
            SpecRef = "F&O 17.3.5",
            // start 2 + length 3 = 5 > size 3 + 1 = 4
            XPathExpr = @"array:subarray([1, 2, 3], 2, 3)",
            ExpectedErrorCode = "FOAY0001"
        },

        // --- array:remove / array:insert-before ---------------------------

        new TestCase
        {
            Id = "arr-008",
            Area = "xpath: arrays",
            Title = "array:remove accepts positions in any order; empty position sequence is a no-op",
            SpecRef = "F&O 17.3.6",
            XPathExpr = @"(string-join(array:remove(['a', 'b', 'c', 'd'], (3, 1))?*, ''), array:size(array:remove([1, 2, 3], ())))",
            Expected = @"bd|3"
        },
        new TestCase
        {
            Id = "arr-009",
            Area = "xpath: arrays",
            Title = "array:insert-before inserts mid-array and appends at position size+1",
            SpecRef = "F&O 17.3.7",
            XPathExpr = @"(string-join(array:insert-before(['a', 'c'], 2, 'b')?*, ''), string-join(array:insert-before(['x'], 2, 'y')?*, ''))",
            Expected = @"abc|xy"
        },

        // --- array:head / array:tail ---------------------------------------

        new TestCase
        {
            Id = "arr-010",
            Area = "xpath: arrays",
            Title = "array:head returns the first member sequence; array:tail drops it; tail of singleton is empty array",
            SpecRef = "F&O 17.3.8, 17.3.9",
            // head([ (1,2), 3 ]) = (1,2); tail([1,2,3]) = [2,3]; tail([9]) = []
            XPathExpr = @"(array:head([ (1, 2), 3 ]), string-join(array:tail([1, 2, 3])?*, ''), array:size(array:tail([9])))",
            Expected = @"1|2|23|0"
        },
        new TestCase
        {
            Id = "arr-011",
            Area = "xpath: arrays",
            Title = "array:head on empty array raises FOAY0001",
            SpecRef = "F&O 17.3.8",
            XPathExpr = @"array:head([])",
            ExpectedErrorCode = "FOAY0001"
        },

        // --- array:reverse / array:join / array:flatten --------------------

        new TestCase
        {
            Id = "arr-012",
            Area = "xpath: arrays",
            Title = "array:reverse reverses member order",
            SpecRef = "F&O 17.3.10",
            XPathExpr = @"string-join(array:reverse(['a', 'b', 'c'])?*, '')",
            Expected = @"cba"
        },
        new TestCase
        {
            Id = "arr-013",
            Area = "xpath: arrays",
            Title = "array:join concatenates member lists, empty arrays contribute nothing",
            SpecRef = "F&O 17.3.11",
            XPathExpr = @"(array:size(array:join(([1, 2], [], [3, 4]))), string-join(array:join(([1, 2], [], [3, 4]))?*, ''))",
            Expected = @"4|1234"
        },
        new TestCase
        {
            Id = "arr-014",
            Area = "xpath: arrays",
            Title = "array:flatten recursively flattens nested arrays to a sequence",
            SpecRef = "F&O 17.3.18",
            XPathExpr = @"string-join(array:flatten([1, [2, [3, 4]], 5]), '')",
            Expected = @"12345"
        },

        // --- Higher-order array functions ----------------------------------

        new TestCase
        {
            Id = "arr-015",
            Area = "xpath: arrays",
            Title = "array:for-each applies the function to each member",
            SpecRef = "F&O 17.3.12",
            XPathExpr = @"string-join(array:for-each([1, 2, 3], function($x) { $x * 10 })?*, ',')",
            Expected = @"10,20,30"
        },
        new TestCase
        {
            Id = "arr-016",
            Area = "xpath: arrays",
            Title = "array:filter keeps members for which the predicate is true",
            SpecRef = "F&O 17.3.13",
            XPathExpr = @"string-join(array:filter([1, 2, 3, 4, 5], function($x) { $x mod 2 = 0 })?*, '')",
            Expected = @"24"
        },
        new TestCase
        {
            Id = "arr-017",
            Area = "xpath: arrays",
            Title = "array:fold-left builds left-to-right, array:fold-right right-to-left",
            SpecRef = "F&O 17.3.14, 17.3.15",
            // fold-left: ((z-1)-2)-3 ; fold-right: 1-(2-(3-z))
            XPathExpr = @"(array:fold-left([1, 2, 3], 'z', function($a, $b) { concat($a, '-', $b) }), array:fold-right([1, 2, 3], 'z', function($a, $b) { concat($a, '-', $b) }))",
            Expected = @"z-1-2-3|1-2-3-z"
        },
        new TestCase
        {
            Id = "arr-018",
            Area = "xpath: arrays",
            Title = "array:for-each-pair result length is the shorter array's size",
            SpecRef = "F&O 17.3.16",
            XPathExpr = @"array:for-each-pair(['a', 'b', 'c'], [1, 2], function($x, $y) { concat($x, $y) })?*",
            Expected = @"a1|b2"
        },
        new TestCase
        {
            Id = "arr-019",
            Area = "xpath: arrays",
            Title = "array:sort default orders members ascending",
            SpecRef = "F&O 17.3.17",
            XPathExpr = @"string-join(array:sort([3, 1, 2])?*, '')",
            Expected = @"123"
        },
        new TestCase
        {
            Id = "arr-020",
            Area = "xpath: arrays",
            Title = "array:sort with key function sorts by computed key (negation gives descending)",
            SpecRef = "F&O 17.3.17",
            XPathExpr = @"string-join(array:sort([1, 3, 2], (), function($x) { -$x })?*, '')",
            Expected = @"321"
        },

        // --- Lookup operators and arrays as functions ----------------------

        new TestCase
        {
            Id = "arr-021",
            Area = "xpath: arrays",
            Title = "Postfix ?N lookup, dynamic function call on array, and dynamic ?($i) key",
            SpecRef = "XPath 3.1 3.11.2.2, 3.11.3.2",
            XPathExpr = @"([10, 20, 30]?2, [10, 20, 30](3), let $i := 1 return [10, 20, 30]?($i))",
            Expected = @"20|30|10"
        },
        new TestCase
        {
            Id = "arr-022",
            Area = "xpath: arrays",
            Title = "Chained ?* over nested arrays yields all inner members",
            SpecRef = "XPath 3.1 3.11.3.2",
            XPathExpr = @"string-join([['a', 'b'], ['c']]?*?*, '')",
            Expected = @"abc"
        },

        // --- Atomization and comparisons ------------------------------------

        new TestCase
        {
            Id = "arr-023",
            Area = "xpath: arrays",
            Title = "fn:sum atomizes an array by deep-flattening its members",
            SpecRef = "F&O 14.4.5; XPath 3.1 2.4.2",
            XPathExpr = @"sum([1, 2, [3, 4]])",
            Expected = @"10"
        },
        new TestCase
        {
            Id = "arr-024",
            Area = "xpath: arrays",
            Title = "Atomization of a nested array in xsl:value-of yields the flattened atomic items",
            SpecRef = "XPath 3.1 2.4.2",
            XPathExpr = @"[1, [2, 3], 4]",
            Expected = @"1|2|3|4"
        },
        new TestCase
        {
            Id = "arr-025",
            Area = "xpath: arrays",
            Title = "Arithmetic on array atomizing to two items raises XPTY0004",
            SpecRef = "XPath 3.1 3.4",
            XPathExpr = @"1 + [2, 3]",
            ExpectedErrorCode = "XPTY0004"
        },
        new TestCase
        {
            Id = "arr-026",
            Area = "xpath: arrays",
            Title = "deep-equal: array is not equal to plain sequence; identical nested arrays are equal",
            SpecRef = "F&O 14.2.3",
            XPathExpr = @"(deep-equal([1, 2], (1, 2)), deep-equal([1, [2, 3]], [1, [2, 3]]))",
            Expected = @"false|true"
        },

        // --- Empty arrays and arrays inside maps ---------------------------

        new TestCase
        {
            Id = "arr-027",
            Area = "xpath: arrays",
            Title = "Empty array has size 0 and ?* yields empty sequence; empty-sequence members still count",
            SpecRef = "F&O 17.3.1",
            XPathExpr = @"(array:size([]), count([]?*), array:size([(), ()]))",
            Expected = @"0|0|2"
        },
        new TestCase
        {
            Id = "arr-028",
            Area = "xpath: arrays",
            Title = "Array stored as map value reachable via chained lookup ?k?2",
            SpecRef = "XPath 3.1 3.11.3.2",
            XPathExpr = @"map { 'k' : [1, 2, 3] }?k?2",
            Expected = @"2"
        }
    };
}
