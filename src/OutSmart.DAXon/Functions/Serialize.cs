////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{

    // fn:serialize, XPath 3.1. The parameters come as an output:serialization-parameters element or as a map; what the
    // map may hold, and of what type, is the table of F&O 3.1 (14.7.3). The text is made by the serializer that
    // xsl:output drives (SerializerFactory), into a string.
    internal sealed class Serialize : SystemFunction
    {
        // The parameters of the map form and the type of each. All are optional in their type: an empty sequence asks
        // for the default, as an absent entry does. method and json-node-output-method are union(xs:string,
        // xs:QName)?, told apart in MethodName; escape-solidus is of XSLT 4.0, and the JSON method here knows it.
        private static readonly Dictionary<string, SequenceType> PARAMETERS = new Dictionary<string, SequenceType>(StringComparer.Ordinal)
        {
            { "allow-duplicate-names", SequenceType.OPTIONAL_BOOLEAN },
            { "byte-order-mark", SequenceType.OPTIONAL_BOOLEAN },
            { "cdata-section-elements", BuiltInAtomicType.QNAME.ZeroOrMore() },
            { "doctype-public", SequenceType.OPTIONAL_STRING },
            { "doctype-system", SequenceType.OPTIONAL_STRING },
            { "encoding", SequenceType.OPTIONAL_STRING },
            { "escape-solidus", SequenceType.OPTIONAL_BOOLEAN },
            { "escape-uri-attributes", SequenceType.OPTIONAL_BOOLEAN },
            { "html-version", SequenceType.OPTIONAL_DECIMAL },
            { "include-content-type", SequenceType.OPTIONAL_BOOLEAN },
            { "indent", SequenceType.OPTIONAL_BOOLEAN },
            { "item-separator", SequenceType.OPTIONAL_STRING },
            { "json-node-output-method", SequenceType.OPTIONAL_ATOMIC },
            { "media-type", SequenceType.OPTIONAL_STRING },
            { "method", SequenceType.OPTIONAL_ATOMIC },
            { "normalization-form", SequenceType.OPTIONAL_STRING },
            { "omit-xml-declaration", SequenceType.OPTIONAL_BOOLEAN },
            { "standalone", SequenceType.OPTIONAL_BOOLEAN },
            { "suppress-indentation", BuiltInAtomicType.QNAME.ZeroOrMore() },
            { "undeclare-prefixes", SequenceType.OPTIONAL_BOOLEAN },
            { "use-character-maps", MapType.OPTIONAL_MAP_ITEM },
            { "version", SequenceType.OPTIONAL_STRING },
        };

        public Serialize() { }
        public static Func<Serialize> New() => () => new Serialize();

        public static CharacterMap ToCharacterMap(MapItem charMap)
        {
            var intHashMap = new OutSmart.DAXon.Collections.IntHashMap<string>();
            foreach (OutSmart.DAXon.Values.Maps.KeyValuePair pair in charMap.KeyValuePairs())
            {
                UnicodeString ch = pair.key.UnicodeStringValue;
                if (pair.value.GetLength() == 0)
                {
                    // a character mapped to no string: Head() of it was null
                    throw new XPathException("use-character-maps must be a map(xs:string, xs:string)", "XPTY0004").AsTypeError();
                }

                string str = pair.value.Head().GetStringValue();
                if (ch.Length() != 1)
                {
                    throw new XPathException("In the serialization parameter for the character map, each character to be mapped " +
                        "must be a single Unicode character", "SEPM0016");
                }
                int code = ch.CodePointAt(0);
                string prev = intHashMap.Put(code, str);
                if (prev != null)
                {
                    throw new XPathException("In the serialization parameters, the character map contains two entries for the character \\u" +
                        (65536 + code).ToString("x", CultureInfo.InvariantCulture).Substring(1), "SEPM0018");
                }
            }
            StructuredQName name = new StructuredQName("output", NamespaceUri.OUTPUT, "serialization-parameters");
            return new CharacterMap(name, intHashMap);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            var iter = arguments[0].Iterate();
            IItem param = arguments.Length < 2 ? null : arguments[1].Head();
            SerializationProperties sprops;
            if (param == null)
            {
                sprops = new SerializationProperties(new Properties());
            }
            else if (param is NodeInfo pnode)
            {
                // The element form: a wrong element name or namespace is a type error (XPTY0004, what the function
                // signature would raise); SerializationParamsHandler validates the parameters (SEPM0017 bad parameter,
                // SEPM0018 duplicate character, SEPM0019 duplicate parameter) and assembles them with any character map.
                NodeInfo el = pnode;
                if (el.GetNodeKind() == OutSmart.DAXon.Types.Type.DOCUMENT)
                {
                    el = Navigator.GetOutermostElement(el.GetTreeInfo());
                }

                if (el == null || el.GetNodeKind() != OutSmart.DAXon.Types.Type.ELEMENT
                    || el.GetLocalPart() != "serialization-parameters"
                    || !SerializationParamsHandler.NAMESPACE.Equals(el.GetNamespaceUri()))
                {
                    throw new XPathException("The second argument of fn:serialize must be an output:serialization-parameters element or a map", "XPTY0004");
                }

                SerializationParamsHandler sph = new SerializationParamsHandler(new Properties());
                sph.SetSerializationParams(el);
                sprops = sph.GetSerializationProperties();
            }
            else if (param is MapItem paramMap)
            {
                sprops = ParamsFromMap(paramMap, context);
            }
            else
            {
                throw new XPathException("The second argument of fn:serialize must be an output:serialization-parameters element or a map", "XPTY0004").AsTypeError();
            }

            // What the parameters do not say: the XML method, and no XML declaration. F&O fixes these for a map; for the
            // element form and for no parameters the defaults are the implementation's, and they are the same here -
            // but that an element which asks for what only a declaration can say is given the declaration (in a map
            // that is SEPM0009, by the default F&O fixes).
            Properties props = sprops.GetProperties();
            if (props.GetProperty("method") == null)
            {
                props.SetProperty("method", "xml");
            }

            if (props.GetProperty("omit-xml-declaration") == null)
            {
                props.SetProperty("omit-xml-declaration", param is NodeInfo && NeedsDeclaration(props) ? "no" : "yes");
            }

            try
            {
                // Byte-path in-memory sink: Latin1 output (the overwhelmingly common case) accumulates
                // as raw bytes instead of int[] codepoints + rope archiving, and the result is wrapped
                // without a final to-string pass. Wide content degrades gracefully inside the collector.
                UniStringCollector builder = new UniStringCollector();
                UnicodeWriterResult result = new UnicodeWriterResult(builder, null);
                SerializerFactory sf = context.GetConfiguration().SerializerFactory;
                // The run's pipeline (upstream: the configuration's), so copying a large node honours its deadline.
                PipelineConfiguration pipe = context.GetController()?.MakePipelineConfiguration() ?? context.GetConfiguration().MakePipelineConfiguration();
                // Inline sequence-copy (real SequenceCopier.cs uses a newer 0-arg Append() this IReceiver lacks):
                // Open -> Append(item) per item -> Close.
                using (IReceiver outr = sf.GetReceiver(result, sprops, pipe))
                {
                    outr.Open();
                    IItem it;
                    while ((it = iter.Next()) != null)
                    {
                        outr.Append(it);
                    }
                    outr.Close();
                }

                return new StringValue(builder.ToUnicodeString());
            }
            catch (XPathException e)
            {
                e.MaybeSetErrorCode("SENR0001");
                throw;
            }
        }

        // standalone, or a version other than 1.0 beside a document type: without a declaration each is SEPM0009
        private static bool NeedsDeclaration(Properties props)
        {
            string standalone = props.GetProperty("standalone");
            string version = props.GetProperty("version");
            return (standalone != null && standalone != "omit")
                || (version != null && version != "1.0" && props.GetProperty("doctype-system") != null);
        }

        // The value of a parameter, made of its required type by the function conversion rules as the conventions
        // for options have it: an untyped value is cast, an array or a node is atomized.
        private static IGroundedValue Converted(string name, IGroundedValue value, SequenceType required, IXPathContext context)
        {
            TypeHierarchy th = context.GetConfiguration().GetTypeHierarchy();
            if (required.Matches(value, th))
            {
                return value;
            }

            try
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.OPTION, name, 0, "XPTY0004");
                return th.ApplyFunctionConversionRules(value, required, role, Loc.NONE).Materialize();
            }
            catch (XPathException e) when (!e.HasErrorCode("XPTY0004"))
            {
                // A value that cannot be made of the required type - a cast that fails, an item with no atoms - is
                // the type error of the conventions, whatever the conversion itself called it.
                throw new XPathException(e.Message, "XPTY0004").AsTypeError();
            }
        }

        // The entries of the map that are parameters. An entry whose key is not one of the strings of the table - a
        // QName, which is for a parameter of an implementation; a name that has no meaning here - is not looked at.
        private static SerializationProperties ParamsFromMap(MapItem map, IXPathContext context)
        {
            Properties props = new Properties();
            CharacterMapIndex maps = null;
            foreach (OutSmart.DAXon.Values.Maps.KeyValuePair entry in map.KeyValuePairs())
            {
                // (xs:untypedAtomic and xs:anyURI are the same key as the string, and are StringValue too)
                if (!(entry.key is StringValue))
                {
                    continue;
                }

                string name = entry.key.GetStringValue();
                if (!PARAMETERS.TryGetValue(name, out SequenceType required))
                {
                    continue;
                }

                IGroundedValue value = Converted(name, entry.value, required, context);
                if (value.GetLength() == 0)
                {
                    // the default: for the lists of names that is no names, for standalone "omit"
                    continue;
                }

                switch (name)
                {
                    case "allow-duplicate-names":
                    case "byte-order-mark":
                    case "escape-solidus":
                    case "escape-uri-attributes":
                    case "include-content-type":
                    case "indent":
                    case "omit-xml-declaration":
                    case "standalone":
                    case "undeclare-prefixes":
                        props.SetProperty(name, ((BooleanValue)value.Head()).GetBooleanValue() ? "yes" : "no");
                        break;
                    case "doctype-public":
                    case "doctype-system":
                        // a zero-length string is "absent", as the empty sequence is
                        if (value.Head().GetStringValue().Length != 0)
                        {
                            props.SetProperty(name, Checked(name, value.Head().GetStringValue(), context));
                        }

                        break;
                    case "normalization-form":
                        props.SetProperty(name, Checked(name, value.Head().GetStringValue(), context));
                        break;
                    case "encoding":
                    case "html-version":
                    case "item-separator":
                    case "media-type":
                    case "version":
                        props.SetProperty(name, value.Head().GetStringValue());
                        break;
                    case "method":
                    case "json-node-output-method":
                        props.SetProperty(name, MethodName(name, value.Head(), context));
                        break;
                    case "cdata-section-elements":
                    case "suppress-indentation":
                        // the space-separated Clark names that CDATAFilter and the indenters read back
                        var names = new System.Text.StringBuilder();
                        ISequenceIterator qnames = value.Iterate();
                        for (IItem qname; (qname = qnames.Next()) != null;)
                        {
                            if (names.Length > 0)
                            {
                                names.Append(' ');
                            }

                            names.Append(((QualifiedNameValue)qname).GetStructuredQName().ClarkName);
                        }

                        props.SetProperty(name, names.ToString());
                        break;
                    case "use-character-maps":
                        // The serializer applies the maps NAMED in the property, so the one map is given a name.
                        StructuredQName mapName = NamespaceUri.NULL.QName("charMap");
                        maps = new CharacterMapIndex();
                        maps.PutCharacterMap(mapName, CharacterMapOf((MapItem)value.Head(), mapName));
                        props.SetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS, "charMap");
                        break;
                }
            }

            return maps != null ? new SerializationProperties(props, maps) : new SerializationProperties(props);
        }

        // union(xs:string, xs:QName): a string names a method of the specification; a QName, which must be in a
        // namespace, one of the implementation's own - and there is none, so the factory refuses it.
        private static string MethodName(string key, IItem value, IXPathContext context)
        {
            string name;
            if (value is QualifiedNameValue qname)
            {
                StructuredQName q = qname.GetStructuredQName();
                if (q.GetNamespaceUri().IsEmpty())
                {
                    throw new XPathException("The value of the '" + key + "' serialization parameter, when it is an xs:QName, must be in a namespace: "
                        + "the methods of the specification are named by strings", "SEPM0016");
                }

                name = q.EQName;
            }
            else if (value is StringValue)
            {
                name = value.GetStringValue();
            }
            else
            {
                throw new XPathException("The value of the '" + key + "' serialization parameter must be an xs:string or an xs:QName", "XPTY0004").AsTypeError();
            }

            return Checked(key, name, context);
        }

        // A value of the right type that is not one the parameter may have is SEPM0016: a method that is none (a typo
        // such as 'XML' was taken for the class name of a user's method), a public identifier with a character it may
        // not hold, a normalization form that is no name. The check is the one the API and xsl:output make.
        private static string Checked(string key, string value, IXPathContext context)
        {
            try
            {
                return context.GetConfiguration().SerializerFactory.CheckOutputProperty(key, value);
            }
            catch (XPathException e)
            {
                e.MaybeSetErrorCode("SEPM0016");
                throw;
            }
        }

        // map(xs:string, xs:string): the conventions for options do not reach into this map, so a key or a value of
        // another type is not converted (XPTY0004); a key that is not one character is SEPM0016.
        private static CharacterMap CharacterMapOf(MapItem map, StructuredQName name)
        {
            var entries = new OutSmart.DAXon.Collections.IntHashMap<string>();
            foreach (OutSmart.DAXon.Values.Maps.KeyValuePair pair in map.KeyValuePairs())
            {
                IGroundedValue replacement = pair.value;
                if (!(pair.key is StringValue) || replacement == null || replacement.GetLength() != 1 || !(replacement.Head() is StringValue))
                {
                    throw new XPathException("use-character-maps must be a map(xs:string, xs:string)", "XPTY0004").AsTypeError();
                }

                UnicodeString ch = pair.key.UnicodeStringValue;
                if (ch.Length() != 1)
                {
                    throw new XPathException("In the serialization parameter for the character map, each character to be mapped " +
                        "must be a single Unicode character", "SEPM0016");
                }

                entries.Put(ch.CodePointAt(0), replacement.Head().GetStringValue());
            }

            return new CharacterMap(name, entries);
        }
    }
}
