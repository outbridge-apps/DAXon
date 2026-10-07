////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// A minimal .NET IPlatform so Configuration/Processor can construct. The real
// JavaPlatform (1075 lines, heavy deps) is excluded; this provides sane values for the construction path.
// Usings mirror Platform.cs so the IPlatform method signatures bind to the same types.
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Resources;
using System.Globalization;
using System.Text;
using System.IO;

namespace OutSmart.DAXon.Core
{
    internal sealed class DotNetPlatform : IPlatform
    {
        public string PlatformSuffix => "N";
        public string DefaultCountry
        {
            get
            {
                try { return RegionInfo.CurrentRegion.TwoLetterISORegionName; }
                catch { return "US"; }
            }
        }
        public IIDynamicLoader DefaultDynamicLoader => NoDynamicLoader.Instance;

        // ---- construction path: sane values / no-ops ----
        public void Initialize(Configuration config)
        {
            // Faithful to JavaPlatform.initialize: install the default collection finder used to
            // dereference fn:collection / fn:uri-collection URIs.
            config.CollectionFinder = new OutSmart.DAXon.Resources.StandardCollectionFinder();
        }
        public bool IsDotNet() => true;
        public string GetDefaultLanguage() => CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        // Backed by embedded resources (upstream data/*.xml via the csproj): casevariants/categories/
        // unicodeBlocks — regex case-blind matching, \p{} categories and \p{Is...} blocks need them.
        public Stream LocateResource(string filename, IList<string> messages)
        {
            var asm = typeof(DotNetPlatform).Assembly;
            foreach (var n in asm.GetManifestResourceNames())
            {
                if (n.EndsWith("." + filename, StringComparison.OrdinalIgnoreCase) || string.Equals(n, filename, StringComparison.OrdinalIgnoreCase))
                {
                    var s = asm.GetManifestResourceStream(n);
                    if (s != null) // IO-removal Stage B: returns System.IO.Stream directly
                    {
                        return s;
                    }
                }
            }
            messages?.Add("Resource not found in embedded manifest: " + filename);
            return null;
        }
        public IModuleURIResolver MakeStandardModuleURIResolver(Configuration config) => new OutSmart.DAXon.Lib.StandardModuleURIResolver(config);
        // A collation can supply xsl:key/collation keys iff equal-under-collation implies equal keys. The
        // CompareInfo locale collations qualify, and so do the algorithmic collators whose GetCollationKey is a
        // real value (codepoint = the string itself; html5-ascii-case-blind = case-normalized form). Only the
        // rule-based substring matcher returns null keys and must be excluded.
        public bool CanReturnCollationKeys(IStringCollator collation)
            => (collation is SimpleCollation sc && sc.Comparator is CompareInfoComparer)
               || collation is CodepointCollator
               || collation is HTML5CaseBlindCollator
               || collation is AlphanumericCollator;
        public bool JAXPStaticContextCheck(RetainedStaticContext retainedStaticContext, IStaticContext sc) => false;

