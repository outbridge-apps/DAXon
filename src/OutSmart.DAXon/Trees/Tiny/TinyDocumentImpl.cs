////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Text;
namespace OutSmart.DAXon.Trees.Tiny
{
    internal sealed class TinyDocumentImpl : TinyParentNodeImpl
    {
        private IntHashMap<IList<NodeInfo>> elementList;
        private string baseURI;

        /// <summary>
        /// Get the tree containing this node
        /// </summary>
        public override TinyTree Tree => tree;

        public override NodeInfo Root => this;
        public TinyDocumentImpl(TinyTree tree) : base(tree, 0)
        {
        }

        /// <summary>
        /// Get the configuration previously set using setConfiguration
        /// </summary>
        public override Configuration GetConfiguration()
        {
            return tree.GetConfiguration();
        }

        /// <summary>
        /// Set the system id of this node
        /// </summary>
        public override void SetSystemId(string uri)
        {
            tree.SetSystemId(nodeNr, uri);
        }

        /// <summary>
        /// Get the system id of this root node
        /// </summary>
        public override string GetSystemId()
        {
            return tree.GetSystemId(nodeNr);
        }

        /// <summary>
        /// Get the system id of this root node
        /// </summary>
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

        public override int GetLineNumber()
        {
            return 0;
        }

        public override int GetNodeKind()
        {
            return Types.Type.DOCUMENT;
        }

        public override TinyNodeImpl GetParent()
        {
            return null;
        }

        public override void GenerateId(StringBuilder buffer)
        {
            buffer.Append('d');
            AppendIdDigits(buffer, GetTreeInfo().GetDocumentNumber());
        }

        public override IAtomicSequence Atomize()
        {
            return StringValue.MakeUntypedAtomic(UnicodeStringValue);
        }

        public IAxisIterator GetAllElements(int fingerprint)
        {
            if (elementList == null)
            {
                elementList = new IntHashMap<IList<NodeInfo>>(20);
            }

            IList<NodeInfo> list = elementList[fingerprint];
            if (list == null)
            {
                list = MakeElementList(fingerprint);
                elementList.Put(fingerprint, list);
            }

            return new NodeListIterator(list);
        }

        IList<NodeInfo> MakeElementList(int fingerprint)
        {
            int size = tree.NumberOfNodes / 20;
            if (size > 100)
            {
                size = 100;
            }

            if (size < 20)
            {
                size = 20;
            }

            List<NodeInfo> list = new List<NodeInfo>(size);
            int i = nodeNr + 1;
            try
            {
                while (tree.depth[i] != 0)
                {
                    byte kind = tree.nodeKind[i];
                    if ((kind & 0x0f) == Types.Type.ELEMENT && (tree.nameCode[i] & 0xfffff) == fingerprint)
                    {
                        list.Add(tree.GetNode(i));
                    }

                    i++;
                }
            }
            catch (IndexOutOfRangeException)
            {

                // this shouldn't happen. If it does happen, it means the tree wasn't properly closed
                // during construction (there is no stopper node at the end). In this case, we'll recover
                return list;
            }

            /* TrimToSize: noop on List<T> -- list */
            return list;
        }

        public override ISchemaType GetSchemaType()
        {
            IAxisIterator children = IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT);
            NodeInfo node = children.Next();
            if (node == null || node.GetSchemaType() == Untyped.INSTANCE)
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
            if (tree.entityTable != null)
            {
                foreach (KeyValuePair<string, string[]> entry in tree.entityTable)
                {
                    string name = entry.Key;
                    string[] details = entry.Value;
                    string systemId = details[0];
                    string publicId = details[1];
                    @out.SetUnparsedEntity(name, systemId, publicId);
                }
            }


            // output the children
            foreach (NodeInfo child in Children())
            {
                child.Copy(@out, copyOptions, locationId);
            }

            @out.EndDocument();
        }

        public void ShowSize(Logger logger)
        {
            tree.ShowSize(logger);
        }

        public override int GetHashCode()
        {

            // Chosen to give a hashcode that is likely (a) to be distinct from other documents, and (b) to
            // be distinct from other nodes in the same document
            return (int)tree.GetDocumentNumber();
        }
    }
}
