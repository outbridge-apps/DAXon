////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Api
{
    public class Serializer : AbstractDestination
    {
        private static readonly Dictionary<string, Property> standardProperties = new Dictionary<string, Property>();
        private Processor processor; // never null
        private readonly Dictionary<StructuredQName, string> properties = new Dictionary<StructuredQName, string>(10);
        private readonly StreamResult result = new StreamResult();
        private CharacterMapIndex characterMapIndex = null;
        private bool mustClose = false;

        protected virtual Properties LocallyDefinedProperties
        {
            get
            {
                Properties props = new Properties();
                foreach (StructuredQName p in properties.Keys)
                {
                    string value = properties.GetOrDefault(p);
                    props.SetProperty(p.ClarkName, value);
                }

                return props;
            }
        }
        static Serializer()
        {
            Property[] propertyValues = (Property[])Enum.GetValues(typeof(Property));
            foreach (Property p in propertyValues)
            {
                // by the parameter's name in Clark notation, which is what GetProperty(QName) looks up
                standardProperties[p.GetPropertyName()] = p;
            }
        }

        public Serializer(Processor processor)
        {
            SetProcessor(processor);
        }

        public virtual void SetProcessor(Processor processor)
        {
            this.processor = processor ?? throw new NullReferenceException();
        }

        public virtual Processor GetProcessor()
        {
            return processor;
        }

        public virtual void SetOutputProperties(Properties suppliedProperties)
        {
            foreach (string name in suppliedProperties.StringPropertyNames())
            {
                properties[StructuredQName.FromClarkName(name)] = suppliedProperties.GetProperty(name);
            }
        }

        public virtual void SetOutputProperties(SerializationProperties suppliedProperties)
        {
            SetOutputProperties(suppliedProperties.GetProperties());
            SetCharacterMap(suppliedProperties.GetCharacterMapIndex());
        }

        public virtual void SetCloseOnCompletion(bool value)
        {
            mustClose = value;
        }

        public virtual void SetCharacterMap(CharacterMapIndex characterMap)
        {
            CharacterMapIndex existingIndex = this.characterMapIndex;
            if (existingIndex == null || existingIndex.IsEmpty())
            {
                existingIndex = characterMap;
            }
            else if (characterMap != null && !characterMap.IsEmpty() && existingIndex != characterMap)
            {

                // Merge the character maps
                existingIndex = existingIndex.Copy();
                foreach (CharacterMap map in characterMap)
                {
                    existingIndex.PutCharacterMap(map.Name, map);
                }
            }

            this.characterMapIndex = existingIndex;
        }

        public virtual CharacterMapIndex GetCharacterMapIndex()
        {
            if (characterMapIndex == null)
            {
                characterMapIndex = new CharacterMapIndex();
            }

            return characterMapIndex;
        }

        public virtual void SetOutputProperty(Property property, string value)
        {
            SerializerFactory sf = processor.UnderlyingConfiguration.SerializerFactory;
            value = WithoutHalfPairs(value);
            try
            {
                // Upstream Property.toString() returns the Clark-notation parameter name; the C# enum's
                // ToString() is the member name (SetOutputProperty(OMIT_XML_DECLARATION) threw "Unknown
                // serialization parameter {OMIT_XML_DECLARATION}").
                value = sf.CheckOutputProperty(property.GetPropertyName(), value);
            }
            catch (XPathException e)
            {
                throw new ArgumentException(e.Message);
            }

            if (value == null)
            {
                properties.Remove(property.GetQName().GetStructuredQName());
            }
            else
            {
                properties[property.GetQName().GetStructuredQName()] = value;
            }
        }

        public virtual string GetOutputProperty(Property property)
        {
            return properties.GetOrDefault(property.GetQName().GetStructuredQName());
        }

        // A value the host passes - an item separator, a doctype identifier - is text of the output like any other:
        // half a surrogate pair in it is U+FFFD.
        private static string WithoutHalfPairs(string value)
        {
            return value == null ? null : OutSmart.DAXon.Text.StringTool.WithoutHalfPairs(value);
        }

        public virtual void SetOutputProperty(QName property, string value)
        {
            SerializerFactory sf = processor.UnderlyingConfiguration.SerializerFactory;
            value = WithoutHalfPairs(value);
            NamespaceUri uri = property.GetNamespaceUri();
            if (uri.IsEmpty() || uri.Equals(NamespaceUri.SAXON))
            {
                try
                {
                    value = sf.CheckOutputProperty(property.ClarkName, value);
                }
                catch (XPathException e)
                {
                    throw new ArgumentException(e.Message);
                }

                if (uri.Equals(NamespaceUri.SAXON) && property.LocalName.Equals("next-in-chain"))
                {

                    // reject the next-in-chain property: it's not relevant to a Serializer
                    throw new ArgumentException("saxon:next-in-chain is not a valid serialization property");
                }
            }

            if (value == null)
            {
                properties.Remove(property.GetStructuredQName());
            }
            else
            {
                properties[property.GetStructuredQName()] = value;
            }
        }

        public virtual string GetOutputProperty(QName property)
        {
            return properties.GetOrDefault(property.GetStructuredQName());
        }

        public virtual void SetOutputWriter(TextWriter writer)
        {
            result.SetOutputStream(null);
            result.SetSystemId((string)null);
            result.SetWriter(writer);
            mustClose = false;
        }

        public virtual void SetOutputStream(System.IO.Stream stream)
        {
            result.SetWriter(null);
            result.SetSystemId((string)null);
            result.SetOutputStream(stream);
            mustClose = false;
        }

        public virtual void SetOutputFile(string file)
        {
            result.SetOutputStream(null);
            result.SetWriter(null);
            result.SetSystemId(file);
            try
            {
                DestinationBaseURI = new Uri(Path.GetFullPath(file)).AbsoluteUri;
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is IOException || e is FormatException || e is System.Security.SecurityException)
            {
                // A name the file system refuses is reported when the file is opened (SXRD0004), with every other
                // reason a file cannot be written; here it was .NET's own exception, a different one for each fault.
                DestinationBaseURI = null;
            }

            mustClose = true;
        }

        public virtual void SerializeNode(XdmNode node)
        {
            StreamResult res = result;
            if (res.GetOutputStream() == null && res.GetWriter() == null && res.GetSystemId() == null)
            {
                throw new InvalidOperationException("Either an outputStream, or a TextWriter, or a string must be supplied");
            }

            SerializeNodeToResult(node, res);
        }

        public virtual void SerializeXdmValue(XdmValue value)
        {
            if (value is XdmNode)
            {
                SerializeNode((XdmNode)value);
            }
            else
            {
                try
                {
                    SerializationProperties properties = new SerializationProperties(LocallyDefinedProperties, characterMapIndex);
                    QueryResult.SerializeSequence(((IGroundedValue)value.UnderlyingValue).Iterate(), processor.UnderlyingConfiguration, result, properties);
                }
                catch (XPathException e)
                {
                    throw new DAXonApiException(e);
                }
                catch (RecursionDepthError e)
                {
                    throw new DAXonApiException(e.ToXPathException());
                }
            }

            CloseAndNotify();
        }

        public virtual void Serialize(ResolvedResource source)
        {
            // Parses the source: claims the deadline slot as DocumentBuilder does, or a spent token from an earlier
            // run on this thread failed it at once.
            OutSmart.DAXon.Core.Controller.DeadlineToken prevDeadline = OutSmart.DAXon.Core.Controller.ArmThreadDeadline(processor.UnderlyingConfiguration, "Parsing");
            try
            {
                SerializerFactory sf = processor.UnderlyingConfiguration.SerializerFactory;
                IReceiver tr = sf.GetReceiver(result, new SerializationProperties(LocallyDefinedProperties));
                Sender.Send(InputSizeLimit.Apply(source, InputSizeLimit.MaxFor(processor.UnderlyingConfiguration), "FODC0002"), tr, processor.UnderlyingConfiguration.GetParseOptions());
                CloseAndNotify();
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
            finally
            {
                OutSmart.DAXon.Core.Controller.RestoreThreadDeadline(prevDeadline);
            }
        }

        public virtual string SerializeToString(ResolvedResource source)
        {
            // Parses the source: claims the deadline slot (see Serialize(ResolvedResource)).
            OutSmart.DAXon.Core.Controller.DeadlineToken prevDeadline = OutSmart.DAXon.Core.Controller.ArmThreadDeadline(processor.UnderlyingConfiguration, "Parsing");
            try
            {
                SerializerFactory sf = processor.UnderlyingConfiguration.SerializerFactory;
                StringWriter sw = new StringWriter();
                IReceiver tr = sf.GetReceiver(new StreamResult((TextWriter)sw), new SerializationProperties(LocallyDefinedProperties));
                Sender.Send(InputSizeLimit.Apply(source, InputSizeLimit.MaxFor(processor.UnderlyingConfiguration), "FODC0002"), tr, processor.UnderlyingConfiguration.GetParseOptions());
                CloseAndNotify();
                return sw.ToString();
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
            finally
            {
                OutSmart.DAXon.Core.Controller.RestoreThreadDeadline(prevDeadline);
            }
        }

        public virtual string SerializeNodeToString(XdmNode node)
        {
            StringWriter sw = new StringWriter();
            StreamResult sr = new StreamResult((TextWriter)sw);
            SerializeNodeToResult(node, sr);
            return sw.ToString();
        }

        private void SerializeNodeToResult(XdmNode node, IResultTarget res)
        {
            try
            {
                SerializationProperties props = new SerializationProperties(LocallyDefinedProperties, characterMapIndex);
                QueryResult.Serialize(node.UnderlyingNode, res, props);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        // Returns a System.Xml.XmlWriter whose events feed this serializer's output.
        public virtual StreamWriterToReceiver GetXmlWriter()
        {
            PipelineConfiguration pipe = processor.UnderlyingConfiguration.MakePipelineConfiguration();
            IReceiver r = GetReceiver(pipe, GetSerializationProperties());
            r = new NamespaceReducer(r);
            return new StreamWriterToReceiver(r);
        }


        public virtual object GetOutputDestination()
        {
            if (result.GetOutputStream() != null)
            {
                return result.GetOutputStream();
            }

            if (result.GetWriter() != null)
            {
                return result.GetWriter();
            }

            string systemId = result.GetSystemId();
            if (systemId != null)
            {
                try
                {
                    return new Uri(new URI(systemId).ToString()).LocalPath;
                }
                catch (URISyntaxException e)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public override IReceiver GetReceiver(PipelineConfiguration pipe, SerializationProperties @params)
        {
            try
            {
                SerializerFactory sf = pipe.GetConfiguration().SerializerFactory;
                SerializationProperties mergedParams = GetSerializationProperties().CombineWith(@params);
                IReceiver target = sf.GetReceiver(result, mergedParams, pipe);
                if (helper.Listeners != null)
                {
                    if (target is SequenceNormalizer)
                    {
                        ((SequenceNormalizer)target).OnClose(helper.Listeners);
                    }
                    else
                    {
                        target = new CloseNotifier(target, helper.Listeners);
                    }
                }

                if (target.GetSystemId() == null && DestinationBaseURI != null)
                {
                    target.SetSystemId(DestinationBaseURI.ToASCIIString());
                }

                return target;
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        public virtual Properties GetCombinedOutputProperties(Properties defaultOutputProperties)
        {
            Properties props = defaultOutputProperties == null ? new Properties() : new Properties(defaultOutputProperties);
            foreach (StructuredQName p in properties.Keys)
            {
                string value = properties.GetOrDefault(p);
                props.SetProperty(p.ClarkName, value);
            }

            return props;
        }

        public virtual SerializationProperties GetSerializationProperties()
        {
            return new SerializationProperties(LocallyDefinedProperties, characterMapIndex);
        }

        protected virtual IResultTarget GetResult()
        {
            return result;
        }

        public override void Close()
        {
            if (mustClose)
            {

                // ExpandedStreamResult publishes the stream it opens from the system ID back into
                // this result (round BG - before that the port never set it, so this branch was
                // dead and a failed run left its output file locked until finalization). Null the
                // slot out after closing: a reused Serializer must reopen from the system ID, not
                // find last run's closed stream.
                System.IO.Stream stream = result.GetOutputStream();
                if (stream != null)
                {
                    try
                    {
                        stream.Dispose();
                    }
                    catch (IOException err)
                    {
                        throw new DAXonApiException("Failed while closing output file", err);
                    }
                    finally
                    {
                        result.SetOutputStream(null);
                    }
                }

                TextWriter writer = result.GetWriter();
                if (writer != null)
                {
                    try
                    {
                        writer.Dispose();
                    }
                    catch (IOException err)
                    {
                        throw new DAXonApiException("Failed while closing output file", err);
                    }
                }
            }
        }

        // Abort-path release: free the published stream/writer silently (no listener
        // notifications, no DAXonApiException on the unwind path) and null the slots so a
        // preceding successful Close makes this a no-op.
        public override void Dispose()
        {
            if (mustClose)
            {
                System.IO.Stream stream = result.GetOutputStream();
                if (stream != null)
                {
                    try { stream.Dispose(); } catch (IOException) { }
                    result.SetOutputStream(null);
                }

                TextWriter writer = result.GetWriter();
                if (writer != null)
                {
                    try { writer.Dispose(); } catch (IOException) { }
                    result.SetWriter(null);
                }
            }
        }

        public static Property GetProperty(QName name)
        {
            string clarkName = name.ClarkName;
            // TryGetValue, not Get-then-null-test: Property is an enum, so the Java null-check
            // was always true and an unknown name silently answered enum value 0 (METHOD)
            // instead of throwing.
            if (standardProperties.TryGetValue(clarkName, out Property prop))
            {
                return prop;
            }

            throw new ArgumentException("Unknown serialization property " + clarkName);
        }

        public virtual bool IsMustCloseAfterUse()
        {
            return mustClose;
        }

        /// <summary>
        /// Enumeration class defining the permitted serialization properties
        /// </summary>
        public enum Property
        {
            /// <summary>
            /// Serialization method: xml, html, xhtml, text, json, adaptive; or <code>Q{uri}local</code>
            /// </summary>
            METHOD,
            /// <summary>
            /// Version of output method, for example "1.0" or "1.1" for XML
            /// </summary>
            VERSION,
            /// <summary>
            /// Character encoding of output stream
            /// </summary>
            ENCODING,
            /// <summary>
            /// Set to "yes" if the XML declaration is to be omitted from the output file
            /// </summary>
            OMIT_XML_DECLARATION,
            STANDALONE,
            /// <summary>
            /// Set to any string to indicate that the output is to include a DOCTYPE declaration with this public id
            /// </summary>
            DOCTYPE_PUBLIC,
            /// <summary>
            /// Set to any string to indicate that the output is to include a DOCTYPE declaration with this system id
            /// </summary>
            DOCTYPE_SYSTEM,
            CDATA_SECTION_ELEMENTS,
            /// <summary>
            /// Set to "yes" or "no" to indicate whether indentation is required
            /// </summary>
            INDENT,
            /// <summary>
            /// Set to indicate the media type (MIME type) of the output
            /// </summary>
            MEDIA_TYPE,
            USE_CHARACTER_MAPS,
            INCLUDE_CONTENT_TYPE,
            UNDECLARE_PREFIXES,
            ESCAPE_URI_ATTRIBUTES,
            /// <summary>
            /// Set to "yes" or "no" to indicate whether a byte order mark is to be written
            /// </summary>
            BYTE_ORDER_MARK,
            NORMALIZATION_FORM,
            /// <summary>
            /// Set to a string used to separate adjacent items in an XQuery result sequence
            /// </summary>
            ITEM_SEPARATOR,
            /// <summary>
            /// HTML version number
            /// </summary>
            HTML_VERSION,
            /// <summary>
            /// Build-tree option (XSLT only), "yes" or "no"
            /// </summary>
            BUILD_TREE,
            SAXON_INDENT_SPACES,
            SAXON_INTERNAL_DTD_SUBSET,
            SAXON_LINE_LENGTH,
            SAXON_ATTRIBUTE_ORDER,
            SAXON_CANONICAL,
            SAXON_NEWLINE,
            SAXON_SUPPRESS_INDENTATION,
            SAXON_DOUBLE_SPACE,
            SAXON_STYLESHEET_VERSION,
            SAXON_CHARACTER_REPRESENTATION,
            SAXON_RECOGNIZE_BINARY,
            SAXON_REQUIRE_WELL_FORMED,
            SAXON_WRAP,
            SAXON_SUPPLY_SOURCE_LOCATOR

            // --------------------
            // private final String name;
            // Property(String propertyName) {
            //     this.name = propertyName;
            // }
        }
    }
}
