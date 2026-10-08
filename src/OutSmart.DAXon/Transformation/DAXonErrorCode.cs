////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Transformation
{
    internal sealed class DAXonErrorCode
    {
        /// <summary>
        /// SXLM0001: stylesheet or query appears to be looping/recursing indefinitely
        /// </summary>
        public const string SXLM0001 = "SXLM0001";
        /// <summary>
        /// SXLM0002: a tree is nested deeper than the implementation's depth limit
        /// </summary>
        public const string SXLM0002 = "SXLM0002";
        /// <summary>
        /// SXLM0003: a call went over its memory limit (ProcessorOptions.MaxMemoryBytes)
        /// </summary>
        public const string SXLM0003 = "SXLM0003";
        /// <summary>
        /// SXCH0002: cannot supply output to ContentHandler because it is not well-formed
        /// </summary>
        public const string SXCH0002 = "SXCH0002";
        /// <summary>
        /// SXCH0003: error reported by the ContentHandler (SAXResult) to which the result tree was sent
        /// </summary>
        public const string SXCH0003 = "SXCH0003";
        /// <summary>
        /// SXCH0004: cannot load user-supplied ContentHandler
        /// </summary>
        public const string SXCH0004 = "SXCH0004";
        /// <summary>
        /// SXCH0005: invalid pseudo-attribute syntax
        /// </summary>
        public const string SXCH0005 = "SXCH0005";
        /// <summary>
        /// SXRE0001: stack overflow within regular expression evaluation
        /// </summary>
        public const string SXRE0001 = "SXRE0001";
        /// <summary>
        /// SXSE0001: cannot use character maps in an environment with no Controller
        /// </summary>
        public const string SXSE0001 = "SXSE0001";
        /// <summary>
        /// SXSE0002: cannot use output property saxon:supply-source-locator unless tracing was enabled at compile time
        /// </summary>
        public const string SXSE0002 = "SXSE0002";
        public const string SXXP0003 = "SXXP0003";
        public const string SXXP0004 = "SXXP0004";
        public const string SXXP0005 = "SXXP0005";
        /// <summary>
        /// SXXP0006: general error in schema processing/validation
        /// </summary>
        public const string SXXP0006 = "SXXP0006";
        /// <summary>
        /// SXSQ0001: value of argument to SQL instruction is not a JDBC Connection object
        /// </summary>
        public const string SXSQ0001 = "SXSQ0001";
        /// <summary>
        /// SXSQ0002: failed to close JDBC Connection
        /// </summary>
        public const string SXSQ0002 = "SXSQ0002";
        /// <summary>
        /// SXSQ0003: failed to open JDBC Connection
        /// </summary>
        public const string SXSQ0003 = "SXSQ0003";
        /// <summary>
        /// SXSQ0004: SQL Insert/Update/Delete action failed
        /// </summary>
        public const string SXSQ0004 = "SXSQ0004";
        /// <summary>
        /// SXSQ0005: Warning JDBC is not thread safe
        /// </summary>
        public const string SXSQ0005 = "SXSQ0005";
        /// <summary>
        /// SXJE0001:  Must supply an argument for a non-static extension function
        /// </summary>
        public const string SXJE0001 = "SXJE0001";
        /// <summary>
        /// SXJE0005: cannot convert xs:string to Java char unless the length is exactly one
        /// </summary>
        public const string SXJE0005 = "SXJE0005";
        /// <summary>
        /// SXJE0051: supplied Java List/Array contains a member that cannot be converted to an IItem
        /// </summary>
        public const string SXJE0051 = "SXJE0051";
        /// <summary>
        /// SXJE0052: exception thrown by extension function
        /// </summary>
        public const string SXJE0052 = "SXJE0052";
        /// <summary>
        /// SXJE0053: I/O error in saxon-read-binary-resource
        /// </summary>
        public const string SXJE0053 = "SXJE0053";
        /// <summary>
        /// SXJM0001: Error in arguments to saxon:send-mail
        /// </summary>
        public const string SXJM0001 = "SXJM0001";
        /// <summary>
        /// SXJM0002: Failure in saxon:send-mail reported by mail service
        /// </summary>
        public const string SXJM0002 = "SXJM0002";
        /// <summary>
        /// SXOR0001: XSD saxon:ordered constraint not satisfied
        /// </summary>
        public const string SXOR0001 = "SXOR0001";
        /// <summary>
        /// SXJX0001: integer in input to octets-to-base64Binary or octets-to-hexBinary is out of range 0-255
        /// </summary>
        public const string SXJX0001 = "SXJX0001";
        /// <summary>
        /// SXJS0001: Cannot export for Javascript if the stylesheet uses unsupported features
        /// </summary>
        public const string SXJS0001 = "SXJS0001";
        /// <summary>
        /// SXPK0001: No binding available for call-template instruction
        /// </summary>
        public const string SXPK0001 = "SXPK0001";
        /// <summary>
        /// SXPK0002: invalid content found in compiled package
        /// </summary>
        public const string SXPK0002 = "SXPK0002";
        /// <summary>
        /// SXPK0003: stylesheet package has unsatisfied schema dependency
        /// </summary>
        public const string SXPK0003 = "SXPK0003";
        /// <summary>
        /// SXPK0004: documentation namespace can be used only for documentation
        /// </summary>
        public const string SXPK0004 = "SXPK0004";
        /// <summary>
        /// SXPK0005: unresolved component reference in SEF file
        /// </summary>
        public const string SXPK0005 = "SXPK0005";
        /// <summary>
        /// SXRD0001: URI supplied to xsl:result-document does not identify a writable destination
        /// </summary>
        public const string SXRD0001 = "SXRD0001";
        /// <summary>
        /// SXRD0002: Base output URI for xsl:result-document is unknown
        /// </summary>
        public const string SXRD0002 = "SXRD0002";
        /// <summary>
        /// SXRD0003: Failure while closing the xsl:result-document destination after writing
        /// </summary>
        public const string SXRD0003 = "SXRD0003";
        /// <summary>
        /// SXRD0004: Unwritable file given as the result destination
        /// </summary>
        public const string SXRD0004 = "SXRD0004";
        /// <summary>
        /// SXST0001: Static error in template rule, found during JIT compilation
        /// </summary>
        public const string SXST0001 = "SXST0001";
        /// <summary>
        /// SXST0060: Template in a streaming mode is not streamable
        /// </summary>
        public const string SXST0060 = "SXST0060";
        /// <summary>
        /// SXST0061: Requested initial mode is streamable; must supply SAXSource or StreamSource
        /// </summary>
        public const string SXST0061 = "SXST0061";
        /// <summary>
        /// SXST0062: Component cannot be streamed, though it should be streamable
        /// </summary>
        public const string SXST0062 = "SXST0062";
        /// <summary>
        /// SXST0065: Cannot use tracing with streaming templates
        /// </summary>
        public const string SXST0065 = "SXST0065";
        /// <summary>
        /// SXST0066: Cannot disable optimization when xsl:stream is used
        /// </summary>
        public const string SXST0066 = "SXST0066";
        /// <summary>
        /// SXST0067: Internal problem executing expression in streaming mode
        /// </summary>
        public const string SXST0067 = "SXST0067";
        /// <summary>
        /// SXST0068: This configuration does not allow streaming
        /// </summary>
        public const string SXST0068 = "SXST0068";
        /// <summary>
        /// SXST0069: Exporting a stylesheet containing static references to XQuery functions
        /// </summary>
        public const string SXST0069 = "SXST0069";
        /// <summary>
        /// SXST0070: Exporting a stylesheet containing static references to external Java objects
        /// </summary>
        public const string SXST0070 = "SXST0070";
        /// <summary>
        /// SXST0071: Exporting a stylesheet containing static references to saxon:tabulate-maps instruction
        /// </summary>
        public const string SXST0071 = "SXST0071";
        /// <summary>
        /// SXST0072: Exporting a stylesheet containing extensions instruction
        /// </summary>
        public const string SXST0072 = "SXST0072";
        /// <summary>
        /// SXTA0001: unresolved type alias
        /// </summary>
        public const string SXTA0001 = "SXTA0001";
        /// <summary>
        /// SXTM0001: tabulate-maps: selecting an item with no pedigree
        /// </summary>
        public const string SXTM0001 = "SXTM0001";
        /// <summary>
        /// SXTO0001: transformation exceeded its configured time limit (cooperative deadline)
        /// </summary>
        public const string SXTO0001 = "SXTO0001";
        /// <summary>
        /// SXTT0001: field name not defined in tuple type
        /// </summary>
        public const string SXTT0001 = "SXTT0001";
        /// <summary>
        /// SXUP0081: attempt to update a non-updatable node
        /// </summary>
        public const string SXUP0081 = "SXUP0081";
        /// <summary>
        /// SXWN9000: miscellaneous warning message
        /// </summary>
        public const string SXWN9000 = "SXWN9000";
        /// <summary>
        /// SXWN9001: a variable declaration with no following siblings has no effect
        /// </summary>
        public const string SXWN9001 = "SXWN9001";
        /// <summary>
        /// SXWN9002: saxon:indent-spaces must be a positive integer
        /// </summary>
        public const string SXWN9002 = "SXWN9002";
        /// <summary>
        /// SXWN9003: saxon:require-well-formed must be "yes" or "no"
        /// </summary>
        public const string SXWN9003 = "SXWN9003";
        /// <summary>
        /// SXWN9004: saxon:next-in-chain cannot be specified dynamically
        /// </summary>
        public const string SXWN9004 = "SXWN9004";
        /// <summary>
        /// SXWN9005: The 'default' attribute of saxon:collation no longer has any effect
        /// </summary>
        public const string SXWN9005 = "SXWN9005";
        public const string SXWN9006 = "SXWN9006";
        /// <summary>
        /// SXWN9007: Cannot use reserved @namespace in extension-element-prefixes
        /// </summary>
        public const string SXWN9007 = "SXWN9007";
        public const string SXWN9008 = "SXWN9008";
        /// <summary>
        /// SXWN9009: an empty xsl:for-each or xsl:for-each-group has no effect
        /// </summary>
        public const string SXWN9009 = "SXWN9009";
        /// <summary>
        /// SXWN9010: saxon:recognize-binary must be "yes" or "no"
        /// </summary>
        public const string SXWN9010 = "SXWN9010";
        /// <summary>
        /// SXWN9011: saxon:memo-function ignored under Saxon-HE
        /// </summary>
        public const string SXWN9011 = "SXWN9011";
        /// <summary>
        /// SXWN9012: saxon:threads ignored when compiling with trace enabled
        /// </summary>
        public const string SXWN9012 = "SXWN9012";
        /// <summary>
        /// SXWN9013: saxon:threads ignored when not running under Saxon-EE
        /// </summary>
        public const string SXWN9013 = "SXWN9013";
        /// <summary>
        /// SXWN9014: xsl:function/@override is deprecated in 3.0
        /// </summary>
        public const string SXWN9014 = "SXWN9014";
        /// <summary>
        /// SXWN9015: Pattern will never match anything
        /// </summary>
        public const string SXWN9015 = "SXWN9015";
        /// <summary>
        /// SXWN9016: saxon:assign used with multi-threading enabled
        /// </summary>
        public const string SXWN9016 = "SXWN9016";
        /// <summary>
        /// SXWN9017: saxon:copy-of copying accumulators pointlessly
        /// </summary>
        public const string SXWN9017 = "SXWN9017";
        /// <summary>
        /// SXWN9018: warning during schema processing
        /// </summary>
        public const string SXWN9018 = "SXWN9018";
        /// <summary>
        /// SXWN9019: stylesheet module included or imported more than once
        /// </summary>
        public const string SXWN9019 = "SXWN9019";
        /// <summary>
        /// SXWN9020: unrecognized XSLT version
        /// </summary>
        public const string SXWN9020 = "SXWN9020";
        /// <summary>
        /// SXWN9021: extension attribute ignored because not recognized in this Saxon version
        /// </summary>
        public const string SXWN9021 = "SXWN9021";
        /// <summary>
        /// SXWN9022: warning returned by regular expression compiler
        /// </summary>
        public const string SXWN9022 = "SXWN9022";
        /// <summary>
        /// SXWN9023: mode="#current" specified when not inside xsl:template
        /// </summary>
        public const string SXWN9023 = "SXWN9023";
        /// <summary>
        /// SXWN9024: Fallback to non-streamed execution
        /// </summary>
        public const string SXWN9024 = "SXWN9024";
        /// <summary>
        /// SXWN9025: Comparison will always be false
        /// </summary>
        public const string SXWN9025 = "SXWN9025";
        /// <summary>
        /// SXWN9026: The only value that can pass type checking is an empty sequence
        /// </summary>
        public const string SXWN9026 = "SXWN9026";
        /// <summary>
        /// SXWN9027: Expression is valid statically, but will always fail if executed
        /// </summary>
        public const string SXWN9027 = "SXWN9027";
        /// <summary>
        /// SXWN9028: XPath Construct A/[XYZ] is probably not intended: try A/*[XYZ]
        /// </summary>
        public const string SXWN9028 = "SXWN9028";
        /// <summary>
        /// SXWN9029: xsl:on-empty/xsl:on-non-empty in this context has no effect
        /// </summary>
        public const string SXWN9029 = "SXWN9029";
        /// <summary>
        /// SXWN9030: creating an attribute or namespace is likely to fail because children have already been created
        /// </summary>
        public const string SXWN9030 = "SXWN9030";
        /// <summary>
        /// SXWN9031: lax validation has no effect because there is no element/attribute declaration in the schema
        /// </summary>
        public const string SXWN9031 = "SXWN9031";
        /// <summary>
        /// SXWN9032: Function result should be computed using xsl:sequence, not xsl:value-of
        /// </summary>
        public const string SXWN9032 = "SXWN9032";
        /// <summary>
        /// SXWN9033: Value of sort key doesn't depend on the context item
        /// </summary>
        public const string SXWN9033 = "SXWN9033";
        /// <summary>
        /// SXWN9034: Cannot resolve relative collation URI
        /// </summary>
        public const string SXWN9034 = "SXWN9034";
        /// <summary>
        /// SXWN9035: Concatenation operator ('||') used with boolean operands
        /// </summary>
        public const string SXWN9035 = "SXWN9035";
        /// <summary>
        /// SXWN9036: Suspicious use of curly braces in xsl:analyze-string/@regex
        /// </summary>
        public const string SXWN9036 = "SXWN9036";
        /// <summary>
        /// SXWN9037: Result of evaluation will always be an empty sequence
        /// </summary>
        public const string SXWN9037 = "SXWN9037";
        /// <summary>
        /// SXWN9038: Field name not defined in record type
        /// </summary>
        public const string SXWN9038 = "SXWN9038";
        /// <summary>
        /// SXWN9039: Value will always be a singleton; occurrence indicator has no effect
        /// </summary>
        public const string SXWN9039 = "SXWN9039";
        /// <summary>
        /// SXWN9040: Possible confusion between language keyword and element name
        /// </summary>
        public const string SXWN9040 = "SXWN9040";
        /// <summary>
        /// SXWN9041: An attribute node cannot have a complex type
        /// </summary>
        public const string SXWN9041 = "SXWN9041";
        /// <summary>
        /// SXWN9042: Unrecognized or invalid extension in Saxon @namespace
        /// </summary>
        public const string SXWN9042 = "SXWN9042";
        /// <summary>
        /// SXWN9043: Invalid or unrecognized serialization property
        /// </summary>
        public const string SXWN9043 = "SXWN9043";
        /// <summary>
        /// SXWN9045: non-streamed input supplied for a streamable stylesheet
        /// </summary>
        public const string SXWN9045 = "SXWN9045";
        /// <summary>
        /// SXWN9046: predicate [0] selects nothing
        /// </summary>
        public const string SXWN9046 = "SXWN9046";
        /// <summary>
        /// SXWN9047: dynamic error evaluating expression used in XSD type alternative
        /// </summary>
        public const string SXWN9047 = "SXWN9047";
        /// <summary>
        /// SXWN9048: An xs:ID element at the outermost level has no effect
        /// </summary>
        public const string SXWN9048 = "SXWN9048";
        /// <summary>
        /// SXWN9049: Requested XQuery version not supported (request ignored)
        /// </summary>
        public const string SXWN9049 = "SXWN9049";
        /// <summary>
        /// SXWN9050: Invalid document excluded from collection
        /// </summary>
        public const string SXWN9050 = "SXWN9050";
        /// <summary>
        /// SXSD1000: unknown attribute group
        /// </summary>
        public const string SXSD1000 = "SXSD1000";
        /// <summary>
        /// SXSD1001: unknown attribute declaration
        /// </summary>
        public const string SXSD1001 = "SXSD1001";
        /// <summary>
        /// SXSD1002: invalid substitution group membership
        /// </summary>
        public const string SXSD1002 = "SXSD1002";
        /// <summary>
        /// SXSD1003: unknown element declaration
        /// </summary>
        public const string SXSD1003 = "SXSD1003";
        /// <summary>
        /// SXSD1004: field declaration may select no nodes, or multiple nodes
        /// </summary>
        public const string SXSD1004 = "SXSD1004";
        /// <summary>
        /// SXSD1005: field declaration selects a node that cannot be atomized
        /// </summary>
        public const string SXSD1005 = "SXSD1005";
        /// <summary>
        /// SXSD1006: unknown named model group
        /// </summary>
        public const string SXSD1006 = "SXSD1006";
        /// <summary>
        /// SXSD1007: missing component in schema
        /// </summary>
        public const string SXSD1007 = "SXSD1007";
        /// <summary>
        /// SXSD1008: unknown schema type
        /// </summary>
        public const string SXSD1008 = "SXSD1008";
        /// <summary>
        /// SXSD1009: constraints in derived type may not be compatible with constraints in the base type
        /// </summary>
        public const string SXSD1009 = "SXSD1009";
        /// <summary>
        /// SXSD1010: enumeration value is not a valid instance of the type
        /// </summary>
        public const string SXSD1010 = "SXSD1010";
        /// <summary>
        /// SXSD1011: type of local element is not derived from type of global element
        /// </summary>
        public const string SXSD1011 = "SXSD1011";
        /// <summary>
        /// SXSD1012: minOccurs/maxOccurs limits adjusted because out of supported range
        /// </summary>
        public const string SXSD1012 = "SXSD1012";
        /// <summary>
        /// SXSD1014: unrecognized schema versioning attribute
        /// </summary>
        public const string SXSD1014 = "SXSD1014";
        /// <summary>
        /// SXSD1015: use="prohibited" on an attribute group has no effect
        /// </summary>
        public const string SXSD1015 = "SXSD1015";
        /// <summary>
        /// SXSD1016: redefined component @is in the wrong schema module
        /// </summary>
        public const string SXSD1016 = "SXSD1016";
        /// <summary>
        /// SXSD1017: target of xs:override is not a valid schema
        /// </summary>
        public const string SXSD1017 = "SXSD1017";
    }
}