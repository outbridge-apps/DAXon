////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;

namespace OutSmart.DAXon.Resources
{
    // Port of net.sf.saxon.resource.CatalogCollection (Saxon 12.9): a collection listed by an XML
    // catalog, <collection stable="true|false"><doc href="..."/>...</collection>, each href resolved
    // against the base URI of its <doc>. Unlike upstream, the collection URI's query parameters
    // (on-error, content-type, metadata is not offered) reach the members.
    internal sealed class CatalogCollection : AbstractResourceCollection
    {
        private bool stable;
        private ISpaceStrippingRule whitespaceRules;

        public CatalogCollection(Configuration config, string collectionURI, URIQueryParameters @params) : base(config)
        {
            this.collectionURI = collectionURI;
            this.@params = @params;
        }

        public override bool StripWhitespace(ISpaceStrippingRule rules)
        {
            this.whitespaceRules = rules;
            return true;
        }

        public override bool IsStable(IXPathContext context)
        {
            return stable;
        }

        public override IEnumerator<string> GetResourceURIs(IXPathContext context)
        {
            CheckNotNull(collectionURI, context);
            return CatalogContents(collectionURI, context).GetEnumerator();
        }

        public override IEnumerator<IResource> GetResources(IXPathContext context)
        {
            CheckNotNull(collectionURI, context);
            foreach (string uri in CatalogContents(collectionURI, context))
            {
                IResource resource;
                try
                {
                    if (uri.StartsWith("data:", StringComparison.Ordinal))
                    {
                        resource = MakeTypedResource(context, DataURIScheme.Decode(uri));
                    }
                    else
                    {
                        InputDetails details = GetInputDetails(uri);
                        details.parseOptions = context.GetConfiguration().GetParseOptions().WithSpaceStrippingRule(whitespaceRules);
                        details.resourceUri = uri;
                        if (@params?.ContentType != null)
                        {
                            details.contentType = @params.ContentType;
                        }

                        resource = MakeResource(context, details);
                    }
                }
                catch (XPathException e)
                {
                    int onError = @params?.OnError ?? URIQueryParameters.ON_ERROR_FAIL;
                    if (onError == URIQueryParameters.ON_ERROR_FAIL)
                    {
                        resource = new FailedResource(uri, e);
                    }
                    else
                    {
                        if (onError == URIQueryParameters.ON_ERROR_WARNING)
                        {
                            context.GetController()?.Warning("collection(): failed to parse " + uri + ": " + e.Message, e.ShowErrorCode(), null);
                        }

                        continue;
                    }
                }

                yield return resource;
            }
        }

        private List<string> CatalogContents(string href, IXPathContext context)
        {
            ResolvedResource source = DocumentFn.ResolveURI(href, null, href, context);
            ParseOptions options = new ParseOptions().WithSchemaValidationMode(Validation.SKIP).WithDTDValidationMode(Validation.SKIP);
            ITreeInfo catalog = source == null || source.IsEmpty
                ? null
                : source.Node != null ? source.Node.GetTreeInfo() : context.GetConfiguration().BuildDocumentTree(source, options);
            if (catalog == null)
            {
                throw new XPathException("Failed to load collection catalog " + href, "FODC0004", context);
            }

            NodeInfo top = catalog.GetRootNode().IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT).Next();
            if (top == null || top.GetLocalPart() != "collection" || top.GetNamespaceUri() != NamespaceUri.NULL)
            {
                string message = top == null
                    ? "No outermost element found in collection catalog"
                    : "Outermost element of collection catalog should be Q{}collection (found Q{" + top.GetNamespaceUri() + "}" + top.GetLocalPart() + ")";
                throw new XPathException(message, "FODC0004", context);
            }

            string stableAtt = top.GetAttributeValue(NamespaceUri.NULL, "stable");
            if (stableAtt == "true")
            {
                stable = true;
            }
            else if (stableAtt == "false")
            {
                stable = false;
            }
            else if (stableAtt != null)
            {
                throw new XPathException("The 'stable' attribute of element <collection> must be true or false", "FODC0004", context);
            }

            var result = new List<string>();
            IAxisIterator documents = top.IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT);
            NodeInfo item;
            while ((item = documents.Next()) != null)
            {
                if (item.GetLocalPart() != "doc" || item.GetNamespaceUri() != NamespaceUri.NULL)
                {
                    throw new XPathException("Children of <collection> element must be <doc> elements", "FODC0004", context);
                }

                string hrefAtt = item.GetAttributeValue(NamespaceUri.NULL, "href");
                if (hrefAtt == null)
                {
                    throw new XPathException("A <doc> element in the collection catalog has no @href attribute", "FODC0004", context);
                }

                try
                {
                    result.Add(Functions.ResolveURI.MakeAbsolute(hrefAtt, item.GetBaseURI()).ToString());
                }
                catch (URISyntaxException)
                {
                    throw new XPathException("Invalid base URI or href URI in collection catalog: (" + item.GetBaseURI() + ", " + hrefAtt + ")", "FODC0004", context);
                }
            }

            return result;
        }
    }
}
