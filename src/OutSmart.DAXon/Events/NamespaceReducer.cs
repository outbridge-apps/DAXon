////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Events
{
    internal sealed class NamespaceReducer : ProxyReceiver, INamespaceResolver
    {
        private readonly NamespaceBinding[] namespaces = new NamespaceBinding[50]; // all namespace codes currently declared
        private int namespacesSize; // all namespaces currently declared
        private int[] countStack = new int[50];
        private int depth;
        private bool[] disinheritStack = new bool[50];
        public NamespaceReducer(IReceiver next) : base(next)
        {
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaceMap, ILocation location, int properties)
        {
            nextReceiver.StartElement(elemName, type, attributes, namespaceMap, location, properties);

            // Record the current height of the namespace list so it can be reset at endElement time
            countStack[depth] = 0;
            disinheritStack[depth] = ReceiverOption.Contains(properties, ReceiverOption.DISINHERIT_NAMESPACES);
            if (++depth >= countStack.Length)
            {
                Array.Resize(ref countStack, depth * 2);
                Array.Resize(ref disinheritStack, depth * 2);
            }
        }

        //break;
        public override void EndElement()
        {
            if (depth-- == 0)
            {
                throw new InvalidOperationException("Attempt to output end tag with no matching start tag");
            }

            namespacesSize -= countStack[depth];
            nextReceiver.EndElement();
        }

        //break;
        public NamespaceUri GetURIForPrefix(string prefix, bool useDefault)
        {
            if ((prefix.Length == 0) && !useDefault)
            {
                return NamespaceUri.NULL;
            }
            else if (prefix == "xml")
            {
                return NamespaceUri.XML;
            }
            else
            {
                for (int i = namespacesSize - 1; i >= 0; i--)
                {
                    if (namespaces[i].GetPrefix().Equals(prefix))
                    {
                        return namespaces[i].GetNamespaceUri();
                    }
                }
            }

            return (prefix.Length == 0) ? NamespaceUri.NULL : null;
        }

        //break;
        public IEnumerator<string> IteratePrefixes()
        {
            IList<string> prefixes = new List<string>(namespacesSize);
            for (int i = namespacesSize - 1; i >= 0; i--)
            {
                string prefix = namespaces[i].GetPrefix();
                if (!prefixes.Contains(prefix))
                {
                    prefixes.Add(prefix);
                }
            }

            prefixes.Add("xml");
            return prefixes.GetEnumerator();
        }
    }
}