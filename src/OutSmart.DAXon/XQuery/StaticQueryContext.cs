////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
namespace OutSmart.DAXon.XQuery
{
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(Action<Saxon.Hej.s9api.IXmlProcessingError> reporter) {"
    //                + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //                + "    }"
    //})
    public class StaticQueryContext
    {
        private readonly object syncLock = new object();
        private Configuration config;
        private NamePool namePool;
        private string baseURI;
        private Dictionary<string, NamespaceUri> userDeclaredNamespaces;
        private HashSet<GlobalVariable> userDeclaredVariables;
        private bool inheritNamespaces = true;
        private bool preserveNamespaces = true;
        private int constructionMode = Validation.PRESERVE;
        private NamespaceUri defaultFunctionNamespace = NamespaceUri.FN;
        private NamespaceUri defaultElementNamespace = NamespaceUri.NULL;
        private Types.ItemType requiredContextItemType = AnyItemType.GetInstance();
        private bool preserveSpace = false;
        private bool defaultEmptyLeast = true;
        private IModuleURIResolver moduleURIResolver;
        private IErrorReporter errorReporter;
        private ICodeInjector codeInjector;
        private bool updating = false;
        private string defaultCollationName;
        private ILocation moduleLocation;
        private OptimizerOptions optimizerOptions;
        private int languageVersion = 31;
        private UnprefixedElementMatchingPolicy unprefixedElementMatchingPolicy = UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE;

        public virtual int LanguageVersion
        {
            get => languageVersion; set
            {
                if (value == 10 || value == 30 || value == 31)
                {
                    languageVersion = 31;
                }
                else if (value == 40)
                {
                    config.CheckLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION, "XQuery 4.0", -1);
                    languageVersion = 40;
                }
                else
                {
                    throw new ArgumentException("languageVersion = " + value);
                }
            }
        }

        public virtual IFunctionLibrary ExtensionFunctionLibrary => null;

        public virtual ICodeInjector CodeInjector
        {
            get => codeInjector; set
            {
                this.codeInjector = value;
            }
        }

        public virtual int ConstructionMode
        {
            get => constructionMode; set
            {
                constructionMode = value;
            }
        }

        public virtual ILocation ModuleLocation
        {
            get => moduleLocation; set
            {
                this.moduleLocation = value;
            }
        }

        public virtual IEnumerable<QueryLibrary> CompiledLibraries => new HashSet<QueryLibrary>();

        public virtual Dictionary<string, NamespaceUri> UserDeclaredNamespaces => userDeclaredNamespaces;

        public virtual NamespaceUri DefaultFunctionNamespace
        {
            get => defaultFunctionNamespace; set
            {
                this.defaultFunctionNamespace = value;
            }
        }

        public virtual NamespaceUri DefaultElementNamespace
        {
            get => defaultElementNamespace; set
            {
                defaultElementNamespace = value;
                DeclareNamespace("", value);
            }
        }

        public virtual IModuleURIResolver ModuleURIResolver
        {
            get => moduleURIResolver; set
            {
                moduleURIResolver = value;
            }
        }

        public virtual Types.ItemType RequiredContextItemType
        {
            get => requiredContextItemType; set
            {
                requiredContextItemType = value;
            }
        }

        public virtual string BaseURI
        {
            get => baseURI; set
            {
                this.baseURI = value;
            }
        }

        public virtual IErrorReporter ErrorReporter
        {
            get => this.errorReporter; set
            {
                this.errorReporter = value;
            }
        }
        /// <summary>
        /// Private constructor used when copying a context
        /// </summary>
        protected StaticQueryContext()
        {
        }

        public StaticQueryContext(Configuration config) : this(config, true)
        {
        }

        public StaticQueryContext(Configuration config, bool initialize)
        {
            this.config = config;
            this.namePool = config.GetNamePool();
            this.errorReporter = config.MakeErrorReporter();
            this.languageVersion = config.GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS) ? 40 : 31;
            if (initialize)
            {
                CopyFrom(config.DefaultStaticQueryContext);
            }
            else
            {
                userDeclaredNamespaces = new Dictionary<string, NamespaceUri>();
                userDeclaredVariables = new HashSet<GlobalVariable>();
                optimizerOptions = config.GetOptimizerOptions();
                ClearNamespaces();
            }
        }

