////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using System;
using System.IO;

namespace OutSmart.DAXon.Resources
{
    /// <summary>
    /// Default implementation of the <see cref="ICollectionFinder"/> interface (upstream
    /// lib/StandardCollectionFinder.findCollection). Recognises file: directories (DirectoryCollection)
    /// with optional URI query parameters, ZIP archives (JarCollection) and XML catalogs of URIs
    /// (CatalogCollection).
    /// </summary>
    internal sealed class StandardCollectionFinder : ICollectionFinder
    {
        public IResourceCollection FindCollection(IXPathContext context, string collectionURI)
        {
            AbstractResourceCollection.CheckNotNull(collectionURI, context);

            // Split off URI query parameters (?select=...;recurse=yes etc.)
            Functions.URIQueryParameters @params = null;
            int q = collectionURI.IndexOf('?');
            if (q >= 0)
            {
                @params = new Functions.URIQueryParameters(collectionURI.Substring(q + 1), context.GetConfiguration());
                collectionURI = collectionURI.Substring(0, q);
            }

            Uri resolvedURI;
            try
            {
                resolvedURI = new Uri(collectionURI, UriKind.Absolute);
            }
            catch (Exception e)
            {
                throw new XPathException("Invalid collection URI " + collectionURI + " passed to collection() function: " + e.Message, "FODC0004", context);
            }

            string denied = OutSmart.DAXon.Internal.ResourceGate.IsRestricted(context.GetConfiguration())
                ? OutSmart.DAXon.Internal.ResourceGate.CheckRead(context.GetConfiguration(), resolvedURI.AbsoluteUri, OutSmart.DAXon.Api.ResourceKind.Collection)
                : null;
            if (denied != null)
            {
                throw new XPathException(denied, "FODC0002", context);
            }

            if (resolvedURI.IsFile)
            {
                // Java's new File(URI) throws for a URI with a fragment ("##invalid" resolves to
                // base+empty-path+fragment, which .NET LocalPath silently maps to the base DIRECTORY —
                // turning an invalid collection URI into a directory listing).
                if (!string.IsNullOrEmpty(resolvedURI.Fragment))
                {
                    throw new XPathException("Invalid collection URI " + collectionURI + " (URI has a fragment component)", "FODC0004", context);
                }

                string path = resolvedURI.LocalPath;
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    throw new XPathException("The file or directory " + resolvedURI + " does not exist", "FODC0002", context);
                }

                if (Directory.Exists(path))
                {
                    return new DirectoryCollection(context.GetConfiguration(), collectionURI, new DirectoryInfo(path), @params);
                }
            }

            // Anything else is a ZIP archive when its URI says so, and otherwise a catalog of URIs.
            string zipPattern = context.GetConfiguration().GetConfigurationProperty(Feature<string>.ZIP_URI_PATTERN)
                ?? "^jar:|\\.jar$|\\.zip$|\\.docx$|\\.xlsx$";
            if (Regex.ARegularExpression.Compile(zipPattern, "").ContainsMatch(Text.StringView.Of(collectionURI).Tidy()))
            {
                return new JarCollection(context, collectionURI, @params);
            }

            return new CatalogCollection(context.GetConfiguration(), collectionURI, @params);
        }
    }
}
