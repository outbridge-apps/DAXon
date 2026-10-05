////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;using OutSmart.DAXon.Functions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Streams;
using OutSmart.DAXon.Core;
using System.IO;
using OutSmart.DAXon.Serialization;
namespace OutSmart.DAXon.Lib
{
    internal sealed class StandardOutputResolver : IOutputURIResolver
    {
        private static readonly StandardOutputResolver theInstance = new StandardOutputResolver();
        public static StandardOutputResolver GetInstance()
        {
            return theInstance;
        }

        public StandardOutputResolver NewInstance()
        {
            return this;
        }

        public IResultTarget Resolve(string href, string @base)
        {

            string which = "base";
            try
            {
                URI absoluteURI;
                if ((href.Length == 0))
                {
                    if (@base == null)
                    {
                        throw new XPathException("The system identifier of the principal output file is unknown", DAXonErrorCode.SXRD0002);
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
                        throw new XPathException("The system identifier of the principal output file is unknown", DAXonErrorCode.SXRD0002);
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
                throw new XPathException("Invalid syntax for " + which + " URI", DAXonErrorCode.SXRD0001);
            }
            catch (ArgumentException err2)
            {
                throw new XPathException("Invalid " + which + " URI syntax", DAXonErrorCode.SXRD0001);
            }
            catch (UriFormatException err3)
            {
                throw new XPathException("Resolved URL is malformed", err3).WithErrorCode(DAXonErrorCode.SXRD0001);
            }
            // (UnknownServiceException catch dropped - URL branch neutered)
            catch (IOException err5)
            {
                throw new XPathException("Cannot open connection to specified URL", err5).WithErrorCode(DAXonErrorCode.SXRD0001);
            }
        }

        protected IResultTarget CreateResult(URI absoluteURI)
        {
            if (absoluteURI.Scheme == "file")
            {
                return StandardResultDocumentResolver.MakeOutputFile(absoluteURI);
            }
            else
            {

                // See if the Java VM can conjure up a writable URL connection for us.
                // This is optimistic: I have yet to discover a URL scheme that it can handle "out of the box".
                // But it can apparently be achieved using custom-written protocol handlers.
                // URL-protocol output branch neutered (no compat URLConnection output; stock JVM throws here too):
                throw new XPathException("Failed to establish connection to non-file output destination: " + absoluteURI.ToASCIIString(), DAXonErrorCode.SXRD0004);
            }
        }

        public void Dispose(IResultTarget result)
        {
            if (result is StreamResult)
            {
                System.IO.Stream stream = ((StreamResult)result).GetOutputStream();
                if (stream != null)
                {
                    try
                    {
                        stream.Dispose();
                    }
                    catch (IOException err)
                    {
                        throw new XPathException("Failed while closing output file", err).WithErrorCode(DAXonErrorCode.SXRD0003);
                    }
                }

                TextWriter writer = ((StreamResult)result).GetWriter(); // Path not used, but there for safety
                if (writer != null)
                {
                    try
                    {
                        writer.Dispose();
                    }
                    catch (IOException err)
                    {
                        throw new XPathException("Failed while closing output file", err).WithErrorCode(DAXonErrorCode.SXRD0003);
                    }
                }
            }
        }
        IOutputURIResolver IOutputURIResolver.NewInstance() => NewInstance(); // covariant bridge
    }
}
