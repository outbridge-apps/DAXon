////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
namespace OutSmart.DAXon.Api
{
    public class QName
    {

        /// <summary>
        /// QName denoting the schema type xs:string *
        /// </summary>
        public static readonly QName XS_STRING = new QName("xs", NamespaceConstant.SCHEMA, "string");
        /// <summary>
        /// QName denoting the schema type xs:boolean *
        /// </summary>
        public static readonly QName XS_BOOLEAN = new QName("xs", NamespaceConstant.SCHEMA, "boolean");
        /// <summary>
        /// QName denoting the schema type xs:decimal *
        /// </summary>
        public static readonly QName XS_DECIMAL = new QName("xs", NamespaceConstant.SCHEMA, "decimal");
        /// <summary>
        /// QName denoting the schema type xs:float *
        /// </summary>
        public static readonly QName XS_FLOAT = new QName("xs", NamespaceConstant.SCHEMA, "float");
        /// <summary>
        /// QName denoting the schema type xs:double *
        /// </summary>
        public static readonly QName XS_DOUBLE = new QName("xs", NamespaceConstant.SCHEMA, "double");
        /// <summary>
        /// QName denoting the schema type xs:duration *
        /// </summary>
        public static readonly QName XS_DURATION = new QName("xs", NamespaceConstant.SCHEMA, "duration");
        /// <summary>
        /// QName denoting the schema type xs:dateTime *
        /// </summary>
        public static readonly QName XS_DATE_TIME = new QName("xs", NamespaceConstant.SCHEMA, "dateTime");
        /// <summary>
        /// QName denoting the schema type xs:time *
        /// </summary>
        public static readonly QName XS_TIME = new QName("xs", NamespaceConstant.SCHEMA, "time");
        /// <summary>
        /// QName denoting the schema type xs:date *
        /// </summary>
        public static readonly QName XS_DATE = new QName("xs", NamespaceConstant.SCHEMA, "date");
        /// <summary>
        /// QName denoting the schema type xs:gYearMonth *
        /// </summary>
        public static readonly QName XS_G_YEAR_MONTH = new QName("xs", NamespaceConstant.SCHEMA, "gYearMonth");
        /// <summary>
        /// QName denoting the schema type xs:gYear *
        /// </summary>
        public static readonly QName XS_G_YEAR = new QName("xs", NamespaceConstant.SCHEMA, "gYear");
        /// <summary>
        /// QName denoting the schema type xs:gMonthDay *
        /// </summary>
        public static readonly QName XS_G_MONTH_DAY = new QName("xs", NamespaceConstant.SCHEMA, "gMonthDay");
        /// <summary>
        /// QName denoting the schema type xs:gDay *
        /// </summary>
        public static readonly QName XS_G_DAY = new QName("xs", NamespaceConstant.SCHEMA, "gDay");
        /// <summary>
        /// QName denoting the schema type xs:gMonth *
        /// </summary>
        public static readonly QName XS_G_MONTH = new QName("xs", NamespaceConstant.SCHEMA, "gMonth");
        /// <summary>
        /// QName denoting the schema type xs:hexBinary *
        /// </summary>
        public static readonly QName XS_HEX_BINARY = new QName("xs", NamespaceConstant.SCHEMA, "hexBinary");
        /// <summary>
        /// QName denoting the schema type xs:base64Binary *
        /// </summary>
        public static readonly QName XS_BASE64_BINARY = new QName("xs", NamespaceConstant.SCHEMA, "base64Binary");
        /// <summary>
        /// QName denoting the schema type xs:anyURI *
        /// </summary>
        public static readonly QName XS_ANY_URI = new QName("xs", NamespaceConstant.SCHEMA, "anyURI");
        /// <summary>
        /// QName denoting the schema type xs:QName *
        /// </summary>
        public static readonly QName XS_QNAME = new QName("xs", NamespaceConstant.SCHEMA, "QName");
        /// <summary>
        /// QName denoting the schema type xs:NOTATION *
        /// </summary>
        public static readonly QName XS_NOTATION = new QName("xs", NamespaceConstant.SCHEMA, "NOTATION");
        /// <summary>
        /// QName denoting the schema type xs:integer *
        /// </summary>
        public static readonly QName XS_INTEGER = new QName("xs", NamespaceConstant.SCHEMA, "integer");
        /// <summary>
        /// QName denoting the schema type xs:nonPositiveInteger *
        /// </summary>
        public static readonly QName XS_NON_POSITIVE_INTEGER = new QName("xs", NamespaceConstant.SCHEMA, "nonPositiveInteger");
        /// <summary>
        /// QName denoting the schema type xs:negativeInteger *
        /// </summary>
        public static readonly QName XS_NEGATIVE_INTEGER = new QName("xs", NamespaceConstant.SCHEMA, "negativeInteger");
        /// <summary>
        /// QName denoting the schema type xs:long *
        /// </summary>
        public static readonly QName XS_LONG = new QName("xs", NamespaceConstant.SCHEMA, "long");
        /// <summary>
        /// QName denoting the schema type xs:int *
        /// </summary>
        public static readonly QName XS_INT = new QName("xs", NamespaceConstant.SCHEMA, "int");
        /// <summary>
        /// QName denoting the schema type xs:short *
        /// </summary>
        public static readonly QName XS_SHORT = new QName("xs", NamespaceConstant.SCHEMA, "short");
        /// <summary>
        /// QName denoting the schema type xs:byte *
        /// </summary>
        public static readonly QName XS_BYTE = new QName("xs", NamespaceConstant.SCHEMA, "byte");
        /// <summary>
        /// QName denoting the schema type xs:nonNegativeInteger *
        /// </summary>
        public static readonly QName XS_NON_NEGATIVE_INTEGER = new QName("xs", NamespaceConstant.SCHEMA, "nonNegativeInteger");
        /// <summary>
        /// QName denoting the schema type xs:positiveInteger *
        /// </summary>
        public static readonly QName XS_POSITIVE_INTEGER = new QName("xs", NamespaceConstant.SCHEMA, "positiveInteger");
        /// <summary>
        /// QName denoting the schema type xs:unsignedLong *
        /// </summary>
        public static readonly QName XS_UNSIGNED_LONG = new QName("xs", NamespaceConstant.SCHEMA, "unsignedLong");
        /// <summary>
        /// QName denoting the schema type xs:unsignedInt *
        /// </summary>
        public static readonly QName XS_UNSIGNED_INT = new QName("xs", NamespaceConstant.SCHEMA, "unsignedInt");
        /// <summary>
        /// QName denoting the schema type xs:unsignedShort *
        /// </summary>
        public static readonly QName XS_UNSIGNED_SHORT = new QName("xs", NamespaceConstant.SCHEMA, "unsignedShort");
        /// <summary>
        /// QName denoting the schema type xs:unsignedByte *
        /// </summary>
        public static readonly QName XS_UNSIGNED_BYTE = new QName("xs", NamespaceConstant.SCHEMA, "unsignedByte");
        /// <summary>
        /// QName denoting the schema type xs:normalizedString *
        /// </summary>
        public static readonly QName XS_NORMALIZED_STRING = new QName("xs", NamespaceConstant.SCHEMA, "normalizedString");
        /// <summary>
        /// QName denoting the schema type xs:token *
        /// </summary>
        public static readonly QName XS_TOKEN = new QName("xs", NamespaceConstant.SCHEMA, "token");
        /// <summary>
        /// QName denoting the schema type xs:language *
        /// </summary>
        public static readonly QName XS_LANGUAGE = new QName("xs", NamespaceConstant.SCHEMA, "language");
        /// <summary>
        /// QName denoting the schema type xs:NMTOKEN *
        /// </summary>
        public static readonly QName XS_NMTOKEN = new QName("xs", NamespaceConstant.SCHEMA, "NMTOKEN");
        /// <summary>
        /// QName denoting the schema type xs:NMTOKENS *
        /// </summary>
        public static readonly QName XS_NMTOKENS = new QName("xs", NamespaceConstant.SCHEMA, "NMTOKENS");
        /// <summary>
        /// QName denoting the schema type xs:Name *
        /// </summary>
        public static readonly QName XS_NAME = new QName("xs", NamespaceConstant.SCHEMA, "Name");
        /// <summary>
        /// QName denoting the schema type xs:NCName *
        /// </summary>
        public static readonly QName XS_NCNAME = new QName("xs", NamespaceConstant.SCHEMA, "NCName");
        /// <summary>
        /// QName denoting the schema type xs:ID *
        /// </summary>
        public static readonly QName XS_ID = new QName("xs", NamespaceConstant.SCHEMA, "ID");
        /// <summary>
        /// QName denoting the schema type xs:IDREF *
        /// </summary>
        public static readonly QName XS_IDREF = new QName("xs", NamespaceConstant.SCHEMA, "IDREF");
        /// <summary>
        /// QName denoting the schema type xs:IDREFS *
        /// </summary>
        public static readonly QName XS_IDREFS = new QName("xs", NamespaceConstant.SCHEMA, "IDREFS");
        /// <summary>
        /// QName denoting the schema type xs:ENTITY *
        /// </summary>
        public static readonly QName XS_ENTITY = new QName("xs", NamespaceConstant.SCHEMA, "ENTITY");
        /// <summary>
        /// QName denoting the schema type xs:ENTITIES *
        /// </summary>
        public static readonly QName XS_ENTITIES = new QName("xs", NamespaceConstant.SCHEMA, "ENTITIES");
        /// <summary>
        /// QName denoting the schema type xs:untyped *
        /// </summary>
        public static readonly QName XS_UNTYPED = new QName("xs", NamespaceConstant.SCHEMA, "untyped");
        /// <summary>
        /// QName denoting the schema type xs:untypedAtomic *
        /// </summary>
        public static readonly QName XS_UNTYPED_ATOMIC = new QName("xs", NamespaceConstant.SCHEMA, "untypedAtomic");
        /// <summary>
        /// QName denoting the schema type xs:anyAtomicType *
        /// </summary>
        public static readonly QName XS_ANY_ATOMIC_TYPE = new QName("xs", NamespaceConstant.SCHEMA, "anyAtomicType");
        /// <summary>
        /// QName denoting the schema type xs:yearMonthDuration *
        /// </summary>
        public static readonly QName XS_YEAR_MONTH_DURATION = new QName("xs", NamespaceConstant.SCHEMA, "yearMonthDuration");
        /// <summary>
        /// QName denoting the schema type xs:dayTimeDuration *
        /// </summary>
        public static readonly QName XS_DAY_TIME_DURATION = new QName("xs", NamespaceConstant.SCHEMA, "dayTimeDuration");
        /// <summary>
        /// QName denoting the schema type xs:dateTimeStamp *
        /// </summary>
        public static readonly QName XS_DATE_TIME_STAMP = new QName("xs", NamespaceConstant.SCHEMA, "dateTimeStamp");
        private readonly StructuredQName sqName;

