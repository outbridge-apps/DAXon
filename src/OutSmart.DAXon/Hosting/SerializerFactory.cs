////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Serialization;
namespace OutSmart.DAXon.Lib
{
    public class SerializerFactory
    {

        private static readonly OutSmart.DAXon.Internal.Regex.Pattern publicIdPattern = OutSmart.DAXon.Internal.Regex.Pattern.Compile("^[\\s\\r\\na-zA-Z0-9\\-'()+,./:=?;!*#@$_%]*$");
        readonly Configuration config;
        public SerializerFactory(Configuration config)
        {
            this.config = config;
        }

        public SerializerFactory(PipelineConfiguration pipe)
        {
            this.config = pipe.GetConfiguration();
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual IReceiver GetReceiver(IResultTarget result, PipelineConfiguration pipe, Properties props)
        {
            return GetReceiver(result, new SerializationProperties(props), pipe);
        }

        public virtual IReceiver GetReceiver(IResultTarget result)
        {
            return GetReceiver(result, new SerializationProperties(), config.MakePipelineConfiguration());
        }

        public virtual IReceiver GetReceiver(IResultTarget result, SerializationProperties @params)
        {
            return GetReceiver(result, @params, config.MakePipelineConfiguration());
        }

        public virtual IReceiver GetReceiver(IResultTarget result, SerializationProperties @params, PipelineConfiguration pipe)
        {
            if (result == null)
                throw new NullReferenceException();
            if (@params == null)
                throw new NullReferenceException();
            if (pipe == null)
                throw new NullReferenceException();
            Properties props = @params.GetProperties();
            CharacterMapIndex charMapIndex = @params.GetCharacterMapIndex();
            if (charMapIndex == null)
            {
                charMapIndex = new CharacterMapIndex();
            }

            string nextInChain = props.GetProperty(DAXonOutputKeys.NEXT_IN_CHAIN);
            if (nextInChain != null && !(nextInChain.Length == 0))
            {
                string href = props.GetProperty(DAXonOutputKeys.NEXT_IN_CHAIN);
                string @base = props.GetProperty(DAXonOutputKeys.NEXT_IN_CHAIN_BASE_URI);
                if (@base == null)
                {
                    @base = "";
                }

                Properties sansNext = new Properties(props);
                sansNext.SetProperty(DAXonOutputKeys.NEXT_IN_CHAIN, "");
                return PrepareNextStylesheet(pipe, href, @base, result);
            }

            string paramDoc = props.GetProperty(DAXonOutputKeys.PARAMETER_DOCUMENT);
            if (paramDoc != null && !(paramDoc.Length == 0))
            {
                string @base = props.GetProperty(DAXonOutputKeys.PARAMETER_DOCUMENT_BASE_URI);
                if (@base == null)
                {
                    @base = result.GetSystemId();
                }

                Properties props2 = new Properties(props);
                props2.SetProperty(DAXonOutputKeys.PARAMETER_DOCUMENT, "");
                ResourceRequest rr = new ResourceRequest();
                rr.relativeUri = paramDoc;
                rr.baseUri = @base;
                try
                {
                    rr.uri = ResolveURI.MakeAbsolute(paramDoc, @base).ToString();
                }
                catch (URISyntaxException err)
                {
                    throw XPathException.MakeXPathException(err);
                }

                rr.nature = NamespaceConstant.OUTPUT;
                rr.purpose = ResourceRequest.ANY_PURPOSE;
                ResolvedResource source = rr.Resolve(config.GetResourceResolver(), new DirectResourceResolver(config));
                ITreeInfo doc = config.BuildDocumentTree(source);
                SerializationParamsHandler ph = new SerializationParamsHandler();
                ph.SetSerializationParams(doc.GetRootNode());
                Properties paramDocProps = ph.GetSerializationProperties().GetProperties();
                foreach (string name in paramDocProps.StringPropertyNames())
                {
                    string value = paramDocProps.GetProperty(name);
                    props2.SetProperty(name, value);
                }

                CharacterMap charMap = ph.GetCharacterMap();
                if (charMap != null)
                {
                    props2.SetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS, charMap.Name.ClarkName);
                    charMapIndex.PutCharacterMap(charMap.Name, charMap);
                }

                props = props2;
                @params = new SerializationProperties(props2, charMapIndex);
            }

            IUnicodeWriter uWriter = null;
            ExpandedStreamResult expandedResult = null;
            if (result is UnicodeWriterResult)
            {
                uWriter = ((UnicodeWriterResult)result).UnicodeWriter;
            }
            else if (result is StreamResult)
            {
                expandedResult = new ExpandedStreamResult(GetConfiguration(), (StreamResult)result, props);
            }

            if (result is StreamResult || result is UnicodeWriterResult)
            {

                // The "target" is the start of the output pipeline, the IReceiver that
                // instructions will actually write to (except that other things like a
                // NamespaceReducer may get added in front of it). The "emitter" is the
                // last thing in the output pipeline, the IReceiver that actually generates
                // characters or bytes that are written to the StreamResult.
                SequenceReceiver target;
                string method = props.GetProperty(DAXonOutputKeys.METHOD);
                if (method == null)
                {
                    return NewUncommittedSerializer(result, new Sink(pipe), @params);
                }

                Emitter emitter = null;
                switch (method)
                {
                    case "html":
                        {
                            emitter = NewHTMLEmitter(props);
                            emitter.SetPipelineConfiguration(pipe);
                            if (uWriter == null)
                            {
                                uWriter = expandedResult.ObtainUnicodeWriter();
                                emitter.SetMustClose(expandedResult.IsMustCloseAfterUse());
                            }

                            emitter.SetUnicodeWriter(uWriter);
                            target = CreateHTMLSerializer(emitter, @params, pipe);
                            break;
                        }

                    case "xml":
                        {
                            emitter = NewXMLEmitter(props);
                            emitter.SetPipelineConfiguration(pipe);
                            if (uWriter == null)
                            {
                                uWriter = expandedResult.ObtainUnicodeWriter();
                                emitter.SetMustClose(expandedResult.IsMustCloseAfterUse());
                            }

                            emitter.SetUnicodeWriter(uWriter);
                            target = CreateXMLSerializer((XMLEmitter)emitter, @params);
                            break;
                        }

                    case "xhtml":
                        {
                            emitter = NewXHTMLEmitter(props);
                            emitter.SetPipelineConfiguration(pipe);
                            if (uWriter == null)
                            {
                                uWriter = expandedResult.ObtainUnicodeWriter();
                                emitter.SetMustClose(expandedResult.IsMustCloseAfterUse());
                            }

                            emitter.SetUnicodeWriter(uWriter);
                            target = CreateXHTMLSerializer(emitter, @params, pipe);
                            break;
                        }

                    case "text":
                        {
                            emitter = NewTEXTEmitter();
                            emitter.SetPipelineConfiguration(pipe);
                            if (uWriter == null)
                            {
                                uWriter = expandedResult.ObtainUnicodeWriter();
                                emitter.SetMustClose(expandedResult.IsMustCloseAfterUse());
                            }

                            emitter.SetUnicodeWriter(uWriter);
                            target = CreateTextSerializer(emitter, @params);
                            break;
                        }

                    case "json":
                        {
                            props.SetProperty(DAXonOutputKeys.OMIT_XML_DECLARATION, "yes");
                            if (uWriter == null)
                            {
                                uWriter = expandedResult.ObtainUnicodeWriter();
                            }

                            JSONEmitter je = new JSONEmitter(pipe, uWriter, props);
                            if (expandedResult != null)
                            {
                                je.SetMustClose(expandedResult.IsMustCloseAfterUse());
                            }

                            JSONSerializer js = new JSONSerializer(pipe, je, props);
                            string sortOrder = props.GetProperty(DAXonOutputKeys.PROPERTY_ORDER);
                            if (sortOrder != null)
                            {
                                js.SetPropertySorter(GetPropertySorter(sortOrder));
                            }

                            CharacterMapExpander characterMapExpander = MakeCharacterMapExpander(pipe, props, charMapIndex);
                            ProxyReceiver normalizer = MakeUnicodeNormalizer(pipe, props);
                            return CustomizeJSONSerializer(js, props, characterMapExpander, normalizer);
                        }

                    case "adaptive":
                        {
                            if (uWriter == null)
                            {
                                uWriter = expandedResult.ObtainUnicodeWriter();
                            }

                            AdaptiveEmitter je = new AdaptiveEmitter(pipe, uWriter);
                            je.SetOutputProperties(props);
                            if (expandedResult != null)
                            {
                                je.SetMustClose(expandedResult.IsMustCloseAfterUse());
                            }

                            CharacterMapExpander characterMapExpander = MakeCharacterMapExpander(pipe, props, charMapIndex);
                            ProxyReceiver normalizer = MakeUnicodeNormalizer(pipe, props);
                            return CustomizeAdaptiveSerializer(je, props, characterMapExpander, normalizer);
                        }

                    default:
                        {
                            if (method.StartsWith("{", StringComparison.Ordinal))
                            {

                                // We should have an EQName name here rather than a Clark name, but handle both for robustness
                                method = "Q" + method;
                            }

                            if (method.StartsWith("Q{" + NamespaceConstant.SAXON + "}", StringComparison.Ordinal))
                            {
                                CharacterMapExpander characterMapExpander = MakeCharacterMapExpander(pipe, props, charMapIndex);
                                ProxyReceiver normalizer = MakeUnicodeNormalizer(pipe, props);
                                target = CreateSaxonSerializationMethod(method, @params, pipe, characterMapExpander, normalizer, expandedResult, result);
                                if (target is Emitter)
                                {
                                    emitter = (Emitter)target;
                                }
                            }
                            else
                            {
                                IReceiver userReceiver;
                                userReceiver = CreateUserDefinedOutputMethod(method, props, pipe);
                                if (userReceiver is Emitter)
                                {
                                    emitter = (Emitter)userReceiver;
                                    if (uWriter == null)
                                    {
                                        uWriter = expandedResult.ObtainUnicodeWriter();
                                    }

                                    emitter.SetUnicodeWriter(uWriter);
                                    target = @params.MakeSequenceNormalizer(emitter);
                                }
                                else
                                {
                                    return @params.MakeSequenceNormalizer(userReceiver);
                                }
                            }
                        }

                        break;
                }

                if (emitter != null)
                {
                    emitter.SetOutputProperties(props);
                }


                //target = new RegularSequenceChecker(target); // add this back in for diagnostics only
                target.SetSystemId(result.GetSystemId());
                return target;
            }
            else
            {

                // Handle results other than StreamResult: these generally do not involve serialization
                return GetReceiverForNonSerializedResult(result, props, pipe);
            }
        }

