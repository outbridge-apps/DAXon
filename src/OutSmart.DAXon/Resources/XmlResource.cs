////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System.IO;

namespace OutSmart.DAXon.Resources
{
    // Faithful port of net.sf.saxon.resource.XmlResource (Saxon 12.9). Was a hollow stub whose factory
    // threw, so DirectoryCollection could never deliver XML documents.
    // A Resource (typically an item in a collection) representing an XML document, parsed lazily.
    // Port note: the deleted JAXP Source hierarchy is replaced by ActiveStreamSource.
    internal sealed class XmlResource : IResource
    {

        public static readonly IResourceFactory FACTORY = new XmlResourceFactory();
        private NodeInfo doc;
        private readonly IXPathContext context;
        private readonly Configuration config;
        private AbstractResourceCollection.InputDetails details;

        public string ResourceURI
        {
            get
            {
                if (doc == null)
                {
                    return details.resourceUri;
                }
                else
                {
                    return doc.GetSystemId();
                }
            }
        }

        /// <summary>
        /// Get an item representing the resource: a document node for the XML document.
        /// Returns null if there is an error and the error is to be ignored.
        /// </summary>
        public IItem Item
        {
            get
            {
                if (doc == null)
                {
                    string resourceURI = details.resourceUri;
                    ParseOptions options = details.parseOptions;
                    if (options == null)
                    {
                        options = config.GetParseOptions();
                    }

                    IActiveSource source;
                    Stream stream = null;
                    if (details.characterContent != null)
                    {
                        source = new ActiveStreamSource(null, new StringReader(details.characterContent), resourceURI);
                    }
                    else if (details.binaryContent != null)
                    {
                        stream = new MemoryStream(details.binaryContent);
                        source = new ActiveStreamSource(stream, null, resourceURI);
                    }
                    else
                    {
                        try
                        {
                            stream = details.GetInputStream(config);
                            source = new ActiveStreamSource(stream, null, resourceURI);
                        }
                        catch (IOException e)
                        {
                            if (details.onError == URIQueryParameters.ON_ERROR_FAIL)
                            {
                                throw new XPathException(e.Message, "FODC0002");
                            }
                            else if (details.onError == URIQueryParameters.ON_ERROR_WARNING)
                            {
                                context.GetController().Warning("collection(): failed to read XML file " + details.resourceUri + ": " + e.Message, "FODC0005", null);
                                return null;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }

                    try
                    {
                        doc = config.BuildDocumentTree(source, options).GetRootNode();
                    }
                    catch (XPathException e)
                    {
                        if (details.onError == URIQueryParameters.ON_ERROR_FAIL)
                        {
                            throw e.WithMessage("collection(): failed to parse XML file " + resourceURI + ": " + e.Message);
                        }
                        else if (details.onError == URIQueryParameters.ON_ERROR_WARNING)
                        {
                            context.GetController().Warning("collection(): failed to parse XML file " + resourceURI + ": " + e.Message, e.ShowErrorCode(), null);
                        }

                        doc = null;
                    }
                    finally
                    {
                        if (stream != null)
                        {
                            try
                            {
                                stream.Dispose();
                            }
                            catch (IOException)
                            {
                                // ignore the failure
                            }
                        }
                    }
                }

                return doc;
            }
        }

        public string ContentType => "application/xml";

        public XmlResource(IXPathContext context, AbstractResourceCollection.InputDetails details)
        {
            this.config = context.GetConfiguration();
            this.context = context;
            this.details = details;
        }
    }
}