        public StaticQueryContext(StaticQueryContext c)
        {
            CopyFrom(c);
        }

        public virtual void CopyFrom(StaticQueryContext c)
        {
            config = c.config;
            namePool = c.namePool;
            baseURI = c.baseURI;
            moduleURIResolver = c.moduleURIResolver;
            if (c.userDeclaredNamespaces != null)
            {
                userDeclaredNamespaces = new Dictionary<string, NamespaceUri>(c.userDeclaredNamespaces);
            }

            if (c.userDeclaredVariables != null)
            {
                userDeclaredVariables = new HashSet<GlobalVariable>(c.userDeclaredVariables);
            }

            inheritNamespaces = c.inheritNamespaces;
            preserveNamespaces = c.preserveNamespaces;
            constructionMode = c.constructionMode;
            defaultElementNamespace = c.defaultElementNamespace;
            defaultFunctionNamespace = c.defaultFunctionNamespace;
            requiredContextItemType = c.requiredContextItemType;
            preserveSpace = c.preserveSpace;
            defaultEmptyLeast = c.defaultEmptyLeast;
            errorReporter = c.errorReporter;
            codeInjector = c.codeInjector;
            updating = c.updating;
            optimizerOptions = c.optimizerOptions;
            unprefixedElementMatchingPolicy = c.unprefixedElementMatchingPolicy;
        }