        private ProxyReceiver MakeUnicodeNormalizer(PipelineConfiguration pipe, Properties props)
        {
            string normForm = props.GetProperty(DAXonOutputKeys.NORMALIZATION_FORM);
            if (normForm != null && !normForm.Equals("none"))
            {
                return NewUnicodeNormalizer(new Sink(pipe), props);
            }

            return null;
        }

        private CharacterMapExpander MakeCharacterMapExpander(PipelineConfiguration pipe, Properties props, CharacterMapIndex charMapIndex)
        {
            string useMaps = props.GetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS);
            if (useMaps != null)
            {
                return charMapIndex.MakeCharacterMapExpander(useMaps, new Sink(pipe), this);
            }

            return null;
        }

        private IReceiver GetReceiverForNonSerializedResult(IResultTarget result, Properties props, PipelineConfiguration pipe)
        {
            if (result is Emitter)
            {
                if (((Emitter)result).GetOutputProperties() == null)
                {
                    ((Emitter)result).SetOutputProperties(props);
                }

                return (Emitter)result;
            }
            else if (result is JSONSerializer)
            {
                if (((JSONSerializer)result).GetOutputProperties() == null)
                {
                    ((JSONSerializer)result).SetOutputProperties(props);
                }

                return (JSONSerializer)result;
            }
            else if (result is AdaptiveEmitter)
            {
                if (((AdaptiveEmitter)result).GetOutputProperties() == null)
                {
                    ((AdaptiveEmitter)result).SetOutputProperties(props);
                }

                return (AdaptiveEmitter)result;
            }
            else if (result is IReceiver)
            {
                IReceiver receiver = (IReceiver)result;
                receiver.SetSystemId(result.GetSystemId());
                receiver.SetPipelineConfiguration(pipe);
                if (((IReceiver)result).HandlesAppend() && props.GetProperty(DAXonOutputKeys.BUILD_TREE) == "no")
                {
                    return receiver; // TODO: handle item-separator
                }
                else
                {
                    return new TreeReceiver(receiver);
                }
            }
            else
            {
                if (pipe != null)
                {

                    // try to find an external object model that knows this kind of IResultTarget
                    IList<IExternalObjectModel> externalObjectModels = pipe.GetConfiguration().ExternalObjectModels;
                    foreach (IExternalObjectModel model in externalObjectModels)
                    {
                        IReceiver builder = model.GetDocumentBuilder(result);
                        if (builder != null)
                        {
                            builder.SetSystemId(result.GetSystemId());
                            builder.SetPipelineConfiguration(pipe);
                            return new TreeReceiver(builder);
                        }
                    }
                }
            }

            throw new ArgumentException("Unknown type of result: " + result.GetType());
        }

