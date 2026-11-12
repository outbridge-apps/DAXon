using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OutSmart.DAXon.SpecTests
{
    /// <summary>
    /// Discovers every case provider (static class in OutSmart.DAXon.SpecTests.Cases exposing
    /// `public static TestCase[] Cases()`), runs each case with a time budget, prints a per-area
    /// summary and writes machine-readable results.tsv.
    ///
    /// Exit code: 0 always when the run completes (failures are EXPECTED — this suite measures
    /// spec coverage of the port; the report is the deliverable). 99 = harness-level fault.
    /// </summary>
    internal static class Runner
    {
        private const int PerTestTimeoutMs = 15000;

        private static int Main(string[] args)
        {
            string filter = null;
            string outPath = "results.tsv";
            bool listOnly = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--filter" && i + 1 < args.Length) filter = args[++i];
                else if (args[i] == "--out" && i + 1 < args.Length) outPath = args[++i];
                else if (args[i] == "--list") listOnly = true;
            }

            var cases = Discover();
            var dup = cases.GroupBy(c => c.Id).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) { Console.Error.WriteLine("FATAL: duplicate case id " + dup.Key); return 99; }
            if (filter != null) cases = cases.Where(c => c.Id.Contains(filter) || c.Area.Contains(filter)).ToList();
            if (filter != null && cases.Count == 0) { Console.Error.WriteLine("FATAL: no case matches --filter " + filter); return 98; }
            Console.WriteLine($"OutSmart.DAXon spec-coverage suite: {cases.Count} cases");

            if (listOnly)
            {
                foreach (var c in cases) Console.WriteLine($"{c.Id}\t{c.Area}\t{c.Title}");
                return 0;
            }

            var results = new List<TestResult>(cases.Count);
            foreach (var c in cases)
                results.Add(RunOne(c));

            // ---- console summary ----
            Console.WriteLine();
            foreach (var g in results.GroupBy(r => r.Case.Area).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                int pass = g.Count(r => r.Status == TestStatus.Pass);
                Console.WriteLine($"{g.Key,-34} {pass,4}/{g.Count(),-4}" +
                    Summarize(g, TestStatus.Fail) + Summarize(g, TestStatus.WrongError) +
                    Summarize(g, TestStatus.Error) + Summarize(g, TestStatus.Timeout));
            }
            int totPass = results.Count(r => r.Status == TestStatus.Pass);
            Console.WriteLine();
            Console.WriteLine($"TOTAL: {totPass}/{results.Count} pass, " +
                $"{results.Count(r => r.Status == TestStatus.Fail)} fail, " +
                $"{results.Count(r => r.Status == TestStatus.WrongError)} wrong-error, " +
                $"{results.Count(r => r.Status == TestStatus.Error)} error, " +
                $"{results.Count(r => r.Status == TestStatus.Timeout)} timeout");

            // ---- failures detail ----
            foreach (var r in results.Where(r => r.Status != TestStatus.Pass))
            {
                Console.WriteLine();
                Console.WriteLine($"[{r.Status.ToString().ToUpperInvariant()}] {r.Case.Id} ({r.Case.Area}) {r.Case.Title} [{r.Case.SpecRef}]");
                if (r.Case.ExpectedErrorCode != null) Console.WriteLine($"  expected error: {r.Case.ExpectedErrorCode}");
                else Console.WriteLine($"  expected: {Trunc(r.Case.Expected)}");
                Console.WriteLine($"  actual  : {Trunc(r.Actual)}");
                if (!string.IsNullOrEmpty(r.Detail)) Console.WriteLine($"  detail  : {Trunc(r.Detail)}");
            }

            // ---- results.tsv ----
            var sb = new StringBuilder();
            sb.Append("id\tarea\tstatus\ttitle\tspecref\texpected\tactual\tdetail\n");
            foreach (var r in results)
            {
                sb.Append(Tsv(r.Case.Id)).Append('\t').Append(Tsv(r.Case.Area)).Append('\t')
                  .Append(r.Status).Append('\t').Append(Tsv(r.Case.Title)).Append('\t')
                  .Append(Tsv(r.Case.SpecRef)).Append('\t')
                  .Append(Tsv(r.Case.ExpectedErrorCode != null ? "error:" + r.Case.ExpectedErrorCode : r.Case.Expected))
                  .Append('\t').Append(Tsv(r.Actual)).Append('\t').Append(Tsv(r.Detail)).Append('\n');
            }
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine();
            Console.WriteLine("results written to " + Path.GetFullPath(outPath));
            return 0;
        }

        private static string Summarize(IGrouping<string, TestResult> g, TestStatus s)
        {
            int n = g.Count(r => r.Status == s);
            return n == 0 ? "" : $"  {s.ToString().ToLowerInvariant()}={n}";
        }

        private static TestResult RunOne(TestCase c)
        {
            var r = new TestResult { Case = c };
            string actual = null;
            Exception ex = null;
            var task = Task.Run(() =>
            {
                try { actual = Engine.Run(c); }
                catch (Exception e) { ex = e; }
            });
            if (!task.Wait(PerTestTimeoutMs))
            {
                r.Status = TestStatus.Timeout;
                r.Actual = "(no completion within " + PerTestTimeoutMs + " ms)";
                return r;
            }

            if (ex != null)
            {
                var chain = Chain(ex);
                if (c.ExpectedErrorCode != null)
                {
                    if (chain.Contains(c.ExpectedErrorCode)) { r.Status = TestStatus.Pass; r.Actual = "error as expected: " + c.ExpectedErrorCode; }
                    else { r.Status = TestStatus.WrongError; r.Actual = chain; }
                }
                else
                {
                    r.Status = TestStatus.Error;
                    r.Actual = chain;
                }
                return r;
            }

            if (c.ExpectedErrorCode != null)
            {
                r.Status = TestStatus.Fail;
                r.Actual = actual;
                r.Detail = "expected error " + c.ExpectedErrorCode + " but transform succeeded";
                return r;
            }

            var exp = c.Expected ?? "";
            var act = actual ?? "";
            if (c.TrimWs) { exp = exp.Trim(); act = act.Trim(); }
            if (string.Equals(exp, act, StringComparison.Ordinal)) { r.Status = TestStatus.Pass; }
            else
            {
                r.Status = TestStatus.Fail;
                r.Actual = actual;
                int at = 0; int len = Math.Min(exp.Length, act.Length);
                while (at < len && exp[at] == act[at]) at++;
                r.Detail = "first diff at char " + at;
            }
            return r;
        }

        private static string Chain(Exception e)
        {
            var sb = new StringBuilder();
            for (var x = e; x != null; x = x.InnerException)
            {
                if (sb.Length > 0) sb.Append(" <= ");
                var code = ErrorCodeOf(x);
                if (code != null) sb.Append('[').Append(code).Append("] ");
                sb.Append(x.GetType().Name).Append(": ").Append(x.Message);
                if (System.Environment.GetEnvironmentVariable("SPEC_STACK") == "1" && x.StackTrace != null)
                {
                    var frames = x.StackTrace.Split('\n');
                    for (int fi = 0; fi < frames.Length && fi < 6; fi++) sb.Append(" ## ").Append(frames[fi].Trim());
                }
                if (sb.Length > 2000) break;
            }
            return sb.ToString();
        }

        // The spec error code (FOAR0001, XPTY0004, ...) is carried on the exception object,
        // not in the message — extract it so ExpectedErrorCode matching works.
        private static string ErrorCodeOf(Exception x)
        {
            try
            {
                if (x is SpecCompileErrorException sce)
                    return sce.Code;
                if (x is global::OutSmart.DAXon.Api.DAXonApiException sae)
                    return sae.GetErrorCode()?.LocalName;
                if (x is global::OutSmart.DAXon.Transformation.XPathException xpe)
                    return xpe.ErrorCodeQName?.GetLocalPart();
            }
            catch { }
            return null;
        }

        private static List<TestCase> Discover()
        {
            var all = new List<TestCase>();
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.Namespace == "OutSmart.DAXon.SpecTests.Cases" && t.IsClass)
                .OrderBy(t => t.Name, StringComparer.Ordinal);
            foreach (var t in types)
            {
                var m = t.GetMethod("Cases", BindingFlags.Public | BindingFlags.Static);
                if (m == null) continue;
                var arr = (TestCase[])m.Invoke(null, null);
                all.AddRange(arr);
            }
            return all;
        }

        private static string Tsv(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

        private static string Trunc(string s)
        {
            s = (s ?? "").Replace("\r", "\\r").Replace("\n", "\\n");
            return s.Length <= 220 ? s : s.Substring(0, 220) + "…(" + s.Length + " chars)";
        }
    }
}
