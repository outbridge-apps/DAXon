////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Internal.Net;

namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the fn:collection() function. This is responsible for calling the registered
    /// <see cref="ICollectionFinder"/>. For the effect of the default system-supplied CollectionFinder,
    /// see <see cref="OutSmart.DAXon.Resources.StandardCollectionFinder"/>.
    ///
    /// Port note: this is a faithful port of Saxon 12.9 CollectionFn trimmed of two branches that are
    /// unreachable in this build: (a) the multithreaded ObjectValue&lt;Resource&gt; mapping (a parallelism
    /// optimization — semantically identical to mapping each Resource to its item); (b) the XSLT
    /// SpaceStrippedDocument wrapping, which is a no-op unless xsl:strip-space is in effect and whose
    /// wrapper is not ported. The stable-collection caching in Controller UserData IS retained, because
    /// repeated calls (e.g. collection() | collection()) rely on node identity being preserved.
    /// </summary>
    internal sealed class CollectionFn : SystemFunction, ICallable
    {
        /// <summary>URI representing a collection that is always empty.</summary>
        public static string EMPTY_COLLECTION_URI = "http://saxon.sf.net/collection/empty";

        public override int GetSpecialProperties(Expression[] arguments)
        {
            // See redmine bug 1652. We cannot assume the nodes will be in document order, distinct, or newly created.
            return (base.GetSpecialProperties(arguments) & ~StaticProperty.NO_NODES_NEWLY_CREATED) | StaticProperty.PEER_NODESET;
        }

        public static string GetAbsoluteCollectionURI(string baseUri, string href, IXPathContext context)
        {
            string absoluteURI;
            if (href == null)
            {
                absoluteURI = context.GetConfiguration().DefaultCollection;
            }
            else
            {
                URI uri;
                try
                {
                    uri = new URI(href);
                }
                catch (Exception)
                {
                    href = IriToUri.IriToUriFn(StringView.Tidy(new StringView(href))).ToString();
                    try
                    {
                        uri = new URI(href);
                    }
                    catch (Exception e2)
                    {
                        throw new XPathException(e2.Message, "FODC0004");
                    }
                }

                try
                {
                    if (uri.IsAbsolute())
                    {
                        absoluteURI = uri.ToString();
                    }
                    else if (baseUri != null)
                    {
                        absoluteURI = ResolveURI.MakeAbsolute(href, baseUri).ToString();
                    }
                    else
                    {
                        throw new XPathException("Relative collection URI cannot be resolved: no base URI available", "FODC0002");
                    }
                }
                catch (XPathException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new XPathException(e.Message, "FODC0004");
                }
            }

            return absoluteURI;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            string href;
            if (GetArity() == 0)
            {
                href = context.GetController().GetDefaultCollection();
            }
            else
            {
                IItem arg = arguments[0].Head();
                href = arg == null ? context.GetController().GetDefaultCollection() : arg.GetStringValue();
            }

            if (href == null)
            {
                throw new XPathException("No default collection has been defined", "FODC0002", context);
            }

            string absoluteURI = GetAbsoluteCollectionURI(GetRetainedStaticContext().StaticBaseUriString, href, context);
            string collectionKey = absoluteURI;

            // In XSLT the stylesheet's xsl:strip-space rules apply to collection documents; a stripped
            // collection needs its own cache key (same policy as upstream).
            PackageData packageData = GetRetainedStaticContext().GetPackageData();
            ISpaceStrippingRule whitespaceRule = NoElementsSpaceStrippingRule.GetInstance();
            if (packageData is Xslt.StylesheetPackage ssp0 && packageData.IsXSLT())
            {
                whitespaceRule = ssp0.SpaceStrippingRule;
                if (whitespaceRule != NoElementsSpaceStrippingRule.GetInstance())
                {
                    collectionKey = ssp0.PackageName + ssp0.GetPackageVersion() + " " + absoluteURI;
                }
            }

            IGroundedValue cachedCollection = (IGroundedValue)context.GetController().GetUserData("saxon:collections", collectionKey);
            if (cachedCollection != null)
            {
                return cachedCollection;
            }

            // Use a collection registered with the configuration if there is one; otherwise call the CollectionFinder.
            IResourceCollection collection = context.GetConfiguration().GetRegisteredCollection(absoluteURI);
            if (collection == null)
            {
                ICollectionFinder collectionFinder = context.GetController().GetCollectionFinder();
                if (collectionFinder != null)
                {
                    collection = collectionFinder.FindCollection(context, absoluteURI);
                }
            }

            if (collection == null)
            {
                collection = new EmptyCollection(EMPTY_COLLECTION_URI);
            }

            // In XSLT, worry about whitespace stripping
            if (packageData is Xslt.StylesheetPackage && whitespaceRule != NoElementsSpaceStrippingRule.GetInstance())
            {
                if (collection is Resources.AbstractResourceCollection arc && arc.StripWhitespace(whitespaceRule))
                {
                    whitespaceRule = null;
                }
            }

            ISequenceIterator result = new CollectionIterator(collection.GetResources(context), context);

            // Apply space-stripping to document nodes in the collection (no-op when the builder already did)
            if (whitespaceRule != null && whitespaceRule != NoElementsSpaceStrippingRule.GetInstance())
            {
                ISpaceStrippingRule rule = whitespaceRule;
                result = ItemMappingIterator.IMap(result, (item =>
                {
                    if (item is NodeInfo node && node.GetNodeKind() == Types.Type.DOCUMENT)
                    {
                        ITreeInfo treeInfo = node.GetTreeInfo();
                        if (treeInfo.SpaceStrippingRule != rule)
                        {
                            return new Trees.Wrappers.SpaceStrippedDocument(treeInfo, rule).GetRootNode();
                        }
                    }
                    return item;
                }));
            }

            // If the collection is stable, cache the grounded result so repeated calls preserve node identity.
            if (collection.IsStable(context) || context.GetConfiguration().GetBooleanProperty(Feature<bool>.STABLE_COLLECTION_URI))
            {
                cachedCollection = SequenceTool.ToGroundedValue(result);
                context.GetController().SetUserData("saxon:collections", collectionKey, cachedCollection);
                return cachedCollection;
            }

            return new LazySequence(result);
        }

        private sealed class EmptyCollection : IResourceCollection
        {
            private readonly string collectionUri;
            public string CollectionURI => collectionUri;
            public EmptyCollection(string cUri) { collectionUri = cUri; }
            public IEnumerator<string> GetResourceURIs(IXPathContext context) { yield break; }
            public IEnumerator<IResource> GetResources(IXPathContext context) { yield break; }
            public bool IsStable(IXPathContext context) { return true; }
        }

        // A SequenceIterator over the resources of a collection, delivering each Resource's item.
        // (Upstream wraps each resource in ObjectValue<Resource> and maps through a multithreaded
        // iterator; the observable sequence is identical to mapping each Resource to its item.)
        private sealed class CollectionIterator : ISequenceIterator
        {
            private readonly IEnumerator<IResource> sources;
            public CollectionIterator(IEnumerator<IResource> sources, IXPathContext context)
            {
                this.sources = sources;
            }

            public IItem Next()
            {
                // a clock read per member: only XML members met the deadline inside their parse, so
                // 20k text or metadata members ran 5 s past a 2 s limit
                Core.Controller.CheckActiveTimeoutNow();
                while (sources.MoveNext())
                {
                    Core.Controller.CheckActiveTimeoutNow();
                    IResource r = sources.Current;
                    if (r == null)
                    {
                        continue;
                    }
                    IItem item = r.Item;
                    if (item != null)
                    {
                        return item;
                    }
                }
                return null;
            }

            public void Dispose() { (sources as IDisposable)?.Dispose(); }
        }
    }
}