        public virtual SequenceReceiver MakeSequenceNormalizer(IReceiver receiver, Properties properties)
        {
            string method = properties.GetProperty(DAXonOutputKeys.METHOD);
            if (method == "json" || method == "adaptive")
            {
                return receiver is SequenceReceiver ? (SequenceReceiver)receiver : new TreeReceiver(receiver);
            }
            else
            {
                PipelineConfiguration pipe = receiver.GetPipelineConfiguration();
                SequenceReceiver result;
                string separator = properties.GetProperty(DAXonOutputKeys.ITEM_SEPARATOR);
                if (separator == null || separator == "#absent")
                {
                    result = new SequenceNormalizerWithSpaceSeparator(receiver);
                }
                else
                {
                    result = new SequenceNormalizerWithItemSeparator(receiver, StringView.Of(separator));
                }

                result.SetPipelineConfiguration(pipe);
                return result;
            }
        }

        protected virtual SequenceReceiver CreateHTMLSerializer(Emitter emitter, SerializationProperties @params, PipelineConfiguration pipe)
        {
            IReceiver target;
            target = emitter;
            Properties props = @params.GetProperties();
            if (props.GetProperty(DAXonOutputKeys.INDENT) != "no")
            {
                target = NewHTMLIndenter(target, props);
            }

            target = new NamespaceDifferencer(target, props);
            target = InjectUnicodeNormalizer(@params, target);
            target = InjectCharacterMapExpander(@params, target, true);
            string cdataElements = props.GetProperty(DAXonOutputKeys.CDATA_SECTION_ELEMENTS);
            if (cdataElements != null && !(cdataElements.Length == 0))
            {
                target = NewCDATAFilter(target, props);
            }

            if (DAXonOutputKeys.IsHtmlVersion5(props))
            {
                target = AddHtml5Component(target, props);
            }

            if (props.GetProperty(DAXonOutputKeys.ESCAPE_URI_ATTRIBUTES) != "no")
            {
                target = NewHTMLURIEscaper(target, props);
            }

            if (props.GetProperty(DAXonOutputKeys.INCLUDE_CONTENT_TYPE) != "no")
            {
                target = NewHTMLMetaTagAdjuster(target, props);
            }

            string attributeOrder = props.GetProperty(DAXonOutputKeys.ATTRIBUTE_ORDER);
            if (attributeOrder != null && !(attributeOrder.Length == 0))
            {
                target = NewAttributeSorter(target, props);
            }

            IFilterFactory validationFactory = @params.ValidationFactory;
            if (validationFactory != null)
            {
                target = validationFactory.MakeFilter(target);
            }

            return MakeSequenceNormalizer(target, props);
        }

