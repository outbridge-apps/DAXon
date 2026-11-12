using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OutSmart.DAXon.Api;

// Memo-correctness attack for the singleton hosting pattern: MANY near-identical
// stylesheets through ONE shared Processor. The construction memos (the CCO namespace
// pipeline, NamespaceMap.PutAll LRU-2, TinyTree.AddNamespaces) key on object identities;
// the twins share interned NodeName objects but differ in namespace maps / inherit
// properties / literal values, so any key omission or cross-contamination produces a
// WRONG serialized answer, not a crash. Reference outputs come from a FRESH Processor
// per sheet; the singleton then runs all sheets interleaved single-threaded, staggered
// on 16 threads, and in 32-thread same-instant bursts, full-string compared.
// Set TWINS_DUMP=<dir> to write the reference outputs and stylesheets for inspection.
internal static class Program
{
    public static int Main()
    {
        string xslHead = "<xsl:stylesheet version=\"3.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"><xsl:output method=\"xml\" omit-xml-declaration=\"yes\"/><xsl:template match=\"/\">";
        string xslTail = "</xsl:template></xsl:stylesheet>";
        var sheets = new (string name, string body)[]
        {
            // t1/t2: same local names (shared interned NodeName), different default ns
            ("t1_nsA", "<root xmlns=\"urn:a\"><xsl:for-each select=\"//row\"><e key=\"v1\"><f/></e></xsl:for-each></root>"),
            ("t2_nsB", "<root xmlns=\"urn:b\"><xsl:for-each select=\"//row\"><e key=\"v1\"><f/></e></xsl:for-each></root>"),
            // t3: same URI as t1 but prefixed form (different serialization)
            ("t3_prefixed", "<p:root xmlns:p=\"urn:a\"><xsl:for-each select=\"//row\"><p:e key=\"v1\"><p:f/></p:e></xsl:for-each></p:root>"),
            // t4: attribute prefix CONFLICTS with element prefix (p->urn:c vs p->urn:a):
            // forces GetSubstitutePrefix + the nsClean=false full path every element
            ("t4_conflict", "<p:root xmlns:p=\"urn:a\"><xsl:for-each select=\"//row\"><p:e><xsl:attribute name=\"p:x\" xmlns:p=\"urn:c\" select=\"'z'\"/></p:e></xsl:for-each></p:root>"),
            // t5: inherit-namespaces=no -> DISINHERIT property; memo key must see props
            ("t5_noinherit", "<root xmlns=\"urn:a\"><xsl:for-each select=\"//row\"><xsl:element name=\"e\" inherit-namespaces=\"no\"><f xmlns=\"\"/></xsl:element></xsl:for-each></root>"),
            // t6: child re-binds prefix q to a DIFFERENT uri -> real MergePutAll, not the
            // ContainsAll shortcut; child must serialize q=urn:q2
            ("t6_rebind", "<root xmlns:q=\"urn:q1\"><xsl:for-each select=\"//row\"><e xmlns:q=\"urn:q2\"><xsl:attribute name=\"q:k\" select=\"'y'\" xmlns:q=\"urn:q2\"/></e></xsl:for-each></root>"),
            // t7: same shape as t1, different hoisted literal attribute value
            ("t7_v2", "<root xmlns=\"urn:a\"><xsl:for-each select=\"//row\"><e key=\"v2\"><f/></e></xsl:for-each></root>"),
            // t8/t9: empty-literal content (static no-op push) vs text content
            ("t8_empty", "<root><xsl:for-each select=\"//row\"><e/></xsl:for-each></root>"),
            ("t9_text", "<root><xsl:for-each select=\"//row\"><e>txt</e></xsl:for-each></root>"),
            // t10: computed attribute from source (fused AVT path) next to constants
            ("t10_avt", "<root><xsl:for-each select=\"//row\"><e a=\"{@v}\" key=\"v1\"/></xsl:for-each></root>"),
            // t11/t12: deep copy of source rows under different wrapper namespaces —
            // CCO full-form StartElement + PutAll on the SHARED source-tree map object
            ("t11_copyW", "<w xmlns=\"urn:w\"><xsl:for-each select=\"//row\"><xsl:copy-of select=\".\"/></xsl:for-each></w>"),
            ("t12_copyV", "<v xmlns=\"urn:v\"><xsl:for-each select=\"//row\"><xsl:copy-of select=\".\"/></xsl:for-each></v>"),
        };

        // small synthetic input: enough rows to make every per-instruction memo hot.
        // Every row declares its own namespace + prefixed attribute so the source tree's
        // (shared, deduped) namespace map is NON-empty: the copy-of twins then merge that
        // shared map object under two different wrappers instead of hitting the empty-map
        // shortcut
        var xb = new System.Text.StringBuilder("<doc>");
        for (int i = 0; i < 400; i++)
        {
            xb.Append("<row xmlns:s=\"urn:s\" s:a=\"A").Append(i).Append("\" v=\"").Append(i).Append("\"/>");
        }

        xb.Append("</doc>");
        string inputXml = xb.ToString();

        // references: fresh Processor per sheet (no shared state at all)
        var reference = new string[sheets.Length];
        for (int i = 0; i < sheets.Length; i++)
        {
            var fresh = new Processor();
            XdmNode d = fresh.NewDocumentBuilder().Build(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(inputXml)), "urn:twin-in");
            var refExe = fresh.NewXsltCompiler().Compile(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xslHead + sheets[i].body + xslTail)), "urn:twin-" + i);
            var refTr = refExe.Load30();
            var refW = new StringWriter();
            refTr.SetGlobalContextItem(d, true);
            refTr.ApplyTemplates(d, fresh.NewSerializer(refW));
            reference[i] = refW.ToString();
        }

        string dumpDir = Environment.GetEnvironmentVariable("TWINS_DUMP");
        if (dumpDir != null)
        {
            Directory.CreateDirectory(dumpDir);
            for (int i = 0; i < sheets.Length; i++)
            {
                File.WriteAllText(Path.Combine(dumpDir, sheets[i].name + ".out"), reference[i]);
                File.WriteAllText(Path.Combine(dumpDir, sheets[i].name + ".xslt"), xslHead + sheets[i].body + xslTail);
            }

            File.WriteAllText(Path.Combine(dumpDir, "input.xml"), inputXml);
        }

        // the singleton under attack: one Processor, one shared input, all sheets compiled on it
        var proc = new Processor();
        XdmNode input = proc.NewDocumentBuilder().Build(
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(inputXml)), "urn:twin-in");
        var exes = new XsltExecutable[sheets.Length + 1];
        for (int i = 0; i < sheets.Length; i++)
        {
            exes[i] = proc.NewXsltCompiler().Compile(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xslHead + sheets[i].body + xslTail)), "urn:twin-" + i);
        }

        // extra: the SAME sheet text compiled twice -> distinct instruction objects, must
        // byte-match t1's reference
        exes[sheets.Length] = proc.NewXsltCompiler().Compile(
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xslHead + sheets[0].body + xslTail)), "urn:twin-dup");

        string RunOn(XsltExecutable exe)
        {
            var tr = exe.Load30();
            var w = new StringWriter();
            tr.SetGlobalContextItem(input, true);
            tr.ApplyTemplates(input, proc.NewSerializer(w));
            return w.ToString();
        }

        int errors = 0;
        void Check(string got, string want, string label)
        {
            if (got != want)
            {
                errors++;
                Console.Error.WriteLine("TWINS-FAIL " + label + ":\n  want: " + Trunc(want) + "\n  got:  " + Trunc(got));
            }
        }

        // phase 1: single thread, sheets interleaved 25 rounds (maximum memo alternation)
        for (int round = 0; round < 25; round++)
        {
            for (int i = 0; i < sheets.Length; i++)
            {
                Check(RunOn(exes[i]), reference[i], sheets[i].name + " r" + round);
            }

            Check(RunOn(exes[sheets.Length]), reference[0], "dup-of-t1 r" + round);
        }

        // phase 2: 16 threads, each cycling through all sheets in a different phase
        var mtFailures = new ConcurrentBag<string>();
        var threads = new List<Thread>();
        for (int t = 0; t < 16; t++)
        {
            int tid = t;
            threads.Add(new Thread(() =>
            {
                try
                {
                    for (int k = 0; k < 40; k++)
                    {
                        int i = (tid + k) % sheets.Length;
                        string got = RunOn(exes[i]);
                        if (got != reference[i])
                        {
                            mtFailures.Add(sheets[i].name + " thread" + tid + " iter" + k);
                        }
                    }
                }
                catch (Exception e)
                {
                    mtFailures.Add("thread" + tid + " EX " + e.GetType().Name + ": " + e.Message);
                }
            }));
        }

        foreach (var th in threads) th.Start();
        foreach (var th in threads) th.Join();
        foreach (string f in mtFailures)
        {
            errors++;
            Console.Error.WriteLine("TWINS-MT-FAIL " + f);
        }

        // phase 3: barrier burst — ALL threads start the SAME executable at the same
        // instant (then pivot to a different sheet each), maximum simultaneous write
        // contention on the PutAll memos of the shared constant maps
        const int BURST = 32;
        for (int i = 0; i <= sheets.Length; i++)
        {
            int idx = i;
            string want = reference[i == sheets.Length ? 0 : i];
            string label = i == sheets.Length ? "dup-of-t1" : sheets[i].name;
            var barrier = new Barrier(BURST);
            var burstFails = new ConcurrentBag<string>();
            var burstThreads = new List<Thread>();
            for (int t = 0; t < BURST; t++)
            {
                int tid = t;
                burstThreads.Add(new Thread(() =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        if (RunOn(exes[idx]) != want)
                        {
                            burstFails.Add(label + " burst-thread" + tid);
                        }

                        int j = (idx + 1 + tid) % sheets.Length;
                        if (RunOn(exes[j]) != reference[j])
                        {
                            burstFails.Add(sheets[j].name + " burst-pivot-thread" + tid);
                        }
                    }
                    catch (Exception e)
                    {
                        burstFails.Add(label + " burst-thread" + tid + " EX " + e.GetType().Name + ": " + e.Message);
                    }
                }));
            }

            foreach (var th in burstThreads) th.Start();
            foreach (var th in burstThreads) th.Join();
            foreach (string f in burstFails)
            {
                errors++;
                Console.Error.WriteLine("TWINS-BURST-FAIL " + f);
            }
        }

        if (errors > 0)
        {
            Console.WriteLine("TWINS: " + errors + " FAILURES");
            return 1;
        }

        Console.WriteLine("TWINS-OK: " + sheets.Length + " near-identical sheets + dup on ONE Processor: 25 interleaved rounds, 16 threads x 40 staggered, " + BURST + "-thread same-instant bursts per sheet + pivot — every output byte-equal to fresh-Processor reference");
        return 0;
    }

    private static string Trunc(string s)
    {
        s = s.Length > 300 ? s.Substring(0, 300) + "..." : s;
        return s.Replace("\n", "\\n");
    }
}
