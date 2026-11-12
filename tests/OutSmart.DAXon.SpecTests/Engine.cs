using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OutSmart.DAXon.SpecTests
{
    /// <summary>
    /// Thrown by Engine when stylesheet COMPILATION fails carrying a spec error code. Saxon reports
    /// static errors (XPST0017, XPTY0004, XTSE0680, ...) to the compiler's error list and then throws a
    /// generic "Errors were reported during stylesheet compilation" wrapper that has no code on it; this
    /// surfaces the collected code so ExpectedErrorCode matching works for compile-time errors too.
    /// </summary>
    public sealed class SpecCompileErrorException : Exception
    {
        public string Code { get; }

        public SpecCompileErrorException(string code, Exception inner)
            : base("compile error " + code, inner)
        {
            Code = code;
        }
    }

    /// <summary>
    /// Thin wrapper over the port's s9api surface. Mirrors the invocation pattern of
    /// acceptance/runner (Processor -> XsltCompiler.Compile -> Load30 -> ApplyTemplates ->
    /// Serializer), but sources/outputs are in-memory strings.
    /// A fresh Processor per case keeps cases isolated.
    /// </summary>
    internal static class Engine
    {
        internal const string DefaultInput = "<r/>";

        // Standard wrapper for XPathExpr cases: text output, sequence items joined with "|".
        private const string XPathWrapperFmt =
@"<xsl:stylesheet version=""3.0""
    xmlns:xsl=""http://www.w3.org/1999/XSL/Transform""
    xmlns:xs=""http://www.w3.org/2001/XMLSchema""
    xmlns:fn=""http://www.w3.org/2005/xpath-functions""
    xmlns:map=""http://www.w3.org/2005/xpath-functions/map""
    xmlns:array=""http://www.w3.org/2005/xpath-functions/array""
    xmlns:math=""http://www.w3.org/2005/xpath-functions/math""
    xmlns:err=""http://www.w3.org/2005/xqt-errors""
    exclude-result-prefixes=""#all"">
  <xsl:output method=""text""/>
  <xsl:template match=""/"">
    <xsl:value-of select=""{0}"" separator=""|""/>
  </xsl:template>
</xsl:stylesheet>";

        internal static string BuildXsltForCase(TestCase c)
        {
            if (c.Xslt != null) return c.Xslt;
            // XML-escape the expression for embedding in the select attribute.
            var e = c.XPathExpr
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
            return string.Format(XPathWrapperFmt, e);
        }

        internal static string Run(TestCase c)
        {
            var xslt = BuildXsltForCase(c);
            var input = c.InputXml ?? DefaultInput;

            var processor = new global::OutSmart.DAXon.Api.Processor();
            var compiler = processor.NewXsltCompiler();

            // Collect static errors so a compile failure can surface its spec code (XPST0017 etc).
            var compileErrors = new List<global::OutSmart.DAXon.Api.IXmlProcessingError>();
            compiler.SetErrorList(compileErrors);

            global::OutSmart.DAXon.Api.XsltExecutable exe;
            try
            {
                using (var xs = new MemoryStream(Encoding.UTF8.GetBytes(xslt)))
                {
                    exe = compiler.Compile(xs, "file:///spec-tests/" + c.Id + ".xslt");
                }
            }
            catch (Exception ex)
            {
                foreach (var e in compileErrors)
                {
                    string code = null;
                    try { code = e.GetErrorCode()?.LocalName; } catch { }
                    if (!string.IsNullOrEmpty(code))
                    {
                        throw new SpecCompileErrorException(code, ex);
                    }
                }

                throw;
            }

            global::OutSmart.DAXon.Api.XdmNode inputDoc;
            using (var ds = new MemoryStream(Encoding.UTF8.GetBytes(input)))
            {
                inputDoc = processor.NewDocumentBuilder().Build(ds, "file:///spec-tests/" + c.Id + ".xml");
            }

            var transformer = exe.Load30();
            transformer.SetGlobalContextItem(inputDoc, true); // Transform CLI semantics (as acceptance/runner)
            var sw = new StringWriter();
            transformer.ApplyTemplates(inputDoc, processor.NewSerializer(sw));
            return sw.ToString();
        }
    }
}
