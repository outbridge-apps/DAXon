////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Parsing
{
    internal abstract class Token
    {
        /*
     * Token numbers. Those in the range 0 to LAST_OPERATOR are tokens that can be followed
     * by a name or expression; those above this range are tokens that can be
     * followed by an binary @operator.
     */
        public const int IMPLICIT_EOF = -1;
        /// <summary>
        /// Pseudo-token representing the end of the expression
        /// </summary>
        public const int EOF = 0;
        /// <summary>
        /// "union" or "|" token
        /// </summary>
        public const int UNION = 1;
        /// <summary>
        /// Forwards "/"
        /// </summary>
        public const int SLASH = 2;
        /// <summary>
        /// At token, "@"
        /// </summary>
        public const int AT = 3;
        /// <summary>
        /// Left square bracket
        /// </summary>
        public const int LSQB = 4;
        /// <summary>
        /// Left parenthesis
        /// </summary>
        public const int LPAR = 5;
        /// <summary>
        /// Equals token ("=")
        /// </summary>
        public const int EQUALS = 6;
        /// <summary>
        /// Comma token
        /// </summary>
        public const int COMMA = 7;
        /// <summary>
        /// Double forwards slash, "//"
        /// </summary>
        public const int SLASH_SLASH = 8;
        /// <summary>
        /// Operator "or"
        /// </summary>
        public const int OR = 9;
        /// <summary>
        /// Operator "and"
        /// </summary>
        public const int AND = 10;
        /// <summary>
        /// Operator "&gt;"
        /// </summary>
        public const int GT = 11;
        /// <summary>
        /// Operator "&lt;"
        /// </summary>
        public const int LT = 12;
        /// <summary>
        /// Operator "&gt;="
        /// </summary>
        public const int GE = 13;
        /// <summary>
        /// Operator "&lt;="
        /// </summary>
        public const int LE = 14;
        /// <summary>
        /// Operator "+"
        /// </summary>
        public const int PLUS = 15;
        /// <summary>
        /// Binary minus @operator
        /// </summary>
        public const int MINUS = 16;
        /// <summary>
        /// Multiply @operator, "*" when used in an operator context
        /// </summary>
        public const int MULT = 17;
        /// <summary>
        /// Operator "div"
        /// </summary>
        public const int DIV = 18;
        /// <summary>
        /// Operator "mod"
        /// </summary>
        public const int MOD = 19;
        /// <summary>
        /// Operator "is"
        /// </summary>
        public const int IS = 20;
        /// <summary>
        /// "$" symbol
        /// </summary>
        public const int DOLLAR = 21;
        /// <summary>
        /// Operator not-equals. That @is, "!="
        /// </summary>
        public const int NE = 22;
        /// <summary>
        /// Operator "intersect"
        /// </summary>
        public const int INTERSECT = 23;
        /// <summary>
        /// Operator "except"
        /// </summary>
        public const int EXCEPT = 24;
        /// <summary>
        /// Keyword "return"
        /// </summary>
        public const int RETURN = 25;
        /// <summary>
        /// Ketword "then"
        /// </summary>
        public const int THEN = 26;
        /// <summary>
        /// Keyword "else"
        /// </summary>
        public const int ELSE = 27;
        /// <summary>
        /// Keyword "where"
        /// </summary>
        public const int WHERE = 28;
        /// <summary>
        /// Operator "to"
        /// </summary>
        public const int TO = 29;
        /// <summary>
        /// Operator "||"
        /// </summary>
        public const int CONCAT = 30;
        /// <summary>
        /// Keyword "in"
        /// </summary>
        public const int IN = 31;
        /// <summary>
        /// Keyword "some"
        /// </summary>
        public const int SOME = 32;
        /// <summary>
        /// Keyword "every"
        /// </summary>
        public const int EVERY = 33;
        /// <summary>
        /// Keyword "satisfies"
        /// </summary>
        public const int SATISFIES = 34;
        /// <summary>
        /// Token representing the name of a function and the following "(" symbol
        /// </summary>
        public const int FUNCTION = 35;
        /// <summary>
        /// Token representing the name of an axis and the following "::" symbol
        /// </summary>
        public const int AXIS = 36;
        /// <summary>
        /// Keyword "if"
        /// </summary>
        public const int IF = 37;
        /// <summary>
        /// Operator "&lt;&lt;"
        /// </summary>
        public const int PRECEDES = 38;
        /// <summary>
        /// Operator "&gt;&gt;"
        /// </summary>
        public const int FOLLOWS = 39;
        /// <summary>
        /// Operator "!"
        /// </summary>
        public const int BANG = 40;
        /// <summary>
        /// "::" symbol
        /// </summary>
        public const int COLONCOLON = 41;
        /// <summary>
        /// ":*" symbol
        /// </summary>
        public const int COLONSTAR = 42;
        /// <summary>
        /// Token representing a function name and the following "#" symbol
        /// </summary>
        public const int NAMED_FUNCTION_REF = 43;
        /// <summary>
        /// # symbol
        /// </summary>
        public const int HASH = 44;
        /// <summary>
        /// operator "instance of"
        /// </summary>
        public const int INSTANCE_OF = 45;
        /// <summary>
        /// operator "cast as"
        /// </summary>
        public const int CAST_AS = 46;
        /// <summary>
        /// operator "treat as"
        /// </summary>
        public const int TREAT_AS = 47;
        /// <summary>
        /// operator "??"
        /// </summary>
        public const int QMARK_QMARK = 48;
        /*
     * operator "!!"
     */
        public const int BANG_BANG = 49;
        /// <summary>
        /// operator "eq"
        /// </summary>
        public const int FEQ = 50; // "Fortran" style comparison operators eq, ne, etc
        /// <summary>
        /// operator "ne"
        /// </summary>
        public const int FNE = 51;
        /// <summary>
        /// operator "gt"
        /// </summary>
        public const int FGT = 52;
        /// <summary>
        /// operator "lt"
        /// </summary>
        public const int FLT = 53;
        /// <summary>
        /// operator "ge"
        /// </summary>
        public const int FGE = 54;
        /// <summary>
        /// opeartor "le"
        /// </summary>
        public const int FLE = 55;
        /// <summary>
        /// operator "idiv"
        /// </summary>
        public const int IDIV = 56;
        /// <summary>
        /// operator "castable as"
        /// </summary>
        public const int CASTABLE_AS = 57;
        /// <summary>
        /// ":=" symbol (XQuery only)
        /// </summary>
        public const int ASSIGN = 58;
        /// <summary>
        /// "{" symbol (XQuery only)
        /// </summary>
        public const int LCURLY = 59;
        /// <summary>
        /// composite token: &lt;keyword "{"&gt; (XQuery only)
        /// </summary>
        public const int KEYWORD_CURLY = 60;
        /// <summary>
        /// composite token &lt;'element' QNAME&gt; (XQuery only)
        /// </summary>
        public const int ELEMENT_QNAME = 61;
        /// <summary>
        /// composite token &lt;'attribute' QNAME&gt; (XQuery only)
        /// </summary>
        public const int ATTRIBUTE_QNAME = 62;
        /// <summary>
        /// composite token &lt;'pi' QNAME&gt; (XQuery only)
        /// </summary>
        public const int PI_QNAME = 63;
        /// <summary>
        /// composite token &lt;'namespace' QNAME&gt; (XQuery only)
        /// </summary>
        public const int NAMESPACE_QNAME = 64;
        /// <summary>
        /// Keyword "typeswitch"
        /// </summary>
        public const int TYPESWITCH = 65;
        /// <summary>
        /// Keyword "switch" (XQuery 1.1)
        /// </summary>
        public const int SWITCH = 66;
        /// <summary>
        /// Keyword "case"
        /// </summary>
        public const int CASE = 67;
        /// <summary>
        /// Keyword "modify"
        /// </summary>
        public const int MODIFY = 68;
        /// <summary>
        /// `name(` for a reserved function name, e.g. "node(" or "comment(" or "function(" or "union("
        /// </summary>
        public const int KEYWORD_LBRA = 69;
        /// <summary>
        /// "*:" token
        /// </summary>
        public const int SUFFIX = 70; // e.g. *:suffix - the suffix is actually a separate token
        /// <summary>
        /// "as" (in XQuery Update rename expression)
        /// </summary>
        public const int AS = 71;
        /*
     * "group by" (XQuery 3.0)
     */
        public const int GROUP_BY = 72;
        /// <summary>
        /// "for tumbling" (XQuery 3.0)
        /// </summary>
        public const int FOR_TUMBLING = 73;
        /// <summary>
        /// "for sliding" (XQuery 3.0)
        /// </summary>
        public const int FOR_SLIDING = 74;
        /// <summary>
        /// "for member" (XQuery 4.0)
        /// </summary>
        public const int FOR_MEMBER = 75;
        // map colon key-entry separator
        /// <summary>
        /// ":" (XPath 3.0 maps)
        /// </summary>
        public const int COLON = 76;
        /// <summary>
        /// Arrow operator "=&gt;" (XQuery 3.1)
        /// </summary>
        public const int FAT_ARROW = 77;
        /// <summary>
        /// First part of a string template. Token value includes all the text from ``[ up to the first `{
        /// </summary>
        public const int STRING_CONSTRUCTOR_INITIAL = 78;
        /// <summary>
        /// "otherwise" (Saxon extension)
        /// </summary>
        public const int OTHERWISE = 79;
        /// <summary>
        /// "andAlso" (Saxon extension)
        /// </summary>
        public const int AND_ALSO = 80;
        /// <summary>
        /// "orElse" (Saxon extension)
        /// </summary>
        public const int OR_ELSE = 81;
        /// <summary>
        /// Keyword "while"
        /// </summary>
        public const int WHILE = 82;
        /// <summary>
        /// Thin arrow operator "-&gt;" (XQuery 4.0)
        /// </summary>
        public const int THIN_ARROW = 83;
        /// <summary>
        /// Mathematical multiply operator "×"
        /// </summary>
        public const int MATH_MULT = 84;
        /// <summary>
        /// Mathematical divide operator "÷"
        /// </summary>
        public const int MATH_DIVIDE = 85;
        /// <summary>
        /// Arrow operator "=&gt;" (XQuery 3.1)
        /// </summary>
        public const int MAPPING_ARROW = 86;
        // The following tokens are used only in the query prolog. They are categorized
        // as operators on the basis that a following name is treated as a name rather than
        // an @operator.
        /// <summary>
        /// "xquery version"
        /// </summary>
        public const int XQUERY_VERSION = 88;
        /// <summary>
        /// "xquery encoding"
        /// </summary>
        public const int XQUERY_ENCODING = 89;
        /// <summary>
        /// "declare namespace"
        /// </summary>
        public const int DECLARE_NAMESPACE = 90;
        /// <summary>
        /// "declare default"
        /// </summary>
        public const int DECLARE_DEFAULT = 91;
        /// <summary>
        /// "declare fixed"
        /// </summary>
        public const int DECLARE_FIXED = 92;
        /// <summary>
        /// "declare construction"
        /// </summary>
        public const int DECLARE_CONSTRUCTION = 98;
        /// <summary>
        /// "declare base-uri"
        /// </summary>
        public const int DECLARE_BASEURI = 99;
        /// <summary>
        /// "declare boundary-space"
        /// </summary>
        public const int DECLARE_BOUNDARY_SPACE = 101;
        /// <summary>
        /// "declare decimal-format"
        /// </summary>
        public const int DECLARE_DECIMAL_FORMAT = 103;
        /// <summary>
        /// "import schema"
        /// </summary>
        public const int IMPORT_SCHEMA = 105;
        /// <summary>
        /// "import module"
        /// </summary>
        public const int IMPORT_MODULE = 107;
        /// <summary>
        /// "declare variable"
        /// </summary>
        public const int DECLARE_VARIABLE = 108;
        /// <summary>
        /// "declare context"
        /// </summary>
        public const int DECLARE_CONTEXT = 109;
        /// <summary>
        /// "declare function"
        /// </summary>
        public const int DECLARE_FUNCTION = 110;
        /// <summary>
        /// "module namespace"
        /// </summary>
        public const int MODULE_NAMESPACE = 111;
        /// <summary>
        /// Various compound symbols supporting XQuery validation expression
        /// </summary>
        public const int VALIDATE = 112;
        public const int VALIDATE_STRICT = 113;
        public const int VALIDATE_LAX = 114;
        public const int VALIDATE_TYPE = 115;
        /// <summary>
        /// percent sign '%'
        /// </summary>
        public const int PERCENT = 116;
        /// <summary>
        /// "declare xmlspace"
        /// </summary>
        public const int DECLARE_ORDERING = 117;
        /// <summary>
        /// "declare copy-namespaces"
        /// </summary>
        public const int DECLARE_COPY_NAMESPACES = 118;
        /// <summary>
        /// "declare option"
        /// </summary>
        public const int DECLARE_OPTION = 119;
        /// <summary>
        /// "declare revalidation"
        /// </summary>
        public const int DECLARE_REVALIDATION = 124;
        /// <summary>
        /// "insert node/nodes"
        /// </summary>
        public const int INSERT_NODE = 125;
        /// <summary>
        /// "delete node/nodes"
        /// </summary>
        public const int DELETE_NODE = 126;
        /// <summary>
        /// "replace node/nodes"
        /// </summary>
        public const int REPLACE_NODE = 127;
        /// <summary>
        /// "replace value"
        /// </summary>
        public const int REPLACE_VALUE = 128;
        /// <summary>
        /// "rename node"
        /// </summary>
        public const int RENAME_NODE = 130;
        /// <summary>
        /// "first into"
        /// </summary>
        public const int FIRST_INTO = 131;
        /// <summary>
        /// "last into"
        /// </summary>
        public const int LAST_INTO = 132;
        /// <summary>
        /// "after"
        /// </summary>
        public const int AFTER = 133;
        /// <summary>
        /// "before"
        /// </summary>
        public const int BEFORE = 134;
        /// <summary>
        /// "into"
        /// </summary>
        public const int INTO = 135;
        /// <summary>
        /// "with"
        /// </summary>
        public const int WITH = 136;
        /// <summary>
        /// "declare updating [function]"
        /// </summary>
        public const int DECLARE_UPDATING = 138;
        /// <summary>
        /// declare %
        /// </summary>
        public const int DECLARE_ANNOTATED = 140;
        /// <summary>
        /// Saxon extension: declare type
        /// </summary>
        public const int DECLARE_ITEM_TYPE = 144;
        public const int SWITCH_CASE = 145;
        /// <summary>
        /// semicolon separator
        /// </summary>
        public const int SEMICOLON = 149;
        /// <summary>
        /// Constant identifying the token number of the last token to be classified as an @operator
        /// </summary>
        public const int LAST_OPERATOR = 150;
        // Tokens that set "operator" context, so an immediately following "div" is recognized
        // as an @operator, not as an element name
        /// <summary>
        /// Name token (a QName, in general)
        /// </summary>
        public const int NAME = 201;
        /// <summary>
        /// String literal
        /// </summary>
        public const int STRING_LITERAL = 202;
        /// <summary>
        /// Right square bracket
        /// </summary>
        public const int RSQB = 203;
        /// <summary>
        /// Right parenthesis
        /// </summary>
        public const int RPAR = 204;
        /// <summary>
        /// "." symbol
        /// </summary>
        public const int DOT = 205;
        /// <summary>
        /// ".." symbol
        /// </summary>
        public const int DOTDOT = 206;
        /// <summary>
        /// "*" symbol when used as a wildcard
        /// </summary>
        public const int STAR = 207;
        /// <summary>
        /// "prefix:*" token
        /// </summary>
        public const int PREFIX = 208; // e.g. prefix:*
        /// <summary>
        /// Numeric literal
        /// </summary>
        public const int NUMBER = 209;
        /// <summary>
        /// "for" keyword
        /// </summary>
        public const int FOR = 211;
        /// <summary>
        /// Keyword "default"
        /// </summary>
        public const int DEFAULT = 212;
        /// <summary>
        /// Question mark symbol. That @is, "?"
        /// </summary>
        public const int QMARK = 213;
        /// <summary>
        /// "}" symbol (XQuery only)
        /// </summary>
        public const int RCURLY = 215;
        /// <summary>
        /// "let" keyword (XQuery only)
        /// </summary>
        public const int LET = 216;
        public const int TAG = 217;
        public const int PRAGMA = 218;
        /// <summary>
        /// "copy" keyword
        /// </summary>
        public const int COPY = 219;
        /// <summary>
        /// "count" keyword
        /// </summary>
        public const int COUNT = 220;
        /// <summary>
        /// Complete string constructor with no embedded expressions
        /// </summary>
        public const int STRING_LITERAL_BACKTICKED = 222;
        /// <summary>
        /// Backtick (introducing a 4.0 string template)
        /// </summary>
        public const int BACKTICK = 223;
        public const int HEX_INTEGER = 224;
        public const int BINARY_INTEGER = 225;
        /// <summary>
        /// Unary minus sign
        /// </summary>
        public const int NEGATE = 299; // unary minus: not actually a token, but we
        /// <summary>
        /// Pseudo-token representing the start of the expression
        /// </summary>
        public const int UNKNOWN = -1;
        // use token numbers to identify operators.
        /// <summary>
        /// The following strings are used to represent tokens in error messages
        /// </summary>
        public static readonly string[] tokens = new string[300];

        /// <summary>
        /// Lookup table for composite (two-keyword) tokens
        /// </summary>
        public static Dictionary<string, int> doubleKeywords = new Dictionary<string, int>(30);
        static Token()
        {
            InitMapDoubles();
            tokens[EOF] = "<eof>";
            tokens[UNION] = "|";
            tokens[SLASH] = "/";
            tokens[AT] = "@";
            tokens[LSQB] = "[";
            tokens[LPAR] = "(";
            tokens[EQUALS] = "=";
            tokens[COMMA] = ",";
            tokens[SLASH_SLASH] = "//";
            tokens[OR] = "or";
            tokens[AND] = "and";
            tokens[GT] = ">";
            tokens[LT] = "<";
            tokens[GE] = ">=";
            tokens[LE] = "<=";
            tokens[PLUS] = "+";
            tokens[MINUS] = "-";
            tokens[MULT] = "*";
            tokens[MATH_MULT] = "×";
            tokens[DIV] = "div";
            tokens[MATH_DIVIDE] = "÷";
            tokens[MOD] = "mod";
            tokens[IS] = "is";
            tokens[DOLLAR] = "$";
            tokens[NE] = "!=";
            tokens[BANG] = "!";
            tokens[CONCAT] = "||";
            tokens[INTERSECT] = "intersect";
            tokens[EXCEPT] = "except";
            tokens[RETURN] = "return";
            tokens[THEN] = "then";
            tokens[ELSE] = "else";
            tokens[TO] = "to";
            tokens[IN] = "in";
            tokens[SOME] = "some";
            tokens[EVERY] = "every";
            tokens[SATISFIES] = "satisfies";
            tokens[FUNCTION] = "<function>(";
            tokens[AXIS] = "<axis>";
            tokens[IF] = "if(";
            tokens[PRECEDES] = "<<";
            tokens[FOLLOWS] = ">>";
            tokens[COLONCOLON] = "::";
            tokens[COLONSTAR] = ":*";
            tokens[HASH] = "#";
            tokens[INSTANCE_OF] = "instance of";
            tokens[CAST_AS] = "cast as";
            tokens[TREAT_AS] = "treat as";
            tokens[QMARK_QMARK] = "??";
            tokens[BANG_BANG] = "!!";
            tokens[FEQ] = "eq";
            tokens[FNE] = "ne";
            tokens[FGT] = "gt";
            tokens[FGE] = "ge";
            tokens[FLT] = "lt";
            tokens[FLE] = "le";
            tokens[IDIV] = "idiv";
            tokens[CASTABLE_AS] = "castable as";
            tokens[ASSIGN] = ":=";
            tokens[SWITCH] = "switch";
            tokens[TYPESWITCH] = "typeswitch";
            tokens[CASE] = "case";
            tokens[DEFAULT] = "default";

            //tokens [ AS_FIRST ] = "as first";
            //tokens [ AS_LAST ] = "as last";
            tokens[AFTER] = "after";
            tokens[BEFORE] = "before";
            tokens[INTO] = "into";
            tokens[WITH] = "with";
            tokens[MODIFY] = "modify";
            tokens[AS] = "as";
            tokens[COLON] = ":";
            tokens[FAT_ARROW] = "=>";
            tokens[MAPPING_ARROW] = "=!>";
            tokens[THIN_ARROW] = "->";
            tokens[AND_ALSO] = "andAlso";
            tokens[OR_ELSE] = "orElse";
            tokens[STRING_CONSTRUCTOR_INITIAL] = "``[<string>`{";
            tokens[STRING_LITERAL_BACKTICKED] = "``[<string>]``";
            tokens[BACKTICK] = "`";
            tokens[OTHERWISE] = "otherwise";
            tokens[NAME] = "<name>";
            tokens[STRING_LITERAL] = "<string-literal>";
            tokens[RSQB] = "]";
            tokens[RPAR] = ")";
            tokens[DOT] = ".";
            tokens[DOTDOT] = "..";
            tokens[STAR] = "*";
            tokens[PREFIX] = "<prefix:*>";
            tokens[NUMBER] = "<numeric-literal>";
            tokens[HEX_INTEGER] = "<hex-integer>";
            tokens[BINARY_INTEGER] = "<binary-integer>";
            tokens[KEYWORD_LBRA] = "<node-type>()";
            tokens[FOR] = "for";
            tokens[SUFFIX] = "<*:local-name>";
            tokens[QMARK] = "?";
            tokens[LCURLY] = "{";
            tokens[KEYWORD_CURLY] = "<keyword> {";
            tokens[RCURLY] = "}";
            tokens[LET] = "let";
            tokens[WHERE] = "where";
            tokens[WHILE] = "while";
            tokens[VALIDATE] = "validate {";
            tokens[TAG] = "<element>";
            tokens[PRAGMA] = "(# ... #)";
            tokens[SEMICOLON] = ";";
            tokens[COPY] = "copy";
            tokens[NEGATE] = "-";
            tokens[PERCENT] = "%";
        }

        private static void InitMapDoubles()
        {
            MapDouble("instance of", INSTANCE_OF);
            MapDouble("cast as", CAST_AS);
            MapDouble("treat as", TREAT_AS);
            MapDouble("castable as", CASTABLE_AS);
            MapDouble("group by", GROUP_BY);
            MapDouble("for tumbling", FOR_TUMBLING);
            MapDouble("for sliding", FOR_SLIDING);
            MapDouble("for member", FOR_MEMBER);
            MapDouble("xquery version", XQUERY_VERSION);
            MapDouble("xquery encoding", XQUERY_ENCODING);
            MapDouble("declare namespace", DECLARE_NAMESPACE);
            MapDouble("declare default", DECLARE_DEFAULT);
            MapDouble("declare construction", DECLARE_CONSTRUCTION);
            MapDouble("declare base-uri", DECLARE_BASEURI);
            MapDouble("declare boundary-space", DECLARE_BOUNDARY_SPACE);
            MapDouble("declare decimal-format", DECLARE_DECIMAL_FORMAT);
            MapDouble("declare fixed", DECLARE_FIXED);
            MapDouble("declare ordering", DECLARE_ORDERING);
            MapDouble("declare copy-namespaces", DECLARE_COPY_NAMESPACES);
            MapDouble("declare option", DECLARE_OPTION);
            MapDouble("declare revalidation", DECLARE_REVALIDATION);
            MapDouble("declare item-type", DECLARE_ITEM_TYPE);
            MapDouble("import schema", IMPORT_SCHEMA);
            MapDouble("import module", IMPORT_MODULE);
            MapDouble("declare variable", DECLARE_VARIABLE);
            MapDouble("declare context", DECLARE_CONTEXT);
            MapDouble("declare function", DECLARE_FUNCTION);
            MapDouble("declare updating", DECLARE_UPDATING);
            MapDouble("module namespace", MODULE_NAMESPACE);
            MapDouble("validate strict", VALIDATE_STRICT);
            MapDouble("validate lax", VALIDATE_LAX);
            MapDouble("validate type", VALIDATE_TYPE);
            MapDouble("insert node", INSERT_NODE);
            MapDouble("insert nodes", INSERT_NODE);
            MapDouble("delete node", DELETE_NODE);
            MapDouble("delete nodes", DELETE_NODE);
            MapDouble("replace node", REPLACE_NODE);
            MapDouble("replace value", REPLACE_VALUE);
            MapDouble("rename node", RENAME_NODE);
            MapDouble("rename nodes", RENAME_NODE);
            MapDouble("first into", FIRST_INTO);
            MapDouble("last into", LAST_INTO);
            MapDouble("switch case", SWITCH_CASE);
        }

        private static void MapDouble(string doubleKeyword, int token)
        {
            doubleKeywords[doubleKeyword] = token;
            tokens[token] = doubleKeyword;
        }

        public static int Inverse(int @operator)
        {
            switch (@operator)
            {
                case LT:
                    return GT;
                case LE:
                    return GE;
                case GT:
                    return LT;
                case GE:
                    return LE;
                case FLT:
                    return FGT;
                case FLE:
                    return FGE;
                case FGT:
                    return FLT;
                case FGE:
                    return FLE;
                default:
                    return @operator;
            }
        }

        public static int Negate(int @operator)
        {
            switch (@operator)
            {
                case FEQ:
                    return FNE;
                case FNE:
                    return FEQ;
                case FLT:
                    return FGE;
                case FLE:
                    return FGT;
                case FGT:
                    return FLE;
                case FGE:
                    return FLT;
                default:
                    throw new ArgumentException("Invalid operator for negate()");
            }
        }

        public static bool IsOrderedOperator(int @operator)
        {
            return @operator != FEQ && @operator != FNE;
        }
    }
}