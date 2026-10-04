////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Internal.Regex
{
    using System.Threading;
    using OutSmart.DAXon.Internal.Caching;
    using SysRegex = System.Text.RegularExpressions.Regex;
    using RegexOptions = System.Text.RegularExpressions.RegexOptions;

    /// <summary>
    /// Java regex Pattern shim over .NET Regex. WARNING: Java and .NET regex have subtle differences in
    /// lookbehind variability, Unicode categories, and named-group syntax. Phase 3 will need careful audit
    /// for XPath conformance (the spec requires Java regex semantics).
    ///
    /// Compile() is backed by a process-wide, bounded (LFU) cache keyed by (pattern string, flags).
    /// Java's Pattern.compile is pure and Pattern is immutable (all match state lives on Matcher), so
    /// handing out a shared Pattern instance is faithful to Java semantics. The first Compile of
    /// a key builds an interpreted Regex; the second request for the same key upgrades the entry
    /// once to RegexOptions.Compiled (created exactly once, swapped atomically via a volatile field).
    /// The cache is bounded because collection-URI globs (URIQueryParameters -> JavaRegularExpression)
    /// feed user-supplied pattern text; an unbounded cache could accumulate process-wide. On eviction a
    /// later Compile of the same text just rebuilds an equivalent immutable Pattern, as Java would.
    /// </summary>
    internal sealed class Pattern
    {
        public const int CASE_INSENSITIVE = 1;
        public const int MULTILINE = 2;
        public const int DOTALL = 4;
        public const int UNICODE_CASE = 8;
        public const int COMMENTS = 16;

        private static readonly ClockCache<PatternKey, CacheEntry> Cache =
            new ClockCache<PatternKey, CacheEntry>(256);

        private readonly string _patternString;
        private readonly int _flags;
        private readonly RegexOptions _options;
        private volatile SysRegex _regex;
        private int _upgraded; // 0 = interpreted, 1 = Compiled upgrade claimed/done

        /// <summary>Current backing Regex. May be swapped (interpreted -> Compiled) at most once;
        /// both produce identical match results, so racing readers are safe.</summary>
        internal SysRegex Regex { get { return _regex; } }

        private Pattern(string pattern, int flags)
        {
            _patternString = pattern;
            _flags = flags;
            var opts = RegexOptions.None;
            // Invariant: the thread's culture must not decide what matches (tr-TR folds I to dotless i)
            if ((flags & CASE_INSENSITIVE) != 0)
                opts |= RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            if ((flags & MULTILINE) != 0)
                opts |= RegexOptions.Multiline;
            if ((flags & DOTALL) != 0)
                opts |= RegexOptions.Singleline;
            if ((flags & COMMENTS) != 0)
                opts |= RegexOptions.IgnorePatternWhitespace;
            _options = opts;
            _regex = new SysRegex(pattern, opts);
        }

        public static Pattern Compile(string pattern) => Compile(pattern, 0);

        public static Pattern Compile(string pattern, int flags)
        {
            // Invalid patterns throw from the Regex constructor inside the value factory and are
            // never cached, matching the previous (uncached) behavior.
            var entry = Cache.GetOrAdd(new PatternKey(pattern, flags),
                k => new CacheEntry(new Pattern(k.PatternString, k.Flags)));
            if (Interlocked.Increment(ref entry.Requests) >= 2)
            {
                entry.Pattern.UpgradeToCompiled();
            }
            return entry.Pattern;
        }

        private void UpgradeToCompiled()
        {
            // First caller claims the upgrade; everyone else keeps using whatever _regex currently
            // holds (interpreted until the swap lands), which is semantically identical.
            if (Interlocked.CompareExchange(ref _upgraded, 1, 0) != 0)
                return;
            _regex = new SysRegex(_patternString, _options | RegexOptions.Compiled);
        }

        public Matcher Matcher(string input) => new Matcher(this, input);
        public int Flags() => _flags;
        public override string ToString() => _patternString;

        private struct PatternKey : System.IEquatable<PatternKey>
        {
            internal readonly string PatternString;
            internal readonly int Flags;

            internal PatternKey(string patternString, int flags)
            {
                PatternString = patternString;
                Flags = flags;
            }

            public bool Equals(PatternKey other) =>
                Flags == other.Flags &&
                string.Equals(PatternString, other.PatternString, System.StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is PatternKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((PatternString != null ? PatternString.GetHashCode() : 0) * 397) ^ Flags;
                }
            }
        }

        private sealed class CacheEntry
        {
            internal readonly Pattern Pattern;
            internal int Requests; // via Interlocked; >= 2 triggers the one-time Compiled upgrade

            internal CacheEntry(Pattern pattern)
            {
                Pattern = pattern;
            }
        }
    }
}
