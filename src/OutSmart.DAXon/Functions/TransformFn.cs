////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using SequenceType = OutSmart.DAXon.Values.SequenceType;
using URI = OutSmart.DAXon.Internal.Net.URI;

namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// fn:transform() — XPath 3.1. Runs a nested XSLT transformation using this engine's own s9api layer.
    /// HE port notes: no EE licensing (requested-properties asking for schema-awareness/streaming raise
    /// FOXT0001), no saxon:configuration vendor option (ignored), and a stylesheet supplied as a node is
    /// re-serialized and compiled from text (the Source hierarchy was removed from this port).
    /// </summary>
    internal sealed class TransformFn : SystemFunction
    {

        private const string DummyBaseOutputUriScheme = "dummy";
        private static readonly string[] transformOptionNames30 = new string[]
        {
            "package-name", "package-version", "package-node", "package-location", "static-params", "global-context-item",
            "template-params", "tunnel-params", "initial-function", "function-params",
            "enable-assertions"
        };

        private bool IsTransformOptionName30(string str)
        {
            foreach (string s in transformOptionNames30)
            {
                if (s.Equals(str))
                {
                    return true;
                }
            }

            return false;
        }

        public static OptionsParameter MakeOptionsParameter()
        {
            SequenceType singleQName = SequenceType.MakeSequenceType(BuiltInAtomicType.QNAME, StaticProperty.EXACTLY_ONE);
            SequenceType singleMap = SequenceType.MakeSequenceType(MapType.ANY_MAP_TYPE, StaticProperty.EXACTLY_ONE);
            OptionsParameter op = new OptionsParameter();
            op.AddAllowedOption("xslt-version", SequenceType.SINGLE_DECIMAL);
            op.AddAllowedOption("stylesheet-location", SequenceType.SINGLE_STRING);
            op.AddAllowedOption("stylesheet-node", SequenceType.SINGLE_NODE);
            op.AddAllowedOption("stylesheet-text", SequenceType.SINGLE_STRING);
            op.AddAllowedOption("stylesheet-base-uri", SequenceType.SINGLE_STRING);
            op.AddAllowedOption("base-output-uri", SequenceType.SINGLE_STRING);
            op.AddAllowedOption("stylesheet-params", singleMap);
            op.AddAllowedOption("source-node", SequenceType.SINGLE_NODE);
            op.AddAllowedOption("source-location", SequenceType.SINGLE_STRING);
            op.AddAllowedOption("initial-mode", singleQName);
            op.AddAllowedOption("initial-match-selection", SequenceType.ANY_SEQUENCE);
            op.AddAllowedOption("initial-template", singleQName);
            op.AddAllowedOption("delivery-format", SequenceType.SINGLE_STRING);
            op.AddAllowedOption("serialization-params", singleMap);
            op.AddAllowedOption("vendor-options", singleMap);
            op.AddAllowedOption("cache", SequenceType.SINGLE_BOOLEAN);
            op.AddAllowedOption("enable-assertions", SequenceType.SINGLE_BOOLEAN);
            op.AddAllowedOption("enable-messages", SequenceType.SINGLE_BOOLEAN);
            op.AddAllowedOption("package-name", SequenceType.SINGLE_STRING);
            op.AddAllowedOption("package-version", SequenceType.SINGLE_STRING);
            op.AddAllowedOption("package-node", SequenceType.SINGLE_NODE);
            op.AddAllowedOption("package-location", SequenceType.SINGLE_STRING);
            op.AddAllowedOption("static-params", singleMap);
            op.AddAllowedOption("global-context-item", SequenceType.SINGLE_ITEM);
            op.AddAllowedOption("template-params", singleMap);
            op.AddAllowedOption("tunnel-params", singleMap);
            op.AddAllowedOption("initial-function", singleQName);
            op.AddAllowedOption("function-params", ArrayItemType.SINGLE_ARRAY);
            op.AddAllowedOption("requested-properties", singleMap);
            // function(xs:string, item()*) as item()* — function-type Matches is permissive in this port
            op.AddAllowedOption("post-process", SequenceType.ANY_SEQUENCE);
            return op;
        }

        private void CheckTransformOptions(Dictionary<string, IGroundedValue> options, IXPathContext context, int languageVersion)
        {
            if (options.Count == 0)
            {
                throw new XPathException("No transformation options supplied", "FOXT0002");
            }

            foreach (string keyName in options.Keys)
            {
                if (IsTransformOptionName30(keyName) && languageVersion < 30)
                {
                    throw new XPathException("The transform option " + keyName + " is only available when using an XSLT 3.0 processor", "FOXT0002");
                }
            }
        }

        private string CheckStylesheetMutualExclusion30(Dictionary<string, IGroundedValue> map)
        {
            string styleOption = ExactlyOneOf(map, "stylesheet-location", "stylesheet-node", "stylesheet-text",
                                              "package-name", "package-node", "package-location");
            if (styleOption.Equals("package-location"))
            {
                throw new XPathException("The transform option " + styleOption + " is not implemented in Saxon", "FOXT0002");
            }

            return styleOption;
        }

        private string OneOf(Dictionary<string, IGroundedValue> map, params string[] keys)
        {
            string found = null;
            foreach (string s in keys)
            {
                if (map.GetOrDefault(s) != null)
                {
                    if (found != null)
                    {
                        throw new XPathException(
                            "The following transform options are mutually exclusive: " + Enumerate(keys), "FOXT0002");
                    }
                    else
                    {
                        found = s;
                    }
                }
            }

            return found;
        }

        private string ExactlyOneOf(Dictionary<string, IGroundedValue> map, params string[] keys)
        {
            string found = OneOf(map, keys);
            if (found == null)
            {
                // Upstream throws without a code here; FOXT0002 is what the fn:transform spec requires
                throw new XPathException("One of the following transform options must be present: " + Enumerate(keys), "FOXT0002");
            }

            return found;
        }

        private string Enumerate(params string[] keys)
        {
            return string.Join(" | ", keys);
        }

        private string CheckInvocationMutualExclusion30(Dictionary<string, IGroundedValue> map)
        {
            return OneOf(map, "initial-mode", "initial-template", "initial-function");
        }

        private void Unsuitable(string option, string value)
        {
            throw new XPathException("No XSLT processor is available with xsl:" + option + " = " + value, "FOXT0001");
        }

        private bool AsBoolean(AtomicValue value)
        {
            if (value is BooleanValue)
            {
                return ((BooleanValue)value).GetBooleanValue();
            }
            else if (value is StringValue)
            {
                string s = Whitespace.NormalizeWhitespace(value.UnicodeStringValue).ToString();
                if (s.Equals("yes") || s.Equals("true") || s.Equals("1"))
                {
                    return true;
                }
                else if (s.Equals("no") || s.Equals("false") || s.Equals("0"))
                {
                    return false;
                }
            }

            throw new XPathException("Unrecognized boolean value " + value, "FOXT0002");
        }

        private void SetRequestedProperties(Dictionary<string, IGroundedValue> options)
        {
            MapItem requestedProps = (MapItem)options.GetOrDefault("requested-properties").Head();
            foreach (OutSmart.DAXon.Values.Maps.KeyValuePair entry in requestedProps.KeyValuePairs())
            {
                if (!(entry.key is QNameValue))
                {
                    continue;
                }

                StructuredQName optionName = ((QNameValue)entry.key).GetStructuredQName();
                AtomicValue value = (AtomicValue)entry.value.Head();
                if (optionName.GetNamespaceUri().Equals(NamespaceUri.XSLT))
                {
                    string localName = optionName.GetLocalPart();
                    string val = value.GetStringValue();
                    switch (localName)
                    {
                        case "vendor-url":
                            if (!(val.Contains("saxonica.com") || val.Equals("Saxonica")))
                            {
                                Unsuitable("vendor-url", val);
                            }

                            break;
                        case "product-name":
                            if (!val.Equals("SAXON"))
                            {
                                Unsuitable("vendor-url", val);
                            }

                            break;
                        case "product-version":
                            if (!Core.Version.ProductVersion.StartsWith(val, StringComparison.Ordinal))
                            {
                                Unsuitable("product-version", val);
                            }

                            break;
                        case "is-schema-aware":
                            // HE build: schema-awareness is never available
                            if (AsBoolean(value))
                            {
                                Unsuitable("is-schema-aware", val);
                            }

                            break;
                        case "supports-serialization":
                            if (!AsBoolean(value))
                            {
                                Unsuitable("supports-serialization", val);
                            }

                            break;
                        case "supports-backwards-compatibility":
                            if (!AsBoolean(value))
                            {
                                Unsuitable("supports-backwards-compatibility", val);
                            }

                            break;
                        case "supports-namespace-axis":
                            if (!AsBoolean(value))
                            {
                                Unsuitable("supports-namespace-axis", val);
                            }

                            break;
                        case "supports-streaming":
                            if (AsBoolean(value))
                            {
                                Unsuitable("supports-streaming", val);
                            }

                            break;
                        case "supports-dynamic-evaluation":
                            // xsl:evaluate is available; a request to disable it is accepted silently
                            break;
                        case "supports-higher-order-functions":
                            if (!AsBoolean(value))
                            {
                                Unsuitable("supports-higher-order-functions", val);
                            }

                            break;
                        case "xpath-version":
                            if (!double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double xpv) || xpv > 3.1)
                            {
                                Unsuitable("xpath-version", val);
                            }

                            break;
                        case "xsd-version":
                            if (!double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double xsdv) || xsdv > 1.1)
                            {
                                Unsuitable("xsd-version", val);
                            }

                            break;
                    }
                }
            }
        }

        private void SetStaticParams(Dictionary<string, IGroundedValue> options, XsltCompiler xsltCompiler)
        {
            MapItem staticParamsMap = (MapItem)options.GetOrDefault("static-params").Head();
            foreach (OutSmart.DAXon.Values.Maps.KeyValuePair entry in staticParamsMap.KeyValuePairs())
            {
                if (!(entry.key is QNameValue))
                {
                    throw new XPathException("Parameter names in static-params must be supplied as QNames", "FOXT0002");
                }

                QName paramName = new QName(((QNameValue)entry.key).GetStructuredQName());
                xsltCompiler.SetParameter(paramName, XdmValue.Wrap(entry.value));
            }
        }

        private XsltExecutable GetStylesheet(Dictionary<string, IGroundedValue> options, XsltCompiler xsltCompiler, string styleOptionStr, IXPathContext context)
        {
            IItem styleOptionItem = options.GetOrDefault(styleOptionStr).Head();
            URI stylesheetBaseUri = null;
            IGroundedValue seq;
            if ((seq = options.GetOrDefault("stylesheet-base-uri")) != null)
            {
                string styleBaseUri = seq.Head().GetStringValue();
                stylesheetBaseUri = URI.Create(styleBaseUri);
                if (!stylesheetBaseUri.IsAbsolute())
                {
                    stylesheetBaseUri = ResolveURI.MakeAbsolute(styleBaseUri, StaticBaseUriString);
                }
            }

            List<IXmlProcessingError> compileErrors = new List<IXmlProcessingError>();
            IErrorReporter originalReporter = xsltCompiler.GetErrorReporter();
            xsltCompiler.SetErrorReporter(new TransformErrorReporter(compileErrors, originalReporter));
            bool cacheable = options.GetOrDefault("static-params") == null;
            if (options.GetOrDefault("cache") != null)
            {
                cacheable &= ((BooleanValue)options.GetOrDefault("cache").Head()).GetBooleanValue();
            }

            StylesheetCache cache = context.GetController().GetStylesheetCache();
            XsltExecutable executable = null;
            switch (styleOptionStr)
            {
                case "stylesheet-location":
                    {
                        string stylesheetLocation = styleOptionItem.GetStringValue();
                        if (cacheable)
                        {
                            executable = cache.GetStylesheetByLocation(stylesheetLocation);
                        }

                        if (executable == null)
                        {
                            ResolvedResource style;
                            try
                            {
                                string @base = StaticBaseUriString;
                                ResourceRequest request = new ResourceRequest();
                                request.baseUri = @base;
                                request.relativeUri = stylesheetLocation;
                                request.uri = ResolveURI.MakeAbsolute(stylesheetLocation, @base).ToString();
                                request.nature = ResourceRequest.XSLT_NATURE;
                                request.purpose = ResourceRequest.ANY_PURPOSE;
                                Configuration config = xsltCompiler.GetProcessor().UnderlyingConfiguration;
                                style = request.Resolve(xsltCompiler.GetResourceResolver(),
                                                        config.GetResourceResolver(),
                                                        new DirectResourceResolver(config));
                            }
                            catch (OutSmart.DAXon.Internal.ResourceDeniedException e)
                            {
                                // as for a stylesheet that cannot be read
                                throw new XPathException(e.Message, "FOXT0002");
                            }
                            catch (XPathException)
                            {
                                throw;
                            }
                            catch (Exception e)
                            {
                                throw new XPathException("Failed to resolve stylesheet-location in fn:transform: " + e.Message);
                            }

                            if (style == null)
                            {
                                throw new XPathException("Failed to resolve stylesheet-location " + stylesheetLocation, "FOXT0002");
                            }

                            try
                            {
                                executable = CompileResolved(xsltCompiler, style, stylesheetBaseUri);
                            }
                            catch (DAXonApiException e)
                            {
                                return ReportCompileError(e, compileErrors);
                            }

                            if (cacheable)
                            {
                                cache.SetStylesheetByLocation(stylesheetLocation, executable);
                            }
                        }

                        break;
                    }

                case "stylesheet-node":
                case "package-node":
                    {
                        NodeInfo stylesheetNode = (NodeInfo)styleOptionItem;
                        if (cacheable)
                        {
                            executable = cache.GetStylesheetByNode(stylesheetNode);
                        }

                        if (executable == null)
                        {
                            // The Source hierarchy is gone from this port: re-serialize the node and compile from
                            // text, with the systemId carrying the effective base URI (stylesheet-base-uri wins).
                            string sysId = stylesheetBaseUri != null ? stylesheetBaseUri.ToASCIIString() : stylesheetNode.GetBaseURI();
                            string text = SerializeStylesheetNode(xsltCompiler.GetProcessor(), stylesheetNode);
                            try
                            {
                                executable = xsltCompiler.Compile(new StringReader(text), sysId);
                            }
                            catch (DAXonApiException e)
                            {
                                ReportCompileError(e, compileErrors);
                            }

                            if (cacheable)
                            {
                                cache.SetStylesheetByNode(stylesheetNode, executable);
                            }
                        }

                        break;
                    }

                case "stylesheet-text":
                    {
                        string stylesheetText = styleOptionItem.GetStringValue();
                        if (cacheable)
                        {
                            executable = cache.GetStylesheetByText(stylesheetText);
                        }

                        if (executable == null)
                        {
                            // upstream: systemId stays absent without stylesheet-base-uri (relative includes must fail)
                            string sysId = stylesheetBaseUri?.ToASCIIString();
                            try
                            {
                                executable = xsltCompiler.Compile(new StringReader(stylesheetText), sysId);
                            }
                            catch (DAXonApiException e)
                            {
                                ReportCompileError(e, compileErrors);
                            }

                            if (cacheable)
                            {
                                cache.SetStylesheetByText(stylesheetText, executable);
                            }
                        }

                        break;
                    }

                case "package-name":
                    {
                        string packageName = styleOptionItem.GetStringValue().Trim();
                        string packageVersion = null;
                        if (options.GetOrDefault("package-version") != null)
                        {
                            packageVersion = options.GetOrDefault("package-version").Head().GetStringValue();
                        }

                        try
                        {
                            XsltPackage pack = xsltCompiler.ObtainPackage(packageName, packageVersion);
                            if (pack == null)
                            {
                                throw new XPathException("Cannot locate package " + packageName + " version " + packageVersion, "FOXT0002");
                            }

                            executable = pack.Link();
                        }
                        catch (DAXonApiException e)
                        {
                            if (e.InnerException is XPathException)
                            {
                                throw (XPathException)e.InnerException;
                            }
                            else
                            {
                                throw new XPathException(e.Message);
                            }
                        }

                        break;
                    }
            }

            return executable;
        }

        private static string SerializeStylesheetNode(Processor processor, NodeInfo node)
        {
            // Same receiver chain as fn:serialize (SerializerFactory -> emitter), which is the proven
            // serialization path in this port.
            var builder = new OutSmart.DAXon.Text.UnicodeBuilder();
            var uwResult = new OutSmart.DAXon.Serialization.UnicodeWriterResult(builder, null);
            var props = new Properties();
            props.SetProperty("method", "xml");
            props.SetProperty("omit-xml-declaration", "yes");
            Configuration config = processor.UnderlyingConfiguration;
            SerializerFactory sf = config.SerializerFactory;
            PipelineConfiguration pipe = config.MakePipelineConfiguration();
            using (IReceiver outr = sf.GetReceiver(uwResult, new SerializationProperties(props), pipe))
            {
                outr.Open();
                outr.Append(node);
                outr.Close();
            }

            return builder.ToString();
        }

        private static XsltExecutable CompileResolved(XsltCompiler xsltCompiler, ResolvedResource rr, URI stylesheetBaseUri)
        {
            string sysId = stylesheetBaseUri != null ? stylesheetBaseUri.ToASCIIString() : rr.SystemId;
            if (rr.Node != null)
            {
                return xsltCompiler.Compile(new StringReader(SerializeStylesheetNode(xsltCompiler.GetProcessor(), rr.Node)), sysId ?? rr.Node.GetBaseURI());
            }

            if (rr.TextReader != null)
            {
                return xsltCompiler.Compile(rr.TextReader, sysId);
            }

            if (rr.Stream == null && rr.SystemId == null)
            {
                throw new XPathException("Failed to read stylesheet " + sysId, "FOXT0002");
            }

            // Only a system id when the resolver could not open it (missing, in use, a directory): the parser opens
            // it, as Java compiles the resolved source, and its I/O error says why.
            return rr.Stream == null ? xsltCompiler.Compile(rr) : xsltCompiler.Compile(rr.Stream, sysId);
        }

        private XsltExecutable ReportCompileError(DAXonApiException e, List<IXmlProcessingError> compileErrors)
        {
            foreach (IXmlProcessingError te in compileErrors)
            {
                // The fn:transform spec requires FOXT0002 for a stylesheet compile error
                throw XPathException.FromXmlProcessingError(te)
                        .MaybeWithErrorCode("FOXT0002")
                        .ReplacingErrorCode(DAXonErrorCode.SXXP0003, "FOXT0002");
            }

            if (e.InnerException is XPathException)
            {
                throw (XPathException)e.InnerException;
            }
            else
            {
                throw new XPathException(e.Message, "FOXT0002");
            }
        }

        // Depth of fn:transform calls open on this thread. A transformation is single-threaded and a
        // nested transform runs to completion inside its caller's frame, so this counts exactly the
        // open nesting - the same quantity the include chain reads off compilation.ImportStack.
        [ThreadStatic]
        private static int transformNesting;

        // QTDBG_XF=1 prints the open nesting on entry.
        private static readonly bool DbgNesting = Environment.GetEnvironmentVariable("QTDBG_XF") != null;

        // Stack the UNWIND needs per open level, over and above StackGuard's own margin. MEASURED in
        // three steps, and each step was needed (see Call for the shape of the guard):
        //   1. A plain "1 idiv 0" raised 30 levels deep already killed a 1 MB thread while 10 levels
        //      reported fine - so the error path costs tens of KB per level against ~1.2 KB to descend.
        //   2. At 32 KB the guard fired exactly where the arithmetic said it would (QTDBG_XF showed
        //      nesting=23, ~990 KB still free) and the process died ANYWAY on the unwind.
        //   3. That pins the real cost: >43 KB per level. The reserve must therefore be >= the unwind
        //      cost itself, not merely proportional to depth - with k < u, a deeper n always outruns
        //      the reserve, which is why step 2 failed while looking arithmetically correct.
        // 64 KB caps nesting near 11 on a 1 MB thread and ~59 on 4 MB; real stylesheets nest one or
        // two transforms, and the alternative at any depth past the cap is process death.
        private const ulong UnwindReservePerTransform = 64 * 1024;

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            // A sheet may call fn:transform on ITSELF (stylesheet-location pointing at its own URI),
            // and nothing else bounds that nesting: every level builds a fresh Processor and
            // Controller below, so the SXLM0001 recursion counter starts over at each one, and the
            // compile-time stylesheet-depth probes only measure the depth WITHIN one sheet. Measured
            // before this guard: ~500 levels completed on a 1 MB thread and 700 killed the process
            // outright, while the same 700 completed on 4 MB - stack-bound, uncatchable, no diagnosis.
            //   The reserve scales with the open nesting, and that was MEASURED rather than assumed
            // (round AW's lesson, and the first attempt here got it wrong): a plain Probe() does fire
            // exactly as designed - QTDBG_SG showed "THREW at remaining=255KB" - and the process died
            // anyway, because unwinding several hundred open levels costs more than the whole fixed
            // margin. The per-level unwind cost is small (~1 KB, far below the include chain's ~28 KB,
            // since round BC made RecursionDepthError a type no catch site re-decorates), but small
            // times depth still beats any constant, which is the whole point of the AW rule.
            if (DbgNesting)
            {
                // How the reserve above was calibrated, and the only way to see it: an uncatchable
                // overflow leaves no trace, so the last nesting printed is the diagnosis.
                Console.Error.WriteLine("[XF] nesting=" + transformNesting);
                Console.Error.Flush();
            }

            try
            {
                StackGuard.Probe(UnwindReservePerTransform * (ulong)transformNesting);
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                throw e.Describe("fn:transform nesting is too deep (insufficient stack on this thread)",
                    "FOXT0003", null);
            }

            transformNesting++;
            try
            {
                return CallTransform(context, arguments);
            }
            finally
            {
                transformNesting--;
            }
        }

        private ISequence CallTransform(IXPathContext context, ISequence[] arguments)
        {
            Dictionary<string, IGroundedValue> options = Details.optionDetails.ProcessSuppliedOptions((MapItem)arguments[0].Head(), context);

            // vendor-options (saxon:configuration / saxon:schema-validation) are not supported in this HE
            // port; the option is accepted and ignored.
            Processor processor = new Processor(context.GetConfiguration());
            int languageVersion = GetRetainedStaticContext().GetPackageData().HostLanguageVersion;
            if (languageVersion == 0)
            {
                languageVersion = 30;
            }

            CheckTransformOptions(options, context, languageVersion);
            if (options.GetOrDefault("xslt-version") != null)
            {
                DecimalValue requestedVersion = (DecimalValue)options.GetOrDefault("xslt-version").Head();
                if (requestedVersion.GetDoubleValue() * 10 > languageVersion)
                {
                    throw new XPathException("The transform option xslt-version is higher than the language version supported by the calling transformation", "FOXT0002");
                }
            }

            string principalInput = OneOf(options, "source-node", "source-location", "initial-match-selection");

            string invocationOption = CheckInvocationMutualExclusion30(options);
            string invocationName = invocationOption ?? "invocation";
            if (!invocationName.Equals("initial-template") && !invocationName.Equals("initial-function") && principalInput == null)
            {
                invocationName = "initial-template";
                options["initial-template"] = new QNameValue("", NamespaceUri.XSLT, "initial-template");
            }

            if (invocationName.Equals("initial-function") && options.GetOrDefault("function-params") == null)
            {
                throw new XPathException("Use of the transform option initial-function requires the function parameters to be supplied using the option function-params", "FOXT0002");
            }

            if (!invocationName.Equals("initial-function") && options.GetOrDefault("function-params") != null)
            {
                throw new XPathException("The transform option function-params can only be used if the option initial-function is also used", "FOXT0002");
            }

            string styleOption = CheckStylesheetMutualExclusion30(options);

            if (options.GetOrDefault("requested-properties") != null)
            {
                SetRequestedProperties(options);
            }

            XsltCompiler xsltCompiler = processor.NewXsltCompiler();
            xsltCompiler.SetResourceResolver(context.GetResourceResolver());
            xsltCompiler.SetJustInTimeCompilation(false);
            xsltCompiler.SetErrorReporter(context.GetErrorReporter());
            if (options.GetOrDefault("enable-assertions") != null)
            {
                xsltCompiler.SetAssertionsEnabled(AsBoolean((AtomicValue)options.GetOrDefault("enable-assertions").Head()));
            }

            if (options.GetOrDefault("static-params") != null)
            {
                SetStaticParams(options, xsltCompiler);
            }

            XsltExecutable sheet = GetStylesheet(options, xsltCompiler, styleOption, context);
            Xslt30Transformer transformer = sheet.Load30();
            transformer.SetErrorReporter(context.GetErrorReporter());

            // The nested run arms its own controller from its own Processor, so without this it
            // would start a FULL fresh time budget inside a run that is already on the clock -
            // and could do so recursively. Cap it at the calling run's deadline.
            transformer.UnderlyingController.CapDeadlineTo(context.GetController());

            bool enableMessages = true;
            if (options.GetOrDefault("enable-messages") != null)
            {
                enableMessages = AsBoolean((AtomicValue)options.GetOrDefault("enable-messages").Head());
            }

            if (!enableMessages)
            {
                transformer.UnderlyingController.SetMessageFactory(() => new Sink(transformer.UnderlyingController.MakePipelineConfiguration()));
            }

            string deliveryFormat = "document";
            NodeInfo sourceNode = null;
            string sourceLocation = null;
            XdmValue initialMatchSelection = null;
            QName initialTemplate = null;
            QName initialMode = null;
            string baseOutputUri = null;
            Dictionary<QName, XdmValue> stylesheetParams = new Dictionary<QName, XdmValue>();
            MapItem serializationParamsMap = null;
            XdmItem globalContextItem = null;
            Dictionary<QName, XdmValue> templateParams = new Dictionary<QName, XdmValue>();
            Dictionary<QName, XdmValue> tunnelParams = new Dictionary<QName, XdmValue>();
            QName initialFunction = null;
            XdmValue[] functionParams = null;
            IFunctionItem postProcessor = null;
            string principalResultKey = "output";

            foreach (string name in options.Keys)
            {
                IGroundedValue value = options.GetOrDefault(name);
                IItem head = value.Head();
                switch (name)
                {
                    case "source-node":
                        sourceNode = (NodeInfo)head;
                        break;
                    case "source-location":
                        sourceLocation = head.GetStringValue();
                        break;
                    case "initial-template":
                        initialTemplate = new QName(((QNameValue)head).GetStructuredQName());
                        break;
                    case "initial-mode":
                        initialMode = new QName(((QNameValue)head).GetStructuredQName());
                        break;
                    case "initial-match-selection":
                        initialMatchSelection = XdmValue.Wrap(value);
                        break;
                    case "delivery-format":
                        deliveryFormat = head.GetStringValue();
                        if (!deliveryFormat.Equals("document") && !deliveryFormat.Equals("serialized") && !deliveryFormat.Equals("raw"))
                        {
                            throw new XPathException("The transform option delivery-format should be one of: document|serialized|raw ", "FOXT0002");
                        }

                        break;
                    case "base-output-uri":
                        baseOutputUri = head.GetStringValue();
                        principalResultKey = baseOutputUri;
                        break;
                    case "serialization-params":
                        serializationParamsMap = (MapItem)head;
                        break;
                    case "stylesheet-params":
                        ProcessParams((MapItem)head, stylesheetParams);
                        break;
                    case "global-context-item":
                        globalContextItem = (XdmItem)XdmValue.Wrap(head);
                        break;
                    case "template-params":
                        ProcessParams((MapItem)head, templateParams);
                        break;
                    case "tunnel-params":
                        ProcessParams((MapItem)head, tunnelParams);
                        break;
                    case "initial-function":
                        initialFunction = new QName(((QNameValue)head).GetStructuredQName());
                        break;
                    case "function-params":
                        {
                            ArrayItem functionParamsArray = (ArrayItem)head;
                            functionParams = new XdmValue[functionParamsArray.ArrayLength()];
                            for (int i = 0; i < functionParams.Length; i++)
                            {
                                functionParams[i] = XdmValue.Wrap(functionParamsArray.Get(i));
                            }

                            break;
                        }

                    case "post-process":
                        postProcessor = head as IFunctionItem;
                        break;
                }
            }

            if (baseOutputUri == null)
            {
                baseOutputUri = StaticBaseUriString;
            }
            else
            {
                try
                {
                    URI @base = new URI(baseOutputUri);
                    if (!@base.IsAbsolute())
                    {
                        @base = ResolveURI.MakeAbsolute(baseOutputUri, StaticBaseUriString);
                        baseOutputUri = @base.ToASCIIString();
                    }
                }
                catch (Exception)
                {
                    throw new XPathException("Invalid base output URI " + baseOutputUri, "FOXT0002");
                }
            }

            Deliverer deliverer = Deliverer.MakeDeliverer(processor, deliveryFormat);
            deliverer.SetTransformer(transformer);
            deliverer.SetBaseOutputUri(baseOutputUri);
            deliverer.SetPrincipalResultKey(principalResultKey);
            deliverer.SetPostProcessor(postProcessor, context);

            XsltController controller = transformer.UnderlyingController;
            controller.ResultDocumentResolver = deliverer;

            IDestination destination = deliverer.GetPrimaryDestination(serializationParamsMap);
            ISequence result;
            try
            {
                transformer.SetStylesheetParameters(stylesheetParams);
                transformer.SetBaseOutputURI(baseOutputUri);
                transformer.SetInitialTemplateParameters(templateParams, false);
                transformer.SetInitialTemplateParameters(tunnelParams, true);
                transformer.SetResourceResolver(context.GetResourceResolver());

                if (sourceLocation != null && sourceNode == null && initialMatchSelection == null)
                {
                    // Resolve and build the source document (no streaming path in this port)
                    string @base = StaticBaseUriString;
                    ResourceRequest rr = new ResourceRequest();
                    rr.relativeUri = sourceLocation;
                    rr.baseUri = @base;
                    rr.nature = ResourceRequest.XML_NATURE;
                    rr.purpose = ResourceRequest.ANY_PURPOSE;
                    try
                    {
                        rr.uri = ResolveURI.MakeAbsolute(sourceLocation, @base).ToString();
                    }
                    catch (Exception)
                    {
                        throw new XPathException("Unresolvable sourceLocation URI " + sourceLocation, "FOXT0003");
                    }

                    Configuration targetConfig = context.GetConfiguration();
                    try
                    {
                        ResolvedResource ss = rr.Resolve(xsltCompiler.GetResourceResolver(),
                                                         targetConfig.GetResourceResolver(),
                                                         new DirectResourceResolver(targetConfig));
                        if (ss == null)
                        {
                            throw new XPathException("Cannot resolve source-location " + sourceLocation, "FOXT0003");
                        }

                        sourceNode = targetConfig.BuildDocumentTree(ss, targetConfig.GetParseOptions()).GetRootNode();
                    }
                    catch (XPathException e)
                    {
                        e.MaybeSetErrorCode("FOXT0003");
                        throw;
                    }
                }

                if (sourceNode != null && globalContextItem == null)
                {
                    transformer.GlobalContextItem = new XdmNode(sourceNode.Root);
                }

                if (globalContextItem != null)
                {
                    transformer.GlobalContextItem = globalContextItem;
                }

                if (initialTemplate != null)
                {
                    transformer.CallTemplate(initialTemplate, destination);
                    result = deliverer.PrimaryResult;
                }
                else if (initialFunction != null)
                {
                    transformer.CallFunction(initialFunction, functionParams, destination);
                    result = deliverer.PrimaryResult;
                }
                else
                {
                    if (initialMode != null)
                    {
                        controller.SetInitialMode(initialMode.GetStructuredQName());
                    }

                    if (initialMatchSelection == null && sourceNode != null)
                    {
                        initialMatchSelection = XdmValue.Wrap(sourceNode);
                    }

                    transformer.ApplyTemplates(initialMatchSelection, destination);
                    result = deliverer.PrimaryResult;
                }
            }
            catch (DAXonApiException e)
            {
                if (e.InnerException is XPathException)
                {
                    XPathException e2 = (XPathException)e.InnerException;
                    e2.SetIsGlobalError(false);
                    throw e2;
                }
                else
                {
                    throw new XPathException(e.Message);
                }
            }

            MapItem resultMap = new HashTrieMap();
            resultMap = deliverer.PopulateResultMap(resultMap);

            if (result != null)
            {
                AtomicValue resultKey = new StringValue(principalResultKey);
                resultMap = resultMap.AddEntry(resultKey, result.Materialize());
            }

            return resultMap;
        }

        private void ProcessParams(MapItem suppliedParams, Dictionary<QName, XdmValue> checkedParams)
        {
            foreach (OutSmart.DAXon.Values.Maps.KeyValuePair entry in suppliedParams.KeyValuePairs())
            {
                if (!(entry.key is QNameValue))
                {
                    throw new XPathException("The names of parameters must be supplied as QNames", "FOXT0002");
                }

                QName paramName = new QName(((QNameValue)entry.key).GetStructuredQName());
                checkedParams[paramName] = XdmValue.Wrap(entry.value);
            }
        }

        private sealed class TransformErrorReporter : IErrorReporter
        {
            private readonly List<IXmlProcessingError> compileErrors;
            private readonly IErrorReporter originalReporter;

            public TransformErrorReporter(List<IXmlProcessingError> compileErrors, IErrorReporter originalReporter)
            {
                this.compileErrors = compileErrors;
                this.originalReporter = originalReporter;
            }

            public void Report(IXmlProcessingError error)
            {
                if (!error.IsWarning())
                {
                    compileErrors.Add(error);
                }

                originalReporter?.Report(error);
            }
        }

        // ---------- delivery formats ----------

        private abstract class Deliverer : IResultDocumentResolver
        {
            protected Xslt30Transformer transformer;
            protected string baseOutputUri;
            protected string principalResultKey;
            protected IFunctionItem postProcessor;
            protected IXPathContext context;

            public abstract ISequence PrimaryResult { get; }

            public static Deliverer MakeDeliverer(Processor processor, string deliveryFormat)
            {
                switch (deliveryFormat)
                {
                    case "document":
                        return new DocumentDeliverer();
                    case "serialized":
                        return new SerializedDeliverer(processor);
                    case "raw":
                        return new RawDeliverer();
                    default:
                        throw new ArgumentException("delivery-format");
                }
            }

            public void SetTransformer(Xslt30Transformer transformer)
            {
                this.transformer = transformer;
            }

            public void SetPrincipalResultKey(string key)
            {
                this.principalResultKey = key;
            }

            public void SetBaseOutputUri(string uri)
            {
                this.baseOutputUri = uri;
            }

            public void SetPostProcessor(IFunctionItem postProcessor, IXPathContext context)
            {
                this.postProcessor = postProcessor;
                this.context = context;
            }

            protected URI GetAbsoluteUri(string href, string baseUri)
            {
                try
                {
                    return ResolveURI.MakeAbsolute(href, baseUri);
                }
                catch (Exception e)
                {
                    throw new XPathException(e.Message);
                }
            }

            public abstract MapItem PopulateResultMap(MapItem resultMap);

            public abstract IDestination GetPrimaryDestination(MapItem serializationParamsMap);

            public abstract IReceiver Resolve(IXPathContext context, string href, string baseUri, SerializationProperties properties);

            protected Serializer MakeSerializer(Processor processor, MapItem serializationParamsMap)
            {
                Serializer serializer = processor.NewSerializer();
                if (serializationParamsMap != null)
                {
                    foreach (OutSmart.DAXon.Values.Maps.KeyValuePair entry in serializationParamsMap.KeyValuePairs())
                    {
                        AtomicValue param = entry.key;
                        QName paramName;
                        if (param is QNameValue)
                        {
                            paramName = new QName(((QNameValue)param).GetStructuredQName());
                        }
                        else if (param is StringValue)
                        {
                            paramName = new QName(param.GetStringValue());
                        }
                        else
                        {
                            throw new XPathException("Serialization parameters must be strings or QNames", "XPTY0004");
                        }

                        string paramValue = null;
                        IGroundedValue supplied = entry.value;
                        if (supplied.GetLength() > 0)
                        {
                            if (supplied.GetLength() == 1)
                            {
                                IItem val = supplied.ItemAt(0);
                                if (val is StringValue)
                                {
                                    paramValue = val.GetStringValue();
                                }
                                else if (val is BooleanValue)
                                {
                                    paramValue = ((BooleanValue)val).GetBooleanValue() ? "yes" : "no";
                                }
                                else if (val is DecimalValue)
                                {
                                    paramValue = val.GetStringValue();
                                }
                                else if (val is QNameValue)
                                {
                                    paramValue = ((QNameValue)val).GetStructuredQName().EQName;
                                }
                                else if (val is MapItem && paramName.ClarkName.Equals(DAXonOutputKeys.USE_CHARACTER_MAPS))
                                {
                                    CharacterMap charMap = Serialize.ToCharacterMap((MapItem)val);
                                    CharacterMapIndex charMapIndex = new CharacterMapIndex();
                                    charMapIndex.PutCharacterMap(charMap.Name, charMap);
                                    serializer.SetCharacterMap(charMapIndex);
                                    string existingCm = serializer.GetOutputProperty(paramName);
                                    serializer.SetOutputProperty(paramName,
                                        existingCm == null ? charMap.Name.EQName : existingCm + " " + charMap.Name.EQName);
                                    continue;
                                }
                            }

                            if (paramValue == null)
                            {
                                // if more than one, the only possibility is a sequence of QNames
                                var iter = supplied.Iterate();
                                IItem it;
                                paramValue = "";
                                while ((it = iter.Next()) != null)
                                {
                                    if (it is QNameValue)
                                    {
                                        paramValue += " " + ((QNameValue)it).GetStructuredQName().EQName;
                                    }
                                    else
                                    {
                                        throw new XPathException("Value of serialization parameter " + paramName.EQName + " not recognized", "XPTY0004");
                                    }
                                }
                            }

                            if (paramName.ClarkName.Equals("cdata-section-elements")
                                || paramName.ClarkName.Equals(DAXonOutputKeys.SUPPRESS_INDENTATION))
                            {
                                string existing = serializer.GetOutputProperty(paramName);
                                serializer.SetOutputProperty(paramName, existing == null ? paramValue : existing + paramValue);
                            }
                            else
                            {
                                serializer.SetOutputProperty(paramName, paramValue);
                            }
                        }
                    }
                }

                return serializer;
            }

            public IGroundedValue PostProcess(string uri, ISequence result)
            {
                if (postProcessor != null)
                {
                    result = postProcessor.Call(context.NewCleanContext(), new ISequence[] { new StringValue(uri), result });
                }

                return result.Materialize();
            }
        }

        private sealed class DocumentDeliverer : Deliverer
        {
            private readonly Dictionary<string, IGroundedValue> results = new Dictionary<string, IGroundedValue>();
            private readonly XdmDestination destination = new XdmDestination();

            public override ISequence PrimaryResult
            {
                get
                {
                    XdmNode node = destination.builder == null ? null : destination.GetXdmNode();
                    return node == null ? null : (ISequence)PostProcess(baseOutputUri, (NodeInfo)node.UnderlyingValue);
                }
            }

            public override IDestination GetPrimaryDestination(MapItem serializationParamsMap)
            {
                return destination;
            }

            public override IReceiver Resolve(IXPathContext context, string href, string baseUri, SerializationProperties properties)
            {
                URI absolute = GetAbsoluteUri(href, baseUri);
                XdmDestination dest = new XdmDestination();
                dest.DestinationBaseURI = absolute;
                dest.OnClose(() =>
                {
                    XdmNode root = dest.GetXdmNode();
                    IGroundedValue res = PostProcess(absolute.ToASCIIString(), root.UnderlyingValue);
                    lock (results)
                    {
                        results[absolute.ToASCIIString()] = res;
                    }
                });
                PipelineConfiguration pipe = context.GetController().MakePipelineConfiguration();
                return dest.GetReceiver(pipe, properties);
            }

            public override MapItem PopulateResultMap(MapItem resultMap)
            {
                foreach (var entry in results)
                {
                    resultMap = resultMap.AddEntry(new StringValue(entry.Key), entry.Value);
                }

                return resultMap;
            }
        }

        private sealed class SerializedDeliverer : Deliverer
        {
            private readonly Processor processor;
            private readonly Dictionary<string, IGroundedValue> results = new Dictionary<string, IGroundedValue>();
            private StringWriter primaryWriter;

            public override ISequence PrimaryResult
            {
                get
                {
                    string str = primaryWriter.ToString();
                    if (str.Length == 0)
                    {
                        return null;
                    }

                    return PostProcess(baseOutputUri, new StringValue(str));
                }
            }

            public SerializedDeliverer(Processor processor)
            {
                this.processor = processor;
            }

            public override IDestination GetPrimaryDestination(MapItem serializationParamsMap)
            {
                Serializer serializer = MakeSerializer(processor, serializationParamsMap);
                primaryWriter = new StringWriter();
                serializer.SetOutputWriter(primaryWriter);
                return serializer;
            }

            public override IReceiver Resolve(IXPathContext context, string href, string baseUri, SerializationProperties properties)
            {
                URI absolute = GetAbsoluteUri(href, baseUri);
                if (DummyBaseOutputUriScheme.Equals(absolute.Scheme))
                {
                    throw new XPathException("The location of output documents is undefined: use the transform option base-output-uri", "FOXT0002");
                }

                StringWriter writer = new StringWriter();
                Serializer serializer = MakeSerializer(processor, null);
                serializer.SetCharacterMap(properties.GetCharacterMapIndex());
                serializer.SetOutputWriter(writer);
                serializer.OnClose(() =>
                {
                    IGroundedValue res = PostProcess(absolute.ToASCIIString(), new StringValue(writer.ToString()));
                    lock (results)
                    {
                        results[absolute.ToASCIIString()] = res;
                    }
                });
                PipelineConfiguration pipe = context.GetController().MakePipelineConfiguration();
                IReceiver @out = serializer.GetReceiver(pipe, properties);
                @out.SetSystemId(absolute.ToASCIIString());
                return @out;
            }

            public override MapItem PopulateResultMap(MapItem resultMap)
            {
                foreach (var entry in results)
                {
                    resultMap = resultMap.AddEntry(new StringValue(entry.Key), entry.Value);
                }

                return resultMap;
            }
        }

        private sealed class RawDeliverer : Deliverer
        {
            private readonly Dictionary<string, IGroundedValue> results = new Dictionary<string, IGroundedValue>();
            private readonly RawDestination primaryDestination = new RawDestination();

            public override ISequence PrimaryResult
            {
                get
                {
                    ISequence actualResult = (ISequence)primaryDestination.GetXdmValue().UnderlyingValue;
                    return PostProcess(baseOutputUri, actualResult);
                }
            }

            public override IDestination GetPrimaryDestination(MapItem serializationParamsMap)
            {
                return primaryDestination;
            }

            public override IReceiver Resolve(IXPathContext context, string href, string baseUri, SerializationProperties properties)
            {
                URI absolute = GetAbsoluteUri(href, baseUri);
                RawDestination dest = new RawDestination();
                dest.OnClose(() =>
                {
                    dest.Close();   // upstream closes the destination before reading its value
                    IGroundedValue res = PostProcess(absolute.ToASCIIString(), (ISequence)dest.GetXdmValue().UnderlyingValue);
                    lock (results)
                    {
                        results[absolute.ToASCIIString()] = res;
                    }
                });
                PipelineConfiguration pipe = context.GetController().MakePipelineConfiguration();
                return dest.GetReceiver(pipe, properties);
            }

            public override MapItem PopulateResultMap(MapItem resultMap)
            {
                foreach (var entry in results)
                {
                    resultMap = resultMap.AddEntry(new StringValue(entry.Key), entry.Value);
                }

                return resultMap;
            }
        }
    }
}
