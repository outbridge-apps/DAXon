////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Transformation.Rules
{
    internal sealed class DeepSkipRuleSet : IBuiltInRuleSet
    {
        private static readonly DeepSkipRuleSet THE_INSTANCE = new DeepSkipRuleSet();

        public string Name => "deep-skip";

        private DeepSkipRuleSet()
        {
        }
        public static DeepSkipRuleSet GetInstance()
        {
            return THE_INSTANCE;
        }

        public void Process(IItem item, ParameterSet parameters, ParameterSet tunnelParams, Outputter output, IXPathContext context, ILocation locationId)
        {
            if (item is NodeInfo && ((NodeInfo)item).GetNodeKind() == Types.Type.DOCUMENT)
            {
                XPathContextMajor c2 = context.NewContext();
                c2.Origin = this;
                c2.TrackFocus(((NodeInfo)item).IterateAxis(AxisInfo.CHILD));
                c2.SetCurrentComponent(c2.GetCurrentMode());
                ITailCall tc = c2.GetCurrentMode().GetActor().ApplyTemplates(parameters, tunnelParams, null, output, c2, locationId);
                while (tc != null)
                {
                    tc = tc.ProcessLeavingTail();
                }
            } // otherwise, do nothing
        }

        public BuiltInRules[] GetActionForParentNodes(int nodeKind)
        {
            if (nodeKind == Types.Type.DOCUMENT)
            {
                return new BuiltInRules[]
                {
                    BuiltInRules.APPLY_TEMPLATES_TO_CHILDREN
                };
            }
            else
            {
                return new BuiltInRules[]
                {
                    BuiltInRules.DEEP_SKIP
                };
            }
        }
    }
}