        protected virtual SequenceReceiver CreateTextSerializer(Emitter emitter, SerializationProperties @params)
        {
            Properties props = @params.GetProperties();
            IReceiver target;
            target = InjectUnicodeNormalizer(@params, emitter);
            target = InjectCharacterMapExpander(@params, target, false);
            target = AddTextOutputFilter(target, props);
            IFilterFactory validationFactory = @params.ValidationFactory;
            if (validationFactory != null)
            {
                target = validationFactory.MakeFilter(target);
            }

            return MakeSequenceNormalizer(target, props);
        }

        protected virtual SequenceReceiver CustomizeJSONSerializer(JSONSerializer emitter, Properties props, CharacterMapExpander characterMapExpander, ProxyReceiver normalizer)
        {
            if (normalizer is UnicodeNormalizer)
            {
                emitter.SetNormalizationForm(((UnicodeNormalizer)normalizer).NormalizationForm);
            }

            if (characterMapExpander != null)
            {
                emitter.SetCharacterMap(characterMapExpander.GetCharacterMap());
            }

            return emitter;
        }

        protected virtual SequenceReceiver CustomizeAdaptiveSerializer(AdaptiveEmitter emitter, Properties props, CharacterMapExpander characterMapExpander, ProxyReceiver normalizer)
        {
            if (normalizer is UnicodeNormalizer)
            {
                emitter.SetNormalizationForm(((UnicodeNormalizer)normalizer).NormalizationForm);
            }

            if (characterMapExpander != null)
            {
                emitter.SetCharacterMap(characterMapExpander.GetCharacterMap());
            }

            return emitter;
        }

        protected virtual SequenceReceiver CreateXHTMLSerializer(Emitter emitter, SerializationProperties @params, PipelineConfiguration pipe)
        {
            IReceiver target = emitter;
            Properties props = @params.GetProperties();
            if (props.GetProperty(DAXonOutputKeys.INDENT) != "no")
            {
                target = NewXHTMLIndenter(target, props);
            }

            target = new NamespaceDifferencer(target, props);
            target = InjectUnicodeNormalizer(@params, target);
            target = InjectCharacterMapExpander(@params, target, true);
            string cdataElements = props.GetProperty(DAXonOutputKeys.CDATA_SECTION_ELEMENTS);
            if (cdataElements != null && !(cdataElements.Length == 0))
            {
                target = NewCDATAFilter(target, props);
            }

            if (DAXonOutputKeys.IsXhtmlHtmlVersion5(props))
            {
                target = AddHtml5Component(target, props);
            }

            if (props.GetProperty(DAXonOutputKeys.ESCAPE_URI_ATTRIBUTES) != "no")
            {
                target = NewXHTMLURIEscaper(target, props);
            }

            if (props.GetProperty(DAXonOutputKeys.INCLUDE_CONTENT_TYPE) != "no")
            {
                target = NewXHTMLMetaTagAdjuster(target, props);
            }

            string attributeOrder = props.GetProperty(DAXonOutputKeys.ATTRIBUTE_ORDER);
            if (attributeOrder != null && !(attributeOrder.Length == 0))
            {
                target = NewAttributeSorter(target, props);
            }

            if (@params.ValidationFactory != null)
            {
                target = @params.ValidationFactory.MakeFilter(target);
            }

            return MakeSequenceNormalizer(target, props);
        }

