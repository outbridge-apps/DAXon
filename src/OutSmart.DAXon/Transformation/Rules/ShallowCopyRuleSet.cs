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
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Transformation.Rules
{
    /// <summary>
    /// The built-in rule set introduced in XSLT 3.0, which is effectively an identity template.
    /// </summary>
    internal class ShallowCopyRuleSet : IBuiltInRuleSet
    {
        private static readonly ShallowCopyRuleSet THE_INSTANCE = new ShallowCopyRuleSet();

        public virtual string Name => "shallow-copy";

        protected ShallowCopyRuleSet()
        {
        }
        public static ShallowCopyRuleSet GetInstance()
        {
            return THE_INSTANCE;
        }

        public virtual void Process(IItem item, ParameterSet parameters, ParameterSet tunnelParams, Outputter @out, IXPathContext context, ILocation locationId)
        {
            if (item is NodeInfo)
            {
                NodeInfo node = (NodeInfo)item;
                switch (node.GetNodeKind())
                {
                    case Types.Type.DOCUMENT:
                        {
                            PipelineConfiguration pipe = @out.GetPipelineConfiguration();
                            if (@out.GetSystemId() == null)
                            {
                                @out.SetSystemId(node.GetBaseURI());
                            }

                            @out.StartDocument(ReceiverOption.NONE);
                            XPathContextMajor c2 = context.NewContext();
                            c2.Origin = this;
                            c2.TrackFocus(node.IterateAxis(AxisInfo.CHILD));
                            c2.SetCurrentComponent(c2.GetCurrentMode()); // Bug 3508
                            pipe.XPathContext = c2;
                            ITailCall tc = context.GetCurrentMode().GetActor().ApplyTemplates(parameters, tunnelParams, null, @out, c2, locationId);
                            while (tc != null)
                            {
                                tc = tc.ProcessLeavingTail();
                            }

                            @out.EndDocument();
                            pipe.XPathContext = context;
                            return;
                        }

                    case Types.Type.ELEMENT:
                        {
                            bool schemaAware = context.GetController().GetExecutable().IsSchemaAware();
                            PipelineConfiguration pipe = @out.GetPipelineConfiguration();
                            if (@out.GetSystemId() == null)
                            {
                                @out.SetSystemId(node.GetBaseURI());
                            }

                            INodeName fqn = NameOfNode.MakeName(node);
                            @out.StartElement(fqn, schemaAware ? AnyType.INSTANCE : Untyped.INSTANCE, locationId, ReceiverOption.NONE);
                            foreach (NamespaceBinding ns in node.AllNamespaces)
                            {
                                @out.Namespace(ns.GetPrefix(), ns.GetNamespaceUri(), ReceiverOption.NONE);
                            }

                            XPathContextMajor c2 = context.NewContext();
                            c2.SetCurrentComponent(c2.GetCurrentMode()); // Bug 3508
                            pipe.XPathContext = c2;

                            // apply-templates to all attributes
                            IAxisIterator attributes = node.IterateAxis(AxisInfo.ATTRIBUTE);
                            if (attributes != EmptyIterator.OfNodes())
                            {
                                c2.Origin = this;
                                c2.TrackFocus(attributes);
                                ITailCall tc = c2.GetCurrentMode().GetActor().ApplyTemplates(parameters, tunnelParams, null, @out, c2, locationId);
                                while (tc != null)
                                {
                                    tc = tc.ProcessLeavingTail();
                                }
                            }


                            // apply-templates to all children
                            if (node.HasChildNodes())
                            {
                                c2.TrackFocus(node.IterateAxis(AxisInfo.CHILD));
                                ITailCall tc = c2.GetCurrentMode().GetActor().ApplyTemplates(parameters, tunnelParams, null, @out, c2, locationId);
                                while (tc != null)
                                {
                                    tc = tc.ProcessLeavingTail();
                                }
                            }

                            @out.EndElement();
                            pipe.XPathContext = context;
                            return;
                        }

                    case Types.Type.TEXT:
                        @out.Characters(node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        return;
                    case Types.Type.COMMENT:
                        @out.Comment(node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        return;
                    case Types.Type.PROCESSING_INSTRUCTION:
                        @out.ProcessingInstruction(node.GetLocalPart(), node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        GC.KeepAlive(node); // C# port: break tail position - net472 x64 JIT tail-call helper corrupts the stack on 4/5-arg Outputter calls in tail position (AccessViolation)
                        return;
                    case Types.Type.ATTRIBUTE:
                        @out.Attribute(NameOfNode.MakeName(node), (ISimpleType)node.GetSchemaType(), node.GetStringValue(), locationId, ReceiverOption.NONE);
                        GC.KeepAlive(node); // C# port: break tail position - net472 x64 JIT tail-call helper corrupts the stack on 4/5-arg Outputter calls in tail position (AccessViolation)
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

        public virtual BuiltInRules[] GetActionForParentNodes(int nodeKind)
        {
            return new BuiltInRules[]
            {
                BuiltInRules.SHALLOW_COPY,
                BuiltInRules.APPLY_TEMPLATES_TO_ATTRIBUTES,
                BuiltInRules.APPLY_TEMPLATES_TO_CHILDREN
            };
        }
    }
}