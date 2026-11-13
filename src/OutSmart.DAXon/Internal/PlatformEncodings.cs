////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Concurrent;
using System.Text;

namespace OutSmart.DAXon.Internal
{
    /// <summary>
    /// .NET Framework carries ~140 encodings in the BCL; .NET ships seven (UTF-*, ASCII, Latin-1)
    /// and keeps the legacy code pages behind CodePagesEncodingProvider. Registering it is what
    /// makes windows-125x, ISO-8859-x, KOI8-R, Shift_JIS resolve on .NET: for the serializer, for
    /// unparsed-text(), and for System.Xml decoding an input document's declaration, which the
    /// engine never sees. Process-wide by design and purely additive.
    /// </summary>
    internal static class PlatformEncodings
    {
#if NET
        private static readonly object Gate = new object();
        private static bool registered;
#endif

        /// <summary>
        /// The name the platform knows an encoding by. Java's names for the Windows code pages are known to .NET 8 and later
        /// in part and to .NET Framework not at all; they mean the same everywhere.
        /// </summary>
        public static string Named(string name)
        {
            if (name.Length == 6 && name.StartsWith("cp125", StringComparison.OrdinalIgnoreCase) && name[5] >= '0' && name[5] <= '8')
            {
                return "windows-125" + name[5];
            }

            if (name.Equals("ms932", StringComparison.OrdinalIgnoreCase) || name.Equals("windows-31j", StringComparison.OrdinalIgnoreCase))
            {
                return "shift_jis";
            }

            if (name.Equals("tis620", StringComparison.OrdinalIgnoreCase) || name.Equals("tis620.2533", StringComparison.OrdinalIgnoreCase))
            {
                return Tis620;
            }

            if (name.Equals("x-iso-8859-11", StringComparison.OrdinalIgnoreCase) || name.Equals("iso8859_11", StringComparison.OrdinalIgnoreCase))
            {
                return Iso8859Thai;
            }

            if (name.Equals("x-windows-874", StringComparison.OrdinalIgnoreCase) || name.Equals("ms874", StringComparison.OrdinalIgnoreCase)
                || name.Equals("ms-874", StringComparison.OrdinalIgnoreCase))
            {
                return "windows-874";
            }

            return name.Equals("macroman", StringComparison.OrdinalIgnoreCase) ? "macintosh" : name;
        }

        // .NET reads both as windows-874; Java's tables tell them apart: TIS-620 has nothing at 0x80-0x9F, ISO 8859-11 the C1 controls.
        private const string Tis620 = "tis-620";
        private const string Iso8859Thai = "iso-8859-11";
        private const int Tis620Key = -620;
        private const int Iso8859ThaiKey = -885911;

        /// <summary>The same for bytes to read. Labelled UTF-16 or UTF-32 with no mark they are big-endian, as the engine writes them; a mark wins.</summary>
        public static string ForReading(string name)
        {
            return IsUtf16(name) ? "UTF-16BE" : IsUtf32(name) ? "UTF-32BE" : Named(name);
        }

