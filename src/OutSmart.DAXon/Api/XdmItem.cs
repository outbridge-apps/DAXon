////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Api
{
    public abstract class XdmItem : XdmValue
    {

        public new IItem UnderlyingValue => (IItem)base.UnderlyingValue;

        public virtual UnicodeString UnicodeStringValue
        {
            get
            {
                try
                {
                    return UnderlyingValue.UnicodeStringValue;
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(e.Message, e);
                }
            }
        }
        public XdmItem(IItem item) : base(item)
        {
        }

        public static XdmItem WrapItem(IItem item)
        {
            return item == null ? null : (XdmItem)XdmValue.Wrap(item);
        }

        public static XdmNode WrapItem(NodeInfo item)
        {
            return item == null ? null : (XdmNode)XdmValue.Wrap(item);
        }

        public static XdmAtomicValue WrapItem(AtomicValue item)
        {
            return item == null ? null : (XdmAtomicValue)XdmValue.Wrap(item);
        }

        public virtual string GetStringValue()
        {
            try
            {
                return UnderlyingValue.GetStringValue();
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(e.Message, e);
            }
        }

        public virtual bool IsNode()
        {
            return UnderlyingValue is NodeInfo;
        }

        public virtual bool IsAtomicValue()
        {
            return UnderlyingValue is AtomicValue;
        }

        public int Size()
        {
            return 1;
        }

        public virtual Dictionary<XdmAtomicValue, XdmValue> AsMap()
        {
            return null; // Overridden in XdmMap. The method is retained on this interface for compatibility reasons.
        }

        public virtual bool Matches(ItemType type)
        {
            return type.Matches(this);
        }
    }
}
