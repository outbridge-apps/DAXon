// B10_Maps.cs
// Spec-derived test cases for XPath 3.1 maps: constructor syntax (XPath 3.1 3.11.1),
// the map functions of F&O 3.1 section 17.1, and the lookup operator (XPath 3.1 3.11.3).
// Expected values reasoned from the W3C specs; map key/entry ordering is
// implementation-dependent, so all multi-entry probes go through sort()/sum()/size().

namespace OutSmart.DAXon.SpecTests.Cases;

public static class B10_Maps
{
    public static TestCase[] Cases() => new[]
    {
        new TestCase
        {
            Id = "map-001",
            Area = "xpath: maps",
            Title = "map constructor with three entries; map:size",
            SpecRef = "XPath 3.1 3.11.1 / F&O 3.1 17.1.3",
            XPathExpr = @"map:size(map{'a':1,'b':2,'c':3})",
            Expected = "3"
        },
        new TestCase
        {
            Id = "map-002",
            Area = "xpath: maps",
            Title = "mixed key types (string, integer, date) each retrievable",
            SpecRef = "XPath 3.1 3.11.1 / F&O 3.1 17.1.6",
            XPathExpr = @"let $m := map{'a':1, 2:'two', xs:date('2024-01-15'):'D'} return (map:size($m), map:get($m, 2), map:get($m, xs:date('2024-01-15')))",
            Expected = "3|two|D"
        },
        new TestCase
        {
            Id = "map-003",
            Area = "xpath: maps",
            Title = "map:keys wrapped in sort() for string and integer keys",
            SpecRef = "F&O 3.1 17.1.4",
            XPathExpr = @"(string-join(sort(map:keys(map{'b':2,'a':1,'c':3})) ! string(.), ','), string-join(sort(map:keys(map{2:'b',1:'a',10:'c'})) ! string(.), ','))",
            Expected = "a,b,c|1,2,10"
        },
        new TestCase
        {
            Id = "map-004",
            Area = "xpath: maps",
            Title = "map:contains true for present key, false for absent key",
            SpecRef = "F&O 3.1 17.1.5",
            XPathExpr = @"(map:contains(map{'a':1}, 'a'), map:contains(map{'a':1}, 'b'))",
            Expected = "true|false"
        },
        new TestCase
        {
            Id = "map-005",
            Area = "xpath: maps",
            Title = "map:get returns value for present key, empty sequence for missing key",
            SpecRef = "F&O 3.1 17.1.6",
            XPathExpr = @"let $m := map{'a':1,'b':2} return (map:get($m,'a'), count(map:get($m,'zzz')))",
            Expected = "1|0"
        },
        new TestCase
        {
            Id = "map-006",
            Area = "xpath: maps",
            Title = "map:put replaces value of existing key",
            SpecRef = "F&O 3.1 17.1.8",
            XPathExpr = @"map:get(map:put(map{'a':1,'b':2}, 'a', 99), 'a')",
            Expected = "99"
        },
        new TestCase
        {
            Id = "map-007",
            Area = "xpath: maps",
            Title = "map:put adds new entry; original map is unchanged (immutability)",
            SpecRef = "F&O 3.1 17.1.8",
            XPathExpr = @"let $m := map{'a':1}, $m2 := map:put($m, 'b', 2) return (map:size($m), map:size($m2), $m2?b)",
            Expected = "1|2|2"
        },
        new TestCase
        {
            Id = "map-008",
            Area = "xpath: maps",
            Title = "map:remove drops entry; original map is unchanged",
            SpecRef = "F&O 3.1 17.1.10",
            XPathExpr = @"let $m := map{'a':1,'b':2}, $m2 := map:remove($m, 'a') return (map:size($m2), map:contains($m2, 'a'), map:size($m))",
            Expected = "1|false|2"
        },
        new TestCase
        {
            Id = "map-009",
            Area = "xpath: maps",
            Title = "map:remove: missing key is a no-op; sequence of keys removes each",
            SpecRef = "F&O 3.1 17.1.10",
            XPathExpr = @"let $m := map{'a':1,'b':2,'c':3} return (map:size(map:remove($m, 'zzz')), map:size(map:remove($m, ('a','c'))))",
            Expected = "3|1"
        },
        new TestCase
        {
            Id = "map-010",
            Area = "xpath: maps",
            Title = "map:entry builds a singleton map",
            SpecRef = "F&O 3.1 17.1.9",
            XPathExpr = @"let $e := map:entry('k', 42) return (map:size($e), $e?k)",
            Expected = "1|42"
        },
        new TestCase
        {
            Id = "map-011",
            Area = "xpath: maps",
            Title = "map:merge default duplicates policy is use-first",
            SpecRef = "F&O 3.1 17.1.2",
            XPathExpr = @"let $m := map:merge((map{'a':1,'b':2}, map{'a':9,'c':3})) return (map:size($m), $m?a)",
            Expected = "3|1"
        },
        new TestCase
        {
            Id = "map-012",
            Area = "xpath: maps",
            Title = "map:merge with duplicates=use-last takes value from last map",
            SpecRef = "F&O 3.1 17.1.2",
            XPathExpr = @"map:get(map:merge((map{'a':1}, map{'a':2}), map{'duplicates':'use-last'}), 'a')",
            Expected = "2"
        },
        new TestCase
        {
            Id = "map-013",
            Area = "xpath: maps",
            Title = "map:merge with duplicates=combine concatenates values in input-map order",
            SpecRef = "F&O 3.1 17.1.2",
            XPathExpr = @"map:merge((map{'a':1}, map{'a':2}), map{'duplicates':'combine'})?a",
            Expected = "1|2"
        },
        new TestCase
        {
            Id = "map-014",
            Area = "xpath: maps",
            Title = "map:merge with duplicates=reject raises FOJS0003 on duplicate key",
            SpecRef = "F&O 3.1 17.1.2",
            XPathExpr = @"map:size(map:merge((map{'a':1}, map{'a':2}), map{'duplicates':'reject'}))",
            ExpectedErrorCode = "FOJS0003"
        },
        new TestCase
        {
            Id = "map-015",
            Area = "xpath: maps",
            Title = "map:merge of empty sequence yields empty map; disjoint maps union",
            SpecRef = "F&O 3.1 17.1.2",
            XPathExpr = @"(map:size(map:merge(())), map:size(map:merge((map{'a':1}, map{'b':2}))))",
            Expected = "0|2"
        },
        new TestCase
        {
            Id = "map-016",
            Area = "xpath: maps",
            Title = "map:for-each projecting values; sum is order-independent",
            SpecRef = "F&O 3.1 17.1.11",
            XPathExpr = @"sum(map:for-each(map{'a':1,'b':2,'c':3}, function($k, $v) { $v }))",
            Expected = "6"
        },
        new TestCase
        {
            Id = "map-017",
            Area = "xpath: maps",
            Title = "map:find searches nested maps and returns array of matched values",
            SpecRef = "F&O 3.1 17.1.7",
            XPathExpr = @"let $f := map:find((map{'a':1}, map{'b': map{'a': 3}}), 'a') return (array:size($f), string-join(sort($f?*) ! string(.), ','))",
            Expected = "2|1,3"
        },
        new TestCase
        {
            Id = "map-018",
            Area = "xpath: maps",
            Title = "postfix lookup ?key on map returns entry value",
            SpecRef = "XPath 3.1 3.11.3.2",
            XPathExpr = @"map{'a':1,'b':2}?a",
            Expected = "1"
        },
        new TestCase
        {
            Id = "map-019",
            Area = "xpath: maps",
            Title = "wildcard lookup ?* returns all values (sorted for determinism)",
            SpecRef = "XPath 3.1 3.11.3.2",
            XPathExpr = @"string-join(sort(map{'b':2,'a':1,'c':3}?*) ! string(.), ',')",
            Expected = "1,2,3"
        },
        new TestCase
        {
            Id = "map-020",
            Area = "xpath: maps",
            Title = "dynamic lookup ?($k) with parenthesized key expression",
            SpecRef = "XPath 3.1 3.11.3.2",
            XPathExpr = @"let $m := map{'a':1,'b':2}, $k := 'b' return $m?($k)",
            Expected = "2"
        },
        new TestCase
        {
            Id = "map-021",
            Area = "xpath: maps",
            Title = "map applied as function item: value for present key, empty for missing",
            SpecRef = "XPath 3.1 3.11.1.2",
            XPathExpr = @"(map{'a':1,'b':2}('b'), count(map{'a':1,'b':2}('zzz')))",
            Expected = "2|0"
        },
        new TestCase
        {
            Id = "map-022",
            Area = "xpath: maps",
            Title = "unary lookup ?v over a sequence of maps via simple map operator",
            SpecRef = "XPath 3.1 3.11.3.1",
            XPathExpr = @"sum((map{'v':1}, map{'v':2}, map{'v':3}) ! ?v)",
            Expected = "6"
        },
        new TestCase
        {
            Id = "map-023",
            Area = "xpath: maps",
            Title = "deep-equal on maps is entry-order-insensitive; differing values compare false",
            SpecRef = "F&O 3.1 14.2.3",
            XPathExpr = @"(deep-equal(map{'a':1,'b':2}, map{'b':2,'a':1}), deep-equal(map{'a':1}, map{'a':2}))",
            Expected = "true|false"
        },
        new TestCase
        {
            Id = "map-024",
            Area = "xpath: maps",
            Title = "entry with empty-sequence value: contains is true, lookup yields empty",
            SpecRef = "XPath 3.1 3.11.1 / F&O 3.1 17.1.5",
            XPathExpr = @"let $m := map{'a': (), 'b': 1} return (map:size($m), map:contains($m, 'a'), count($m?a), count(map:keys($m)))",
            Expected = "2|true|0|2"
        },
        new TestCase
        {
            Id = "map-025",
            Area = "xpath: maps",
            Title = "chained lookups ?a?b?c on nested maps",
            SpecRef = "XPath 3.1 3.11.3.2",
            XPathExpr = @"map{'a': map{'b': map{'c': 7}}}?a?b?c",
            Expected = "7"
        },
        new TestCase
        {
            Id = "map-026",
            Area = "xpath: maps",
            Title = "op:same-key: numeric keys match across types; string '1' does not match integer 1",
            SpecRef = "F&O 3.1 17.1.1",
            XPathExpr = @"(map:get(map{1:'i'}, xs:double('1')), map{1.0:'d'}(1), map:contains(map{'1':'s'}, 1))",
            Expected = "i|d|false"
        },
        new TestCase
        {
            Id = "map-027",
            Area = "xpath: maps",
            Title = "map constructor with duplicate keys raises XQDY0137",
            SpecRef = "XPath 3.1 3.11.1",
            XPathExpr = @"map:size(map{'a':1, 'a':2})",
            ExpectedErrorCode = "XQDY0137"
        },
        new TestCase
        {
            Id = "map-028",
            Area = "xpath: maps",
            Title = "map:merge with invalid duplicates option value raises FOJS0005",
            SpecRef = "F&O 3.1 17.1.2",
            XPathExpr = @"map:size(map:merge((map{'a':1}, map{'a':2}), map{'duplicates':'always'}))",
            ExpectedErrorCode = "FOJS0005"
        }
    };
}
