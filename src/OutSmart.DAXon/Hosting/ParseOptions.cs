////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Collections.Trie;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
namespace OutSmart.DAXon.Lib
{
    public class ParseOptions
    {
        private readonly object syncLock = new object();

        private readonly IImmutableMap<Key, object> properties;
        private IList<Key> cacheKeys;
        private IList<object> cacheValues;
        private IList<ParseOptions> cacheResults;

        public virtual IList<IFilterFactory> Filters => (IList<IFilterFactory>)GetProperty(Key.FILTERS);

        public virtual ISpaceStrippingRule SpaceStrippingRule => (ISpaceStrippingRule)GetProperty(Key.SPACE_STRIPPING_RULE);

        public virtual Dictionary<string, bool> ParserFeatures
        {
            get
            {
                Dictionary<string, bool> parserFeatures = (Dictionary<string, bool>)GetProperty(Key.PARSER_FEATURES);
                if (parserFeatures == null)
                {
                    return new Dictionary<string, bool>();
                }
                else
                {
                    return parserFeatures;
                }
            }
        }

        public virtual Dictionary<string, object> ParserProperties
        {
            get
            {
                Dictionary<string, object> parserProperties = (Dictionary<string, object>)GetProperty(Key.PARSER_PROPERTIES);
                if (parserProperties == null)
                {
                    return new Dictionary<string, object>();
                }
                else
                {
                    return parserProperties;
                }
            }
        }

        public virtual TreeModel Model
        {
            get
            {
                TreeModel treeModel = (TreeModel)GetProperty(Key.MODEL);
                return treeModel == null ? TreeModel.TINY_TREE : treeModel;
            }
        }

        public virtual StructuredQName TopLevelElement => (StructuredQName)GetProperty(Key.TOP_LEVEL_ELEMENT);

        public virtual ISchemaType TopLevelType => (ISchemaType)GetProperty(Key.TOP_LEVEL_TYPE);

        public virtual int ValidationErrorLimit => GetIntegerProperty(Key.VALIDATION_ERROR_LIMIT, int.MaxValue);

        public virtual int DTDValidationMode => GetIntegerProperty(Key.DTD_VALIDATION, Validation.SKIP);

        public virtual IValidationStatisticsRecipient ValidationStatisticsRecipient => (IValidationStatisticsRecipient)GetProperty(Key.VALIDATION_STATISTICS_RECIPIENT);

        public virtual string EntityResolverClass => (string)GetProperty(Key.ENTITY_RESOLVER);

        public virtual IInvalidityHandler InvalidityHandler => (IInvalidityHandler)GetProperty(Key.INVALIDITY_HANDLER);

        public virtual HashSet<Accumulator> ApplicableAccumulators => (HashSet<Accumulator>)GetProperty(Key.APPLICABLE_ACCUMULATORS);
        /// <summary>
        /// Create a ParseOptions object with default options set
        /// </summary>
        public ParseOptions()
        {
            properties = Init();
        }

        private ParseOptions(IImmutableMap<Key, object> properties)
        {
            this.properties = properties;
        }

        private IImmutableMap<Key, object> Init()
        {
            return ImmutableHashTrieMap<Key, object>.Empty();
        }

