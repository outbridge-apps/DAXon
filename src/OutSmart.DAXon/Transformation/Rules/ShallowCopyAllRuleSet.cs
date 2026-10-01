////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Transformation.Rules
{
    internal sealed class ShallowCopyAllRuleSet : ShallowCopyRuleSet
    {
        private static readonly ShallowCopyAllRuleSet THE_INSTANCE = new ShallowCopyAllRuleSet();

        public override string Name => "shallow-copy-all";

        private ShallowCopyAllRuleSet()
        {
        }
        public static ShallowCopyAllRuleSet GetInstance()
        {
            return THE_INSTANCE;
        }

        public override void Process(IItem item, ParameterSet parameters, ParameterSet tunnelParams, Outputter @out, IXPathContext context, ILocation locationId)
        {
            if (item is ArrayItem)
            {
                SequenceCollector collector = context.GetController().AllocateSequenceOutputter();
                ComplexContentOutputter cco = new ComplexContentOutputter(collector);
                ProxyOutputter checker = new ShallowCopyProxyOutputterForArrays(cco, locationId);
                collector.SetSystemId(@out.GetSystemId());
                ISequenceIterator iter = ((ArrayItem)item).Parcels();
                PipelineConfiguration pipe = @out.GetPipelineConfiguration();
                XPathContextMajor c2 = context.NewContext();
                c2.Origin = this;
                c2.TrackFocus(iter);
                c2.SetCurrentComponent(c2.GetCurrentMode());
                pipe.XPathContext = c2;
                ITailCall tc = context.GetCurrentMode().GetActor().ApplyTemplates(parameters, tunnelParams, null, checker, c2, locationId);
                while (tc != null)
                {
                    tc = tc.ProcessLeavingTail();
                }

                pipe.XPathContext = context;
                IList<IGroundedValue> members = new List<IGroundedValue>();
                foreach (IItem it in collector.List)
                {
                    members.Add(((MapItem)it)[new StringValue("value")]);
                }

                SimpleArrayItem newArray = new SimpleArrayItem(members);
                @out.Append(newArray, locationId, 0);
            }
            else if (item is MapItem)
            {
                int size = ((MapItem)item).Count;
                if (size == 1)
                {

                    // If it's a singleton map, we can't break it down any further
                    AtomicValue key = null;
                    IGroundedValue singletonValue = null;
                    foreach (OutSmart.DAXon.Values.Maps.KeyValuePair pair in ((MapItem)item).KeyValuePairs())
                    {
                        key = pair.key;
                        singletonValue = pair.value;
                        break;
                    }

                    SequenceCollector collector = context.GetController().AllocateSequenceOutputter();
                    ComplexContentOutputter cco = new ComplexContentOutputter(collector);
                    collector.SetSystemId(@out.GetSystemId());
                    ISequenceIterator iter = singletonValue.Iterate();
                    PipelineConfiguration pipe = @out.GetPipelineConfiguration();
                    XPathContextMajor c2 = context.NewContext();
                    c2.Origin = this;
                    c2.TrackFocus(iter);
                    c2.SetCurrentComponent(c2.GetCurrentMode());
                    pipe.XPathContext = c2;
                    ITailCall tc = context.GetCurrentMode().GetActor().ApplyTemplates(parameters, tunnelParams, null, cco, c2, locationId);
                    while (tc != null)
                    {
                        tc = tc.ProcessLeavingTail();
                    }

                    pipe.XPathContext = context;
                    MapItem singletonMap = new SingleEntryMap(key, collector.Sequence);
                    @out.Append(singletonMap, locationId, 0);
                }
                else if (size > 1)
                {
                    SequenceCollector collector = context.GetController().AllocateSequenceOutputter();
                    ComplexContentOutputter cco = new ComplexContentOutputter(collector);
                    ProxyOutputter checker = new ShallowCopyProxyOutputterForMaps(cco, locationId);
                    collector.SetSystemId(@out.GetSystemId());
                    ISequenceIterator iter = ((MapItem)item).Entries();
                    PipelineConfiguration pipe = @out.GetPipelineConfiguration();
                    XPathContextMajor c2 = context.NewContext();
                    c2.Origin = this;
                    c2.TrackFocus(iter);
                    c2.SetCurrentComponent(c2.GetCurrentMode());
                    pipe.XPathContext = c2;
                    ITailCall tc = context.GetCurrentMode().GetActor().ApplyTemplates(parameters, tunnelParams, null, checker, c2, locationId);
                    while (tc != null)
                    {
                        tc = tc.ProcessLeavingTail();
                    }

                    pipe.XPathContext = context;
                    MapItem mergedMap = MapFunctionSet.MapMerge.MergeMaps(collector.Iterate(), context, "use-last", null, null);
                    @out.Append(mergedMap, locationId, 0);
                }
            }
            else
            {
                SequenceCollector collector = context.GetController().AllocateSequenceOutputter();
                ComplexContentOutputter cco = new ComplexContentOutputter(collector);
                collector.SetSystemId(@out.GetSystemId());
                base.Process(item, parameters, tunnelParams, cco, context, locationId);
                ISequenceIterator resultIter = collector.Iterate();
                IItem resultItem;
                while ((resultItem = resultIter.Next()) != null)
                {
                    @out.Append(resultItem);
                }
            }
        }

        private sealed class ShallowCopyProxyOutputterForMaps : ProxyOutputter
        {
            private readonly ILocation locationId;
            public ShallowCopyProxyOutputterForMaps(ComplexContentOutputter cco, ILocation locationId) : base(cco)
            {
                this.locationId = locationId;
            }

            public override void Append(IItem item)
            {
                if (item is MapItem)
                {
                    base.Append(item);
                }
                else
                {
                    MustBeParcel(locationId);
                }
            }

            public override void Append(IItem item, ILocation locationId, int properties)
            {
                if (item is MapItem)
                {
                    base.Append(item, locationId, properties);
                }
                else
                {
                    MustBeParcel(locationId);
                }
            }

            private void MustBeParcel(ILocation locationId)
            {
                throw new XPathException("Template rule invoked when processing a map using the " + "shallow-copy-all rule must return a sequence of maps", "XPTY0004", locationId);
            }
        }

        private sealed class ShallowCopyProxyOutputterForArrays : ProxyOutputter
        {
            private readonly ILocation locationId;
            public ShallowCopyProxyOutputterForArrays(ComplexContentOutputter cco, ILocation locationId) : base(cco)
            {
                this.locationId = locationId;
            }

            public override void Append(IItem item)
            {
                if (RecordTest.VALUE_RECORD.Matches(item, GetConfiguration().GetTypeHierarchy()))
                {
                    base.Append(item);
                }
                else
                {
                    MustBeValueRecord(locationId);
                }
            }

            public override void Append(IItem item, ILocation locationId, int properties)
            {
                if (RecordTest.VALUE_RECORD.Matches(item, GetConfiguration().GetTypeHierarchy()))
                {
                    base.Append(item, locationId, properties);
                }
                else
                {
                    MustBeValueRecord(locationId);
                }
            }

            private void MustBeValueRecord(ILocation locationId)
            {
                throw new XPathException("Template rule invoked when processing an array using the " + "shallow-copy-all rule must return a sequence of value records", "XPTY0004", locationId);
            }
        }
    }
}