////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// DAXonEnumExtensions.cs
//
// paulirwin converts Java enums with methods into C# enums + commented-out
// method bodies (since C# enums can't have methods). The Saxon source has
// many such enums (OccurrenceIndicator.getCardinality(), ValidationMode.getNumber(),
// FunctionStreamability.isStreaming()).
//
// This file provides extension methods that recreate the Java method semantics
// for use sites that still call them as `enum.Method()`.
//

using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Api
{
    // Serializer.Property carried a per-value property name in Java (toString() = Clark name);
    // the C# enum lost it. These extensions restore the upstream name table.
    public static class SerializerPropertyExtensions
    {
        // The serialization-parameter name in Clark notation, exactly as upstream
        // Property.toString() (OutputKeys / DAXonOutputKeys constants).
        public static string GetPropertyName(this Serializer.Property p)
        {
            switch (p)
            {
                case Serializer.Property.SAXON_INDENT_SPACES: return "{http://saxon.sf.net/}indent-spaces";
                case Serializer.Property.SAXON_INTERNAL_DTD_SUBSET: return "{http://saxon.sf.net/}internal-dtd-subset";
                case Serializer.Property.SAXON_LINE_LENGTH: return "{http://saxon.sf.net/}line-length";
                case Serializer.Property.SAXON_ATTRIBUTE_ORDER: return "{http://saxon.sf.net/}attribute-order";
                case Serializer.Property.SAXON_CANONICAL: return "{http://saxon.sf.net/}canonical";
                case Serializer.Property.SAXON_NEWLINE: return "{http://saxon.sf.net/}newline";
                case Serializer.Property.SAXON_DOUBLE_SPACE: return "{http://saxon.sf.net/}double-space";
                case Serializer.Property.SAXON_STYLESHEET_VERSION: return "{http://saxon.sf.net/}stylesheet-version";
                case Serializer.Property.SAXON_CHARACTER_REPRESENTATION: return "{http://saxon.sf.net/}character-representation";
                case Serializer.Property.SAXON_RECOGNIZE_BINARY: return "{http://saxon.sf.net/}recognize-binary";
                case Serializer.Property.SAXON_REQUIRE_WELL_FORMED: return "{http://saxon.sf.net/}require-well-formed";
                case Serializer.Property.SAXON_WRAP: return "{http://saxon.sf.net/}wrap-result-sequence";
                case Serializer.Property.SAXON_SUPPLY_SOURCE_LOCATOR: return "{http://saxon.sf.net/}supply-source-locator";
                // Standard XSLT 3.0 property despite the SAXON_ enum prefix (DAXonOutputKeys.SUPPRESS_INDENTATION).
                case Serializer.Property.SAXON_SUPPRESS_INDENTATION: return "suppress-indentation";
                default: return p.ToString().ToLowerInvariant().Replace('_', '-');
            }
        }

        public static StructuredQName GetQName(this Serializer.Property p)
        {
            string name = p.GetPropertyName();
            if (name.Length > 0 && name[0] == '{')
            {
                int close = name.IndexOf('}');
                return new StructuredQName("saxon", name.Substring(1, close - 1), name.Substring(close + 1));
            }
            return new StructuredQName("", "", name);
        }
    }
}
