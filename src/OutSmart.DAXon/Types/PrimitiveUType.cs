////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2013-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Types
{
    public enum PrimitiveUType
    {
        // DOCUMENT(0)
        DOCUMENT,
        // ELEMENT(1)
        ELEMENT,
        // ATTRIBUTE(2)
        ATTRIBUTE,
        // TEXT(3)
        TEXT,
        // COMMENT(4)
        COMMENT,
        // PI(5)
        PI,
        // NAMESPACE(6)
        NAMESPACE,
        // FUNCTION(7)
        FUNCTION,
        // STRING(8)
        STRING,
        // BOOLEAN(9)
        BOOLEAN,
        // DECIMAL(10)
        DECIMAL,
        // FLOAT(11)
        FLOAT,
        // DOUBLE(12)
        DOUBLE,
        // DURATION(13)
        DURATION,
        // DATE_TIME(14)
        DATE_TIME,
        // TIME(15)
        TIME,
        // DATE(16)
        DATE,
        // G_YEAR_MONTH(17)
        G_YEAR_MONTH,
        // G_YEAR(18)
        G_YEAR,
        // G_MONTH_DAY(19)
        G_MONTH_DAY,
        // G_DAY(20)
        G_DAY,
        // G_MONTH(21)
        G_MONTH,
        // HEX_BINARY(22)
        HEX_BINARY,
        // BASE64_BINARY(23)
        BASE64_BINARY,
        // ANY_URI(24)
        ANY_URI,
        // QNAME(25)
        QNAME,
        // NOTATION(26)
        NOTATION,
        // UNTYPED_ATOMIC(27)
        UNTYPED_ATOMIC,
        // EXTENSION(30)
        EXTENSION
    }

    internal static class PrimitiveUTypeNames
    {
        // As a message says it: "an element node".
        internal static string Display(this PrimitiveUType type)
        {
            switch (type)
            {
                case PrimitiveUType.PI:
                    return "processing-instruction";
                case PrimitiveUType.DATE_TIME:
                    return "dateTime";
                case PrimitiveUType.G_YEAR_MONTH:
                    return "gYearMonth";
                case PrimitiveUType.G_YEAR:
                    return "gYear";
                case PrimitiveUType.G_MONTH_DAY:
                    return "gMonthDay";
                case PrimitiveUType.G_DAY:
                    return "gDay";
                case PrimitiveUType.G_MONTH:
                    return "gMonth";
                case PrimitiveUType.HEX_BINARY:
                    return "hexBinary";
                case PrimitiveUType.BASE64_BINARY:
                    return "base64Binary";
                case PrimitiveUType.ANY_URI:
                    return "anyURI";
                case PrimitiveUType.QNAME:
                    return "QName";
                case PrimitiveUType.NOTATION:
                    return "NOTATION";
                case PrimitiveUType.UNTYPED_ATOMIC:
                    return "untypedAtomic";
                case PrimitiveUType.EXTENSION:
                    return "external object";
                default:
                    return type.ToString().ToLowerInvariant();
            }
        }
    }
}