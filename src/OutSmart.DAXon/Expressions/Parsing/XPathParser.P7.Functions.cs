////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Trees.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Expressions.Parsing
{
    // XPathParser part: map/array constructors, static and dynamic function calls, currying, named
    // function references, annotations, inline functions, missing-function diagnostics.
    public partial class XPathParser
    {
        protected virtual Expression ParseMapExpression()
        {
            CheckMapExtensions();

            // have read the "map {"
            Tokenizer t = GetTokenizer();
            int offset = t.currentTokenStartOffset;
            IList<Expression> entries = new List<Expression>();
            // Parallel record of the raw key/value expressions: if every key turns out to be a
            // distinct xs:string literal we compile a FixedKeyMapConstructor instead of the
            // map:merge(map:entry...) chain (see the default case below).
            IList<Expression> valueExprs = new List<Expression>();
            List<string> literalKeys = new List<string>();
            bool allStringLiteralKeys = true;
            var seenKeys = new HashSet<string>();
            NextToken();
            if (t.currentToken != Token.RCURLY)
            {
                while (true)
                {
                    Expression key = ParseExprSingle();
                    if (t.currentToken == Token.ASSIGN)
                    {
                        Grumble("The ':=' notation is no longer accepted in map expressions: use ':' instead");
                    }

                    Expect(Token.COLON);
                    NextToken();
                    Expression value = ParseExprSingle();
                    valueExprs.Add(value);
                    if (allStringLiteralKeys
                        && key is Literal keyLit && keyLit.GroundedValue is StringValue keySv
                        && !keySv.IsUntypedAtomic() && seenKeys.Add(keySv.GetStringValue()))
                    {
                        literalKeys.Add(keySv.GetStringValue());
                    }
                    else
                    {
                        allStringLiteralKeys = false;
                    }

                    Expression entry;
                    if (key is Literal && ((Literal)key).GroundedValue is AtomicValue && value is Literal)
                    {
                        entry = Literal.MakeLiteral(new SingleEntryMap((AtomicValue)((Literal)key).GroundedValue, ((Literal)value).GroundedValue));
                    }
                    else
                    {
                        entry = MapFunctionSet.GetInstance(31).MakeFunction("entry", 2).MakeFunctionCall(key, value);
                    }

                    entries.Add(entry);
                    if (t.currentToken == Token.RCURLY)
                    {
                        break;
                    }
                    else
                    {
                        Expect(Token.COMMA);
                        NextToken();
                    }
                }
            }

            t.LookAhead(); //manual lookahead after an RCURLY
            NextToken();
            Expression result;
            switch (entries.Count)
            {
                case 0:
                    result = Literal.MakeLiteral(new HashTrieMap());
                    break;
                case 1:
                    result = entries[0];
                    break;
                default:
                    if (allStringLiteralKeys && literalKeys.Count == entries.Count)
                    {
                        // Every key a distinct xs:string literal: skip merge/entry, build the map
                        // directly through a shared key layout. Source key order is preserved.
                        result = new FixedKeyMapConstructor(literalKeys.ToArray(), valueExprs);
                        break;
                    }

                    Expression[] entriesArray = new Expression[entries.Count];
                    Block block = new Block(entries.ToArray());
                    HashTrieMap options = new HashTrieMap();
                    options.InitialPut(new StringValue("duplicates"), new StringValue("reject"));
                    options.InitialPut(new QNameValue("", NamespaceUri.SAXON, "duplicates-error-code"), new StringValue("XQDY0137"));
                    result = MapFunctionSet.GetInstance(31).MakeFunction("merge", 2).MakeFunctionCall(block, Literal.MakeLiteral(options));
                    break;
            }

            SetLocation(result, offset);
            return result;
        }

        protected virtual Expression ParseArraySquareConstructor()
        {
            CheckLanguageVersion31();
            Tokenizer t = GetTokenizer();
            int offset = t.currentTokenStartOffset;
            IList<Expression> members = new List<Expression>();
            NextToken();
            if (t.currentToken == Token.RSQB)
            {
                NextToken();
                SquareArrayConstructor arrayBlock = new SquareArrayConstructor(members);
                SetLocation(arrayBlock, offset);
                return arrayBlock;
            }

            while (true)
            {
                Expression member = ParseExprSingle();
                members.Add(member);
                if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                    continue;
                }
                else if (t.currentToken == Token.RSQB)
                {
                    NextToken();
                    break;
                }

                Grumble("Expected ',' or ']', " + "found " + Token.tokens[t.currentToken]);
                return new ErrorExpression();
            }

            SquareArrayConstructor block = new SquareArrayConstructor(members);
            block.SetLocation(MakeLocation(offset));
            return block;
        }

        protected virtual Expression ParseArrayCurlyConstructor()
        {
            CheckLanguageVersion31();
            Tokenizer t = GetTokenizer();
            int offset = t.currentTokenStartOffset;
            NextToken();
            if (t.currentToken == Token.RCURLY)
            {
                t.LookAhead(); //manual lookahead after an RCURLY
                NextToken();
                return Literal.MakeLiteral(SimpleArrayItem.EMPTY_ARRAY);
            }

            Expression body = ParseExpression();
            Expect(Token.RCURLY);
            t.LookAhead(); //manual lookahead after an RCURLY
            NextToken();
            SystemFunction sf = ArrayFunctionSet.GetInstance(40).MakeFunction("_from-sequence", 1);
            Expression result = sf.MakeFunctionCall(body);
            SetLocation(result, offset);
            return result;
        }

        public virtual Expression ParseFunctionCall(Expression prefixArgument)
        {
            string fname = t.currentTokenValue;
            int offset = t.currentTokenStartOffset;
            List<Expression> args = new List<Expression>(10);
            if (prefixArgument != null)
            {
                args.Add(prefixArgument);
            }

            StructuredQName functionName = ResolveFunctionName(fname);
            IntSet placeMarkers = null;

            // the "(" has already been read by the Tokenizer: now parse the arguments
            Dictionary<StructuredQName, int> keywordArgs = null;
            NextToken();
            if (t.currentToken != Token.RPAR)
            {
                while (true)
                {
                    int peek = t.PeekAhead();
                    Expression arg;
                    if (t.currentToken == Token.NAME && peek == Token.ASSIGN && allowXPath40Syntax)
                    {

                        // keyword argument
                        StructuredQName paramName = qNameParser.Parse(t.currentTokenValue, NamespaceUri.NULL);
                        NextToken(); // read the operator
                        NextToken(); // position on the expression giving the value
                        arg = ParseExprSingle();
                        if (keywordArgs == null)
                        {
                            keywordArgs = new Dictionary<StructuredQName, int>();
                        }
                        else if (keywordArgs.ContainsKey(paramName))
                        {
                            Grumble("Duplicate keyword '" + paramName + "'in function arguments");
                        }

                        keywordArgs[paramName] = args.Count;
                        args.Add(arg);
                    }
                    else
                    {
                        if (keywordArgs != null)
                        {
                            Grumble("Keyword arguments must not be followed by positional arguments in a function call");
                        }

                        if (t.currentToken == Token.QMARK && (peek == Token.COMMA || peek == Token.RPAR))
                        {
                            NextToken();

                            // this is a "?" placemarker
                            if (placeMarkers == null)
                            {
                                placeMarkers = new IntArraySet();
                            }

                            placeMarkers.Add(args.Count);
                            arg = Literal.MakeEmptySequence(); // a convenient fiction
                        }
                        else
                        {
                            arg = ParseFunctionArgument();
                        }

                        args.Add(arg);
                    }

                    if (t.currentToken == Token.COMMA)
                    {
                        NextToken();
                    }
                    else
                    {
                        break;
                    }
                }

                Expect(Token.RPAR);
            }

            NextToken();
            if (scanOnly)
            {
                return new StringLiteral(StringValue.EMPTY_STRING);
            }

            Expression[] arguments = new Expression[args.Count];
            arguments = args.ToArray();
            if (placeMarkers != null)
            {
                return MakeCurriedFunction(this, offset, functionName, arguments, placeMarkers);
            }

            Expression fcall;
            SymbolicName.F sn = new SymbolicName.F(functionName, args.Count);
            IList<string> reasons = new List<string>();
            try
            {
                fcall = env.GetFunctionLibrary().Bind(sn, arguments, keywordArgs, env, reasons);
            }
            catch (UncheckedXPathException e)
            {
                fcall = null;
                reasons.Add(e.GetMessage());
            }

            if (fcall == null)
            {
                return ReportMissingFunction(offset, functionName, arguments, reasons);
            }


            // There are special rules for certain functions appearing in a pattern
            if (language == ParsedLanguage.XSLT_PATTERN)
            {
                if (fcall.IsCallOn(typeof(RegexGroup)))
                {
                    return Literal.MakeEmptySequence();
                }
                else if (fcall is CurrentGroupCall)
                {
                    Grumble("The current-group() function cannot be used in a pattern", "XTSE1060", offset);
                    return new ErrorExpression();
                }
                else if (fcall is CurrentGroupingKeyCall)
                {
                    Grumble("The current-grouping-key() function cannot be used in a pattern", "XTSE1070", offset);
                    return new ErrorExpression();
                }
                else if (fcall.IsCallOn(typeof(CurrentMergeGroup)))
                {
                    Grumble("The current-merge-group() function cannot be used in a pattern", "XTSE3470", offset);
                    return new ErrorExpression();
                }
                else if (fcall.IsCallOn(typeof(CurrentMergeKey)))
                {
                    Grumble("The current-merge-key() function cannot be used in a pattern", "XTSE3500", offset);
                    return new ErrorExpression();
                }
            }

            SetLocation(fcall, offset);
            foreach (Expression argument in arguments)
            {
                if (fcall != argument && argument.ParentExpression == null && !functionName.HasURI(NamespaceUri.GLOBAL_JS))
                {

                    // avoid doing this when the function has already been optimized away, e.g. unordered()
                    // Also avoid doing this when a js: function is parsed into an ixsl:call()
                    // TODO move the adoptChildExpression into individual function libraries
                    fcall.AdoptChildExpression(argument);
                }
            }

            return MakeTracer(fcall, functionName);
        }

        public virtual Expression MakeCurriedFunction(XPathParser parser, int offset, StructuredQName name, Expression[] args, IntSet placeMarkers)
        {
            IStaticContext env = parser.GetStaticContext();
            IFunctionLibrary lib = env.GetFunctionLibrary();
            SymbolicName.F sn = new SymbolicName.F(name, args.Length);
            IFunctionItem target = lib.GetFunctionItem(sn, env);
            if (target == null)
            {

                // This will not happen in XQuery; instead, a dummy function will be created in the
                // UnboundFunctionLibrary in case it's a forward reference to a function not yet compiled
                IList<string> reasons = new List<string>();
                return parser.ReportMissingFunction(offset, name, args, reasons);
            }

            Expression targetExp = MakeNamedFunctionReference(name, target);
            parser.SetLocation(targetExp, offset);
            return CurryFunction(targetExp, args, placeMarkers);
        }

        public static Expression CurryFunction(Expression functionExp, Expression[] args, IntSet placeMarkers)
        {
            IIntIterator ii = placeMarkers.IIterator();
            while (ii.MoveNext())
            {
                args[ii.Current] = null;
            }

            return new PartialApply(functionExp, args);
        }

        public virtual Expression CreateDynamicCurriedFunction(XPathParser p, Expression functionItem, List<Expression> args, IntSet placeMarkers)
        {
            Expression[] arguments = new Expression[args.Count];
            arguments = args.ToArray();
            Expression result = CurryFunction(functionItem, arguments, placeMarkers);
            p.SetLocation(result, p.GetTokenizer().currentTokenStartOffset);
            return result;
        }

        public virtual void HandleExternalFunctionDeclaration(XQueryParser p, XQueryFunction func)
        {
            parserExtension.NeedExtension(p, "External function declarations");
        }

        public virtual Expression ReportMissingFunction(int offset, StructuredQName functionName, Expression[] arguments, IList<string> reasons)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Cannot find a ").Append(arguments.Length).Append("-argument function named ").Append(functionName.EQName).Append("()");
            Configuration config = env.GetConfiguration();
            foreach (string reason in reasons)
            {
                sb.Append(". ").Append(reason);
            }

            if (config.GetBooleanProperty(Feature<bool>.ALLOW_EXTERNAL_FUNCTIONS))
            {
                bool existsWithDifferentArity = false;
                for (int i = 0; i < arguments.Length + 5; i++)
                {
                    if (i != arguments.Length)
                    {
                        SymbolicName.F sn = new SymbolicName.F(functionName, i);
                        if (env.GetFunctionLibrary().IsAvailable(sn, 31))
                        {
                            existsWithDifferentArity = true;
                            break;
                        }
                    }
                }

                if (existsWithDifferentArity)
                {
                    sb.Append(". The namespace URI and local name are recognized, but the number of arguments is wrong");
                }
                else
                {
                    string supplementary = GetMissingFunctionExplanation(functionName, config);
                    if (supplementary != null)
                    {
                        sb.Append(". ").Append(supplementary);
                    }
                }
            }
            else
            {
                sb.Append(". External function calls have been disabled");
            }

            if (env.IsInBackwardsCompatibleMode())
            {

                // treat this as a dynamic error to be reported only if the function call is executed
                return new ErrorExpression(sb.ToString(), "XTDE1425", false);
            }
            else
            {
                Grumble(sb.ToString(), "XPST0017", offset);
                return null;
            }
        }

        public static string GetMissingFunctionExplanation(StructuredQName functionName, Configuration config)
        {
            string actualURI = functionName.GetNamespaceUri().ToString();
            string similarNamespace = NamespaceConstant.FindSimilarNamespace(actualURI);
            if (similarNamespace != null)
            {
                if (similarNamespace.Equals(actualURI))
                {
                    switch (similarNamespace)
                    {
                        case NamespaceConstant.FN:
                            return null;
                        case NamespaceConstant.SAXON:
                            if (config.EditionCode.Equals("HE"))
                            {
                                return "Saxon extension functions are not available under Saxon-HE";
                            }
                            else if (!config.IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
                            {
                                return "Saxon extension functions require a Saxon-PE or Saxon-EE license";
                            }

                            break;
                        case NamespaceConstant.XSLT:
                            if (functionName.GetLocalPart().Equals("original"))
                            {
                                return "Function name xsl:original is only available within an overriding function";
                            }
                            else
                            {
                                return "There are no functions defined in the XSLT namespace";
                            }
                    }
                }
                else
                {
                    return "Perhaps the intended namespace was '" + similarNamespace + "'";
                }
            }
            else if (actualURI.Contains("java"))
            {
                return DiagnoseCallToJavaMethod(config);
            }
            else if (actualURI.StartsWith("clitype:", StringComparison.Ordinal))
            {
                return DiagnoseCallToCliMethod(config);
            }

            return null;
        }

        private static string DiagnoseCallToJavaMethod(Configuration config)
        {
            if (config.EditionCode.Equals("HE"))
            {
                return "Reflexive calls to Java methods are not available under Saxon-HE";
            }
            else if (!config.IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
            {
                return "Reflexive calls to Java methods require a Saxon-PE or Saxon-EE license, and none was found";
            }
            else
            {
                return "For diagnostics on calls to Java methods, use the -TJ command line option " + "or set the Configuration property FeatureKeys.TRACE_EXTERNAL_FUNCTIONS";
            }
        }

        private static string DiagnoseCallToCliMethod(Configuration config)
        {
            if (config.EditionCode.Equals("HE"))
            {
                return "Reflexive calls to external .NET methods are not available under Saxon-HE";
            }
            else if (!config.IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
            {
                return "Reflexive calls to external .NET methods require a Saxon-PE or Saxon-EE license, and none was found";
            }
            else
            {
                return "For diagnostics on calls to .NET methods, use the -TJ command line option " + "or call processor.SetProperty(\"http://saxon.sf.net/feature/trace-external-functions\", \"true\")";
            }
        }

        protected virtual StructuredQName ResolveFunctionName(string fname)
        {
            if (scanOnly)
            {
                return NamespaceUri.SAXON.QName("dummy");
            }

            StructuredQName functionName = null;
            try
            {
                functionName = qNameParser.Parse(fname, env.GetDefaultFunctionNamespace());
            }
            catch (XPathException e)
            {
                Grumble(e.Message, e.ErrorCodeQName);
            }

            if (functionName.HasURI(NamespaceUri.SCHEMA))
            {
                Types.ItemType t = Types.Type.GetBuiltInItemType(functionName.GetNamespaceUri(), functionName.GetLocalPart());
                if (t is BuiltInAtomicType)
                {
                    CheckAllowedType(env, (BuiltInAtomicType)t);
                }
            }

            return functionName;
        }

        public virtual Expression ParseFunctionArgument()
        {
            return ParseExprSingle();
        }

        protected virtual Expression ParseNamedFunctionReference()
        {
            string fname = t.currentTokenValue;
            int offset = t.currentTokenStartOffset;
            IStaticContext env = GetStaticContext();

            // the "#" has already been read by the Tokenizer: now parse the arity
            NextToken();
            Expect(Token.NUMBER);
            NumericValue number = NumericValue.ParseNumber(t.currentTokenValue);
            if (!(number is IntegerValue))
            {
                Grumble("Number following '#' must be an integer");
            }

            if (number.CompareTo(0) < 0 || number.CompareTo(int.MaxValue) > 0)
            {
                Grumble("Number following '#' is out of range", "FOAR0002");
            }

            int arity = (int)number.LongValue();
            NextToken();
            StructuredQName functionName = null;
            try
            {
                functionName = GetQNameParser().Parse(fname, env.GetDefaultFunctionNamespace());
                if (functionName.GetPrefix().Equals(""))
                {
                    if (XPathParser.IsReservedFunctionName(functionName.GetLocalPart(), languageVersion))
                    {
                        Grumble("The unprefixed function name '" + functionName.GetLocalPart() + "' is reserved in XPath 3.1");
                    }
                }
            }
            catch (XPathException e)
            {
                Grumble(e.Message, e.ErrorCodeQName);
            }

            IFunctionItem fcf = null;
            try
            {
                IFunctionLibrary lib = env.GetFunctionLibrary();
                SymbolicName.F sn = new SymbolicName.F(functionName, arity);
                fcf = lib.GetFunctionItem(sn, env);
                if (fcf == null)
                {
                    Grumble("Function " + functionName.EQName + "#" + arity + " not found", "XPST0017", offset);
                }
            }
            catch (XPathException e)
            {
                Grumble(e.Message, "XPST0017", offset);
            }


            // Special treatment of functions in the system function library that depend on dynamic context; turn these
            // into calls on function-lookup()
            if (functionName.HasURI(NamespaceUri.FN) && fcf is SystemFunction)
            {
                BuiltInFunctionSet.Entry details = ((SystemFunction)fcf).Details;
                if (fcf is ContextAccessorFunction || (details != null && (details.properties & (BuiltInFunctionSet.FOCUS | BuiltInFunctionSet.DEPENDS_ON_STATIC_CONTEXT)) != 0))
                {

                    // For a context-dependent function, return a call on function-lookup(), which saves the context
                    SystemFunction lookup = XPath31FunctionSet.GetInstance().MakeFunction("function-lookup", 2);
                    lookup.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                    return lookup.MakeFunctionCall(Literal.MakeLiteral(new QNameValue(functionName, BuiltInAtomicType.QNAME)), Literal.MakeLiteral(Int64Value.MakeIntegerValue(arity)));
                }
            }

            Expression @ref = MakeNamedFunctionReference(functionName, fcf);
            SetLocation(@ref, offset);
            return @ref;
        }

        private static Expression MakeNamedFunctionReference(StructuredQName functionName, IFunctionItem fcf)
        {
            if (fcf is UserFunction && !functionName.HasURI(NamespaceUri.XSLT))
            {

                // This case is treated specially because a UserFunctionReference in XSLT can be redirected
                // at link time to an overriding function. However, this doesn't apply to xsl:original
                return new UserFunctionReference((UserFunction)fcf);
            }
            else if (fcf is UnresolvedXQueryFunctionItem)
            {
                return ((UnresolvedXQueryFunctionItem)fcf).FunctionReference;
            }
            else
            {
                return new FunctionLiteral(fcf);
            }
        }

        protected virtual AnnotationList ParseAnnotationsList()
        {
            Grumble("Function annotations are not allowed in XPath");
            return null;
        }

        protected virtual Expression ParseInlineFunction(AnnotationList annotations)
        {
            NextToken();
            IList<UserFunctionParameter> @params = new List<UserFunctionParameter>(8);
            Values.SequenceType resultType = Values.SequenceType.ANY_SEQUENCE;
            int paramSlot = 0;
            while (t.currentToken != Token.RPAR)
            {

                //     ParamList   ::=     Param ("," Param)*
                //     Param       ::=     "$" VarName  TypeDeclaration?
                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                string argName = t.currentTokenValue;
                StructuredQName argQName = MakeStructuredQName(argName, NamespaceUri.NULL);
                Values.SequenceType paramType = Values.SequenceType.ANY_SEQUENCE;
                NextToken();
                if (t.currentToken == Token.AS)
                {
                    NextToken();
                    paramType = ParseSequenceType();
                }

                UserFunctionParameter arg = new UserFunctionParameter();
                arg.SetRequiredType(paramType);
                arg.SetVariableQName(argQName);
                arg.SetSlotNumber(paramSlot++);
                @params.Add(arg);
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
                    Grumble("Expected ',' or ')' after function argument, found '" + Token.tokens[t.currentToken] + '\'');
                }
            }

            t.State = Tokenizer.BARE_NAME_STATE;
            NextToken();
            if (t.currentToken == Token.AS)
            {
                t.State = Tokenizer.SEQUENCE_TYPE_STATE;
                NextToken();
                resultType = ParseSequenceType();
            }

            return ParseInlineFunctionBody(annotations, @params, resultType);
        }

        protected virtual Expression ParseInlineFunctionBody(AnnotationList annotations, IList<UserFunctionParameter> @params, Values.SequenceType resultType)
        {

            // the next token should be the "{" at the start of the function body
            int offset = t.currentTokenStartOffset;
            InlineFunctionDetails details = new InlineFunctionDetails();
            details.outerVariables = new IndexedStack<ILocalBinding>();
            foreach (ILocalBinding lb in RangeVariables)
            {
                details.outerVariables.IPush(lb);
            }

            details.outerVariablesUsed = new List<ILocalBinding>(4);
            details.implicitParams = new List<UserFunctionParameter>(4);
            inlineFunctionStack.IPush(details);

            RangeVariables = new IndexedStack<ILocalBinding>();
            HashSet<StructuredQName> paramNameSet = new HashSet<StructuredQName>();
            foreach (UserFunctionParameter arg in @params)
            {
                if (!scanOnly)
                {
                    if (!paramNameSet.Add(arg.GetVariableQName()))
                    {
                        Grumble("Duplicate parameter name " + Err.Wrap(arg.GetVariableQName().EQName, Err.VARIABLE), "XQST0039");
                    }
                }

                DeclareRangeVariable(arg);
            }

            Expect(Token.LCURLY);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression body;
            if (t.currentToken == Token.RCURLY && IsAllowXPath31Syntax())
            {
                t.LookAhead();
                NextToken();
                body = Literal.MakeEmptySequence();
            }
            else
            {
                body = ParseExpression();
                Expect(Token.RCURLY);
                t.LookAhead(); // must be done manually after an RCURLY
                NextToken();
            }

            ExpressionTool.SetDeepRetainedStaticContext(body, GetStaticContext().MakeRetainedStaticContext());
            Expression result = MakeInlineFunctionValue(this, annotations, details, @params, resultType, body);
            SetLocation(result, offset);
            foreach (UserFunctionParameter arg in @params)
            {
                UndeclareRangeVariable();
            }


            // restore the previous stack of range variables
            RangeVariables = details.outerVariables;
            inlineFunctionStack.Pop();
            return result;
        }

        public static Expression MakeInlineFunctionValue(XPathParser p, AnnotationList annotations, InlineFunctionDetails details, IList<UserFunctionParameter> @params, Values.SequenceType resultType, Expression body)
        {

            // Does this function access any outer variables?
            // If so, we create a UserFunction in which the outer variables are defined as extra parameters
            // in addition to the declared parameters, and then we return a call to partial-apply() that
            // sets these additional parameters to the values they have in the calling context.
            int arity = @params.Count;
            UserFunction uf = new UserFunction();
            uf.SetFunctionName(new StructuredQName("anon", NamespaceUri.ANONYMOUS, "f_" + uf.GetHashCode()));
            uf.SetPackageData(p.GetStaticContext().GetPackageData());
            uf.SetBody(body);
            uf.SetAnnotations(annotations);
            uf.ResultType = resultType;
            uf.IncrementReferenceCount();
            if (uf.GetPackageData() is StylesheetPackage)
            {

                // Add the inline function as a private component to the package, so that it can have binding
                // slots allocated for any references to global variables or functions, and so that it will
                // be copied as a hidden component into any using packages
                StylesheetPackage pack = (StylesheetPackage)uf.GetPackageData();
                Component comp = Component.MakeComponent(uf, Visibility.PRIVATE, VisibilityProvenance.DEFAULTED, pack, pack);
                uf.DeclaringComponent = comp;
            }

            Expression result;
            IList<UserFunctionParameter> implicitParams = details.implicitParams;
            if (implicitParams.Count > 0)
            {
                int extraParams = implicitParams.Count;
                int expandedArity = @params.Count + extraParams;
                UserFunctionParameter[] paramArray = new UserFunctionParameter[expandedArity];
                for (int i = 0; i < @params.Count; i++)
                {
                    paramArray[i] = @params[i];
                }

                int k = @params.Count;
                foreach (UserFunctionParameter implicitParam in implicitParams)
                {
                    paramArray[k++] = implicitParam;
                }

                uf.SetParameterDefinitions(paramArray);
                SlotManager stackFrame = p.GetStaticContext().GetConfiguration().MakeSlotManager();
                for (int i = 0; i < expandedArity; i++)
                {
                    int slot = stackFrame.AllocateSlotNumber(paramArray[i].GetVariableQName(), paramArray[i]);
                    paramArray[i].SetSlotNumber(slot);
                }

                ExpressionTool.AllocateSlots(body, expandedArity, stackFrame);
                uf.SetStackFrameMap(stackFrame);
                result = new UserFunctionReference(uf);
                Expression[] partialArgs = new Expression[expandedArity];
                for (int i = 0; i < arity; i++)
                {
                    partialArgs[i] = null;
                }

                for (int ip = 0; ip < implicitParams.Count; ip++)
                {
                    UserFunctionParameter ufp = implicitParams[ip];
                    ILocalBinding binding = details.outerVariablesUsed[ip];
                    VariableReference var;
                    if (binding is ParserExtension.TemporaryXSLTVariableBinding)
                    {
                        var = new LocalVariableReference(binding);
                        ((ParserExtension.TemporaryXSLTVariableBinding)binding).declaration.RegisterReference(var);
                    }
                    else
                    {
                        var = new LocalVariableReference(binding);
                    }

                    var.SetStaticType(binding.GetRequiredType(), null, 0);
                    ufp.SetRequiredType(binding.GetRequiredType());
                    partialArgs[ip + arity] = var;
                }

                result = new PartialApply(result, partialArgs);
            }
            else
            {

                // there are no implicit parameters
                UserFunctionParameter[] paramArray = @params.ToArray();
                uf.SetParameterDefinitions(paramArray);
                SlotManager stackFrame = p.GetStaticContext().GetConfiguration().MakeSlotManager();
                foreach (UserFunctionParameter param in paramArray)
                {
                    stackFrame.AllocateSlotNumber(param.GetVariableQName(), param);
                }

                ExpressionTool.AllocateSlots(body, @params.Count, stackFrame);
                uf.SetStackFrameMap(stackFrame);
                result = new UserFunctionReference(uf);
            }

            if (uf.GetPackageData() is StylesheetPackage)
            {

                // Note: inline functions in XSLT are registered as components; but not if they
                // are declared within a static expression, e.g. the initializer of a static
                // global variable
                ((StylesheetPackage)uf.GetPackageData()).AddComponent(uf.DeclaringComponent);
            }

            return result;
        }

    }
}
