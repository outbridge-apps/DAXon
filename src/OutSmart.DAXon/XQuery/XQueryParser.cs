////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.XQuery
{
    // Split into partial files (XQueryParser.Pn.*.cs); the Pn prefix fixes compile order:
    // member order across parts is the assembly metadata order — keep it byte-stable.
    public partial class XQueryParser : XPathParser
    {

        private static readonly OutSmart.DAXon.Internal.Regex.Pattern encNamePattern = OutSmart.DAXon.Internal.Regex.Pattern.Compile("^[A-Za-z]([A-Za-z0-9._\\x2D])*$");

        public static readonly StructuredQName SAXON_MEMO_FUNCTION = new StructuredQName("saxon", NamespaceUri.SAXON, "memo-function");
        private bool memoFunction;
        private bool streaming;
        private int errorCount;
        private XPathException firstError;
        protected Executable executable;
        private bool foundCopyNamespaces;
        private bool foundBoundarySpaceDeclaration;
        private bool foundOrderingDeclaration;
        private bool foundEmptyOrderingDeclaration;
        private bool foundDefaultCollation;
        private bool foundConstructionDeclaration;
        private bool foundDefaultFunctionNamespace;
        private bool foundDefaultElementNamespace;
        private bool foundBaseURIDeclaration;
        private bool foundContextItemDeclaration;
        private bool foundDefaultDecimalFormat;
        private bool preambleProcessed;
        public readonly HashSet<NamespaceUri> importedModules = new HashSet<NamespaceUri>();
        readonly IList<NamespaceUri> namespacesToBeSealed = new List<NamespaceUri>(10);
        readonly IList<Import> schemaImports = new List<Import>(5);
        readonly IList<Import> moduleImports = new List<Import>(5);
        private readonly HashSet<StructuredQName> outputPropertiesSeen = new HashSet<StructuredQName>();
        private Properties parameterDocProperties;
        public XQueryParser(IStaticContext env) : base(env)
        {
            this.languageVersion = 31; // Until proved otherwise
            SetLanguage(ParsedLanguage.XQUERY, languageVersion);
        }

        private XQueryParser NewParser()
        {
            XQueryParser qp = new XQueryParser(env);
            qp.SetLanguage(language, languageVersion);
            qp.SetParserExtension(parserExtension);
            return qp;
        }

        public virtual XQueryExpression MakeXQueryExpression(string query, QueryModule mainModule, Configuration config)
        {
            try
            {
                SetLanguage(ParsedLanguage.XQUERY, languageVersion);
                if (config.XMLVersion == Configuration.XML10)
                {
                    query = NormalizeLineEndings10(query);
                }
                else
                {
                    query = NormalizeLineEndings11(query);
                }

                Executable exec = mainModule.GetExecutable();
                if (exec == null)
                {
                    exec = new Executable(config);
                    exec.SetHostLanguage(HostLanguage.XQUERY);
                    exec.TopLevelPackage = mainModule.GetPackageData();
                    SetExecutable(exec); //mainModule.setExecutable(exec);
                }

                GlobalContextRequirement requirement = exec.GlobalContextRequirement;
                if (requirement != null)
                {
                    requirement.AddRequiredItemType(mainModule.GetRequiredContextItemType());
                }
                else if (mainModule.GetRequiredContextItemType() != null && mainModule.GetRequiredContextItemType() != AnyItemType.GetInstance())
                {
                    GlobalContextRequirement req = new GlobalContextRequirement();
                    req.SetExternal(true);
                    req.AddRequiredItemType(mainModule.GetRequiredContextItemType());
                    exec.GlobalContextRequirement = req;
                }


                Properties outputProps = new Properties(config.DefaultSerializationProperties);
                if (outputProps.GetProperty(DAXonOutputKeys.METHOD) == null)
                {
                    outputProps.SetProperty(DAXonOutputKeys.METHOD, "xml");
                }

                parameterDocProperties = new Properties(outputProps);
                exec.SetDefaultOutputProperties(new Properties(parameterDocProperties));

                FunctionLibraryList libList = new FunctionLibraryList();
                libList.AddFunctionLibrary(new ExecutableFunctionLibrary(config));
                exec.FunctionLibrary = libList;

                // this will be changed later
                SetExecutable(exec);
                CodeInjector = mainModule.CodeInjector;
                Expression exp = ParseQuery(query, mainModule);
                if (streaming)
                {
                    env.GetConfiguration().CheckLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY, "streaming", -1);
                }

                exec.FixupQueryModules(mainModule);

                // Make the XQueryExpression object
                XQueryExpression queryExp = config.MakeXQueryExpression(exp, mainModule, streaming);
                CreateRunTimeFunctionLibrary(mainModule, config, exec);
                return queryExp;
            }
            catch (XPathException e)
            {
                if (!e.HasBeenReported())
                {
                    ReportError(e);
                }

                throw;
            }
        }

        public static void CreateRunTimeFunctionLibrary(QueryModule mainModule, Configuration config, Executable exec)
        {

            // Make the function library that's available at run-time (e.g. for saxon:evaluate() and function-lookup()).
            // This includes all user-defined functions regardless of which module they are in
            IFunctionLibrary userlib = exec.FunctionLibrary;
            FunctionLibraryList lib = new FunctionLibraryList();
            lib.AddFunctionLibrary(mainModule.GetBuiltInFunctionSet());
            lib.AddFunctionLibrary(config.GetBuiltInExtensionLibraryList(31));
            lib.AddFunctionLibrary(new ConstructorFunctionLibrary(config));
            lib.AddFunctionLibrary(config.GetIntegratedFunctionLibrary());
            lib.AddFunctionLibrary(mainModule.GlobalFunctionLibrary);
            config.AddExtensionBinders(lib);
            lib.AddFunctionLibrary(userlib);
            exec.FunctionLibrary = lib;
        }

        private static string NormalizeLineEndings11(string @in)
        {
            if (@in.IndexOf((char)0xd) < 0 && @in.IndexOf((char)0x85) < 0 && @in.IndexOf((char)0x2028) < 0)
            {
                return @in;
            }

            StringBuilder sb = new StringBuilder(@in.Length);
            for (int i = 0; i < @in.Length; i++)
            {
                char ch = @in[i];
                switch (ch)
                {
                    case (char)0x85:
                    case (char)0x2028:
                        sb.Append((char)0xa);
                        break;
                    case (char)0xd:
                        if (i < @in.Length - 1 && (@in[i + 1] == (char)0xa || @in[i + 1] == (char)0x85))
                        {
                            sb.Append((char)0xa);
                            i++;
                        }
                        else
                        {
                            sb.Append((char)0xa);
                        }

                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }

        private static string NormalizeLineEndings10(string @in)
        {
            if (@in.IndexOf((char)0xd) < 0)
            {
                return @in;
            }

            StringBuilder sb = new StringBuilder(@in.Length);
            for (int i = 0; i < @in.Length; i++)
            {
                char ch = @in[i];
                if (ch == 0xd)
                {
                    if (i < @in.Length - 1 && @in[i + 1] == (char)0xa)
                    {
                        sb.Append((char)0xa);
                        i++;
                    }
                    else
                    {
                        sb.Append((char)0xa);
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        public virtual Executable GetExecutable()
        {
            return executable;
        }

        public virtual void SetExecutable(Executable exec)
        {
            executable = exec;
        }

        protected override void CustomizeTokenizer(Tokenizer t)
        {
            t.isXQuery = true;
        }

        public virtual void SetStreaming(bool option)
        {
            streaming = option;
        }

        public virtual bool IsStreaming()
        {
            return streaming;
        }

        private Expression ParseQuery(string queryString, QueryModule env)
        {
            this.env = env ?? throw new NullReferenceException();
            charChecker = env.GetConfiguration().ValidCharacterChecker;

            language = ParsedLanguage.XQUERY;
            t = new Tokenizer();
            t.languageLevel = languageVersion = env.GetXPathVersion();
            t.isXQuery = true;
            try
            {
                t.Tokenize(queryString ?? throw new NullReferenceException(), 0, -1);
            }
            catch (XPathException err)
            {
                Grumble(err.Message);
            }

            ParseVersionDeclaration();
            allowXPath40Syntax = t.allowSaxonExtensions = allowXPath40Syntax || env.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS) || languageVersion >= 40;
            env.SetXPathVersion(languageVersion);
            env.InitializeFunctionLibraries();
            QNameParser qp = new QNameParser(env.LiveNamespaceResolver).WithAcceptEQName(true).WithUnescaper(new Unescaper(env.GetConfiguration().ValidCharacterChecker));
            SetQNameParser(qp);
            ParseProlog();
            ProcessPreamble();
            Expression exp = ParseExpression();
            exp = MakeTracer(exp, null);

            if (t.currentToken != Token.EOF)
            {
                Grumble("Unexpected token " + CurrentTokenDisplay() + ": no further input expected");
            }

            SetLocation(exp);
            ExpressionTool.SetDeepRetainedStaticContext(exp, env.MakeRetainedStaticContext());
            if (errorCount == 0)
            {
                return exp;
            }
            else
            {
                XPathException err = new XPathException("One or more static errors were reported during query analysis");
                err.SetHasBeenReported(true);
                err.ErrorCodeQName = firstError.ErrorCodeQName; // largely for the XQTS test driver
                throw err;
            }
        }

        public void ParseLibraryModule(string queryString, QueryModule env)
        {
            this.env = env;
            Configuration config = env.GetConfiguration();
            charChecker = config.ValidCharacterChecker;
            if (config.XMLVersion == Configuration.XML10)
            {
                queryString = NormalizeLineEndings10(queryString);
            }
            else
            {
                queryString = NormalizeLineEndings11(queryString);
            }

            Executable exec = env.GetExecutable();
            if (exec == null)
            {
                throw new InvalidOperationException("Query library module has no associated Executable");
            }

            executable = exec;

            t = new Tokenizer();
            t.languageLevel = languageVersion;
            t.isXQuery = true;
            QNameParser qp = new QNameParser(env.LiveNamespaceResolver).WithAcceptEQName(true).WithUnescaper(new Unescaper(config.ValidCharacterChecker));
            SetQNameParser(qp);
            try
            {
                t.Tokenize(queryString, 0, -1);
            }
            catch (XPathException err)
            {
                Grumble(err.Message);
            }

            ParseVersionDeclaration();
            allowXPath40Syntax = t.allowSaxonExtensions = allowXPath40Syntax || env.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS);
            env.SetXPathVersion(languageVersion);
            env.InitializeFunctionLibraries();
            if (t.currentToken != Token.MODULE_NAMESPACE)
            {
                if (t.currentToken == Token.EOF)
                {
                    Grumble("The file imported for module " + env.ModuleNamespace + (queryString.Trim().Length == 0 ? " is empty" : " has no significant content"));
                }
                else
                {
                    Grumble("The file imported for module " + env.ModuleNamespace + " is not a valid XQuery library module. " + "The content starts: " + Err.Truncate30(StringView.Of(queryString.Substring(t.currentTokenStartOffset))));
                }
            }

            ParseModuleDeclaration();
            ParseProlog();
            ProcessPreamble();
            if (t.currentToken != Token.EOF)
            {
                Grumble("Unrecognized content found after the variable and function declarations in a library module");
            }

            if (errorCount != 0)
            {
                XPathException err = new XPathException("Static errors were reported in the imported library module");
                err.ErrorCodeQName = firstError.ErrorCodeQName;
                throw err;
            }
        }

        private void ReportError(XPathException exception)
        {
            errorCount++;
            if (firstError == null)
            {
                firstError = exception;
            }

            ((QueryModule)env).ReportStaticError(exception);
            throw exception;
        }
    }
}
