////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using System;
namespace OutSmart.DAXon.Lib
{
    internal sealed class ResourceResolverDelegate : IResourceResolver
    {
        private readonly Func<ResourceRequest, ResolvedResource> lambda;
        public ResourceResolverDelegate(Func<ResourceRequest, ResolvedResource> lambda)
        {
            this.lambda = lambda;
        }

        public ResolvedResource Resolve(ResourceRequest request)
        {
            try
            {
                return lambda(request);
            }
            catch (UncheckedXPathException err)
            {
                throw err.GetXPathException();
            }
        }
    }
}