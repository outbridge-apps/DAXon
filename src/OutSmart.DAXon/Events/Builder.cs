////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using System;
using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Events
{
    public abstract class Builder : IReceiver
    {
        /// <summary>
        /// Constant denoting a request for the default tree model
        /// </summary>
        public const int UNSPECIFIED_TREE_MODEL = -1;
        /// <summary>
        /// Constant denoting the "linked tree" in which each node is represented as an object
        /// </summary>
        public const int LINKED_TREE = 0;
        public const int TINY_TREE = 1;
        public const int TINY_TREE_CONDENSED = 2;
        public const int JDOM_TREE = 3;
        public const int JDOM2_TREE = 4;
        public const int AXIOM_TREE = 5;
        public const int DOMINO_TREE = 6;
        public const int MUTABLE_LINKED_TREE = 7;
        protected PipelineConfiguration pipe;
        protected Configuration config;
        protected NamePool namePool;
        protected string systemId;
        protected string baseURI;
        protected bool uniformBaseURI = true;
        protected NodeInfo currentRoot;
        protected bool lineNumbering;
        protected bool useEventLocation = true;
        protected Durability durability = Durability.LASTING;
        protected bool started;
        protected bool timing;
        protected bool opened;
        private long startTime;

        public virtual string BaseURI
        {
            get => baseURI; set
            {
                this.baseURI = value;
            }
        }

        public virtual NodeInfo CurrentRoot => currentRoot;
        public Builder()
        {
        }

        public Builder(PipelineConfiguration pipe)
        {
            this.pipe = pipe;
            config = pipe.GetConfiguration();
            lineNumbering = config.IsLineNumbering();
            namePool = config.GetNamePool();
        }

        public virtual void SetPipelineConfiguration(PipelineConfiguration pipe)
        {
            this.pipe = pipe;
            config = pipe.GetConfiguration();
            lineNumbering = lineNumbering || config.IsLineNumbering();
            namePool = config.GetNamePool();
        }

        public virtual PipelineConfiguration GetPipelineConfiguration()
        {
            return pipe;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual BuilderMonitor GetBuilderMonitor()
        {
            return null;
        }

        public virtual void SetUseEventLocation(bool useEventLocation)
        {
            this.useEventLocation = useEventLocation;
        }

        public virtual bool IsUseEventLocation()
        {
            return useEventLocation;
        }

        public virtual void SetSystemId(string systemId)
        {
            this.systemId = systemId;
        }

        public virtual string GetSystemId()
        {
            return systemId;
        }

        public virtual void SetDurability(Durability durability)
        {
            this.durability = durability;
        }

        public virtual Durability GetDurability()
        {
            return durability;
        }

        public virtual void SetLineNumbering(bool lineNumbering)
        {
            this.lineNumbering = lineNumbering;
        }

        public virtual void SetTiming(bool on)
        {
            timing = on;
        }

        public virtual bool IsTiming()
        {
            return timing;
        }

        public virtual void Open()
        {
            if (timing && !opened)
            {
                string sysId = GetSystemId();
                if (sysId == null)
                {
                    sysId = "(unknown systemId)";
                }

                GetConfiguration().Logger.Info("Building tree for " + sysId + " using " + GetType());
                startTime = (DateTime.Now.Ticks * 100L);
            }

            opened = true;
        }

        // Abort-path release: the tree under construction is memory-only, nothing to free.
        public virtual void Dispose()
        {
        }

        public virtual void Close()
        {
            if (timing && opened)
            {
                long endTime = (DateTime.Now.Ticks * 100L);
                Logger logger = GetConfiguration().Logger;
                logger.Info("Tree built in " + Timer.ShowExecutionTimeNano(endTime - startTime));
                if (currentRoot is TinyDocumentImpl)
                {
                    ((TinyDocumentImpl)currentRoot).ShowSize(logger);
                }

                startTime = endTime;
            }

            opened = false;
        }

        public virtual bool UsesTypeAnnotations()
        {
            return true;
        }

        public virtual void Reset()
        {
            systemId = null;
            baseURI = null;
            currentRoot = null;
            lineNumbering = false;
            started = false;
            timing = false;
            opened = false;
        }
        public abstract void StartDocument(int arg0);
        public abstract void EndDocument();
        public abstract void SetUnparsedEntity(string arg0, string arg1, string arg2);
        public abstract void StartElement(INodeName arg0, ISchemaType arg1, IAttributeMap arg2, NamespaceMap arg3, ILocation arg4, int arg5);
        public abstract void EndElement();
        public abstract void Characters(UnicodeString arg0, ILocation arg1, int arg2);
        public abstract void ProcessingInstruction(string arg0, UnicodeString arg1, ILocation arg2, int arg3);
        public abstract void Comment(UnicodeString arg0, ILocation arg1, int arg2);
        // IReceiver Append default stubs.
        public virtual void Append(IItem item) { }
        public virtual void Append(IItem item, ILocation locationId, int copyNamespaces) { }
        public virtual bool HandlesAppend() => false;
    }
}
