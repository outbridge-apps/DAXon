////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Lib
{
    internal sealed class NamespaceConstant
    {
        /// <summary>
        /// Fixed namespace name for XML: "http://www.w3.org/XML/1998/namespace".
        /// </summary>
        public const string XML = "http://www.w3.org/XML/1998/namespace";
        /// <summary>
        /// Fixed namespace name for XSLT: "http://www.w3.org/1999/XSL/Transform"
        /// </summary>
        public const string XSLT = "http://www.w3.org/1999/XSL/Transform";
        /// <summary>
        /// Current namespace name for SAXON (from 7.0 onwards): "http://saxon.sf.net/"
        /// </summary>
        public const string SAXON = "http://saxon.sf.net/";
        /// <summary>
        /// Old namespace name for SAXON6: "http://icl.com/saxon"
        /// </summary>
        public const string SAXON6 = "http://icl.com/saxon";
        /// <summary>
        /// Fixed namespace name for the export of a Saxon stylesheet package
        /// </summary>
        public const string SAXON_XSLT_EXPORT = "http://ns.saxonica.com/xslt/export";
        /// <summary>
        /// Namespace name for XML Schema: "http://www.w3.org/2001/XMLSchema"
        /// </summary>
        public const string SCHEMA = "http://www.w3.org/2001/XMLSchema";
        /// <summary>
        /// XML-schema-defined namespace for use in instance documents ("xsi")
        /// </summary>
        public const string SCHEMA_INSTANCE = "http://www.w3.org/2001/XMLSchema-instance";
        /// <summary>
        /// Namespace defined in XSD 1.1 for schema versioning
        /// </summary>
        public const string SCHEMA_VERSIONING = "http://www.w3.org/2007/XMLSchema-versioning";
        /// <summary>
        /// Fixed namespace name for SAXON SQL extension: "http://saxon.sf.net/sql"
        /// </summary>
        public const string SQL = "http://saxon.sf.net/sql";
        /// <summary>
        /// Fixed namespace name for EXSLT/Common: "http://exslt.org/common"
        /// </summary>
        public const string EXSLT_COMMON = "http://exslt.org/common";
        /// <summary>
        /// Fixed namespace name for EXSLT/math: "http://exslt.org/math"
        /// </summary>
        public const string EXSLT_MATH = "http://exslt.org/math";
        /// <summary>
        /// Fixed namespace name for EXSLT/sets: "http://exslt.org/sets"
        /// </summary>
        public const string EXSLT_SETS = "http://exslt.org/sets";
        /// <summary>
        /// Fixed namespace name for EXSLT/date: "http://exslt.org/dates-and-times"
        /// </summary>
        public const string EXSLT_DATES_AND_TIMES = "http://exslt.org/dates-and-times";
        /// <summary>
        /// Fixed namespace name for EXSLT/random: "http://exslt.org/random"
        /// </summary>
        public const string EXSLT_RANDOM = "http://exslt.org/random";
        /// <summary>
        /// The standard namespace for functions and operators
        /// </summary>
        public const string FN = "http://www.w3.org/2005/xpath-functions";
        /// <summary>
        /// The standard namespace for XQuery output declarations
        /// </summary>
        public const string OUTPUT = "http://www.w3.org/2010/xslt-xquery-serialization";
        /// <summary>
        /// The standard namespace for system error codes
        /// </summary>
        public const string ERR = "http://www.w3.org/2005/xqt-errors";
        /// <summary>
        /// Predefined XQuery namespace for local functions
        /// </summary>
        public const string LOCAL = "http://www.w3.org/2005/xquery-local-functions";
        /// <summary>
        /// Math namespace for the XPath 3.0 math functions
        /// </summary>
        public const string MATH = "http://www.w3.org/2005/xpath-functions/math";
        /// <summary>
        /// Namespace URI for XPath 3.0 functions associated with maps
        /// </summary>
        public const string MAP_FUNCTIONS = "http://www.w3.org/2005/xpath-functions/map";
        /// <summary>
        /// Namespace URI for XPath 3.1 functions associated with arrays
        /// </summary>
        public const string ARRAY_FUNCTIONS = "http://www.w3.org/2005/xpath-functions/array";
        /// <summary>
        /// The XHTML namespace http://www.w3.org/1999/xhtml
        /// </summary>
        public const string XHTML = "http://www.w3.org/1999/xhtml";
        /// <summary>
        /// The SVG namespace
        /// </summary>
        public const string SVG = "http://www.w3.org/2000/svg";
        /// <summary>
        /// The MathML namespace
        /// </summary>
        public const string MATHML = "http://www.w3.org/1998/Math/MathML";
        /// <summary>
        /// The XMLNS namespace (used in DOM)
        /// </summary>
        public const string XMLNS = "http://www.w3.org/2000/xmlns/";
        /// <summary>
        /// The XLink namespace
        /// </summary>
        public const string XLINK = "http://www.w3.org/1999/xlink";
        /// <summary>
        /// The xquery namespace for the XQuery 3.0 declare option
        /// </summary>
        public const string XQUERY = "http://www.w3.org/2012/xquery";
        /// <summary>
        /// Namespace for types representing external Java objects
        /// </summary>
        public const string JAVA_TYPE = "http://saxon.sf.net/java-type";
        /// <summary>
        /// Namespace for types representing external .NET objects
        /// </summary>
        public const string DOT_NET_TYPE = "http://saxon.sf.net/clitype";
        public const string ANONYMOUS = "http://ns.saxonica.com/anonymous-type";
        /// <summary>
        /// Namespace for the Saxon serialization of the schema component model
        /// </summary>
        public const string SCM = "http://ns.saxonica.com/schema-component-model";
        /// <summary>
        /// URI identifying the Saxon object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public const string OBJECT_MODEL_SAXON = "http://saxon.sf.net/jaxp/xpath/om";
        /// <summary>
        /// URI identifying the XOM object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public const string OBJECT_MODEL_XOM = "http://www.xom.nu/jaxp/xpath/xom";
        /// <summary>
        /// URI identifying the JDOM object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public const string OBJECT_MODEL_JDOM = "http://jdom.org/jaxp/xpath/jdom";
        /// <summary>
        /// URI identifying the AXIOM object model for use in the JAXP 1.3 XPath API
        /// </summary>
        // Note: this URI is a Saxon invention
        public const string OBJECT_MODEL_AXIOM = "http://ws.apache.org/jaxp/xpath/axiom";
        /// <summary>
        /// URI identifying the DOM4J object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public const string OBJECT_MODEL_DOM4J = "http://www.dom4j.org/jaxp/xpath/dom4j";
        /// <summary>
        /// URI identifying the .NET DOM object model (not used, but needed for consistency)
        /// </summary>
        public const string OBJECT_MODEL_DOT_NET_DOM = "http://saxon.sf.net/object-model/dotnet/dom";
        /// <summary>
        /// URI identifying the DOMINO object model (not used, but needed for consistency)
        /// </summary>
        public const string OBJECT_MODEL_DOMINO = "http://saxon.sf.net/object-model/domino";
        /// <summary>
        /// URI identifying the Unicode codepoint collation
        /// </summary>
        public const string CODEPOINT_COLLATION_URI = "http://www.w3.org/2005/xpath-functions/collation/codepoint";
        /// <summary>
        /// URI identifying the HTML5 ascii-case-blind collation
        /// </summary>
        public const string HTML5_CASE_BLIND_COLLATION_URI = "http://www.w3.org/2005/xpath-functions/collation/html-ascii-case-insensitive";
        /// <summary>
        /// URI for the names of generated variables
        /// </summary>
        public const string SAXON_GENERATED_VARIABLE = "http://saxon.sf.net/generated-variable";
        /// <summary>
        /// URI for the Saxon configuration file
        /// </summary>
        public const string SAXON_CONFIGURATION = "http://saxon.sf.net/ns/configuration";
        /// <summary>
        /// URI for the EXPath zip module
        /// </summary>
        public const string EXPATH_ZIP = "http://expath.org/ns/zip";
        /// <summary>
        /// URI for the user extension calls in SaxonJS
        /// </summary>
        public const string GLOBAL_JS = "http://saxonica.com/ns/globalJS";
        /// <summary>
        /// URI for the user extension calls in SaxonC for C++ and PHP
        /// </summary>
        public const string PHP = "http://php.net/xsl";
        /// <summary>
        /// URI for interactive XSLT extensions in Saxon-CE and SaxonJS
        /// </summary>
        public const string IXSL = "http://saxonica.com/ns/interactiveXSLT";

        public static string FindSimilarNamespace(string candidate)
        {
            if (IsSimilar(candidate, XML))
            {
                return XML;
            }
            else if (IsSimilar(candidate, SCHEMA))
            {
                return SCHEMA;
            }
            else if (IsSimilar(candidate, XSLT))
            {
                return XSLT;
            }
            else if (IsSimilar(candidate, SCHEMA_INSTANCE))
            {
                return SCHEMA_INSTANCE;
            }
            else if (IsSimilar(candidate, FN))
            {
                return FN;
            }
            else if (IsSimilar(candidate, SAXON))
            {
                return SAXON;
            }
            else if (IsSimilar(candidate, EXSLT_COMMON))
            {
                return EXSLT_COMMON;
            }
            else if (IsSimilar(candidate, EXSLT_MATH))
            {
                return EXSLT_MATH;
            }
            else if (IsSimilar(candidate, EXSLT_DATES_AND_TIMES))
            {
                return EXSLT_DATES_AND_TIMES;
            }
            else if (IsSimilar(candidate, EXSLT_RANDOM))
            {
                return EXSLT_RANDOM;
            }
            else if (IsSimilar(candidate, XHTML))
            {
                return XHTML;
            }
            else if (IsSimilar(candidate, ERR))
            {
                return ERR;
            }
            else if (IsSimilar(candidate, JAVA_TYPE))
            {
                return JAVA_TYPE;
            }
            else if (IsSimilar(candidate, DOT_NET_TYPE))
            {
                return DOT_NET_TYPE;
            }
            else
            {
                return null;
            }
        }

        private static bool IsSimilar(string s1, string s2)
        {
            s1 = Whitespace.RemoveAllWhitespace(s1);
            s2 = Whitespace.RemoveAllWhitespace(s2);
            if (s1.Equals(s2, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (s1.StartsWith(s2, StringComparison.Ordinal) && s1.Length - s2.Length < 3)
            {
                return true;
            }
            else if (s2.StartsWith(s1, StringComparison.Ordinal) && s2.Length - s1.Length < 3)
            {
                return true;
            }
            else if (s1.Length > 8 && Math.Abs(s2.Length - s1.Length) < 3)
            {
                int diff = 0;
                for (int i = 0; i < s1.Length; i++)
                {
                    char c1 = s1[i];
                    if (!((i < s2.Length && c1 == s2[i]) || (i > 0 && i < s2.Length - 1 && c1 == s2[i - 1]) || (i + 1 < s2.Length && c1 == s2[i + 1])))
                    {
                        diff++;
                    }
                }

                return diff < 3;
            }
            else
            {
                return false;
            }
        }
    }
}