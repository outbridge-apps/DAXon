////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;

using OutSmart.DAXon.Lib;
namespace OutSmart.DAXon.Serialization
{
    /// <summary>
    /// CDATAFilter: This ProxyReceiver converts character data to CDATA sections,
    /// if the character data belongs to one of a set of element types to be handled this way.
    /// </summary>
    internal sealed class CDATAFilter : ProxyReceiver
    {
        private UnicodeBuilder buffer = new UnicodeBuilder();
        private readonly Stack<INodeName> stack = new Stack<INodeName>();
        private ISet<INodeName> nameList;             // names of cdata elements
        private ICharacterSet characterSet;

        /// <summary>
        /// Create a CDATA Filter
        /// </summary>
        /// <param name="next">the next receiver in the pipeline</param>
        public CDATAFilter(IReceiver next) : base(next)
        {
        }

        /// <summary>
        /// Set the properties for this CDATA filter
        /// </summary>
        /// <param name="details">the output properties</param>
        public void SetOutputProperties(Properties details)
        {
            GetCdataElements(details);
            characterSet = GetConfiguration().GetCharacterSetFactory().GetCharacterSet(details);
        }

        /// <summary>
        /// Output element start tag
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            Flush();
            stack.Push(elemName);
            nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        /// <summary>
        /// Output element end tag
        /// </summary>
        public override void EndElement()
        {
            Flush();
            stack.Pop();
            nextReceiver.EndElement();
        }

        /// <summary>
        /// Output a processing instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            Flush();
            nextReceiver.ProcessingInstruction(target, data, locationId, properties);
        }

        /// <summary>
        /// Output character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (!ReceiverOption.Contains(properties, ReceiverOption.DISABLE_ESCAPING))
            {
                buffer.Append(chars.ToString());
            }
            else
            {
                // if the user requests disable-output-escaping, this overrides the CDATA request. We end
                // the CDATA section and output the characters as supplied.
                Flush();
                nextReceiver.Characters(chars, locationId, properties);
            }
        }

        /// <summary>
        /// Output a comment
        /// </summary>
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            Flush();
            nextReceiver.Comment(chars, locationId, properties);
        }

        /// <summary>
        /// Flush the buffer containing accumulated character data,
        /// generating it as CDATA where appropriate
        /// </summary>
        private void Flush()
        {
            bool cdata;
            int end = (int)buffer.Length();
            if (end == 0)
            {
                return;
            }

            if (stack.Count == 0)
            {
                cdata = false;      // text is not part of any element
            }
            else
            {
                INodeName top = stack.Peek();
                cdata = IsCDATA(top);
            }

            if (cdata)
            {

                // If we're doing Unicode normalization, we need to do this before CDATA processing.
                // In this situation the normalizer will be the next thing in the serialization pipeline.

                if (NextReceiver is UnicodeNormalizer)
                {
                    UnicodeString normal = ((UnicodeNormalizer)NextReceiver).Normalize(buffer.ToUnicodeString(), true);
                    buffer = new UnicodeBuilder();
                    buffer.Accept(normal);
                    end = (int)buffer.Length();
                }

                // Check that the buffer doesn't include a character not available in the current
                // encoding

                UnicodeString bufferContent = buffer.ToUnicodeString();

                int start = 0;
                int k = 0;
                while (k < end)
                {
                    int next = bufferContent.CodePointAt(k);
                    if (next != 0 && characterSet.InCharset(next))
                    {
                        k++;
                    }
                    else
                    {

                        // flush out the preceding characters as CDATA

                        FlushCDATA(bufferContent.Substring(start, k));

                        while (true)
                        {
                            // output consecutive non-encodable characters
                            // before restarting the CDATA section
                            nextReceiver.Characters(bufferContent.Substring(k, k + 1),
                                                    Loc.NONE, ReceiverOption.DISABLE_CHARACTER_MAPS);
                            k++;
                            if (k >= end)
                            {
                                break;
                            }
                            next = bufferContent.CodePointAt(k);
                            if (characterSet.InCharset(next))
                            {
                                break;
                            }
                        }
                        start = k;
                    }
                }
                FlushCDATA(bufferContent.Substring(start, end));

            }
            else
            {
                nextReceiver.Characters(buffer.ToUnicodeString(), Loc.NONE, ReceiverOption.NONE);
            }

            buffer.Clear();
        }

        /// <summary>
        /// Output an array as a CDATA section. At this stage we have checked that all the characters
        /// are OK, but we haven't checked that there is no "]]&gt;" sequence in the data
        /// </summary>
        /// <param name="data">the data to be output</param>
        private void FlushCDATA(UnicodeString data)
        {
            data = data.Tidy();
            if (data.IsEmpty())
            {
                return;
            }
            long len = data.Length();
            const int chprop =
                    ReceiverOption.DISABLE_ESCAPING | ReceiverOption.DISABLE_CHARACTER_MAPS;
            ILocation loc = Loc.NONE;
            nextReceiver.Characters(BMPString.Of("<![CDATA["), loc, chprop);

            // Check that the character data doesn't include the substring "]]>"
            // Also get rid of any zero bytes inserted by character map expansion

            long i = 0;
            long doneto = 0;
            while (i < len - 2)
            {
                if (data.CodePointAt(i) == ']' && data.CodePointAt(i + 1) == ']' && data.CodePointAt(i + 2) == '>')
                {
                    nextReceiver.Characters(data.Substring(doneto, i + 2), loc, chprop);
                    nextReceiver.Characters(BMPString.Of("]]><![CDATA["), loc, chprop);
                    doneto = i + 2;
                }
                else if (data.CodePointAt(i) == 0)
                {
                    nextReceiver.Characters(data.Substring(doneto, i), loc, chprop);
                    doneto = i + 1;
                }
                i++;
            }
            nextReceiver.Characters(data.Substring(doneto, len), loc, chprop);
            nextReceiver.Characters(BMPString.Of("]]>"), loc, chprop);
        }

        /// <summary>
        /// See if a particular element is a CDATA element. Method is protected to allow
        /// overriding in a subclass.
        /// </summary>
        /// <param name="elementName">identifies the name of element we are interested in</param>
        /// <returns>true if this element is included in cdata-section-elements</returns>
        protected bool IsCDATA(INodeName elementName)
        {
            return nameList.Contains(elementName);
        }

        /// <summary>
        /// Extract the list of CDATA elements from the output properties
        /// </summary>
        /// <param name="details">the output properties</param>
        private void GetCdataElements(Properties details)
        {
            bool isHTML = details.GetProperty(DAXonOutputKeys.METHOD) == "html";
            bool isHTML5 = isHTML && details.GetProperty(DAXonOutputKeys.VERSION) == "5.0";
            bool isHTML4 = isHTML && !isHTML5;
            string cdata = details.GetProperty(DAXonOutputKeys.CDATA_SECTION_ELEMENTS);
            if (cdata == null)
            {
                // this doesn't happen, but there's no harm allowing for it
                nameList = new HashSet<INodeName>();
                return;
            }
            nameList = new HashSet<INodeName>();
            string[] tokens = cdata.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string expandedName in tokens)
            {
                StructuredQName sq = StructuredQName.FromClarkName(expandedName);
                NamespaceUri uri = sq.GetNamespaceUri();
                if (!isHTML || (isHTML4 && !uri.Equals(NamespaceUri.NULL))
                        || (isHTML5 && !uri.Equals(NamespaceUri.NULL) && !uri.Equals(NamespaceUri.XHTML)))
                {
                    nameList.Add(new FingerprintedQName("", uri, sq.GetLocalPart()));
                }
            }
        }
    }
}
