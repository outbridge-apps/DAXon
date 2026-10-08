////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;

using OutSmart.DAXon.Values;
using OutSmart.DAXon.Lib;
namespace OutSmart.DAXon.Model
{
    public class NamespaceUri
    {
        // A map from strings to NamespaceUris. Concurrent (as in the Java original): Of() is called
        // while parsing source documents, and two threads first meeting new URIs at once would race
        // an unsynchronized Dictionary's resize. Grows with distinct URIs for the process lifetime.
        //
        // NOT a cache, and deliberately NOT bounded (round A3). NamespaceUri overrides neither
        // Equals nor GetHashCode nor ==, so every comparison in the engine - IsEmpty() against NULL,
        // IsReserved() against XSLT/FN/..., the built-in function-set dispatch, ~15 explicit
        // `== NamespaceUri.X` sites - is REFERENCE equality. This table is what makes that sound.
        // Evicting an entry would let the same URI be interned twice and silently compare unequal
        // to itself: the null namespace stops being empty, XSLT stops being reserved, built-ins
        // stop resolving. Scoping it per-Configuration fails for the same reason (the well-known
        // singletons are static). Bounding becomes possible only after equality is made value-based,
        // which would put a string compare on the hottest path in the engine. Watch InternedCount
        // instead; see docs/HOSTING.md.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, NamespaceUri> stringToNamespaceUri
            = new System.Collections.Concurrent.ConcurrentDictionary<string, NamespaceUri>();

        /// <summary>
        /// Distinct namespace URIs interned process-wide. Only ever grows, and is NOT released by
        /// disposing a Processor - this table outlives every Configuration. Diagnostic.
        /// </summary>
        public static int InternedCount => stringToNamespaceUri.Count;

