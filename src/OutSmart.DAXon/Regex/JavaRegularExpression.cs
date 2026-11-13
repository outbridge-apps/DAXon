////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using JPattern = OutSmart.DAXon.Internal.Regex.Pattern;

namespace OutSmart.DAXon.Regex
{
    // The matching part of net.sf.saxon.regex.JavaRegularExpression, backed by Internal.Regex.Pattern
    // (System.Text.RegularExpressions underneath). Only consumer: the ?select= globs of collection URIs
    // (URIQueryParameters.MakeGlobFilter; ?match= is an XPath regex).
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
    internal sealed class JavaRegularExpression
    {
        private readonly JPattern pattern;

        /// <summary>
        /// Create a regular expression from an already-translated Java regex.
        /// </summary>
        /// <param name="javaRegex">the regular expression in Java notation</param>
        /// <param name="flags">the user-specified flags (prior to any semicolon)</param>
        public JavaRegularExpression(UnicodeString javaRegex, string flags)
        {
            bool literal;
            int flagBits = ParseFlags(flags ?? "", out literal);
            string regex = javaRegex.ToString();
            // Java Pattern.LITERAL ('q'): the entire pattern is treated as a literal sequence.
            // .NET has no equivalent option; escaping every metacharacter is semantically identical.
            string netRegex = literal
                ? System.Text.RegularExpressions.Regex.Escape(regex)
                : regex;
            int brace = literal ? -1 : IllegalRepetition(regex);
            if (brace >= 0)
            {
                throw new XPathException("Incorrect syntax for native regular expression: illegal repetition at " + brace, "FORX0002");
            }

            try
            {
                pattern = JPattern.Compile(netRegex, flagBits);
            }
            catch (System.ArgumentException e)
            {
                // compat Pattern.Compile surfaces invalid patterns as ArgumentException from the
                // System.Text.RegularExpressions.Regex ctor (Java: PatternSyntaxException).
                throw new XPathException("Incorrect syntax for native regular expression: " + e.Message, "FORX0002");
            }
        }

        // Java refuses a '{' that opens no repetition {n}, {n,} or {n,m} ("Illegal repetition"), and .NET reads it as a
        // literal: a glob with braces (*.{xml,txt}) matched nothing where Saxon fails with FODC0004.
        private static int IllegalRepetition(string regex)
        {
            bool inClass = false;
            for (int i = 0; i < regex.Length; i++)
            {
                char c = regex[i];
                if (c == '\\')
                {
                    i++;
                }
                else if (inClass)
                {
                    inClass = c != ']';
                }
                else if (c == '[')
                {
                    inClass = true;
                }
                else if (c == '{' && !System.Text.RegularExpressions.Regex.IsMatch(regex.Substring(i), @"^\{[0-9]+(,[0-9]*)?\}"))
                {
                    return i;
                }
            }

            return -1;
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
    }
}
