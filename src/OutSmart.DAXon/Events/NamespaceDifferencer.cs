////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Events
{
    internal sealed class NamespaceDifferencer : ProxyReceiver
    {
        private readonly bool undeclareNamespaces;
        private readonly Stack<NamespaceMap> namespaceStack = new Stack<NamespaceMap>();
        public NamespaceDifferencer(IReceiver next) : base(next)
        {
            undeclareNamespaces = false;
            namespaceStack.Push(NamespaceMap.EmptyMap());
        }

        public NamespaceDifferencer(IReceiver next, Properties details) : this(next)
        {
            undeclareNamespaces = details.GetProperty(DAXonOutputKeys.UNDECLARE_PREFIXES) == "yes";
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            NamespaceMap parentMap = namespaceStack.Peek();
            namespaceStack.Push(namespaces);
            NamespaceMap delta = GetDifferences(namespaces, parentMap, elemName.HasURI(NamespaceUri.NULL));
            nextReceiver.StartElement(elemName, type, attributes, delta, location, properties);
        }

        public override void EndElement()
        {
            namespaceStack.Pop();
            base.EndElement();
        }

        private NamespaceMap GetDifferences(NamespaceMap thisMap, NamespaceMap parentMap, bool elementInDefaultNamespace)
        {
            if (thisMap != parentMap)
            {
                NamespaceMap delta = NamespaceDeltaMap.EmptyMap();
                foreach (NamespaceBinding nb in thisMap)
                {
                    NamespaceUri parentUri = parentMap.GetNamespaceUri(nb.GetPrefix());
                    if (parentUri == null)
                    {
                        delta = delta.Put(nb.GetPrefix(), nb.GetNamespaceUri());
                    }
                    else if (!parentUri.Equals(nb.GetNamespaceUri()))
                    {
                        delta = delta.Put(nb.GetPrefix(), nb.GetNamespaceUri());
                    }
                }

                if (undeclareNamespaces)
                {
                    foreach (NamespaceBinding nb in parentMap)
                    {
                        if (thisMap.GetNamespaceUri(nb.GetPrefix()) == null)
                        {
                            delta = delta.Put(nb.GetPrefix(), NamespaceUri.NULL);
                        }
                    }
                }
                else
                {

                    // undeclare the default namespace if the parent element has a default namespace and the child does not
                    // See also bug 4696, test
                    if (!parentMap.DefaultNamespace.IsEmpty() && thisMap.DefaultNamespace.IsEmpty())
                    {
                        delta = delta.Put("", NamespaceUri.NULL);
                    }
                }

                return delta;
            }

            return NamespaceMap.EmptyMap();
        }
    }
}