////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Xslt;namespace OutSmart.DAXon.Transformation.Packages
{
    public class VersionedPackageName
    {
        public string packageName;
        public PackageVersion packageVersion;
        public VersionedPackageName(string packageName, PackageVersion version)
        {
            this.packageName = packageName;
            this.packageVersion = version;
        }

        public VersionedPackageName(string packageName, string version)
        {
            this.packageName = packageName;
            this.packageVersion = new PackageVersion(version);
        }

        public override string ToString()
        {
            return packageName + " (" + packageVersion.ToString() + ")";
        }

        public virtual bool EqualsIgnoringSuffix(VersionedPackageName other)
        {
            return packageName.Equals(other.packageName) && packageVersion.EqualsIgnoringSuffix(other.packageVersion);
        }

        public override bool Equals(object obj)
        {
            return obj is VersionedPackageName && packageName.Equals(((VersionedPackageName)obj).packageName) && packageVersion.Equals(((VersionedPackageName)obj).packageVersion);
        }

        public override int GetHashCode()
        {
            return packageName.GetHashCode() ^ packageVersion.GetHashCode();
        }
    }
}