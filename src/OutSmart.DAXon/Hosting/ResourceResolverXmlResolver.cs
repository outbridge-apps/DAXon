////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace OutSmart.DAXon.Lib
{
    /// <summary>
    /// A native <see cref="System.Xml.XmlResolver"/> that resolves external XML entities (and external DTD
    /// subsets) through Saxon's <see cref="IResourceResolver"/>. It lets the .NET-native input path
    /// (XmlReaderToReceiver) keep the entity-resolution behaviour of the SAX style/source parser without the
    /// org.xml.sax EntityResolver bridge: a ResourceRequest is built for the requested URI, resolved to a
    /// Source, and the Source's stream is handed back to the XmlReader.
    /// </summary>
    internal sealed class ResourceResolverXmlResolver : XmlResolver
    {
        private readonly IResourceResolver resolver;

        public override ICredentials Credentials
        {
            set { }
        }

        private readonly OutSmart.DAXon.Core.Configuration config;
        private readonly Uri principal;
        private bool principalPending;   // the parser opens the document itself first, before anything it references
        private PublicIdProbe publicIds;

        public ResourceResolverXmlResolver(IResourceResolver resolver)
            : this(resolver, null, null)
        {
        }

        // config gates this resolver's own file fallback (the wrapped resolver is the host's and is
        // trusted); principal is the document itself when the parser opens it by system id, resolved
        // only under a restricted policy, the only one that asks.
        public ResourceResolverXmlResolver(IResourceResolver resolver, OutSmart.DAXon.Core.Configuration config, string principalSystemId)
        {
            this.resolver = resolver;
            this.config = config;
            principalPending = !string.IsNullOrEmpty(principalSystemId);
            if (OutSmart.DAXon.Internal.ResourceGate.IsRestricted(config) && !string.IsNullOrEmpty(principalSystemId))
            {
                try
                {
                    principal = ResolveUri(null, principalSystemId);
                }
                catch (Exception)
                {
                    principal = null;
                }
            }
        }

        public override Uri ResolveUri(Uri baseUri, string relativeUri)
        {
            Uri resolved;
            try
            {
                resolved = base.ResolveUri(baseUri, relativeUri);
            }
            catch (Exception)
            {
                publicIds.NotResolved(relativeUri);
                throw;
            }

            publicIds.Resolved(relativeUri, resolved);
            return resolved;
        }

        public override object GetEntity(Uri absoluteUri, string role, System.Type ofObjectToReturn)
        {
            object entity;
            try
            {
                entity = Fetch(absoluteUri, principalPending, publicIds.Take());
            }
            catch (Exception)
            {
                publicIds.NotOpened(absoluteUri);
                throw;
            }

            if (entity == null)
            {
                publicIds.NotOpened(absoluteUri);
            }

            if (principalPending)
            {
                // The input the host asked for by system id: capped as a stream it passes would be.
                principalPending = false;
                entity = OutSmart.DAXon.Internal.Streams.InputSizeLimit.Apply(entity as Stream, OutSmart.DAXon.Internal.Streams.InputSizeLimit.MaxFor(config), absoluteUri?.OriginalString, "FODC0002") ?? entity;
            }

            return entity;
        }

        private object Fetch(Uri absoluteUri, bool isPrincipal, string publicId)
        {
            ResolvedResource resolved = Ask(resolver, absoluteUri, publicId);
            if (resolved == null)
            {
                // Java's SAX parser fetches file-relative external DTDs/entities itself when no
                // user resolver claims them; a null here makes System.Xml fail the whole parse.
                if (absoluteUri != null && absoluteUri.IsFile && OutSmart.DAXon.Internal.ResourceGate.IsRestricted(config) && !absoluteUri.Equals(principal))
                {
                    string denied = OutSmart.DAXon.Internal.ResourceGate.CheckRead(config, absoluteUri.AbsoluteUri, OutSmart.DAXon.Api.ResourceKind.ExternalEntity);
                    if (denied != null)
                    {
                        throw new IOException(denied);
                    }
                }

                // The document itself is opened even when it is not there, so the failure says why (missing, a
                // directory) instead of "Cannot resolve"; an entity is probed, as System.Xml tries a PUBLIC id first.
                if (absoluteUri != null && absoluteUri.IsFile && (isPrincipal || File.Exists(absoluteUri.LocalPath)))
                {
                    return File.OpenRead(absoluteUri.LocalPath);
                }
                return null;
            }

            return StreamOf(resolved);
        }

        // The host's resolver asked for an external DTD subset or an external entity. publicId: of its declaration,
        // when the parser tried that first (see PublicIdProbe).
        internal static ResolvedResource Ask(IResourceResolver resolver, Uri absoluteUri, string publicId)
        {
            ResourceRequest request = new ResourceRequest();
            request.uri = absoluteUri?.ToString();
            request.publicId = publicId;
            request.nature = ResourceRequest.EXTERNAL_ENTITY_NATURE;
            request.purpose = ResourceRequest.ANY_PURPOSE;
            return resolver.Resolve(request);
        }

        // Its answer as the stream System.Xml reads; null when it carries none.
        internal static Stream StreamOf(ResolvedResource resolved)
        {
            if (resolved == null)
            {
                return null;
            }

            if (resolved.Stream != null)
            {
                return resolved.Stream;
            }

            // XmlReader wants a Stream; entities are small, so materialize the reader.
            return resolved.TextReader != null ? new MemoryStream(Encoding.UTF8.GetBytes(resolved.TextReader.ReadToEnd())) : null;
        }
    }

    // System.Xml tries a PUBLIC identifier as if it were a URI before the SYSTEM one, and gives a resolver one of them at
    // a time. A fetch that follows one which opened nothing is the SYSTEM one of the same declaration.
    internal struct PublicIdProbe
    {
        private string lastRelative;
        private Uri lastResolved;
        private string unopened;

        public void Resolved(string relativeUri, Uri resolved)
        {
            lastRelative = relativeUri;
            lastResolved = resolved;
        }

        public void NotResolved(string relativeUri)
        {
            unopened = relativeUri;
        }

        public void NotOpened(Uri absoluteUri)
        {
            unopened = absoluteUri != null && absoluteUri.Equals(lastResolved) ? lastRelative : null;
        }

        // The PUBLIC identifier of the declaration being fetched, if the parser tried one first.
        public string Take()
        {
            string publicId = unopened;
            unopened = null;
            return publicId;
        }
    }
}
