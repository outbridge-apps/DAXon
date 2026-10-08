////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.XQuery;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Parsing
{
    // XPathParser part: SequenceType/ItemType grammar — occurrence indicators, plain/simple types,
    // record/union/enum/function/map/array tests.
    public partial class XPathParser
    {
        private Types.ItemType GetPlainType(string qname)
        {
            if (scanOnly)
            {
                return BuiltInAtomicType.STRING;
            }

            StructuredQName sq;
            try
            {
                sq = qNameParser.Parse(qname, env.GetDefaultElementNamespace());
            }
            catch (XPathException e)
            {
                Grumble(e.Message, e.ErrorCodeQName);
                return null;
            }

            return GetPlainType(sq);
        }

        public virtual Types.ItemType GetPlainType(StructuredQName sq)
        {
            Configuration config = env.GetConfiguration();
            NamespaceUri uri = sq.GetNamespaceUri();
            if (uri.IsEmpty())
            {
                uri = env.GetDefaultElementNamespace();
            }

            string local = sq.GetLocalPart();
            string qname = sq.DisplayName;
            bool builtInNamespace = uri.Equals(NamespaceUri.SCHEMA);
            if (builtInNamespace)
            {
                Types.ItemType t = Types.Type.GetBuiltInItemType(uri, local);
                if (t == null && local == "numeric")
                {
                    // xs:numeric is the built-in union double|float|decimal. NumericType registers itself in
                    // BuiltInType only when GetInstance() is first called (a deliberate dodge of a static-init
                    // cycle through xs:double/float/decimal), and that trigger is otherwise reached only via a
                    // function that declares an xs:numeric argument. An `instance of xs:numeric` (or a bare
                    // SequenceType) can be the first reference, so force the lazy registration here and retry.
                    NumericType.GetInstance();
                    t = Types.Type.GetBuiltInItemType(uri, local);
                }

                if (t == null)
                {
                    Grumble("Unknown atomic type " + qname, "XPST0051");
                }

                if (t is BuiltInAtomicType)
                {
                    CheckAllowedType(env, (BuiltInAtomicType)t);
                    return t;
                }
                else if (t.IsPlainType())
                {
                    return t;
                }
                else
                {
                    Grumble("The type " + qname + " is not atomic", "XPST0051");
                }
            }
            else if (uri.Equals(NamespaceUri.JAVA_TYPE))
            {
                System.Type theClass;
                try
                {
                    string className = JavaExternalObjectType.LocalNameToClassName(local);
                    theClass = config.GetType(className, false);
                }
                catch (XPathException)
                {
                    Grumble("Unknown Java class " + local, "XPST0051");
                    return AnyItemType.GetInstance();
                }


                lock (config.syncLock)
                {
                    return JavaExternalObjectType.Of(theClass);
                }
            }
            else if (uri.Equals(NamespaceUri.DOT_NET_TYPE))
            {
                return Core.Version.platform.GetExternalObjectType(config, uri, local);
            }
            else
            {
                if (allowXPath40Syntax)
                {
                    Types.ItemType it = env.ResolveTypeAlias(sq);
                    if (it != null)
                    {
                        return it;
                    }
                }

                ISchemaType st = config.GetSchemaType(sq);
                if (st == null)
                {
                    Grumble("Unknown simple type " + qname, "XPST0051");
                }
                else if (st.IsAtomicType())
                {
                    if (!env.IsImportedSchema(uri))
                    {
                        Grumble("Atomic type " + qname + " exists, but its schema definition has not been imported", "XPST0051");
                    }

                    return (IAtomicType)st;
                }
                else if (st is Types.ItemType && ((Types.ItemType)st).IsPlainType() && allowXPath30Syntax)
                {
                    if (!env.IsImportedSchema(uri))
                    {
                        Grumble("Type " + qname + " exists, but its schema definition has not been imported", "XPST0051");
                    }

                    return (Types.ItemType)st;
                }
                else if (st.IsComplexType())
                {
                    Grumble("Type (" + qname + ") is a complex type", "XPST0051");
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
                else if (((ISimpleType)st).IsListType())
                {
                    Grumble("Type (" + qname + ") is a list type", "XPST0051");
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
                else if (allowXPath30Syntax)
                {
                    Grumble("Type (" + qname + ") is a union type that cannot be used as an item type", "XPST0051");
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
                else
                {
                    Grumble("The union type (" + qname + ") cannot be used as an item type unless XPath 3.0 is enabled", "XPST0051");
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
            }

            Grumble("Unknown atomic type " + qname, "XPST0051");
            return BuiltInAtomicType.ANY_ATOMIC;
        }

        private void CheckAllowedType(IStaticContext env, BuiltInAtomicType type)
        {
            string s = WhyDisallowedType(env.GetPackageData(), type);
            if (s != null)
            {
                Grumble(s, "XPST0080");
            }
        }

        public static string WhyDisallowedType(PackageData pack, BuiltInAtomicType type)
        {
            if (!type.IsAllowedInXSD10() && pack.GetConfiguration().XsdVersion == Configuration.XSD10)
            {
                return "The built-in atomic type " + type.DisplayName + " is not recognized unless XSD 1.1 is enabled";
            }

            return null;
        }

        private ICastingTarget GetSimpleType(string qname)
        {
            if (scanOnly)
            {
                return BuiltInAtomicType.STRING;
            }

            StructuredQName sq = null;
            try
            {
                sq = qNameParser.Parse(qname, env.GetDefaultElementNamespace());
            }
            catch (XPathException e)
            {
                Grumble(e.Message, e.ErrorCodeQName);
            }

            NamespaceUri uri = sq.GetNamespaceUri();
            string local = sq.GetLocalPart();
            bool builtInNamespace = uri.Equals(NamespaceUri.SCHEMA);
            if (builtInNamespace)
            {
                ISimpleType target = (ISimpleType)Types.Type.GetBuiltInSimpleType(uri, local);
                if (target == null)
                {
                    Grumble("Unknown simple type " + qname, allowXPath30Syntax ? "XQST0052" : "XPST0051");
                }
                else if (!(target is ICastingTarget))
                {
                    Grumble("Unsuitable type for cast: " + target.Description, "XPST0080");
                }

                ICastingTarget t = (ICastingTarget)target;
                if (t is BuiltInAtomicType)
                {
                    CheckAllowedType(env, (BuiltInAtomicType)t);
                }

                return t;
            }
            else if (uri.Equals(NamespaceUri.DOT_NET_TYPE))
            {
                return (IAtomicType)Core.Version.platform.GetExternalObjectType(env.GetConfiguration(), uri, local);
            }
            else
            {
                ISchemaType st = env.GetConfiguration().GetSchemaType(new StructuredQName("", uri, local));
                if (st == null)
                {
                    if (allowXPath30Syntax)
                    {
                        Grumble("Unknown simple type " + qname, "XQST0052");
                    }
                    else
                    {
                        Grumble("Unknown simple type " + qname, "XPST0051");
                    }

                    return BuiltInAtomicType.ANY_ATOMIC;
                }

                if (allowXPath30Syntax)
                {

                    // XPath 3.0
                    if (!env.IsImportedSchema(uri))
                    {
                        Grumble("Simple type " + qname + " exists, but its target namespace has not been imported in the static context");
                    }

                    return (ICastingTarget)st;
                }
                else
                {

                    // XPath 2.0
                    if (st.IsAtomicType())
                    {
                        if (!env.IsImportedSchema(uri))
                        {
                            Grumble("Atomic type " + qname + " exists, but its target namespace has not been imported in the static context");
                        }

                        return (IAtomicType)st;
                    }
                    else if (st.IsComplexType())
                    {
                        Grumble("Cannot cast to a complex type (" + qname + ")", "XPST0051");
                        return BuiltInAtomicType.ANY_ATOMIC;
                    }
                    else if (((ISimpleType)st).IsListType())
                    {
                        Grumble("Casting to a list type (" + qname + ") requires XPath 3.0", "XPST0051");
                        return BuiltInAtomicType.ANY_ATOMIC;
                    }
                    else
                    {
                        Grumble("casting to a union type (" + qname + ") requires XPath 3.0", "XPST0051");
                        return BuiltInAtomicType.ANY_ATOMIC;
                    }
                }
            }
        }

        public virtual Values.SequenceType ParseSequenceType()
        {
            bool disallowIndicator = t.currentTokenValue.Equals("empty-sequence");
            Types.ItemType primaryType = ParseItemType();
            if (disallowIndicator)
            {

                // No occurrence indicator allowed
                return Values.SequenceType.MakeSequenceType(primaryType, StaticProperty.EMPTY);
            }

            int occurrenceFlag = ParseOccurrenceIndicator();
            return Values.SequenceType.MakeSequenceType(primaryType, occurrenceFlag);
        }

        public virtual int ParseOccurrenceIndicator()
        {
            int occurrenceFlag;
            switch (t.currentToken)
            {
                case Token.STAR:
                case Token.MULT:

                    // "*" will be tokenized different ways depending on what precedes it
                    occurrenceFlag = StaticProperty.ALLOWS_ZERO_OR_MORE;

                    // Make the tokenizer ignore the occurrence indicator when classifying the next token
                    t.currentToken = Token.RPAR;
                    NextToken();
                    break;
                case Token.PLUS:
                    occurrenceFlag = StaticProperty.ALLOWS_ONE_OR_MORE;

                    // Make the tokenizer ignore the occurrence indicator when classifying the next token
                    t.currentToken = Token.RPAR;
                    NextToken();
                    break;
                case Token.QMARK:
                    occurrenceFlag = StaticProperty.ALLOWS_ZERO_OR_ONE;

                    // Make the tokenizer ignore the occurrence indicator when classifying the next token
                    t.currentToken = Token.RPAR;
                    NextToken();
                    break;
                default:
                    occurrenceFlag = StaticProperty.EXACTLY_ONE;
                    break;
            }

            return occurrenceFlag;
        }

        public virtual Types.ItemType ParseItemType()
        {
            // Same .NET stack-overflow guard as ParseExprSingle: the item-type grammar is a second
            // recursive descent (parenthesized/function/array/map types nest through here), so a
            // pathologically deep type like `((((item()))))` would otherwise crash the process.
            if (++expressionDepth > MAX_EXPRESSION_NESTING)
            {
                expressionDepth--;
                Grumble("Item type is too deeply nested (exceeds the limit of " + MAX_EXPRESSION_NESTING + ")", "XPST0003");
            }

            // As in ParseExprSingle: the counter is the Java-parity ceiling, but a level of this
            // grammar costs more stack in some productions than in others (a nested `function() as`
            // return type costs several times a nested `map(...)` value type), so 3000 levels do
            // not fit a 1 MB thread on every shape. The adaptive probe raises the same XPST0003
            // first on whichever shape runs out of stack before it runs out of counter.
            try
            {
                StackGuard.Probe();
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                expressionDepth--;
                throw e.Describe("Item type is too deeply nested (insufficient stack on this thread)", "XPST0003", null);
            }

            try
            {
                Types.ItemType extended = parserExtension.ParseExtendedItemType(this);
                return extended == null ? ParseSimpleItemType() : extended;
            }
            finally
            {
                expressionDepth--;
            }
        }

        private Types.ItemType ParseSimpleItemType()
        {
            Types.ItemType primaryType;
            if (t.currentToken == Token.LPAR)
            {
                primaryType = ParseParenthesizedItemType(); //nextToken();
            }
            else if (t.currentToken == Token.NAME)
            {
                primaryType = GetPlainType(t.currentTokenValue);
                NextToken();
            }
            else if (t.currentToken == Token.KEYWORD_LBRA || t.currentToken == Token.FUNCTION)
            {

                // Which includes things such as "map" and "array"
                switch (t.currentTokenValue)
                {
                    case "item":
                        NextToken();
                        Expect(Token.RPAR);
                        NextToken();
                        primaryType = AnyItemType.GetInstance();
                        break;
                    case "function":
                        {
                            CheckLanguageVersion30();
                            AnnotationList annotations = AnnotationList.EMPTY;
                            primaryType = ParseFunctionItemType(annotations);
                            break;
                        }

                    case "fn":
                        {
                            CheckLanguageVersion40();
                            AnnotationList annotations = AnnotationList.EMPTY;
                            primaryType = ParseFunctionItemType(annotations);
                            break;
                        }

                    case "map":
                        primaryType = ParseMapItemType();
                        break;
                    case "array":
                        primaryType = ParseArrayItemType();
                        break;
                    case "record":
                    case "tuple":
                        primaryType = ParseRecordTest(this);
                        break;
                    case "atomic":

                        // Allowed only in patterns, not in item types??
                        // TODO: not in spec, drop this
                        CheckLanguageVersion40();
                        Warning("The pattern syntax atomic(typename) is likely to be dropped from the 4.0 specification. Use type(typename) instead.", DAXonErrorCode.SXWN9000);
                        NextToken();
                        Expect(Token.NAME);
                        StructuredQName typeName = GetQNameParser().Parse(t.currentTokenValue, NamespaceUri.SCHEMA);
                        primaryType = GetPlainType(typeName);
                        if (!(primaryType is IAtomicType))
                        {
                            Grumble("Type " + t.currentTokenValue + " exists, but is not atomic");
                        }

                        NextToken();
                        Expect(Token.RPAR);
                        NextToken();
                        break;
                    case "union":
                        primaryType = ParseUnionType();
                        break;
                    case "enum":
                        primaryType = ParseEnumType();
                        break;
                    case "empty-sequence":
                        NextToken();
                        Expect(Token.RPAR);
                        NextToken();
                        primaryType = ErrorType.GetInstance();
                        break;
                    case "type":
                        CheckLanguageVersion40();
                        NextToken();
                        if (t.currentToken == Token.NAME)
                        {
                            StructuredQName qName = GetQNameParser().Parse(t.currentTokenValue, NamespaceUri.NULL);
                            Types.ItemType realType = GetStaticContext().ResolveTypeAlias(qName);
                            if (realType != null)
                            {
                                NextToken();
                                Expect(Token.RPAR);
                                NextToken();
                                return realType;
                            }
                        }

                        if (language != ParsedLanguage.XSLT_PATTERN)
                        {
                            Grumble("In an XPath expression (as distinct from an XSLT pattern), type(N) must refer to a named item type");
                        }

                        Types.ItemType it = ParseItemType();
                        Expect(Token.RPAR);
                        NextToken();
                        return it;
                    default:
                        primaryType = ParseKindTest();
                        break;
                }
            }
            else if (t.currentToken == Token.PERCENT)
            {
                AnnotationList annotations = ParseAnnotationsList();
                if (t.currentTokenValue.Equals("function"))
                {
                    primaryType = ParseFunctionItemType(annotations);
                }
                else
                {
                    Grumble("Expected 'function' to follow annotation assertions, found " + Token.tokens[t.currentToken]);
                    return null;
                }
            }
            else if (language == ParsedLanguage.EXTENDED_ITEM_TYPE && t.currentToken == Token.PREFIX)
            {
                string tokv = t.currentTokenValue;
                NextToken();
                return MakeNamespaceTest(Types.Type.ELEMENT, tokv);
            }
            else if (language == ParsedLanguage.EXTENDED_ITEM_TYPE && t.currentToken == Token.SUFFIX)
            {
                NextToken();
                Expect(Token.NAME);
                string tokv = t.currentTokenValue;
                NextToken();
                return MakeLocalNameTest(Types.Type.ELEMENT, tokv);
            }
            else if (language == ParsedLanguage.EXTENDED_ITEM_TYPE && t.currentToken == Token.AT)
            {
                NextToken();
                if (t.currentToken == Token.PREFIX)
                {
                    string tokv = t.currentTokenValue;
                    NextToken();
                    return MakeNamespaceTest(Types.Type.ATTRIBUTE, tokv);
                }
                else if (t.currentToken == Token.SUFFIX)
                {
                    NextToken();
                    Expect(Token.NAME);
                    string tokv = t.currentTokenValue;
                    NextToken();
                    return MakeLocalNameTest(Types.Type.ATTRIBUTE, tokv);
                }
                else
                {
                    Grumble("Expected NodeTest after '@'");
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
            }
            else
            {
                Grumble("Expected type name in SequenceType, found " + Token.tokens[t.currentToken]);
                return BuiltInAtomicType.ANY_ATOMIC;
            }

            return primaryType;
        }

        private Types.ItemType ParseRecordTest(XPathParser p)
        {

            // The initial "record(" has been read
            CheckLanguageVersion40();
            Tokenizer t = p.GetTokenizer();
            p.NextToken();
            IList<string> fieldNames = new List<string>(6);
            IList<string> optionalFieldNames = new List<string>(6);
            IList<Values.SequenceType> fieldTypes = new List<Values.SequenceType>(6);
            bool extensible = false;
            RecordTest recordTest = new RecordTest();
            while (true)
            {
                string name;
                if (t.currentToken == Token.STAR || t.currentToken == Token.MULT)
                {
                    extensible = true;
                    p.NextToken();
                    p.Expect(Token.RPAR);
                    break;
                }

                if (t.currentToken == Token.NAME)
                {
                    name = t.currentTokenValue;
                    if (!NameChecker.IsValidNCName(name))
                    {
                        p.Grumble(Err.Wrap(name) + " is not a valid NCName");
                    }
                }
                else if (t.currentToken == Token.STRING_LITERAL)
                {
                    name = t.currentTokenValue;
                }
                else
                {
                    p.Grumble("Name of field in tuple must be either an NCName or a quoted string literal");
                    name = "dummy";
                }

                if (fieldNames.Contains(name))
                {
                    p.Grumble("Duplicate field name (" + name + ")");
                    name = "dummy";
                }

                fieldNames.Add(name);
                p.NextToken();
                if (t.currentToken == Token.QMARK)
                {
                    optionalFieldNames.Add(name);
                    p.NextToken();
                }

                Values.SequenceType arg = Values.SequenceType.ANY_SEQUENCE;
                if (t.currentToken == Token.AS)
                {
                    p.NextToken();
                    if (t.currentToken == Token.DOTDOT)
                    {

                        // self-reference
                        p.NextToken();
                        int occ = ParseOccurrenceIndicator();
                        arg = Values.SequenceType.MakeSequenceType((Types.ItemType)(new SelfReferenceRecordTest(recordTest)), occ);
                        if (!Cardinality.AllowsZero(occ) && !optionalFieldNames.Contains(name))
                        {
                            throw new XPathException("A self-referencing field in a record type must be emptiable or optional", "XPST0140");
                        }
                    }
                    else
                    {
                        arg = p.ParseSequenceType();
                    }
                }

                fieldTypes.Add(arg);
                if (t.currentToken == Token.RPAR)
                {
                    break;
                }
                else if (t.currentToken == Token.COMMA)
                {
                    p.NextToken();
                }
                else
                {
                    p.Grumble("Expected ',' or ')' after field in RecordTest, found '" + Token.tokens[t.currentToken] + '\'');
                }
            }

            p.NextToken();
            recordTest.SetDetails(fieldNames, fieldTypes, optionalFieldNames, extensible);
            return recordTest;
        }

        public virtual Types.ItemType ParseUnionType()
        {

            // The initial "union(" has been read
            CheckLanguageVersion40();
            NextToken();
            IList<IAtomicType> memberTypes = new List<IAtomicType>(6);
            while (true)
            {
                if (t.currentToken == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("enum"))
                {
                    EnumerationType type = ParseEnumType();
                    memberTypes.Add(type);
                }
                else
                {
                    Expect(Token.NAME);
                    if (scanOnly)
                    {
                        memberTypes.Add(BuiltInAtomicType.STRING);
                    }
                    else
                    {
                        StructuredQName member = GetQNameParser().Parse(t.currentTokenValue, GetStaticContext().GetDefaultElementNamespace());
                        Types.ItemType type = GetPlainType(member);
                        if (type is IAtomicType)
                        {
                            memberTypes.Add((IAtomicType)type);
                        }
                        else if (type is IPlainType)
                        {
                            foreach (IPlainType pt in ((IUnionType)type).PlainMemberTypes)
                            {
                                if (pt is IAtomicType)
                                {
                                    memberTypes.Add((IAtomicType)pt);
                                }
                                else
                                {
                                    Grumble("Union type " + type + " has a non-atomic member type " + pt);
                                }
                            }
                        }
                        else
                        {
                            Grumble("Type " + t.currentTokenValue + " exists, but is not atomic");
                        }
                    }

                    NextToken();
                }

                if (t.currentToken == Token.RPAR)
                {
                    break;
                }
                else if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else
                {
                    Grumble("Expected ',' or ')' after member name in union type, found '" + Token.tokens[t.currentToken] + '\'');
                }
            }

            NextToken();
            return new LocalUnionType(memberTypes);
        }

        public virtual EnumerationType ParseEnumType()
        {

            // The initial "enum(" has been read
            CheckLanguageVersion40();
            NextToken();
            HashSet<string> values = new HashSet<string>();
            while (true)
            {
                Expect(Token.STRING_LITERAL);
                values.Add(t.currentTokenValue);
                NextToken();
                if (t.currentToken == Token.RPAR)
                {
                    break;
                }
                else if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else
                {
                    Grumble("Expected ',' or ')' after string literal in enum type, found '" + Token.tokens[t.currentToken] + '\'');
                }
            }

            NextToken();
            return new EnumerationType(values);
        }

        protected virtual Types.ItemType ParseFunctionItemType(AnnotationList annotations)
        {
            NextToken();
            IList<Values.SequenceType> argTypes = new List<Values.SequenceType>(3);
            Values.SequenceType resultType;
            if (t.currentToken == Token.STAR || t.currentToken == Token.MULT)
            {

                // Allow both to be safe
                NextToken();
                Expect(Token.RPAR);
                NextToken();
                if (annotations.IsEmpty())
                {
                    return AnyFunctionType.GetInstance();
                }
                else
                {
                    return (Types.ItemType)new AnyFunctionTypeWithAssertions(annotations, GetStaticContext().GetConfiguration());
                }
            }
            else
            {
                while (t.currentToken != Token.RPAR)
                {
                    Values.SequenceType arg = ParseSequenceType();
                    argTypes.Add(arg);
                    if (t.currentToken == Token.RPAR)
                    {
                        break;
                    }
                    else if (t.currentToken == Token.COMMA)
                    {
                        NextToken();
                    }
                    else
                    {
                        Grumble("Expected ',' or ')' after function argument type, found '" + Token.tokens[t.currentToken] + '\'');
                    }
                }

                NextToken();
                if (t.currentToken == Token.AS)
                {
                    NextToken();
                    resultType = ParseSequenceType();
                    Values.SequenceType[] argArray = new Values.SequenceType[argTypes.Count];
                    argArray = argTypes.ToArray();
                    return new SpecificFunctionType(argArray, resultType, annotations);
                }
                else if (argTypes.Count > 0)
                {
                    Grumble("Result type must be given if an argument type is given: expected 'as (type)'");
                    return null;
                }
                else
                {
                    Grumble("function() is no longer allowed for a general function type: must be function(*)");
                    return null; // in the new syntax adopted on 2009-09-22, this case is an error
                }
            }
        }

        protected virtual Types.ItemType ParseMapItemType()
        {
            CheckMapExtensions();
            Tokenizer t = GetTokenizer();
            NextToken();
            if (t.currentToken == Token.STAR || t.currentToken == Token.MULT)
            {

                // Allow both to be safe
                NextToken();
                Expect(Token.RPAR);
                NextToken();
                return MapType.ANY_MAP_TYPE;
            }
            else
            {
                Types.ItemType keyType = ParseItemType();
                Expect(Token.COMMA);
                NextToken();
                Values.SequenceType valueType = ParseSequenceType();
                Expect(Token.RPAR);
                NextToken();
                if (!(keyType is IPlainType))
                {
                    Grumble("Key type of a map must be an atomic or pure union type: found " + keyType);
                    return null;
                }

                return new MapType((IPlainType)keyType, valueType);
            }
        }

        protected virtual Types.ItemType ParseArrayItemType()
        {
            CheckLanguageVersion31();
            Tokenizer t = GetTokenizer();
            NextToken();
            if (t.currentToken == Token.STAR || t.currentToken == Token.MULT)
            {

                // Allow both to be safe
                NextToken();
                Expect(Token.RPAR);
                NextToken();
                return ArrayItemType.ANY_ARRAY_TYPE;
            }
            else
            {
                Values.SequenceType memberType = ParseSequenceType();
                Expect(Token.RPAR);
                NextToken();
                return new ArrayItemType(memberType);
            }
        }

        private Types.ItemType ParseParenthesizedItemType()
        {
            if (!allowXPath30Syntax)
            {
                Grumble("Parenthesized item types require 3.0 to be enabled");
            }

            NextToken();
            Types.ItemType primaryType = ParseItemType();
            while (primaryType is NodeTest && language == ParsedLanguage.EXTENDED_ITEM_TYPE && t.currentToken != Token.RPAR)
            {
                switch (t.currentToken)
                {
                    case Token.UNION:
                    case Token.EXCEPT:
                    case Token.INTERSECT:
                        int op = t.currentToken;
                        NextToken();
                        primaryType = new CombinedNodeTest((NodeTest)primaryType, op, (NodeTest)ParseItemType());
                        break;
                }
            }

            Expect(Token.RPAR);
            NextToken();
            return primaryType;
        }

    }
}
