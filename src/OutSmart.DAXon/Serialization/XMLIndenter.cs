////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Serialization
{
    internal sealed class XMLIndenter : ProxyReceiver
    {
        private int level = 0;
        private bool sameline = false;
        private bool afterStartTag = false;
        private bool afterEndTag = true;
        private Events.Event.Text pendingWhitespace = null;
        private int line = 0; // line and column measure the number of lines and columns
        private int column = 0; // .. in whitespace text nodes between tags
        private int suppressedAtLevel = -1;
        private HashSet<INodeName> suppressedElements = null;
        private readonly XMLEmitter emitter;

        protected int Indentation => 3;

        protected int LineLength => 80;
        public XMLIndenter(XMLEmitter next) : base(next)
        {
            emitter = next;
        }

        public void SetOutputProperties(Properties props)
        {
            string omit = props.GetProperty(DAXonOutputKeys.OMIT_XML_DECLARATION);
            afterEndTag = omit == null || Whitespace.Trim(omit) != "yes" || props.GetProperty(DAXonOutputKeys.DOCTYPE_SYSTEM) != null;
            string s = props.GetProperty(DAXonOutputKeys.SUPPRESS_INDENTATION);
            if (s == null)
            {
                s = props.GetProperty("{http://saxon.sf.net/}suppress-indentation"); // for compatibility: since 9.3 also available in default namespace
            }

            if (s != null)
            {
                suppressedElements = new HashSet<INodeName>();
                foreach (string eqName in s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    suppressedElements.Add(FingerprintedQName.FromEQName(eqName));
                }
            }
        }

        /// <summary>
        /// Start of document
        /// </summary>
        public override void Open()
        {
            emitter.Open();
        }

        /// <summary>
        /// Output element start tag
        /// </summary>
        public override void StartElement(INodeName nameCode, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            if (afterStartTag || afterEndTag)
            {
                bool doubleSpaced = IsDoubleSpaced(nameCode);

                //            if (doubleSpaced) {
                //                line = 0;
                //                column = 0;
                //            }
                Indent(doubleSpaced);
            }
            else
            {
                FlushPendingWhitespace();
            }

            level++;
            if (suppressedAtLevel < 0)
            {
                string xmlSpace = attributes.GetValue(NamespaceUri.XML, "space");
                if (xmlSpace != null && xmlSpace.Trim().Equals("preserve"))
                {

                    // Note, we are suppressing indentation within an xml:space="preserve" region even if a descendant
                    // specifies xml:space="default"
                    suppressedAtLevel = level;
                }
            }

            sameline = true;
            afterStartTag = true;
            afterEndTag = false;
            line = 0;
            if (suppressedElements != null && suppressedAtLevel == -1 && suppressedElements.Contains(nameCode))
            {
                suppressedAtLevel = level;
            }

            if (type != AnyType.INSTANCE && type != Untyped.INSTANCE && suppressedAtLevel < 0 && type.IsComplexType() && ((IComplexType)type).IsMixedContent())
            {

                // suppress indentation for elements with mixed content. (Note this also suppresses
                // indentation for all descendants of such elements. We could be smarter than this.)
                suppressedAtLevel = level;
            }


            // Calculate indentation to be applied to attributes/namespaces
            if (suppressedAtLevel < 0)
            {
                int len = 0;
                foreach (INamespaceBindingSet nbs in namespaces)
                {
                    foreach (NamespaceBinding binding in nbs)
                    {
                        string prefix = binding.GetPrefix();
                        if ((prefix.Length == 0))
                        {
                            len += 9 + binding.GetNamespaceUri().ToString().Length;
                        }
                        else
                        {
                            len += prefix.Length + 10 + binding.GetNamespaceUri().ToString().Length;
                        }
                    }
                }

                foreach (AttributeInfo att in attributes)
                {
                    INodeName name = att.GetNodeName();
                    string prefix = name.GetPrefix();
                    len += name.GetLocalPart().Length + att.Value.Length + 4 + ((prefix.Length == 0) ? 4 : prefix.Length + 5);
                }

                if (len > LineLength)
                {
                    int indent = (level - 1) * Indentation + 2;
                    emitter.SetIndentForNextAttribute(indent);
                }
            }

            nextReceiver.StartElement(nameCode, type, attributes, namespaces, location, properties);
        }

        /// <summary>
        /// Output element end tag
        /// </summary>
        public override void EndElement()
        {
            level--;
            if (afterEndTag && !sameline)
            {
                Indent(false);
            }
            else
            {
                FlushPendingWhitespace();
            }

            emitter.EndElement();
            sameline = false;
            afterEndTag = true;
            afterStartTag = false;
            line = 0;
            if (level == (suppressedAtLevel - 1))
            {
                suppressedAtLevel = -1; // remove the suppression of indentation
            }
        }

        /// <summary>
        /// Output a processing instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (afterEndTag)
            {
                Indent(false);
            }
            else
            {
                FlushPendingWhitespace();
            }

            emitter.ProcessingInstruction(target, data, locationId, properties); //afterStartTag = false;
            //afterEndTag = false;
        }

        /// <summary>
        /// Output character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (suppressedAtLevel < 0 && Whitespace.IsAllWhite(chars))
            {
                if (pendingWhitespace != null)
                {
                    FlushPendingWhitespace(); // bug 6494
                }

                pendingWhitespace = new Events.Event.Text(chars, locationId, properties);
            }
            else
            {
                FlushPendingWhitespace();
                IIntIterator iter = chars.CodePoints();
                while (iter.MoveNext())
                {
                    int c = iter.Current;
                    if (c == '\n')
                    {
                        sameline = false;
                        line++;
                        column = 0;
                    }

                    column++;
                }

                emitter.Characters(chars, locationId, properties);
                afterStartTag = false;
                afterEndTag = false;
            }
        }

        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            if (afterEndTag)
            {
                Indent(false);
            }
            else
            {
                FlushPendingWhitespace();
            }

            emitter.Comment(chars, locationId, properties); //afterStartTag = false;
            //afterEndTag = false;
        }

        public override bool UsesTypeAnnotations()
        {
            return true;
        }

        private void Indent(bool doubleSpace)
        {
            if (suppressedAtLevel >= 0)
            {

                // indentation has been suppressed (e.g. by xmlspace="preserve")
                FlushPendingWhitespace();
                return;
            }

            pendingWhitespace = null; // if we're adding new whitespace, we're allowed to discard existing whitespace
            int spaces = level * Indentation;
            if (line > 0)
            {
                spaces -= column;
                if (spaces <= 0)
                {
                    return; // there's already enough white space, don't add more
                }
            }

            emitter.Characters(IndentWhitespace.Of(line == 0 ? (doubleSpace ? 2 : 1) : 0, spaces), Loc.NONE, ReceiverOption.NO_SPECIAL_CHARS);
            sameline = false;
        }

        private void FlushPendingWhitespace()
        {
            if (pendingWhitespace != null)
            {
                pendingWhitespace.Replay(nextReceiver);
                pendingWhitespace = null;
            }
        }

        public override void EndDocument()
        {
            if (afterEndTag)
            {
                emitter.Characters(BMPString.Of("\n"), Loc.NONE, ReceiverOption.NONE); // if permitted, output a trailing newline, for tidier console output
            }

            base.EndDocument();
        }

        protected bool IsDoubleSpaced(INodeName name)
        {
            return false;
        }
    }
}
