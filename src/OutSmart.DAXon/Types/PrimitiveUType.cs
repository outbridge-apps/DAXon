////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2013-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Patterns;using OutSmart.DAXon.Functions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
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

        // --------------------
        // private final int bit;
        // PrimitiveUType(int bit) {
        //     this.bit = bit;
        // }
        // public int getBit() {
        // public UType toUType() {
        // public static PrimitiveUType forBit(int bit) {
        // @CSharpModifiers(code = { "public", "override" })
        // public String toString() {
        //             return "document";
        //         case ELEMENT:
        //             return "element";
        //         case ATTRIBUTE:
        //             return "attribute";
        //         case TEXT:
        //             return "text";
        //         case COMMENT:
        //             return "comment";
        //         case PI:
        //             return "processing-instruction";
        //         case NAMESPACE:
        //             return "namespace";
        //         case FUNCTION:
        //             return "function";
        //         case STRING:
        //             return "string";
        //         case BOOLEAN:
        //             return "boolean";
        //         case DECIMAL:
        //             return "decimal";
        //         case FLOAT:
        //             return "float";
        //         case DOUBLE:
        //             return "double";
        //         case DURATION:
        //             return "duration";
        //         case DATE_TIME:
        //             return "dateTime";
        //         case TIME:
        //             return "time";
        //         case DATE:
        //             return "date";
        //         case G_YEAR_MONTH:
        //             return "gYearMonth";
        //         case G_YEAR:
        //             return "gYear";
        //         case G_MONTH_DAY:
        //             return "gMonthDay";
        //         case G_DAY:
        //             return "gDay";
        //         case G_MONTH:
        //             return "gMoonth";
        //         case HEX_BINARY:
        //             return "hexBinary";
        //         case BASE64_BINARY:
        //             return "base64Binary";
        //         case ANY_URI:
        //             return "anyURI";
        //         case QNAME:
        //             return "QName";
        //         case NOTATION:
        //             return "NOTATION";
        //         case UNTYPED_ATOMIC:
        //             return "untypedAtomic";
        //         case EXTENSION:
        //             return "external object";
        //         default:
        //             return "???";
        // public ItemType toItemType() {
        //             return NodeKindTest.DOCUMENT;
        //         case ELEMENT:
        //             return NodeKindTest.ELEMENT;
        //         case ATTRIBUTE:
        //             return NodeKindTest.ATTRIBUTE;
        //         case TEXT:
        //             return NodeKindTest.TEXT;
        //         case COMMENT:
        //             return NodeKindTest.COMMENT;
        //         case PI:
        //             return NodeKindTest.PROCESSING_INSTRUCTION;
        //         case NAMESPACE:
        //             return NodeKindTest.NAMESPACE;
        //             return BuiltInAtomicType.STRING;
        //         case BOOLEAN:
        //             return BuiltInAtomicType.BOOLEAN;
        //         case DECIMAL:
        //             return BuiltInAtomicType.DECIMAL;
        //         case FLOAT:
        //             return BuiltInAtomicType.FLOAT;
        //         case DOUBLE:
        //             return BuiltInAtomicType.DOUBLE;
        //         case DURATION:
        //             return BuiltInAtomicType.DURATION;
        //         case DATE_TIME:
        //             return BuiltInAtomicType.DATE_TIME;
        //         case TIME:
        //             return BuiltInAtomicType.TIME;
        //         case DATE:
        //             return BuiltInAtomicType.DATE;
        //         case G_YEAR_MONTH:
        //             return BuiltInAtomicType.G_YEAR_MONTH;
        //         case G_YEAR:
        //             return BuiltInAtomicType.G_YEAR;
        //         case G_MONTH_DAY:
        //             return BuiltInAtomicType.G_MONTH_DAY;
        //         case G_DAY:
        //             return BuiltInAtomicType.G_DAY;
        //         case G_MONTH:
        //             return BuiltInAtomicType.G_MONTH;
        //         case HEX_BINARY:
        //             return BuiltInAtomicType.HEX_BINARY;
        //         case BASE64_BINARY:
        //             return BuiltInAtomicType.BASE64_BINARY;
        //         case ANY_URI:
        //             return BuiltInAtomicType.ANY_URI;
        //         case QNAME:
        //             return BuiltInAtomicType.QNAME;
        //         case NOTATION:
        //             return BuiltInAtomicType.NOTATION;
        //         case UNTYPED_ATOMIC:
        //             return BuiltInAtomicType.UNTYPED_ATOMIC;
        //         case EXTENSION:
        //             //return JavaExternalObjectType.EXTERNAL_OBJECT_TYPE;
        //         default:
        //             throw new ArgumentException();
        // --------------------
    }
}