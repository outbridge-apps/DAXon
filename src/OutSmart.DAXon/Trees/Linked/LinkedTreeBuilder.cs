////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Trees.Linked
{
    internal sealed class LinkedTreeBuilder : Builder
    {
        private ParentNodeImpl currentNode;
        private INodeFactory nodeFactory;
        private int[] size = new int[100]; // stack of number of children for each open node
        private int depth;
        private List<NodeImpl[]> arrays = new List<NodeImpl[]>(20); // reusable arrays for creating nodes
        private readonly Stack<NamespaceMap> namespaceStack = new Stack<NamespaceMap>();
        private bool allocateSequenceNumbers = true;
        private int nextNodeNumber = 1;
        private bool inLedger;   // the tree grew large and is in the ledger of the call building it

        // Bytes a node adds to the tree: its object, its child slot, its attributes and text, and a large namespace map of
        // its own (one binding a prefix per level copies its parent's).
        private void Charge(long bytes)
        {
            if (currentRoot is DocumentImpl doc)
            {
                doc.heldBytes += bytes;
                if (!inLedger && doc.heldBytes >= MemoryLedger.Big)
                {
                    inLedger = true;
                    MemoryLedger.Hold(doc, DocumentImpl.HeldSizer);
                }
            }
        }

        private static long ElementBytes(IAttributeMap attributes, NamespaceMap namespaces, NamespaceMap parentNamespaces)
        {
            long bytes = 128;
            foreach (AttributeInfo att in attributes)
            {
                bytes += 88 + 2L * att.Value.Length;
            }

            int bindings = namespaces == null || ReferenceEquals(namespaces, parentNamespaces) ? 0 : namespaces.Size();
            return bindings >= 256 ? bytes + 80 + 16L * bindings : bytes;
        }

        public override NodeInfo CurrentRoot
        {
            get
            {
                NodeInfo physicalRoot = currentRoot;
                if (physicalRoot is DocumentImpl && ((DocumentImpl)physicalRoot).IsImaginary())
                {
                    return ((DocumentImpl)physicalRoot).DocumentElement;
                }
                else
                {
                    return physicalRoot;
                }
            }
        }

        public ParentNodeImpl CurrentParentNode => currentNode;

        public LinkedTreeBuilder(PipelineConfiguration pipe, Durability durability) : base(pipe)
        {
            this.durability = durability;
            nodeFactory = DefaultNodeFactory.THE_INSTANCE;
        }

        public override void SetDurability(Durability durability)
        {
            if (this.durability != Durability.MUTABLE)
            {
                this.durability = durability; // TODO: mutability and durability should be orthogonal
            }
        }

        public override void Reset()
        {
            base.Reset();
            currentNode = null;
            nodeFactory = DefaultNodeFactory.THE_INSTANCE;
            depth = 0;
            allocateSequenceNumbers = true;
            nextNodeNumber = 1;
        }

        public void SetAllocateSequenceNumbers(bool allocate)
        {
            allocateSequenceNumbers = allocate;
        }

        public void SetNodeFactory(INodeFactory factory)
        {
            nodeFactory = factory;
        }

        /// <summary>
        /// Open the stream of IReceiver events
        /// </summary>
        public override void Open()
        {
            started = true;
            depth = 0;
            size[depth] = 0;
            if (arrays == null)
            {
                arrays = new List<NodeImpl[]>(20);
            }

            if (useEventLocation)
            {
                object copier = GetPipelineConfiguration().GetComponent(typeof(ICopyInformee).FullName);
                if (copier is LocationCopier)
                {
                    SetSystemId(((LocationCopier)copier).GetSystemId());
                }
            }

            base.Open();
        }

        /// <summary>
        /// Open the stream of IReceiver events
        /// </summary>
        public override void StartDocument(int properties)
        {
            DocumentImpl doc = new DocumentImpl();
            doc.SetMutable(durability == Durability.MUTABLE);
            currentRoot = doc;
            inLedger = false;
            doc.heldBytes = 400;   // the document node and the builder's share of the tree
            doc.SetSystemId(GetSystemId());
            doc.SetBaseURI(BaseURI);
            doc.SetConfiguration(config);
            currentNode = doc;
            depth = 0;
            size[depth] = 0;
            if (arrays == null)
            {
                arrays = new List<NodeImpl[]>(20);
            }

            doc.SetRawSequenceNumber(0);
            if (lineNumbering)
            {
                doc.SetLineNumbering();
            }
        }

        /// <summary>
        /// Notify the end of the document
        /// </summary>
        public override void EndDocument()
        {
            currentNode.Compact(size[depth]);
        }

        /// <summary>
        /// Close the stream of IReceiver events
        /// </summary>
        public override void Close()
        {
            if (currentNode == null)
            {
                return; // can be called twice on an error path
            }

            currentNode.Compact(size[depth]);
            currentNode = null;
            if (!inLedger && currentRoot is DocumentImpl doc)
            {
                inLedger = true;
                MemoryLedger.Active?.Sample(doc, doc.heldBytes);
            }

            // we're not going to use this Builder again so give the garbage collector
            // something to play with
            arrays = null;
            base.Close();
            nodeFactory = DefaultNodeFactory.THE_INSTANCE;
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap suppliedAttributes, NamespaceMap namespaces, ILocation location, int properties)
        {

            if (currentNode == null)
            {
                StartDocument(ReceiverOption.NONE);
                ((DocumentImpl)currentRoot).SetImaginary(true);
            }

            bool isNilled = ReceiverOption.Contains(properties, ReceiverOption.NILLED_ELEMENT);
            namespaceStack.Push(namespaces);
            bool isTopWithinEntity = location is ISourceLocator && ((ISourceLocator)location).LevelInEntity == 0;
            AttributeInfo xmlId = suppliedAttributes.Get(NamespaceUri.XML, "id");
            if (xmlId != null && Whitespace.ContainsWhitespace(StringTool.CodePoints(xmlId.Value)))
            {
                suppliedAttributes = suppliedAttributes.Put(new AttributeInfo(xmlId.GetNodeName(), xmlId.GetType(), Whitespace.Trim(xmlId.Value), xmlId.GetLocation(), xmlId.GetProperties()));
            }

            if (location.GetSystemId() == null || (!useEventLocation && GetSystemId() != null))
            {
                // Bug 5800. A constructed node takes the builder's URI (the static base URI), as in the tiny tree.
                location = new Loc(GetSystemId(), location.GetLineNumber(), location.GetColumnNumber());
            }

            ElementImpl elem = nodeFactory.MakeElementNode(currentNode, elemName, type, isNilled, suppliedAttributes, namespaceStack.Peek(), pipe, location, allocateSequenceNumbers ? nextNodeNumber++ : -1);
            Charge(ElementBytes(suppliedAttributes, namespaces, (currentNode as ElementImpl)?.AllNamespaces));

            // the initial array used for pointing to children will be discarded when the exact number
            // of children in known. Therefore, it can be reused. So we allocate an initial array from
            // a pool of reusable arrays. A nesting depth of >20 is so rare that we don't bother.
            while (depth >= arrays.Count)
            {
                arrays.Add(new NodeImpl[20]);
            }

            elem.SetChildren(arrays[depth]);
            currentNode.AddChild(elem, size[depth]++);
            if (depth >= size.Length - 1)
            {
                Array.Resize(ref size, size.Length * 2);
            }

            size[++depth] = 0;
            if (currentNode is ITreeInfo)
            {
                ((DocumentImpl)currentNode).DocumentElement = elem;
            }

            if (isTopWithinEntity)
            {
                currentNode.PhysicalRoot.MarkTopWithinEntity(elem);
            }

            currentNode = elem;
        }

        /// <summary>
        /// Notify the end of an element
        /// </summary>
        public override void EndElement()
        {

            currentNode.Compact(size[depth]);
            depth--;
            currentNode = (ParentNodeImpl)currentNode.GetParent();
            namespaceStack.Pop();
        }

        /// <summary>
        /// Notify a text node. Adjacent text nodes must have already been merged
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {

            if (!chars.IsEmpty())
            {
                UnicodeString t = chars.Tidy();
                Charge(64 + 2 * t.Length());
                NodeInfo prev = currentNode.GetNthChild(size[depth] - 1);
                if (prev is TextImpl)
                {
                    ((TextImpl)prev).AppendStringValue(t);
                }
                else
                {
                    TextImpl n = nodeFactory.MakeTextNode(currentNode, t);

                    //TextImpl n = new TextImpl(chars.toString());
                    currentNode.AddChild(n, size[depth]++);
                }
            }
        }

        /// <summary>
        /// Notify a processing instruction
        /// </summary>
        public override void ProcessingInstruction(string name, UnicodeString remainder, ILocation locationId, int properties)
        {
            ProcInstImpl pi = new ProcInstImpl(name, remainder.Tidy());
            currentNode.AddChild(pi, size[depth]++);
            pi.SetLocation(LeafSystemId(locationId), locationId.GetLineNumber(), locationId.GetColumnNumber());
        }

        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            CommentImpl comment = new CommentImpl(chars.Tidy());
            currentNode.AddChild(comment, size[depth]++);
            comment.SetLocation(LeafSystemId(locationId), locationId.GetLineNumber(), locationId.GetColumnNumber());
        }

        // As for elements (and in the tiny tree): a constructed node has the builder's URI, not the instruction's.
        private string LeafSystemId(ILocation locationId)
        {
            return !useEventLocation && GetSystemId() != null ? GetSystemId() : locationId.GetSystemId();
        }

        public void GraftElement(ElementImpl element)
        {
            currentNode.AddChild(element, size[depth]++);
        }

        public override void SetUnparsedEntity(string name, string uri, string publicId)
        {
            if (((DocumentImpl)currentRoot).GetUnparsedEntity(name) == null)
            {

                // bug 2187
                ((DocumentImpl)currentRoot).SetUnparsedEntity(name, uri, publicId);
            }
        }

        public override BuilderMonitor GetBuilderMonitor()
        {
            return new LinkedBuilderMonitor(this);
        }

        // Inner class DefaultNodeFactory. This creates the nodes in the tree.
        // It can be overridden, e.g. when building the stylesheet tree
        private sealed class DefaultNodeFactory : INodeFactory
        {
            public static DefaultNodeFactory THE_INSTANCE = new DefaultNodeFactory();
            public ElementImpl MakeElementNode(NodeInfo parent, INodeName nodeName, ISchemaType elementType, bool isNilled, IAttributeMap attlist, NamespaceMap namespaces, PipelineConfiguration pipe, ILocation locationId, int sequenceNumber)
            {
                ElementImpl e = new ElementImpl();
                e.SetNamespaceMap(namespaces);
                e.Initialise(nodeName, elementType, attlist, parent, sequenceNumber);
                if (isNilled)
                {
                    e.SetNilled();
                }

                if (locationId != Loc.NONE && sequenceNumber >= 0)
                {
                    string baseURI = locationId.GetSystemId();
                    int lineNumber = locationId.GetLineNumber();
                    int columnNumber = locationId.GetColumnNumber();
                    e.SetLocation(baseURI, lineNumber, columnNumber);
                }

                return e;
            }

            public TextImpl MakeTextNode(NodeInfo parent, UnicodeString content)
            {
                return new TextImpl(content);
            }
        }
    }
}