        /// <summary>UTF-16 whose mark tells the order, under Java's names for it too: to .NET "unicode" is little-endian only.</summary>
        public static bool IsUtf16(string name)
        {
            return name.Equals("UTF-16", StringComparison.OrdinalIgnoreCase) || name.Equals("UTF_16", StringComparison.OrdinalIgnoreCase)
                || name.Equals("UTF16", StringComparison.OrdinalIgnoreCase) || name.Equals("unicode", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>UTF-32 whose mark tells the order, under Java's names for it too.</summary>
        public static bool IsUtf32(string name)
        {
            return name.Equals("UTF-32", StringComparison.OrdinalIgnoreCase) || name.Equals("UTF_32", StringComparison.OrdinalIgnoreCase)
                || name.Equals("UTF32", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The encoding to write under this name.</summary>
        public static Encoding Get(string name)
        {
            string known = Named(name);
            return Corrected(Supported(Encoding.GetEncoding(known), name), known);
        }

        /// <summary>The encoding to read under this name.</summary>
        public static Encoding GetForReading(string name)
        {
            string known = ForReading(name);
            return Corrected(Supported(Encoding.GetEncoding(known), name), known);
        }

        // UTF-7 only .NET Framework has, a way to hide markup in text: refused there too, as on .NET 8 and in Java.
        private static Encoding Supported(Encoding encoding, string name)
        {
            if (encoding.CodePage == Utf7Page)
            {
                throw new ArgumentException("'" + name + "' is not a supported encoding name");
            }

            return encoding;
        }

        private const int Utf7Page = 65000;

        /// <summary>
        /// The encoding to decode a document with itself, or null to leave it to System.Xml: a name in its declaration that
        /// System.Xml does not know though the engine does, or a code page whose table System.Xml has wrong. UTF-7, under
        /// any of its names, is refused in the words .NET 8's parser uses.
        /// </summary>
        public static Encoding ForDeclaration(string name)
        {
            if (name == null)
            {
                return null;
            }

            try
            {
                string known = Named(name);
                Encoding platform = Encoding.GetEncoding(known);
                if (platform.CodePage == Utf7Page)
                {
                    // System.Xml on .NET Framework would decode it, and markup hidden in text become markup.
                    throw new System.Xml.XmlException("System does not support '" + name + "' encoding.");
                }

                if (name.StartsWith("UTF", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (platform.CodePage == AsciiPage)
                {
                    return StrictAscii;   // System.Xml reads a byte past 7 bits as '?'
                }

                Encoding corrected = Corrected(platform, known);
                return !ReferenceEquals(corrected, platform) || !ReferenceEquals(known, name) ? corrected : null;
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
            {
                return null;   // the parser refuses it in its own words
            }
        }

        public const int AsciiPage = 20127;

        private static readonly Encoding StrictAscii = Encoding.GetEncoding(AsciiPage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        private static readonly ConcurrentDictionary<int, Encoding> CorrectedPages = new ConcurrentDictionary<int, Encoding>();

        private static Encoding Corrected(Encoding platform, string name)
        {
            int page = platform.CodePage;
            if (page == 874)
            {
                page = name.Equals(Tis620, StringComparison.OrdinalIgnoreCase) ? Tis620Key
                    : name.Equals(Iso8859Thai, StringComparison.OrdinalIgnoreCase) ? Iso8859ThaiKey
                    : page;
            }

            switch (page)
            {
                case 874:
                case Tis620Key:
                case Iso8859ThaiKey:
                case 1250:
                case 1251:
                case 1252:
                case 1253:
                case 1254:
                case 1255:
                case 1257:
                case 1258:
                case 28593:
                case 28596:
                case 28597:
                case 28598:
                case 21866:
                    return CorrectedPages.GetOrAdd(page, MakeCorrected);
                default:
                    return platform;
            }
        }

        private static Encoding MakeCorrected(int page)
        {
            switch (page)
            {
                case 28593:
                case 28596:
                    return new CorrectedCodePage(page, false);
                case 28597:
                    // ISO 8859-7:2003: quotation marks, euro, drachma, ypogegrammeni.
                    return new CorrectedCodePage(page, false, 0xA1, 0x2018, 0xA2, 0x2019, 0xA4, 0x20AC, 0xA5, 0x20AF, 0xAA, 0x037A);
                case 28598:
                    // ISO 8859-8:1999: macron, and the left-to-right and right-to-left marks.
                    return new CorrectedCodePage(page, false, 0xAF, 0x00AF, 0xFD, 0x200E, 0xFE, 0x200F);
                case 21866:
                    // RFC 2319: box drawing here, as in KOI8-R.
                    return new CorrectedCodePage(page, false, 0xAE, 0x255D, 0xBE, 0x256C);
                case Tis620Key:
                    return new CorrectedCodePage(Tis620, 874, true, HighControls(false));
                case Iso8859ThaiKey:
                    return new CorrectedCodePage(Iso8859Thai, 874, false, HighControls(true));
                default:
                    return new CorrectedCodePage(page, true);
            }
        }

        // Bytes 0x80-0x9F as the C1 controls, or as undefined (U+FFFF is no character).
        private static int[] HighControls(bool controls)
        {
            var changes = new int[64];
            for (int b = 0x80; b <= 0x9F; b++)
            {
                changes[(b - 0x80) * 2] = b;
                changes[((b - 0x80) * 2) + 1] = controls ? b : 0xFFFF;
            }

            return changes;
        }

        public static void EnsureRegistered()
        {
#if NET
            lock (Gate)
            {
                if (registered)
                {
                    return;
                }

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                registered = true;
            }
#endif
        }
    }
}
