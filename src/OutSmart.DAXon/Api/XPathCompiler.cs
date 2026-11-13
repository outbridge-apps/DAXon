////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Internal.Net;
using System;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Caching;
namespace OutSmart.DAXon.Api
{
    public class XPathCompiler
    {
        private readonly Processor processor;
        private readonly XPathEvaluator evaluator;
        private readonly IndependentContext env;
        private ItemType requiredContextItemType;
        // Thread-safe when caching is on: an XPathCompiler may be shared and Compile() called
        // concurrently. (Was Expr.Sort.LFUCache(concurrent:true), which the port silently backed with
        // a plain Dictionary -- not actually thread-safe; Internal.Caching.ClockCache is.)
        private ClockCache<string, XPathExecutable> cache;

        public virtual string LanguageVersion
        {
            get
            {
                if (env.GetXPathVersion() == 20)
                {
                    return "2.0";
                }
                else if (env.GetXPathVersion() == 30)
                {
                    return "3.0";
                }
                else if (env.GetXPathVersion() == 31)
                {
                    return "3.1";
                }
                else if (env.GetXPathVersion() == 40)
                {
                    return "4.0";
                }
                else
                {
                    throw new InvalidOperationException("Unknown XPath version " + env.GetXPathVersion());
                }
            }
            set
            {
                if (cache != null)
                {
                    cache.Clear();
                }

                int version;
                if (value == "1.0")
                {
                    version = 20;
                    env.SetBackwardsCompatibilityMode(true);
                }
                else if (value == "2.0")
                {
                    version = 20;
                }
                else if (value == "3.0" || value == "3.05")
                {
                    version = 30;
                }
                else if (value == "3.1")
                {
                    version = 31;
                }
                else if (value == "4.0")
                {
                    version = 40;
                }
                else
                {
                    throw new ArgumentException("XPath version");
                }

                try
                {
                    env.SetXPathLanguageLevel(version);
                }
                catch (XPathException e)
                {
                    // XPath 4.0 is Saxon-PE's: refused as SetSchemaAware refuses schema awareness
                    throw new NotSupportedException(e.Message, e);
                }

                env.SetDefaultFunctionLibrary(version);
            }
        }

        public virtual URI BaseURI
        {
            get
            {
                try
                {
                    return new URI(env.StaticBaseURI);
                }
                catch (URISyntaxException)
                {
                    throw new InvalidOperationException("Invalid base URI for XPath: " + env.StaticBaseURI);
                }
            }
            set
            {
                if (cache != null)
                {
                    cache.Clear();
                }

                if (value == null)
                {
                    env.SetBaseURI(null);
                }
                else
                {
                    if (!value.IsAbsolute())
                    {
                        throw new ArgumentException("Supplied base URI must be absolute");
                    }

                    env.SetBaseURI(value.ToString());
                }
            }
        }

        public virtual ItemType RequiredContextItemType
        {
            get => requiredContextItemType; set
            {
                ArgumentCheck.NotNull(value, nameof(value));
                requiredContextItemType = value;
                Changed();
                env.SetRequiredContextItemType(value.UnderlyingItemType);
            }
        }

        public virtual IStaticContext UnderlyingStaticContext => env;
        public XPathCompiler(Processor processor)
        {
            ArgumentCheck.NotNull(processor, nameof(processor));
            this.processor = processor;
            this.evaluator = new XPathEvaluator(processor.UnderlyingConfiguration);
            env = (IndependentContext)this.evaluator.StaticContext;
        }

        public virtual Processor GetProcessor()
        {
            return processor;
        }

        public virtual void SetBackwardsCompatible(bool option)
        {
            if (cache != null)
            {
                cache.Clear();
            }

            env.SetBackwardsCompatibilityMode(option);
        }

        public virtual bool IsBackwardsCompatible()
        {
            return env.IsInBackwardsCompatibleMode();
        }

        public virtual void SetSchemaAware(bool schemaAware)
        {
            if (schemaAware && !processor.UnderlyingConfiguration.IsLicensedFeature(Configuration.LicenseFeature.SCHEMA_VALIDATION))
            {
                throw new NotSupportedException("Schema processing requires a licensed Saxon-EE configuration");
            }

            Changed();
            env.SetSchemaAware(schemaAware);
        }

        public virtual bool IsSchemaAware()
        {
            return env.GetPackageData().IsSchemaAware();
        }

        public virtual void SetUnprefixedElementMatchingPolicy(UnprefixedElementMatchingPolicy policy)
        {
            ArgumentCheck.Defined(policy, nameof(policy));
            Changed();
            env.SetUnprefixedElementMatchingPolicy(policy);
        }

        public virtual UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy()
        {
            return env.GetUnprefixedElementMatchingPolicy();
        }

