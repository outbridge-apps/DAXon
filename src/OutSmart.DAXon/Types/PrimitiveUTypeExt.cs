////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Patterns;

namespace OutSmart.DAXon.Types
{

    // Extension method bringing Java's PrimitiveUType.toUType() method to the enum.
    // The original method was commented-out by paulirwin's enum-conversion logic.
    internal static class PrimitiveUTypeExt
    {
        // toUType() per real Saxon: each primitive maps to a single bit (1 << bit). The hollow `=> UType.ANY`
        // both returned the wrong value AND was a static-init-order trap: UType's own static fields call
        // PrimitiveUType.X.ToUType() (UType.cs:28-54) BEFORE UType.ANY (UType.cs:62) is initialized, so they
        // read a still-null UType.ANY -> the first .Union() at UType.cs:55 (NUMERIC) NRE'd, killing the .cctor.
        // new UType(1 << bit) returns a fresh value with no forward reference. The decomposition at
        // UType.cs:209 also uses GetBit(), so set and test round-trip consistently.
        public static UType ToUType(this PrimitiveUType pu) => new UType(1 << pu.GetBit());
        // Java bit values are the ordinals 0..27, except EXTENSION which is declared EXTENSION(30) -- a
        // deliberate gap (bits 28,29 reserved). Faithful to net.sf.saxon.type.PrimitiveUType.
        public static int GetBit(this PrimitiveUType pu) => pu == PrimitiveUType.EXTENSION ? 30 : (int)pu;
        // ToItemType() on PrimitiveUType enum — ported from upstream PrimitiveUType.toItemType().
        // Maps each primitive UType to its representative item type (node-kind test / built-in atomic
        // type / any-function). Was a hollow `=> null`, which NRE'd when UType.ToItemType() returned it
        // to callers such as Types.GetCommonSuperType (union of two disjoint node tests -> ELEMENT).
        public static ItemType ToItemType(this PrimitiveUType pu)
        {
            switch (pu)
            {
                case PrimitiveUType.DOCUMENT:
                    return NodeKindTest.DOCUMENT;
                case PrimitiveUType.ELEMENT:
                    return NodeKindTest.ELEMENT;
                case PrimitiveUType.ATTRIBUTE:
                    return NodeKindTest.ATTRIBUTE;
                case PrimitiveUType.TEXT:
                    return NodeKindTest.TEXT;
                case PrimitiveUType.COMMENT:
                    return NodeKindTest.COMMENT;
                case PrimitiveUType.PI:
                    return NodeKindTest.PROCESSING_INSTRUCTION;
                case PrimitiveUType.NAMESPACE:
                    return NodeKindTest.NAMESPACE;
                case PrimitiveUType.FUNCTION:
                    return AnyFunctionType.GetInstance();
                case PrimitiveUType.STRING:
                    return BuiltInAtomicType.STRING;
                case PrimitiveUType.BOOLEAN:
                    return BuiltInAtomicType.BOOLEAN;
                case PrimitiveUType.DECIMAL:
                    return BuiltInAtomicType.DECIMAL;
                case PrimitiveUType.FLOAT:
                    return BuiltInAtomicType.FLOAT;
                case PrimitiveUType.DOUBLE:
                    return BuiltInAtomicType.DOUBLE;
                case PrimitiveUType.DURATION:
                    return BuiltInAtomicType.DURATION;
                case PrimitiveUType.DATE_TIME:
                    return BuiltInAtomicType.DATE_TIME;
                case PrimitiveUType.TIME:
                    return BuiltInAtomicType.TIME;
                case PrimitiveUType.DATE:
                    return BuiltInAtomicType.DATE;
                case PrimitiveUType.G_YEAR_MONTH:
                    return BuiltInAtomicType.G_YEAR_MONTH;
                case PrimitiveUType.G_YEAR:
                    return BuiltInAtomicType.G_YEAR;
                case PrimitiveUType.G_MONTH_DAY:
                    return BuiltInAtomicType.G_MONTH_DAY;
                case PrimitiveUType.G_DAY:
                    return BuiltInAtomicType.G_DAY;
                case PrimitiveUType.G_MONTH:
                    return BuiltInAtomicType.G_MONTH;
                case PrimitiveUType.HEX_BINARY:
                    return BuiltInAtomicType.HEX_BINARY;
                case PrimitiveUType.BASE64_BINARY:
                    return BuiltInAtomicType.BASE64_BINARY;
                case PrimitiveUType.ANY_URI:
                    return BuiltInAtomicType.ANY_URI;
                case PrimitiveUType.QNAME:
                    return BuiltInAtomicType.QNAME;
                case PrimitiveUType.NOTATION:
                    return BuiltInAtomicType.NOTATION;
                case PrimitiveUType.UNTYPED_ATOMIC:
                    return BuiltInAtomicType.UNTYPED_ATOMIC;
                case PrimitiveUType.EXTENSION:
                    return AnyItemType.GetInstance();
                default:
                    throw new ArgumentException();
            }
        }
    }
}
