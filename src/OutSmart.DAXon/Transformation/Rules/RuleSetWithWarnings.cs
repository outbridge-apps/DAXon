////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees.Utilities;
namespace OutSmart.DAXon.Transformation.Rules
{
    internal sealed class RuleSetWithWarnings : IBuiltInRuleSet
    {
        private readonly IBuiltInRuleSet baseRuleSet;

        public IBuiltInRuleSet BaseRuleSet => baseRuleSet;

        public string Name => baseRuleSet + " with warnings";
        public RuleSetWithWarnings(IBuiltInRuleSet baseRuleSet)
        {
            this.baseRuleSet = baseRuleSet;
        }

        public void Process(IItem item, ParameterSet parameters, ParameterSet tunnelParams, Outputter output, IXPathContext context, ILocation locationId)
        {
            OutputWarning(item, context);
            baseRuleSet.Process(item, parameters, tunnelParams, output, context, locationId);
        }

        public void OutputWarning(IItem item, IXPathContext context)
        {
            string id = item is NodeInfo ? "the node " + Navigator.GetPath((NodeInfo)item) : "the atomic value " + item.UnicodeStringValue;
            XmlProcessingIncident warning = new XmlProcessingIncident("No user-defined template rule matches " + id, "XTDE0555").AsWarning();
            context.GetController().ErrorReporter.Report(warning);
        }

        public BuiltInRules[] GetActionForParentNodes(int nodeKind)
        {
            return baseRuleSet.GetActionForParentNodes(nodeKind);
        }
    }
}