        /// <summary>
        /// A URI representing the null namespace (actually, an empty string)
        /// </summary>
        public static readonly NamespaceUri NULL = NamespaceUri.Of("");
        /// <summary>
        /// Fixed namespace name for XML: NamespaceConstant.XML.
        /// </summary>
        public static readonly NamespaceUri XML = NamespaceUri.Of(NamespaceConstant.XML);
        /// <summary>
        /// Fixed namespace name for XSLT: NamespaceConstant.XSLT
        /// </summary>
        public static readonly NamespaceUri XSLT = NamespaceUri.Of(NamespaceConstant.XSLT);
        /// <summary>
        /// Current namespace name for SAXON (from 7.0 onwards): NamespaceConstant.SAXON
        /// </summary>
        public static readonly NamespaceUri SAXON = NamespaceUri.Of(NamespaceConstant.SAXON);
        /// <summary>
        /// Old namespace name for SAXON6: NamespaceConstant.SAXON6
        /// </summary>
        public static readonly NamespaceUri SAXON6 = NamespaceUri.Of(NamespaceConstant.SAXON6);
        /// <summary>
        /// Fixed namespace name for the export of a Saxon stylesheet package
        /// </summary>
        public static readonly NamespaceUri SAXON_XSLT_EXPORT = NamespaceUri.Of(NamespaceConstant.SAXON_XSLT_EXPORT);
        /// <summary>
        /// Namespace name for XML Schema: NamespaceConstant.SCHEMA
        /// </summary>
        public static readonly NamespaceUri SCHEMA = NamespaceUri.Of(NamespaceConstant.SCHEMA);
        /// <summary>
        /// XML-schema-defined namespace for use in instance documents ("xsi")
        /// </summary>
        public static readonly NamespaceUri SCHEMA_INSTANCE = NamespaceUri.Of(NamespaceConstant.SCHEMA_INSTANCE);
        /// <summary>
        /// Namespace defined in XSD 1.1 for schema versioning
        /// </summary>
        public static readonly NamespaceUri SCHEMA_VERSIONING = NamespaceUri.Of(NamespaceConstant.SCHEMA_VERSIONING);
        /// <summary>
        /// Fixed namespace name for SAXON SQL extension: NamespaceConstant.SQL
        /// </summary>
        public static readonly NamespaceUri SQL = NamespaceUri.Of(NamespaceConstant.SQL);
        /// <summary>
        /// Fixed namespace name for EXSLT/Common: NamespaceConstant.EXSLT_COMMON
        /// </summary>
        public static readonly NamespaceUri EXSLT_COMMON = NamespaceUri.Of(NamespaceConstant.EXSLT_COMMON);
        /// <summary>
        /// Fixed namespace name for EXSLT/math: NamespaceConstant.EXSLT_MATH
        /// </summary>
        public static readonly NamespaceUri EXSLT_MATH = NamespaceUri.Of(NamespaceConstant.EXSLT_MATH);
        /// <summary>
        /// Fixed namespace name for EXSLT/sets: NamespaceConstant.EXSLT_SETS
        /// </summary>
        public static readonly NamespaceUri EXSLT_SETS = NamespaceUri.Of(NamespaceConstant.EXSLT_SETS);
        /// <summary>
        /// Fixed namespace name for EXSLT/date: NamespaceConstant.EXSLT_DATES_AND_TIMES
        /// </summary>
        public static readonly NamespaceUri EXSLT_DATES_AND_TIMES = NamespaceUri.Of(NamespaceConstant.EXSLT_DATES_AND_TIMES);
        /// <summary>
        /// Fixed namespace name for EXSLT/random: NamespaceConstant.EXSLT_RANDOM
        /// </summary>
        public static readonly NamespaceUri EXSLT_RANDOM = NamespaceUri.Of(NamespaceConstant.EXSLT_RANDOM);
        /// <summary>
        /// The standard namespace for functions and operators
        /// </summary>
        public static readonly NamespaceUri FN = NamespaceUri.Of(NamespaceConstant.FN);
        /// <summary>
        /// The standard namespace for XQuery output declarations
        /// </summary>
        public static readonly NamespaceUri OUTPUT = NamespaceUri.Of(NamespaceConstant.OUTPUT);
        /// <summary>
        /// The standard namespace for system error codes
        /// </summary>
        public static readonly NamespaceUri ERR = NamespaceUri.Of(NamespaceConstant.ERR);
        /// <summary>
        /// Predefined XQuery namespace for local functions
        /// </summary>
        public static readonly NamespaceUri LOCAL = NamespaceUri.Of(NamespaceConstant.LOCAL);
        /// <summary>
        /// Math namespace for the XPath 3.0 math functions
        /// </summary>
        public static readonly NamespaceUri MATH = NamespaceUri.Of(NamespaceConstant.MATH);
        /// <summary>
        /// Namespace URI for XPath 3.0 functions associated with maps
        /// </summary>
        public static readonly NamespaceUri MAP_FUNCTIONS = NamespaceUri.Of(NamespaceConstant.MAP_FUNCTIONS);
        /// <summary>
        /// Namespace URI for XPath 3.1 functions associated with arrays
        /// </summary>
        public static readonly NamespaceUri ARRAY_FUNCTIONS = NamespaceUri.Of(NamespaceConstant.ARRAY_FUNCTIONS);
        /// <summary>
        /// Namespace URI for the EXPath Binary module
        /// </summary>
        public static readonly NamespaceUri EXPATH_BINARY = NamespaceUri.Of("http://expath.org/ns/binary");
        /// <summary>
        /// Namespace URI for the EXPath string module
        /// </summary>
        public static readonly NamespaceUri EXPATH_FILE = NamespaceUri.Of("http://expath.org/ns/file");
        /// <summary>
        /// The XHTML namespace http://www.w3.org/1999/xhtml
        /// </summary>
        public static readonly NamespaceUri XHTML = NamespaceUri.Of(NamespaceConstant.XHTML);
        /// <summary>
        /// The SVG namespace http://www.w3.org/2000/svg
        /// </summary>
        public static readonly NamespaceUri SVG = NamespaceUri.Of(NamespaceConstant.SVG);
        /// <summary>
        /// The MathML namespace http://www.w3.org/1998/Math/MathML
        /// </summary>
        public static readonly NamespaceUri MATHML = NamespaceUri.Of(NamespaceConstant.MATHML);
        /// <summary>
        /// The XMLNS namespace http://www.w3.org/2000/xmlns/ (used in DOM)
        /// </summary>
        public static readonly NamespaceUri XMLNS = NamespaceUri.Of(NamespaceConstant.XMLNS);
        /// <summary>
        /// The XLink namespace http://www.w3.org/1999/xlink
        /// </summary>
        public static readonly NamespaceUri XLINK = NamespaceUri.Of(NamespaceConstant.XLINK);
        /// <summary>
        /// The xquery namespace http://www.w3.org/2012/xquery for the XQuery 3.0 declare option
        /// </summary>
        public static readonly NamespaceUri XQUERY = NamespaceUri.Of(NamespaceConstant.XQUERY);
        /// <summary>
        /// Namespace for types representing external Java objects: http://saxon.sf.net/java-type
        /// </summary>
        public static readonly NamespaceUri JAVA_TYPE = NamespaceUri.Of(NamespaceConstant.JAVA_TYPE);
        /// <summary>
        /// Namespace for types representing external .NET objects
        /// </summary>
        public static readonly NamespaceUri DOT_NET_TYPE = NamespaceUri.Of(NamespaceConstant.DOT_NET_TYPE);
        public static readonly NamespaceUri ANONYMOUS = NamespaceUri.Of(NamespaceConstant.ANONYMOUS);
        /// <summary>
        /// Namespace for the Saxon serialization of the schema component model
        /// </summary>
        public static readonly NamespaceUri SCM = NamespaceUri.Of(NamespaceConstant.SCM);
        /// <summary>
        /// URI identifying the Saxon object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_SAXON = NamespaceUri.Of(NamespaceConstant.OBJECT_MODEL_SAXON);
        /// <summary>
        /// URI identifying the XOM object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_XOM = NamespaceUri.Of(NamespaceConstant.OBJECT_MODEL_XOM);
        /// <summary>
        /// URI identifying the JDOM object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_JDOM = NamespaceUri.Of(NamespaceConstant.OBJECT_MODEL_JDOM);
        /// <summary>
        /// URI identifying the AXIOM object model for use in the JAXP 1.3 XPath API
        /// </summary>
        // Note: this URI is a Saxon invention
        public static readonly NamespaceUri OBJECT_MODEL_AXIOM = NamespaceUri.Of(NamespaceConstant.OBJECT_MODEL_AXIOM);
        /// <summary>
        /// URI identifying the DOM4J object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_DOM4J = NamespaceUri.Of(NamespaceConstant.OBJECT_MODEL_DOM4J);
        /// <summary>
        /// URI identifying the .NET DOM object model (not used, but needed for consistency)
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_DOT_NET_DOM = NamespaceUri.Of(NamespaceConstant.OBJECT_MODEL_DOT_NET_DOM);
        /// <summary>
        /// URI identifying the DOMINO object model (not used, but needed for consistency)
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_DOMINO = NamespaceUri.Of(NamespaceConstant.OBJECT_MODEL_DOMINO);
        /// <summary>
        /// URI for the names of generated variables
        /// </summary>
        public static readonly NamespaceUri SAXON_GENERATED_VARIABLE = NamespaceUri.Of(NamespaceConstant.SAXON_GENERATED_VARIABLE);
        /// <summary>
        /// URI for the Saxon configuration file
        /// </summary>
        public static readonly NamespaceUri SAXON_CONFIGURATION = NamespaceUri.Of(NamespaceConstant.SAXON_CONFIGURATION);
        /// <summary>
        /// URI for the EXPath zip module
        /// </summary>
        public static readonly NamespaceUri EXPATH_ZIP = NamespaceUri.Of(NamespaceConstant.EXPATH_ZIP);
        /// <summary>
        /// URI for the user extension calls in SaxonJS
        /// </summary>
        public static readonly NamespaceUri GLOBAL_JS = NamespaceUri.Of(NamespaceConstant.GLOBAL_JS);
        /// <summary>
        /// URI for the user extension calls in SaxonC for C++ and PHP
        /// </summary>
        public static readonly NamespaceUri PHP = NamespaceUri.Of(NamespaceConstant.PHP);
        /// <summary>
        /// URI for interactive XSLT extensions in Saxon-CE and SaxonJS
        /// </summary>
        public static readonly NamespaceUri IXSL = NamespaceUri.Of(NamespaceConstant.IXSL);

