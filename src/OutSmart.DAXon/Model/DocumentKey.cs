////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Model
{
    public class DocumentKey
    {
        // As upstream's new File("a").equals(new File("A")): file URIs are keyed case-blind on Windows.
        public static readonly bool CASE_BLIND_FILES = Path.DirectorySeparatorChar == '\\';
        private string displayValue;
        private string normalizedValue;
        private string packageName = "";
        private PackageVersion packageVersion = PackageVersion.ONE;

        public virtual string AbsoluteURI => displayValue;
        public DocumentKey(string uri)
        {
            if (uri == null)
                throw new NullReferenceException();
            this.displayValue = uri;
            this.normalizedValue = NormalizeURI(uri);
        }

        public DocumentKey(string uri, string packageName, PackageVersion version)
        {
            if (uri == null)
                throw new NullReferenceException();
            this.displayValue = uri;
            this.normalizedValue = NormalizeURI(uri);
            this.packageName = packageName == null ? "" : packageName;
            this.packageVersion = version;
        }

        public override string ToString()
        {
            return displayValue;
        }

        public override bool Equals(object obj)
        {
            return obj is DocumentKey && normalizedValue.Equals(((DocumentKey)obj).normalizedValue) && packageName.Equals(((DocumentKey)obj).packageName) && packageVersion.Equals(((DocumentKey)obj).packageVersion);
        }

        public override int GetHashCode()
        {
            return normalizedValue.GetHashCode();
        }

        public static string NormalizeURI(string uri)
        {
            if (uri == null)
            {
                return null;
            }

            if (uri.StartsWith("FILE:", StringComparison.Ordinal))
            {
                uri = "file:" + uri.Substring(5);
            }

            if (uri.StartsWith("file:", StringComparison.Ordinal))
            {
                if (uri.StartsWith("file:///", StringComparison.Ordinal))
                {
                    uri = "file:/" + uri.Substring(8);
                }

                if (uri.StartsWith("file:/", StringComparison.Ordinal))
                {

                    // Bug 6565: No longer use getCanonicalPath() to remove any "." and ".." path segments, for performance reasons
                    try
                    {
                        string cpath = GetCanonicalPath(uri);
                        uri = "file:" + cpath;
                    }
                    catch (Exception ioe)
                    {
                    }
                }

                if (CASE_BLIND_FILES)
                {
                    uri = uri.ToLowerInvariant();
                }
            }

            return uri;
        }

        private static string GetCanonicalPath(string uri)
        {
            return Path.GetFullPath(uri.Substring(6));
        }
    }
}