////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Resources
{
    /// <summary>
    /// FailedResource represents an item in a collection that could not be processed because of some error.
    /// Reading its item throws the recorded error (used with the ?on-error=fail query parameter).
    /// </summary>
    internal sealed class FailedResource : IResource
    {
        private readonly string uri;
        private readonly XPathException error;

        public string ContentType => null;

        public string ResourceURI => uri;

        public IItem Item
        {
            get
            {
                throw error;
            }
        }

        public FailedResource(string uri, XPathException error)
        {
            this.uri = uri;
            this.error = error;
        }
    }
}
