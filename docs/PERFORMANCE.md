# Performance: DAXon against Saxon-HE on Java

This report compares DAXon 1.5.0 with Saxon-HE 12.9, the Java engine it was ported from, on speed and on memory. Both
engines ran the same 12 stylesheets on the same input files, on one machine, on 2026-11-13. Every one of the 12 outputs
was identical, byte for byte.

## On what

| | DAXon | Saxon-HE |
|---|---|---|
| Version | 1.5.0, the `net472` build | 12.9, built from the Saxonica source release |
| Runtime | .NET Framework 4.8, 64-bit, default settings of a console program | Java 17 (OpenJDK 17.0.14, Microsoft build), 64-bit, default settings |
| Started as | a small console program that calls DAXon's public API | Saxon's own command line, `net.sf.saxon.Transform` |

Both ran on the same virtual machine: 8 virtual CPUs (Intel Xeon E5-2650 v2, 2.6 GHz), 31 GB of RAM, Windows Server
2022. Nothing else ran during the measurements. This is an old and slow CPU, so a modern desktop gives smaller
times for both engines. Compare the two columns with each other, not with your own machine.

## The data

Three generated files hold the same 20,000 sales orders, with 5 lines each, 100,000 lines in all. An order has an
id, a customer id, an order number (`SO-1`, ...) and its lines. A line has an id, an item id (5 different items), a
quantity, a price and an amount. Ids are 13-14 digit numbers, and prices and amounts have two decimals.

| File | Size | What it looks like |
|---|---|---|
| `orders.xml` | 15.8 MB | `<orders><order><id>...</id><customer_id>...</customer_id><sales_id>SO-1</sales_id><lines><line>...</line>...</lines></order>...</orders>` |
| `orders.json` | 10.6 MB | an array of orders, each with an array of lines |
| `orders.csv` | 7.0 MB | one row per order line, 100,000 rows plus a header; every third customer name is in quotes with `""` and a comma inside |

## The 12 tests

| Test | Input | What the stylesheet does |
|---|---|---|
| count | XML | counts every element in the document: `count(//*)` |
| map | XML | turns each order and line into `SalesOrder` / `Line` elements with attributes, an order total (`sum`, `format-number`) and a decimal comparison per line |
| group | XML | groups the lines by item (`xsl:for-each-group`), sorts the groups by total, writes a count, sums and a formatted total per group |
| to JSON | XML | builds a JSON array of the orders with their lines, typed numbers included, and writes it with `method="json"` |
| json-doc | JSON | reads the file with `json-doc()`, counts the orders and sums all quantities |
| JSON mapping | JSON | reads the file with `json-doc()` and writes a `SalesOrder` / `Line` element for every order and line |
| json-to-xml | JSON | converts the file with `json-to-xml()`, counts every element and sums the quantities |
| strings | XML | per order: `upper-case`, `replace` with a regex, `tokenize` / `substring` / `string-join`, `format-number`, `translate`, `normalize-space` |
| keys | XML | looks lines up by item with `xsl:key` / `key()`, filters orders by their total, takes `max` of decimals and `avg` of integers |
| copy | XML | copies the whole document unchanged (identity transformation) |
| sort | XML | sorts all 100,000 lines by amount (as a decimal), then by id, and writes the first 20 |
| CSV regex | CSV | reads the file line by line and splits each line into fields with a regular expression (`xsl:analyze-string`), writing `Row` / `F` elements |

For the XML tests the program first reads `orders.xml` into a tree and then runs the stylesheet on it. The JSON and CSV
tests start with no input document: the stylesheet itself reads the file, so reading it is part of the run.

## How it was measured

1. **Speed in a running program.** Each test ran 6 times in a row inside one process, the way a service that keeps
   the engine loaded would run it. Each run read the input and ran the stylesheet, and the result is the middle value
   (median) of the last 4 runs. The first runs, in which .NET and Java are still compiling their own code, are left
   out, and so is compiling the stylesheet. DAXon's times were taken with a `Stopwatch` in the test program. Saxon's
   came from its own report (`Transform -t -repeat:6`): the total is its "Execution time", and the part of it spent
   reading the document is its "Tree built".
2. **Speed of a whole program run.** One transformation in a fresh process, from start to exit, the way a command line
   or a scheduled job would run it. This includes starting .NET or Java.
3. **Memory.** For the run in step 2, the most memory the process took from Windows at any moment. Both processes were
   measured the same way, through a Windows job object. For Java there is a second figure: how much of its heap held
   live data after the last run in step 1, from Saxon's own report. Java takes heap from the system well ahead of need,
   so the second figure is closer to what the data actually occupies.

## Speed in a running program

