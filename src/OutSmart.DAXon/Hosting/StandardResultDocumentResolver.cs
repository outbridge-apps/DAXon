////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Streams;
using OutSmart.DAXon.Core;
using System.IO;
namespace OutSmart.DAXon.Lib
{
    internal sealed class StandardResultDocumentResolver : IResultDocumentResolver
    {
        private static readonly StandardResultDocumentResolver theInstance = new StandardResultDocumentResolver();
        public static StandardResultDocumentResolver GetInstance()
        {
            return theInstance;
        }

        public IReceiver Resolve(IXPathContext context, string href, string baseUri, SerializationProperties properties)
        {
            // Outside Resolve(href, base): its catch-all would re-wrap the denial without its code.
            string denied = OutSmart.DAXon.Internal.ResourceGate.CheckOutput(context.GetConfiguration(), href, baseUri);
            if (denied != null)
            {
                throw new XPathException(denied, DAXonErrorCode.SXRD0004);
            }

            StreamResult result = Resolve(href, baseUri);
            SerializerFactory factory = context.GetConfiguration().SerializerFactory;
            PipelineConfiguration pipe = context.GetController().MakePipelineConfiguration();
            return factory.GetReceiver(result, properties, pipe);
        }

        public StreamResult Resolve(string href, string @base)
        {

            string which = "base";
            try
            {
                URI absoluteURI;
                if ((href.Length == 0))
                {
                    if (@base == null)
                    {
                        throw new XPathException("The system identifier of the principal output file is unknown");
                    }

                    absoluteURI = new URI(@base);
                }
                else
                {
                    which = "relative";
                    absoluteURI = new URI(href);
                }

                if (!absoluteURI.IsAbsolute())
                {
                    if (@base == null)
                    {
                        throw new XPathException("The system identifier of the principal output file is unknown");
                    }

                    which = "base";
                    URI baseURI = new URI(@base);
                    which = "relative";
                    absoluteURI = baseURI.Resolve(href);
                }

                return CreateResult(absoluteURI);
            }
            catch (URISyntaxException err)
            {
                throw new XPathException("Invalid syntax for " + which + " URI", err);
            }
            catch (ArgumentException err2)
            {
                throw new XPathException("Invalid " + which + " URI syntax", err2);
            }
            catch (UriFormatException err3)
            {
                throw new XPathException("Resolved URL is malformed", err3);
            }
            // (UnknownServiceException catch dropped - URL branch neutered)
            catch (IOException err4)
            {
                throw new XPathException("Cannot open connection to specified URL", err4);
            }
            catch (Exception err6)
            {
                throw new XPathException("Standard result document resolver failed", err6);
            }
        }

        protected StreamResult CreateResult(URI absoluteURI)
        {
            if ("file".Equals(absoluteURI.Scheme))
            {
                return MakeOutputFile(absoluteURI);
            }
            else
            {

                // See if the Java VM can conjure up a writable URL connection for us.
                // This is optimistic: I have yet to discover a URL scheme that it can handle "out of the box".
                // But it can apparently be achieved using custom-written protocol handlers.
                // URL-protocol output branch neutered (no compat URLConnection output; stock JVM throws here too):
                throw new XPathException("Failed to establish connection to non-file output destination: " + absoluteURI.ToASCIIString());
            }
        }

        public static StreamResult MakeOutputFile(URI absoluteURI)
        {
            lock (typeof(StandardResultDocumentResolver))
            {
                try
                {
                    string outputFile = new Uri(absoluteURI.ToString()).LocalPath;
                    if (Directory.Exists(outputFile))
                    {
                        throw new XPathException("Cannot write to a directory: " + absoluteURI, DAXonErrorCode.SXRD0004);
                    }

                    if ((File.Exists(outputFile) || Directory.Exists(outputFile)) && !(File.Exists(outputFile) && !new FileInfo(outputFile).IsReadOnly))
                    {
                        throw new XPathException("Cannot write to URI " + absoluteURI, DAXonErrorCode.SXRD0004);
                    }

                    return new StreamResult(outputFile);
                }
                catch (ArgumentException err)
                {
                    throw new XPathException("Cannot write to URI " + absoluteURI + " (" + err.Message + ")");
                }
            }
        }
    }
}