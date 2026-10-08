////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Events
{
    public abstract class Outputter : IReceiver
    {
        protected PipelineConfiguration pipelineConfiguration;
        protected string systemId = null;
        public virtual void SetPipelineConfiguration(PipelineConfiguration pipe)
        {
            this.pipelineConfiguration = pipe;
        }

        public virtual PipelineConfiguration GetPipelineConfiguration()
        {
            return pipelineConfiguration;
        }

        public Configuration GetConfiguration()
        {
            return pipelineConfiguration.GetConfiguration();
        }

        public virtual void SetSystemId(string systemId)
        {
            this.systemId = systemId;
        }

        public virtual string GetSystemId()
        {
            return systemId;
        }

        public virtual void Open()
        {
        }

        public abstract void StartDocument(int properties);
        public abstract void EndDocument();
        public virtual void SetUnparsedEntity(string name, string systemID, string publicID)
        {
        }

        public abstract void StartElement(INodeName elemName, ISchemaType typeCode, ILocation location, int properties);
        public virtual void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {

            // This is a default implementation which is not particularly efficient. An Outputter that feeds
            // directly into a IReceiver should try to avoid decomposing the attributes and namespaces.
            SpreadStartElement(elemName, type, attributes, namespaces, location, properties, this);
        }

        protected virtual void SpreadStartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties, Outputter @out)
        {
            @out.StartElement(elemName, type, location, properties);

            // index loops: alloc-free, unlike the NamespaceMap/IAttributeMap enumerators
            string[] nsPrefixes = namespaces.PrefixArray;
            NamespaceUri[] nsUris = namespaces.URIsAsArray;
            for (int i = 0; i < nsPrefixes.Length; i++)
            {
                @out.Namespace(nsPrefixes[i], nsUris[i], properties);
            }

            int attCount = attributes.Size();
            for (int i = 0; i < attCount; i++)
            {
                AttributeInfo att = attributes.ItemAt(i);
                @out.Attribute(att.GetNodeName(), att.GetType(), att.Value, att.GetLocation(), att.GetProperties());
            }

            @out.StartContent();
        }

        public abstract void Namespace(string prefix, NamespaceUri namespaceUri, int properties);
        public virtual void Namespaces(INamespaceBindingSet bindings, int properties)
        {

            // Optimized in ComplexContentOutputter subclass
            foreach (NamespaceBinding nb in bindings)
            {
                Namespace(nb.GetPrefix(), nb.GetNamespaceUri(), properties);
            }
        }

        public abstract void Attribute(INodeName attName, ISimpleType typeCode, string value, ILocation location, int properties);
        public virtual void StartContent()
        {
        }

        public abstract void EndElement();
        public abstract void Characters(UnicodeString chars, ILocation location, int properties);
        public abstract void ProcessingInstruction(string name, UnicodeString data, ILocation location, int properties);
        public abstract void Comment(UnicodeString content, ILocation location, int properties);
        public virtual void Append(IItem item, ILocation locationId, int properties)
        {
            throw new NotSupportedException();
        }

        public virtual void Append(IItem item)
        {
            Append(item, Loc.NONE, ReceiverOption.ALL_NAMESPACES);
        }

        public virtual IUniStringConsumer GetStringReceiver(bool asTextNode, ILocation loc)
        {
            if (asTextNode)
            {
                return new AnonymousAbstractUniStringConsumer(this, loc);
            }
            else
            {
                return new AnonymousAbstractUniStringConsumer1(this);
            }
        }

        public virtual void Close()
        {
        }

        // Abort-path release (see IReceiver): no events, idempotent.
        public virtual void Dispose()
        {
        }

        public virtual bool UsesTypeAnnotations()
        {
            return false;
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual bool HandlesAppend() => false; // upstream Receiver default: callers must decompose items

        private sealed class AnonymousAbstractUniStringConsumer : AbstractUniStringConsumer
        {

            private readonly Outputter parent;
            private readonly ILocation loc;
            readonly UniStringCollector buffer = new UniStringCollector();
            public AnonymousAbstractUniStringConsumer(Outputter parent, ILocation loc)
            {
                this.parent = parent;
                this.loc = loc;
            }
            public override IUniStringConsumer Accept(UnicodeString chars)
            {
                buffer.Accept(chars);
                return this;
            }

            public override void Close()
            {
                parent.Characters(buffer.ToUnicodeString(), loc, ReceiverOption.NONE);
            }
        }

        private sealed class AnonymousAbstractUniStringConsumer1 : AbstractUniStringConsumer
        {

            private readonly Outputter parent;
            readonly UniStringCollector buffer = new UniStringCollector();
            public AnonymousAbstractUniStringConsumer1(Outputter parent)
            {
                this.parent = parent;
            }
            public override IUniStringConsumer Accept(UnicodeString chars)
            {
                buffer.Accept(chars);
                return this;
            }

            public override void Close()
            {
                parent.Append(new StringValue(buffer.ToUnicodeString()));
            }
        }
    }
}