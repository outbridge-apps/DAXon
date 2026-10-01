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
    // Faithful port of net.sf.saxon.event.OutputterEvent (Saxon 12.9). NEW in the port — needed by the
    // real OutputterEventBuffer (xsl:try rollback buffering).
    // Represents one event passed to an Outputter, retaining enough information to be replayed later.
    internal abstract class OutputterEvent
    {
        public virtual void Replay(Outputter @out)
        {
        }

        internal sealed class StartDocument : OutputterEvent
        {
            internal readonly int properties;
            public StartDocument(int properties)
            {
                this.properties = properties;
            }

            public override void Replay(Outputter @out)
            {
                @out.StartDocument(properties);
            }
        }

        internal sealed class EndDocument : OutputterEvent
        {
            public override void Replay(Outputter @out)
            {
                @out.EndDocument();
            }
        }

        internal sealed class StartElement : OutputterEvent
        {
            internal readonly INodeName name;
            internal readonly ISchemaType type;
            internal readonly ILocation location;
            internal readonly int properties;

            public StartElement(INodeName name, ISchemaType type, ILocation location, int properties)
            {
                this.name = name;
                this.type = type;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(Outputter @out)
            {
                @out.StartElement(name, type, location, properties);
            }
        }

        internal sealed class Attribute : OutputterEvent
        {
            internal readonly INodeName name;
            internal readonly ISimpleType type;
            internal readonly string value;
            internal readonly ILocation location;
            internal readonly int properties;

            public Attribute(INodeName name, ISimpleType type, string value, ILocation location, int properties)
            {
                this.name = name;
                this.type = type;
                this.value = value;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(Outputter @out)
            {
                @out.Attribute(name, type, value, location, properties);
            }
        }

        internal sealed class Namespace : OutputterEvent
        {
            internal readonly string prefix;
            internal readonly NamespaceUri uri;
            internal readonly int properties;

            public Namespace(string prefix, NamespaceUri uri, int properties)
            {
                this.prefix = prefix;
                this.uri = uri;
                this.properties = properties;
            }

            public override void Replay(Outputter @out)
            {
                @out.Namespace(prefix, uri, properties);
            }
        }

        internal sealed class StartContent : OutputterEvent
        {
            public override void Replay(Outputter @out)
            {
                @out.StartContent();
            }
        }

        internal sealed class EndElement : OutputterEvent
        {
            public override void Replay(Outputter @out)
            {
                @out.EndElement();
            }
        }

        internal sealed class Text : OutputterEvent
        {
            internal readonly UnicodeString content;
            internal readonly ILocation location;
            internal readonly int properties;

            public Text(UnicodeString content, ILocation location, int properties)
            {
                this.content = content;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(Outputter @out)
            {
                @out.Characters(content, location, properties);
            }
        }

        internal sealed class Comment : OutputterEvent
        {
            internal readonly UnicodeString content;
            internal readonly ILocation location;
            internal readonly int properties;

            public Comment(UnicodeString content, ILocation location, int properties)
            {
                this.content = content;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(Outputter @out)
            {
                @out.Comment(content, location, properties);
            }
        }

        internal sealed class ProcessingInstruction : OutputterEvent
        {
            internal readonly string target;
            internal readonly UnicodeString content;
            internal readonly ILocation location;
            internal readonly int properties;

            public ProcessingInstruction(string target, UnicodeString content, ILocation location, int properties)
            {
                this.target = target;
                this.content = content;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(Outputter @out)
            {
                @out.ProcessingInstruction(target, content, location, properties);
            }
        }

        /// <summary>
        /// An arbitrary item sent to the event stream in composed form (an atomic value,
        /// or an entire element or document).
        /// </summary>
        internal sealed class Append : OutputterEvent
        {
            internal readonly IItem item;
            internal readonly ILocation location;
            internal readonly int properties;

            public Append(IItem item, ILocation location, int properties)
            {
                this.item = item;
                this.location = location;
                this.properties = properties;
            }

            public override void Replay(Outputter @out)
            {
                @out.Append(item, location, properties);
            }
        }
    }
}