        public virtual void Reset()
        {
            userDeclaredNamespaces = new Dictionary<string, NamespaceUri>(10);
            errorReporter = config.MakeErrorReporter();
            constructionMode = GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY) ? Validation.PRESERVE : Validation.STRIP;
            preserveSpace = false;
            defaultEmptyLeast = true;
            requiredContextItemType = AnyItemType.GetInstance();
            defaultFunctionNamespace = NamespaceUri.FN;
            defaultElementNamespace = NamespaceUri.NULL;
            moduleURIResolver = null;
            defaultCollationName = config.GetDefaultCollationName();
            ClearNamespaces();
            updating = false;
            optimizerOptions = config.GetOptimizerOptions();
            unprefixedElementMatchingPolicy = UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE;
        }

        public virtual void SetConfiguration(Configuration config)
        {
            if (this.config != null && this.config != config)
            {
                throw new ArgumentException("Configuration cannot be changed dynamically");
            }

            this.config = config;
            namePool = config.GetNamePool();
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual Executable MakeExecutable()
        {
            Executable executable = new Executable(config);
            executable.SetSchemaAware(IsSchemaAware());
            executable.SetHostLanguage(HostLanguage.XQUERY);
            return executable;
        }

        public virtual void SetSchemaAware(bool aware)
        {
            if (aware)
            {
                throw new NotSupportedException("Schema-awareness requires Saxon-EE");
            }
        }

        public virtual bool IsSchemaAware()
        {
            return false;
        }

        public virtual void SetStreaming(bool option)
        {
            if (option)
            {
                throw new NotSupportedException("Streaming requires Saxon-EE");
            }
        }

        public virtual bool IsStreaming()
        {
            return false;
        }

        public virtual bool IsCompileWithTracing()
        {
            return codeInjector is TraceCodeInjector;
        }

        public virtual void SetCompileWithTracing(bool trace)
        {
            codeInjector = trace ? new XQueryTraceCodeInjector() : null;
        }

        public virtual bool IsUpdating()
        {
            return updating;
        }

        public virtual void SetInheritNamespaces(bool inherit)
        {
            inheritNamespaces = inherit;
        }

        public virtual bool IsInheritNamespaces()
        {
            return inheritNamespaces;
        }

        public virtual void SetPreserveNamespaces(bool inherit)
        {
            preserveNamespaces = inherit;
        }

        public virtual bool IsPreserveNamespaces()
        {
            return preserveNamespaces;
        }

        public virtual void SetOptimizerOptions(OptimizerOptions options)
        {
            this.optimizerOptions = options;
        }

        public virtual OptimizerOptions GetOptimizerOptions()
        {
            return this.optimizerOptions;
        }

        public virtual XQueryExpression CompileQuery(string query)
        {
            using RunResources run = RunResources.Enter();
            // Compile under the Processor's deadline: constant folding of hostile query text is
            // otherwise unbounded work before any run-time deadline exists (see ArmThreadDeadline).
            Controller.DeadlineToken prevDeadline = Controller.ArmThreadDeadline(config, "Compilation");
            try
            {
                QueryModule mainModule = new QueryModule(this);
                XQueryParser qp = (XQueryParser)config.NewExpressionParser("XQ", updating, mainModule);
                if (codeInjector != null)
                {
                    qp.CodeInjector = codeInjector;
                }
                else if (config.IsCompileWithTracing())
                {
                    qp.CodeInjector = new XQueryTraceCodeInjector();
                }

                qp.SetStreaming(IsStreaming());
                return qp.MakeXQueryExpression(query, mainModule, config);
            }
            finally
            {
                Controller.RestoreThreadDeadline(prevDeadline);
            }
        }

        public virtual XQueryExpression CompileQuery(TextReader source)
        {
            lock (syncLock)
            {
                char[] buffer = new char[4096];
                StringBuilder sb = new StringBuilder(4096);
                while (true)
                {
                    int n = source.Read(buffer, 0, buffer.Length);
                    if (n > 0)
                    {
                        sb.Append(buffer, 0, n);
                    }
                    else
                    {
                        break;
                    }
                }

                return CompileQuery(sb.ToString());
            }
        }

        public virtual XQueryExpression CompileQuery(System.IO.Stream source, string encoding)
        {
            lock (syncLock)
            {
                try
                {
                    string query = QueryReader.ReadInputStream(source, encoding, config.ValidCharacterChecker);
                    return CompileQuery(query);
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }
        }

        public virtual void CompileLibrary(string query)
        {
            throw new XPathException("Separate compilation of query libraries requires Saxon-EE");
        }

        public virtual void CompileLibrary(TextReader source)
        {
            throw new XPathException("Separate compilation of query libraries requires Saxon-EE");
        }

        public virtual void CompileLibrary(System.IO.Stream source, string encoding)
        {
            throw new NotSupportedException("Separate compilation of query libraries requires Saxon-EE");
        }

        public virtual QueryLibrary GetCompiledLibrary(NamespaceUri @namespace)
        {
            return null;
        }

        public virtual void DeclareNamespace(string prefix, NamespaceUri uri)
        {
            if (prefix == null)
            {
                throw new NullReferenceException("Null prefix supplied to declareNamespace()");
            }

            if (uri == null)
            {
                throw new NullReferenceException("Null namespace URI supplied to declareNamespace()");
            }

            if (prefix.Equals("xml") != uri.Equals(NamespaceUri.XML))
            {
                throw new ArgumentException("Misdeclaration of XML namespace");
            }

            if (prefix.Equals("xmlns") || uri.Equals(NamespaceUri.XMLNS))
            {
                throw new ArgumentException("Misdeclaration of xmlns namespace");
            }

            if ((prefix.Length == 0))
            {
                defaultElementNamespace = uri;
            }

            if (uri.IsEmpty())
            {
                userDeclaredNamespaces.Remove(prefix);
            }
            else
            {
                userDeclaredNamespaces[prefix] = uri;
            }
        }

        public virtual void ClearNamespaces()
        {
            userDeclaredNamespaces.Clear();
            DeclareNamespace("xml", NamespaceUri.XML);
            DeclareNamespace("xs", NamespaceUri.SCHEMA);
            DeclareNamespace("xsi", NamespaceUri.SCHEMA_INSTANCE);
            DeclareNamespace("fn", NamespaceUri.FN);
            DeclareNamespace("math", NamespaceUri.MATH);
            DeclareNamespace("map", NamespaceUri.MAP_FUNCTIONS);
            DeclareNamespace("array", NamespaceUri.ARRAY_FUNCTIONS);
            DeclareNamespace("local", NamespaceUri.LOCAL);
            DeclareNamespace("err", NamespaceUri.ERR);
            DeclareNamespace("saxon", NamespaceUri.SAXON);
            DeclareNamespace("", NamespaceUri.NULL);
        }

        public virtual IEnumerator<string> IterateDeclaredPrefixes()
        {
            return userDeclaredNamespaces.Keys.GetEnumerator();
        }

        public virtual NamespaceUri GetNamespaceForPrefix(string prefix)
        {
            return userDeclaredNamespaces.GetOrDefault(prefix);
        }

        public virtual UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy()
        {
            return unprefixedElementMatchingPolicy;
        }

        public virtual void SetUnprefixedElementMatchingPolicy(UnprefixedElementMatchingPolicy unprefixedElementMatchingPolicy)
        {
            this.unprefixedElementMatchingPolicy = unprefixedElementMatchingPolicy;
        }

        public virtual void DeclareGlobalVariable(StructuredQName qName, Values.SequenceType type, IGroundedValue value, bool external)
        {
            if (value == null && !external)
            {
                throw new NullReferenceException("No initial value for declared variable");
            }

            if (value != null && !type.Matches(value, GetConfiguration().GetTypeHierarchy()))
            {
                throw new XPathException("Value of declared variable does not match its type");
            }

            GlobalVariable var = external ? new GlobalParam() : new GlobalVariable();
            var.SetVariableQName(qName);
            var.SetRequiredType(type);
            if (value != null)
            {
                var.SetBody(Literal.MakeLiteral(value.Materialize()));
            }

            if (userDeclaredVariables == null)
            {
                userDeclaredVariables = new HashSet<GlobalVariable>();
            }

            userDeclaredVariables.Add(var);
        }

        public virtual IEnumerable<GlobalVariable> IterateDeclaredGlobalVariables()
        {
            if (userDeclaredVariables == null)
            {
                return new List<GlobalVariable>();
            }
            else
            {
                return userDeclaredVariables;
            }
        }

        public virtual void ClearDeclaredGlobalVariables()
        {
            userDeclaredVariables = null;
        }

        public virtual void DeclareDefaultCollation(string name)
        {
            if (name == null)
            {
                throw new NullReferenceException();
            }

            IStringCollator c;
            try
            {
                c = GetConfiguration().GetCollation(name);
            }
            catch (XPathException e)
            {
                c = null;
            }

            if (c == null)
            {
                throw new InvalidOperationException("Unknown collation " + name);
            }

            this.defaultCollationName = name;
        }

        public virtual string GetDefaultCollationName()
        {
            return defaultCollationName;
        }

        public virtual NamePool GetNamePool()
        {
            return namePool;
        }

        public virtual string GetSystemId()
        {
            return baseURI;
        }

        public virtual void SetPreserveBoundarySpace(bool preserve)
        {
            preserveSpace = preserve;
        }

        public virtual bool IsPreserveBoundarySpace()
        {
            return preserveSpace;
        }

        public virtual void SetEmptyLeast(bool least)
        {
            defaultEmptyLeast = least;
        }

        public virtual bool IsEmptyLeast()
        {
            return defaultEmptyLeast;
        }

        public virtual void SetUpdatingEnabled(bool updating)
        {
            this.updating = updating;
        }

        public virtual bool IsUpdatingEnabled()
        {
            return updating;
        }
    }
}