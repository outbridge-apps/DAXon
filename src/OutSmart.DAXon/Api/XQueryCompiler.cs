////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Api
{
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(Action<OutSmart.DAXon.Api.IXmlProcessingError> reporter) {"
    //                + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //                + "    }"
    //})
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
            catch (URISyntaxException err)
            {
                throw new InvalidOperationException("Invalid base URI for query: " + staticQueryContext.BaseURI);
            }
        }

        public virtual void SetErrorReporter(IErrorReporter reporter)
        {
            staticQueryContext.ErrorReporter = reporter;
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
            switch (version)
            {
                case "3.1":
                    languageVersion = 31;
                    break;
                case "4.0":
                    languageVersion = 40;
                    break;
                default:
                    throw new ArgumentException("XQuery version must be 3.1 or 4.0 (not " + version + ")");
            }

            staticQueryContext.LanguageVersion = languageVersion;
        }

        public virtual string GetLanguageVersion()
        {
            return languageVersion == 40 ? "4.0" : "3.1";
        }

        public virtual void DeclareNamespace(string prefix, string uri)
        {
            staticQueryContext.DeclareNamespace(prefix, NamespaceUri.Of(uri));
        }

        public virtual UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy()
        {
            return staticQueryContext.GetUnprefixedElementMatchingPolicy();
        }

        public virtual void SetUnprefixedElementMatchingPolicy(UnprefixedElementMatchingPolicy unprefixedElementMatchingPolicy)
        {
            staticQueryContext.SetUnprefixedElementMatchingPolicy(unprefixedElementMatchingPolicy);
        }

        public virtual void DeclareDefaultCollation(string uri)
        {
            staticQueryContext.DeclareDefaultCollation(uri);
        }

        public virtual string GetDefaultCollationName()
        {
            return staticQueryContext.GetDefaultCollationName();
        }

        public virtual void SetRequiredContextItemType(ItemType type)
        {
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
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (XmlProcessingAbort e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        // IO-removal: CompileLibrary(string) deleted (uncalled; collided with CompileLibrary(string) source overload under string->string).

        public virtual void CompileLibrary(TextReader query)
        {
            try
            {
                staticQueryContext.CompileLibrary(query);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (IOException e)
            {
                throw new DAXonApiException(e);
            }
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (XmlProcessingAbort e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        public virtual void CompileLibrary(System.IO.Stream query)
        {
            try
            {
                staticQueryContext.CompileLibrary(query, encoding);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (IOException e)
            {
                throw new DAXonApiException(e);
            }
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (XmlProcessingAbort e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        public virtual XQueryExecutable Compile(string query)
        {
            try
            {
                return new XQueryExecutable(processor, staticQueryContext.CompileQuery(query));
            }
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e.GetXPathException());
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (XmlProcessingAbort e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        // IO-removal: Compile(string) deleted (uncalled; collided with Compile(string) source overload under string->string).

        public virtual XQueryExecutable Compile(System.IO.Stream query)
        {
            try
            {
                return new XQueryExecutable(processor, staticQueryContext.CompileQuery(query, encoding));
            }
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e.GetXPathException());
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (XmlProcessingAbort e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        public virtual XQueryExecutable Compile(TextReader query)
        {
            try
            {
                return new XQueryExecutable(processor, staticQueryContext.CompileQuery(query));
            }
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e.GetXPathException());
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (XmlProcessingAbort e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        public virtual void SetErrorList(IList<IXmlProcessingError> errorList)
        {
            SetErrorReporter(new DelegateErrorReporter(err => errorList.Add(err)));
        }
    }
}