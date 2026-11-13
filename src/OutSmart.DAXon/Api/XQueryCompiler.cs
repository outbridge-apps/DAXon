////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Api
{
    public class XQueryCompiler
    {
        private readonly Processor processor;
        private readonly StaticQueryContext staticQueryContext;
        private ItemType requiredContextItemType;
        private string encoding;
        private int languageVersion;

        public virtual string Encoding
        {
            get => encoding; set
            {
                this.encoding = value;
            }
        }

        public virtual StaticQueryContext UnderlyingStaticContext => staticQueryContext;
        public XQueryCompiler(Processor processor)
        {
            ArgumentCheck.NotNull(processor, nameof(processor));
            this.processor = processor;
            this.staticQueryContext = processor.UnderlyingConfiguration.NewStaticQueryContext();
        }

        public virtual Processor GetProcessor()
        {
            return processor;
        }

        public virtual void SetBaseURI(URI baseURI)
        {
            if (baseURI == null)
            {
                staticQueryContext.BaseURI = null;
            }
            else
            {
                if (!baseURI.IsAbsolute())
                {
                    throw new ArgumentException("Base URI must be an absolute URI: " + baseURI);
                }

                staticQueryContext.BaseURI = baseURI.ToString();
            }
        }

        public virtual URI GetBaseURI()
        {
            if (staticQueryContext.BaseURI == null)
            {
                return null;
            }

            try
            {
                return new URI(staticQueryContext.BaseURI);
            }
            catch (URISyntaxException)
            {
                throw new InvalidOperationException("Invalid base URI for query: " + staticQueryContext.BaseURI);
            }
        }

        public virtual void SetErrorReporter(IErrorReporter reporter)
        {
            // null clears it (HOSTING, "A null argument"): back to the default, which static errors are reported to.
            staticQueryContext.ErrorReporter = reporter ?? processor.UnderlyingConfiguration.MakeErrorReporter();
        }

        public virtual IErrorReporter GetErrorReporter()
        {
            return staticQueryContext.ErrorReporter;
        }

        public virtual void SetCompileWithTracing(bool option)
        {
            staticQueryContext.SetCompileWithTracing(option);
        }

        public virtual bool IsCompileWithTracing()
        {
            return staticQueryContext.IsCompileWithTracing();
        }

        public virtual void SetModuleURIResolver(IModuleURIResolver resolver)
        {
            staticQueryContext.ModuleURIResolver = resolver;
        }

        public virtual IModuleURIResolver GetModuleURIResolver()
        {
            return staticQueryContext.ModuleURIResolver;
        }

        public virtual void SetUpdatingEnabled(bool updating)
        {
            if (updating && !staticQueryContext.GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY))
            {
                throw new NotSupportedException("XQuery Update is not supported in this Saxon Configuration");
            }

            staticQueryContext.SetUpdatingEnabled(updating);
        }

        public virtual bool IsUpdatingEnabled()
        {
            return staticQueryContext.IsUpdatingEnabled();
        }

        public virtual void SetSchemaAware(bool schemaAware)
        {

            // We check this again more securely, but it's good to give the error as soon as possible
            if (schemaAware && !processor.UnderlyingConfiguration.IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY))
            {
                throw new NotSupportedException("Schema-awareness requires a Saxon-EE license");
            }

            staticQueryContext.SetSchemaAware(schemaAware);
        }

        public virtual bool IsSchemaAware()
        {
            return staticQueryContext.IsSchemaAware();
        }

        public virtual void SetStreaming(bool option)
        {
            staticQueryContext.SetStreaming(option);

            // We check this again more securely, but it's good to give the error as soon as possible
            if (option && !processor.UnderlyingConfiguration.IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY))
            {
                throw new NotSupportedException("Streaming requires a Saxon-EE license");
            }

            if (option)
            {
                SetRequiredContextItemType(ItemType.DOCUMENT_NODE);
            }
        }

        public virtual bool IsStreaming()
        {
            return staticQueryContext.IsStreaming();
        }

        public virtual void SetLanguageVersion(string version)
        {
            int level;
            switch (version)
            {
                case "3.1":
                    level = 31;
                    break;
                case "4.0":
                    level = 40;
                    break;
                default:
                    throw new ArgumentException("XQuery version must be 3.1 or 4.0 (not " + version + ")");
            }

            try
            {
                staticQueryContext.LanguageVersion = level;
            }
            catch (XPathException e)
            {
                // XQuery 4.0 is Saxon-PE's; the version stays as it was
                throw new NotSupportedException(e.Message, e);
            }

            languageVersion = level;
        }

        public virtual string GetLanguageVersion()
        {
            return languageVersion == 40 ? "4.0" : "3.1";
        }

        public virtual void DeclareNamespace(string prefix, string uri)
        {
            ArgumentCheck.NotNull(prefix, nameof(prefix));
            staticQueryContext.DeclareNamespace(prefix, NamespaceUri.Of(uri));
        }

        public virtual UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy()
        {
            return staticQueryContext.GetUnprefixedElementMatchingPolicy();
        }

        public virtual void SetUnprefixedElementMatchingPolicy(UnprefixedElementMatchingPolicy unprefixedElementMatchingPolicy)
        {
            ArgumentCheck.Defined(unprefixedElementMatchingPolicy, nameof(unprefixedElementMatchingPolicy));
            staticQueryContext.SetUnprefixedElementMatchingPolicy(unprefixedElementMatchingPolicy);
        }

        public virtual void DeclareDefaultCollation(string uri)
        {
            ArgumentCheck.NotNull(uri, nameof(uri));
            staticQueryContext.DeclareDefaultCollation(uri);
        }

        public virtual string GetDefaultCollationName()
        {
            return staticQueryContext.GetDefaultCollationName();
        }

        public virtual void SetRequiredContextItemType(ItemType type)
        {
            ArgumentCheck.NotNull(type, nameof(type));
            requiredContextItemType = type;
            staticQueryContext.RequiredContextItemType = type.UnderlyingItemType;
        }

        public virtual ItemType GetRequiredContextItemType()
        {
            return requiredContextItemType;
        }

        public virtual void SetFastCompilation(bool fast)
        {
            if (fast)
            {
                staticQueryContext.SetOptimizerOptions(new OptimizerOptions(0));
            }
            else
            {
                staticQueryContext.SetOptimizerOptions(GetProcessor().UnderlyingConfiguration.GetOptimizerOptions());
            }
        }

        public virtual bool IsFastCompilation()
        {
            return staticQueryContext.GetOptimizerOptions().GetOptions() == 0;
        }

        public virtual void CompileLibrary(string query)
        {
            try
            {
                staticQueryContext.CompileLibrary(query);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        // IO-removal: CompileLibrary(string) deleted (uncalled; collided with CompileLibrary(string) source overload under string->string).

        public virtual void CompileLibrary(TextReader query)
        {
            try
            {
                staticQueryContext.CompileLibrary(query);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
            catch (IOException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public virtual void CompileLibrary(System.IO.Stream query)
        {
            try
            {
                staticQueryContext.CompileLibrary(query, encoding);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
            catch (IOException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public virtual XQueryExecutable Compile(string query)
        {
            ArgumentCheck.NotNull(query, nameof(query));
            try
            {
                return new XQueryExecutable(processor, staticQueryContext.CompileQuery(query));
            }
            catch (Exception e) when (ApiErrors.IsInputError(e))
            {
                throw ApiErrors.Wrap(e, staticQueryContext.BaseURI ?? "urn:query");   // and an input that failed to read, as XsltCompiler
            }
        }

        // IO-removal: Compile(string) deleted (uncalled; collided with Compile(string) source overload under string->string).

        public virtual XQueryExecutable Compile(System.IO.Stream query)
        {
            ArgumentCheck.NotNull(query, nameof(query));
            try
            {
                // A query the host passes is input like a stylesheet: capped as XsltCompiler caps one (round B1 missed it).
                query = InputSizeLimit.Apply(query, InputSizeLimit.MaxFor(processor.UnderlyingConfiguration), staticQueryContext.BaseURI ?? "urn:query", "FODC0002");
                return new XQueryExecutable(processor, staticQueryContext.CompileQuery(query, encoding));
            }
            catch (Exception e) when (ApiErrors.IsInputError(e))
            {
                throw ApiErrors.Wrap(e, staticQueryContext.BaseURI ?? "urn:query");   // and an input that failed to read, as XsltCompiler
            }
        }

        public virtual XQueryExecutable Compile(TextReader query)
        {
            ArgumentCheck.NotNull(query, nameof(query));
            try
            {
                query = InputSizeLimit.Apply(query, InputSizeLimit.MaxFor(processor.UnderlyingConfiguration), staticQueryContext.BaseURI ?? "urn:query", "FODC0002");
                return new XQueryExecutable(processor, staticQueryContext.CompileQuery(query));
            }
            catch (Exception e) when (ApiErrors.IsInputError(e))
            {
                throw ApiErrors.Wrap(e, staticQueryContext.BaseURI ?? "urn:query");   // and an input that failed to read, as XsltCompiler
            }
        }

        public virtual void SetErrorList(IList<IXmlProcessingError> errorList)
        {
            SetErrorReporter(errorList == null ? null : new DelegateErrorReporter(err => errorList.Add(err)));
        }
    }
}