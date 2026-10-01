////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using JMatcher = OutSmart.DAXon.Internal.Regex.Matcher;
using JPattern = OutSmart.DAXon.Internal.Regex.Pattern;

namespace OutSmart.DAXon.Regex
{
    // Faithful port of net.sf.saxon.regex.JavaRegularExpression, backed by Internal.Regex.Pattern/Matcher
    // (System.Text.RegularExpressions underneath). Consumer: URIQueryParameters ?match= collection-URI filters.
    //
    // Flag mapping notes (Java letters have their java.util.regex meanings here, NOT XPath meanings):
    //   d UNIX_LINES       -> no-op: .NET ^/$/. already treat only \n as a line terminator, which IS
    //                         Java's UNIX_LINES behavior (upstream always sets it as the baseline).
    //   m MULTILINE        -> compat Pattern.MULTILINE -> RegexOptions.Multiline
    //   i CASE_INSENSITIVE -> compat Pattern.CASE_INSENSITIVE -> RegexOptions.IgnoreCase
    //   s DOTALL           -> compat Pattern.DOTALL -> RegexOptions.Singleline
    //   x COMMENTS         -> compat Pattern.COMMENTS -> RegexOptions.IgnorePatternWhitespace
    //   u UNICODE_CASE     -> compat Pattern.UNICODE_CASE (no RegexOptions bit: .NET case folding is
    //                         Unicode-aware by default; bit kept so it participates in the cache key)
    //   q LITERAL          -> emulated by Regex.Escape on the whole pattern before compilation
    //   c CANON_EQ         -> NOT supported by System.Text.RegularExpressions; fails loud (XPathException)
    internal sealed class JavaRegularExpression : IRegularExpression
    {
        private readonly JPattern pattern;
        private readonly string javaRegex;
        private readonly string originalFlags;
        private readonly int flagBits;

        /// <summary>
        /// Get the flags used at the time the regular expression was compiled (the original flag string).
        /// </summary>
        public string Flags => originalFlags;

        /// <summary>
        /// Create a regular expression from an already-translated Java regex.
        /// </summary>
        /// <param name="javaRegex">the regular expression in Java notation</param>
        /// <param name="flags">the user-specified flags (prior to any semicolon)</param>
        public JavaRegularExpression(UnicodeString javaRegex, string flags)
        {
            this.originalFlags = flags ?? "";
            bool literal;
            this.flagBits = ParseFlags(this.originalFlags, out literal);
            this.javaRegex = javaRegex.ToString();
            // Java Pattern.LITERAL ('q'): the entire pattern is treated as a literal sequence.
            // .NET has no equivalent option; escaping every metacharacter is semantically identical.
            string netRegex = literal
                ? System.Text.RegularExpressions.Regex.Escape(this.javaRegex)
                : this.javaRegex;
            try
            {
                pattern = JPattern.Compile(netRegex, flagBits);
            }
            catch (ArgumentException e)
            {
                // compat Pattern.Compile surfaces invalid patterns as ArgumentException from the
                // System.Text.RegularExpressions.Regex ctor (Java: PatternSyntaxException).
                throw new XPathException("Incorrect syntax for native regular expression: " + e.Message, "FORX0002");
            }
        }

        /// <summary>
        /// Analyze an input string in support of xsl:analyze-string / fn:analyze-string.
        /// </summary>
        public IRegexIterator Analyze(UnicodeString input)
        {
            // Faithful port would be: return new JRegexIterator(input.ToString(), pattern);
            // but poc\output\full\regex\JRegexIterator.cs is <Compile Remove>d and IRegexIterator's
            // matching/non-matching alternation plus group bookkeeping is too large to inline here.
            // Consumers that would need this: functions\AnalyzeStringFn.cs (fn:analyze-string) and
            // functions\FormatDate.cs - only when the ";j" native-regex flag selects this engine.
            // Throwing (not hollow-returning) per repo policy.
            throw new NotImplementedException(
                "JavaRegularExpression.Analyze requires JRegexIterator (excluded); needed by fn:analyze-string (AnalyzeStringFn) and FormatDate when the ;j native regex engine is selected");
        }

        /// <summary>
        /// Determine whether the regular expression contains a match for a given string
        /// (Java semantics: Matcher.find()).
        /// </summary>
        public bool ContainsMatch(UnicodeString input)
        {
            return pattern.Matcher(input.ToString()).Find();
        }

        /// <summary>
        /// Determine whether the regular expression matches a given string in its entirety
        /// (Java semantics: Matcher.matches()).
        /// </summary>
        public bool Matches(UnicodeString input)
        {
            return pattern.Matcher(input.ToString()).Matches();
        }

