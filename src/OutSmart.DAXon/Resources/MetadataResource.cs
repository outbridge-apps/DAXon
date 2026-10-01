////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Values.Maps;
using System.Collections.Generic;

namespace OutSmart.DAXon.Resources
{
    // Port of net.sf.saxon.resource.MetadataResource (Saxon 12.9): what collection(...?metadata=yes)
    // delivers per member - a map of the member's properties, its "name", and a "fetch" function
    // that reads the content only when called.
    internal sealed class MetadataResource : IResource
    {
        private readonly IDictionary<string, IGroundedValue> properties;
        private readonly string resourceURI;
        private readonly IResource content;

        public MetadataResource(string resourceURI, IResource content, IDictionary<string, IGroundedValue> properties)
        {
            this.resourceURI = resourceURI;
            this.content = content;
            this.properties = properties;
        }

        public string ContentType => content.ContentType;

        public string ResourceURI => resourceURI;

        public IItem Item
        {
            get
            {
                var map = new DictionaryMap();
                foreach (KeyValuePair<string, IGroundedValue> entry in properties)
                {
                    map.InitialPut(entry.Key, entry.Value);
                }

                map.InitialPut("name", StringValue.MakeStringValue(resourceURI));
                var fetcher = new CallableDelegate((context, arguments) => (ISequence)content.Item ?? EmptySequence.GetInstance());
                var fetcherType = new SpecificFunctionType(new SequenceType[0], SequenceType.SINGLE_ITEM);
                map.InitialPut("fetch", new CallableFunction(0, fetcher, fetcherType));
                return map;
            }
        }
    }
}
