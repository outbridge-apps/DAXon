////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;

namespace OutSmart.DAXon.Events
{
    internal sealed class NamePoolConverter : ProxyReceiver
    {
        NamePool oldPool;
        NamePool newPool;
        public NamePoolConverter(IReceiver next, NamePool oldPool, NamePool newPool) : base(next)
        {
            this.oldPool = oldPool;
            this.newPool = newPool;
        }

        /// <summary>
        /// Output element start tag
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            CheckType(type);
            int fp = newPool.AllocateFingerprint(elemName.GetNamespaceUri(), elemName.GetLocalPart());
            CodedName newElemName = new CodedName(fp, elemName.GetPrefix(), newPool);
            IAttributeMap newAtts = EmptyAttributeMap.GetInstance();
            foreach (AttributeInfo att in attributes)
            {
                CheckType(att.GetType());
                int afp = newPool.AllocateFingerprint(att.GetNodeName().GetNamespaceUri(), att.GetNodeName().GetLocalPart());
                INodeName newAttName = new CodedName(afp, att.GetNodeName().GetPrefix(), newPool);
                newAtts = newAtts.Put(new AttributeInfo(newAttName, att.GetType(), att.Value, att.GetLocation(), att.GetProperties()));
            }

            nextReceiver.StartElement(newElemName, type, newAtts, namespaces, location, properties);
        }

        /// <summary>
        /// Output element start tag
        /// </summary>
        private void CheckType(ISchemaType type)
        {
            if ((type.Fingerprint & NamePool.USER_DEFINED_MASK) != 0)
            {
                throw new NotSupportedException("Cannot convert a user-typed node to a different name pool");
            }
        }
    }
}