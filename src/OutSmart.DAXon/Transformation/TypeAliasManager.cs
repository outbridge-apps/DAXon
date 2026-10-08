////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Transformation
{
    public class TypeAliasManager
    {

        // map of type aliases (Saxon extension)
        private readonly Dictionary<StructuredQName, ComponentDeclaration> unresolvedDeclarations = new Dictionary<StructuredQName, ComponentDeclaration>();
        private readonly Dictionary<StructuredQName, ItemType> typeAliases = new Dictionary<StructuredQName, ItemType>();
        public TypeAliasManager()
        {
        }
        public virtual void RegisterTypeAlias(StructuredQName name, ItemType type)
        {
            typeAliases[name] = type;
            unresolvedDeclarations.Remove(name);
        }

        public virtual void ProcessDeclaration(ComponentDeclaration declaration)
        {
            XSLItemType sta = (XSLItemType)declaration.SourceElement;
            ItemType type = sta.TryToResolve();
            if (type != null)
            {
                RegisterTypeAlias(sta.GetObjectName(), type);
            }
            else
            {
                unresolvedDeclarations[sta.GetObjectName()] = declaration;
            }
        }

        public virtual void ProcessAllDeclarations(IList<ComponentDeclaration> topLevel)
        {
            foreach (ComponentDeclaration decl in topLevel)
            {
                if (decl.SourceElement is XSLItemType)
                {
                    ProcessDeclaration(decl);
                }
            }

            int unresolved = unresolvedDeclarations.Count;
            while (unresolved > 0)
            {
                HashSet<ComponentDeclaration> pending = new HashSet<ComponentDeclaration>(unresolvedDeclarations.Values);
                foreach (ComponentDeclaration decl in pending)
                {
                    ProcessDeclaration(decl);
                }

                if (unresolvedDeclarations.Count >= unresolved)
                {
                    StringBuilder fsb = new StringBuilder(256);
                    fsb.Append("Cannot resolve all type aliases, because of missing or circular definitions. Unresolved names: ");
                    foreach (StructuredQName name in unresolvedDeclarations.Keys)
                    {
                        fsb.Append(name.DisplayName);
                        fsb.Append(' ');
                    }

                    throw new XPathException(fsb.ToString(), DAXonErrorCode.SXTA0001);
                }

                unresolved = unresolvedDeclarations.Count;
            }
        }

        public virtual ItemType GetItemType(StructuredQName alias)
        {
            return typeAliases.GetOrDefault(alias);
        }
    }
}