        public virtual string LocalName => sqName.GetLocalPart();

        public virtual string ClarkName
        {
            get
            {
                NamespaceUri uri = GetNamespaceUri();
                if (uri.IsEmpty())
                {
                    return LocalName;
                }
                else
                {
                    return "{" + uri + "}" + LocalName;
                }
            }
        }

        /*
   * The expanded name, as a string using the notation defined by the EQName production in XPath 3.0.
   * If the name @is in a @namespace, the resulting string takes the form <code>Q{uri}local</code>.
   * Otherwise, the value is the local part of the name.
   *
   */
        public virtual string EQName
        {
            get
            {
                NamespaceUri uri = GetNamespaceUri();
                if (uri.IsEmpty())
                {
                    return LocalName;
                }
                else
                {
                    return "Q{" + uri + "}" + LocalName;
                }
            }
        }
        public QName(string prefix, string uri, string localName)
        {
            sqName = new StructuredQName(prefix, NamespaceUri.Of(uri), localName);
        }

        public QName(string uri, string lexical)
        {
            uri = (uri == null ? "" : uri);
            int colon = lexical.IndexOf(':');
            if (colon < 0)
            {
                sqName = new StructuredQName("", NamespaceUri.Of(uri), lexical);
            }
            else
            {
                string prefix = lexical.Substring(0, colon);
                string local = lexical.Substring(colon + 1);
                sqName = new StructuredQName(prefix, NamespaceUri.Of(uri), local);
            }
        }