        public virtual void SetWarningHandler(IErrorReporter reporter)
        {
            env.SetWarningHandler((message, code, location) =>
            {
                reporter.Report(new XmlProcessingIncident(message, code, location).AsWarning());
            });
        }

        public virtual void DeclareNamespace(string prefix, string uri)
        {
            ArgumentCheck.NotNull(prefix, nameof(prefix));
            if (cache != null)
            {
                cache.Clear();
            }

            env.DeclareNamespace(prefix, NamespaceUri.Of(uri));
        }

        public virtual void ImportSchemaNamespace(string uri)
        {
            if (cache != null)
            {
                cache.Clear();
            }

            env.GetImportedSchemaNamespaces().Add(NamespaceUri.Of(uri));
            env.SetSchemaAware(true);
        }

        public virtual void SetAllowUndeclaredVariables(bool allow)
        {
            if (cache != null)
            {
                cache.Clear();
            }

            env.SetAllowUndeclaredVariables(allow);
        }

        public virtual bool IsAllowUndeclaredVariables()
        {
            return env.IsAllowUndeclaredVariables();
        }

        public virtual void DeclareVariable(QName qname)
        {
            ArgumentCheck.NotNull(qname, nameof(qname));
            if (cache != null)
            {
                cache.Clear();
            }

            env.DeclareVariable(qname.GetNamespaceUri(), qname.LocalName);
        }

        public virtual void DeclareVariable(QName qname, ItemType itemType, OccurrenceIndicator occurrences)
        {
            ArgumentCheck.NotNull(qname, nameof(qname));
            ArgumentCheck.NotNull(itemType, nameof(itemType));
            if (cache != null)
            {
                cache.Clear();
            }

            XPathVariable var = env.DeclareVariable(qname.GetNamespaceUri(), qname.LocalName);
            var.SetRequiredType(Values.SequenceType.MakeSequenceType(itemType.UnderlyingItemType, occurrences.GetCardinality()));
        }

        public virtual void AddXsltFunctionLibrary(XsltPackage libraryPackage)
        {
            ArgumentCheck.NotNull(libraryPackage, nameof(libraryPackage));
            Changed();
            ((FunctionLibraryList)env.GetFunctionLibrary()).AddFunctionLibrary(libraryPackage.UnderlyingPreparedPackage.PublicFunctions);
        }

        public virtual void DeclareDefaultCollation(string uri)
        {
            IStringCollator c;
            try
            {
                c = GetProcessor().UnderlyingConfiguration.GetCollation(uri);
            }
            catch (XPathException)
            {
                c = null;
            }

            if (c == null)
            {
                throw new InvalidOperationException("Unknown collation " + uri);
            }

            Changed();
            env.SetDefaultCollationName(uri);
        }

        // A setting that changes what an expression compiles to empties the cache: these kept serving expressions compiled
        // before the change (another default collation, decimal format, context item type, function library).
        private void Changed()
        {
            cache?.Clear();
        }

        public virtual void SetCaching(bool caching)
        {
            if (caching)
            {
                if (cache == null)
                {
                    cache = new ClockCache<string, XPathExecutable>(100);
                }
            }
            else
            {
                cache = null;
            }
        }

        public virtual bool IsCaching()
        {
            return cache != null;
        }

        public virtual void SetFastCompilation(bool fast)
        {
            if (fast)
            {
                env.SetOptimizerOptions(new OptimizerOptions(0));
            }
            else
            {
                env.SetOptimizerOptions(GetProcessor().UnderlyingConfiguration.GetOptimizerOptions());
            }
        }

        public virtual bool IsFastCompilation()
        {
            return env.GetOptimizerOptions().GetOptions() == 0;
        }

        public virtual XPathExecutable Compile(string source)
        {
            ArgumentCheck.NotNull(source, nameof(source));
            if (cache != null)
            {
                // GetOrAdd runs InternalCompile at most once per source (factory outside the lock);
                // InternalCompile never returns null -- it returns an executable or throws, and a
                // throw leaves nothing cached, exactly as the former get/compile/put did.
                return cache.GetOrAdd(source, InternalCompile);
            }
            else
            {
                return InternalCompile(source);
            }
        }

