////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Text;
namespace OutSmart.DAXon.Xslt
{
    using Regex = System.Text.RegularExpressions.Regex;   // OutSmart.DAXon.Regex namespace shadows the BCL type

    // Faithful port of net.sf.saxon.style.PackageVersionRanges (Saxon 12.9). Was a hollow compat stub
    // with Contains => FALSE — no version range ever matched, so PackageLibrary.FindPackage returned
    // null and every xsl:use-package raised XTSE3000.
    // A set of package version ranges per http://www.w3.org/TR/xslt-30/#package-versions:
    // '*' | version | version'.*' | version'+' | 'to 'version | version' to 'version, comma-separated.
    public class PackageVersionRanges
    {
        private readonly List<PackageVersionRange> ranges;

        /// <summary>
        /// Generate a set of package version ranges from the comma-separated grammar.
        /// </summary>
        public PackageVersionRanges(string s)
        {
            ranges = new List<PackageVersionRange>();
            string trimmed = Whitespace.Normalize(s);
            if (trimmed.Equals("*"))
            {
                ranges.Add(new PackageVersionRange());
            }
            else
            {
                foreach (string p in Regex.Split(trimmed, @"\s?,\s?"))
                {
                    ranges.Add(new PackageVersionRange(p));
                }
            }
        }

        /// <summary>
        /// Test whether a given package version lies within any of the ranges.
        /// </summary>
        public virtual bool Contains(PackageVersion version)
        {
            foreach (PackageVersionRange r in ranges)
            {
                if (r.Contains(version))
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            if (ranges.Count == 1)
            {
                return ranges[0].ToString();
            }
            else
            {
                StringBuilder buffer = new StringBuilder(256);
                foreach (PackageVersionRange r in ranges)
                {
                    buffer.Append(r.ToString()).Append(',');
                }

                buffer.Length = buffer.Length - 1;
                return buffer.ToString();
            }
        }

        private sealed class PackageVersionRange
        {
            private readonly string display;
            private readonly PackageVersion low;
            private readonly PackageVersion high;
            private readonly bool all;
            private readonly bool prefix;

            public PackageVersionRange(string s)
            {
                display = s;
                if (s.EndsWith("+", StringComparison.Ordinal))
                {
                    low = new PackageVersion(s.Replace("+", ""));
                    high = PackageVersion.MAX_VALUE;
                }
                else if (Regex.IsMatch(s, @"^to\s.*$"))
                {
                    low = PackageVersion.ZERO;
                    string end = s.Substring(3);
                    if (end.EndsWith(".*", StringComparison.Ordinal))
                    {
                        high = new PackageVersion(end.Substring(0, end.Length - 2));
                        prefix = true;
                    }
                    else
                    {
                        high = new PackageVersion(end);
                    }
                }
                else if (Regex.IsMatch(s, @"^.*\s?to\s+.*$"))
                {
                    string[] range = Regex.Split(s, @"\s*to\s+");
                    if (range.Length > 2)
                    {
                        throw new XPathException("Invalid version range:" + s, "XTSE0020");
                    }

                    low = new PackageVersion(range[0]);
                    string end = range[1];
                    if (end.EndsWith(".*", StringComparison.Ordinal))
                    {
                        high = new PackageVersion(end.Substring(0, end.Length - 2));
                        prefix = true;
                    }
                    else
                    {
                        high = new PackageVersion(end);
                    }
                }
                else if (s.EndsWith(".*", StringComparison.Ordinal))
                {
                    prefix = true;
                    low = new PackageVersion(s.Substring(0, s.Length - 2));
                }
                else
                {
                    low = new PackageVersion(s);
                    high = low;
                }
            }

            /// <summary>
            /// Create a range representing "all" packages
            /// </summary>
            public PackageVersionRange()
            {
                display = "*";
                all = true;
            }

            internal bool Contains(PackageVersion v)
            {
                if (all)
                {
                    return true;
                }
                else if (prefix)
                {
                    if (high != null)
                    {
                        return low.CompareTo(v) <= 0
                            && (v.CompareTo(high) <= 0 || high.IsPrefix(v));
                    }
                    else
                    {
                        return low.IsPrefix(v);
                    }
                }
                else
                {
                    return low.CompareTo(v) <= 0 && v.CompareTo(high) <= 0;
                }
            }

            public override string ToString()
            {
                return display;
            }
        }
    }
}