        public virtual IReceiver AddHtml5Component(IReceiver target, Properties outputProperties)
        {
            target = new NamespaceReducer(target);
            target = new XHTMLPrefixRemover(target);
            return target;
        }

        protected virtual SequenceReceiver CreateXMLSerializer(XMLEmitter emitter, SerializationProperties @params)
        {
            IReceiver target;
            Properties props = @params.GetProperties();
            bool canonical = props.GetProperty(DAXonOutputKeys.CANONICAL) == "yes";
            if (props.GetProperty(DAXonOutputKeys.INDENT) == "yes" || canonical)
            {
                target = NewXMLIndenter(emitter, props);
            }
            else
            {
                target = emitter;
            }

            target = new NamespaceDifferencer(target, props);
            if (props.GetProperty(DAXonOutputKeys.VERSION) == "1.0" && config.XMLVersion == Configuration.XML11)
            {

                // Check result meets XML 1.0 constraints if configuration allows XML 1.1 input but
                // this result document must conform to 1.0
                target = NewXML10ContentChecker(target, props);
            }

            target = InjectUnicodeNormalizer(@params, target);
            if (!canonical)
            {
                target = InjectCharacterMapExpander(@params, target, true);
            }

            string cdataElements = props.GetProperty(DAXonOutputKeys.CDATA_SECTION_ELEMENTS);
            if (cdataElements != null && !(cdataElements.Length == 0) && !canonical)
            {
                target = NewCDATAFilter(target, props);
            }

            if (canonical)
            {
                target = NewAttributeSorter(target, props);
                target = NewNamespaceSorter(target, props);
            }
            else
            {
                string attributeOrder = props.GetProperty(DAXonOutputKeys.ATTRIBUTE_ORDER);
                if (attributeOrder != null && !(attributeOrder.Length == 0))
                {
                    target = NewAttributeSorter(target, props);
                }
            }

            if (@params.ValidationFactory != null)
            {
                target = @params.ValidationFactory.MakeFilter(target);
            }

            return MakeSequenceNormalizer(target, props);
        }

        protected virtual SequenceReceiver CreateSaxonSerializationMethod(string method, SerializationProperties @params, PipelineConfiguration pipe, CharacterMapExpander characterMapExpander, ProxyReceiver normalizer, ExpandedStreamResult expandedResult, IResultTarget result)
        {
            throw new XPathException("Saxon serialization methods require Saxon-PE to be enabled");
        }

        protected virtual SequenceReceiver CreateUserDefinedOutputMethod(string method, Properties props, PipelineConfiguration pipe)
        {
            IReceiver userReceiver;

            // See if this output method is recognized by the Configuration
            userReceiver = pipe.GetConfiguration().MakeEmitter(method, props);
            userReceiver.SetPipelineConfiguration(pipe);
            return userReceiver is SequenceReceiver ? (SequenceReceiver)userReceiver : new TreeReceiver(userReceiver);
        }

        protected virtual IReceiver InjectCharacterMapExpander(SerializationProperties @params, IReceiver @out, bool useNullMarkers)
        {
            CharacterMapIndex charMapIndex = @params.GetCharacterMapIndex();
            if (charMapIndex != null)
            {
                string useMaps = @params.GetProperties().GetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS);
                if (useMaps != null)
                {
                    CharacterMapExpander expander = charMapIndex.MakeCharacterMapExpander(useMaps, @out, this);
                    expander.SetUseNullMarkers(useNullMarkers);
                    return expander;
                }
            }