        /// <summary>
        /// Replace all substrings of a supplied input string that match the regular expression
        /// with a replacement string ($N group references are passed through to the engine).
        /// </summary>
        public UnicodeString Replace(UnicodeString input, UnicodeString replacement)
        {
            JMatcher matcher = pattern.Matcher(input.ToString());
            try
            {
                return StringView.Tidy(matcher.ReplaceAll(replacement.ToString()));
            }
            catch (ArgumentException e)
            {
                // Java throws IndexOutOfBoundsException on a bad group reference -> FORX0004.
                throw new XPathException(e.Message, "FORX0004");
            }
        }

        /// <summary>
        /// Replace matching substrings via a callback. Upstream Java throws unconditionally:
        /// "fn:replace#5 is not supported with the Java regex engine" - ported as-is.
        /// </summary>
        public UnicodeString ReplaceWith(UnicodeString input, Func<UnicodeString, UnicodeString[], UnicodeString> replacement)
        {
            throw new XPathException("fn:replace#5 is not supported with the Java regex engine");
        }

        /// <summary>
        /// Use this regular expression to tokenize an input string (fn:tokenize semantics).
        /// </summary>
        public IAtomicIterator Tokenize(UnicodeString input)
        {
            if (input.IsEmpty())
            {
                return EmptyIterator.OfAtomic();
            }
            return new JTokenIteratorImpl(input.ToString(), pattern);
        }

        /// <summary>
        /// Parse the flag letters (java.util.regex meanings, see class comment) into compat
        /// Pattern bits. Mirrors upstream setFlags(); unknown letters -> FORX0001.
        /// </summary>
        private static int ParseFlags(string inFlags, out bool literal)
        {
            literal = false;
            int flags = 0; // upstream baseline UNIX_LINES is the .NET default behavior (see class comment)
            for (int i = 0; i < inFlags.Length; i++)
            {
                char c = inFlags[i];
                switch (c)
                {
                    case 'd':
                        // UNIX_LINES: .NET already treats only \n as line terminator - no-op.
                        break;
                    case 'm':
                        flags |= JPattern.MULTILINE;
                        break;
                    case 'i':
                        flags |= JPattern.CASE_INSENSITIVE;
                        break;
                    case 's':
                        flags |= JPattern.DOTALL;
                        break;
                    case 'x':
                        flags |= JPattern.COMMENTS; // note, this enables comments as well as whitespace
                        break;
                    case 'u':
                        flags |= JPattern.UNICODE_CASE;
                        break;
                    case 'q':
                        literal = true; // Pattern.LITERAL emulated via Regex.Escape in the ctor
                        break;
                    case 'c':
                        // Java Pattern.CANON_EQ (canonical equivalence) has no .NET counterpart;
                        // silently ignoring it would change match semantics, so fail loud.
                        throw new XPathException(
                            "Regular expression flag 'c' (CANON_EQ) is not supported by the .NET regex engine", "FORX0001");
                    default:
                        throw new XPathException("Invalid character '" + c + "' in regular expression flags", "FORX0001");
                }
            }
            return flags;
        }

        /// <summary>
        /// Ask whether the regular expression is using platform-native syntax (Java or .NET), or XPath syntax
        /// </summary>
        public bool IsPlatformNative()
        {
            return true;
        }

        /// <summary>
        /// Faithful inline port of net.sf.saxon.regex.JTokenIterator (the transpiled copy at
        /// poc\output\full\regex\JTokenIterator.cs is excluded AND carries a subSequence->Substring
        /// conversion bug: Substring(prevEnd, matcher.Start()) passes an end index as a length).
        /// Yields the substrings BETWEEN matches, including a trailing token after the last match.
        /// </summary>
        private sealed class JTokenIteratorImpl : IAtomicIterator
        {
            private readonly string input;
            private readonly JMatcher matcher;
            private int prevEnd; // -1 => exhausted

            internal JTokenIteratorImpl(string input, JPattern pattern)
            {
                this.input = input;
                this.matcher = pattern.Matcher(input);
                this.prevEnd = 0;
            }

            public AtomicValue Next()
            {
                if (prevEnd < 0)
                {
                    return null;
                }
                string current;
                if (matcher.Find())
                {
                    current = input.Substring(prevEnd, matcher.Start() - prevEnd);
                    prevEnd = matcher.End();
                }
                else
                {
                    current = input.Substring(prevEnd);
                    prevEnd = -1;
                }
                return StringValue.MakeStringValue(current);
            }

            IItem ISequenceIterator.Next()
            {
                return Next();
            }

            public void Dispose()
            {
            }
        }
    }
}
