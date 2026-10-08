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
namespace OutSmart.DAXon.Transformation.Rules
{
    internal sealed class FailRuleSet : IBuiltInRuleSet
    {
        private static readonly FailRuleSet THE_INSTANCE = new FailRuleSet();

        public string Name => "fail";

        private FailRuleSet()
        {
        }
        public static FailRuleSet GetInstance()
        {
            return THE_INSTANCE;
        }

        public void Process(IItem item, ParameterSet parameters, ParameterSet tunnelParams, Outputter output, IXPathContext context, ILocation locationId)
        {
            string id = Err.Depict(item);
            XPathException err = new XPathException("No user-defined template rule in " + context.GetCurrentMode().GetActor().GetModeTitle(false) + " matches " + id, "XTDE0555");
            err.SetLocator(locationId.SaveLocation());
            throw err;
        }

        public BuiltInRules[] GetActionForParentNodes(int nodeKind)
        {
            return new BuiltInRules[]
            {
                BuiltInRules.FAIL
            };
        }
    }
}