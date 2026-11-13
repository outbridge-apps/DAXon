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
    public interface IBuiltInRuleSet : IContextOriginator
    {
        void Process(IItem item, ParameterSet parameters, ParameterSet tunnelParams, Outputter output, IXPathContext context, ILocation locationId);
        string Name { get; }
        BuiltInRules[] GetActionForParentNodes(int nodeKind);
    }

    internal static class BuiltInRuleSets
    {
        /// <summary>
        /// What a built-in rule does with the items it passes on: applies the current mode to them from a new context
        /// whose origin is the rule set, and runs the tail calls left.
        /// </summary>
        internal static void ApplyTo(IBuiltInRuleSet rules, ISequenceIterator items, ParameterSet parameters, ParameterSet tunnelParams, Outputter output, IXPathContext context, ILocation locationId)
        {
            XPathContextMajor c2 = context.NewContext();
            c2.Origin = rules;
            c2.TrackFocus(items);
            c2.SetCurrentComponent(c2.GetCurrentMode()); // Bug 3508
            ITailCall tc = c2.GetCurrentMode().GetActor().ApplyTemplates(parameters, tunnelParams, null, output, c2, locationId);
            while (tc != null)
            {
                tc = tc.ProcessLeavingTail();
            }
        }
    }
}