            return @out;
        }

        protected virtual IReceiver InjectUnicodeNormalizer(SerializationProperties @params, IReceiver @out)
        {
            Properties props = @params.GetProperties();
            string normForm = props.GetProperty(DAXonOutputKeys.NORMALIZATION_FORM);
            if (normForm != null && !normForm.Equals("none"))
            {
                return NewUnicodeNormalizer(@out, props);
            }

            return @out;
        }

        protected virtual UncommittedSerializer NewUncommittedSerializer(IResultTarget result, IReceiver next, SerializationProperties @params)
        {
            return new UncommittedSerializer(result, next, @params);
        }

        protected virtual Emitter NewXMLEmitter(Properties properties)
        {
            return new XMLEmitter();
        }

        protected virtual Emitter NewHTMLEmitter(Properties properties)
        {
            HTMLEmitter emitter;

            // Note, we recognize html-version even when running XSLT 2.0.
            if (DAXonOutputKeys.IsHtmlVersion5(properties))
            {
                emitter = new HTML50Emitter();
            }
            else
            {
                emitter = new HTML40Emitter();
            }

            return emitter;
        }

        protected virtual Emitter NewXHTMLEmitter(Properties properties)
        {
            bool is5 = DAXonOutputKeys.IsXhtmlHtmlVersion5(properties);
            if (is5)
            {
                return new XHTML5Emitter();
            }
            else
            {
                return new XHTML1Emitter();
            }
        }

        public virtual IReceiver AddTextOutputFilter(IReceiver next, Properties properties)
        {
            return next;
        }

        protected virtual Emitter NewTEXTEmitter()
        {
            return new TEXTEmitter();
        }

        protected virtual ProxyReceiver NewXMLIndenter(XMLEmitter next, Properties outputProperties)
        {
            XMLIndenter r = new XMLIndenter(next);
            r.SetOutputProperties(outputProperties);
            return r;
        }

        protected virtual ProxyReceiver NewHTMLIndenter(IReceiver next, Properties outputProperties)
        {
            HTMLIndenter r = new HTMLIndenter(next);
            r.SetOutputProperties(outputProperties);
            return r;
        }

        protected virtual ProxyReceiver NewXHTMLIndenter(IReceiver next, Properties outputProperties)
        {
            HTMLIndenter r = new HTMLIndenter(next);
            r.SetOutputProperties(outputProperties);
            return r;
        }

        protected virtual MetaTagAdjuster NewXHTMLMetaTagAdjuster(IReceiver next, Properties outputProperties)
        {
            MetaTagAdjuster r = new MetaTagAdjuster(next);
            r.SetIsXHTML(true);
            r.SetOutputProperties(outputProperties);
            return r;
        }

        protected virtual MetaTagAdjuster NewHTMLMetaTagAdjuster(IReceiver next, Properties outputProperties)
        {
            MetaTagAdjuster r = new MetaTagAdjuster(next);
            r.SetIsXHTML(false);
            r.SetOutputProperties(outputProperties);
            return r;
        }

        protected virtual ProxyReceiver NewHTMLURIEscaper(IReceiver next, Properties outputProperties)
        {
            return new HTMLURIEscaper(next);
        }

        protected virtual ProxyReceiver NewXHTMLURIEscaper(IReceiver next, Properties outputProperties)
        {
            return new XHTMLURIEscaper(next);
        }

        protected virtual ProxyReceiver NewCDATAFilter(IReceiver next, Properties outputProperties)
        {
            CDATAFilter r = new CDATAFilter(next);
            r.SetOutputProperties(outputProperties);
            return r;
        }

        protected virtual IReceiver NewAttributeSorter(IReceiver next, Properties outputProperties)
        {
            return next;
        }

        protected virtual IReceiver NewNamespaceSorter(IReceiver next, Properties outputProperties)
        {
            return next;
        }

        protected virtual ProxyReceiver NewXML10ContentChecker(IReceiver next, Properties outputProperties)
        {
            return new XML10ContentChecker(next);
        }

        protected virtual ProxyReceiver NewUnicodeNormalizer(IReceiver next, Properties outputProperties)
        {
            string normForm = outputProperties.GetProperty(DAXonOutputKeys.NORMALIZATION_FORM);
            return new UnicodeNormalizer(normForm, next);
        }

        public virtual CharacterMapExpander NewCharacterMapExpander(IReceiver next)
        {
            return new CharacterMapExpander(next);
        }

        public virtual SequenceReceiver PrepareNextStylesheet(PipelineConfiguration pipe, string href, string baseURI, IResultTarget result)
        {
            pipe.GetConfiguration().CheckLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION, "saxon:next-in-chain", -1);
            return null;
        }

        public virtual SequenceWrapper NewSequenceWrapper(IReceiver destination)
        {
            return new SequenceWrapper(destination);
        }

        public virtual string CheckOutputProperty(string key, string value)
        {
            if (!key.StartsWith("{", StringComparison.Ordinal))
            {
                switch (key)
                {
                    case DAXonOutputKeys.ALLOW_DUPLICATE_NAMES:
                    case DAXonOutputKeys.ESCAPE_URI_ATTRIBUTES:
                    case DAXonOutputKeys.INCLUDE_CONTENT_TYPE:
                    case DAXonOutputKeys.INDENT:
                    case DAXonOutputKeys.OMIT_XML_DECLARATION:
                    case DAXonOutputKeys.UNDECLARE_PREFIXES:
                        if (value != null)
                        {
                            value = CheckYesOrNo(key, value);
                        }

                        break;
                    case DAXonOutputKeys.BUILD_TREE:
                        if (value != null)
                        {
                            value = CheckYesOrNo(key, value);
                        }

                        break;
                    case DAXonOutputKeys.BYTE_ORDER_MARK:
                        if (value != null)
                        {
                            value = CheckYesOrNo(key, value);
                        }

                        break;
                    case DAXonOutputKeys.CDATA_SECTION_ELEMENTS:
                    case DAXonOutputKeys.SUPPRESS_INDENTATION:
                    case DAXonOutputKeys.USE_CHARACTER_MAPS:
                        if (value != null)
                        {
                            value = CheckListOfEQNames(key, value);
                        }

                        break;
                    case DAXonOutputKeys.DOCTYPE_PUBLIC:
                        if (value != null)
                        {
                            CheckPublicIdentifier(value);
                        }

                        break;
                    case DAXonOutputKeys.DOCTYPE_SYSTEM:
                        if (value != null)
                        {
                            CheckSystemIdentifier(value);
                        }

                        break;
                    case DAXonOutputKeys.ENCODING:

                        // no constraints
                        break;
                    case DAXonOutputKeys.ESCAPE_SOLIDUS:
                        if (value != null)
                        {
                            value = CheckYesOrNo(key, value);
                        }

                        break;
                    case DAXonOutputKeys.HTML_VERSION:
                        if (value != null)
                        {
                            CheckDecimal(key, value);
                        }

                        break;
                    case DAXonOutputKeys.ITEM_SEPARATOR:

                        // no checking needed
                        break;
                    case DAXonOutputKeys.METHOD:
                    case DAXonOutputKeys.JSON_NODE_OUTPUT_METHOD:
                        if (value != null)
                        {
                            value = CheckMethod(key, value);
                        }

                        break;
                    case DAXonOutputKeys.MEDIA_TYPE:

                        // no constraints
                        break;
                    case DAXonOutputKeys.NORMALIZATION_FORM:
                        if (value != null)
                        {
                            CheckNormalizationForm(value);
                        }

                        break;
                    case DAXonOutputKeys.PARAMETER_DOCUMENT:

                        // no checking
                        break;
                    case DAXonOutputKeys.STANDALONE:
                        if (value != null && !value.Equals("omit"))
                        {
                            try
                            {
                                value = CheckYesOrNo(key, value);
                            }
                            catch (XPathException)
                            {
                                throw new XPathException("Serialization parameter {standalone} must have the value yes|no, true|false, 1|0, or 'omit'", "SEPM0016");
                            }
                        }

                        break;
                    case DAXonOutputKeys.VERSION:

                        // no constraints
                        break;
                    default:
                        throw new XPathException("Unknown serialization parameter " + Err.Wrap(key), "XQST0109");
                }
            }
            else if (key.StartsWith("{http://saxon.sf.net/}", StringComparison.Ordinal))
            {

                // Some Saxon serialization parameters are recognized in HE if they are used for internal purposes
                switch (key)
                {
                    case DAXonOutputKeys.STYLESHEET_VERSION:

                        // return
                        break;
                    case DAXonOutputKeys.PARAMETER_DOCUMENT_BASE_URI:

                        // return
                        break;
                    case DAXonOutputKeys.SUPPLY_SOURCE_LOCATOR:
                    case DAXonOutputKeys.UNFAILING:
                        if (value != null)
                        {
                            value = CheckYesOrNo(key, value);
                        }

                        break;
                    default:
                        throw new XPathException("Serialization parameter " + Err.Wrap(key, Err.EQNAME) + " is not available in Saxon-HE", "XQST0109");
                }
            }
            else
            {
            }

            return value;
        }

        protected static string CheckYesOrNo(string key, string value)
        {
            if (value == "yes" || value == "true" || value == "1")
            {
                return "yes";
            }
            else if (value == "no" || value == "false" || value == "0")
            {
                return "no";
            }
            else
            {
                throw new XPathException("Serialization parameter " + Err.Wrap(key) + " must have the value yes|no, true|false, or 1|0", "SEPM0016");
            }
        }

        private string CheckMethod(string key, string value)
        {
            if (value != "xml" && value != "html" && value != "xhtml" && value != "text")
            {
                string allowed;
                if (DAXonOutputKeys.JSON_NODE_OUTPUT_METHOD.Equals(key))
                {
                    allowed = "xml|html|xhtml|text";
                }
                else
                {
                    allowed = "xml|html|xhtml|text|json|adaptive";
                    if (value == "json" || value == "adaptive")
                    {
                        return value;
                    }
                }

                if (value.StartsWith("{", StringComparison.Ordinal))
                {
                    value = "Q" + value;
                }

                if (IsValidEQName(value))
                {
                    CheckExtensions(value);
                }
                else
                {
                    throw new XPathException("Invalid value (" + value + ") for serialization method: " + "must be " + allowed + ", or a QName in 'Q{uri}local' form", "SEPM0016");
                }
            }

            return value;
        }

        private static void CheckNormalizationForm(string value)
        {
            if (!NameChecker.IsValidNmtoken(StringView.Of(value)))
            {
                throw new XPathException("Invalid value for normalization-form: " + "must be NFC, NFD, NFKC, NFKD, fully-normalized, or none", "SEPM0016");
            }
        }

        private static bool IsValidEQName(string value)
        {
            if (value == null)
                throw new NullReferenceException();
            if ((value.Length == 0) || !value.StartsWith("Q{", StringComparison.Ordinal))
            {
                return false;
            }

            int closer = value.IndexOf('}', 2);
            return closer >= 2 && closer != value.Length - 1 && NameChecker.IsValidNCName(value.Substring(closer + 1));
        }

        private static bool IsValidClarkName(string value)
        {
            if (value.StartsWith("{", StringComparison.Ordinal))
            {
                return IsValidEQName("Q" + value);
            }
            else
            {
                return IsValidEQName("Q{}" + value);
            }
        }

        protected static void CheckNonNegativeInteger(string key, string value)
        {
            try
            {
                int n = int.Parse(value, CultureInfo.InvariantCulture);
                if (n < 0)
                {
                    throw new XPathException("Value of " + Err.Wrap(key) + " must be a non-negative integer", "SEPM0016");
                }
            }
            catch (Exception err) when (err is FormatException || err is OverflowException)
            {
                throw new XPathException("Value of " + Err.Wrap(key) + " must be a non-negative integer", "SEPM0016");
            }
        }

        private static void CheckDecimal(string key, string value)
        {
            if (!BigDecimalValue.CastableAsDecimal(value))
            {
                throw new XPathException("Value of " + Err.Wrap(key) + " must be a decimal number", "SEPM0016");
            }
        }

        protected static string CheckListOfEQNames(string key, string value)
        {
            Whitespace.Tokenizer tokenizer = new Whitespace.Tokenizer(StringView.Of(value).Tidy());
            StringBuilder builder = new StringBuilder();
            StringValue tok;
            while ((tok = tokenizer.Next()) != null)
            {
                string s = tok.GetStringValue();
                if (IsValidEQName(s) || NameChecker.IsValidNCName(tok.CodePoints()))
                {
                    builder.Append(s);
                }
                else if (IsValidClarkName(s))
                {
                    if (s.StartsWith("{", StringComparison.Ordinal))
                    {
                        builder.Append('Q').Append(s);
                    }
                    else
                    {
                        builder.Append("Q{}").Append(s);
                    }
                }
                else
                {
                    throw new XPathException("Value of " + Err.Wrap(key) + " must be a list of QNames in 'Q{uri}local' notation", "SEPM0016");
                }

                builder.Append(' ');
            }

            return builder.ToString();
        }

        protected static string CheckListOfEQNamesAllowingStar(string key, string value)
        {
            Whitespace.Tokenizer tokenizer = new Whitespace.Tokenizer(StringView.Of(value).Tidy());
            StringBuilder builder = new StringBuilder();
            StringValue tok;
            while ((tok = tokenizer.Next()) != null)
            {
                string s = tok.GetStringValue();
                if (s == "*" || IsValidEQName(s) || NameChecker.IsValidNCName(s))
                {
                    builder.Append(s);
                }
                else if (IsValidClarkName(s))
                {
                    if (s.StartsWith("{", StringComparison.Ordinal))
                    {
                        builder.Append('Q').Append(s);
                    }
                    else
                    {
                        builder.Append("Q{}").Append(s);
                    }
                }
                else
                {
                    throw new XPathException("Value of " + Err.Wrap(key) + " must be a list of QNames in 'Q{uri}local' notation", "SEPM0016");
                }

                builder.Append(' ');
            }

            return builder.ToString().Trim();
        }
        private static void CheckPublicIdentifier(string value)
        {
            if (!publicIdPattern.Matcher(value).Matches())
            {
                throw new XPathException("Invalid character in doctype-public parameter", "SEPM0016");
            }
        }

        private static void CheckSystemIdentifier(string value)
        {
            if (value.Contains("'") && value.Contains("\""))
            {
                throw new XPathException("The doctype-system parameter must not contain both an apostrophe and a quotation mark", "SEPM0016");
            }
        }

        protected virtual void CheckExtensions(string key)
        {
            throw new XPathException("Serialization property " + Err.Wrap(key, Err.EQNAME) + " is not available in Saxon-HE");
        }

        protected virtual IComparer<AtomicValue> GetPropertySorter(string sortSpecification)
        {
            throw new XPathException("Serialization property saxon:property-order is not available in Saxon-HE");
        }

    }
}