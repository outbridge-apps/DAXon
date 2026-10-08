////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using System;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the XSLT document() function
    /// </summary>
    internal sealed class DocumentFn : SystemFunction, ICallable
    {
        private ILocation location;
        public override int GetCardinality(Expression[] arguments)
        {
            Expression expression = arguments[0];
            if (Cardinality.AllowsMany(expression.GetCardinality()))
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
            else
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            } // may have to revise this if the argument can be a list-valued element or attribute
        }

        public override int GetSpecialProperties(Expression[] arguments)
        {
            return StaticProperty.ORDERED_NODESET | StaticProperty.PEER_NODESET | StaticProperty.NO_NODES_NEWLY_CREATED; // Declaring it as a peer node-set expression avoids sorting of expressions such as
            // document(XXX)/a/b/c
            // The document() function might appear to be creative: but it isn't, because multiple calls
            // with the same arguments will produce identical results.
        }

        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            location = arguments[0].GetLocation();
            Expression expr = Doc.MaybePreEvaluate(this, arguments);
            return expr == null ? base.MakeFunctionCall(arguments) : expr;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            int numArgs = GetArity();
            ISequenceIterator hrefSequence = arguments[0].Iterate();
            string baseURI = null;
            if (numArgs == 2)
            {

                // we can trust the type checking: it must be a node
                NodeInfo @base = (NodeInfo)arguments[1].Head();
                baseURI = @base.GetBaseURI();
                if (baseURI == null)
                {
                    throw new XPathException("The second argument to document() is a node with no base URI", "XTDE1162");
                }
            }

            DocumentMappingFunction map = new DocumentMappingFunction(context);
            map.baseURI = baseURI;
            map.stylesheetURI = StaticBaseUriString;
            map.packageData = GetRetainedStaticContext().GetPackageData();
            map.locator = location;
            ItemMappingIterator iter = new ItemMappingIterator(hrefSequence, map);

            return SequenceTool.ToLazySequence(new DocumentOrderIterator(iter, GlobalOrderComparer.GetInstance())); // this is to make sure we eliminate duplicates: two href's might be the same
        }

        public static NodeInfo MakeDoc(string href, string baseURI, PackageData packageData, ParseOptions options, IXPathContext c, ILocation locator, bool silent)
        {
            Configuration config = c.GetConfiguration();

            // If the href contains a fragment identifier, strip it out now
            string[] parts = ExtractFragment(href);
            string fragmentId = parts[1];
            href = parts[0];

            // Extract any query part
            URIQueryParameters @params = null;
            if (config.GetBooleanProperty(Feature<bool>.RECOGNIZE_URI_QUERY_PARAMETERS))
            {
                int qMark = href.IndexOf('?');
                if (qMark >= 0)
                {
                    @params = new URIQueryParameters(href.Substring(qMark + 1), config);
                    href = href.Substring(0, qMark);
                }
            }

            Controller controller = c.GetController();
            if (controller == null)
            {
                throw new XPathException("doc() function is not available in this environment");
            }


            // java.net.URI rejects these during resolution (Java maps that to FODC0005); the port's lenient
            // URI lets them reach the fetch layer, which can crash outside our catches (e.g. ':/' throws
            // NotSupportedException inside System.Xml.XmlDownloadManager) — so screen the href up front.
            if (!OutSmart.DAXon.Functions.ResolveURI.IsValidUriSyntax(href) || href.IndexOfAny(new[] { '<', '>', '"', '|' }) >= 0)
            {
                throw new XPathException("Invalid URI passed to doc(): " + href).WithErrorCode("FODC0005").WithXPathContext(c).WithLocation(locator);
            }

            // Resolve relative URI
            DocumentKey documentKey = ComputeDocumentKey(href, baseURI, packageData, c);

            // see if the document is already loaded
            ITreeInfo doc = config.GlobalDocumentPool.Find(documentKey);
            if (doc != null)
            {
                return doc.GetRootNode();
            }

            DocumentPool pool = controller.GetDocumentPool();

            lock (controller.syncLock)
            {
                doc = pool.Find(documentKey);
                if (doc != null)
                {
                    return GetFragment(doc, fragmentId, c, locator);
                }


                // check that the document was not written by this transformation
                if (controller is XsltController && !((XsltController)controller).CheckUniqueOutputDestination(documentKey))
                {
                    pool.MarkUnavailable(documentKey);
                    throw new XPathException("Cannot read a document that was written during the same transformation: " + documentKey).WithXPathContext(c).WithErrorCode("XTRE1500").WithLocation(locator);
                }

                if (pool.IsMarkedUnavailable(documentKey))
                {
                    throw new XPathException("Document has been marked not available: " + documentKey).WithXPathContext(c).WithErrorCode("FODC0002").WithLocation(locator);
                }
            }

            try
            {

                // Get a resolved resource from the resolver chain
                ResolvedResource source = ResolveURI(href, baseURI, documentKey.ToString(), c);
                if (source == null || source.IsEmpty)
                {
                    return null;
                }

                ITreeInfo newdoc;
                if (source.Node != null)
                {
                    NodeInfo startNode = controller.PrepareInputTree(source.Node.AsActiveSource());
                    newdoc = startNode.GetTreeInfo();
                }
                else
                {
                    Builder b = controller.MakeBuilder();
                    b.SetDurability(Durability.LASTING);
                    b.SetUseEventLocation(true);
                    if (b is TinyBuilder)
                    {
                        ((TinyBuilder)b).SetStatistics(config.GetTreeStatistics().SOURCE_DOCUMENT_STATISTICS);
                    }

                    IReceiver s = b;
                    if (options == null)
                    {
                        options = b.GetPipelineConfiguration().GetParseOptions();
                        if (packageData is StylesheetPackage)
                        {
                            ISpaceStrippingRule rule = ((StylesheetPackage)packageData).SpaceStrippingRule;
                            if (rule != NoElementsSpaceStrippingRule.GetInstance())
                            {
                                options = options.WithSpaceStrippingRule(rule);
                            }
                        }

                        options = options.WithSchemaValidationMode(controller.SchemaValidationMode);
                    }

                    if (@params != null)
                    {
                        options = options.Merge(@params.MakeParseOptions(config));
                    }

                    b.GetPipelineConfiguration().SetParseOptions(options);
                    if (options.IsLineNumbering())
                    {
                        b.SetLineNumbering(true);
                    }

                    if (packageData is StylesheetPackage && ((StylesheetPackage)packageData).IsStripsTypeAnnotations())
                    {
                        s = config.GetAnnotationStripper(s);
                    }

                    PathMap map = controller.PathMapForDocumentProjection;
                    if (map != null)
                    {
                        PathMap.PathMapRoot pathRoot = map.GetRootForDocument(documentKey.ToString());
                        if (pathRoot != null && !pathRoot.IsReturnable() && !pathRoot.HasUnknownDependencies())
                        {
                            options = options.WithFilter(config.MakeDocumentProjector(pathRoot));
                        }
                    }

                    s.SetPipelineConfiguration(b.GetPipelineConfiguration());
                    try
                    {
                        Sender.Send(source, s, options);
                        newdoc = b.CurrentRoot.GetTreeInfo();
                        b.Reset();
                    }
                    catch (XPathException err)
                    {
                        if (err.ErrorCodeQName == null || err.HasErrorCode(DAXonErrorCode.SXXP0003))
                        {
                            err.SetErrorCode("FODC0002");
                        }

                        throw err.MaybeWithLocation(locator).MaybeWithContext(c);
                    }
                    catch (System.Xml.XmlException xe)
                    {
                        // .NET's XmlReader throws a raw XmlException on a malformed source document; upstream's
                        // SAX parser surfaced this as a wrapped error that fn:doc reports as FODC0002. Map it the
                        // same way (previously it escaped as a code-less internal error, so doc() of an invalid
                        // document / doc-available() of a non-XML file failed instead of raising FODC0002 / false).
                        XPathException err = new XPathException("Error reported by XML parser: " + xe.Message, "FODC0002");
                        throw err.MaybeWithLocation(locator).MaybeWithContext(c);
                    }
                    finally
                    {
                        if (options.IsPleaseCloseAfterUse())
                        {
                            ParseOptions.Dispose(source);
                        }
                    }
                }


                // At this point, we have built the document. But it's possible that another thread
                // has built the same document and put it in the document pool. So we do another
                // check on the document pool, and if this has happened, we discard the document
                // we have just built and use the one from the pool instead.
                lock (controller.syncLock)
                {
                    doc = pool.Find(documentKey);
                    if (doc != null)
                    {
                        return GetFragment(doc, fragmentId, c, locator);
                    }

                    controller.RegisterDocument(newdoc, documentKey);
                    if (controller is XsltController)
                    {
                        ((XsltController)controller).AddUnavailableOutputDestination(documentKey);
                    }
                }

                return GetFragment(newdoc, fragmentId, c, locator);
            }
            catch (XPathException err)
            {
                pool.MarkUnavailable(documentKey);
                string code = (err.InnerException is URISyntaxException) ? "FODC0005" : "FODC0002";
                XPathException xerr = XPathException.MakeXPathException(err);
                throw xerr.MaybeWithLocation(locator).MaybeWithErrorCode(code);
            }
        }

        private static String[] ExtractFragment(string href)
        {

            // If the href contains a fragment identifier, strip it out now
            int hash = href.IndexOf('#');
            string fragmentId = null;
            if (hash >= 0)
            {
                if (hash == href.Length - 1)
                {

                    // If there's a # sign at end - just ignore it
                    href = href.Substring(0, hash);
                }
                else
                {
                    fragmentId = href.Substring(hash + 1);
                    href = href.Substring(0, hash);
                    if (!NameChecker.IsValidNCName(fragmentId))
                    {
                        throw new XPathException("The fragment identifier " + Err.Wrap(fragmentId) + " is not a valid NCName", "XTDE1160");
                    }
                }
            }

            return new string[]
            {
                href,
                fragmentId
            };
        }

        public static ResolvedResource ResolveURI(string href, string baseURI, string documentKey, IXPathContext context)
        {
            Configuration config = context.GetConfiguration();
            IResourceResolver resolver = context.GetResourceResolver();
            if (href.Contains(" "))
            {
                href = Functions.ResolveURI.EscapeSpaces(href);
            }

            if (baseURI == null)
            {
                try
                {
                    URI uri = new URI(href);
                    if (!uri.IsAbsolute())
                    {
                        throw new XPathException("Relative URI passed to document() function (" + href + "); but no base URI is available", "XTDE1162");
                    }
                }
                catch (URISyntaxException e)
                {
                    throw new XPathException("Invalid URI passed to document() function: " + href, "FODC0005");
                }
            }

            ResourceRequest request = new ResourceRequest();
            request.relativeUri = href;
            if (baseURI != null)
            {
                request.baseUri = baseURI;
                request.uri = documentKey;
            }
            else
            {
                request.uri = href;
            }

            request.nature = ResourceRequest.XML_NATURE;
            request.purpose = ResourceRequest.ANY_PURPOSE;
            try
            {
                return request.Resolve(resolver, config.GetResourceResolver(), new DirectResourceResolver(config));
            }
            catch (OutSmart.DAXon.Internal.ResourceDeniedException err)
            {
                // keeps FODC0002, the code of a document that cannot be retrieved
                err.MaybeSetContext(context);
                throw;
            }
            catch (XPathException err)
            {
                err.SetErrorCode("FODC0005");
                err.MaybeSetContext(context);
                throw;
            }
            catch (Exception ex)
            {
                XPathException de = new XPathException("Exception thrown by URIResolver resolving `" + href + "` against `" + baseURI + "'", ex);
                // Java validates the URI up front and reports FODC0005 for an invalid one; this port can reach
                // the file-open stage with a bad path (':/' -> NotSupportedException) and previously threw an
                // XPathException with NO error code (surfaced as a bare "ERR"). Classify: a genuinely missing
                // resource is FODC0002; an unusable URI/path is FODC0005 (K2-SeqDocFunc-14, fn-doc-1).
                de.SetErrorCode(ex is System.IO.FileNotFoundException || ex is System.IO.DirectoryNotFoundException ? "FODC0002" : "FODC0005");
                throw de;
            }
        }

        public static DocumentKey ComputeDocumentKey(string href, string baseURI, PackageData packageData, IXPathContext c)
        {
            return ComputeDocumentKey(href, baseURI, packageData, true);
        }

        public static DocumentKey ComputeDocumentKey(string href, string baseURI, PackageData packageData, bool strip)
        {
            string absURI;

            // Saxon takes charge of absolutization, leaving the user URIResolver to handle dereferencing only
            href = Functions.ResolveURI.EscapeSpaces(href);
            if (baseURI == null)
            {

                // no base URI available
                try
                {

                    // the href might be an absolute URL
                    absURI = new URI(href).ToString();
                }
                catch (URISyntaxException err)
                {

                    // it isn't; but the URI resolver might know how to cope
                    absURI = '/' + href;
                }
            }
            else if ((href.Length == 0))
            {

                // common case in XSLT, which OutSmart.DAXon.Internal.Net.URI#resolve() does not handle correctly
                absURI = baseURI;
            }
            else
            {
                try
                {
                    absURI = Functions.ResolveURI.MakeAbsolute(href, baseURI).ToString();
                }
                catch (URISyntaxException err)
                {
                    absURI = baseURI + "/../" + href;
                }
                catch (ArgumentException err)
                {
                    absURI = baseURI + "/../" + href;
                }
                catch (FormatException err)
                {
                    // .NET's Uri throws UriFormatException (a FormatException, NOT ArgumentException) for an
                    // unparseable href like ':/'. Fall back to a synthetic key like the other catches: doc()
                    // then fails to fetch and reports FODC0005, and doc-available() maps that to false —
                    // instead of the raw exception leaking out as an uncaught error (K2-SeqDocFunc-14,
                    // K2-SeqDocAvailableFunc-1/1a).
                    absURI = baseURI + "/../" + href;
                }
            }

            if (strip && packageData is StylesheetPackage && ((StylesheetPackage)packageData).SpaceStrippingRule != NoElementsSpaceStrippingRule.GetInstance())
            {
                string name = ((StylesheetPackage)packageData).PackageName;
                if (name != null)
                {
                    return new DocumentKey(absURI, name, ((StylesheetPackage)packageData).GetPackageVersion());
                }
            }

            return new DocumentKey(absURI);
        }

        public static NodeInfo PreLoadDoc(string href, string baseURI, PackageData packageData, Configuration config, ILocation locator)
        {
            int hash = href.IndexOf('#');
            if (hash >= 0)
            {
                throw new XPathException("Fragment identifier not supported for preloaded documents");
            }


            // Extract any query part
            URIQueryParameters @params = null;
            if (config.GetBooleanProperty(Feature<bool>.RECOGNIZE_URI_QUERY_PARAMETERS))
            {
                int qMark = href.IndexOf('?');
                if (qMark >= 0)
                {
                    @params = new URIQueryParameters(href.Substring(qMark + 1), config);
                    href = href.Substring(0, qMark);
                }
            }

            DocumentKey documentKey = ComputeDocumentKey(href, baseURI, packageData, true);

            // see if the document is already loaded
            ITreeInfo doc = config.GlobalDocumentPool.Find(documentKey);
            if (doc != null)
            {
                return doc.GetRootNode();
            }

            ResourceRequest rr = new ResourceRequest();
            rr.relativeUri = href;
            rr.baseUri = baseURI;
            rr.uri = documentKey.AbsoluteURI;
            ResolvedResource source;
            try
            {

                // Get a resolved resource from the resolver chain
                source = rr.Resolve(config.GetResourceResolver(), new DirectResourceResolver(config));
            }
            catch (Exception ex)
            {
                XPathException de = new XPathException("Exception thrown by IResourceResolver", ex);
                de.SetLocator(locator);
                throw de;
            }

            ParseOptions options = config.GetParseOptions();
            if (@params != null)
            {
                options = options.Merge(@params.MakeParseOptions(config));
            }

            ITreeInfo newdoc = config.BuildDocumentTree(source, options);
            config.GlobalDocumentPool.Add(newdoc, documentKey);
            return newdoc.GetRootNode();
        }

        public static void SendDoc(string href, string baseURI, PackageData packageData, IXPathContext context, ILocation locator, IReceiver @out, ParseOptions parseOptions)
        {
            PipelineConfiguration pipe = @out.GetPipelineConfiguration();
            if (pipe == null)
            {
                pipe = context.GetController().MakePipelineConfiguration();
                pipe.XPathContext = context;
                @out.SetPipelineConfiguration(pipe);
            }

            string[] parts = ExtractFragment(href);
            if (parts[1] != null)
            {
                href = parts[0];
                @out = (IReceiver)(new IDFilter(@out, parts[1]));
            }


            // Resolve relative URI
            DocumentKey documentKey = ComputeDocumentKey(href, baseURI, packageData, true);
            Controller controller = context.GetController();
            Configuration config = controller.GetConfiguration();

            // see if the document is already loaded
            ITreeInfo doc = controller.GetDocumentPool().Find(documentKey);
            ResolvedResource source = null;
            if (doc != null)
            {
                source = new ResolvedResource { Node = doc.GetRootNode() };
            }
            else
            {
                try
                {

                    // Get a resolved resource from the resolver chain
                    ResourceRequest request = new ResourceRequest();
                    request.baseUri = baseURI;
                    request.relativeUri = href;
                    request.uri = documentKey.AbsoluteURI;
                    request.streamable = true;
                    request.nature = ResourceRequest.XML_NATURE;
                    request.purpose = ResourceRequest.ANY_PURPOSE;
                    source = request.Resolve(context.GetResourceResolver(), config.GetResourceResolver(), new DirectResourceResolver(config));
                    if (source == null)
                    {
                        XPathException xerr = new XPathException("Failed to resolve streamed document URI " + request.uri, "FODC0005");
                        xerr.SetLocator(locator);
                        throw xerr;
                    }

                    if (source.Node != null)
                    {
                        NodeInfo startNode = controller.PrepareInputTree(source.Node.AsActiveSource());
                        source = new ResolvedResource { Node = startNode.Root };
                    }
                }
                catch (XPathException err)
                {
                    XPathException xerr = XPathException.MakeXPathException(err);
                    xerr.SetLocator(locator);
                    xerr.MaybeSetErrorCode("FODC0005");
                    throw xerr;
                }
            }

            if (controller.GetConfiguration().IsTiming())
            {
                controller.GetConfiguration().Logger.Info("Streaming input document " + source.SystemId);
            }

            @out.SetPipelineConfiguration(pipe);
            try
            {
                Sender.Send(source, @out, parseOptions);
            }
            catch (XPathException e)
            {
                throw e.MaybeWithLocation(locator).MaybeWithErrorCode("FODC0002").ReplacingErrorCode(DAXonErrorCode.SXXP0003, "FODC0002");
            }
        }

        // Get a Source from the resource resolver
        private static NodeInfo GetFragment(ITreeInfo doc, string fragmentId, IXPathContext context, ILocation locator)
        {

            // TODO: we only support one kind of fragment identifier. The rules say
            // that the interpretation of the fragment identifier depends on media type,
            // but we aren't getting the media type from the URIResolver.
            if (fragmentId == null)
            {
                return doc.GetRootNode();
            }

            if (!NameChecker.IsValidNCName(fragmentId))
            {
                context.GetController().Warning("Invalid fragment identifier in URI", "XTDE1160", locator);
                return doc.GetRootNode();
            }

            return doc.SelectID(fragmentId, false);
        }

        private sealed class DocumentMappingFunction : IItemMappingFunction
        {
            public string baseURI;
            public string stylesheetURI;
            public ILocation locator;
            public PackageData packageData;
            public IXPathContext context;
            public DocumentMappingFunction(IXPathContext context)
            {
                this.context = context;
            }

            public IItem MapItem(IItem item)
            {
                string b = baseURI;
                if (b == null)
                {
                    if (item is NodeInfo)
                    {
                        b = ((NodeInfo)item).GetBaseURI();
                    }
                    else
                    {
                        b = stylesheetURI;
                    }
                }

                try
                {
                    return MakeDoc(item.GetStringValue(), b, packageData, null, context, locator, false);
                }
                catch (XPathException e) when (context.GetConfiguration().IsRecoverFromDocFailures()
                                               && (e.HasErrorCode("FODC0002") || e.HasErrorCode("FODC0005")))
                {
                    // XSLT recoverable dynamic error: document() could not retrieve the resource; recover by
                    // producing no node for this URI (ItemMappingIterator drops a null result). Gated on the
                    // config flag (error-FODC0002a-ignore); the default keeps raising (error-FODC0002a).
                    return null;
                }
            }
        }
    }
}