        private ParseOptions SearchCache(Key key, object value)
        {
            lock (syncLock)
            {
                if (cacheKeys == null)
                {
                    return null;
                }

                for (int i = 0; i < cacheKeys.Count; i++)
                {
                    if (cacheKeys[i] == key && cacheValues[i] == value)
                    {
                        return cacheResults[i];
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Add a new entry to the cache
        /// </summary>
        private void AddToCache(Key key, object value, ParseOptions result)
        {
            lock (syncLock)
            {
                if (cacheKeys == null)
                {
                    cacheKeys = new List<Key>(10);
                    cacheValues = new List<object>(10);
                    cacheResults = new List<ParseOptions>(10);
                }
                else if (cacheKeys.Count >= 10)
                {

                    // rough and ready - empty the cache and start again
                    cacheKeys.Clear();
                    cacheValues.Clear();
                    cacheResults.Clear();
                }

                cacheKeys.Add(key);
                cacheValues.Add(value);
                cacheResults.Add(result);
            }
        }

        private ParseOptions WithProperty(Key key, object value)
        {

            if (value == properties[key])
            {
                return this;
            }


            ParseOptions result = SearchCache(key, value);
            if (result != null)
            {

                return result;
            }


            if (value == null)
            {
                result = new ParseOptions(properties.Remove(key));
            }
            else
            {
                result = new ParseOptions(properties.Put(key, value));
            }

            // A reporter is made new for each use, so its entry never hit; and it keeps the last error it reported, with the
            // context of the failed run, so ten entries on the configuration's options held the documents of failed runs.
            if (key != Key.ERROR_REPORTER)
            {
                AddToCache(key, value, result);
            }

            return result;
        }

        private object GetProperty(Key key)
        {
            return properties[key];
        }

        private bool HasProperty(Key key)
        {
            return properties[key] != null;
        }

        private int GetIntegerProperty(Key key, int defaultValue)
        {
            object value = properties[key];
            if (value == null)
            {
                return defaultValue;
            }
            else
            {
                return (int)value;
            }
        }

        private bool GetBooleanProperty(Key key, bool defaultValue)
        {
            object value = properties[key];
            if (value == null)
            {
                return defaultValue;
            }
            else
            {
                return (bool)value;
            }
        }

        public virtual ParseOptions Merge(ParseOptions other)
        {
            ParseOptions result = this;
            if (other.DTDValidationMode != Validation.DEFAULT)
            {
                result = result.WithDTDValidationMode(other.DTDValidationMode);
            }

            if (other.GetSchemaValidationMode() != Validation.DEFAULT)
            {
                result = result.WithSchemaValidationMode(other.GetSchemaValidationMode());
            }

            result = result.WithPropertyIfNotNull(Key.INVALIDITY_HANDLER, other.InvalidityHandler);
            result = result.WithPropertyIfNotNull(Key.TOP_LEVEL_ELEMENT, other.TopLevelElement);
            result = result.WithPropertyIfNotNull(Key.TOP_LEVEL_TYPE, other.TopLevelType);
            result = result.WithPropertyIfNotNull(Key.SPACE_STRIPPING_RULE, other.SpaceStrippingRule);
            result = result.WithPropertyIfNotNull(Key.TREE_MODEL, other.GetTreeModel());
            if (other.HasProperty(Key.LINE_NUMBERING))
            {
                result = result.WithLineNumbering(other.IsLineNumbering());
            }

            if (other.IsPleaseCloseAfterUse())
            {
                result = result.WithPleaseCloseAfterUse(true);
            }

            if (other.Filters != null)
            {
                foreach (IFilterFactory ff in other.Filters)
                {
                    result = result.WithFilter(ff);
                }
            }

            if (other.ParserFeatures != null)
            {
                foreach (KeyValuePair<string, bool> entry in other.ParserFeatures)
                {
                    result = result.WithParserFeature(entry.Key, entry.Value);
                }
            }

            if (other.ParserProperties != null)
            {
                foreach (KeyValuePair<string, object> entry in other.ParserProperties)
                {
                    result = result.WithParserProperty(entry.Key, entry.Value);
                }
            }

            if (!other.IsExpandAttributeDefaults())
            {

                // expand defaults unless the other options says don't
                result = result.WithExpandAttributeDefaults(false);
            }

            if (!other.IsUseXsiSchemaLocation())
            {
                result = result.WithUseXsiSchemaLocation(false);
            }

            if (other.IsAddCommentsAfterValidationErrors())
            {

                // add comments if either set of options requests it
                result = result.WithUseXsiSchemaLocation(true);
            }

            result = result.WithValidationErrorLimit(Math.Min(this.ValidationErrorLimit, other.ValidationErrorLimit));
            result = result.WithPropertyIfNotNull(Key.ERROR_REPORTER, other.GetErrorReporter());
            return result;
        }

        private ParseOptions WithPropertyIfNotNull(Key key, object value)
        {
            if (value != null)
            {
                return WithProperty(key, value);
            }

            return this;
        }

        public virtual ParseOptions ApplyDefaults(Configuration config)
        {
            ParseOptions result = this;
            if (DTDValidationMode == Validation.DEFAULT)
            {
                result = result.WithDTDValidationMode(config.IsValidation() ? Validation.STRICT : Validation.SKIP);
            }

            if (GetSchemaValidationMode() == Validation.DEFAULT)
            {
                result = result.WithSchemaValidationMode(config.SchemaValidationMode);
            }

            if (Model == null)
            {
                result = result.WithModel(TreeModel.GetTreeModel(config.GetTreeModel()));
            }

            if (SpaceStrippingRule == null)
            {
                result = result.WithSpaceStrippingRule(config.GetParseOptions().SpaceStrippingRule);
            }

            if (GetProperty(Key.LINE_NUMBERING) == null)
            {
                result = result.WithProperty(Key.LINE_NUMBERING, config.IsLineNumbering());
            }

            if (GetProperty(Key.ERROR_REPORTER) == null)
            {
                result = result.WithErrorReporter(config.MakeErrorReporter());
            }

            return result;
        }

        public virtual ParseOptions WithFilter(IFilterFactory filterFactory)
        {
            IList<IFilterFactory> list = Filters;
            if (list == null)
            {
                list = new List<IFilterFactory>(2);
            }
            else
            {
                list = new List<IFilterFactory>(list); // to keep it immutable; it's not going to be a long list
            }

            list.Add(filterFactory);
            return WithProperty(Key.FILTERS, list);
        }

        public virtual ParseOptions WithSpaceStrippingRule(ISpaceStrippingRule rule)
        {
            return WithProperty(Key.SPACE_STRIPPING_RULE, rule);
        }

        public virtual ParseOptions WithTreeModel(int model)
        {
            return WithProperty(Key.TREE_MODEL, model);
        }

        public virtual ParseOptions WithParserFeature(string uri, bool value)
        {
            Dictionary<string, bool> parserFeatures = (Dictionary<string, bool>)GetProperty(Key.PARSER_FEATURES);
            Dictionary<string, bool> parserFeatures2;
            if (parserFeatures == null)
            {
                parserFeatures2 = new Dictionary<string, bool>(4);
            }
            else
            {
                parserFeatures2 = new Dictionary<string, bool>(parserFeatures);
            }

            // Presence must be tracked explicitly: Put returns default(bool) for an absent key,
            // so the Java "old != null && old == value" no-change test silently dropped the
            // update when an absent feature was being set to false.
            bool had = parserFeatures2.TryGetValue(uri, out bool prev);
            parserFeatures2[uri] = value;
            return had && prev == value ? this : WithProperty(Key.PARSER_FEATURES, parserFeatures2);
        }

        public virtual ParseOptions WithParserProperty(string uri, object value)
        {
            // PARSER_PROPERTIES, not PARSER_FEATURES: the copy-paste key made this cast throw
            // whenever features were set, and discarded any existing properties otherwise.
            Dictionary<string, object> parserProperties = (Dictionary<string, object>)GetProperty(Key.PARSER_PROPERTIES);
            Dictionary<string, object> parserProperties2;
            if (parserProperties == null)
            {
                parserProperties2 = new Dictionary<string, object>(4);
            }
            else
            {
                parserProperties2 = new Dictionary<string, object>(parserProperties);
            }

            object old;
            if (value != null)
            {
                old = parserProperties2.PutAndGetPrevious(uri, value);
            }
            else
            {
                old = parserProperties2.RemoveAndGet(uri);
            }

            return (old != null && old.Equals(value)) ? this : WithProperty(Key.PARSER_PROPERTIES, parserProperties2);
        }

        public virtual bool HasParserFeature(string uri)
        {
            Dictionary<string, bool> parserFeatures = (Dictionary<string, bool>)GetProperty(Key.PARSER_FEATURES);
            if (parserFeatures == null)
            {
                return false;
            }

            return parserFeatures.TryGetValue(uri, out bool value) && value;
        }

        public virtual bool IsParserFeatureSet(string uri)
        {
            Dictionary<string, bool> parserFeatures = (Dictionary<string, bool>)GetProperty(Key.PARSER_FEATURES);
            if (parserFeatures == null)
            {
                return false;
            }

            // ContainsKey, not Get-then-null-test: default(bool) is never null, so this
            // method used to answer true for EVERY uri once any feature map existed.
            return parserFeatures.ContainsKey(uri);
        }

        public virtual object GetParserProperty(string name)
        {
            Dictionary<string, object> parserProperties = (Dictionary<string, object>)GetProperty(Key.PARSER_PROPERTIES);
            if (parserProperties == null)
            {
                return null;
            }
            else
            {
                return parserProperties.GetOrDefault(name);
            }
        }

        public virtual int GetTreeModel()
        {
            TreeModel model = Model;
            if (model == null)
            {
                return Builder.UNSPECIFIED_TREE_MODEL;
            }

            return model.SymbolicValue;
        }

        public virtual ParseOptions WithModel(TreeModel model)
        {
            return WithProperty(Key.MODEL, model);
        }

        public virtual ParseOptions WithSchemaValidationMode(int option)
        {
            return WithProperty(Key.SCHEMA_VALIDATION, option);
        }

        public virtual int GetSchemaValidationMode()
        {
            return GetIntegerProperty(Key.SCHEMA_VALIDATION, Validation.DEFAULT);
        }

        public virtual ParseOptions WithExpandAttributeDefaults(bool expand)
        {
            return WithProperty(Key.EXPAND_ATTRIBUTE_DEFAULTS, expand);
        }

        public virtual bool IsExpandAttributeDefaults()
        {
            return GetBooleanProperty(Key.EXPAND_ATTRIBUTE_DEFAULTS, true);
        }

        public virtual ParseOptions WithTopLevelElement(StructuredQName elementName)
        {
            return WithProperty(Key.TOP_LEVEL_ELEMENT, elementName);
        }

        public virtual ParseOptions WithTopLevelType(ISchemaType type)
        {
            return WithProperty(Key.TOP_LEVEL_TYPE, type);
        }

        public virtual ParseOptions WithUseXsiSchemaLocation(bool use)
        {
            return WithProperty(Key.USE_XSI_SCHEMA_LOCATION, use);
        }

        public virtual bool IsUseXsiSchemaLocation()
        {
            return GetBooleanProperty(Key.USE_XSI_SCHEMA_LOCATION, true);
        }

        public virtual ParseOptions WithValidationErrorLimit(int validationErrorLimit)
        {
            return WithProperty(Key.VALIDATION_ERROR_LIMIT, validationErrorLimit);
        }

        public virtual ParseOptions WithDTDValidationMode(int option)
        {
            return WithParserFeature("http://xml.org/sax/features/validation", option == Validation.STRICT || option == Validation.LAX).WithProperty(Key.DTD_VALIDATION, option);
        }

        public virtual ParseOptions WithValidationStatisticsRecipient(IValidationStatisticsRecipient recipient)
        {
            return WithProperty(Key.VALIDATION_STATISTICS_RECIPIENT, recipient);
        }

        public virtual ParseOptions WithLineNumbering(bool lineNumbering)
        {
            return WithProperty(Key.LINE_NUMBERING, lineNumbering);
        }

        public virtual bool IsLineNumbering()
        {
            return GetBooleanProperty(Key.LINE_NUMBERING, false);
        }

        public virtual bool IsLineNumberingSet()
        {
            return HasProperty(Key.LINE_NUMBERING);
        }

        // SAX entity resolvers are unsupported in the SAX-free engine; only the configured resolver
        // class name is retained, and its presence enables external-entity resolution (see ActiveStreamSource).
        public virtual ParseOptions WithEntityResolverClass(string className)
        {
            return WithProperty(Key.ENTITY_RESOLVER, className);
        }

        public virtual ParseOptions WithXIncludeAware(bool state)
        {
            return WithParserFeature("http://apache.org/xml/features/xinclude", state);
        }

        public virtual bool IsXIncludeAwareSet()
        {
            return IsParserFeatureSet("http://apache.org/xml/features/xinclude");
        }

        public virtual bool IsXIncludeAware()
        {
            return HasParserFeature("http://apache.org/xml/features/xinclude");
        }

        public virtual ParseOptions WithErrorReporter(IErrorReporter reporter)
        {
            if (reporter == null)
            {
                reporter = new StandardErrorReporter();
            }

            return WithProperty(Key.ERROR_REPORTER, reporter);
        }

        public virtual IErrorReporter GetErrorReporter()
        {
            return (IErrorReporter)GetProperty(Key.ERROR_REPORTER);
        }

        public virtual ParseOptions WithContinueAfterValidationErrors(bool keepGoing)
        {
            return WithProperty(Key.CONTINUE_AFTER_VALIDATION_ERRORS, keepGoing);
        }

        public virtual bool IsContinueAfterValidationErrors()
        {
            return GetBooleanProperty(Key.CONTINUE_AFTER_VALIDATION_ERRORS, false);
        }

        public virtual ParseOptions WithAddCommentsAfterValidationErrors(bool addComments)
        {
            return WithProperty(Key.ADD_COMMENTS_AFTER_VALIDATION_ERRORS, addComments);
        }

        public virtual bool IsAddCommentsAfterValidationErrors()
        {
            return GetBooleanProperty(Key.ADD_COMMENTS_AFTER_VALIDATION_ERRORS, false);
        }

        public virtual ParseOptions WithValidationParams(ValidationParams @params)
        {
            return WithProperty(Key.VALIDATION_PARAMS, @params);
        }

        public virtual ValidationParams GetValidationParams()
        {
            return (ValidationParams)GetProperty(Key.VALIDATION_PARAMS);
        }

        public virtual ParseOptions WithCheckEntityReferences(bool check)
        {
            return WithProperty(Key.CHECK_ENTITY_REFERENCES, check);
        }

        public virtual bool IsCheckEntityReferences()
        {
            return GetBooleanProperty(Key.CHECK_ENTITY_REFERENCES, false);
        }

        public virtual bool IsStable()
        {
            return GetBooleanProperty(Key.STABLE, true);
        }

        public virtual ParseOptions WithStable(bool stable)
        {
            return WithProperty(Key.STABLE, stable);
        }

        public virtual ParseOptions WithInvalidityHandler(IInvalidityHandler invalidityHandler)
        {
            return WithProperty(Key.INVALIDITY_HANDLER, invalidityHandler);
        }

        public virtual ParseOptions WithApplicableAccumulators(HashSet<Accumulator> accumulators)
        {
            return WithProperty(Key.APPLICABLE_ACCUMULATORS, accumulators);
        }

        public virtual ParseOptions WithPleaseCloseAfterUse(bool close)
        {
            return WithProperty(Key.PLEASE_CLOSE, close);
        }

        public virtual bool IsPleaseCloseAfterUse()
        {
            return GetBooleanProperty(Key.PLEASE_CLOSE, false);
        }

        public static void Dispose(ResolvedResource resource)
        {
            try
            {
                if (resource == null)
                {
                    return;
                }

                if (resource.Stream != null)
                {
                    resource.Stream.Dispose();
                }

                if (resource.TextReader != null)
                {
                    resource.TextReader.Dispose();
                }
            }
            catch (IOException err)
            {
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (TrieKVP<Key, object> entry in properties)
            {
                sb.Append(entry.GetKey());
                sb.Append('=');
                sb.Append(entry.Value);
                sb.Append(' ');
            }

            return sb.ToString();
        }
        private enum Key
        {
            PARSER_FEATURES,
            PARSER_PROPERTIES,
            ENTITY_RESOLVER,
            XINCLUDE_AWARE,
            ADD_COMMENTS_AFTER_VALIDATION_ERRORS,
            APPLICABLE_ACCUMULATORS,
            CHECK_ENTITY_REFERENCES,
            CONTINUE_AFTER_VALIDATION_ERRORS,
            DTD_VALIDATION,
            ERROR_REPORTER,
            EXPAND_ATTRIBUTE_DEFAULTS,
            FILTERS,
            INVALIDITY_HANDLER,
            LINE_NUMBERING,
            MODEL,
            PLEASE_CLOSE,
            SCHEMA_VALIDATION,
            SPACE_STRIPPING_RULE,
            STABLE,
            TOP_LEVEL_ELEMENT,
            TOP_LEVEL_TYPE,
            TREE_MODEL,
            USE_XSI_SCHEMA_LOCATION,
            VALIDATION_ERROR_LIMIT,
            VALIDATION_PARAMS,
            VALIDATION_STATISTICS_RECIPIENT,
            WRAP_DOCUMENT
        }
    }
}
