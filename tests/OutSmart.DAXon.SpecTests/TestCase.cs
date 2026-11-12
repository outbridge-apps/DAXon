using System;

namespace OutSmart.DAXon.SpecTests
{
    /// <summary>
    /// One spec-derived test case. Either <see cref="XPathExpr"/> (wrapped into the standard
    /// stylesheet by the engine) or <see cref="Xslt"/> (a complete stylesheet) is set.
    /// Expected values come from the XPath 3.1 / XSLT 3.0 specs, NOT from observed port behavior.
    /// </summary>
    public sealed class TestCase
    {
        /// <summary>Unique id, e.g. "axes-001". Prefix must match the cluster.</summary>
        public string Id;

        /// <summary>Cluster display name, e.g. "XPath axes/paths".</summary>
        public string Area;

        /// <summary>Short human title.</summary>
        public string Title;

        /// <summary>Spec anchor, e.g. "XPath 3.1 3.3.2" or "F&amp;O 5.4.5 fn:substring".</summary>
        public string SpecRef;

        /// <summary>XPath expression; engine wraps it in the standard text-output stylesheet
        /// (value-of separator="|"). Mutually exclusive with Xslt.</summary>
        public string XPathExpr;

        /// <summary>Complete XSLT 3.0 stylesheet source. Mutually exclusive with XPathExpr.</summary>
        public string Xslt;

        /// <summary>Input document; defaults to &lt;r/&gt; when null.</summary>
        public string InputXml;

        /// <summary>Expected serialized output. Compared ordinal after trimming both sides
        /// (unless TrimWs=false).</summary>
        public string Expected;

        /// <summary>When set, the case expects a static/dynamic error whose message contains this
        /// code (e.g. "FOAR0001", "XPTY0004"). Expected is ignored.</summary>
        public string ExpectedErrorCode;

        /// <summary>Trim leading/trailing whitespace before comparing. Default true.</summary>
        public bool TrimWs = true;
    }

    public enum TestStatus
    {
        Pass,        // output matched, or expected error code matched
        Fail,        // ran fine but output differs from spec expectation
        WrongError,  // an error was raised but with a different code than the spec says
        Error,       // unexpected engine exception (crash / NotImplemented / internal)
        Timeout      // exceeded the per-test time budget
    }

    public sealed class TestResult
    {
        public TestCase Case;
        public TestStatus Status;
        public string Actual;   // actual output or exception summary
        public string Detail;   // extra context (first-diff position, full exception chain head)
    }
}
