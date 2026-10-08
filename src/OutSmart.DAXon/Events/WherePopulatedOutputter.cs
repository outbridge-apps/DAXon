////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Events
{
    internal sealed class WherePopulatedOutputter : ProxyOutputter
    {
        private int level = 0;
        private bool pendingStartTag = false;
        private INodeName pendingElemName;
        private ISchemaType pendingSchemaType;
        private ILocation pendingLocationId;
        private int pendingProperties;
        private IAttributeMap pendingAttributes;
        private NamespaceMap pendingNamespaces;
        public WherePopulatedOutputter(Outputter next) : base(next)
        {
        }

        /// <summary>
        /// Start of a document node.
        /// </summary>
        public override void StartDocument(int properties)
        {
            if (level++ == 0)
            {
                pendingStartTag = true;
                pendingElemName = null;
                pendingProperties = properties;
            }
            else
            {
                base.StartDocument(properties);
            }
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, ILocation location, int properties)
        {
            ReleaseStartTag();
            if (level++ == 0)
            {
                pendingStartTag = true;
                pendingElemName = elemName;
                pendingSchemaType = type;
                pendingLocationId = location.SaveLocation();
                pendingProperties = properties;
                pendingAttributes = EmptyAttributeMap.GetInstance();
                pendingNamespaces = NamespaceMap.EmptyMap();
            }
            else
            {
                base.StartElement(elemName, type, location, properties);
            }
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            ReleaseStartTag();
            if (level++ == 0)
            {
                pendingStartTag = true;
                pendingElemName = elemName;
                pendingSchemaType = type;
                pendingLocationId = location.SaveLocation();
                pendingProperties = properties;
                pendingAttributes = attributes;
                pendingNamespaces = namespaces;
            }
            else
            {
                base.StartElement(elemName, type, attributes, namespaces, location, properties);
            }
        }

        /// <summary>
        /// Notify a namespace binding.
        /// </summary>
        public override void Namespace(string prefix, NamespaceUri namespaceUri, int properties)
        {
            if (level == 1)
            {
                pendingNamespaces = pendingNamespaces.Put(prefix, namespaceUri);
            }
            else
            {
                base.Namespace(prefix, namespaceUri, properties);
            }
        }

        /// <summary>
        /// Notify an attribute.
        /// </summary>
        public override void Attribute(INodeName attName, ISimpleType typeCode, string value, ILocation location, int properties)
        {
            if (level == 1)
            {
                pendingAttributes = pendingAttributes.Put(new AttributeInfo(attName, typeCode, value.ToString(), location, properties));
            }
            else if (!(level == 0 && value.Length == 0))
            {
                base.Attribute(attName, typeCode, value, location, properties);
            }
        }

        /// <summary>
        /// Notify the end of a document node
        /// </summary>
        public override void EndDocument()
        {
            if (--level == 0)
            {
                if (!pendingStartTag)
                {
                    base.EndDocument();
                }
            }
            else
            {
                base.EndDocument();
            } //pendingStartTag = false;
        }

        public override void EndElement()
        {
            if (--level == 0)
            {
                if (!pendingStartTag)
                {
                    base.EndElement();
                }
            }
            else
            {
                base.EndElement();
            }

            pendingStartTag = false;
        }

        public void ReleaseStartTag()
        {
            if (level >= 1 && pendingStartTag)
            {
                // Nested where-populated regions release into each other, a cascade as deep as the nesting.
                StackGuard.ProbeNesting(null);
                if (pendingElemName == null)
                {
                    NextOutputter.StartDocument(pendingProperties);
                }
                else
                {

                    // Bug #6577
                    SpreadStartElement(pendingElemName, pendingSchemaType, pendingAttributes, pendingNamespaces, pendingLocationId, pendingProperties, NextOutputter);
                }

                pendingStartTag = false;
            }
        }

        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (level == 0)
            {
                if (!chars.IsEmpty())
                {
                    base.Characters(chars, locationId, properties);
                }
            }
            else if (level == 1)
            {
                if (!chars.IsEmpty())
                {
                    ReleaseStartTag();
                    base.Characters(chars, locationId, properties);
                }
            }
            else
            {
                base.Characters(chars, locationId, properties);
            }
        }

        public override void ProcessingInstruction(string name, UnicodeString data, ILocation location, int properties)
        {
            if (level == 0)
            {
                if (!data.IsEmpty())
                {
                    base.ProcessingInstruction(name, data, location, properties);
                }
            }
            else if (level == 1)
            {
                if (!data.IsEmpty())
                {
                    ReleaseStartTag();
                    base.ProcessingInstruction(name, data, location, properties);
                }
            }
            else
            {
                base.ProcessingInstruction(name, data, location, properties);
            }
        }

        public override void Comment(UnicodeString content, ILocation location, int properties)
        {
            if (level == 0)
            {
                if (!content.IsEmpty())
                {
                    base.Comment(content, location, properties);
                }
            }
            else if (level == 1)
            {
                if (!content.IsEmpty())
                {
                    ReleaseStartTag();
                    base.Comment(content, location, properties);
                }
            }
            else
            {
                base.Comment(content, location, properties);
            }
        }

        public override void Append(IItem item)
        {
            if (level == 0)
            {
                if (!WherePopulated.IsDeemedEmpty(item))
                {
                    NextOutputter.Append(item);
                }
            }
            else if (level == 1 && pendingStartTag)
            {
                if (item is NodeInfo)
                {
                    NodeInfo node = (NodeInfo)item;
                    switch (node.GetNodeKind())
                    {
                        case Types.Type.TEXT:

                            // ignore empty text nodes
                            if (node.GetNodeKind() == Types.Type.TEXT && node.UnicodeStringValue.Length() == 0)
                            {
                                return;
                            }

                            break;
                        case Types.Type.DOCUMENT:

                            // ignore empty document nodes
                            if (node.GetNodeKind() == Types.Type.DOCUMENT && !node.HasChildNodes())
                            {
                                return;
                            }

                            break;
                        case Types.Type.ATTRIBUTE:
                            Attribute(NameOfNode.MakeName(node), (ISimpleType)node.GetSchemaType(), node.GetStringValue(), Loc.NONE, 0);
                            return;
                        case Types.Type.NAMESPACE:
                            Namespace(node.GetLocalPart(), NamespaceUri.Of(node.GetStringValue()), 0);
                            return;
                        default:
                            break;
                    }
                }

                ReleaseStartTag();
                NextOutputter.Append(item);
            }
            else
            {
                base.Append(item);
            }
        }

        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (level == 0)
            {
                if (!WherePopulated.IsDeemedEmpty(item))
                {
                    NextOutputter.Append(item, locationId, copyNamespaces);
                }
            }
            else if (level == 1 && pendingStartTag)
            {
                if (item is NodeInfo)
                {
                    NodeInfo node = (NodeInfo)item;
                    switch (node.GetNodeKind())
                    {
                        case Types.Type.TEXT:

                            // ignore empty text nodes
                            if (node.GetNodeKind() == Types.Type.TEXT && node.UnicodeStringValue.Length() == 0)
                            {
                                return;
                            }

                            break;
                        case Types.Type.DOCUMENT:

                            // ignore empty document nodes
                            if (node.GetNodeKind() == Types.Type.DOCUMENT && !node.HasChildNodes())
                            {
                                return;
                            }

                            break;
                        case Types.Type.ATTRIBUTE:
                            Attribute(NameOfNode.MakeName(node), (ISimpleType)node.GetSchemaType(), node.GetStringValue(), locationId, 0);
                            return;
                        case Types.Type.NAMESPACE:
                            Namespace(node.GetLocalPart(), NamespaceUri.Of(node.GetStringValue()), 0);
                            return;
                        default:
                            break;
                    }
                }

                ReleaseStartTag();
                NextOutputter.Append(item, locationId, copyNamespaces);
            }
            else
            {
                base.Append(item, locationId, copyNamespaces);
            }
        }
    }
}
