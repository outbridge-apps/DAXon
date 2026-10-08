////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Linq;
namespace OutSmart.DAXon.Model
{
    public sealed class DocumentPool
    {
        private readonly object syncLock = new object();
        // The document pool ensures that the document()
        // function, when called twice with the same URI, returns the same document
        // each time. For this purpose we use a hashtable from
        // URI to DocumentInfo object.
        private readonly Dictionary<DocumentKey, ITreeInfo> documentNameMap = new Dictionary<DocumentKey, ITreeInfo>(10);
        // The set of documents known to be unavailable. These documents must remain
        // unavailable for the duration of a transformation or query!
        private readonly HashSet<DocumentKey> unavailableDocuments = new HashSet<DocumentKey>();

        /// <summary>
        /// True when this pool holds nothing at all. Lets the end-of-run release skip its work
        /// entirely for the common shape where the host hands in an already-built node and the
        /// stylesheet calls neither doc() nor document(), so nothing was ever pooled.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                lock (syncLock)
                {
                    return documentNameMap.Count == 0 && unavailableDocuments.Count == 0;
                }
            }
        }

        // Documents and documents known to be unavailable.
        internal int Count
        {
            get
            {
                lock (syncLock)
                {
                    return documentNameMap.Count + unavailableDocuments.Count;
                }
            }
        }

        public void Add(ITreeInfo doc, string uri)
        {
            lock (syncLock)
            {
                if (uri != null)
                {
                    Add(doc, new DocumentKey(uri));
                }
            }
        }

        public void Add(ITreeInfo doc, DocumentKey uri)
        {
            lock (syncLock)
            {
                if (uri != null)
                {
                    ITreeInfo existing = documentNameMap.TryGetValue(uri, out var __ti3) ? __ti3 : null;
                    if (existing != null && existing != doc)
                    {
                        throw new XPathException("Cannot have two different documents with the same document-uri " + uri.AbsoluteURI);
                    }

                    documentNameMap[uri] = doc;
                }
            }
        }

        public ITreeInfo Find(string uri)
        {
            lock (syncLock)
            {
                return documentNameMap.TryGetValue(new DocumentKey(uri), out var __ti1) ? __ti1 : null;
            }
        }

        public ITreeInfo Find(DocumentKey uri)
        {
            lock (syncLock)
            {
                return documentNameMap.TryGetValue(uri, out var __ti2) ? __ti2 : null;
            }
        }

        public string GetDocumentURI(NodeInfo doc)
        {
            lock (syncLock)
            {
                foreach (KeyValuePair<DocumentKey, ITreeInfo> e in documentNameMap)
                {
                    if (e.Value == null)
                    {
                        continue;
                    }

                    if (e.Value.GetRootNode().Equals(doc))
                    {
                        return e.Key.ToString();
                    }
                }

                return null;
            }
        }

        public bool Contains(ITreeInfo doc)
        {
            lock (syncLock)
            {

                // relies on "equals" for nodes comparing node identity
                return documentNameMap.Values.Contains(doc);
            }
        }

        public ITreeInfo Discard(ITreeInfo doc)
        {
            lock (syncLock)
            {
                foreach (KeyValuePair<DocumentKey, ITreeInfo> e in documentNameMap)
                {
                    DocumentKey name = e.Key;
                    ITreeInfo entry = e.Value;
                    if (entry.Equals(doc))
                    {
                        documentNameMap.Remove(name);
                        return doc;
                    }
                }

                return doc;
            }
        }

        // Locked like every other member: today these three are only called on per-Controller
        // pools, but the class also backs Configuration's shared globalDocumentPool, and the
        // asymmetry (all-but-three methods locked) invites a torn HashSet the day a caller moves.
        public void DiscardIndexes(KeyManager keyManager)
        {
            lock (syncLock)
            {
                foreach (ITreeInfo doc in documentNameMap.Values)
                {
                    keyManager.ClearDocumentIndexes(doc);
                }
            }
        }

        public void MarkUnavailable(DocumentKey uri)
        {
            lock (syncLock)
            {
                unavailableDocuments.Add(uri);
            }
        }

        public bool IsMarkedUnavailable(DocumentKey uri)
        {
            lock (syncLock)
            {
                return unavailableDocuments.Contains(uri);
            }
        }
    }
}