Milliseconds per transformation, lower is better.

| Test | DAXon | Saxon (Java) | Faster | Reading the XML (DAXon / Saxon) |
|---|---|---|---|---|
| count | 627 | 503 | Saxon, 1.2x | 621 / 475 |
| map | 1,388 | 1,397 | even | 557 / 528 |
| group | 646 | 668 | even | 547 / 479 |
| to JSON | 1,179 | 1,281 | even | 565 / 458 |
| json-doc | 769 | 737 | even | |
| JSON mapping | 2,235 | 3,663 | DAXon, 1.6x | |
| json-to-xml | 1,720 | 1,760 | even | |
| strings | 1,038 | 1,119 | even | 600 / 508 |
| keys | 891 | 735 | Saxon, 1.2x | 608 / 485 |
| copy | 928 | 1,441 | DAXon, 1.6x | 496 / 536 |
| sort | 903 | 677 | Saxon, 1.3x | 611 / 469 |
| CSV regex | 4,634 | 3,839 | Saxon, 1.2x | |

"Even" means a difference under 15%. That is about how much the same test moved from one series of runs to the next.

In the XML tests, reading the 15.8 MB document takes most of the time, and DAXon reads it about 15% slower than Java.
Without that step, DAXon runs the stylesheet itself faster in most of these tests: group about 1.9x, copy 2.1x,
to JSON 1.3x, strings 1.4x. It is slower in sort (1.4x) and keys (1.1x).

## Speed of a whole program run

Milliseconds from process start to exit for one transformation, lower is better.

| Test | DAXon | Saxon (Java) | Faster |
|---|---|---|---|
| count | 2,032 | 3,331 | DAXon, 1.6x |
| map | 3,302 | 6,651 | DAXon, 2.0x |
| group | 2,724 | 3,796 | DAXon, 1.4x |
| to JSON | 3,184 | 5,347 | DAXon, 1.7x |
| json-doc | 2,327 | 3,565 | DAXon, 1.5x |
| JSON mapping | 3,991 | 6,712 | DAXon, 1.7x |
| json-to-xml | 3,972 | 4,912 | DAXon, 1.2x |
| strings | 2,942 | 6,238 | DAXon, 2.1x |
| keys | 2,803 | 4,108 | DAXon, 1.5x |
| copy | 2,223 | 4,253 | DAXon, 1.9x |
| sort | 2,592 | 4,385 | DAXon, 1.7x |
| CSV regex | 7,279 | 7,959 | even |

Java needs time to start and to compile its own code before it reaches full speed. In a single run DAXon is ahead in
every test except CSV regex, where the two are even.

## Memory

The most memory the process took at any moment during one transformation, in MB. Lower is better.

| Test | DAXon | Saxon (Java) | DAXon uses less, by | Java heap holding data after the run |
|---|---|---|---|---|
| count | 69 | 654 | 9x | 73 |
| map | 74 | 659 | 9x | 192 |
| group | 75 | 658 | 9x | 79 |
| to JSON | 100 | 1,239 | 12x | 220 |
| json-doc | 103 | 655 | 6x | 96 |
| JSON mapping | 104 | 1,211 | 12x | 302 |
| json-to-xml | 177 | 662 | 4x | 447 |
| strings | 74 | 678 | 9x | 124 |
| keys | 84 | 663 | 8x | 89 |
| copy | 72 | 660 | 9x | 56 |
| sort | 95 | 661 | 7x | 78 |
| CSV regex | 28 | 718 | 26x | 100 |

DAXon's figure is the whole process: .NET itself, the input, its tree and the result. With default settings Java takes
at least about 650 MB, whatever the test. Even against the last column, which counts only the heap holding data,
DAXon's whole process is about the same size or smaller in most tests.

## Where DAXon is slower, and why

- **Reading XML, about 15%.** DAXon reads XML with the .NET `XmlReader`, and Saxon reads it with Java's built-in
  parser. Most of the difference is in that platform parser, not in the tree DAXon builds from what it reads.
- **Sorting, 1.3x.** Sorting creates a few small objects for every item it sorts. With the default settings of a .NET
  console program these objects cost more to clean up than they do in Java, which avoids many of them. Web hosts
  on IIS / ASP.NET usually run a different .NET memory cleaner, the server garbage collector. With it switched on,
  the sort and keys tests came out even with Java.
- **Regular expressions, 1.2x.** DAXon's regular-expression engine is a port of Saxon's own. It creates small objects
  on every match attempt.

## Limits

These figures come from one machine, one kind of data and one day. Treat them as a guide, not a promise: a stylesheet
that spends its time on other things can come out differently. The test program and the scripts that ran these
measurements are not part of this repository.
