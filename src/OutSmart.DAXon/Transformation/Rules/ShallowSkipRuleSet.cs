////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Transformation.Rules
{
    internal sealed class ShallowSkipRuleSet : IBuiltInRuleSet
    {
        private static readonly ShallowSkipRuleSet THE_INSTANCE = new ShallowSkipRuleSet();

        public string Name => "shallow-skip";

        private ShallowSkipRuleSet()
        {
        }
        public static ShallowSkipRuleSet GetInstance()
        {
            return THE_INSTANCE;
        }

        public void Process(IItem item, ParameterSet parameters, ParameterSet tunnelParams, Outputter output, IXPathContext context, ILocation locationId)
        {
            if (item is NodeInfo)
            {
                NodeInfo node = (NodeInfo)item;
                switch (node.GetNodeKind())
                {
                    case Types.Type.ELEMENT:
                        {
                            BuiltInRuleSets.ApplyTo(this, node.IterateAxis(AxisInfo.ATTRIBUTE), parameters, tunnelParams, output, context, locationId);

                            goto case Types.Type.DOCUMENT;
                        }

                    case Types.Type.DOCUMENT:
                        {
                            BuiltInRuleSets.ApplyTo(this, node.IterateAxis(AxisInfo.CHILD), parameters, tunnelParams, output, context, locationId);

                            return;
                        }

                    case Types.Type.TEXT:
                    case Types.Type.ATTRIBUTE:
                    case Types.Type.COMMENT:
                    case Types.Type.PROCESSING_INSTRUCTION:
                    case Types.Type.NAMESPACE:
                        break;
                }
            }
            else if (item is ArrayItem)
            {
                ISequence seq = ArrayFunctionSet.ArrayToSequence.ToSequence((ArrayItem)item);
                ISequenceIterator members = seq.Iterate();
                BuiltInRuleSets.ApplyTo(this, members, parameters, tunnelParams, output, context, locationId);
            }
            else
            {
            }
        }

        public BuiltInRules[] GetActionForParentNodes(int nodeKind)
        {
            return new BuiltInRules[]
            {
                BuiltInRules.APPLY_TEMPLATES_TO_ATTRIBUTES,
                BuiltInRules.APPLY_TEMPLATES_TO_CHILDREN
            };
        }
    }
}