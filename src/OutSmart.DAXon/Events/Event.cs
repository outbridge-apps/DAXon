////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Events
{
    internal abstract class Event
    {
        public virtual void Replay(IReceiver @out)
        {
        }

        /// <summary>
        /// Event representing start of document
        /// </summary>
        internal sealed class StartDocument : Event
        {
            readonly int properties;
            public StartDocument(int properties)
            {
                this.properties = properties;
            }

            public override void Replay(IReceiver @out)
            {
                @out.StartDocument(properties);
            }
        }

        /// <summary>
        /// Event representing end of document
        /// </summary>
        internal sealed class EndDocument : Event
        {
            public EndDocument()
            {
            }

            public override void Replay(IReceiver @out)
            {
                @out.EndDocument();
            }
        }

        /// <summary>
        /// Event representing the start of an element (including attributes or namespaces)
        /// </summary>
        internal sealed class StartElement : Event
        {
            readonly INodeName name;
            readonly ISchemaType type;
            readonly IAttributeMap attributes;
            readonly NamespaceMap namespaces;
            readonly ILocation location;
            readonly int properties;
            public StartElement(INodeName name, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
            {
                this.name = name;
                this.type = type;
                this.attributes = attributes;
                this.namespaces = namespaces;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(IReceiver @out)
            {
                @out.StartElement(name, type, attributes, namespaces, location, properties);
            }
        }

        /// <summary>
        /// Event representing the end of an element
        /// </summary>
        internal sealed class EndElement : Event
        {
            public EndElement()
            {
            }

            public override void Replay(IReceiver @out)
            {
                @out.EndElement();
            }
        }

        /// <summary>
        /// Event representing a text node
        /// </summary>
        internal sealed class Text : Event
        {
            readonly UnicodeString content;
            readonly ILocation location;
            readonly int properties;
            public Text(UnicodeString content, ILocation location, int properties)
            {
                this.content = content;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(IReceiver @out)
            {
                @out.Characters(content, location, properties);
            }
        }

        /// <summary>
        /// Event representing a comment node
        /// </summary>
        internal sealed class Comment : Event
        {
            readonly UnicodeString content;
            readonly ILocation location;
            readonly int properties;
            public Comment(UnicodeString content, ILocation location, int properties)
            {
                this.content = content;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(IReceiver @out)
            {
                @out.Comment(content, location, properties);
            }
        }

        /// <summary>
        /// Event representing a processing instruction node
        /// </summary>
        internal sealed class ProcessingInstruction : Event
        {
            readonly string target;
            readonly UnicodeString content;
            readonly ILocation location;
            readonly int properties;
            public ProcessingInstruction(string target, UnicodeString content, ILocation location, int properties)
            {
                this.target = target;
                this.content = content;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(IReceiver @out)
            {
                @out.ProcessingInstruction(target, content, location, properties);
            }
        }

        internal sealed class Append : Event
        {
            readonly IItem item;
            readonly ILocation location;
            readonly int properties;
            public Append(IItem item, ILocation location, int properties)
            {
                this.item = item;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(IReceiver @out)
            {
                @out.Append(item, location, properties);
            }
        }
    }
}