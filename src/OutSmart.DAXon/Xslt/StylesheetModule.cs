////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Packages;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Xslt
{
    public class StylesheetModule
    {
        // Stack reserved per already-open module beyond the ~2 KB its descent takes. No level of a failing include
        // enters a handler (the XTSE0210 re-code is a filter), so the unwind of an error costs nothing per level:
        // 100 nested modules report an error cleanly on a 256 KB thread with no reserve. This keeps ~2.5x of that:
        // ~35 modules on 256 KB, ~250 on 1 MB.
        private const ulong UnwindReservePerModule = 2 * 1024;

        private readonly StyleElement rootElement;
        private int precedence;
        private int minImportPrecedence;
        private StylesheetModule importer;
        bool wasIncluded;
        // the value of the inputTypeAnnotations attribute on this module, combined with the values
        // on all imported/included modules. This is a combination of the bit-significant values
        // ANNOTATION_STRIP and ANNOTATION_PRESERVE.
        private int inputTypeAnnotations;
        // A list of all the declarations in the stylesheet and its descendants, in increasing precedence order
        protected IList<ComponentDeclaration> topLevel = new List<ComponentDeclaration>();

        public virtual StylesheetModule Importer
        {
            get => importer; set
            {
                this.importer = value;
            }
        }

        public virtual StyleElement RootElement => rootElement;

        public virtual XSLModuleRoot StylesheetElement => (XSLModuleRoot)rootElement;

        public virtual int Precedence => wasIncluded ? importer.Precedence : precedence;

        public virtual int MinImportPrecedence
        {
            get => this.minImportPrecedence; set
            {
                this.minImportPrecedence = value;
            }
        }

        public virtual int InputTypeAnnotations
        {
            get => inputTypeAnnotations; set
            {
                inputTypeAnnotations |= value;
                if (inputTypeAnnotations == (XSLModuleRoot.ANNOTATION_STRIP | XSLModuleRoot.ANNOTATION_PRESERVE))
                {
                    GetPrincipalStylesheetModule().CompileError("One stylesheet module specifies input-type-annotations='strip', " + "another specifies input-type-annotations='preserve'", "XTSE0265");
                }

                if (value == XSLModuleRoot.ANNOTATION_STRIP)
                {
                    GetPrincipalStylesheetModule().GetStylesheetPackage().SetStripsTypeAnnotations(true);
                }
            }
        }
        public StylesheetModule(StyleElement rootElement, int precedence)
        {
            this.rootElement = rootElement;
            this.precedence = precedence;
        }

        public static DocumentImpl LoadStylesheetModule(ResolvedResource styleSource, bool topLevelModule, Compilation compilation, NestedIntegerValue precedence)
        {
            string systemId = styleSource.SystemId;
            DocumentKey docURI = systemId == null ? null : new DocumentKey(systemId);
            // The resource is closed by the parse: refused before it, it is closed here (a cycle left the module's file
            // open after the failed compile).
            if (systemId != null && compilation.ImportStack.Contains(docURI))
            {
                ParseOptions.Dispose(styleSource);
                throw new XPathException("The stylesheet module includes/imports itself directly or indirectly", "XTSE0180");
            }

            // Every nested xsl:include/xsl:import is loaded from here, and each one recurses -
            // parsing a module runs UseWhenFilter, which loads that module's own includes. The
            // cycle check above only catches a REPEATED uri, so a chain that hands out a fresh uri
            // per level recurses without bound and overflows the uncatchable .NET stack while
            // compiling (~900 levels on a 1 MB worker thread, round AW).
            // The reserve scales with depth: a level that catches and re-codes the error re-enters
            // exception dispatch, so its stack grows on the unwind. None does now (UseWhenFilter's
            // filter), but a cycle error once killed a 256 KB thread 20 includes deep.
            try
            {
                StackGuard.Probe(UnwindReservePerModule * (ulong)compilation.ImportStack.Count);
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                ParseOptions.Dispose(styleSource);
                throw e.Describe("Stylesheet include/import nesting is too deep (insufficient stack on this thread)", "XTSE0010", null);
            }

            compilation.ImportStack.Push(docURI);
            Configuration config = compilation.GetConfiguration();
            PipelineConfiguration pipe = config.MakePipelineConfiguration();
            pipe.SetErrorReporter(compilation.GetCompilerInfo().ErrorReporter);
            LinkedTreeBuilder styleBuilder = new LinkedTreeBuilder(pipe, Durability.LASTING);
            styleBuilder.SetSystemId(styleSource.SystemId);

            styleBuilder.SetNodeFactory(compilation.GetStyleNodeFactory(topLevelModule));
            styleBuilder.SetLineNumbering(true);
            UseWhenFilter useWhenFilter = new UseWhenFilter(compilation, styleBuilder, precedence);
            useWhenFilter.SetSystemId(styleSource.SystemId);
            StylesheetSpaceStrippingRule rule = new StylesheetSpaceStrippingRule(config.GetNamePool());
            Stripper styleStripper = new Stripper(rule, useWhenFilter);
            CommentStripper commentStripper = new CommentStripper(styleStripper);
            if (compilation.GetCompilerInfo().XsltVersion == 40)
            {
                NamePool pool = config.GetNamePool();
                commentStripper.SetSkippedElementTest((name) => name.ObtainFingerprint(pool) == StandardNames.XSL_NOTE);
            }


            // build the stylesheet document
            DocumentImpl doc;
            ParseOptions options = MakeStylesheetParseOptions(pipe);
            try
            {
                // Direct XmlReaderToReceiver pump (ActiveStreamSource); no SAX XMLReader fabricated.
                Sender.Send(styleSource, commentStripper, options);
                doc = (DocumentImpl)styleBuilder.CurrentRoot;
                styleBuilder.Reset();
                compilation.ImportStack.Pop();
                return doc;
            }
            // A FILTER, not a plain catch: for a nested module (or an already-reported error) the
            // body would do nothing, and an unentered filter costs no funclet and no re-dispatch -
            // which is what sets the include-nesting ceiling, since the unwind is what runs out of
            // stack, not the descent (AW).
            catch (XPathException err) when (topLevelModule && !err.HasBeenReported())
            {
                // bug 2244
                compilation.ReportError(err);
                throw;
            }
            finally
            {
                if (options.IsPleaseCloseAfterUse())
                {
                    ParseOptions.Dispose(styleSource);
                }
            }
        }

        private static ParseOptions MakeStylesheetParseOptions(PipelineConfiguration pipe)
        {
            ParseOptions options = new ParseOptions();
            options = options.WithSchemaValidationMode(Validation.STRIP).WithDTDValidationMode(Validation.STRIP).WithLineNumbering(true).WithSpaceStrippingRule(NoElementsSpaceStrippingRule.GetInstance()).WithErrorReporter(pipe.GetErrorReporter());
            return options;
        }

        public static PreparedStylesheet LoadStylesheet(ResolvedResource styleSource, Compilation compilation)
        {
            return LoadStylesheet(styleSource.SystemId, compilation, (pipeline, options) =>
            {
                try
                {
                    // The same direct XmlReaderToReceiver pump as source documents (ActiveStreamSource).
                    Sender.Send(styleSource, pipeline, options);
                }
                finally
                {
                    if (options.IsPleaseCloseAfterUse())
                    {
                        ParseOptions.Dispose(styleSource);
                    }
                }
            });
        }

        // A stylesheet read from a System.Xml.XmlReader: external entities resolve through the reader's resolver.
        public static PreparedStylesheet LoadStylesheet(System.Xml.XmlReader reader, string systemId, Compilation compilation)
        {
            return LoadStylesheet(systemId, compilation, (pipeline, options) => Sender.Send(reader, systemId, pipeline, options));
        }

        // The two entry points differ only in how the text reaches the pipeline: the use-when filter, the stylesheet
        // stripper, the comment stripper, and the Valve that diverts a precompiled package.
        private static PreparedStylesheet LoadStylesheet(string systemId, Compilation compilation, System.Action<IReceiver, ParseOptions> send)
        {
            DocumentKey docURI = systemId == null ? null : new DocumentKey(systemId);
            if (systemId != null && compilation.ImportStack.Contains(docURI))
            {
                throw new XPathException("The stylesheet module includes/imports itself directly or indirectly", "XTSE0180");
            }

            compilation.ImportStack.Push(docURI);
            compilation.SetMinimalPackageData();
            Configuration config = compilation.GetConfiguration();
            PipelineConfiguration pipe = config.MakePipelineConfiguration();
            pipe.SetErrorReporter(compilation.GetCompilerInfo().ErrorReporter);
            LinkedTreeBuilder styleBuilder = new LinkedTreeBuilder(pipe, Durability.LASTING);
            styleBuilder.SetSystemId(systemId);
            styleBuilder.SetNodeFactory(compilation.GetStyleNodeFactory(true));
            styleBuilder.SetLineNumbering(true);

            // Pipeline for source XSLT code
            UseWhenFilter useWhenFilter = new UseWhenFilter(compilation, styleBuilder, NestedIntegerValue.TWO);
            useWhenFilter.SetSystemId(systemId);
            StylesheetSpaceStrippingRule rule = new StylesheetSpaceStrippingRule(config.GetNamePool());
            Stripper styleStripper = new Stripper(rule, useWhenFilter);
            CommentStripper commentStripper = new CommentStripper(styleStripper);
            if (compilation.GetCompilerInfo().XsltVersion == 40)
            {
                NamePool pool = config.GetNamePool();
                commentStripper.SetSkippedElementTest((name) => name.ObtainFingerprint(pool) == StandardNames.XSL_NOTE);
            }

            // Pipeline for compiled XSLT code
            TinyBuilder packageBuilder = new TinyBuilder(pipe);
            packageBuilder.SetSystemId(systemId);
            CheckSumFilter checksummer = new CheckSumFilter(packageBuilder);
            checksummer.SetCheckExistingChecksum(true);
            Valve valve = new Valve(NamespaceUri.SAXON_XSLT_EXPORT, commentStripper, checksummer);

            try
            {
                send(valve, MakeStylesheetParseOptions(pipe));
                if (valve.WasDiverted())
                {
                    // Implies we have loaded a pre-compiled package
                    if (!checksummer.IsChecksumCorrect())
                    {
                        throw new XPathException("Compiled package cannot be loaded: incorrect checksum");
                    }

                    IIPackageLoader loader = config.MakePackageLoader();
                    if (loader == null)
                    {
                        throw new XPathException("A compiled package (SEF) cannot be loaded by this edition: compile the stylesheet from source");
                    }

                    StylesheetPackage pack = loader.LoadPackageDoc(packageBuilder.CurrentRoot);
                    compilation.SetPackageData(pack);
                    PreparedStylesheet pss = new PreparedStylesheet(compilation);
                    pack.CheckForAbstractComponents();
                    pack.UpdatePreparedStylesheet(pss);
                    return pss;
                }
                else
                {
                    // We loaded source XSLT (could be xsl:package or xsl:stylesheet or an LRE...
                    NodeInfo doc = styleBuilder.CurrentRoot;
                    styleBuilder.Reset();
                    compilation.ImportStack.Pop();
                    PreparedStylesheet pss = new PreparedStylesheet(compilation);
                    PrincipalStylesheetModule psm = compilation.CompilePackage(new ResolvedResource { Node = doc });
                    if (compilation.ErrorCount > 0)
                    {
                        throw compilation.MakeCompilationFailure();
                    }

                    psm.GetStylesheetPackage().CheckForAbstractComponents();
                    psm.GetStylesheetPackage().UpdatePreparedStylesheet(pss);
                    pss.AddPackage(compilation.GetPackageData());
                    return pss;
                }
            }
            catch (XPathException err)
            {
                if (!err.HasBeenReported())
                {
                    // bug 2244
                    compilation.ReportError(err);
                }

                throw;
            }
        }

        public static ResolvedResource GetAssociatedStylesheet(Configuration config, IResourceResolver resolver, ResolvedResource source, string media, string title, string charset)
        {
            PIGrabber grabber = new PIGrabber(new Sink(config.MakePipelineConfiguration()));
            grabber.SetFactory(config);
            grabber.SetCriteria(media, title);
            grabber.SetBaseURI(source.SystemId);
            grabber.SetResourceResolver(resolver);
            try
            {
                Sender.Send(source, grabber, null); // this parse will be aborted when the first start tag is found
            }
            catch (XPathException err)
            {
                if (grabber.IsTerminated())
                {
                }
                else
                {
                    // With the cause's code and message: an input too large (FODC0002), a limit or a parse error said
                    // only that the search failed.
                    throw new XPathException("Failed while looking for xml-stylesheet PI: " + err.Message, err) { ErrorCodeQName = err.ErrorCodeQName };
                }
            }

            ResolvedResource[] sources = (ResolvedResource[])grabber.AssociatedStylesheets;
            if (sources == null)
            {
                throw new XPathException("No matching <?xml-stylesheet?> processing instruction found");
            }

            return CompositeStylesheet(source.SystemId, sources);
        }

        private static ResolvedResource CompositeStylesheet(string baseURI, ResolvedResource[] sources)
        {
            if (sources.Length == 1)
            {
                return sources[0];
            }
            else if (sources.Length == 0)
            {
                throw new XPathException("No stylesheets were supplied");
            }


            // create a new top-level stylesheet that imports all the others
            StringBuilder sb = new StringBuilder(250);
            sb.Append("<xsl:stylesheet version='1.0' ");
            sb.Append(" xmlns:xsl='" + NamespaceConstant.XSLT + "'>");
            foreach (ResolvedResource source in sources)
            {
                sb.Append("<xsl:import href='").Append(source.SystemId).Append("'/>");
            }

            sb.Append("</xsl:stylesheet>");
            return new ResolvedResource { TextReader = new StringReader(sb.ToString()), SystemId = baseURI };
        }

        public virtual PrincipalStylesheetModule GetPrincipalStylesheetModule()
        {
            return importer.GetPrincipalStylesheetModule();
        }

        public virtual Configuration GetConfiguration()
        {
            return rootElement.GetConfiguration();
        }

        public virtual void SetWasIncluded()
        {
            wasIncluded = true;
        }

        public virtual void SpliceIncludes()
        {
            if (topLevel == null || topLevel.Count == 0)
            {
                topLevel = new List<ComponentDeclaration>(50);
            }

            minImportPrecedence = precedence;
            StyleElement previousElement = rootElement;
            foreach (NodeInfo child in StylesheetElement.Children())
            {
                if (child.GetNodeKind() == Types.Type.TEXT)
                {

                    // in an embedded stylesheet, white space nodes may still be there
                    if (!Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {
                        previousElement.CompileError("No character data is allowed between top-level elements", "XTSE0120");
                    }
                }
                else if (child is DataElement)
                {
                    if (((DataElement)child).GetNodeName().GetNamespaceUri().IsEmpty())
                    {
                        Loc loc = new Loc(child);
                        previousElement.CompileError("Top-level elements must be in a namespace: " + ((DataElement)child).GetNodeName().GetLocalPart() + " is not", "XTSE0130", loc);
                    }
                }
                else
                {
                    previousElement = (StyleElement)child;
                    if (child is XSLGeneralIncorporate)
                    {
                        XSLGeneralIncorporate xslinc = (XSLGeneralIncorporate)child;
                        xslinc.ProcessAttributes();

                        // get the included stylesheet. This follows the URL, builds a tree, and splices
                        // in any indirectly-included stylesheets.
                        xslinc.ValidateInstruction();
                        int errors = ((XSLGeneralIncorporate)child).GetCompilation().ErrorCount;
                        StylesheetModule inc = xslinc.GetIncludedStylesheet(this, precedence);
                        if (inc == null)
                        {
                            return; // error has been reported
                        }

                        errors = ((XSLGeneralIncorporate)child).GetCompilation().ErrorCount - errors;
                        if (errors > 0)
                        {
                            xslinc.CompileError("Reported " + errors + (errors == 1 ? " error" : " errors") + " in " + (xslinc.IsImport() ? "imported" : "included") + " stylesheet module", "XTSE0165");
                        }


                        // after processing the imported stylesheet and any others it brought in,
                        // adjust the import precedence of this stylesheet if necessary
                        if (xslinc.IsImport())
                        {
                            precedence = inc.Precedence + 1;
                        }
                        else
                        {
                            precedence = inc.Precedence;
                            inc.MinImportPrecedence = minImportPrecedence;
                            inc.SetWasIncluded();
                        }


                        // copy the top-level elements of the included stylesheet into the top level of this
                        // stylesheet. Normally we add these elements at the end, in order, but if the precedence
                        // of an element is less than the precedence of the previous element, we promote it.
                        // This implements the requirement in the spec that when xsl:include is used to
                        // include a stylesheet, any xsl:import elements in the included document are moved
                        // up in the including document to after any xsl:import elements in the including
                        // document.
                        IList<ComponentDeclaration> incchildren = inc.topLevel;
                        foreach (ComponentDeclaration decl in incchildren)
                        {
                            int last = topLevel.Count - 1;
                            if (last < 0 || decl.Precedence >= topLevel[last].Precedence)
                            {
                                topLevel.Add(decl);
                            }
                            else
                            {
                                while (last >= 0 && decl.Precedence < topLevel[last].Precedence)
                                {
                                    last--;
                                }

                                topLevel.Insert(last + 1,decl);
                            }
                        }
                    }
                    else
                    {
                        ComponentDeclaration decl = new ComponentDeclaration(this, (StyleElement)child);
                        topLevel.Add(decl);
                    }
                }
            }
        }
    }
}