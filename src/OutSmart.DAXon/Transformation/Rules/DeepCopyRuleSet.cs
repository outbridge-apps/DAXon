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
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Transformation.Rules
{
    /// <summary>
    /// The built-in rule set introduced in XSLT 3.0, which performs a deep copy of any unmatched node.
    /// </summary>
    internal sealed class DeepCopyRuleSet : IBuiltInRuleSet
    {
        private static readonly DeepCopyRuleSet THE_INSTANCE = new DeepCopyRuleSet();

        public string Name => "deep-copy";

        private DeepCopyRuleSet()
        {
        }
        public static DeepCopyRuleSet GetInstance()
        {
            return THE_INSTANCE;
        }

        public void Process(IItem item, ParameterSet parameters, ParameterSet tunnelParams, Outputter @out, IXPathContext context, ILocation locationId)
        {
            if (item is NodeInfo)
            {
                NodeInfo node = (NodeInfo)item;
                switch (node.GetNodeKind())
                {
                    case Types.Type.DOCUMENT:
                    case Types.Type.ELEMENT:
                        {

                            // TODO: fast path for TinyTree
                            if (@out.GetSystemId() == null)
                            {
                                @out.SetSystemId(node.GetBaseURI());
                            }

                            Navigator.Copy(node, @out, CopyOptions.ALL_NAMESPACES | CopyOptions.TYPE_ANNOTATIONS, locationId);
                            return;
                        }

                    case Types.Type.TEXT:
                        @out.Characters(item.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        return;
                    case Types.Type.COMMENT:
                        @out.Comment(node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        return;
                    case Types.Type.PROCESSING_INSTRUCTION:
                        @out.ProcessingInstruction(node.GetLocalPart(), node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        return;
                    case Types.Type.ATTRIBUTE:
                        @out.Attribute(NameOfNode.MakeName(node), (ISimpleType)node.GetSchemaType(), node.GetStringValue(), locationId, ReceiverOption.NONE);
                        return;
                    case Types.Type.NAMESPACE:
                        @out.Namespace(node.GetLocalPart(), NamespaceUri.Of(node.GetStringValue()), ReceiverOption.NONE);
                        return;
                    default:
                        break;
                }
            }
            else
            {
                @out.Append(item, locationId, ReceiverOption.NONE);
            }
        }

        public BuiltInRules[] GetActionForParentNodes(int nodeKind)
        {
            return new BuiltInRules[]
            {
                BuiltInRules.DEEP_COPY
            };
        }
    }
}