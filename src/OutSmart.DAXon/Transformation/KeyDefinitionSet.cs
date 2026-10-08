////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using System.Collections.Generic;
namespace OutSmart.DAXon.Transformation
{
    /// <summary>
    /// A set of xsl:key definitions in a stylesheet that share the same name
    /// </summary>
    public class KeyDefinitionSet
    {
        private readonly StructuredQName keyName;
        private readonly int keySetNumber; // unique among the KeyDefinitionSets within a KeyManager
        private readonly IList<KeyDefinition> keyDefinitions;
        private string collationName;
        private bool composite;
        private bool backwardsCompatible; // true if any of the keys is backwards compatible
        private bool rangeKey; // true if any of the keys is a range key
        private bool reusable = true; // true if indexes built for this key can be reused across transformations

        public virtual StructuredQName KeyName => keyName;

        public virtual string CollationName => collationName;

        public virtual int KeySetNumber => keySetNumber;

        public virtual IList<KeyDefinition> KeyDefinitions => keyDefinitions;
        public KeyDefinitionSet(StructuredQName keyName, int keySetNumber)
        {
            this.keyName = keyName;
            this.keySetNumber = keySetNumber;
            keyDefinitions = new List<KeyDefinition>(3);
        }

        public virtual void AddKeyDefinition(KeyDefinition keyDef)
        {
            if (keyDefinitions.Count == 0)
            {
                collationName = keyDef.CollationName;
                composite = keyDef.IsComposite();
            }
            else
            {
                if ((collationName == null && keyDef.CollationName != null) || (collationName != null && !collationName.Equals(keyDef.CollationName)))
                {
                    throw new XPathException("All keys with the same name must use the same collation", "XTSE1220");
                }

                if (keyDef.IsComposite() != composite)
                {
                    throw new XPathException("All keys with the same name must have the same value for @composite", "XTSE1222");
                }


                // ignore this key definition if it is a duplicate of another already present. This can happen when including
                // a stylesheet module more than once
                IList<KeyDefinition> v = KeyDefinitions;
                foreach (KeyDefinition other in v)
                {
                    if (keyDef.Match.IsEqual(other.Match) && keyDef.GetBody().IsEqual(other.GetBody()))
                    {
                        return;
                    }
                }
            }

            if (keyDef.IsBackwardsCompatible())
            {
                backwardsCompatible = true;
            }

            if (keyDef.IsRangeKey())
            {
                rangeKey = true;
            }

            keyDefinitions.Add(keyDef);
        }

        public virtual bool IsComposite()
        {
            return composite;
        }

        public virtual bool IsBackwardsCompatible()
        {
            return backwardsCompatible;
        }

        public virtual bool IsRangeKey()
        {
            return rangeKey;
        }

        public virtual void SetReusable(bool reusable)
        {
            this.reusable = reusable;
        }

        public virtual bool IsReusable()
        {
            return reusable;
        }
    }
}