        private XPathExecutable InternalCompile(string source)
        {
            try
            {
                env.GetDecimalFormatManager().CheckConsistency();
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }

            string module = BaseURI == null ? null : BaseURI.ToString();
            env.SetContainingLocation(new Loc(module, 1, 1));
            XPathEvaluator eval = evaluator;
            IndependentContext ic = env;
            if (ic.IsAllowUndeclaredVariables())
            {

                // self-declaring variables modify the static context. The XPathCompiler must not change state
                // as the result of compiling an expression, so we need to copy the static context.
                eval = new XPathEvaluator(processor.UnderlyingConfiguration);
                ic = new IndependentContext(env);
                eval.StaticContext = ic;
                foreach (XPathVariable var in env.ExternalVariables)
                {
                    XPathVariable var2 = ic.DeclareVariable(var.GetVariableQName());
                    var2.SetRequiredType(var.GetRequiredType());
                }
            }

            // Compile under the Processor's deadline: constant folding of hostile expression text is
            // otherwise unbounded work before any run-time deadline exists (see ArmThreadDeadline).
            using Controller.ThreadLimits deadline = Controller.ArmScope(processor.UnderlyingConfiguration, "Compilation");
            try
            {
                XPathExpression cexp = eval.CreateExpression(source);
                return new XPathExecutable(cexp, processor, ic);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public virtual XdmValue Evaluate(string expression, XdmItem contextItem)
        {
            return Selector(expression, contextItem).Evaluate();
        }

        public virtual XdmItem EvaluateSingle(string expression, XdmItem contextItem)
        {
            return Selector(expression, contextItem).EvaluateSingle();
        }

        // A one-off compile, without the optimizer unless the compiler caches. On this thread only: switching the
        // compiler's option and restoring what it read left it switched for good when two threads evaluated at once.
        private XPathSelector Selector(string expression, XdmItem contextItem)
        {
            ArgumentCheck.NotNull(expression, nameof(expression));
            XPathSelector selector;
            if (IsCaching())
            {
                selector = Compile(expression).Load();
            }
            else
            {
                IndependentContext previous = env.BeginUnoptimized();
                try
                {
                    selector = Compile(expression).Load();
                }
                finally
                {
                    IndependentContext.EndUnoptimized(previous);
                }
            }

            if (contextItem != null)
            {
                selector.SetContextItem(contextItem);
            }

            return selector;
        }

        public virtual XPathExecutable CompilePattern(string source)
        {
            ArgumentCheck.NotNull(source, nameof(source));
            try
            {
                env.GetDecimalFormatManager().CheckConsistency();
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }

            // Compile under the Processor's deadline (see InternalCompile).
            using Controller.ThreadLimits deadline = Controller.ArmScope(processor.UnderlyingConfiguration, "Compilation");
            try
            {
                string @base = BaseURI == null ? null : BaseURI.ToString();
                env.SetContainingLocation(new Loc(@base, 1, 1));
                XPathExpression cexp = evaluator.CreatePattern(source);
                return new XPathExecutable(cexp, processor, env);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public virtual void SetDecimalFormatProperty(string property, string value)
        {
            ArgumentCheck.NotNull(property, nameof(property));
            ArgumentCheck.NotNull(value, nameof(value));
            DecimalFormatManager dfm = env.GetDecimalFormatManager();
            if (dfm == null)
            {
                dfm = new DecimalFormatManager(HostLanguage.XPATH, env.GetXPathVersion());
                env.SetDecimalFormatManager(dfm);
            }

            SetDecimalFormatProperty(dfm.DefaultDecimalFormat, property, value);
        }

        public virtual void SetDecimalFormatProperty(QName format, string property, string value)
        {
            ArgumentCheck.NotNull(format, nameof(format));
            ArgumentCheck.NotNull(property, nameof(property));
            ArgumentCheck.NotNull(value, nameof(value));
            DecimalFormatManager dfm = env.GetDecimalFormatManager();
            if (dfm == null)
            {
                dfm = new DecimalFormatManager(HostLanguage.XPATH, env.GetXPathVersion());
                env.SetDecimalFormatManager(dfm);
            }

            DecimalSymbols symbols = dfm.ObtainNamedDecimalFormat(format.GetStructuredQName());
            Changed();
            SetDecimalFormatProperty(symbols, property, value);
        }

        private static void SetDecimalFormatProperty(DecimalSymbols symbols, string property, string value)
        {
            try
            {
                switch (property)
                {
                    case "decimal-separator":
                        symbols.SetDecimalSeparator(value);
                        break;
                    case "grouping-separator":
                        symbols.SetGroupingSeparator(value);
                        break;
                    case "exponent-separator":
                        symbols.SetExponentSeparator(value);
                        break;
                    case "infinity":
                        symbols.Infinity = value;
                        break;
                    case "NaN":
                        symbols.NaN = value;
                        break;
                    case "minus-sign":
                        symbols.SetMinusSign(value);
                        break;
                    case "percent":
                        symbols.SetPercent(value);
                        break;
                    case "per-mille":
                        symbols.SetPerMille(value);
                        break;
                    case "zero-digit":
                        symbols.SetZeroDigit(value);
                        break;
                    case "digit":
                        symbols.SetDigit(value);
                        break;
                    case "pattern-separator":
                        symbols.SetPatternSeparator(value);
                        break;
                    default:
                        throw new ArgumentException("Unknown decimal format attribute " + property);
                }
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }
    }
}