////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Trees.Linked
{
    public sealed class DocumentImpl : ParentNodeImpl, ITreeInfo, IMutableDocumentInfo
    {
        // What the tree holds, as its builder counts it, for the memory limit of the call building it.
        internal long heldBytes;
        internal static readonly Func<object, long> HeldSizer = d => ((DocumentImpl)d).heldBytes;

        private ElementImpl documentElement;
        // Built whole before it is published, like elementList: runs over a shared document call id() at once.
        private ConcurrentDictionary<string, NodeInfo> idTable;
        private long documentNumber;
        private string baseURI;
        private Dictionary<string, string[]> entityTable;
        private HashSet<ElementImpl> nilledElements;
        private HashSet<ElementImpl> topWithinEntityElements;
        // The elements of each name, built once and shared by the runs over this document, which may be at once: published
        // whole into a concurrent map (an IntHashMap filled by two threads could lose or mix entries).
        private ConcurrentDictionary<int, IList<NodeInfo>> elementList;
        private Dictionary<string, object> userData;
        private Configuration config;
        private LineNumberMap lineNumberMap;
        private SystemIdMap systemIdMap = new SystemIdMap();
        private bool imaginary;
        private Durability durability;
        private ISpaceStrippingRule spaceStrippingRule = NoElementsSpaceStrippingRule.GetInstance();

        // Base URIs resolved for inner elements; shared by threads reading the tree, so locked.
        private Dictionary<ElementImpl, string> knownBaseUris;
        private readonly object baseUriLock = new object();

        public ElementImpl DocumentElement { get => documentElement; set => documentElement = value; }

        public override NodeInfo Root => this;

        public override DocumentImpl PhysicalRoot => this;

        public IEnumerator<string> UnparsedEntityNames
        {
            get
            {
                if (entityTable == null)
                {
                    IList<string> ls = new List<string>();
                    return ls.GetEnumerator();
                }
                else
                {
                    return entityTable.Keys.GetEnumerator();
                }
            }
        }

        public ISpaceStrippingRule SpaceStrippingRule
        {
            get => spaceStrippingRule; set
            {
                this.spaceStrippingRule = value;
            }
        }
        public DocumentImpl()
        {
            SetRawParent(null);
        }

        public NodeInfo GetRootNode()
        {
            return this;
        }

        public void SetConfiguration(Configuration config)
        {
            this.config = config;
            documentNumber = config.DocumentNumberAllocator.AllocateDocumentNumber();
        }

        public override Configuration GetConfiguration()
        {
            return config;
        }

        public bool IsMutable()
        {
            return durability == Durability.MUTABLE;
        }

        public Durability GetDurability()
        {
            return durability;
        }

        public void SetMutable(bool mutable)
        {
            this.durability = mutable ? Durability.MUTABLE : Durability.LASTING;
        }

        public override NamePool GetNamePool()
        {
            return config.GetNamePool();
        }

        public override Builder NewBuilder()
        {
            LinkedTreeBuilder builder = new LinkedTreeBuilder(config.MakePipelineConfiguration(), Durability.MUTABLE);
            builder.SetAllocateSequenceNumbers(false);
            return builder;
        }

        public void SetImaginary(bool imaginary)
        {
            this.imaginary = imaginary;
        }

        public bool IsImaginary()
        {
            return imaginary;
        }

        public bool IsTyped()
        {
            return documentElement != null && documentElement.GetSchemaType() != Untyped.INSTANCE;
        }

        /// <summary>
        /// Get the unique document number
        /// </summary>
        public long GetDocumentNumber()
        {
            return documentNumber;
        }

        /// <summary>
        /// Get the unique document number
        /// </summary>
        public void GraftLocationMap(DocumentImpl original)
        {
            systemIdMap = original.systemIdMap;
            lineNumberMap = original.lineNumberMap;
        }

        /// <summary>
        /// Set the system id (base URI) of this node
        /// </summary>
        public override void SetSystemId(string uri)
        {
            if (uri == null)
            {
                uri = "";
            }

            systemIdMap.SetSystemId(GetRawSequenceNumber(), uri);
        }

        public override string GetSystemId()
        {
            return systemIdMap.GetSystemId(GetRawSequenceNumber());
        }

        public void SetBaseURI(string uri)
        {
            baseURI = uri;
        }

        public override string GetBaseURI()
        {
            if (baseURI != null)
            {
                return baseURI;
            }

            return GetSystemId();
        }

        public void SetSystemId(int seq, string uri)
        {
            if (uri == null)
            {
                uri = "";
            }

            systemIdMap.SetSystemId(seq, uri);
        }

        public string GetSystemId(int seq)
        {
            return systemIdMap.GetSystemId(seq);
        }

        public void SetLineNumbering()
        {
            lineNumberMap = new LineNumberMap();
            lineNumberMap.SetLineAndColumn(GetRawSequenceNumber(), 0, -1);
        }

        public void SetLineAndColumn(int sequence, int line, int column)
        {
            if (lineNumberMap != null && sequence >= 0)
            {
                lineNumberMap.SetLineAndColumn(sequence, line, column);
            }
        }

        public int GetLineNumber(int sequence)
        {
            if (lineNumberMap != null && sequence >= 0)
            {
                return lineNumberMap.GetLineNumber(sequence);
            }

            return -1;
        }

        public int GetColumnNumber(int sequence)
        {
            if (lineNumberMap != null && sequence >= 0)
            {
                return lineNumberMap.GetColumnNumber(sequence);
            }

            return -1;
        }

        public void AddNilledElement(ElementImpl element)
        {
            if (nilledElements == null)
            {
                nilledElements = new HashSet<ElementImpl>();
            }

            nilledElements.Add(element);
        }

        public bool IsNilledElement(ElementImpl element)
        {
            return nilledElements != null && nilledElements.Contains(element);
        }

        public void MarkTopWithinEntity(ElementImpl element)
        {
            if (topWithinEntityElements == null)
            {
                topWithinEntityElements = new HashSet<ElementImpl>();
            }

            topWithinEntityElements.Add(element);
        }

        public bool IsTopWithinEntity(ElementImpl element)
        {
            return topWithinEntityElements != null && topWithinEntityElements.Contains(element);
        }

        internal string GetKnownBaseUri(ElementImpl element)
        {
            lock (baseUriLock)
            {
                return knownBaseUris != null && knownBaseUris.TryGetValue(element, out string uri) ? uri : null;
            }
        }

        internal void SetKnownBaseUri(ElementImpl element, string uri)
        {
            lock (baseUriLock)
            {
                (knownBaseUris ??= new Dictionary<ElementImpl, string>())[element] = uri;
            }
        }

        public override int GetLineNumber()
        {
            return 0;
        }

        public override int GetNodeKind()
        {
            return Types.Type.DOCUMENT;
        }

        public override
        NodeImpl GetNextSibling()
        {
            return null;
        }

        public override NodeImpl GetPreviousSibling()
        {
            return null;
        }

        public override void GenerateId(StringBuilder buffer)
        {
            buffer.Append('d');
            buffer.Append(documentNumber);
        }

        public IAxisIterator GetAllElements(int fingerprint)
        {
            ConcurrentDictionary<int, IList<NodeInfo>> eList = LazyInitializer.EnsureInitialized(ref elementList);
            if (!eList.TryGetValue(fingerprint, out IList<NodeInfo> list))
            {
                // The walk visits every node (350 ms for 400 000 elements) under the run's time limit; a walk the limit
                // stops is not kept.
                var found = new List<NodeInfo>(500);
                NodeImpl next = GetNextInDocument(this);
                while (next != null)
                {
                    Controller.CheckActiveTimeout();
                    if (next.GetNodeKind() == Types.Type.ELEMENT && next.Fingerprint == fingerprint)
                    {
                        found.Add(next);
                    }

                    next = next.GetNextInDocument(this);
                }

                list = eList.GetOrAdd(fingerprint, found);
            }

            return new NodeListIterator(list);
        }

        public void DeIndex(NodeImpl node)
        {
            if (node is ElementImpl)
            {
                ConcurrentDictionary<int, IList<NodeInfo>> eList = elementList;
                if (eList != null)
                {
                    if (!eList.TryGetValue(node.Fingerprint, out IList<NodeInfo> list))
                    {
                        return;
                    }

                    list.Remove(node);
                }

                if (node.IsId())
                {
                    DeregisterID(node.GetStringValue());
                }
            }
            else if (node is AttributeImpl)
            {
                if (node.IsId())
                {
                    DeregisterID(node.GetStringValue());
                }
            }
        }

        private ConcurrentDictionary<string, NodeInfo> IndexIDs()
        {
            // The walk visits every node under the run's time limit; a walk the limit stops is not kept.
            var table = new ConcurrentDictionary<string, NodeInfo>();
            NodeImpl curr = this;
            NodeImpl root = curr;
            while (curr != null)
            {
                Controller.CheckActiveTimeout();
                if (curr.GetNodeKind() == Types.Type.ELEMENT)
                {

                    ElementImpl e = (ElementImpl)curr;
                    if (e.IsId())
                    {
                        table.TryAdd(Whitespace.Trim(e.GetStringValue()), e);
                    }

                    IAttributeMap atts = e.Attributes();
                    foreach (AttributeInfo att in atts)
                    {
                        if (att.IsId() && NameChecker.IsValidNCName(Whitespace.Trim(att.Value)))
                        {

                            // don't index any invalid IDs - these can arise when using a non-validating parser
                            table.TryAdd(Whitespace.Trim(att.Value), e);
                        }
                    }
                }

                curr = curr.GetNextInDocument(root);
            }

            return Interlocked.CompareExchange(ref idTable, table, null) ?? table;
        }

        public void RegisterID(NodeInfo e, string id)
        {

            // the XPath spec (5.2.1) says ignore the second ID if it's not unique
            LazyInitializer.EnsureInitialized(ref idTable).TryAdd(id, e);
        }

        public NodeInfo SelectID(string id, bool getParent)
        {
            ConcurrentDictionary<string, NodeInfo> table = Volatile.Read(ref idTable) ?? IndexIDs();
            table.TryGetValue(id, out NodeInfo node);
            if (node != null && getParent && node.IsId() && node.UnicodeStringValue.Equals(id))
            {
                node = node.GetParent();
            }

            return node;
        }

        public void DeregisterID(string id)
        {
            id = Whitespace.Trim(id);
            idTable?.TryRemove(id, out _);
        }

        public void SetUnparsedEntity(string name, string uri, string publicId)
        {

            if (entityTable == null)
            {
                entityTable = new Dictionary<string, string[]>(10);
            }

            string[] ids = new string[2];
            ids[0] = uri;
            ids[1] = publicId;
            entityTable[name] = ids;
        }

        public String[] GetUnparsedEntity(string name)
        {
            if (entityTable == null)
            {
                return null;
            }

            return entityTable.GetOrDefault(name);
        }

        public override ISchemaType GetSchemaType()
        {
            if (documentElement == null || documentElement.GetSchemaType() == Untyped.INSTANCE)
            {
                return Untyped.INSTANCE;
            }
            else
            {
                return AnyType.INSTANCE;
            }
        }

        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            @out.StartDocument(CopyOptions.GetStartDocumentProperties(copyOptions));

            // copy any unparsed entities
            IEnumerator<string> names = UnparsedEntityNames;
            while (names.MoveNext())
            {
                string name = names.Current;
                string[] details = GetUnparsedEntity(name);
                @out.SetUnparsedEntity(name, details[0], details[1]);
            }


            // copy the children
            NodeImpl next = GetFirstChild();
            while (next != null)
            {
                next.Copy(@out, copyOptions, locationId);
                next = next.GetNextSibling();
            }

            @out.EndDocument();
        }

        public override void ReplaceStringValue(UnicodeString stringValue)
        {
            throw new NotSupportedException("Cannot replace the value of a document node");
        }

        public void ResetIndexes()
        {
            idTable = null;
            elementList = null;
        }

        public void SetUserData(string key, object value)
        {
            if (userData == null)
            {
                userData = new Dictionary<string, object>(4);
            }

            if (value == null)
            {
                userData.Remove(key);
            }
            else
            {
                userData[key] = value;
            }
        }

        public object GetUserData(string key)
        {
            if (userData == null)
            {
                return null;
            }
            else
            {
                return userData.GetOrDefault(key);
            }
        }
    }
}