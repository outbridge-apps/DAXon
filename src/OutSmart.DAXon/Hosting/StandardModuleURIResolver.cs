////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Linq;
namespace OutSmart.DAXon.Lib
{
    internal sealed class StandardModuleURIResolver : IModuleURIResolver
    {
        Configuration config = null;

        public StandardModuleURIResolver(Configuration config)
        {
            this.config = config;
        }

        public void SetConfiguration(Configuration config)
        {
            if (this.config == null)
            {
                this.config = config;
            }
        }

        public ResolvedResource[] Resolve(string moduleURI, string baseURI, string[] locations)
        {
            if (config == null)
            {
                throw new NullReferenceException("No Configuration available in StandardModuleResolver");
            }

            ResolvedResource source = (moduleURI != null || baseURI != null) ? ResolveModuleURI(moduleURI, baseURI) : null;
            if (source != null)
            {
                return new ResolvedResource[]
                {
                    source
                };
            }

            if (locations.Length == 0)
            {
                throw new XPathException("Cannot locate module for namespace " + moduleURI).WithErrorCode("XQST0059").AsStaticError();
            }


            // One or more locations given: import modules from all these locations
            IList<ResolvedResource> moduleSources = new List<ResolvedResource>();
            foreach (string hint in locations)
            {
                ResolvedResource ss = ResolveLocationHint(baseURI, hint);
                if (ss != null)
                {
                    moduleSources.Add(ss);
                }
            }

            return moduleSources.ToArray();
        }

        // Resolve the module namespace URI itself through the configured resource resolver.
        protected ResolvedResource ResolveModuleURI(string moduleURI, string baseURI)
        {
            try
            {
                if (config != null)
                {
                    ResourceRequest rr = new ResourceRequest();
                    rr.uri = moduleURI;
                    rr.uriIsNamespace = true;
                    rr.baseUri = baseURI;
                    rr.nature = ResourceRequest.XQUERY_NATURE;
                    return config.GetResourceResolver().Resolve(rr);
                }
            }
            catch (XPathException)
            {
            }

            return null;
        }

        // Resolve a single location hint through the configured resource resolver.
        protected ResolvedResource ResolveLocationHint(string baseURI, string locationHint)
        {
            ResourceRequest rr = new ResourceRequest();
            rr.baseUri = baseURI;
            rr.relativeUri = locationHint;
            rr.nature = ResourceRequest.XQUERY_NATURE;
            try
            {
                rr.uri = ResolveURI.MakeAbsolute(rr.relativeUri, baseURI).ToString();
                return rr.Resolve(config.GetResourceResolver(), new DirectResourceResolver(config));
            }
            catch (URISyntaxException err)
            {
                throw new XPathException("Cannot resolve relative URI " + rr.relativeUri, err).WithErrorCode("XQST0059").AsStaticError();
            }
        }
    }
}