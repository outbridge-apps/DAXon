////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Linq;

namespace OutSmart.DAXon.Api
{
    // Minimal XdmValue stub with the static Wrap factory used by ~40 sites.
    public class XdmValue
    {
        // Runtime: the real XdmValue.cs is excluded (it pulls in XdmStream/Step). This stub must still faithfully
        // hold the wrapped GroundedValue so XdmNode/XdmItem (real subclasses that base() into here) round-trip the
        // underlying NodeInfo/value. Previously the ctor discarded the arg and GetUnderlyingValue()=>null, so
        // Xslt30Transformer.ApplyTemplates passed a null source into XsltController -> NRE at source.Iterate().
        private readonly object _value;
        public virtual object UnderlyingValue => _value;
        public XdmValue() { }
        public XdmValue(object value) { _value = value; }
        // Type-dispatching Wrap: a NodeInfo must wrap as XdmNode (MessageInstr.MakeMessage casts
        // (XdmNode)XdmNode.Wrap(content)); AtomicValue -> XdmAtomicValue.
        public static XdmValue Wrap(object value)
        {
            if (value is NodeInfo __n)
                return new XdmNode(__n);
            if (value is AtomicValue __a)
                return new XdmAtomicValue(__a);
            // upstream singleton dispatch: a lone map/array/function wraps as its XdmItem subclass
            // (callers cast Wrap(singleItem) to XdmItem, e.g. XPathCompiler.EvaluateSingle)
            if (value is OutSmart.DAXon.Values.Maps.MapItem __m)
                return new XdmMap(__m);
            if (value is OutSmart.DAXon.Values.Arrays.ArrayItem __arr)
                return new XdmArray(__arr);
            if (value is IFunctionItem __f)
                return new XdmFunctionItem(__f);
            return new XdmValue(value);
        }
        // Whether the value is an instance of the sequence type.
        public virtual bool Matches(SequenceType type)
        {
            ItemType itemType = type.GetItemType();
            int size = 0;
            foreach (XdmItem item in this)
            {
                size++;
                if (!itemType.Matches(item))
                {
                    return false;
                }
            }

            return type.GetOccurrenceIndicator().Allows(size);
        }

        // The form callers were compiled against while this answered false to everything: a sequence type, or an
        // item type, which the value matches when it is one such item.
        public bool Matches(object t)
        {
            return t is SequenceType type ? Matches(type) : t is ItemType itemType && Matches(itemType.One());
        }
        // Enumerate the wrapped value's items as XdmItems (was an always-empty stub — any foreach
        // over an XdmValue silently saw nothing, e.g. the driver's context-select narrowing).
        public IEnumerator<XdmItem> GetEnumerator()
        {
            if (this is XdmItem selfItem)
            {
                yield return selfItem;
                yield break;
            }
            if (_value is OutSmart.DAXon.Model.ISequence seq)
            {
                var iter = seq.Iterate();
                for (OutSmart.DAXon.Model.IItem it; (it = iter.Next()) != null;)
                {
                    yield return (XdmItem)Wrap(it);
                }
            }
        }
    }
}
