////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Core;
namespace OutSmart.DAXon.Lib
{
    public class ProtocolRestrictor
    {
        private readonly Func<URI, bool> predicate;
        private readonly string originalRule;
        public ProtocolRestrictor(string value)
        {
            if (value == null)
                throw new NullReferenceException();
            this.originalRule = value;
            value = value.Trim();
            if (value.Equals("all"))
            {

                // Allow all URIs
                predicate = (uri) => true;
            }
            else
            {
                IList<Func<URI, bool>> permitted = new List<Func<URI, bool>>();
                string[] tokens = value.SplitRegex(",\\s*");
                foreach (string token in tokens)
                {
                    if (token.StartsWith("jar:", StringComparison.Ordinal) && token.Length > 4)
                    {
                        string subScheme = token.Substring(4).ToLowerInvariant();
                        permitted.Add((uri) => Scheme(uri).Equals("jar") && SchemeSpecificPart(uri).ToLowerInvariant().StartsWith(subScheme, StringComparison.Ordinal));
                    }
                    else
                    {
                        permitted.Add((uri) => Scheme(uri).Equals(token));
                    }
                }

                predicate = (uri) =>
                {
                    foreach (Func<URI, bool> pred in permitted)
                    {
                        if (pred(uri))
                        {
                            return true;
                        }
                    }

                    return false;
                };
            }
        }

        public virtual bool Test(URI uri)
        {
            return predicate(uri);
        }

        public override string ToString()
        {
            return originalRule;
        }

        public virtual IResourceResolver AsResourceResolver(IResourceResolver existing)
        {
            return new RestrictedResourceResolver(this, existing);
        }

        // The following methods are extracted to enable the C# transpiler to recognise what it needs to do...
        private static string Scheme(URI uri)
        {
            return uri.Scheme;
        }

        private static string SchemeSpecificPart(URI uri)
        {
            return uri.SchemeSpecificPart;
        }

        internal sealed class RestrictedResourceResolver : IResourceResolver
        {
            private readonly ProtocolRestrictor protocolRestrictor;
            private readonly IResourceResolver nextResolver;
            public RestrictedResourceResolver(ProtocolRestrictor pr, IResourceResolver rr)
            {
                this.protocolRestrictor = pr;
                this.nextResolver = rr;
            }

            public ResolvedResource Resolve(ResourceRequest request)
            {
                if (protocolRestrictor.Test(URI.Create(request.uri)))
                {
                    return nextResolver.Resolve(request);
                }
                else
                {
                    throw new XPathException("Access to URI " + request.uri + " has been prohibited");
                }
            }
        }
    }
}