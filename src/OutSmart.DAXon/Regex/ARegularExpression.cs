////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Regex
{
    internal sealed class ARegularExpression : IRegularExpression
    {
        UnicodeString rawPattern;
        string rawFlags;
        REProgram regex;
        // Codepoint for the single-literal-char tokenize fast path; -1 = not applicable,
        // -2 = not yet determined. Lazily computed from the COMPILED program (so \; and the
        // q flag qualify too); benign race: concurrent writers store the same value.
        private int singleCharToken = -2;

        public string Flags => rawFlags;
        public ARegularExpression(UnicodeString pattern, string flags, string hostLanguage, IList<string> warnings, Configuration config)
        {
            rawFlags = flags;
            REFlags reFlags;
            try
            {
                reFlags = new REFlags(flags, hostLanguage);
            }
            catch (RESyntaxException err)
            {
                throw new XPathException(err.Message, "FORX0001");
            }

            try
            {
                rawPattern = pattern;
                RECompiler comp2 = new RECompiler();
                comp2.SetFlags(reFlags);
                regex = comp2.Compile(rawPattern);
                if (warnings != null)
                {
                    warnings.AddRange(comp2.Warnings);
                }

                if (config != null)
                {
                    regex.BacktrackingLimit = config.GetConfigurationProperty(Feature<int>.REGEX_BACKTRACKING_LIMIT);
                }
            }
            catch (RESyntaxException err)
            {
                throw new XPathException(err.Message, "FORX0002");
            }
        }

        public static ARegularExpression Compile(string pattern, string flags)
        {
            try
            {
                return new ARegularExpression(BMPString.Of(pattern), flags, "XP31", null, null);
            }
            catch (XPathException e)
            {
                throw new ArgumentException(e.Message, e);
            }
        }

        public bool Matches(UnicodeString input)
        {
            if (input.IsEmpty() && regex.IsNullable())
            {
                return true;
            }

            REMatcher matcher = new REMatcher(regex);
            try
            {
                return matcher.IsAnchoredMatch(input.Tidy());
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                throw DescribeOverflow(e);
            }
        }

        // Match-time description of the OpCapture/OpSequence stack-guard signal — one per public
        // evaluation entry (analyze-string and tokenize describe in their own iterators). It stays
        // a RecursionDepthError all the way to the API boundary: a regex driven from inside a
        // recursive template carries the whole engine stack above it, and an XPathException would
        // have to unwind through every decorating catch on the way up — which is what killed the
        // process before round BC.
        internal static Internal.RecursionDepthError DescribeOverflow(Internal.RecursionDepthError e)
        {
            return e.Describe("Stack overflow (excessive recursion) during regular expression evaluation", DAXonErrorCode.SXRE0001, Loc.NONE);
        }

        public bool ContainsMatch(UnicodeString input)
        {
            REMatcher matcher = new REMatcher(regex);
            try
            {
                return matcher.Match(input.Tidy(), 0);
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                throw DescribeOverflow(e);
            }
        }

        public IAtomicIterator Tokenize(UnicodeString input)
        {
            int cp = SingleCharLiteral();
            if (cp >= 0)
            {
                return new SingleCharTokenIterator(input.Tidy(), cp);
            }

            return new ATokenIterator(input.Tidy(), new REMatcher(regex));
        }

        // The codepoint of a bare single-character literal pattern (e.g. ";"), or -1. Exposed so the
        // optimizer can fuse tokenize($s, single-char)[N] into a direct field scan; identical gate to
        // the SingleCharTokenIterator fast path, so the fused result is byte-identical.
        public int SingleCharLiteral()
        {
            if (singleCharToken != -2)
            {
                return singleCharToken;
            }

            int result = -1;
            if (!regex.flags.IsCaseIndependent()
                && regex.operation is OpSequence seq
                && seq.Operations.Count == 2
                && seq.Operations[0] is OpAtom atom
                && seq.Operations[1] is OpEndProgram
                && atom.Atom.Length32() == 1)
            {
                result = atom.Atom.CodePointAt(0);
            }

            singleCharToken = result;
            return result;
        }

        public IRegexIterator Analyze(UnicodeString input)
        {
            return new ARegexIterator(input.Tidy(), rawPattern, new REMatcher(regex));
        }

        public UnicodeString Replace(UnicodeString input, UnicodeString replacement)
        {
            REMatcher matcher = new REMatcher(regex);
            try
            {
                return matcher.Replace(input.Tidy(), replacement);
            }
            catch (RESyntaxException err)
            {
                throw new XPathException(err.Message, "FORX0004");
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                throw DescribeOverflow(e);
            }
        }

        public UnicodeString ReplaceWith(UnicodeString input, Func<UnicodeString, UnicodeString[], UnicodeString> replacer)
        {
            REMatcher matcher = new REMatcher(regex);
            try
            {
                return matcher.ReplaceWith(input.Tidy(), replacer);
            }
            catch (RESyntaxException err)
            {
                throw new XPathException(err.Message, "FORX0004");
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                throw DescribeOverflow(e);
            }
        }

        public bool IsPlatformNative()
        {
            return false;
        }
    }
}
