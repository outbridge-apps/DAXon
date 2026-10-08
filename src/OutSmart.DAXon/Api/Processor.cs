////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api.Push;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Api
{
    public class Processor : Configuration.IApiProvider
    {
        // Host-level resource limits, fixed at construction, inherited by everything created from
        // this Processor. Both are PER ENGINE CALL, not per host request - see the doc comments.
        public static readonly TimeSpan DefaultTransformTimeout = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The memory limit of an engine call, and its largest input, when <see cref="ProcessorOptions.MaxMemoryBytes"/>
        /// is not set: 500 MB.
        /// </summary>
        // Not const: a host compiled against this version reads the default of the engine it runs on.
        public static readonly long DefaultMaxMemoryBytes = 500L * 1024 * 1024;

        // 1.3.3's input cap and the default of its maxInputBytes parameters: from those constructors it means "not given".
        private const long LegacyInputCap = 150L * 1024 * 1024;

        /// <summary>1.3.3's default input cap. The largest input is <see cref="ProcessorOptions.MaxMemoryBytes"/> now.</summary>
        [Obsolete("The largest input is ProcessorOptions.MaxMemoryBytes, by default DefaultMaxMemoryBytes.")]
        public const long DefaultMaxInputBytes = LegacyInputCap;

        private const string ObsoleteLimits = "Use Processor(ProcessorOptions): TransformTimeout and MaxMemoryBytes.";
        private const string ObsoleteEdition = "licensedEdition has no effect (DAXon has one edition): use new Processor(), or Processor(ProcessorOptions) for limits.";

        /// <summary>
        /// Wall-clock limit for ONE engine call; exceeded calls abort with SXTO0001.
        /// TimeSpan.Zero (or negative) means no limit.
        /// <para>
        /// Per CALL, not per host request: a compile, a document build, a JSON parse and a
        /// transformation each claim the full budget for their own scope. A host that runs all
        /// four for one message therefore has a worst case of ~4x this value, and if it needs a
        /// bound on the whole request it must impose that itself. The alternative - one budget
        /// shared across the phases - would abort legitimate work on a cold cache, where the
        /// compile is the expensive part and the transform is not.
        /// </para>
        /// </summary>
        public TimeSpan TransformTimeout { get; }

        /// <summary>
        /// Largest input accepted, in bytes, on every entry point: <see cref="ProcessorOptions.MaxMemoryBytes"/>, or
        /// long.MaxValue when that is null.
        /// </summary>
        [Obsolete("The largest input is Options.MaxMemoryBytes.")]
        public long MaxInputBytes => Options.InputCap;

        /// <summary>
        /// The options this Processor runs with: its limits and what its stylesheets and queries may
        /// reach. It has taken them, so they can no longer change. A Processor built over a
        /// Configuration that already serves one reports (and applies) that one's options.
        /// </summary>
        public ProcessorOptions Options { get; }

        private Configuration config;

        // Saxon-base engine version (tracks the 12.9 base: SEF/fn:transform/xsl:product-version compat).
        public virtual string DAXonProductVersion => Core.Version.ProductVersion;

        // This distribution's own name/version (e.g. "OutSmart DAXon" / "1.0").
        public virtual string DistributionName => Core.Version.ProductName;
        public virtual string DistributionVersion => Core.Version.DistributionVersion;

        public virtual string DAXonEdition => config.EditionCode;

        public virtual string XmlVersion
        {
            get
            {
                if (config.XMLVersion == Configuration.XML10)
                {
                    return "1.0";
                }
                else
                {
                    return "1.1";
                }
            }
            set
            {
                switch (value)
                {
                    case "1.0":
                        config.XMLVersion = Configuration.XML10;
                        break;
                    case "1.1":
                        config.XMLVersion = Configuration.XML11;
                        break;
                    default:
                        throw new ArgumentException("XmlVersion");
                }
            }
        }

        public virtual Configuration UnderlyingConfiguration => config;

        // A constructor whose parameters are all optional is invisible to a binder that works
        // through reflection - Activator.CreateInstance and PowerShell's New-Object both look for
        // a zero-parameter one and do not fill defaults in. This is that constructor; C# overload
        // resolution prefers it for `new Processor()` because it substitutes no defaults.
        public Processor()
            : this(new ProcessorOptions())
        {
        }

        /// <summary>Kept for compatibility; use <see cref="Processor(ProcessorOptions)"/>.</summary>
        /// <param name="transformTimeout">Wall-clock limit per transformation; null for the
        /// default (1 minute), TimeSpan.Zero (or negative) for no limit.</param>
        /// <param name="maxInputBytes">The memory limit of a call, and its largest input: see
        /// <see cref="ProcessorOptions.MaxMemoryBytes"/>. 150 MB, the default, stands for the default
        /// limit; long.MaxValue for none.</param>
        [Obsolete(ObsoleteLimits)]
        public Processor(TimeSpan? transformTimeout = null, long maxInputBytes = LegacyInputCap)
            : this(LimitsOf(transformTimeout, maxInputBytes))
        {
        }

        /// <summary>
        /// A Processor with the given options, which it takes (they can no longer change): on a new engine core, or on
        /// the one <see cref="ProcessorOptions.Configuration"/> or <see cref="ProcessorOptions.ConfigurationFile"/> names.
        /// Every other constructor ends here.
        /// </summary>
        public Processor(ProcessorOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Configuration core;
            try
            {
                core = options.TakeCore();
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }

            config = core ?? Configuration.NewLicensedConfiguration();

            // A core already serving a Processor keeps that one's options, so a nested fn:transform or xsl:evaluate
            // cannot widen them.
            Options = config.ProcessorOptions ?? options;
            TransformTimeout = Options.TransformTimeout ?? DefaultTransformTimeout;

            // Make the Processor discoverable from its Configuration (so config.GetProcessor()
            // yields it, e.g. to read TransformTimeout when a query builds its Controller). Don't
            // clobber a processor already registered on the core.
            if (config.GetProcessor() == null)
            {
                config.SetProcessor(this);
            }
        }

        /// <summary>
        /// The s9api form, kept for compatibility. This port has one edition, so both values of
        /// <paramref name="licensedEdition"/> give the same configuration.
        /// </summary>
        [Obsolete(ObsoleteEdition)]
        public Processor(bool licensedEdition, TimeSpan? transformTimeout = null, long maxInputBytes = LegacyInputCap)
            : this(LimitsOf(transformTimeout, maxInputBytes))
        {
        }

        /// <summary>Kept for compatibility: a Processor on the given engine core.</summary>
        [Obsolete("Use Processor(ProcessorOptions) with ProcessorOptions.Configuration.")]
        public Processor(Configuration config)
            : this(new ProcessorOptions { Configuration = config ?? throw new ArgumentNullException(nameof(config)) })
        {
        }

        /// <summary>Kept for compatibility: a Processor on an engine core built from a Saxon configuration file.</summary>
        [Obsolete("Use Processor(ProcessorOptions) with ProcessorOptions.ConfigurationFile.")]
        public Processor(ResolvedResource source)
            : this(new ProcessorOptions { ConfigurationFile = source ?? throw new ArgumentNullException(nameof(source)) })
        {
        }

        // 1.3.3's limits as options: its input cap is the memory limit now, and its default - what every call that gave
        // none passes - the default memory limit.
        private static ProcessorOptions LimitsOf(TimeSpan? transformTimeout, long maxInputBytes)
        {
            if (maxInputBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInputBytes));
            }

            return new ProcessorOptions
            {
                TransformTimeout = transformTimeout,
                MaxMemoryBytes = maxInputBytes == LegacyInputCap ? DefaultMaxMemoryBytes
                    : maxInputBytes == long.MaxValue ? (long?)null : maxInputBytes,
            };
        }

        public virtual DocumentBuilder NewDocumentBuilder()
        {
            return new DocumentBuilder(config);
        }

        public virtual JsonBuilder NewJsonBuilder()
        {
            return new JsonBuilder(UnderlyingConfiguration);
        }

        public virtual XPathCompiler NewXPathCompiler()
        {
            return new XPathCompiler(this);
        }

        public virtual XsltCompiler NewXsltCompiler()
        {
            return new XsltCompiler(this);
        }

        public virtual XQueryCompiler NewXQueryCompiler()
        {
            return new XQueryCompiler(this);
        }

        public virtual Serializer NewSerializer()
        {
            return new Serializer(this);
        }

        public virtual Serializer NewSerializer(System.IO.Stream stream)
        {
            Serializer s = new Serializer(this);
            s.SetOutputStream(stream);
            return s;
        }

        public virtual Serializer NewSerializer(TextWriter writer)
        {
            Serializer s = new Serializer(this);
            s.SetOutputWriter(writer);
            return s;
        }

        public virtual Serializer NewSerializer(string file)
        {
            Serializer s = new Serializer(this);
            s.SetOutputFile(file);
            return s;
        }

        public virtual IPush NewPush(IDestination destination)
        {
            PipelineConfiguration pipe = UnderlyingConfiguration.MakePipelineConfiguration();
            SerializationProperties props = new SerializationProperties();
            try
            {
                return new PushToReceiver(destination.GetReceiver(pipe, props), destination);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public virtual void RegisterExtensionFunction(IExtensionFunction function)
        {
            ExtensionFunctionDefinitionWrapper wrapper = new ExtensionFunctionDefinitionWrapper(function);
            RegisterExtensionFunction(wrapper);
        }

        public virtual void RegisterExtensionFunction(ExtensionFunctionDefinition function)
        {
            try
            {
                config.RegisterExtensionFunction(function);
            }
            catch (Exception err)
            {
                throw new ArgumentException(err.Message, err);
            }
        }

        public virtual SchemaManager GetSchemaManager()
        {
            return null;   // a schema manager is Saxon-EE's; this port is HE
        }

        public virtual bool IsSchemaAware()
        {
            return config.IsLicensedFeature(Configuration.LicenseFeature.SCHEMA_VALIDATION);
        }

        public virtual void SetConfigurationProperty(string name, object value)
        {
            if (name.Equals(FeatureKeys.CONFIGURATION))
            {
                config = (Configuration)(object)value;
            }
            else
            {
                config.SetConfigurationProperty(name, value);
            }
        }

        public virtual object GetConfigurationProperty(string name)
        {
            return config.GetConfigurationProperty(name);
        }

        public virtual void SetConfigurationProperty<T>(Feature<T> feature, T value)
        {
            if ((object)feature == (object)Feature<Configuration>.CONFIGURATION)
            {
                config = (Configuration)(object)value;
            }
            else
            {
                config.SetConfigurationProperty(feature, value);
            }
        }

        public virtual T GetConfigurationProperty<T>(Feature<T> feature)
        {
            return config.GetConfigurationProperty(feature);
        }

        public virtual void DeclareCollation(string uri, IComparer<string> collation)
        {
            if (uri.Equals(NamespaceConstant.CODEPOINT_COLLATION_URI))
            {
                throw new ArgumentException("Cannot redeclare the Unicode codepoint collation URI");
            }

            if (uri.Equals(NamespaceConstant.HTML5_CASE_BLIND_COLLATION_URI))
            {
                throw new ArgumentException("Cannot redeclare the HTML5 caseblind collation URI");
            }

            IStringCollator saxonCollation = MakeStringCollator(uri, collation);
            config.RegisterCollation(uri, saxonCollation);
        }

        private static IStringCollator MakeStringCollator(string uri, IComparer<string> collation)
        {
            if (collation is RuleBasedCollator)
            {
                return new RuleBasedSubstringMatcher();
            }
            else
            {
                return new SimpleCollation(uri, collation);
            }
        }

        public virtual void RegisterCollection(string collectionURI, IResourceCollection collection)
        {
            config.RegisterCollection(collectionURI, collection);
        }

        // XML catalogs are not read: until 1.4 this call did nothing at all. What a catalog would map - a DTD, an
        // entity, a document, a stylesheet module - a resource resolver serves (Configuration.SetResourceResolver).
        public virtual void SetCatalogFiles(params string[] fileNames)
        {
            throw new NotSupportedException("XML catalogs are not supported: serve the resources they would map from a resource resolver (Configuration.SetResourceResolver)");
        }

        public virtual void WriteXdmValue(XdmValue value, IDestination destination)
        {
            if (value == null)
                throw new NullReferenceException();
            if (destination == null)
                throw new NullReferenceException();
            bool closed = false;
            try
            {
                if (destination is Serializer)
                {
                    ((Serializer)destination).SerializeXdmValue(value);
                    closed = true;
                }
                else
                {
                    IReceiver @out = destination.GetReceiver(config.MakePipelineConfiguration(), config.ObtainDefaultSerializationProperties());
                    // using = abort-path release (a failed write frees the destination's file); Close inside = success path.
                    using (ComplexContentOutputter tree = new ComplexContentOutputter(@out))
                    {
                        tree.Open();
                        tree.StartDocument(ReceiverOption.NONE);
                        foreach (XdmItem item in value)
                        {
                            tree.Append(item.UnderlyingValue, Loc.NONE, ReceiverOption.ALL_NAMESPACES);
                        }

                        tree.EndDocument();
                        tree.Close();
                    }

                    destination.CloseAndNotify();
                    closed = true;
                }
            }
            catch (XPathException err)
            {
                throw new DAXonApiException(err);
            }
            catch (RecursionDepthError err)
            {
                throw new DAXonApiException(err.ToXPathException());
            }
            finally
            {
                if (!closed)
                {
                    DestinationHelper.ReleaseUnclosed(destination);
                }
            }
        }

        private sealed class ExtensionFunctionDefinitionWrapper : ExtensionFunctionDefinition
        {
            private readonly IExtensionFunction function;

            public override StructuredQName FunctionQName => function.Name.GetStructuredQName();

            public override int MinimumNumberOfArguments => function.ArgumentTypes.Length;

            public override int MaximumNumberOfArguments => function.ArgumentTypes.Length;

            public override Values.SequenceType[] ArgumentTypes
            {
                get
                {
                    SequenceType[] declaredArgs = function.ArgumentTypes;
                    Values.SequenceType[] types = new Values.SequenceType[declaredArgs.Length];
                    for (int i = 0; i < declaredArgs.Length; i++)
                    {
                        types[i] = Values.SequenceType.MakeSequenceType(declaredArgs[i].GetItemType().UnderlyingItemType, declaredArgs[i].GetOccurrenceIndicator().GetCardinality());
                    }

                    return types;
                }
            }
            public ExtensionFunctionDefinitionWrapper(IExtensionFunction function)
            {
                this.function = function;
            }

            public override Values.SequenceType GetResultType(Values.SequenceType[] suppliedArgumentTypes)
            {
                SequenceType declaredResult = function.ResultType;
                return Values.SequenceType.MakeSequenceType(declaredResult.GetItemType().UnderlyingItemType, declaredResult.GetOccurrenceIndicator().GetCardinality());
            }

            public override bool TrustResultType()
            {
                return false;
            }

            public override bool DependsOnFocus()
            {
                return false;
            }

            public override bool HasSideEffects()
            {
                return false;
            }

            public override ExtensionFunctionCall MakeCallExpression()
            {
                return new AnonymousExtensionFunctionCall(this);
            }

            private sealed class AnonymousExtensionFunctionCall : ExtensionFunctionCall
            {

                private readonly ExtensionFunctionDefinitionWrapper parent;
                public AnonymousExtensionFunctionCall(ExtensionFunctionDefinitionWrapper parent)
                {
                    this.parent = parent;
                }
                public override ISequence Call(IXPathContext context, ISequence[] arguments)
                {
                    XdmValue[] args = new XdmValue[arguments.Length];
                    for (int i = 0; i < args.Length; i++)
                    {
                        IGroundedValue val = arguments[i].Materialize();
                        args[i] = XdmValue.Wrap(val);
                    }

                    try
                    {
                        // null is how a .NET method says it has nothing: the empty sequence
                        XdmValue result = parent.function.Call(args);
                        return result?.UnderlyingValue as ISequence ?? EmptySequence.GetInstance();
                    }
                    catch (DAXonApiException e)
                    {
                        // The host's error as it built it, its code for xsl:try included; otherwise with the host's
                        // exception as the cause (Java: new XPathException(e)), not its message alone.
                        if (e.InnerException is XPathException xe)
                        {
                            throw xe;
                        }

                        throw new XPathException(e.Message, e);
                    }
                    catch (Exception e) when (HostDefects.Mark(e))
                    {
                        // The host's own defect: on through the run as thrown, past xsl:try (HOSTING.md).
                        throw;
                    }
                }
            }
        }
    }
}