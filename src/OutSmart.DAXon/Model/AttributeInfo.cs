////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Model
{
    public class AttributeInfo
    {
        private readonly INodeName nodeName;
        private readonly ISimpleType type;
        private readonly string value;
        private readonly ILocation location;
        private readonly int properties;

        public virtual string Value => value;

        public virtual StringValue XdmStringValue => new StringValue(value);
        public AttributeInfo(INodeName nodeName, ISimpleType type, string value, ILocation location, int properties)
        {
            this.nodeName = nodeName;
            this.type = type;
            this.value = value;
            this.location = location;
            this.properties = properties;
        }

        public virtual INodeName GetNodeName()
        {
            return nodeName;
        }

        public virtual ISimpleType GetType()
        {
            return type;
        }

        public virtual ILocation GetLocation()
        {
            return location;
        }

        public virtual int GetProperties()
        {
            return properties;
        }

        public virtual bool IsId()
        {
            try
            {
                return StandardNames.XML_ID_NAME.Equals(nodeName) || ReceiverOption.Contains(GetProperties(), ReceiverOption.IS_ID) || GetType().IsIdType();
            }
            catch (MissingComponentException e)
            {
                return false;
            }
        }

        public virtual AttributeInfo WithNodeName(INodeName newName)
        {
            return new AttributeInfo(newName, type, value, location, properties);
        }

        /// <summary>
        /// AttributeInfo.Deleted is a subclass used to mark a deleted attribute (in XQuery Update)
        /// </summary>
        internal sealed class Deleted : AttributeInfo
        {
            public Deleted(AttributeInfo att) : base(att.GetNodeName(), att.GetType(), att.Value, att.GetLocation(), att.GetProperties())
            {
            }
        }
    }
}