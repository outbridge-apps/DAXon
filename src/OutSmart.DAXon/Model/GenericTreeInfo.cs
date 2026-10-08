////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    internal class GenericTreeInfo : ITreeInfo
    {
        protected internal readonly object syncLock = new object();
        private Configuration config;
        protected NodeInfo root;
        private string systemId;
        private Dictionary<string, object> userData;
        private long documentNumber = -1;
        private ISpaceStrippingRule spaceStrippingRule = NoElementsSpaceStrippingRule.GetInstance();
        private Durability durability = Durability.UNDEFINED;

        public virtual string SystemId
        {
            get => systemId; set
            {
                this.systemId = value;
            }
        }

        public virtual IEnumerator<string> UnparsedEntityNames => System.Linq.Enumerable.Empty<string>().GetEnumerator();

        public virtual ISpaceStrippingRule SpaceStrippingRule
        {
            get => spaceStrippingRule; set
            {
                this.spaceStrippingRule = value;
            }
        }
        public GenericTreeInfo(Configuration config)
        {
            this.config = config;
        }

        public GenericTreeInfo(Configuration config, NodeInfo root)
        {
            this.config = config;
            SetRootNode(root);
        }

        public virtual void SetConfiguration(Configuration config)
        {
            this.config = config;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual void SetRootNode(NodeInfo root)
        {
            if (root.GetParent() != null)
            {
                throw new ArgumentException("The root node of a tree must be parentless");
            }

            this.root = root;
        }

        public virtual NodeInfo GetRootNode()
        {
            return root;
        }

        public virtual string GetPublicId()
        {
            return null;
        }

        public virtual long GetDocumentNumber()
        {
            if (documentNumber == -1)
            {
                DocumentNumberAllocator dna = config.DocumentNumberAllocator;
                lock (syncLock)
                {
                    if (documentNumber == -1)
                    {
                        documentNumber = dna.AllocateDocumentNumber();
                    }
                }
            }

            return documentNumber;
        }

        public virtual void SetDocumentNumber(long documentNumber)
        {
            lock (syncLock)
            {
                this.documentNumber = documentNumber;
            }
        }

        public virtual NodeInfo SelectID(string id, bool getParent)
        {
            return null;
        }

        public virtual void SetDurability(Durability durability)
        {
            this.durability = durability;
        }

        public virtual Durability GetDurability()
        {
            if (durability == Durability.UNDEFINED)
            {
                return IsMutable() ? Durability.MUTABLE : Durability.LASTING;
            }
            else
            {
                return durability;
            }
        }

        public virtual bool IsMutable()
        {
            return durability == Durability.MUTABLE;
        }

        public virtual String[] GetUnparsedEntity(string name)
        {
            return null;
        }

        public virtual void SetUserData(string key, object value)
        {
            if (userData == null)
            {
                userData = new Dictionary<string, object>();
            }

            userData[key] = value;
        }

        public virtual object GetUserData(string key)
        {
            if (userData == null)
            {
                return userData;
            }
            else
            {
                return userData.GetOrDefault(key);
            }
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual bool IsTyped() => false; /* Phase B: Saxon-HE non-schema-aware -> source tree never type-annotated; real GenericTreeInfo.typed defaults false, no SetTyped caller */
    }
}
