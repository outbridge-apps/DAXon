////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Json;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Resources
{
    // Port of net.sf.saxon.resource.JSONResource (Saxon 12.9): a collection member parsed as JSON,
    // with parse-json's defaults (strict, duplicates use-first, no escaping).
    internal sealed class JSONResource : IResource
    {
        public static readonly IResourceFactory FACTORY = new GenericResourceFactory((context, details) => new JSONResource(context, details));

        private readonly IXPathContext context;
        private readonly Configuration config;
        private readonly AbstractResourceCollection.InputDetails details;
        private readonly string href;
        private IItem item;

        public JSONResource(IXPathContext context, AbstractResourceCollection.InputDetails details)
        {
            this.context = context;
            this.config = context.GetConfiguration();
            this.details = details;
            this.href = details.resourceUri;
        }

        public string ResourceURI => href;

        public string ContentType => "application/json";

        public IItem Item
        {
            get
            {
                if (item != null)
                {
                    return item;
                }

                try
                {
                    string json = details.ObtainCharacterContent(config);
                    if (json == null)
                    {
                        return null;
                    }

                    var options = new Dictionary<string, IGroundedValue>
                    {
                        ["liberal"] = BooleanValue.FALSE,
                        ["duplicates"] = StringValue.Bmp("use-first"),
                        ["escape"] = BooleanValue.FALSE,
                    };
                    return item = ParseJsonFn.Parse(json, options, context);
                }
                catch (XPathException e)
                {
                    return details.Skip(context, e);
                }
            }
        }
    }
}
