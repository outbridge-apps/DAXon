////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Resources
{
    public abstract class AbstractResourceCollection : IResourceCollection
    {
        protected Configuration config;
        protected string collectionURI;
        protected URIQueryParameters @params;
        protected bool noExceptions;

        public virtual string CollectionURI => collectionURI;
        public AbstractResourceCollection(Configuration config)
        {
            this.config = config;
        }

        public static void CheckNotNull(string collectionURI, IXPathContext context)
        {
            if (collectionURI == null)
            {
                throw new XPathException("No default collection has been defined").WithErrorCode("FODC0002").WithXPathContext(context);
            }
        }

        public virtual bool IsStable(IXPathContext context)
        {
            if (@params == null)
            {
                return false;
            }

            bool? stable = @params.Stable;
            if (!stable.HasValue)
            {
                return context.GetConfiguration().GetBooleanProperty(Feature<bool>.STABLE_COLLECTION_URI);
            }
            else
            {
                return stable.Value;
            }
        }

        public virtual void RegisterContentType(string contentType, IResourceFactory factory)
        {
            config.RegisterMediaType(contentType, factory);
        }

        protected virtual ParseOptions OptionsFromQueryParameters(URIQueryParameters @params, IXPathContext context)
        {
            ParseOptions options = context.GetConfiguration().GetParseOptions();
            if (@params != null)
            {
                int? v = @params.GetValidationMode();
                if (v.HasValue)
                {
                    options = options.WithSchemaValidationMode(v.Value);
                }

                bool? xInclude = @params.XInclude;
                if (xInclude.HasValue)
                {
                    options = options.WithXIncludeAware(xInclude.Value);
                }

                ISpaceStrippingRule stripSpace = @params.SpaceStrippingRule;
                if (stripSpace != null)
                {
                    options = options.WithSpaceStrippingRule(stripSpace);
                }

                // If the URI requested suppression of errors, or that errors should be treated
                // as warnings, we set up a special ErrorListener to achieve this
                int onError = URIQueryParameters.ON_ERROR_FAIL;
                if (@params.OnError.HasValue)
                {
                    onError = @params.OnError.Value;
                }

                Controller controller = context.GetController();
                IErrorReporter oldErrorReporter = controller == null ? context.GetConfiguration().MakeErrorReporter() : controller.ErrorReporter;
                options = SetupErrorHandlingForCollection(options, onError, oldErrorReporter);
            }

            return options;
        }

        public static ParseOptions SetupErrorHandlingForCollection(ParseOptions options, int onError, IErrorReporter oldErrorReporter)
        {
            if (onError == URIQueryParameters.ON_ERROR_IGNORE)
            {
                options = options.WithErrorReporter(new ErrorSuppressor());
            }
            else if (onError == URIQueryParameters.ON_ERROR_WARNING)
            {
                options = options.WithErrorReporter(new ErrorAsWarningReporter(oldErrorReporter));
            }

            return options;
        }

        protected virtual InputDetails GetInputDetails(string resourceURI)
        {
            // Every member, before it is sniffed or opened.
            string denied = OutSmart.DAXon.Internal.ResourceGate.CheckRead(config, resourceURI, OutSmart.DAXon.Api.ResourceKind.Collection);
            if (denied != null)
            {
                throw new XPathException(denied, "FODC0002");
            }

            InputDetails inputDetails = new InputDetails();
            try
            {
                inputDetails.resourceUri = resourceURI;
                URI uri = new URI(resourceURI);
                if (uri.Scheme == "file")
                {
                    if (@params != null && @params.ContentType != null)
                    {
                        inputDetails.contentType = @params.ContentType;
                    }
                    else
                    {
                        inputDetails.contentType = GuessContentTypeFromName(resourceURI);
                    }
                }
                else
                {
                    // This connection is opened for its headers alone - the body is fetched again
                    // below by UrlStream - so it must be released here rather than left to
                    // finalization, which would hold a pooled socket per collection member.
                    URLConnection connection = ResourceLoader.UrlConnection(uri.Inner, config, OutSmart.DAXon.Api.ResourceKind.Collection);
                    try
                    {
                        // the charset comes from Content-Type below; Content-Encoding is a transfer coding
                        inputDetails.contentType = connection.ContentType;
                    }
                    finally
                    {
                        connection.Disconnect();
                    }

                    foreach (string param in inputDetails.contentType.Replace(" ", "").Split(';'))
                    {
                        if (param.StartsWith("charset=", StringComparison.Ordinal))
                        {
                            inputDetails.encoding = param.Split(new[] { '=' }, 2)[1];
                        }
                        else
                        {
                            inputDetails.contentType = param;
                        }
                    }
                }

                if (inputDetails.contentType == null || config.GetResourceFactoryForMediaType(inputDetails.contentType) == null)
                {
                    System.IO.Stream stream;
                    if (uri.Scheme == "file")
                    {
                        string file = new Uri(uri.ToString()).LocalPath;
                        stream = OutSmart.DAXon.Internal.Streams.FileNames.OpenRead(file);
                        // kept only within the policy's input cap: the members read these bytes as content
                        if (new FileInfo(file).Length <= Math.Min(1024, InputSizeLimit.MaxFor(config)))
                        {
                            inputDetails.binaryContent = BinaryResource.ReadBinaryFromStream(stream, resourceURI);
                            stream.Dispose();
                            stream = new MemoryStream(inputDetails.binaryContent);
                        }
                    }
                    else
                    {
                        stream = ResourceLoader.UrlStream(config, uri.ToString(), OutSmart.DAXon.Api.ResourceKind.Collection);
                    }

                    // finally, not a bare Dispose after the call: a throw out of the sniffer used to
                    // leave this file handle or response to the finalizer.
                    try
                    {
                        inputDetails.contentType = GuessContentTypeFromContent(stream);
                    }
                    finally
                    {
                        stream.Dispose();
                    }
                }

                if (@params != null && @params.OnError.HasValue)
                {
                    inputDetails.onError = @params.OnError.Value;
                }

                return inputDetails;
            }
            catch (URISyntaxException e)
            {
                throw new XPathException(e.Message, "FODC0004");
            }
            catch (IOException e)
            {
                // a catalog can name a file that is not there: FODC0002 like doc(), not an uncoded error
                throw new XPathException(e.Message, "FODC0002");
            }
        }

        protected virtual string GuessContentTypeFromName(string resourceURI)
        {
            string contentTypeFromName = URLConnection.GuessContentTypeFromName(resourceURI);
            string extension = null;
            if (contentTypeFromName == null)
            {
                extension = GetFileExtension(resourceURI);
                if (extension != null)
                {
                    contentTypeFromName = config.GetMediaTypeForFileExtension(extension);
                }
            }

            return contentTypeFromName;
        }

        protected virtual string GuessContentTypeFromContent(System.IO.Stream stream)
        {
            try
            {
                stream = InputStreamMarker.EnsureMarkSupported(stream);
                return URLConnection.GuessContentTypeFromStream(stream);
            }
            catch (IOException)
            {
                return null;
            }
        }

        private string GetFileExtension(string name)
        {
            int i = name.LastIndexOf('.');
            int p = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            if (i > p && i + 1 < name.Length)
            {
                return name.Substring(i + 1);
            }

            return null;
        }

        public virtual IResource MakeResource(IXPathContext context, InputDetails details)
        {
            IResourceFactory factory = null;
            string contentType = details.contentType;
            if (contentType != null)
            {
                factory = context.GetConfiguration().GetResourceFactoryForMediaType(contentType);
            }

            if (factory == null)
            {
                factory = BinaryResource.FACTORY;
            }

            return factory.MakeResource(context, details);
        }

        public virtual IResource MakeTypedResource(IXPathContext context, IResource basicResource)
        {
            string mediaType = basicResource.ContentType;
            IResourceFactory factory = config.GetResourceFactoryForMediaType(mediaType);
            if (factory == null)
            {
                return basicResource;
            }

            if (basicResource is BinaryResource)
            {
                InputDetails details = new InputDetails();
                details.binaryContent = ((BinaryResource)basicResource).Data;
                details.contentType = mediaType;
                details.resourceUri = basicResource.ResourceURI;
                return factory.MakeResource(context, details);
            }
            else if (basicResource is UnparsedTextResource)
            {
                InputDetails details = new InputDetails();
                details.characterContent = ((UnparsedTextResource)basicResource).Content;
                details.contentType = mediaType;
                details.resourceUri = basicResource.ResourceURI;
                return factory.MakeResource(context, details);
            }
            else
            {
                return basicResource;
            }
        }

        public virtual IResource MakeResource(IXPathContext context, string resourceURI)
        {
            InputDetails details = GetInputDetails(resourceURI);
            return MakeResource(context, details);
        }

        public virtual bool StripWhitespace(ISpaceStrippingRule rules)
        {
            return false;
        }
        public abstract IEnumerator<string> GetResourceURIs(IXPathContext arg0);
        public abstract IEnumerator<IResource> GetResources(IXPathContext arg0);

        private sealed class ErrorSuppressor : IErrorReporter
        {
            public void Report(IXmlProcessingError error)
            {
            }
        }

        private sealed class ErrorAsWarningReporter : IErrorReporter
        {
            private readonly IErrorReporter originalErrorReporter;
            public ErrorAsWarningReporter(IErrorReporter originalErrorReporter)
            {
                this.originalErrorReporter = originalErrorReporter;
            }

            public void Report(IXmlProcessingError error)
            {
                if (error.IsWarning())
                {
                    originalErrorReporter.Report(error);
                }
                else
                {
                    originalErrorReporter.Report(error.AsWarning());
                    XmlProcessingIncident supp = new XmlProcessingIncident("The document will be excluded from the collection", DAXonErrorCode.SXWN9050).AsWarning();
                    supp.SetLocation(error.GetLocation());
                    originalErrorReporter.Report(supp);
                }
            }
        }

        /// <summary>
        /// Information about a resource
        /// </summary>
        public class InputDetails
        {
            /// <summary>
            /// The URI of the resource
            /// </summary>
            public string resourceUri;
            /// <summary>
            /// The binary content of the resource
            /// </summary>
            public byte[] binaryContent;
            /// <summary>
            /// The character content of the resource
            /// </summary>
            public string characterContent;
            /// <summary>
            /// The media type of the resource
            /// </summary>
            public string contentType;
            /// <summary>
            /// The encoding of the resource (if it is text, represented in binary)
            /// </summary>
            public string encoding;
            public ParseOptions parseOptions;
            public int onError = URIQueryParameters.ON_ERROR_FAIL;
            // Capped like doc(): UrlStream holds only compressed bytes to the input cap (MaxMemoryBytes).
            public virtual System.IO.Stream GetInputStream(Configuration config)
            {
                return InputSizeLimit.Apply(ResourceLoader.UrlStream(config, resourceUri, OutSmart.DAXon.Api.ResourceKind.Collection),
                    InputSizeLimit.MaxFor(config), resourceUri, "FODC0002");
            }

            public virtual byte[] ObtainBinaryContent(Configuration config)
            {
                if (binaryContent != null)
                {
                    return binaryContent;
                }
                else if (characterContent != null)
                {
                    string e = encoding != null ? encoding : "UTF-8";
                    return BinaryResource.Encode(characterContent, e);
                }
                else
                {
                    try
                    {
                        using (System.IO.Stream stream = GetInputStream(config))
                        {
                            return BinaryResource.ReadBinaryFromStream(stream, resourceUri);
                        }
                    }
                    catch (IOException e)
                    {
                        throw new XPathException(e.Message, "FODC0002");
                    }
                }
            }

            // Strict, unlike a text resource: undecodable bytes are an error.
            public virtual string ObtainCharacterContent(Configuration config)
            {
                if (characterContent == null)
                {
                    byte[] bytes = ObtainBinaryContent(config);
                    string enc = encoding ?? EncodingDetector.InferEncoding(bytes, bytes.Length, "UTF-8");
                    characterContent = BinaryResource.Decode(bytes, enc);
                }

                return characterContent;
            }

            // A member that fails when its item is read obeys the collection's on-error choice.
            internal IItem Skip(IXPathContext context, XPathException e)
            {
                if (onError == URIQueryParameters.ON_ERROR_FAIL)
                {
                    throw e;
                }
                else if (onError == URIQueryParameters.ON_ERROR_WARNING)
                {
                    context.GetController()?.Warning("collection(): failed to read " + resourceUri + ": " + e.Message, e.ShowErrorCode(), null);
                }

                return null;
            }
        }
    }
}
