////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Serialization
{
    public class UncommittedSerializer : ProxyReceiver
    {
        private bool committed = false;
        private EventBuffer pending = null;
        private readonly IResultTarget finalResult;
        private readonly SerializationProperties properties;
        public UncommittedSerializer(IResultTarget finalResult, IReceiver next, SerializationProperties @params) : base(next)
        {
            this.finalResult = finalResult;
            this.properties = @params;
        }

        public override void Open()
        {
            committed = false;
        }

        /// <summary>
        /// End of document
        /// </summary>
        public override void Close()
        {

            // empty output: must send a beginDocument()/endDocument() pair to the content handler
            if (!committed)
            {
                SwitchToMethod("xml");
            }

            NextReceiver.Close();
        }

        /// <summary>
        /// Produce character output using the current System.IO.TextWriter. <BR>
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (committed)
            {
                NextReceiver.Characters(chars, locationId, properties);
            }
            else
            {
                if (pending == null)
                {
                    pending = new EventBuffer(GetPipelineConfiguration());
                }

                pending.Characters(chars, locationId, properties);
                if (!Whitespace.IsAllWhite(chars))
                {
                    SwitchToMethod("xml");
                }
            }
        }

        /// <summary>
        /// Processing Instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (committed)
            {
                NextReceiver.ProcessingInstruction(target, data, locationId, properties);
            }
            else
            {
                if (pending == null)
                {
                    pending = new EventBuffer(GetPipelineConfiguration());
                }

                pending.ProcessingInstruction(target, data, locationId, properties);
            }
        }

        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            if (committed)
            {
                NextReceiver.Comment(chars, locationId, properties);
            }
            else
            {
                if (pending == null)
                {
                    pending = new EventBuffer(GetPipelineConfiguration());
                }

                pending.Comment(chars, locationId, properties);
            }
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            if (!committed)
            {
                string name = elemName.GetLocalPart();
                NamespaceUri uri = elemName.GetNamespaceUri();
                if (name.Equals("html", StringComparison.OrdinalIgnoreCase) && uri.IsEmpty())
                {
                    SwitchToMethod("html");
                }
                else if (name.Equals("html") && uri.Equals(NamespaceUri.XHTML))
                {
                    string version = this.properties.GetProperties().GetProperty(DAXonOutputKeys.STYLESHEET_VERSION);
                    if (version == "10")
                    {
                        SwitchToMethod("xml");
                    }
                    else
                    {
                        SwitchToMethod("xhtml");
                    }
                }
                else
                {
                    SwitchToMethod("xml");
                }
            }

            NextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        public override void StartDocument(int properties)
        {
            if (committed)
            {
                NextReceiver.StartDocument(properties);
            }
            else
            {
                if (pending == null)
                {
                    pending = new EventBuffer(GetPipelineConfiguration());
                }

                pending.StartDocument(properties);
            }
        }

        public override void EndDocument()
        {

            // empty output: must send a beginDocument()/endDocument() pair to the content handler
            if (!committed)
            {
                SwitchToMethod("xml");
            }

            NextReceiver.EndDocument();
        }

        private void SwitchToMethod(string method)
        {
            Properties newProperties = new Properties(properties.GetProperties());
            newProperties.SetProperty(DAXonOutputKeys.METHOD, method);
            SerializerFactory sf = GetConfiguration().SerializerFactory;
            SerializationProperties newParams = new SerializationProperties(newProperties, properties.GetCharacterMapIndex());
            newParams.ValidationFactory = properties.ValidationFactory;
            IReceiver target = sf.GetReceiver(finalResult, newParams, GetPipelineConfiguration());
            committed = true;
            target.Open();
            if (pending != null)
            {
                pending.Replay(target);
                pending = null;
            }

            SetUnderlyingReceiver(target);
        }

        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (item is NodeInfo)
            {
                NodeInfo node = (NodeInfo)item;
                if (node.GetNodeKind() == Types.Type.ATTRIBUTE || node.GetNodeKind() == Types.Type.NAMESPACE)
                {
                    throw new XPathException("Cannot write a free-standing attribute or namespace node directly to the serializer", "SENR0001");
                }

                node.Copy(this, CopyOptions.ALL_NAMESPACES | CopyOptions.TYPE_ANNOTATIONS, locationId);
            }
            else
            {
                if (!committed)
                {
                    SwitchToMethod("xml");
                }

                NextReceiver.Append(item);
            }
        }
    }
}