        public QName(NamespaceUri uri, string lexical)
        {
            int colon = lexical.IndexOf(':');
            if (colon < 0)
            {
                sqName = new StructuredQName("", uri, lexical);
            }
            else
            {
                string prefix = lexical.Substring(0, colon);
                string local = lexical.Substring(colon + 1);
                sqName = new StructuredQName(prefix, uri, local);
            }
        }

        public QName(string localName)
        {
            int colon = localName.IndexOf(':');
            if (colon < 0)
            {
                sqName = new StructuredQName("", NamespaceUri.NULL, localName);
            }
            else
            {
                throw new ArgumentException("Local name contains a colon");
            }
        }

        public QName(string lexicalQName, XdmNode element)
        {
            if (lexicalQName.StartsWith("{", StringComparison.Ordinal))
            {
                lexicalQName = "Q" + lexicalQName;
            }

            try
            {
                NodeInfo node = element.UnderlyingValue;
                sqName = StructuredQName.FromLexicalQName((lexicalQName), true, true, node.AllNamespaces);
            }
            catch (XPathException err)
            {
                throw new ArgumentException(err.Message, err);
            }
        }

        public QName(System.Xml.XmlQualifiedName qName)
        {
            sqName = new StructuredQName("", NamespaceUri.Of(qName.Namespace), qName.Name);
        }

