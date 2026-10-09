using System;
using System.IO;
using OutSmart.DAXon.Api;

static class Smoke
{
    static int Main()
    {
        string sheet =
            "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='3.0' expand-text='yes'>" +
            "<xsl:output method='xml' omit-xml-declaration='yes'/>" +
            "<xsl:template match=\".[. instance of map(*)]\">" +
              "<out>" +
                "<xsl:for-each-group select='?rows?*' group-by='?dept'>" +
                  "<dept id='{current-grouping-key()}' n='{count(current-group())}'/>" +
                "</xsl:for-each-group>" +
                "<masked>{replace('SKU-12345', '[0-9]+', '#')}</masked>" +
              "</out>" +
            "</xsl:template></xsl:stylesheet>";
        string json = "{\"rows\":[{\"dept\":\"A\"},{\"dept\":\"B\"},{\"dept\":\"A\"}]}";

        var proc = new Processor();
        XdmItem input = proc.NewJsonBuilder().Build(json);
        XsltExecutable exe = proc.NewXsltCompiler().Compile(new StringReader(sheet), "urn:sheet");

        var tr = exe.Load30();
        var w = new StringWriter();
        tr.SetGlobalContextItem(input, true);
        tr.ApplyTemplates(input, proc.NewSerializer(w));
        string got = w.ToString();

        const string want = "<out><dept id=\"A\" n=\"2\"/><dept id=\"B\" n=\"1\"/><masked>SKU-#</masked></out>";
        Console.WriteLine("got : " + got);
        if (got != want)
        {
            Console.WriteLine("want: " + want);
            Console.WriteLine("SMOKE FAILED");
            return 1;
        }

        // Legacy code pages: .NET ships seven encodings unless the engine registers the rest.
        XsltExecutable encExe = proc.NewXsltCompiler().Compile(new StringReader(
            "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='3.0'>" +
            "<xsl:output method='xml' encoding='windows-1251' omit-xml-declaration='yes'/>" +
            "<xsl:template match='/'><xsl:copy-of select='.'/></xsl:template></xsl:stylesheet>"), "urn:enc");
        XdmNode encDoc = proc.NewDocumentBuilder().Build(new StringReader("<a>Привет</a>"), "urn:enc-in");
        var encOut = new MemoryStream();
        Serializer encSer = proc.NewSerializer();
        encSer.SetOutputStream(encOut);
        var encTr = encExe.Load30();
        encTr.SetGlobalContextItem(encDoc, true);
        encTr.ApplyTemplates(encDoc, encSer);
        string encHex = BitConverter.ToString(encOut.ToArray());
        const string encWant = "3C-61-3E-CF-F0-E8-E2-E5-F2-3C-2F-61-3E";
        Console.WriteLine("enc : " + encHex);
        if (encHex != encWant)
        {
            Console.WriteLine("want: " + encWant);
            Console.WriteLine("SMOKE FAILED (windows-1251)");
            return 1;
        }

        // A policy without file reads: unparsed-text of a file: URI fails as a missing file does; by default it reads.
        string file = Path.Combine(Path.GetTempPath(), "daxon-smoke-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(file, "text");
        try
        {
            string read = "unparsed-text('" + new Uri(file).AbsoluteUri + "')";
            string open = Outcome(proc, read);
            string closed = Outcome(new Processor(new ProcessorOptions { AllowFileRead = false }), read);
            Console.WriteLine("read: " + open + " / " + closed);
            if (open != "text" || closed != "FOUT1170")
            {
                Console.WriteLine("want: text / FOUT1170");
                Console.WriteLine("SMOKE FAILED (AllowFileRead)");
                return 1;
            }
        }
        finally
        {
            File.Delete(file);
        }

        Console.WriteLine("SMOKE OK");
        return 0;
    }

    // The value of an expression, or the code of its error.
    static string Outcome(Processor proc, string expression)
    {
        try
        {
            return proc.NewXPathCompiler().Compile(expression).Load().EvaluateSingle().GetStringValue();
        }
        catch (DAXonApiException e)
        {
            return e.GetErrorCode()?.LocalName ?? e.Message;
        }
    }
}
