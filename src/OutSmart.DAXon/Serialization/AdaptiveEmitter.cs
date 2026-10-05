////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Serialization
{
    /// <summary>
    /// This class implements the Adaptive serialization method defined in XSLT+XQuery Serialization 3.1.
    /// </summary>
    public class AdaptiveEmitter : SequenceWriter, IReceiverWithOutputProperties
    {

        static ARegularExpression QUOTES = ARegularExpression.Compile("\"", "");
        private readonly IUnicodeWriter writer;
        private CharacterMap characterMap;
        private Properties outputProperties;
        private string itemSeparator = "\n";
        private bool started = false;
        private bool mustClose = true;
        public AdaptiveEmitter(PipelineConfiguration pipe, IUnicodeWriter writer) : base(pipe)
        {
            this.writer = writer;
        }

        public virtual void SetOutputProperties(Properties props)
        {
            outputProperties = props;
            string sep = props.GetProperty(DAXonOutputKeys.ITEM_SEPARATOR);
            if (sep != null && sep != "#absent")
            {
                itemSeparator = sep;
            }
        }

        public virtual void SetNormalizationForm(NormalizationForm normalizationForm)
        {
        }

        public virtual void SetMustClose(bool mustClose)
        {
            this.mustClose = mustClose;
        }

        public virtual void SetCharacterMap(CharacterMap map)
        {
            this.characterMap = map;
        }

        public Properties GetOutputProperties()
        {
            return outputProperties;
        }

        private void Emit(string s)
        {
            try
            {
                writer.Write(s);
            }
            catch (IOException e)
            {
                throw Emitter.WriteFailure(e, null);
            }
        }

        public override void Write(IItem item)
        {
            if (started)
            {
                Emit(itemSeparator);
            }
            else
            {
                started = true;
            }

            SerializeItem(item);
        }

        private void SerializeItem(IItem item)
        {
            if (item is AtomicValue)
            {
                Emit(SerializeAtomicValue((AtomicValue)item));
            }
            else if (item is NodeInfo)
            {
                SerializeNode((NodeInfo)item);
            }
            else if (item is MapItem)
            {
                SerializeMap((MapItem)item);
            }
            else if (item is ArrayItem)
            {
                SerializeArray((ArrayItem)item);
            }
            else if (item is IFunctionItem)
            {
                SerializeFunction((IFunctionItem)item);
            }
        }
        private string SerializeAtomicValue(AtomicValue value)
        {
            switch (value.PrimitiveType.Fingerprint)
            {
                case StandardNames.XS_STRING:
                case StandardNames.XS_ANY_URI:
                case StandardNames.XS_UNTYPED_ATOMIC:
                    {
                        UnicodeString s = value.UnicodeStringValue;
                        s = QUOTES.Replace(s, BMPString.Of("\"\""));
                        if (characterMap != null)
                        {
                            s = characterMap.IMap(s, false);
                        }

                        return "\"" + s.ToString() + "\"";
                    }

                case StandardNames.XS_BOOLEAN:
                    return value.EffectiveBooleanValue() ? "true()" : "false()";
                case StandardNames.XS_DECIMAL:
                case StandardNames.XS_INTEGER:
                    return value.GetStringValue();
                case StandardNames.XS_DOUBLE:
                    return FormatNumber.FormatExponential((DoubleValue)value);
                case StandardNames.XS_FLOAT:
                case StandardNames.XS_DURATION:
                case StandardNames.XS_DATE_TIME:
                case StandardNames.XS_DATE:
                case StandardNames.XS_TIME:
                case StandardNames.XS_G_YEAR_MONTH:
                case StandardNames.XS_G_MONTH:
                case StandardNames.XS_G_MONTH_DAY:
                case StandardNames.XS_G_YEAR:
                case StandardNames.XS_G_DAY:
                case StandardNames.XS_HEX_BINARY:
                case StandardNames.XS_BASE64_BINARY:
                    return value.PrimitiveType.DisplayName + "(\"" + value.UnicodeStringValue + "\")";
                case StandardNames.XS_DAY_TIME_DURATION:
                case StandardNames.XS_YEAR_MONTH_DURATION:
                    return "xs:duration(\"" + value.UnicodeStringValue + "\")";
                case StandardNames.XS_QNAME:
                case StandardNames.XS_NOTATION:
                    return ((QualifiedNameValue)value).GetStructuredQName().EQName;
                default:
                    return "***";
            }
        }

        private void SerializeFunction(IFunctionItem fn)
        {
            StructuredQName fname = fn.GetFunctionName();
            if (fname == null || fname.HasURI(NamespaceUri.ANONYMOUS))
            {
                Emit("(anonymous-function)");
            }
            else if (fname.HasURI(NamespaceUri.FN))
            {
                Emit("fn:" + fname.GetLocalPart());
            }
            else if (fname.HasURI(NamespaceUri.MATH))
            {
                Emit("math:" + fname.GetLocalPart());
            }
            else if (fname.HasURI(NamespaceUri.MAP_FUNCTIONS))
            {
                Emit("map:" + fname.GetLocalPart());
            }
            else if (fname.HasURI(NamespaceUri.ARRAY_FUNCTIONS))
            {
                Emit("array:" + fname.GetLocalPart());
            }
            else if (fname.HasURI(NamespaceUri.SCHEMA))
            {
                Emit("xs:" + fname.GetLocalPart());
            }
            else
            {
                Emit(fname.EQName);
            }

            Emit("#" + fn.GetArity());
        }

        private void SerializeNode(NodeInfo node)
        {
            switch (node.GetNodeKind())
            {
                case Types.Type.ATTRIBUTE:
                    Emit(node.DisplayName);
                    Emit("=\"");
                    Emit(EscapeAttributeValue(node.GetStringValue()));
                    Emit("\"");
                    break;
                case Types.Type.NAMESPACE:
                    Emit((node.GetLocalPart().Length == 0) ? "xmlns" : "xmlns:" + node.GetLocalPart());
                    Emit("=\"");
                    Emit(EscapeAttributeValue(node.GetStringValue()));
                    Emit("\"");
                    break;
                default:
                    StringWriter sw = new StringWriter();
                    Properties props = new Properties(outputProperties);
                    props.SetProperty("method", "xml");

                    if (props.GetProperty("omit-xml-declaration") == null)
                    {
                        props.SetProperty("omit-xml-declaration", "no");
                    }

                    props.SetProperty(DAXonOutputKeys.UNFAILING, "yes");
                    CharacterMapIndex cmi = null;
                    if (characterMap != null)
                    {

                        // If several character maps have been combined, this name will have been generated.
                        // If only a single map was provided in the first place, this will be the name of that map.
                        props.SetProperty("use-character-maps", characterMap.Name.ClarkName);
                        cmi = new CharacterMapIndex();
                        cmi.PutCharacterMap(characterMap.Name, characterMap);
                    }

                    SerializationProperties sProps = new SerializationProperties(props, cmi);
                    // Serialize the node with method=xml via the SerializerFactory receiver chain (the proven
                    // path fn:serialize uses); QueryResult.Serialize is a hollow stub (the Result hierarchy is
                    // gone from this port) so adaptive output of a node came out empty.
                    PipelineConfiguration p = GetPipelineConfiguration();
                    OutSmart.DAXon.Text.UnicodeBuilder ub = new OutSmart.DAXon.Text.UnicodeBuilder();
                    UnicodeWriterResult uwr = new UnicodeWriterResult(ub, null);
                    IReceiver r = p.GetConfiguration().SerializerFactory.GetReceiver(uwr, sProps, p);
                    r.Open();
                    r.Append(node);
                    r.Close();
                    Emit(ub.ToString());
                    break;
            }
        }

        private string EscapeAttributeValue(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length * 2);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\r':
                        sb.Append("&#xD;");
                        break;
                    case '\t':
                        sb.Append("&#x9;");
                        break;
                    case '\n':
                        sb.Append("&#xA;");
                        break;
                    case '&':
                        sb.Append("&amp;");
                        break;
                    case '<':
                        sb.Append("&lt;");
                        break;
                    case '>':
                        sb.Append("&gt;");
                        break;
                    case '"':
                        sb.Append("&quot;");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        // An array or a map inside another descends through here: the depth is the value's, so the stack is asked.
        private void SerializeArray(ArrayItem array)
        {
            StackGuard.Probe();
            Emit("[");
            bool first = true;
            foreach (ISequence seq in array.Members())
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Emit(",");
                }

                OutputInternalSequence(seq);
            }

            Emit("]");
        }

        private void SerializeMap(MapItem map)
        {
            StackGuard.Probe();
            Emit("map{");
            bool first = true;
            foreach (OutSmart.DAXon.Values.Maps.KeyValuePair pair in map.KeyValuePairs())
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Emit(",");
                }

                SerializeItem(pair.key);
                Emit(":");
                ISequence value = pair.value;
                OutputInternalSequence(value);
            }

            Emit("}");
        }

        private void OutputInternalSequence(ISequence value)
        {
            bool first = true;
            IItem it;
            ISequenceIterator iter = value.Iterate();
            bool omitParens = value is IGroundedValue && ((IGroundedValue)value).GetLength() == 1;
            if (!omitParens)
            {
                Emit("(");
            }

            while ((it = iter.Next()) != null)
            {
                if (!first)
                {
                    Emit(",");
                }

                first = false;
                SerializeItem(it);
            }

            if (!omitParens)
            {
                Emit(")");
            }
        }

        public override void Close()
        {
            base.Close();
            if (writer != null)
            {
                try
                {
                    if (mustClose)
                    {
                        writer.Dispose();
                    }
                    else
                    {
                        writer.Flush();
                    }
                }
                catch (IOException e)
                {
                    throw Emitter.WriteFailure(e, null);
                }
            }
        }
    }
}