////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Core
{
    // Version stub -- the original Version.cs is excluded but ~120 callers depend on
    // Version.platform field and other constants.
    internal static class Version
    {
        public static IPlatform platform = new DotNetPlatform();
        public static string softwareEdition = "HE";
        // xsl:product-name / xsl:vendor. Reported under OUR marks: MPL 2.0 grants no trademark
        // rights (Section 2.3), so this public distribution must not identify itself as "Saxon"
        // /"Saxonica". saxon: extension functions still resolve (the http://saxon.sf.net/ namespace
        // is kept for interop); stylesheets should feature-detect by capability, not by vendor string.
        public static string ProductName => "OutSmart DAXon";
        public static string ProductVendor => "OutSmart";
        // Engine-base version, NOT this distribution's: tracks the Saxon-HE 12.9 base for the SEF
        // guard, the fn:transform version match, xsl:product-version and the trace header.
        public static string ProductVersion => "12.9";
        // THIS distribution's own version (the engine base above stays 12.9): the project's <Version>, read
        // from the informational version it stamps on the assembly, so the two cannot drift apart.
        public static string DistributionVersion => distributionVersion;
        private static readonly string distributionVersion = ReadDistributionVersion();

        private static string ReadDistributionVersion()
        {
            System.Reflection.Assembly assembly = typeof(Version).Assembly;
            var stamped = (System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
            string text = stamped?.InformationalVersion ?? "";
            int end = text.IndexOfAny(new[] { ' ', '+' });
            text = end < 0 ? text : text.Substring(0, end);
            return text.Length > 0 ? text : assembly.GetName().Version.ToString(3);
        }

        public static string SoftwarePlatform => ".NET";
        // xsl:vendor-url. Points at THIS distribution's site (OutSmart), not the Saxon base's.
        public static string WebSiteAddress => "https://outsmartteam.com/";
        // Faithful to Java getProductVariantAndVersion(edition) = edition + " " + getProductVersion()
        // (e.g. "HE 12.9"), which backs xsl:product-version -- NOT "Saxon-HE 12.9".
        public static string GetProductVariantAndVersion(string edition) => edition + " " + ProductVersion;
    }
}
