////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Json
{
    /// <summary>
    /// Handler to generate an XML representation of JSON from a series of events
    /// </summary>
    internal sealed class JsonHandlerXML : JsonHandler
    {
        public const string PREFIX = "";
        private static readonly NamespaceUri JSON_NS = NamespaceUri.FN;
        private static readonly ISimpleType SIMPLE_TYPE = AnySimpleType.INSTANCE;
        private static readonly ISimpleType BOOLEAN_TYPE = BuiltInAtomicType.BOOLEAN;
        private static readonly ISimpleType STRING_TYPE = BuiltInAtomicType.STRING;
        private readonly Outputter @out;
        private readonly Builder builder;
        private Stack<string> keys;
        private readonly Stack<bool> inMap = new Stack<bool>();
        public bool validate;
        private bool checkForDuplicates;
        private NamePool namePool;
        private FingerprintedQName mapQN;
        private FingerprintedQName arrayQN;
        private FingerprintedQName stringQN;
        private FingerprintedQName numberQN;
        private FingerprintedQName booleanQN;
        private FingerprintedQName nullQN;
        private FingerprintedQName keyQN;
        private FingerprintedQName escapedQN;
        private FingerprintedQName escapedKeyQN;
        public Dictionary<string, ISchemaType> types;
        private readonly Stack<HashSet<string>> mapKeys = new Stack<HashSet<string>>();

        public JsonHandlerXML(IXPathContext context, string staticBaseUri, int flags)
        {
            Init(context, flags);
            builder = context.GetController().MakeBuilder();
            builder.SetSystemId(staticBaseUri);
            builder.SetTiming(false);
            builder.SetDurability(Durability.TEMPORARY);
            @out = new ComplexContentOutputter(builder);
            @out.Open();
            @out.StartDocument(ReceiverOption.NONE);
        }
        private FingerprintedQName Qname(string s)
        {
            FingerprintedQName fp = new FingerprintedQName("", NamespaceUri.NULL, s);
            fp.ObtainFingerprint(namePool);
            return fp;
        }

        private FingerprintedQName QnameNS(string s)
        {
            FingerprintedQName fp = new FingerprintedQName(PREFIX, JSON_NS, s);
            fp.ObtainFingerprint(namePool);
            return fp;
        }

        private void Init(IXPathContext context, int flags)
        {
            keys = new Stack<string>();
            /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
            Context = context;
            charChecker = context.GetConfiguration().ValidCharacterChecker;
            escape = (flags & JsonParser.ESCAPE) != 0;
            validate = (flags & JsonParser.VALIDATE) != 0;
            checkForDuplicates = validate || (flags & JsonParser.DUPLICATES_RETAINED) == 0;
            types = new Dictionary<string, ISchemaType>();
            namePool = context.GetConfiguration().GetNamePool();
            mapQN = QnameNS("map");
            arrayQN = QnameNS("array");
            stringQN = QnameNS("string");
            numberQN = QnameNS("number");
            booleanQN = QnameNS("boolean");
            nullQN = QnameNS("null");
            keyQN = Qname("key");
            escapedQN = Qname("escaped");
            escapedKeyQN = Qname("escaped-key");
            if (validate)
            {

                // Note, we do not actually perform schema validation, because we assume the XML we are generating
                // is valid. Instead, we just set type annotations "on trust", as if we were validating.
                // Currently this means we aren't detecting duplicate keys, which would cause validation to fail.
                // The spec needs clarification in this area.
                try
                {
                    Configuration config = context.GetConfiguration();

                    lock (config.syncLock)
                    {
                        config.CheckLicensedFeature(Configuration.LicenseFeature.SCHEMA_VALIDATION, "validation", -1);
                        LoadSchema(config);
                    }

                    string[] typeNames = new[]
                    {
                        "mapType",
                        "arrayType",
                        "stringType",
                        "numberType",
                        "booleanType",
                        "nullType",
                        "mapWithinMapType",
                        "arrayWithinMapType",
                        "stringWithinMapType",
                        "numberWithinMapType",
                        "booleanWithinMapType",
                        "nullWithinMapType"
                    };
                    foreach (string t in typeNames)
                    {
                        SetType(t, config.GetSchemaType(new StructuredQName(PREFIX, JSON_NS, t)));
                    }
                }
                catch (SchemaException e)
                {
                    throw new XPathException(e.Message);
                }
            }
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        private void LoadSchema(Configuration config)
        {
            if (!config.IsSchemaAvailable(JSON_NS))
            {
                IList<string> messages = new List<string>();
                System.IO.Stream stream = Core.Version.platform.LocateResource("xpath-functions.scm", messages);
                if (config.IsTiming())
                {
                    config.Logger.Info("Loading schema for: " + JSON_NS);
                }

                config.AddSchemaSource(new ResolvedResource { Stream = stream, SystemId = "classpath:xpath-functions.xsd" });
            }
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public void SetType(string name, ISchemaType st)
        {
            types[name] = st;
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public override bool SetKey(string unEscaped, string reEscaped)
        {
            this.keys.Push(unEscaped);
            return checkForDuplicates && !mapKeys.Peek().Add(reEscaped);
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public override ISequence GetResult()
        {
            @out.EndDocument();
            @out.Close();
            return builder.CurrentRoot;
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        private bool ContainsEscape(string literal)
        {
            return literal.IndexOf('\\') >= 0;
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        private bool IsInMap()
        {
            return inMap.Count > 0 && inMap.Peek();
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        private void StartElement(FingerprintedQName qn, string typeName)
        {
            // types is populated only under validation; skip the dictionary lookup otherwise
            StartElement(qn, validate ? types.GetOrDefault(typeName) : null);
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        private void StartElement(FingerprintedQName qn, ISchemaType st)
        {
            @out.StartElement(qn, validate && st != null ? st : Untyped.INSTANCE, Loc.NONE, ReceiverOption.NONE);
            if (IsInMap())
            {
                string k = keys.Pop();
                string uk = ReEscape(k);
                if (escape)
                {
                    MarkAsEscaped(uk, true);
                }

                @out.Attribute(keyQN, validate ? STRING_TYPE : SIMPLE_TYPE, uk, Loc.NONE, ReceiverOption.NONE);
            }
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        private void StartContent()
        {
            @out.StartContent();
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        private void Characters(string s)
        {
            @out.Characters(StringView.Of(s), Loc.NONE, ReceiverOption.NONE);
        }

        // For strings already proven BMP (numeric/boolean lexicals, clean-ReEscape results):
        // BMPString skips StringView's per-value surrogate scan.
        private void CharactersBmp(string s)
        {
            @out.Characters(BMPString.Of(s), Loc.NONE, ReceiverOption.NONE);
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        private void EndElement()
        {
            @out.EndElement();
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public override void StartArray()
        {
            StartElement(arrayQN, IsInMap() ? "arrayWithinMapType" : "arrayType");
            inMap.Push(false);
            StartContent();
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public override void EndArray()
        {
            inMap.Pop();
            EndElement();
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public override void StartMap()
        {
            StartElement(mapQN, IsInMap() ? "mapWithinMapType" : "mapType");
            if (checkForDuplicates)
            {
                mapKeys.Push(new HashSet<string>());
            }

            inMap.Push(true);
            StartContent();
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public override void EndMap()
        {
            inMap.Pop();
            if (checkForDuplicates)
            {
                mapKeys.Pop();
            }

            EndElement();
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public override void WriteNumeric(string asString, AtomicValue parsedValue)
        {
            StartElement(numberQN, IsInMap() ? "numberWithinMapType" : "numberType");
            StartContent();
            CharactersBmp(asString);   // numeric lexical is ASCII
            EndElement();
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public override void WriteString(string val)
        {
            StartElement(stringQN, IsInMap() ? "stringWithinMapType" : "stringType");
            string escaped = ReEscape(val);
            bool clean = reEscapeClean;
            if (escape)
            {
                MarkAsEscaped(escaped, false);
            }

            StartContent();
            if (clean)
            {
                CharactersBmp(escaped);
            }
            else
            {
                Characters(escaped);
            }

            EndElement();
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        protected override void MarkAsEscaped(string escaped, bool isKey)
        {
            if (ContainsEscape(escaped) && escape)
            {
                INodeName name = isKey ? escapedKeyQN : escapedQN;
                @out.Attribute(name, validate ? BOOLEAN_TYPE : SIMPLE_TYPE, "true", Loc.NONE, ReceiverOption.NONE);
            }
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public override void WriteBoolean(bool value)
        {
            StartElement(booleanQN, IsInMap() ? "booleanWithinMapType" : "booleanType");
            StartContent();
            CharactersBmp(value ? "true" : "false");
            EndElement();
        }

        /* This may not need to be a stack as there should only be at most one pre-selected key
       * However, the stack neatly indicates its empty state
       * */
        public override void WriteNull()
        {
            StartElement(nullQN, IsInMap() ? "nullWithinMapType" : "nullType");
            StartContent();
            EndElement();
        }
    }
}