        private readonly string stringContent;
        private readonly UnicodeString unicodeStringContent;
        private NamespaceUri(string content)
        {
            this.stringContent = content;
            this.unicodeStringContent = StringTool.FromCharSequence(content);
        }
        public static NamespaceUri Of(string content)
        {
            if (content == null)
            {
                content = "";
            }

            return stringToNamespaceUri.GetOrAdd(Whitespace.Trim(content), k => new NamespaceUri(k));
        }

        public static implicit operator string(NamespaceUri u) => u?.stringContent;
        public override string ToString()
        {
            return stringContent;
        }

        public virtual UnicodeString ToUnicodeString()
        {
            return unicodeStringContent;
        }

        public virtual bool IsEmpty()
        {
            return this == NamespaceUri.NULL;
        }

        public virtual StructuredQName QName(string localName)
        {
            return new StructuredQName("", this, localName);
        }
        public static NamespaceUri GetUriForConventionalPrefix(string prefix)
        {
            switch (prefix)
            {
                case "xsl":
                    return XSLT;
                case "fn":
                    return FN;
                case "xml":
                    return XML;
                case "xs":
                    return SCHEMA;
                case "xsi":
                    return SCHEMA_INSTANCE;
                case "err":
                    return ERR;
                case "ixsl":
                    return IXSL;
                case "js":
                    return GLOBAL_JS;
                case "saxon":
                    return SAXON;
                case "vv":
                    return SAXON_GENERATED_VARIABLE;
                case "math":
                    return MATH;
                case "map":
                    return MAP_FUNCTIONS;
                case "array":
                    return ARRAY_FUNCTIONS;
                default:
                    return null;
            }
        }

        public static bool IsReserved(NamespaceUri uri)
        {
            return uri != null && (uri.Equals(XSLT) || uri.Equals(FN) || uri.Equals(MATH) || uri.Equals(MAP_FUNCTIONS) || uri.Equals(ARRAY_FUNCTIONS) || uri.Equals(XML) || uri.Equals(SCHEMA) || uri.Equals(SCHEMA_INSTANCE) || uri.Equals(ERR) || uri.Equals(XMLNS));
        }

        public static bool IsReservedInQuery31(NamespaceUri uri)
        {
            return uri.Equals(FN) || uri.Equals(XML) || uri.Equals(SCHEMA) || uri.Equals(SCHEMA_INSTANCE) || uri.Equals(MATH) || uri.Equals(XQUERY) || uri.Equals(MAP_FUNCTIONS) || uri.Equals(ARRAY_FUNCTIONS);
        }
    }
}