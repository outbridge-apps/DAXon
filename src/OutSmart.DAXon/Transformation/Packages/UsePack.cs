////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Xslt;
namespace OutSmart.DAXon.Transformation.Packages
{
    public class UsePack
    {
        public string packageName;
        public PackageVersionRanges ranges;
        public ILocation location;
        public UsePack(string name, string version, ILocation location)
        {
            this.packageName = name;
            this.ranges = new PackageVersionRanges(version == null ? "*" : version);
            this.location = location;
        }
    }
}