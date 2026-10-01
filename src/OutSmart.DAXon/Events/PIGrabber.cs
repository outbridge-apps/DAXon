////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Net;
using System.Collections.Generic;
using UnicodeString = OutSmart.DAXon.Text.UnicodeString;

namespace OutSmart.DAXon.Events
{
    // Faithful port of net.sf.saxon.event.PIGrabber (Saxon 12.9). Was a hollow stub (in the WRONG
    // namespace, OutSmart.DAXon.Xslt) whose GetAssociatedStylesheets threw — the xml-stylesheet PI
    // path (GetAssociatedStylesheet) could never find an embedded/associated stylesheet.
    // A ProxyReceiver that looks for xml-stylesheet processing instructions matching given criteria;
    // for those that do, it creates a ResolvedResource referring to the relevant stylesheet.
    internal sealed class PIGrabber : ProxyReceiver
    {
        private Configuration config = null;
        private string reqMedia = null;
        private string reqTitle = null;
        private string baseURI = null;
        private IResourceResolver resourceResolver = null;
        private readonly List<string> stylesheets = new List<string>();
        private bool terminated = false;

        /// <summary>
        /// Return the list of stylesheets that matched, as an array of ResolvedResource objects
        /// (the port's replacement for the deleted JAXP Source hierarchy), or null if none matched.
        /// </summary>
        public ResolvedResource[] AssociatedStylesheets
        {
            get
            {
                if (stylesheets.Count == 0)
                {
                    return null;
                }

                ResolvedResource[] result = new ResolvedResource[stylesheets.Count];
                ResourceRequest request = new ResourceRequest();
                request.baseUri = baseURI;
                request.nature = ResourceRequest.XSLT_NATURE;
                request.purpose = ResourceRequest.ANY_PURPOSE;
                for (int i = 0; i < stylesheets.Count; i++)
                {
                    string href = stylesheets[i];
                    request.relativeUri = href;
                    try
                    {
                        request.uri = ResolveURI.MakeAbsolute(href, baseURI).ToString();
                    }
                    catch (URISyntaxException e)
                    {
                        throw XPathException.MakeXPathException(e);
                    }

                    ResolvedResource s = request.Resolve(resourceResolver,
                                                         config.GetResourceResolver(),
                                                         new DirectResourceResolver(config));
                    result[i] = s;
                }

                return result;
            }
        }

        public PIGrabber(IReceiver next) : base(next)
        {
        }

        public void SetFactory(Configuration config)
        {
            this.config = config;
        }

        /// <summary>
        /// Define the matching criteria (required media and title). Saxon does not implement the
        /// CSS3 media-query syntax; by default the media value comparison uses
        /// Configuration.GetMediaQueryEvaluator().
        /// </summary>
        public void SetCriteria(string media, string title)
        {
            this.reqMedia = media;
            this.reqTitle = title;
        }

        public void SetBaseURI(string uri)
        {
            baseURI = uri;
        }

        public void SetResourceResolver(IResourceResolver resolver)
        {
            resourceResolver = resolver;
        }

        /// <summary>
        /// Abort the parse when the first start element tag is found
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type,
            IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            terminated = true;
            // abort the parse when the first start element tag is found
            throw new XPathException("#start#");
        }

        /// <summary>
        /// Determine whether the parse terminated because the first start element tag was found
        /// (as distinct from being terminated by an exception condition such as a parse error).
        /// </summary>
        public bool IsTerminated()
        {
            return terminated;
        }

        /// <summary>
        /// Handle xml-stylesheet PI
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (target.Equals("xml-stylesheet"))
            {
                string value = data.ToString();
                string piMedia = ProcInstParser.GetPseudoAttribute(value, "media");
                string piTitle = ProcInstParser.GetPseudoAttribute(value, "title");
                string piType = ProcInstParser.GetPseudoAttribute(value, "type");
                string piAlternate = ProcInstParser.GetPseudoAttribute(value, "alternate");

                if (piType == null)
                {
                    return;
                }

                if ((piType.Equals("text/xml") || piType.Equals("application/xml") ||
                        piType.Equals("text/xsl") || piType.Equals("applicaton/xsl") || piType.Equals("application/xml+xslt")) &&

                        (reqMedia == null || piMedia == null ||
                            GetConfiguration().MediaQueryEvaluator.Compare(piMedia, reqMedia) == 0) &&   // see bug 1729

                        ((piTitle == null && (piAlternate == null || piAlternate.Equals("no"))) ||
                                (reqTitle == null) ||
                                (piTitle != null && piTitle.Equals(reqTitle))))
                {
                    string href = ProcInstParser.GetPseudoAttribute(value, "href");
                    if (href == null)
                    {
                        throw new XPathException("xml-stylesheet PI has no href attribute");
                    }

                    if (piTitle == null && (piAlternate == null || piAlternate.Equals("no")))
                    {
                        stylesheets.Insert(0, href);
                    }
                    else
                    {
                        stylesheets.Add(href);
                    }
                }
            }
        }
    }
}