        public QName(StructuredQName sqName)
        {
            this.sqName = sqName ?? throw new NullReferenceException();
        }

        public static QName FromClarkName(string expandedName)
        {
            string namespaceURI;
            string localName;
            if (expandedName == null || (expandedName.Length == 0))
            {
                throw new ArgumentException("Supplied Clark name is null or empty");
            }

            if (expandedName[0] == '{')
            {
                int closeBrace = expandedName.IndexOf('}');
                if (closeBrace < 0)
                {
                    throw new ArgumentException("No closing '}' in Clark name");
                }

                namespaceURI = expandedName.Substring(1, closeBrace - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                if (closeBrace == expandedName.Length)
                {
                    throw new ArgumentException("Missing local part in Clark name");
                }

                localName = expandedName.Substring(closeBrace + 1);
            }
            else
            {
                namespaceURI = "";
                localName = expandedName;
            }

            return new QName("", namespaceURI, localName);
        }

        public static QName FromEQName(string expandedName)
        {
            string namespaceURI;
            string localName;
            if (expandedName[0] == 'Q' && expandedName[1] == '{')
            {
                int closeBrace = expandedName.IndexOf('}');
                if (closeBrace < 0)
                {
                    throw new ArgumentException("No closing '}' in EQName");
                }

                namespaceURI = expandedName.Substring(2, closeBrace - 2) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                if (closeBrace == expandedName.Length)
                {
                    throw new ArgumentException("Missing local part in EQName");
                }

                localName = expandedName.Substring(closeBrace + 1);
            }
            else
            {
                namespaceURI = "";
                localName = expandedName;
            }

            return new QName("", namespaceURI, localName);
        }

        public virtual bool IsValid(Processor processor)
        {
            string prefix = GetPrefix();
            if (!(prefix.Length == 0) && !NameChecker.IsValidNCName(prefix))
            {
                return false;
            }

            return NameChecker.IsValidNCName(LocalName);
        }

        public virtual string GetPrefix()
        {
            return sqName.GetPrefix();
        }

        public virtual string GetNamespace()
        {
            return sqName.GetNamespaceUri().ToString();
        }

        // GetNamespaceURI() lived here too, with a body identical to GetNamespace() above. It had no
        // callers, and differing from GetNamespaceUri only by case made the pair non-CLS-compliant.
        public virtual NamespaceUri GetNamespaceUri()
        {
            return sqName.GetNamespaceUri();
        }

        public override string ToString()
        {
            return sqName.DisplayName;
        }

        public override int GetHashCode()
        {
            return sqName.GetHashCode();
        }

        public override bool Equals(object other)
        {
            return other is QName && sqName.Equals(((QName)other).sqName);
        }

        public virtual StructuredQName GetStructuredQName()
        {
            return sqName;
        }
    }
}