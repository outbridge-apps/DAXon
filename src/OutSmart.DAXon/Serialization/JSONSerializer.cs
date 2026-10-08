////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Serialization
{
    public class JSONSerializer : SequenceWriter, IReceiverWithOutputProperties
    {
        // Nesting depth below which WriteSequence skips its stack probe (see there). No real JSON
        // is anywhere near this deep; a bomb is thousands of levels, so it loses nothing.
        private const int ProbeFreeDepth = 32;

        private bool allowDuplicateKeys;
        private string nodeOutputMethod = "xml";
        private int level;
        private int topLevelCount;
        private int maxLineLength = 80;
        private readonly JSONEmitter emitter;
        private Properties outputProperties;
        private bool isIndenting;
        private IComparer<AtomicValue> propertySorter;
        private bool unfailing;
        public JSONSerializer(PipelineConfiguration pipe, JSONEmitter emitter, Properties outputProperties) : base(pipe)
        {
            SetOutputProperties(outputProperties);
            this.emitter = emitter;
        }

        public virtual void SetOutputProperties(Properties details)
        {
            this.outputProperties = details;
            if (details.GetProperty(DAXonOutputKeys.ALLOW_DUPLICATE_NAMES) == "yes")
            {
                allowDuplicateKeys = true;
            }

            if (details.GetProperty(DAXonOutputKeys.INDENT) == "yes")
            {
                isIndenting = true;
            }

            if (details.GetProperty(DAXonOutputKeys.UNFAILING) == "yes")
            {
                unfailing = true;
                allowDuplicateKeys = true;
            }

            string jnom = details.GetProperty(DAXonOutputKeys.JSON_NODE_OUTPUT_METHOD);
            if (jnom != null)
            {
                nodeOutputMethod = jnom;
            }

            string max = details.GetProperty(DAXonOutputKeys.LINE_LENGTH);
            if (max != null)
            {
                try
                {
                    maxLineLength = int.Parse(max, CultureInfo.InvariantCulture);
                }
                catch (Exception err) when (err is FormatException || err is OverflowException)
                {
                }
            }
        }

        public virtual void SetPropertySorter(IComparer<AtomicValue> sorter)
        {
            this.propertySorter = sorter;
        }

        public Properties GetOutputProperties()
        {
            return outputProperties;
        }

        public virtual void SetNormalizationForm(NormalizationForm form)
        {
            emitter.SetNormalizationForm(form);
        }

        public virtual void SetCharacterMap(CharacterMap map)
        {
            emitter.SetCharacterMap(map);
        }

        public override void Write(IItem item)
        {
            if (level == 0 && ++topLevelCount >= 2)
            {
                throw new XPathException("JSON output method cannot handle sequences of two or more items", "SERE0023");
            }

            if (item is AtomicValue)
            {
                emitter.WriteAtomicValue((AtomicValue)item);
            }
            else if (item is MapItem)
            {
                MapItem map = (MapItem)item;
                bool oneLiner = !isIndenting || IsOneLinerMap(map);
                emitter.StartMap(oneLiner);
                if (propertySorter == null)
                {
                    // Straight-through: one pass over the pairs, no key list and no value re-lookup.
                    // Map keys are unique as ATOMS; SERE0022 only catches distinct atoms with the same
                    // string image (1 vs '1'), impossible when every key is already a string.
                    HashSet<string> keys = allowDuplicateKeys || map.KeyUType == UType.STRING ? null : new HashSet<string>();
                    foreach (OutSmart.DAXon.Values.Maps.KeyValuePair pair in map.KeyValuePairs())
                    {
                        string stringKey = pair.key.GetStringValue();
                        emitter.WriteKey(stringKey);
                        if (keys != null && !keys.Add(stringKey))
                        {
                            throw new XPathException("Key value \"" + stringKey + "\" occurs more than once in JSON map", "SERE0022");
                        }

                        WriteSequence(pair.value.Materialize());
                    }
                }
                else
                {
                    HashSet<string> keys = allowDuplicateKeys ? null : new HashSet<string>();
                    List<AtomicValue> keyList = new List<AtomicValue>();
                    foreach (OutSmart.DAXon.Values.Maps.KeyValuePair pair in map.KeyValuePairs())
                    {
                        keyList.Add(pair.key);
                    }

                    keyList.Sort(propertySorter);
                    foreach (AtomicValue key in keyList)
                    {
                        string stringKey = key.GetStringValue();
                        emitter.WriteKey(stringKey);
                        if (keys != null && !keys.Add(stringKey))
                        {
                            throw new XPathException("Key value \"" + stringKey + "\" occurs more than once in JSON map", "SERE0022");
                        }

                        ISequence value = map[key];
                        WriteSequence(value.Materialize());
                    }
                }

                emitter.EndMap();
            }
            else if (item is ArrayItem)
            {
                bool oneLiner = !isIndenting || IsOneLinerArray((ArrayItem)item);
                emitter.StartArray(oneLiner);
                foreach (ISequence member in ((ArrayItem)item).Members())
                {
                    WriteSequence(member.Materialize());
                }

                emitter.EndArray();
            }
            else if (item is NodeInfo)
            {
                string s = SerializeNode((NodeInfo)item);
                emitter.WriteAtomicValue(new StringValue(s));
            }
            else if (unfailing)
            {
                UnicodeString s = item.UnicodeStringValue;
                emitter.WriteAtomicValue(new StringValue(s));
            }
            else
            {
                throw new XPathException("JSON output method cannot handle an item of type " + item.GetType(), "SERE0021");
            }
        }

        private bool IsOneLinerArray(ArrayItem array)
        {
            int totalSize = 0;
            if (array.ArrayLength() < 0) /* Saxon: array one-liner only if ALL members atomic; empty falls through the (empty) member loop -> true */
            {
                return true;
            }

            foreach (ISequence member in array.Members())
            {
                if (!(member is AtomicValue))
                {
                    return false;
                }

                totalSize += (int)((AtomicValue)member).UnicodeStringValue.EstimatedLength() + 1;
                if (totalSize > maxLineLength)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsOneLinerMap(MapItem map)
        {
            long totalSize = 0;
            if (map.Count < 2)
            {
                return true;
            }

            foreach (OutSmart.DAXon.Values.Maps.KeyValuePair entry in map.KeyValuePairs())
            {
                if (entry.value is AtomicValue)
                {
                    totalSize += (int)entry.key.UnicodeStringValue.EstimatedLength() + ((AtomicValue)entry.value).UnicodeStringValue.EstimatedLength() + 4;
                }
                else if (entry.value.GetLength() == 0)
                {
                    totalSize += (int)entry.key.UnicodeStringValue.EstimatedLength() + 6; // ": null"
                }
                else
                {
                    return false;
                }

                if (totalSize > maxLineLength)
                {
                    return false;
                }
            }

            return true;
        }

        private string SerializeNode(NodeInfo node)
        {
            Properties props = new Properties();
            props.SetProperty("method", nodeOutputMethod);
            props.SetProperty("indent", "no");
            props.SetProperty("omit-xml-declaration", "yes");
            // Serialize the node with the json-node-output-method (default xml) via the SerializerFactory
            // receiver chain; QueryResult.Serialize is a hollow stub (Result hierarchy gone) so a node in
            // JSON output came out empty.
            PipelineConfiguration p = GetPipelineConfiguration();
            OutSmart.DAXon.Text.UnicodeBuilder ub = new OutSmart.DAXon.Text.UnicodeBuilder();
            UnicodeWriterResult uwr = new UnicodeWriterResult(ub, null);
            IReceiver r = p.GetConfiguration().SerializerFactory.GetReceiver(uwr, new SerializationProperties(props), p);
            r.Open();
            r.Append(node);
            r.Close();
            return ub.ToString().Trim();
        }

        private void WriteSequence(IGroundedValue seq)
        {
            // The recursive edge: every member of a map or array descends through here, so the
            // depth is the serialized value's, not the stylesheet's. This also runs once per
            // output value, and probing unconditionally cost 4% on serialize-heavy work - hence
            // the depth gate. Skipping the first ProbeFreeDepth levels costs tens of KB of stack,
            // far inside StackGuard's own margin.
            if (level > ProbeFreeDepth)
            {
                StackGuard.Probe();
            }

            if (seq is IItem single)
            {
                // an item IS a singleton sequence: skip GetLength/Head dispatch
                level++;
                Write(single);
                level--;
                return;
            }

            int len = seq.GetLength();
            if (len == 0)
            {
                emitter.WriteAtomicValue(null);
            }
            else if (len == 1)
            {
                level++;
                Write(seq.Head());
                level--;
            }
            else
            {
                throw new XPathException("JSON serialization: cannot handle a sequence of length " + len + " " + Err.DepictSequence(seq), "SERE0023");
            }
        }

        /// <summary>
        /// End of the document.
        /// </summary>
        public override void Close()
        {
            if (topLevelCount == 0)
            {
                emitter.WriteAtomicValue(null);
            }

            emitter.Dispose();
            base.Close();
        }
    }
}