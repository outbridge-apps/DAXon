// B12_HOF: XPath 3.1 higher-order functions.
// Covers: inline function expressions (untyped/typed, coercion errors), named
// function references (incl. variadic concat#N), closures over let/for/param
// bindings, fn:for-each, fn:filter, fn:fold-left, fn:fold-right,
// fn:for-each-pair, fn:apply, fn:function-arity, fn:function-name, fn:sort
// with key function, functions stored in maps/arrays, function composition.
// All expected values derived by hand from the W3C XPath 3.1 and
// XQuery/XPath Functions & Operators 3.1 Recommendations (2017-03-21).
namespace OutSmart.DAXon.SpecTests.Cases;

public static class B12_HOF
{
    public static TestCase[] Cases() => new[]
    {
        new TestCase
        {
            Id = "hof-001",
            Area = "xpath: higher-order fns",
            Title = "inline function: basic application",
            SpecRef = "XPath 3.1 sec 3.1.7",
            // 21 * 2 = 42
            XPathExpr = @"let $f := function($x) { $x * 2 } return $f(21)",
            Expected = "42"
        },
        new TestCase
        {
            Id = "hof-002",
            Area = "xpath: higher-order fns",
            Title = "inline function: typed parameter and return type",
            SpecRef = "XPath 3.1 sec 3.1.7",
            // 7 * 7 = 49
            XPathExpr = @"let $f := function($x as xs:integer) as xs:integer { $x * $x } return $f(7)",
            Expected = "49"
        },
        new TestCase
        {
            Id = "hof-003",
            Area = "xpath: higher-order fns",
            Title = "coercion: xs:string argument for xs:integer parameter raises XPTY0004",
            SpecRef = "XPath 3.1 sec 3.1.5.2",
            // Function conversion rules: a typed xs:string is neither cast (only
            // untypedAtomic is) nor promoted to xs:integer -> type error.
            XPathExpr = @"let $f := function($x as xs:integer) as xs:integer { $x + 1 } return $f('abc')",
            ExpectedErrorCode = "XPTY0004"
        },
        new TestCase
        {
            Id = "hof-004",
            Area = "xpath: higher-order fns",
            Title = "named function reference fn:upper-case#1",
            SpecRef = "XPath 3.1 sec 3.1.6",
            XPathExpr = @"let $f := fn:upper-case#1 return $f('hello')",
            Expected = "HELLO"
        },
        new TestCase
        {
            Id = "hof-005",
            Area = "xpath: higher-order fns",
            Title = "named function reference concat#3 (variadic fn:concat, fixed arity)",
            SpecRef = "XPath 3.1 sec 3.1.6",
            // Statically known signatures include fn:concat#N for every N >= 2.
            XPathExpr = @"let $f := fn:concat#3 return $f('a', 'b', 'c')",
            Expected = "abc"
        },
        new TestCase
        {
            Id = "hof-006",
            Area = "xpath: higher-order fns",
            Title = "closure captures enclosing let variable",
            SpecRef = "XPath 3.1 sec 3.1.7",
            // Nonlocal variable binding: $n = 100 captured; 5 + 100 = 105
            XPathExpr = @"let $n := 100, $f := function($x) { $x + $n } return $f(5)",
            Expected = "105"
        },
        new TestCase
        {
            Id = "hof-007",
            Area = "xpath: higher-order fns",
            Title = "for-bound closures capture distinct per-iteration bindings",
            SpecRef = "XPath 3.1 sec 3.1.7",
            // Each function item closes over its own binding of $i: 10, 20, 30
            XPathExpr = @"let $fns := (for $i in 1 to 3 return function() { $i * 10 }) return for $g in $fns return $g()",
            Expected = "10|20|30"
        },
        new TestCase
        {
            Id = "hof-008",
            Area = "xpath: higher-order fns",
            Title = "for-each applies function to each item in order",
            SpecRef = "F&O 3.1 sec 16.2.1",
            XPathExpr = @"for-each(1 to 4, function($x) { $x * $x })",
            Expected = "1|4|9|16"
        },
        new TestCase
        {
            Id = "hof-009",
            Area = "xpath: higher-order fns",
            Title = "for-each concatenates (flattens) result sequences (spec example)",
            SpecRef = "F&O 3.1 sec 16.2.1",
            // Codepoints: j=106 o=111 h=104 n=110 / j=106 a=97 n=110 e=101
            XPathExpr = @"for-each(('john', 'jane'), fn:string-to-codepoints#1)",
            Expected = "106|111|104|110|106|97|110|101"
        },
        new TestCase
        {
            Id = "hof-010",
            Area = "xpath: higher-order fns",
            Title = "filter keeps items where predicate is true",
            SpecRef = "F&O 3.1 sec 16.2.2",
            XPathExpr = @"filter(1 to 10, function($n) { $n mod 2 eq 0 })",
            Expected = "2|4|6|8|10"
        },
        new TestCase
        {
            Id = "hof-011",
            Area = "xpath: higher-order fns",
            Title = "filter predicate returning non-boolean raises XPTY0004",
            SpecRef = "F&O 3.1 sec 16.2.2",
            // filter requires function(item()) as xs:boolean; an xs:integer result
            // cannot be coerced to xs:boolean -> type error on invocation.
            XPathExpr = @"filter(1 to 3, function($x) { $x + 1 })",
            ExpectedErrorCode = "XPTY0004"
        },
        new TestCase
        {
            Id = "hof-012",
            Area = "xpath: higher-order fns",
            Title = "fold-left is left-associative (string-building proof)",
            SpecRef = "F&O 3.1 sec 16.2.3",
            // f(f(f('0',1),2),3) = (((0+1)+2)+3)
            XPathExpr = @"fold-left((1, 2, 3), '0', function($a, $b) { concat('(', $a, '+', $b, ')') })",
            Expected = "(((0+1)+2)+3)"
        },
        new TestCase
        {
            Id = "hof-013",
            Area = "xpath: higher-order fns",
            Title = "fold-right is right-associative (string-building proof)",
            SpecRef = "F&O 3.1 sec 16.2.4",
            // f(1, f(2, f(3, '0'))) = (1+(2+(3+0)))
            XPathExpr = @"fold-right((1, 2, 3), '0', function($a, $b) { concat('(', $a, '+', $b, ')') })",
            Expected = "(1+(2+(3+0)))"
        },
        new TestCase
        {
            Id = "hof-014",
            Area = "xpath: higher-order fns",
            Title = "fold-left over empty sequence returns the zero value",
            SpecRef = "F&O 3.1 sec 16.2.3",
            XPathExpr = @"fold-left((), 'seed', function($a, $b) { concat($a, $b) })",
            Expected = "seed"
        },
        new TestCase
        {
            Id = "hof-015",
            Area = "xpath: higher-order fns",
            Title = "fold-left with sequence-valued accumulator reverses input (spec example)",
            SpecRef = "F&O 3.1 sec 16.2.3",
            // Accumulator is item()*: ((((),1) -> (2,1) -> ... -> (5,4,3,2,1)
            XPathExpr = @"fold-left(1 to 5, (), function($a, $b) { ($b, $a) })",
            Expected = "5|4|3|2|1"
        },
        new TestCase
        {
            Id = "hof-016",
            Area = "xpath: higher-order fns",
            Title = "for-each-pair ignores excess items of the longer sequence",
            SpecRef = "F&O 3.1 sec 16.2.5",
            // min(3,2) = 2 pairs: a1, b2
            XPathExpr = @"for-each-pair(('a', 'b', 'c'), (1, 2), function($s, $n) { concat($s, $n) })",
            Expected = "a1|b2"
        },
        new TestCase
        {
            Id = "hof-017",
            Area = "xpath: higher-order fns",
            Title = "apply calls function with array members as arguments",
            SpecRef = "F&O 3.1 sec 16.2.7",
            XPathExpr = @"apply(fn:concat#3, ['x', 'y', 'z'])",
            Expected = "xyz"
        },
        new TestCase
        {
            Id = "hof-018",
            Area = "xpath: higher-order fns",
            Title = "apply with array size differing from function arity raises FOAP0001",
            SpecRef = "F&O 3.1 sec 16.2.7",
            XPathExpr = @"apply(fn:upper-case#1, ['a', 'b'])",
            ExpectedErrorCode = "FOAP0001"
        },
        new TestCase
        {
            Id = "hof-019",
            Area = "xpath: higher-order fns",
            Title = "function-arity on named reference and inline function",
            SpecRef = "F&O 3.1 sec 16.1.3",
            XPathExpr = @"(function-arity(fn:substring#2), function-arity(function($a, $b, $c) { () }))",
            Expected = "2|3"
        },
        new TestCase
        {
            Id = "hof-020",
            Area = "xpath: higher-order fns",
            Title = "function-name of a named reference: local part",
            SpecRef = "F&O 3.1 sec 16.1.2",
            // Prefix of the returned QName is implementation-dependent, so probe
            // only the local part.
            XPathExpr = @"local-name-from-QName(function-name(fn:substring#2))",
            Expected = "substring"
        },
        new TestCase
        {
            Id = "hof-021",
            Area = "xpath: higher-order fns",
            Title = "function-name of an anonymous inline function is the empty sequence",
            SpecRef = "F&O 3.1 sec 16.1.2",
            XPathExpr = @"exists(function-name(function($x) { $x }))",
            Expected = "false"
        },
        new TestCase
        {
            Id = "hof-022",
            Area = "xpath: higher-order fns",
            Title = "sort with () collation and key function is stable (spec example)",
            SpecRef = "F&O 3.1 sec 16.2.6",
            // Keys by abs: 1,2,5,10,10,10,8 -> stable ascending: 1,-2,5,8,10,-10,10
            XPathExpr = @"sort((1, -2, 5, 10, -10, 10, 8), (), fn:abs#1)",
            Expected = "1|-2|5|8|10|-10|10"
        },
        new TestCase
        {
            Id = "hof-023",
            Area = "xpath: higher-order fns",
            Title = "function stored as map entry, invoked after lookup",
            SpecRef = "XPath 3.1 sec 3.11.1",
            // Map is a function of its keys: $m('double') yields the function; 2*7=14
            XPathExpr = @"let $m := map { 'double' : function($x) { 2 * $x }, 'inc' : function($x) { $x + 1 } } return $m('double')(7)",
            Expected = "14"
        },
        new TestCase
        {
            Id = "hof-024",
            Area = "xpath: higher-order fns",
            Title = "function stored as array member, invoked after lookup",
            SpecRef = "XPath 3.1 sec 3.11.2",
            // Array is a function of position: $a(2) is the second member; 9+100=109
            XPathExpr = @"let $a := [ function($x) { $x * $x }, function($x) { $x + 100 } ] return $a(2)(9)",
            Expected = "109"
        },
        new TestCase
        {
            Id = "hof-025",
            Area = "xpath: higher-order fns",
            Title = "function composition via a returned closure",
            SpecRef = "XPath 3.1 sec 3.1.7",
            // h = g(f(x)); (4+1)*10 = 50
            XPathExpr = @"let $compose := function($f, $g) { function($x) { $g($f($x)) } }, $h := $compose(function($x) { $x + 1 }, function($x) { $x * 10 }) return $h(4)",
            Expected = "50"
        },
        new TestCase
        {
            Id = "hof-026",
            Area = "xpath: higher-order fns",
            Title = "context item is absent inside an inline function body: XPDY0002",
            SpecRef = "XPath 3.1 sec 3.1.7 and 3.1.4",
            // The focus is absent when a function body is evaluated; evaluating
            // '.' with an absent context item is err:XPDY0002.
            XPathExpr = @"for-each(1 to 2, function($x) { . })",
            ExpectedErrorCode = "XPDY0002"
        }
    };
}
