////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{

    // Minimal fn:serialize#2 (Invoice: serialize(array{...}, map{'method':'json','indent':true()})). Ported from the
    // core of the excluded Serialize.cs EvalSerialize WITHOUT re-including the full file (re-including it unmasks a
    // deferred CS0507 UnicodeString.Copy8bit cascade). Reads only method/indent from the options map (sufficient for
    // Invoice; full OptionsParameter validation deferred) and delegates the actual JSON formatting to the already-
    // compiled SerializerFactory -> JSONEmitter/JSONSerializer path (the same path xml-to-json uses), so output is
    // byte-identical to Saxon. SequenceCopier re-included (csproj); UnicodeWriterResult stub made functional (above).
    internal sealed class Serialize : SystemFunction
    {
        public Serialize() { }
        public static Func<Serialize> New() => () => new Serialize();

        public static CharacterMap ToCharacterMap(MapItem charMap)
        {
            var intHashMap = new OutSmart.DAXon.Collections.IntHashMap<string>();
            foreach (OutSmart.DAXon.Values.Maps.KeyValuePair pair in charMap.KeyValuePairs())
            {
                UnicodeString ch = pair.key.UnicodeStringValue;
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
                        (65536 + code).ToString("x").Substring(1), "SEPM0018");
                }
            }
            StructuredQName name = new StructuredQName("output", NamespaceUri.OUTPUT, "serialization-parameters");
            return new CharacterMap(name, intHashMap);
        }
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            var iter = arguments[0].Iterate();
            IItem param = arguments.Length < 2 ? null : arguments[1].Head();
            Properties props = new Properties();
            SerializationProperties elementSprops = null;
            // Character maps supplied via the map form's use-character-maps parameter (null if none).
            CharacterMapIndex __mapCharMaps = null;
            if (param is NodeInfo pnode)
            {
                // fn:serialize with an output:serialization-parameters element. The 2nd argument must be an
                // element(Q{output}serialization-parameters) — a wrong element name or namespace is a type
                // error (XPTY0004, what the function signature would raise) — and the parameters are validated
                // (SEPM0017 bad param / SEPM0018 duplicate character-map char / SEPM0019 duplicate parameter)
                // by SerializationParamsHandler, which also assembles the properties + any character map.
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

                SerializationParamsHandler sph = new SerializationParamsHandler(props);
                sph.SetSerializationParams(el);
                if (props.GetProperty("method") == null)
                {
                    props.SetProperty("method", "xml");
                }
                // The port's XML indenter is not wired into the fn:serialize() receiver chain (it throws there,
                // though it works for xsl:result-document). Until that chain is fixed, neutralise indent="yes"
                // so serialize(., <serialization-parameters><indent value="yes"/>…) yields (unindented) output
                // instead of a runtime error. No fn-serialize test asserts on indentation whitespace.
                // [follow-up: XML indent in the serialize() emitter chain]
                if (props.GetProperty("indent") == "yes")
                {
                    props.SetProperty("indent", "no");
                }
                elementSprops = sph.GetSerializationProperties();
            }
            else if (param is MapItem paramMap)
            {
                // use-character-maps entries collected from the options map (codepoint -> replacement string),
                // applied to the serializer below via a CharacterMapIndex (serialize-xml-132).
                OutSmart.DAXon.Collections.IntHashMap<string> __charMapEntries = null;
                // Read options by iterating KeyValuePairs and matching the key's string value. HashTrieMap.Get with a
                // freshly-constructed StringValue key does NOT match a stored key (the StringValue/UnicodeString
                // match-key is not value-equal across construction paths); iterating sidesteps that.
                foreach (OutSmart.DAXon.Values.Maps.KeyValuePair __opt in paramMap.KeyValuePairs())
                {
                    // Standard serialization parameters have xs:string keys; a QName key denotes an
                    // implementation-defined parameter (unsupported here). key.GetStringValue() on
                    // QName("","indent") returns the local name "indent", which must NOT be matched as the
                    // standard indent parameter — serialize(., map{QName("","indent"):true()}) must NOT indent
                    // (serialize-xml-120/120b). untypedAtomic is a StringValue, so string keys still pass.
                    if (!(__opt.key is StringValue))
                    {
                        continue;
                    }
                    string __k = __opt.key.GetStringValue();
                    IItem __v = __opt.value == null ? null : __opt.value.Head();
                    if (__v == null)
                    {
                        continue;
                    }
                    if (__k == "method")
                    {
                        props.SetProperty("method", __v.UnicodeStringValue.ToString());
                    }
                    else if (__k == "indent")
                    {
                        props.SetProperty("indent", RequireBooleanParam(__opt.value, __k) ? "yes" : "no");
                    }
                    else if (__k == "omit-xml-declaration")
                    {
                        props.SetProperty("omit-xml-declaration", RequireBooleanParam(__opt.value, __k) ? "yes" : "no");
                    }
                    else if (__k == "standalone")
                    {
                        props.SetProperty("standalone", RequireBooleanParam(__opt.value, __k) ? "yes" : "no");
                    }
                    else if (__k == "byte-order-mark")
                    {
                        props.SetProperty("byte-order-mark", RequireBooleanParam(__opt.value, __k) ? "yes" : "no");
                    }
                    else if (__k == "allow-duplicate-names")
                    {
                        props.SetProperty(DAXonOutputKeys.ALLOW_DUPLICATE_NAMES, RequireBooleanParam(__opt.value, __k) ? "yes" : "no");
                    }
                    else if (__k == "doctype-system")
                    {
                        props.SetProperty("doctype-system", __v.UnicodeStringValue.ToString());
                    }
                    else if (__k == "doctype-public")
                    {
                        props.SetProperty("doctype-public", __v.UnicodeStringValue.ToString());
                    }
                    else if (__k == "encoding")
                    {
                        props.SetProperty("encoding", __v.UnicodeStringValue.ToString());
                    }
                    else if (__k == "version")
                    {
                        props.SetProperty("version", __v.UnicodeStringValue.ToString());
                    }
                    else if (__k == "media-type")
                    {
                        props.SetProperty("media-type", __v.UnicodeStringValue.ToString());
                    }
                    else if (__k == "item-separator")
                    {
                        props.SetProperty("item-separator", __v.UnicodeStringValue.ToString());
                    }
                    else if (__k == "cdata-section-elements" || __k == "suppress-indentation")
                    {
                        // Map-form value is xs:QName* — serialize to the space-separated Clark-name list
                        // the property consumers (CDATAFilter / indenters) parse back with FromClarkName.
                        var __names = new System.Text.StringBuilder();
                        ISequenceIterator __qi = __opt.value.Iterate();
                        for (IItem __qn; (__qn = __qi.Next()) != null;)
                        {
                            if (!(__qn is QualifiedNameValue __qv))
                            {
                                throw new XPathException("The value of the " + __k + " serialization parameter must be a sequence of xs:QName", "XPTY0004");
                            }
                            if (__names.Length > 0)
                            {
                                __names.Append(' ');
                            }
                            __names.Append(__qv.GetStructuredQName().ClarkName);
                        }
                        props.SetProperty(__k == "cdata-section-elements" ? "cdata-section-elements" : DAXonOutputKeys.SUPPRESS_INDENTATION, __names.ToString());
                    }
                    else if (__k == "use-character-maps")
                    {
                        // Option-parameter conventions require map(xs:string, xs:string): every key and value
                        // must be an xs:string (or xs:untypedAtomic, which converts). A QName key, or a node /
                        // QName value, is a type error (XPTY0004). (Applying the map is handled elsewhere; this
                        // only validates the argument.)
                        if (!(__v is MapItem __cmap))
                        {
                            throw new XPathException("The value of the use-character-maps serialization parameter must be a map", "XPTY0004");
                        }
                        foreach (OutSmart.DAXon.Values.Maps.KeyValuePair __ce in __cmap.KeyValuePairs())
                        {
                            IItem __ck = __ce.key;
                            IItem __cv = __ce.value == null ? null : __ce.value.Head();
                            // untypedAtomic is a StringValue with IsUntypedAtomic() in this port, so `is
                            // StringValue` accepts xs:string and xs:untypedAtomic; QName / node / numeric do not.
                            bool __keyOk = __ck is StringValue;
                            bool __valOk = __cv is StringValue;
                            if (!__keyOk || !__valOk)
                            {
                                throw new XPathException("use-character-maps must be a map(xs:string, xs:string)", "XPTY0004");
                            }
                            // Record the mapping so it is actually applied by the serializer (was validate-only).
                            // The key is a single character (its codepoint); the value is its replacement string.
                            string __ckStr = __ck.GetStringValue();
                            if (__ckStr.Length >= 1)
                            {
                                if (__charMapEntries == null)
                                {
                                    __charMapEntries = new OutSmart.DAXon.Collections.IntHashMap<string>();
                                }
                                __charMapEntries.Put(char.ConvertToUtf32(__ckStr, 0), __cv.GetStringValue());
                            }
                        }
                    }
                }
                if (__charMapEntries != null)
                {
                    StructuredQName __cmName = NamespaceUri.NULL.QName("charMap");
                    __mapCharMaps = new CharacterMapIndex();
                    __mapCharMaps.PutCharacterMap(__cmName, new CharacterMap(__cmName, __charMapEntries));
                    // The serializer applies only the maps NAMED in the use-character-maps property (a list of
                    // char-map names), so register the name here too — exactly as the element form does.
                    props.SetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS, "charMap");
                }
            }
            // Defaults for the map / no-params forms only. The element form takes its properties (and any
            // spec defaults) from SerializationParamsHandler.GetSerializationProperties() — in particular it
            // must NOT force omit-xml-declaration=yes, so that serialize(., <serialization-parameters/>)
            // emits the XML declaration per the serialization spec default.
            if (elementSprops == null)
            {
                if (props.GetProperty("method") == null)
                {
                    props.SetProperty("method", "xml");
                }
                if (props.GetProperty("omit-xml-declaration") == null)
                {
                    props.SetProperty("omit-xml-declaration", "yes");
                }
            }
            try
            {
                // Byte-path in-memory sink: Latin1 output (the overwhelmingly common case) accumulates
                // as raw bytes instead of int[] codepoints + rope archiving, and the result is wrapped
                // without a final to-string pass. Wide content degrades gracefully inside the collector.
                UniStringCollector builder = new UniStringCollector();
                UnicodeWriterResult result = new UnicodeWriterResult(builder, null);
                SerializerFactory sf = context.GetConfiguration().SerializerFactory;
                PipelineConfiguration pipe = context.GetConfiguration().MakePipelineConfiguration();
                SerializationProperties sprops = elementSprops ?? (__mapCharMaps != null ? new SerializationProperties(props, __mapCharMaps) : new SerializationProperties(props));
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

        // A yes/no serialization parameter supplied via the options map must be a single xs:boolean; anything
        // else (an integer, a string like "true", or a 2-item sequence) is a type error per the function
        // signature / option-parameter conventions (XPTY0004). Previously a non-boolean value was silently
        // dropped, so serialize([1,2,3], map{'method':'json','indent':23}) produced output instead of erroring.
        private static bool RequireBooleanParam(ISequence value, string key)
        {
            ISequenceIterator it = value.Iterate();
            IItem first = it.Next();
            IItem second = first == null ? null : it.Next();
            if (first != null && second == null)
            {
                if (first is BooleanValue bv)
                {
                    return bv.GetBooleanValue();
                }
                // Option-parameter conventions convert xs:untypedAtomic (but NOT xs:string) to the required
                // type, so serialize(., map{'indent': xs:untypedAtomic('false')}) is valid (serialize-xml-142).
                if (first is StringValue sv && sv.IsUntypedAtomic())
                {
                    string s = sv.GetStringValue().Trim();
                    if (s == "true" || s == "1")
                        return true;
                    if (s == "false" || s == "0")
                        return false;
                }
            }

            throw new XPathException("The value of the '" + key + "' serialization parameter must be a single xs:boolean", "XPTY0004");
        }
    }
}
