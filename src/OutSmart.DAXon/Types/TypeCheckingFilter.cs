////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;

namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// A filter on the push pipeline that performs type checking, both of the item type and the
    /// cardinality.
    /// <para>Note that the TypeCheckingFilter cannot currently check document node tests of the form
    /// document-node(element(X,Y)), so it is not invoked in such cases. This isn't a big problem, because most
    /// instructions that return document nodes materialize them anyway.</para>
    /// </summary>
    internal sealed class TypeCheckingFilter : ProxyOutputter
    {
        private ItemType itemType;
        private int cardinality;
        private RoleDiagnostic roleDiagnostic;
        private ILocation locator;
        private int count = 0;
        private int level = 0;
        // used to avoid repeated checking when a template creates large numbers of elements of the same type
        // The key is a (namecode, typecode) pair, packed into a single long
        private readonly HashSet<long> checkedElements = new HashSet<long>();
        private readonly TypeHierarchy typeHierarchy;

        public TypeCheckingFilter(Outputter next) : base(next)
        {
            typeHierarchy = GetConfiguration().GetTypeHierarchy();
        }

        public void SetRequiredType(ItemType type, int cardinality, RoleDiagnostic roleDiagnostic, ILocation locator)
        {
            itemType = type;
            this.cardinality = cardinality;
            this.roleDiagnostic = roleDiagnostic;
            this.locator = locator;
        }

        public override void Namespace(string prefix, NamespaceUri namespaceUri, int properties)
        {
            if (level == 0)
            {
                if (++count == 2)
                {
                    CheckAllowsMany(Loc.NONE);
                }

                CheckItemType(NodeKindTest.NAMESPACE, Loc.NONE);
            }

            NextOutputter.Namespace(prefix, namespaceUri, properties);
        }

        public override void Attribute(INodeName attName, ISimpleType typeCode, string value, ILocation location, int properties)
        {
            if (level == 0)
            {
                if (++count == 2)
                {
                    CheckAllowsMany(location);
                }

                ItemType type = new CombinedNodeTest(
                    new NameTest(Types.Type.ATTRIBUTE, attName, GetConfiguration().GetNamePool()),
                    Token.INTERSECT,
                    new ContentTypeTest(Types.Type.ATTRIBUTE, typeCode, GetConfiguration(), false));
                CheckItemType(type, NodeSupplier((short)Types.Type.ATTRIBUTE, attName, typeCode, StringView.Tidy(value)), location);
            }

            NextOutputter.Attribute(attName, typeCode, value, location, properties);
        }

        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (level == 0)
            {
                if (++count == 2)
                {
                    CheckAllowsMany(locationId);
                }

                CheckItemType(NodeKindTest.TEXT, NodeSupplier((short)Types.Type.TEXT, null, null, chars.Tidy()), locationId);
            }

            NextOutputter.Characters(chars, locationId, properties);
        }

        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            if (level == 0)
            {
                if (++count == 2)
                {
                    CheckAllowsMany(locationId);
                }

                CheckItemType(NodeKindTest.COMMENT, NodeSupplier((short)Types.Type.COMMENT, null, null, chars.Tidy()), locationId);
            }

            NextOutputter.Comment(chars, locationId, properties);
        }

        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (level == 0)
            {
                if (++count == 2)
                {
                    CheckAllowsMany(locationId);
                }

                CheckItemType(NodeKindTest.PROCESSING_INSTRUCTION,
                    NodeSupplier((short)Types.Type.PROCESSING_INSTRUCTION, new NoNamespaceName(target), null, data.Tidy()), locationId);
            }

            NextOutputter.ProcessingInstruction(target, data, locationId, properties);
        }

        public override void StartDocument(int properties)
        {
            if (level == 0)
            {
                if (++count == 2)
                {
                    CheckAllowsMany(Loc.NONE);
                }

                CheckItemType(NodeKindTest.DOCUMENT,
                    NodeSupplier((short)Types.Type.DOCUMENT, null, null, EmptyUnicodeString.GetInstance()), Loc.NONE);
            }

            level++;
            NextOutputter.StartDocument(properties);
        }

        public override void StartElement(INodeName elemName, ISchemaType elemType, ILocation location, int properties)
        {
            CheckElementStart(elemName, elemType, location);
            NextOutputter.StartElement(elemName, elemType, location, properties);
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            CheckElementStart(elemName, type, location);
            NextOutputter.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        private void CheckElementStart(INodeName elemName, ISchemaType elemType, ILocation location)
        {
            Configuration config = GetConfiguration();
            NamePool namePool = config.GetNamePool();
            if (level == 0)
            {
                if (++count == 1)
                {
                    // don't bother with any caching on the first item, it will often be the only one
                    ItemType type = new CombinedNodeTest(
                        new NameTest(Types.Type.ELEMENT, elemName, namePool),
                        Token.INTERSECT,
                        new ContentTypeTest(Types.Type.ELEMENT, elemType, config, false));
                    CheckItemType(type, NodeSupplier((short)Types.Type.ELEMENT, elemName, elemType, EmptyUnicodeString.GetInstance()), location);
                }
                else
                {
                    if (count == 2)
                    {
                        CheckAllowsMany(location);
                    }

                    long key = (long)elemName.ObtainFingerprint(namePool) << 32 | (long)(uint)elemType.Fingerprint;
                    if (!checkedElements.Contains(key))
                    {
                        ItemType type = new CombinedNodeTest(
                            new NameTest(Types.Type.ELEMENT, elemName, namePool),
                            Token.INTERSECT,
                            new ContentTypeTest(Types.Type.ELEMENT, elemType, config, false));
                        CheckItemType(type, NodeSupplier((short)Types.Type.ELEMENT, elemName, elemType, EmptyUnicodeString.GetInstance()), location);
                        checkedElements.Add(key);
                    }
                }
            }

            level++;
        }

        public override void EndDocument()
        {
            level--;
            NextOutputter.EndDocument();
        }

        public override void EndElement()
        {
            level--;
            NextOutputter.EndElement();
        }

        public override void Close()
        {
            FinalCheck();
            base.Close();
        }

        public void FinalCheck()
        {
            if (count == 0 && !Cardinality.AllowsZero(cardinality))
            {
                string errorCode = roleDiagnostic.ErrorCode;
                XPathException err = new XPathException("An empty sequence is not allowed as the " +
                                                        roleDiagnostic.GetMessage())
                    .WithErrorCode(errorCode);
                if (!"XPDY0050".Equals(errorCode))
                {
                    err.SetIsTypeError(true);
                }

                throw err;
            }
        }

        private Func<NodeInfo> NodeSupplier(short nodeKind, INodeName name, ISchemaType type, UnicodeString value)
        {
            return () =>
            {
                Orphan o = new Orphan(GetPipelineConfiguration().GetConfiguration());
                o.SetNodeKind(nodeKind);
                if (name != null)
                {
                    o.SetNodeName(name);
                }

                o.SetTypeAnnotation(type);
                o.SetStringValue(value);
                return o;
            };
        }

        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (level == 0)
            {
                if (++count == 2)
                {
                    CheckAllowsMany(locationId);
                }

                CheckItem(item, locationId);
            }

            NextOutputter.Append(item, locationId, copyNamespaces);
        }

        public override void Append(IItem item)
        {
            if (level == 0)
            {
                if (++count == 2)
                {
                    CheckAllowsMany(Loc.NONE);
                }

                CheckItem(item, Loc.NONE);
            }

            NextOutputter.Append(item);
        }

        public override bool UsesTypeAnnotations()
        {
            return true;
        }

        private void CheckItemType(ItemType type, Func<NodeInfo> itemSupplier, ILocation locationId)
        {
            if (!typeHierarchy.IsSubType(type, itemType))
            {
                ThrowTypeError(type, itemSupplier(), locationId);
            }
        }

        private void CheckItemType(ItemType type, ILocation locationId)
        {
            if (!typeHierarchy.IsSubType(type, itemType))
            {
                ThrowTypeError(type, null, locationId);
            }
        }

        private void CheckItem(IItem item, ILocation locationId)
        {
            if (!itemType.Matches(item, typeHierarchy))
            {
                ThrowTypeError(null, item, locationId);
            }
        }

        private void ThrowTypeError(ItemType suppliedType, IItem item, ILocation locationId)
        {
            string message;
            if (item == null)
            {
                message = roleDiagnostic.ComposeErrorMessage(itemType, suppliedType);
            }
            else
            {
                message = roleDiagnostic.ComposeErrorMessage(itemType, item, typeHierarchy);
            }

            string errorCode = roleDiagnostic.ErrorCode;
            throw new XPathException(message, errorCode)
                .AsTypeErrorIf(!"XPDY0050".Equals(errorCode))
                .WithLocation(locationId == null ? locator : locationId.SaveLocation());
        }

        private void CheckAllowsMany(ILocation locationId)
        {
            if (!Cardinality.AllowsMany(cardinality))
            {
                throw new XPathException("A sequence of more than one item is not allowed as the " +
                                         roleDiagnostic.GetMessage())
                    .WithErrorCode(roleDiagnostic.ErrorCode)
                    .AsTypeErrorIf(!"XPDY0050".Equals(roleDiagnostic.ErrorCode))
                    .WithLocation(locationId == null || locationId == Loc.NONE ? locator : locationId);
            }
        }
    }
}