        // ---- collation factory (ported from net.sf.saxon.java.JavaCollationFactory.makeCollation) ----
        // .NET uses System.Globalization.CompareInfo for locale (UCA-by-lang) collation instead of
        // java.text.Collator. This is a DOCUMENTED known divergence from Java Saxon: .NET's CLDR sort
        // tables differ from the JRE's, so locale-sensitive (lang/strength) ordering is NOT guaranteed
        // byte-identical to Java. The algorithmic collations (codepoint, html5-ascii-case-blind,
        // alphanumeric) ARE byte-identical because they are pure algorithms with no locale dependency.
        // Routes for parameters that have no native .NET twin (class=, rules=, case-order/caseFirst)
        // throw a clearly-labelled XPathException rather than silently mis-collating.
        public IStringCollator MakeCollation(Configuration config, Properties props, string uri)
        {
            CompareInfoComparer comparer = null;

            // class= : Java loads a user Comparator class. No .NET equivalent without dynamic class loading.
            string classAtt = props.GetProperty("class");
            if (classAtt != null)
            {
                throw new XPathException("Collation property class=" + classAtt + " is not supported on the .NET platform", "FOCH0002");
            }

            // rules= : Java builds a RuleBasedCollator. Not ported (the RuleBasedCollator here is a stub).
            string rulesAtt = props.GetProperty("rules");
            if (rulesAtt != null)
            {
                throw new XPathException("Collation property 'rules' (RuleBasedCollator) is not supported on the .NET platform", "FOCH0002");
            }

            // lang= : map to CultureInfo.CompareInfo. Absent -> current culture.
            string langAtt = props.GetProperty("lang");
            CompareInfo ci;
            if (langAtt != null)
            {
                ci = GetCultureInfo(langAtt).CompareInfo;
            }
            else
            {
                ci = CultureInfo.CurrentCulture.CompareInfo;
            }
            comparer = new CompareInfoComparer(ci, CompareOptions.None);

            // strength=primary|secondary|tertiary|identical  (mirrors Java Collator.setStrength)
            string strengthAtt = props.GetProperty("strength");
            if (strengthAtt != null)
            {
                switch (strengthAtt)
                {
                    case "primary":
                        comparer.UsePrimaryStrength();
                        break;
                    case "secondary":
                        comparer.Options = CompareOptions.IgnoreCase;
                        break;
                    case "tertiary":
                        comparer.Options = CompareOptions.None;
                        break;
                    case "identical":
                        comparer.Ordinal = true;
                        break;
                    default:
                        throw new XPathException("strength must be primary, secondary, tertiary, or identical", "FOCH0002");
                }
            }

            // ignore-width / ignore-case / ignore-modifiers (only honoured when strength is absent, as in Java)
            string ignore = props.GetProperty("ignore-width");
            if (ignore != null)
            {
                if (ignore.Equals("yes") && strengthAtt == null)
                {
                    comparer.Options = CompareOptions.None;
                }
                else if (ignore.Equals("no")) { /* no-op */ }
                else
                {
                    throw new XPathException("ignore-width must be yes or no", "FOCH0002");
                }
            }
            ignore = props.GetProperty("ignore-case");
            if (ignore != null && strengthAtt == null)
            {
                switch (ignore)
                {
                    case "yes": comparer.Options = CompareOptions.IgnoreCase; break;
                    case "no": break;
                    default: throw new XPathException("ignore-case must be yes or no", "FOCH0002");
                }
            }
            ignore = props.GetProperty("ignore-modifiers");
            if (ignore != null)
            {
                if (ignore.Equals("yes") && strengthAtt == null)
                {
                    comparer.UsePrimaryStrength();   // Java: setStrength(PRIMARY)
                }
                else if (ignore.Equals("no")) { /* no-op */ }
                else
                {
                    throw new XPathException("ignore-modifiers must be yes or no", "FOCH0002");
                }
            }
            // decomposition and ignore-symbols: not separately configurable through CompareInfo -> ignored (as Java ignores ignore-symbols)

            IStringCollator stringCollator = new SimpleCollation(uri, comparer);

            // case-order / caseFirst : as in Java, force the base collator to ignore case differences
            // (setStrength(SECONDARY)) so the CaseFirstCollator wrapper decides the case order.
            string caseOrder = props.GetProperty("case-order");
            if (caseOrder != null && caseOrder != "#default")
            {
                comparer.Options = CompareOptions.IgnoreCase;
                stringCollator = OutSmart.DAXon.Expressions.Sorting.CaseFirstCollator.MakeCaseOrderedCollator(uri, stringCollator, caseOrder);
            }

            // alternate=blanked|shifted: punctuation and symbols do not count (UCA variable weighting). Saxon-HE's Java
            // collator ignores the keyword; the F&O examples of the substring functions need it, and passed only because
            // primary strength used to drop every symbol.
            string alternate = props.GetProperty("alternate");
            if ((alternate == "blanked" || alternate == "shifted") && !comparer.Ordinal)
            {
                comparer.Options |= CompareOptions.IgnoreSymbols;
            }

            // alphanumeric=yes|codepoint  (pure algorithm; byte-identical to Java)
            string alphanumeric = props.GetProperty("alphanumeric");
            if (alphanumeric != null && alphanumeric != "no")
            {
                switch (alphanumeric)
                {
                    case "yes":
                        stringCollator = new AlphanumericCollator(stringCollator);
                        break;
                    case "codepoint":
                        stringCollator = new AlphanumericCollator(CodepointCollator.GetInstance());
                        break;
                    default:
                        throw new XPathException("alphanumeric must be yes, no, or codepoint", "FOCH0002");
                }
            }

            return stringCollator;
        }

        // Get a CultureInfo given a language code in XML (BCP-47-ish) format. Mirrors
        // JavaCollationFactory.getLocale but builds a .NET CultureInfo. Falls back to the invariant
        // culture if the OS does not know the code (so the collation still constructs).
        //
        // Only OS-known names may reach GetCultureInfo: on Windows 10+/Server 2016+ it does not
        // throw for an unknown well-formed BCP-47 tag but SYNTHESIZES a culture, and every name is
        // interned process-wide forever (~1.1 KB/tag measured). lang= arrives in runtime collation
        // URIs (fn:compare#3, xsl:sort AVT), so without this gate distinct tags in input data grow
        // the process without bound. Synthetic cultures collate like the invariant culture anyway.
        private static readonly Lazy<HashSet<string>> knownCultureNames = new Lazy<HashSet<string>>(() =>
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CultureInfo c in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                names.Add(c.Name);
            }

