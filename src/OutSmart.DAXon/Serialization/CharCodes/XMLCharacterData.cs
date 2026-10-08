////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// More stubs (CS0103 bare-identifier patterns)
namespace OutSmart.DAXon.Serialization.CharCodes
{
    internal static class XMLCharacterData
    {
        // NCName classification. The `11` variants implement the XML 1.1 / XML 1.0-5th-edition NameStartChar
        // and NameChar productions DIRECTLY (matching upstream Saxon's Name11Checker), NOT via the BCL's
        // System.Xml.XmlConvert — XmlConvert follows XML 1.0 *4th edition* (the old BaseChar/Ideographic
        // tables), which wrongly rejects e.g. U+2C00-U+2FEF and U+3001-U+3006 that XML 1.1 admits
        // (package-version-005 uses CJK-symbol NameParts). JSON scanning stays correct: `,` `]` `}` `:`
        // and whitespace are still non-NameChars, so JsonParser's unquoted-literal scan terminates.
        // The `10` variants stay on XmlConvert (genuine XML 1.0 4th-ed semantics). IsValid* left permissive.
        public static bool IsValid11(int c = 0) => (c >= 0x1 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD) || (c >= 0x10000 && c <= 0x10FFFF);
        public static bool IsValid10(int c = 0) => c == 0x9 || c == 0xA || c == 0xD || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD) || (c >= 0x10000 && c <= 0x10FFFF);
        public static bool IsNCNameStart11(int c) =>
            (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_'
            || (c >= 0xC0 && c <= 0xD6) || (c >= 0xD8 && c <= 0xF6) || (c >= 0xF8 && c <= 0x2FF)
            || (c >= 0x370 && c <= 0x37D) || (c >= 0x37F && c <= 0x1FFF)
            || (c >= 0x200C && c <= 0x200D) || (c >= 0x2070 && c <= 0x218F)
            || (c >= 0x2C00 && c <= 0x2FEF) || (c >= 0x3001 && c <= 0xD7FF)
            || (c >= 0xF900 && c <= 0xFDCF) || (c >= 0xFDF0 && c <= 0xFFFD)
            || (c >= 0x10000 && c <= 0xEFFFF);
        public static bool IsNCName11(int c) =>
            IsNCNameStart11(c)
            || c == '-' || c == '.' || (c >= '0' && c <= '9') || c == 0xB7
            || (c >= 0x0300 && c <= 0x036F) || (c >= 0x203F && c <= 0x2040);
    }
}
