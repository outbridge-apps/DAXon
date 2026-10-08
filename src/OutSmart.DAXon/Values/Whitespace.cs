////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Collections;
using System;
using System.Text;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// This class provides helper methods and constants for handling whitespace
    /// </summary>
    internal sealed class Whitespace
    {

        public const int PRESERVE = 0;
        public const int REPLACE = 1;
        public const int COLLAPSE = 2;
        public const int TRIM = 3;
        public const int NONE = 0;
        public const int IGNORABLE = 1;
        public const int ALL = 2;
        public const int UNSPECIFIED = 3;
        public const int XSLT = 4;
        private static readonly OutSmart.DAXon.Internal.Regex.Pattern J_oneWhitespace = OutSmart.DAXon.Internal.Regex.Pattern.Compile("[ \\n\\r\\t]");
        private static readonly OutSmart.DAXon.Internal.Regex.Pattern J_anyWhitespace = OutSmart.DAXon.Internal.Regex.Pattern.Compile("[ \\n\\r\\t]+");

        private static readonly bool[] C0WHITE = new[]
        {
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            true,
            true,
            false,
            false,
            true,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            true
        };
        public static UnicodeString ApplyWhitespaceNormalization(int action, UnicodeString value)
        {
            switch (action)
            {
                case PRESERVE:
                    return value;
                case REPLACE:
                    UnicodeBuilder sb = new UnicodeBuilder(value.Length32());
                    IIntIterator iter = value.CodePoints();
                    while (iter.MoveNext())
                    {
                        int c = iter.Current;
                        switch (c)
                        {
                            case '\n':
                            case '\r':
                            case '\t':
                                sb.Append(' ');
                                break;
                            default:
                                sb.Append(c);
                                break;
                        }
                    }

                    return sb.ToUnicodeString();
                case COLLAPSE:
                    return CollapseWhitespace(value);
                case TRIM:
                    return Trim(value);
                default:
                    throw new ArgumentException("Unknown whitespace facet value");
            }
        }

        public static string RemoveAllWhitespace(string value)
        {
            return J_oneWhitespace.Matcher(value).ReplaceAll("");
        }

        public static UnicodeString RemoveLeadingWhitespace(UnicodeString value)
        {
            long start = TrimmedStart(value);
            if (start == 0)
            {
                return value;
            }
            else if (start < 0)
            {
                return EmptyUnicodeString.GetInstance();
            }
            else
            {
                return value.Substring(start);
            }
        }

        public static bool ContainsWhitespace(IIntIterator codePoints)
        {
            while (codePoints.MoveNext())
            {
                int c = codePoints.Current;
                if (c <= 32 && C0WHITE[c])
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsAllWhite(UnicodeString content)
        {
            if (content is WhitespaceString)
            {
                return true;
            }

            return content.IndexWhere((ch) => !IsWhite(ch), 0) < 0;
        }

        private static void Tokenize(IIntIterator input, ITokenHandler handler)
        {
            int position = 0;
            int tokenStart = 0;
            TokenCategory currentCategory = TokenCategory.INITIAL_WHITESPACE;
            while (input.MoveNext())
            {
                int ch = input.Current;
                if (IsWhite(ch))
                {
                    if (currentCategory == TokenCategory.CONTENT)
                    {
                        handler(tokenStart, position, currentCategory);
                        tokenStart = position;
                        currentCategory = TokenCategory.SEPARATOR_WHITESPACE;
                    }
                }
                else
                {
                    if (currentCategory != TokenCategory.CONTENT)
                    {
                        if (position > 0)
                        {
                            handler(tokenStart, position, currentCategory);
                        }

                        tokenStart = position;
                        currentCategory = TokenCategory.CONTENT;
                    }
                }

                position++;
            }

            if (position > tokenStart)
            {
                if (currentCategory == TokenCategory.SEPARATOR_WHITESPACE)
                {
                    handler(tokenStart, position, TokenCategory.FINAL_WHITESPACE);
                }
                else
                {
                    handler(tokenStart, position, currentCategory);
                }
            }
        }

        public static bool IsWhite(int c)
        {
            return c <= 32 && C0WHITE[c];
        }

        public static UnicodeString NormalizeWhitespace(UnicodeString input)
        {
            UnicodeBuilder sb = new UnicodeBuilder(input.Length32());
            IIntIterator iter = input.CodePoints();
            while (iter.MoveNext())
            {
                int c = iter.Current;
                switch (c)
                {
                    case '\n':
                    case '\r':
                    case '\t':
                        sb.Append(' ');
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToUnicodeString();
        }

        public static UnicodeString CollapseWhitespace(UnicodeString @in)
        {
            // Single fused scan replacing the old ContainsWhitespace pre-pass: return the input
            // unchanged when it is already collapsed (no tab/CR/LF, no leading/trailing space, no
            // adjacent spaces). Real-world text is mostly already normalized, often with single
            // interior spaces, and the old code rebuilt any string containing ANY whitespace.
            long length = @in.Length();
            if (length == 0)
            {
                return @in;
            }

            // Byte-backed Latin1 strings (the dominant text-node case) scan their raw bytes
            // directly: CodePointAt on these does a per-char RequireInt + Length32 bounds check
            // + virtual dispatch, so the generic loop below spends most of its time in dispatch,
            // not in the comparison. Whitespace chars are all < 128, so byte value == codepoint.
            if (@in is Slice8 s8)
            {
                return CollapseByteScan(@in, s8.ByteArray, s8.Start, s8.End);
            }

            if (@in is Twine8 t8)
            {
                byte[] tb = t8.ByteArray;
                return CollapseByteScan(@in, tb, 0, tb.Length);
            }

            bool prevSpace = true; // start-of-string counts as a preceding space: a leading space breaks the fast path
            bool alreadyCollapsed = true;
            for (long i = 0; i < length; i++)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);   // these scans and rebuilds: one string, one call
                int c = @in.CodePointAt(i);
                if (c == ' ')
                {
                    if (prevSpace)
                    {
                        alreadyCollapsed = false;
                        break;
                    }

                    prevSpace = true;
                }
                else if (c == '\n' || c == '\r' || c == '\t')
                {
                    alreadyCollapsed = false;
                    break;
                }
                else
                {
                    prevSpace = false;
                }
            }

            if (alreadyCollapsed && !prevSpace) // prevSpace after the loop = trailing space
            {
                return @in;
            }

            return CollapseWhitespaceRebuild(@in);
        }

        // Fast already-collapsed check over a Latin1 byte range: returns the original string
        // unchanged (no allocation) when it needs no collapsing, else the rebuilt form.
        private static UnicodeString CollapseByteScan(UnicodeString original, byte[] bytes, int start, int end)
        {
            bool prevSpace = true; // start-of-string counts as a preceding space
            for (int i = start; i < end; i++)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                int c = bytes[i];
                if (c == ' ')
                {
                    if (prevSpace)
                    {
                        return CollapseWhitespaceRebuild(original);
                    }

                    prevSpace = true;
                }
                else if (c == '\n' || c == '\r' || c == '\t')
                {
                    return CollapseWhitespaceRebuild(original);
                }
                else
                {
                    prevSpace = false;
                }
            }

            // prevSpace still set = trailing space (or an all-space string) -> needs a rebuild
            return prevSpace ? CollapseWhitespaceRebuild(original) : original;
        }

        private static UnicodeString CollapseWhitespaceRebuild(UnicodeString @in)
        {
            long len = TrimmedEnd(@in);
            UnicodeBuilder sb = new UnicodeBuilder(@in.Length32());
            bool inWhitespace = true;
            for (long i = 0; i < len; i++)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                int c = @in.CodePointAt(i);
                switch (c)
                {
                    case '\n':
                    case '\r':
                    case '\t':
                    case ' ':
                        if (!inWhitespace)
                        {
                            sb.Append(0x20);
                            inWhitespace = true;
                        }

                        break;
                    default:
                        sb.Append(c);
                        inWhitespace = false;
                        break;
                }
            }

            return sb.ToUnicodeString();
        }

        public static string CollapseWhitespace(string @in)
        {
            if (!ContainsWhitespace(StringTool.CodePoints(@in)))
            {
                return @in;
            }

            return Trim(J_anyWhitespace.Matcher(@in).ReplaceAll(" "));
        }

        public static long TrimmedStart(UnicodeString @in)
        {
            long len = @in.Length();
            for (int i = 0; i < len; i++)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                if (!IsWhite(@in.CodePointAt(i)))
                {
                    return i;
                }
            }

            return -1;
        }

        public static long TrimmedEnd(UnicodeString @in)
        {
            long len = @in.Length();
            for (long i = len - 1; i >= 0; i--)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                if (!IsWhite(@in.CodePointAt(i)))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        public static UnicodeString Trim(UnicodeString @in)
        {
            long start = TrimmedStart(@in);
            if (start == -1)
            {

                // All whitespace
                return EmptyUnicodeString.GetInstance();
            }

            long end = TrimmedEnd(@in);
            if (start == 0 && end == @in.Length())
            {

                // No leading or trailing whitespace
                return @in;
            }

            return @in.Substring(start, end);
        }

        public static string Trim(string @in)
        {
            if (@in == null)
            {
                return null;
            }

            int firstNonWhite = -1;
            int lastNonWhite = -1;
            int len = @in.Length;
            for (int i = 0; i < len; i++)
            {
                if (!IsWhite(@in[i]))
                {
                    firstNonWhite = i;
                    break;
                }
            }

            if (firstNonWhite == -1)
            {

                // All whitespace
                return "";
            }

            for (int i = len - 1; i >= firstNonWhite; i--)
            {
                if (!IsWhite(@in[i]))
                {
                    lastNonWhite = i;
                    break;
                }
            }

            if (firstNonWhite == 0 && lastNonWhite == @in.Length)
            {

                // No leading or trailing whitespace
                return @in;
            }

            return @in.Substring(firstNonWhite, lastNonWhite + 1 - firstNonWhite) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
        }

        public static string Collapse(string @in)
        {
            StringBuilder builder = new StringBuilder(@in.Length);
            Tokenize(StringTool.CodePoints(@in), (s, e, cat) =>
            {
                TokenCategory category = cat; // redeclared to assist C# conversion
                switch (category)
                {
                    case TokenCategory.CONTENT:
                        builder.Append(@in.Substring(s, e - s));
                        break;
                    case TokenCategory.SEPARATOR_WHITESPACE:
                        builder.Append(' ');
                        break;
                    default:

                        // no action
                        break;
                }
            });
            return builder.ToString();
        }

        public static string Normalize(string @in)
        {
            StringBuilder builder = new StringBuilder(@in.Length);
            Tokenize(StringTool.CodePoints(@in), (s, e, cat) =>
            {
                TokenCategory category = cat; // redeclared to assist C# conversion
                switch (category)
                {
                    case TokenCategory.CONTENT:
                        builder.Append(@in.Substring(s, e - s));
                        break;
                    case TokenCategory.SEPARATOR_WHITESPACE:
                        for (int i = s; i < e; i++)
                        {
                            builder.Append(' ');
                        }

                        break;
                    default:
                        break;
                }
            });
            return builder.ToString();
        }
        private enum TokenCategory
        {
            INITIAL_WHITESPACE,
            SEPARATOR_WHITESPACE,
            FINAL_WHITESPACE,
            CONTENT
        }

        // ITokenHandler interface->delegate.
        private delegate void ITokenHandler(int start, int end, TokenCategory category);

        /// <summary>
        /// An iterator that splits a string on whitespace boundaries, corresponding to the XPath 3.1 function tokenize#1
        /// </summary>
        internal sealed class Tokenizer : IAtomicIterator
        {
            private readonly UnicodeString input;
            private long position;
            private long look = 65536;   // every 64K characters scanned - many tokens or one long run - it looks at the limits

            public Tokenizer(UnicodeString input)
            {
                this.input = input.Tidy();
                this.position = 0;
            }

            public StringValue Next()
            {
                long start = position;
                long eol = input.Length();
                while (start < eol && IsWhite(input.CodePointAt(start)))
                {
                    start++;
                    if (start >= look)
                    {
                        Look(start);
                    }
                }

                if (start >= eol)
                {
                    return null;
                }

                long end = start;
                while (end < eol && !IsWhite(input.CodePointAt(end)))
                {
                    end++;
                    if (end >= look)
                    {
                        Look(end);
                    }
                }

                position = end;
                return new StringValue(input.Substring(start, end));
            }

            private void Look(long at)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();
                look = at + 65536;
            }
            AtomicValue IAtomicIterator.Next() => Next();
            IItem ISequenceIterator.Next() => Next();
            public void Dispose() { }
        }
    }
}