            return names;
        });

        private static CultureInfo GetCultureInfo(string lang)
        {
            string tag = lang.Replace('_', '-');
            if (knownCultureNames.Value.Contains(tag))
            {
                return CultureInfo.GetCultureInfo(tag);
            }

            int hyphen = tag.IndexOf('-');
            string language = hyphen < 1 ? tag : tag.Substring(0, hyphen);
            return knownCultureNames.Value.Contains(language) ? CultureInfo.GetCultureInfo(language) : CultureInfo.InvariantCulture;
        }

        // Same gated lookup for callers that need "known or nothing" (Numberer selection):
        // null for tags the OS doesn't know — never a synthesized culture.
        internal static CultureInfo TryGetKnownCulture(string lang)
        {
            CultureInfo culture = GetCultureInfo(lang);
            return culture.Equals(CultureInfo.InvariantCulture) ? null : culture;
        }

        // .NET CAN return real collation keys (CompareInfo.GetSortKey) for SimpleCollation instances
        // whose comparator is a CompareInfoComparer.
        public IAtomicMatchKey GetCollationKey(SimpleCollation namedCollation, string value)
        {
            if (namedCollation.Comparator is CompareInfoComparer cic)
            {
                if (cic.Ordinal)
                {
                    // identical strength: the value's own UTF-16 bytes are the key (equal strings -> equal keys)
                    return new Base64BinaryValue(Encoding.BigEndianUnicode.GetBytes(value));
                }
                SortKey sk;
                try
                {
                    sk = cic.CompareInfo.GetSortKey(cic.Visible(value), cic.Options);
                }
                catch (ArgumentException)
                {
                    // .NET Framework cannot make the key of a long string (45 million characters) and says the flags are invalid
                    throw new XPathException("The collation cannot make a collation key of a string of " + value.Length + " characters", "FOCH0004");
                }

                return new Base64BinaryValue(sk.KeyData);
            }
            // Fallback: codepoint-equal key (equal strings -> equal keys). Not a locale sort key, but
            // satisfies the collation-key contract for any non-CompareInfo comparator.
            return new Base64BinaryValue(Encoding.BigEndianUnicode.GetBytes(value));
        }

        // UCA collator: routed through the same CompareInfo path as lang= collations. The
        // StandardCollationURIResolver translates UCA query params (strength/numeric/caseFirst) into the
        // Properties consumed by MakeCollation, so we let that path build it. Returning null here makes
        // the resolver fall through to its param-translation + MakeCollation branch (the desired behaviour).
        public IStringCollator MakeUcaCollator(string uri, Configuration config) => null;
        // Always the Saxon-native regex engine (ARegularExpression). Java's "!"-flag selects java.util.regex
        // instead; that engine has no twin here, so the flag is stripped (XPath regex semantics ARE the
        // Saxon engine's native dialect, so this stays spec-conformant).
        public IRegularExpression CompileRegularExpression(Configuration config, UnicodeString regex, string flags, string hostLanguage, IList<string> warnings)
        {
            string f = flags == null ? "" : flags.Replace("!", "");
            int semi = f.IndexOf(';');
            if (semi >= 0) // implementation-defined engine selectors - not applicable
            {
                f = f.Substring(0, semi);
            }
            return new ARegularExpression(regex, f, hostLanguage, warnings, config);
        }
        // A type named in the .NET-type namespace: classes are not loaded by name here, so it is an unknown type.
        public ExternalObjectType GetExternalObjectType(Configuration config, NamespaceUri uri, string localName)
        {
            throw new XPathException("Unknown type Q{" + uri + "}" + localName + ": external object types are not available on this platform", "XPST0051").AsStaticError();
        }

        // IComparer<string> backed by a .NET CompareInfo + CompareOptions. This is the .NET analogue of
        // java.text.Collator that SimpleCollation wraps. The CompareInfo/Options are exposed so the
        // platform can build a faithful collation key (GetSortKey) for the same collation. 'Ordinal'
        // models Java's IDENTICAL strength (full code-unit comparison).
        internal sealed class CompareInfoComparer : IComparer<string>
        {
            private CompareOptions options;

            public CompareInfo CompareInfo { get; }
            public bool Ordinal { get; set; }

            // Setting the options sets another strength, as Java's setStrength does for case-order: spaces and dashes
            // count again.
            public CompareOptions Options
            {
                get => options;
                set
                {
                    options = value;
                    DropsSpacesAndDashes = false;
                }
            }

            // Primary strength as Java's collator has it: spaces and dashes are dropped (PrimaryIgnorables), other
            // punctuation counts. IgnoreSymbols dropped all of it: "C#" equalled "C" and "1.5" equalled "15".
            public bool DropsSpacesAndDashes { get; private set; }

            public CompareInfoComparer(CompareInfo compareInfo, CompareOptions options)
            {
                CompareInfo = compareInfo;
                Options = options;
            }

            public void UsePrimaryStrength()
            {
                Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
                DropsSpacesAndDashes = true;
            }

            // The text the comparison sees.
            public string Visible(string s)
            {
                return DropsSpacesAndDashes ? PrimaryIgnorables.Strip(s) : s;
            }

            public int Compare(string x, string y)
            {
                if (Ordinal)
                {
                    return string.CompareOrdinal(x, y);
                }
                return CompareInfo.Compare(Visible(x), Visible(y), Options);
            }
        }
    }
}
