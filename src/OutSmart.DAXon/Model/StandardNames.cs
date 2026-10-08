////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    internal abstract class StandardNames
    {
        private const int DFLT_NS = 0;
        private const int XSL_NS = 1;
        private const int SAXON_NS = 2;
        private const int XML_NS = 3;
        private const int XS_NS = 4;
        private const int XSI_NS = 5;
        public const int DFLT = 0; //   0
        public const int XSL = 128; // 128
        public const int SAXON = 128 * 2; // 256
        public const int XML = 128 * 3; // 384
        public const int XS = 128 * 4; // 512
        public const int XSI = 128 * 5; // 640
        public const int XSL_ACCEPT = XSL;
        public const int XSL_ACCUMULATOR = XSL + 1;
        public const int XSL_ACCUMULATOR_RULE = XSL + 2;
        public const int XSL_ANALYZE_STRING = XSL + 3;
        public const int XSL_APPLY_IMPORTS = XSL + 4;
        public const int XSL_APPLY_TEMPLATES = XSL + 5;
        public const int XSL_ARRAY = XSL + 6;
        public const int XSL_ARRAY_MEMBER = XSL + 7;
        public const int XSL_ASSERT = XSL + 8;
        public const int XSL_ATTRIBUTE = XSL + 9;
        public const int XSL_ATTRIBUTE_SET = XSL + 10;
        public const int XSL_BREAK = XSL + 11;
        public const int XSL_CALL_TEMPLATE = XSL + 12;
        public const int XSL_CATCH = XSL + 13;
        public const int XSL_CHARACTER_MAP = XSL + 14;
        public const int XSL_CHOOSE = XSL + 15;
        public const int XSL_COMMENT = XSL + 16;
        public const int XSL_CONTEXT_ITEM = XSL + 17;
        public const int XSL_COPY = XSL + 18;
        public const int XSL_COPY_OF = XSL + 19;
        public const int XSL_DECIMAL_FORMAT = XSL + 20;
        public const int XSL_DOCUMENT = XSL + 22;
        public const int XSL_ELEMENT = XSL + 23;
        public const int XSL_EXPOSE = XSL + 24;
        public const int XSL_EVALUATE = XSL + 25;
        public const int XSL_FALLBACK = XSL + 26;
        public const int XSL_FOR_EACH = XSL + 27;
        public const int XSL_FOR_EACH_GROUP = XSL + 28;
        public const int XSL_FORK = XSL + 31;
        public const int XSL_FUNCTION = XSL + 32;
        public const int XSL_GLOBAL_CONTEXT_ITEM = XSL + 33;
        public const int XSL_IF = XSL + 34;
        public const int XSL_IMPORT = XSL + 35;
        public const int XSL_IMPORT_SCHEMA = XSL + 36;
        public const int XSL_INCLUDE = XSL + 37;
        public const int XSL_ITEM_TYPE = XSL + 38;
        public const int XSL_ITERATE = XSL + 40;
        public const int XSL_KEY = XSL + 41;
        public const int XSL_MAP = XSL + 42;
        public const int XSL_MAP_ENTRY = XSL + 43;
        public const int XSL_MATCHING_SUBSTRING = XSL + 44;
        public const int XSL_MERGE = XSL + 45;
        public const int XSL_MERGE_ACTION = XSL + 46;
        public const int XSL_MERGE_KEY = XSL + 47;
        public const int XSL_MERGE_SOURCE = XSL + 48;
        public const int XSL_MESSAGE = XSL + 50;
        public const int XSL_MODE = XSL + 51;
        public const int XSL_NAMESPACE = XSL + 52;
        public const int XSL_NAMESPACE_ALIAS = XSL + 53;
        public const int XSL_NEXT_ITERATION = XSL + 54;
        public const int XSL_NEXT_MATCH = XSL + 55;
        public const int XSL_NON_MATCHING_SUBSTRING = XSL + 56;
        public const int XSL_NOTE = XSL + 57;
        public const int XSL_NUMBER = XSL + 58;
        public const int XSL_OTHERWISE = XSL + 59;
        public const int XSL_ON_COMPLETION = XSL + 60;
        public const int XSL_ON_EMPTY = XSL + 61;
        public const int XSL_ON_NON_EMPTY = XSL + 62;
        public const int XSL_OUTPUT = XSL + 63;
        public const int XSL_OVERRIDE = XSL + 64;
        public const int XSL_OUTPUT_CHARACTER = XSL + 65;
        public const int XSL_PACKAGE = XSL + 66;
        public const int XSL_PARAM = XSL + 67;
        public const int XSL_PERFORM_SORT = XSL + 70;
        public const int XSL_PRESERVE_SPACE = XSL + 71;
        public const int XSL_PROCESSING_INSTRUCTION = XSL + 72;
        public const int XSL_RESULT_DOCUMENT = XSL + 73;
        public const int XSL_SEQUENCE = XSL + 74;
        public const int XSL_SORT = XSL + 75;
        public const int XSL_SOURCE_DOCUMENT = XSL + 76;
        public const int XSL_STRIP_SPACE = XSL + 77;
        public const int XSL_STYLESHEET = XSL + 80;
        public const int XSL_SWITCH = XSL + 81;
        public const int XSL_TEMPLATE = XSL + 82;
        public const int XSL_TEXT = XSL + 83;
        public const int XSL_TRANSFORM = XSL + 84;
        public const int XSL_TRY = XSL + 85;
        public const int XSL_USE_PACKAGE = XSL + 86;
        public const int XSL_VALUE_OF = XSL + 87;
        public const int XSL_VARIABLE = XSL + 90;
        public const int XSL_WHEN = XSL + 91;
        public const int XSL_WHERE_POPULATED = XSL + 92;
        public const int XSL_WITH_PARAM = XSL + 93;
        public const int XSL_DEFAULT_COLLATION = XSL + 100;
        public const int XSL_DEFAULT_MODE = XSL + 101;
        public const int XSL_DEFAULT_VALIDATION = XSL + 102;
        public const int XSL_EXCLUDE_RESULT_PREFIXES = XSL + 103;
        public const int XSL_EXPAND_TEXT = XSL + 104;
        public const int XSL_EXTENSION_ELEMENT_PREFIXES = XSL + 105;
        public const int XSL_INHERIT_NAMESPACES = XSL + 106;
        public const int XSL_TYPE = XSL + 107;
        public const int XSL_USE_ATTRIBUTE_SETS = XSL + 108;
        public const int XSL_USE_WHEN = XSL + 109;
        public const int XSL_VALIDATION = XSL + 110;
        public const int XSL_VERSION = XSL + 111;
        public const int XSL_XPATH_DEFAULT_NAMESPACE = XSL + 112;
        public const int SAXON_ASSIGN = SAXON + 1;
        public const int SAXON_DEEP_UPDATE = SAXON + 3;
        public const int SAXON_DO = SAXON + 6;
        public const int SAXON_DOCTYPE = SAXON + 7;
        public const int SAXON_ENTITY_REF = SAXON + 8;
        public const int SAXON_WHILE = SAXON + 15;
        // Schema extension elements
        public const int SAXON_PARAM = SAXON + 20;
        public const int SAXON_PREPROCESS = SAXON + 21;
        public const int SAXON_DISTINCT = SAXON + 22;
        public const int SAXON_ORDER = SAXON + 23;
        public const int XML_BASE = XML + 1;
        public const int XML_SPACE = XML + 2;
        public const int XML_LANG = XML + 3;
        public const int XML_ID = XML + 4;
        public const int XML_LANG_TYPE = XML + 5;
        public const int XML_SPACE_TYPE = XML + 6;
        public const int XS_STRING = XS + 1;
        public const int XS_BOOLEAN = XS + 2;
        public const int XS_DECIMAL = XS + 3;
        public const int XS_FLOAT = XS + 4;
        public const int XS_DOUBLE = XS + 5;
        public const int XS_DURATION = XS + 6;
        public const int XS_DATE_TIME = XS + 7;
        public const int XS_TIME = XS + 8;
        public const int XS_DATE = XS + 9;
        public const int XS_G_YEAR_MONTH = XS + 10;
        public const int XS_G_YEAR = XS + 11;
        public const int XS_G_MONTH_DAY = XS + 12;
        public const int XS_G_DAY = XS + 13;
        public const int XS_G_MONTH = XS + 14;
        public const int XS_HEX_BINARY = XS + 15;
        public const int XS_BASE64_BINARY = XS + 16;
        public const int XS_ANY_URI = XS + 17;
        public const int XS_QNAME = XS + 18;
        public const int XS_NOTATION = XS + 19;
        public const int XS_INTEGER = XS + 21;
        // Note that any type code <= XS_INTEGER is considered to represent a
        // primitive type: see Type.isPrimitiveType()
        public const int XS_NON_POSITIVE_INTEGER = XS + 22;
        public const int XS_NEGATIVE_INTEGER = XS + 23;
        public const int XS_LONG = XS + 24;
        public const int XS_INT = XS + 25;
        public const int XS_SHORT = XS + 26;
        public const int XS_BYTE = XS + 27;
        public const int XS_NON_NEGATIVE_INTEGER = XS + 28;
        public const int XS_POSITIVE_INTEGER = XS + 29;
        public const int XS_UNSIGNED_LONG = XS + 30;
        public const int XS_UNSIGNED_INT = XS + 31;
        public const int XS_UNSIGNED_SHORT = XS + 32;
        public const int XS_UNSIGNED_BYTE = XS + 33;
        public const int XS_NORMALIZED_STRING = XS + 41;
        public const int XS_TOKEN = XS + 42;
        public const int XS_LANGUAGE = XS + 43;
        public const int XS_NMTOKEN = XS + 44;
        public const int XS_NMTOKENS = XS + 45; // NB: list type
        public const int XS_NAME = XS + 46;
        public const int XS_NCNAME = XS + 47;
        public const int XS_ID = XS + 48;
        public const int XS_IDREF = XS + 49;
        public const int XS_IDREFS = XS + 50; // NB: list type
        public const int XS_ENTITY = XS + 51;
        public const int XS_ENTITIES = XS + 52; // NB: list type
        public const int XS_DATE_TIME_STAMP = XS + 53;
        public const int XS_ANY_TYPE = XS + 60;
        public const int XS_ANY_SIMPLE_TYPE = XS + 61;
        //public static final int XS_INVALID_NAME = XS + 62;
        public const int XS_ERROR = XS + 63;
        public const int XS_ALL = XS + 64;
        public const int XS_ALTERNATIVE = XS + 65;
        public const int XS_ANNOTATION = XS + 66;
        public const int XS_ANY = XS + 67;
        public const int XS_ANY_ATTRIBUTE = XS + 68;
        public const int XS_APPINFO = XS + 69;
        public const int XS_ASSERT = XS + 70;
        public const int XS_ASSERTION = XS + 71;
        public const int XS_ATTRIBUTE = XS + 72;
        public const int XS_ATTRIBUTE_GROUP = XS + 73;
        public const int XS_CHOICE = XS + 74;
        public const int XS_COMPLEX_CONTENT = XS + 75;
        public const int XS_COMPLEX_TYPE = XS + 76;
        public const int XS_DEFAULT_OPEN_CONTENT = XS + 77;
        public const int XS_DOCUMENTATION = XS + 78;
        public const int XS_ELEMENT = XS + 79;
        public const int XS_ENUMERATION = XS + 80;
        public const int XS_EXTENSION = XS + 81;
        public const int XS_FIELD = XS + 82;
        public const int XS_FRACTION_DIGITS = XS + 83;
        public const int XS_GROUP = XS + 84;
        public const int XS_IMPORT = XS + 85;
        public const int XS_INCLUDE = XS + 86;
        public const int XS_KEY = XS + 87;
        public const int XS_KEYREF = XS + 88;
        public const int XS_LENGTH = XS + 89;
        public const int XS_LIST = XS + 90;
        public const int XS_MAX_EXCLUSIVE = XS + 91;
        public const int XS_MAX_INCLUSIVE = XS + 92;
        public const int XS_MAX_LENGTH = XS + 93;
        public const int XS_MAX_SCALE = XS + 94;
        public const int XS_MIN_EXCLUSIVE = XS + 95;
        public const int XS_MIN_INCLUSIVE = XS + 96;
        public const int XS_MIN_LENGTH = XS + 97;
        public const int XS_MIN_SCALE = XS + 98;
        public const int XS_notation = XS + 99;
        public const int XS_OPEN_CONTENT = XS + 100;
        public const int XS_OVERRIDE = XS + 101;
        public const int XS_PATTERN = XS + 102;
        public const int XS_REDEFINE = XS + 103;
        public const int XS_RESTRICTION = XS + 104;
        public const int XS_SCHEMA = XS + 105;
        public const int XS_SELECTOR = XS + 106;
        public const int XS_SEQUENCE = XS + 107;
        public const int XS_SIMPLE_CONTENT = XS + 108;
        public const int XS_SIMPLE_TYPE = XS + 109;
        public const int XS_EXPLICIT_TIMEZONE = XS + 110;
        public const int XS_TOTAL_DIGITS = XS + 111;
        public const int XS_UNION = XS + 112;
        public const int XS_UNIQUE = XS + 113;
        public const int XS_WHITE_SPACE = XS + 114;
        public const int XS_UNTYPED = XS + 118;
        public const int XS_UNTYPED_ATOMIC = XS + 119;
        public const int XS_ANY_ATOMIC_TYPE = XS + 120;
        public const int XS_YEAR_MONTH_DURATION = XS + 121;
        public const int XS_DAY_TIME_DURATION = XS + 122;
        public const int XS_NUMERIC = XS + 123;
        public const int XSI_TYPE = XSI + 1;
        public const int XSI_NIL = XSI + 2;
        public const int XSI_SCHEMA_LOCATION = XSI + 3;
        public const int XSI_NO_NAMESPACE_SCHEMA_LOCATION = XSI + 4;
        public const int XSI_SCHEMA_LOCATION_TYPE = XSI + 5;
        private static readonly string SAXON_B = '{' + NamespaceConstant.SAXON + '}';
        public static readonly string SAXON_ASYCHRONOUS = SAXON_B + "asynchronous";
        public static readonly INodeName XML_ID_NAME = new FingerprintedQName("xml", NamespaceUri.XML, "id", XML_ID);
        private static readonly string[] localNames = new string[1023];
        private static readonly Dictionary<string, int> lookup = new Dictionary<string, int>(1023);
        public static StructuredQName[] errorVariables = new[]
        {
            new StructuredQName("err", NamespaceUri.ERR, "code"),
            new StructuredQName("err", NamespaceUri.ERR, "description"),
            new StructuredQName("err", NamespaceUri.ERR, "value"),
            new StructuredQName("err", NamespaceUri.ERR, "module"),
            new StructuredQName("err", NamespaceUri.ERR, "line-number"),
            new StructuredQName("err", NamespaceUri.ERR, "column-number"),
            new StructuredQName("err", NamespaceUri.ERR, "additional")
        };

        static StandardNames()
        {
            BindXSLTName(XSL_ACCEPT, "accept");
            BindXSLTName(XSL_ACCUMULATOR, "accumulator");
            BindXSLTName(XSL_ACCUMULATOR_RULE, "accumulator-rule");
            BindXSLTName(XSL_ANALYZE_STRING, "analyze-string");
            BindXSLTName(XSL_APPLY_IMPORTS, "apply-imports");
            BindXSLTName(XSL_APPLY_TEMPLATES, "apply-templates");
            BindXSLTName(XSL_ACCEPT, "accept");
            BindXSLTName(XSL_ARRAY, "array");
            BindXSLTName(XSL_ARRAY_MEMBER, "array-member");
            BindXSLTName(XSL_ASSERT, "assert");
            BindXSLTName(XSL_ATTRIBUTE, "attribute");
            BindXSLTName(XSL_ATTRIBUTE_SET, "attribute-set");
            BindXSLTName(XSL_BREAK, "break");
            BindXSLTName(XSL_CALL_TEMPLATE, "call-template");
            BindXSLTName(XSL_CATCH, "catch");
            BindXSLTName(XSL_CHARACTER_MAP, "character-map");
            BindXSLTName(XSL_CHOOSE, "choose");
            BindXSLTName(XSL_COMMENT, "comment");
            BindXSLTName(XSL_CONTEXT_ITEM, "context-item");
            BindXSLTName(XSL_COPY, "copy");
            BindXSLTName(XSL_COPY_OF, "copy-of");
            BindXSLTName(XSL_DECIMAL_FORMAT, "decimal-format");
            BindXSLTName(XSL_DOCUMENT, "document");
            BindXSLTName(XSL_ELEMENT, "element");
            BindXSLTName(XSL_EVALUATE, "evaluate");
            BindXSLTName(XSL_EXPOSE, "expose");
            BindXSLTName(XSL_FALLBACK, "fallback");
            BindXSLTName(XSL_FOR_EACH, "for-each");
            BindXSLTName(XSL_FOR_EACH_GROUP, "for-each-group");
            BindXSLTName(XSL_FORK, "fork");
            BindXSLTName(XSL_FUNCTION, "function");
            BindXSLTName(XSL_GLOBAL_CONTEXT_ITEM, "global-context-item");
            BindXSLTName(XSL_IF, "if");
            BindXSLTName(XSL_IMPORT, "import");
            BindXSLTName(XSL_IMPORT_SCHEMA, "import-schema");
            BindXSLTName(XSL_INCLUDE, "include");
            BindXSLTName(XSL_ITEM_TYPE, "item-type");
            BindXSLTName(XSL_ITERATE, "iterate");
            BindXSLTName(XSL_KEY, "key");
            BindXSLTName(XSL_MAP, "map");
            BindXSLTName(XSL_MAP_ENTRY, "map-entry");
            BindXSLTName(XSL_MATCHING_SUBSTRING, "matching-substring");
            BindXSLTName(XSL_MERGE, "merge");
            BindXSLTName(XSL_MERGE_SOURCE, "merge-source");
            BindXSLTName(XSL_MERGE_ACTION, "merge-action");
            BindXSLTName(XSL_MERGE_KEY, "merge-key");
            BindXSLTName(XSL_MESSAGE, "message");
            BindXSLTName(XSL_MODE, "mode");
            BindXSLTName(XSL_NEXT_MATCH, "next-match");
            BindXSLTName(XSL_NUMBER, "number");
            BindXSLTName(XSL_NAMESPACE, "namespace");
            BindXSLTName(XSL_NAMESPACE_ALIAS, "namespace-alias");
            BindXSLTName(XSL_NEXT_ITERATION, "next-iteration");
            BindXSLTName(XSL_NON_MATCHING_SUBSTRING, "non-matching-substring");
            BindXSLTName(XSL_NOTE, "note");
            BindXSLTName(XSL_ON_COMPLETION, "on-completion");
            BindXSLTName(XSL_ON_EMPTY, "on-empty");
            BindXSLTName(XSL_ON_NON_EMPTY, "on-non-empty");
            BindXSLTName(XSL_OTHERWISE, "otherwise");
            BindXSLTName(XSL_OUTPUT, "output");
            BindXSLTName(XSL_OUTPUT_CHARACTER, "output-character");
            BindXSLTName(XSL_OVERRIDE, "override");
            BindXSLTName(XSL_PACKAGE, "package");
            BindXSLTName(XSL_PARAM, "param");
            BindXSLTName(XSL_PERFORM_SORT, "perform-sort");
            BindXSLTName(XSL_PRESERVE_SPACE, "preserve-space");
            BindXSLTName(XSL_PROCESSING_INSTRUCTION, "processing-instruction");
            BindXSLTName(XSL_RESULT_DOCUMENT, "result-document");
            BindXSLTName(XSL_SEQUENCE, "sequence");
            BindXSLTName(XSL_SORT, "sort");
            BindXSLTName(XSL_SOURCE_DOCUMENT, "source-document");
            BindXSLTName(XSL_STRIP_SPACE, "strip-space");
            BindXSLTName(XSL_STYLESHEET, "stylesheet");
            BindXSLTName(XSL_SWITCH, "switch");
            BindXSLTName(XSL_TEMPLATE, "template");
            BindXSLTName(XSL_TEXT, "text");
            BindXSLTName(XSL_TRANSFORM, "transform");
            BindXSLTName(XSL_TRY, "try");
            BindXSLTName(XSL_USE_PACKAGE, "use-package");
            BindXSLTName(XSL_VALUE_OF, "value-of");
            BindXSLTName(XSL_VARIABLE, "variable");
            BindXSLTName(XSL_WITH_PARAM, "with-param");
            BindXSLTName(XSL_WHEN, "when");
            BindXSLTName(XSL_WHERE_POPULATED, "where-populated");
            BindXSLTName(XSL_DEFAULT_COLLATION, "default-collation");
            BindXSLTName(XSL_DEFAULT_MODE, "default-mode");
            BindXSLTName(XSL_DEFAULT_VALIDATION, "default-validation");
            BindXSLTName(XSL_EXPAND_TEXT, "expand-text");
            BindXSLTName(XSL_EXCLUDE_RESULT_PREFIXES, "exclude-result-prefixes");
            BindXSLTName(XSL_EXTENSION_ELEMENT_PREFIXES, "extension-element-prefixes");
            BindXSLTName(XSL_INHERIT_NAMESPACES, "inherit-namespaces");
            BindXSLTName(XSL_TYPE, "type");
            BindXSLTName(XSL_USE_ATTRIBUTE_SETS, "use-attribute-sets");
            BindXSLTName(XSL_USE_WHEN, "use-when");
            BindXSLTName(XSL_VALIDATION, "validation");
            BindXSLTName(XSL_VERSION, "version");
            BindXSLTName(XSL_XPATH_DEFAULT_NAMESPACE, "xpath-default-namespace");
            BindSaxonName(SAXON_ASSIGN, "assign");
            BindSaxonName(SAXON_DEEP_UPDATE, "deep-update");
            BindSaxonName(SAXON_DISTINCT, "distinct");
            BindSaxonName(SAXON_DO, "do");
            BindSaxonName(SAXON_DOCTYPE, "doctype");
            BindSaxonName(SAXON_ENTITY_REF, "entity-ref");
            BindSaxonName(SAXON_ORDER, "order");
            BindSaxonName(SAXON_WHILE, "while");
            BindSaxonName(SAXON_PARAM, "param");
            BindSaxonName(SAXON_PREPROCESS, "preprocess");
            BindXMLName(XML_BASE, "base");
            BindXMLName(XML_SPACE, "space");
            BindXMLName(XML_LANG, "lang");
            BindXMLName(XML_ID, "id");
            BindXMLName(XML_LANG_TYPE, "_langType");
            BindXMLName(XML_SPACE_TYPE, "_spaceType");
            BindXSName(XS_STRING, "string");
            BindXSName(XS_BOOLEAN, "boolean");
            BindXSName(XS_DECIMAL, "decimal");
            BindXSName(XS_FLOAT, "float");
            BindXSName(XS_DOUBLE, "double");
            BindXSName(XS_DURATION, "duration");
            BindXSName(XS_DATE_TIME, "dateTime");
            BindXSName(XS_TIME, "time");
            BindXSName(XS_DATE, "date");
            BindXSName(XS_G_YEAR_MONTH, "gYearMonth");
            BindXSName(XS_G_YEAR, "gYear");
            BindXSName(XS_G_MONTH_DAY, "gMonthDay");
            BindXSName(XS_G_DAY, "gDay");
            BindXSName(XS_G_MONTH, "gMonth");
            BindXSName(XS_HEX_BINARY, "hexBinary");
            BindXSName(XS_BASE64_BINARY, "base64Binary");
            BindXSName(XS_ANY_URI, "anyURI");
            BindXSName(XS_QNAME, "QName");
            BindXSName(XS_NOTATION, "NOTATION");
            BindXSName(XS_NUMERIC, "numeric");
            BindXSName(XS_INTEGER, "integer");
            BindXSName(XS_NON_POSITIVE_INTEGER, "nonPositiveInteger");
            BindXSName(XS_NEGATIVE_INTEGER, "negativeInteger");
            BindXSName(XS_LONG, "long");
            BindXSName(XS_INT, "int");
            BindXSName(XS_SHORT, "short");
            BindXSName(XS_BYTE, "byte");
            BindXSName(XS_NON_NEGATIVE_INTEGER, "nonNegativeInteger");
            BindXSName(XS_POSITIVE_INTEGER, "positiveInteger");
            BindXSName(XS_UNSIGNED_LONG, "unsignedLong");
            BindXSName(XS_UNSIGNED_INT, "unsignedInt");
            BindXSName(XS_UNSIGNED_SHORT, "unsignedShort");
            BindXSName(XS_UNSIGNED_BYTE, "unsignedByte");
            BindXSName(XS_NORMALIZED_STRING, "normalizedString");
            BindXSName(XS_TOKEN, "token");
            BindXSName(XS_LANGUAGE, "language");
            BindXSName(XS_NMTOKEN, "NMTOKEN");
            BindXSName(XS_NMTOKENS, "NMTOKENS"); // NB: list type
            BindXSName(XS_NAME, "Name");
            BindXSName(XS_NCNAME, "NCName");
            BindXSName(XS_ID, "ID");
            BindXSName(XS_IDREF, "IDREF");
            BindXSName(XS_IDREFS, "IDREFS"); // NB: list type
            BindXSName(XS_ENTITY, "ENTITY");
            BindXSName(XS_ENTITIES, "ENTITIES"); // NB: list type
            BindXSName(XS_DATE_TIME_STAMP, "dateTimeStamp");
            BindXSName(XS_ANY_TYPE, "anyType");
            BindXSName(XS_ANY_SIMPLE_TYPE, "anySimpleType");

            BindXSName(XS_ERROR, "error");
            BindXSName(XS_ALL, "all");
            BindXSName(XS_ALTERNATIVE, "alternative");
            BindXSName(XS_ANNOTATION, "annotation");
            BindXSName(XS_ANY, "any");
            BindXSName(XS_ANY_ATTRIBUTE, "anyAttribute");
            BindXSName(XS_APPINFO, "appinfo");
            BindXSName(XS_ASSERT, "assert");
            BindXSName(XS_ASSERTION, "assertion");
            BindXSName(XS_ATTRIBUTE, "attribute");
            BindXSName(XS_ATTRIBUTE_GROUP, "attributeGroup");
            BindXSName(XS_CHOICE, "choice");
            BindXSName(XS_COMPLEX_CONTENT, "complexContent");
            BindXSName(XS_COMPLEX_TYPE, "complexType");
            BindXSName(XS_DEFAULT_OPEN_CONTENT, "defaultOpenContent");
            BindXSName(XS_DOCUMENTATION, "documentation");
            BindXSName(XS_ELEMENT, "element");
            BindXSName(XS_ENUMERATION, "enumeration");
            BindXSName(XS_EXPLICIT_TIMEZONE, "explicitTimezone");
            BindXSName(XS_EXTENSION, "extension");
            BindXSName(XS_FIELD, "field");
            BindXSName(XS_FRACTION_DIGITS, "fractionDigits");
            BindXSName(XS_GROUP, "group");
            BindXSName(XS_IMPORT, "import");
            BindXSName(XS_INCLUDE, "include");
            BindXSName(XS_KEY, "key");
            BindXSName(XS_KEYREF, "keyref");
            BindXSName(XS_LENGTH, "length");
            BindXSName(XS_LIST, "list");
            BindXSName(XS_MAX_EXCLUSIVE, "maxExclusive");
            BindXSName(XS_MAX_INCLUSIVE, "maxInclusive");
            BindXSName(XS_MAX_LENGTH, "maxLength");
            BindXSName(XS_MAX_SCALE, "maxScale");
            BindXSName(XS_MIN_EXCLUSIVE, "minExclusive");
            BindXSName(XS_MIN_INCLUSIVE, "minInclusive");
            BindXSName(XS_MIN_LENGTH, "minLength");
            BindXSName(XS_MIN_SCALE, "minScale");
            BindXSName(XS_notation, "notation");
            BindXSName(XS_OPEN_CONTENT, "openContent");
            BindXSName(XS_OVERRIDE, "override");
            BindXSName(XS_PATTERN, "pattern");
            BindXSName(XS_REDEFINE, "redefine");
            BindXSName(XS_RESTRICTION, "restriction");
            BindXSName(XS_SCHEMA, "schema");
            BindXSName(XS_SELECTOR, "selector");
            BindXSName(XS_SEQUENCE, "sequence");
            BindXSName(XS_SIMPLE_CONTENT, "simpleContent");
            BindXSName(XS_SIMPLE_TYPE, "simpleType");
            BindXSName(XS_TOTAL_DIGITS, "totalDigits");
            BindXSName(XS_UNION, "union");
            BindXSName(XS_UNIQUE, "unique");
            BindXSName(XS_WHITE_SPACE, "whiteSpace");
            BindXSName(XS_UNTYPED, "untyped");
            BindXSName(XS_UNTYPED_ATOMIC, "untypedAtomic");
            BindXSName(XS_ANY_ATOMIC_TYPE, "anyAtomicType");
            BindXSName(XS_YEAR_MONTH_DURATION, "yearMonthDuration");
            BindXSName(XS_DAY_TIME_DURATION, "dayTimeDuration");
            BindXSIName(XSI_TYPE, "type");
            BindXSIName(XSI_NIL, "nil");
            BindXSIName(XSI_SCHEMA_LOCATION, "schemaLocation");
            BindXSIName(XSI_NO_NAMESPACE_SCHEMA_LOCATION, "noNamespaceSchemaLocation");
            BindXSIName(XSI_SCHEMA_LOCATION_TYPE, "anonymous_schemaLocationType");
        }

        private static void BindXSLTName(int constant, string localName)
        {
            localNames[constant] = localName;
            lookup['{' + NamespaceConstant.XSLT + '}' + localName] = constant;
        }

        private static void BindSaxonName(int constant, string localName)
        {
            localNames[constant] = localName;
            lookup['{' + NamespaceConstant.SAXON + '}' + localName] = constant;
        }

        private static void BindXMLName(int constant, string localName)
        {
            localNames[constant] = localName;
            lookup['{' + NamespaceConstant.XML + '}' + localName] = constant;
        }

        private static void BindXSName(int constant, string localName)
        {
            localNames[constant] = localName;
            lookup['{' + NamespaceConstant.SCHEMA + '}' + localName] = constant;
        }

        private static void BindXSIName(int constant, string localName)
        {
            localNames[constant] = localName;
            lookup['{' + NamespaceConstant.SCHEMA_INSTANCE + '}' + localName] = constant;
        }

        public static int GetFingerprint(NamespaceUri uri, string localName)
        {
            return lookup.GetOrDefault('{' + uri.ToString() + '}' + localName, -1);
        }

        public static string GetLocalName(int fingerprint)
        {
            return localNames[fingerprint];
        }

        public static NamespaceUri GetURI(int fingerprint)
        {
            int c = fingerprint >> 7;
            switch (c)
            {
                case DFLT_NS:
                    return NamespaceUri.NULL;
                case XSL_NS:
                    return NamespaceUri.XSLT;
                case SAXON_NS:
                    return NamespaceUri.SAXON;
                case XML_NS:
                    return NamespaceUri.XML;
                case XS_NS:
                    return NamespaceUri.SCHEMA;
                case XSI_NS:
                    return NamespaceUri.SCHEMA_INSTANCE;
                default:
                    throw new ArgumentException("Unknown system fingerprint " + fingerprint);
            }
        }

        public static string GetPrefix(int fingerprint)
        {
            int c = fingerprint >> 7;
            switch (c)
            {
                case DFLT_NS:
                    return "";
                case XSL_NS:
                    return "xsl";
                case SAXON_NS:
                    return "saxon";
                case XML_NS:
                    return "xml";
                case XS_NS:
                    return "xs";
                case XSI_NS:
                    return "xsi";
                default:
                    return null;
            }
        }

        public static string GetDisplayName(int fingerprint)
        {
            if (fingerprint == -1)
            {
                return "(anonymous type)";
            }

            if (fingerprint > 1023)
            {
                return "(" + fingerprint + ')';
            }

            if ((fingerprint >> 7) == DFLT)
            {
                return GetLocalName(fingerprint);
            }

            return GetPrefix(fingerprint) + ':' + GetLocalName(fingerprint);
        }

        public static StructuredQName GetStructuredQName(int fingerprint)
        {
            return new StructuredQName(GetPrefix(fingerprint), GetURI(fingerprint), GetLocalName(fingerprint));
        }

        public static StructuredQName GetUnprefixedQName(int fingerprint)
        {
            return new StructuredQName("", GetURI(fingerprint), GetLocalName(fingerprint));
        }
    }
}