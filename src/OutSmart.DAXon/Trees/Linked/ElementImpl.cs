////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Trees.Linked
{
    public class ElementImpl : ParentNodeImpl, INamespaceResolver, IInheritedBaseUri
    {
        private INodeName nodeName;
        private ISchemaType type = Untyped.INSTANCE;
        private IAttributeMap attributeMap; // this excludes namespace attributes
        private NamespaceMap namespaceMap = NamespaceMap.EmptyMap();

        public override NamespaceMap AllNamespaces => namespaceMap;
        public ElementImpl()
        {
            this.attributeMap = EmptyAttributeMap.GetInstance();
        }

        public override void SetAttributes(IAttributeMap atts)
        {
            this.attributeMap = atts;
        }

        public virtual void SetNodeName(INodeName name)
        {
            this.nodeName = name;
        }

        public virtual void Initialise(INodeName elemName, ISchemaType elementType, IAttributeMap atts, NodeInfo parent, int sequenceNumber)
        {
            this.nodeName = elemName;
            this.type = elementType;
            SetRawParent((ParentNodeImpl)parent);
            SetRawSequenceNumber(sequenceNumber);
            attributeMap = atts;
        }

        public override INodeName GetNodeName()
        {
            return nodeName;
        }

        public virtual void SetLocation(string systemId, int line, int column)
        {
            DocumentImpl root = GetRawParent().PhysicalRoot;
            root.SetLineAndColumn(GetRawSequenceNumber(), line, column);
            root.SetSystemId(GetRawSequenceNumber(), systemId);
        }

        public override void SetSystemId(string uri)
        {
            PhysicalRoot.SetSystemId(GetRawSequenceNumber(), uri);
        }

        public override string GetSystemId()
        {
            DocumentImpl root = PhysicalRoot;
            return root == null ? null : root.GetSystemId(GetRawSequenceNumber());
        }

        public override string GetBaseURI()
        {
            return Navigator.GetBaseURI(this, (n) => PhysicalRoot.IsTopWithinEntity((ElementImpl)n));
        }

        bool IInheritedBaseUri.IsTopWithinEntity() => PhysicalRoot.IsTopWithinEntity(this);

        string IInheritedBaseUri.KnownBaseUri => KnownBaseUri;

        void IInheritedBaseUri.RememberBaseUri(string uri) => RememberBaseUri(uri);

        // The document caches resolved base URIs, as the tiny tree does; StyleElement keeps its own.
        internal virtual string KnownBaseUri => PhysicalRoot?.GetKnownBaseUri(this);

        internal virtual void RememberBaseUri(string uri)
        {
            PhysicalRoot?.SetKnownBaseUri(this, uri);
        }

        public override bool IsNilled()
        {
            return PhysicalRoot.IsNilledElement(this);
        }

        public override void SetTypeAnnotation(ISchemaType type)
        {
            this.type = type;
        }

        /// <summary>
        /// Say that the element has the nilled property
        /// </summary>
        public virtual void SetNilled()
        {
            PhysicalRoot.AddNilledElement(this);
        }

        /// <summary>
        /// Say that the element has the nilled property
        /// </summary>
        public override ISchemaType GetSchemaType()
        {
            return type;
        }

        public override int GetLineNumber()
        {
            DocumentImpl root = PhysicalRoot;
            if (root == null)
            {
                return -1;
            }
            else
            {
                return root.GetLineNumber(GetRawSequenceNumber());
            }
        }

        public override int GetColumnNumber()
        {
            DocumentImpl root = PhysicalRoot;
            if (root == null)
            {
                return -1;
            }
            else
            {
                return root.GetColumnNumber(GetRawSequenceNumber());
            }
        }

        public override void GenerateId(StringBuilder buffer)
        {
            int sequence = GetRawSequenceNumber();
            if (sequence >= 0)
            {
                PhysicalRoot.GenerateId(buffer);
                buffer.Append('e');
                buffer.Append(sequence);
            }
            else
            {
                GetRawParent().GenerateId(buffer);
                buffer.Append('f');
                buffer.Append(GetSiblingPosition());
            }
        }

        public override int GetNodeKind()
        {
            return Types.Type.ELEMENT;
        }

        public override IAttributeMap Attributes()
        {
            return attributeMap;
        }

        public virtual IAxisIterator IterateAttributes(NodeTest test)
        {
            if (attributeMap is AttributeMapWithIdentity)
            {

                // this case needs special care because of the possibility of deleted attribute nodes
                return new Navigator.AxisFilter(((AttributeMapWithIdentity)attributeMap).IterateAttributes(this), test);
            }
            else
            {
                return new AttributeAxisIterator(this, test);
            }
        }

        public override void Copy(IReceiver @out, int copyOptions, ILocation location)
        {
            // A walk of the subtree, as the tiny tree's copy: recursing per level made the depth the
            // input document's, and 1000 levels refused on a 256KB thread.
            Func<NodeInfo, Object> informee = @out.GetPipelineConfiguration().CopyInformee;
            Stack<ILocation> outer = null;   // the open ancestors' locations, when an informee rewrites them
            ElementImpl element = this;
            location = StartCopy(@out, copyOptions, location, informee);
            NodeImpl next = GetFirstChild();
            while (true)
            {
                Controller.CheckActiveTimeout();   // a copy is as long as its input: per node, as the parser
                if (next == null)
                {
                    @out.EndElement();
                    if (element == this)
                    {
                        return;
                    }

                    next = element.GetNextSibling();
                    element = (ElementImpl)element.GetRawParent();
                    if (outer != null)
                    {
                        location = outer.Pop();
                    }
                }
                else if (next is ElementImpl child)
                {
                    if (informee != null)
                    {
                        (outer ?? (outer = new Stack<ILocation>())).Push(location);
                    }

                    location = child.StartCopy(@out, copyOptions, location, informee);
                    element = child;
                    next = child.GetFirstChild();
                }
                else
                {
                    next.Copy(@out, copyOptions, location);
                    next = next.GetNextSibling();
                }
            }
        }

        // One start tag of a copy. Returns the location the element's children are copied with.
        private ILocation StartCopy(IReceiver @out, int copyOptions, ILocation location, Func<NodeInfo, Object> informee)
        {
            bool copyTypes = CopyOptions.Includes(copyOptions, CopyOptions.TYPE_ANNOTATIONS);
            bool copyForUpdate = CopyOptions.Includes(copyOptions, CopyOptions.FOR_UPDATE);
            ISchemaType typeCode = copyTypes ? GetSchemaType() : Untyped.INSTANCE;
            if (informee != null)
            {
                object o = informee(this);
                if (o is ILocation)
                {
                    location = (ILocation)o;
                }
            }

            NamespaceMap nsMap;
            bool gatherAttributeNamespaces = false;
            if (CopyOptions.Includes(copyOptions, CopyOptions.ALL_NAMESPACES))
            {
                nsMap = AllNamespaces;
            }
            else
            {
                // As the tiny tree (Saxon bugs 5616, 6866): no xmlns="" for a no-namespace element, which
                // would show as a "" in-scope prefix once copied under a default namespace.
                nsMap = GetNamespaceUri().IsEmpty() ? NamespaceMap.EmptyMap() : NamespaceMap.Of(GetPrefix(), GetNamespaceUri());
                gatherAttributeNamespaces = true;
            }

            bool disallowNamespaceSensitiveContent = ((copyOptions & CopyOptions.TYPE_ANNOTATIONS) != 0) && ((copyOptions & CopyOptions.ALL_NAMESPACES) == 0);
            if (copyTypes && disallowNamespaceSensitiveContent)
            {
                try
                {
                    CheckNotNamespaceSensitiveElement(GetSchemaType());
                }
                catch (CopyNamespaceSensitiveException e)
                {
                    throw e.WithErrorCode(@out.GetPipelineConfiguration().IsXSLT() ? "XTTE0950" : "XQTY0086");
                }
            }

            IList<AttributeInfo> atts = new List<AttributeInfo>(Attributes().Size());
            foreach (AttributeInfo att in Attributes())
            {
                ISimpleType attributeType = BuiltInAtomicType.UNTYPED_ATOMIC;
                if (copyTypes)
                {
                    attributeType = att.GetType();
                    if (disallowNamespaceSensitiveContent)
                    {
                        try
                        {
                            CheckNotNamespaceSensitiveAttribute(attributeType, att);
                        }
                        catch (CopyNamespaceSensitiveException e)
                        {
                            throw e.WithErrorCode(@out.GetPipelineConfiguration().IsXSLT() ? "XTTE0950" : "XQTY0086");
                        }
                    }
                }

                atts.Add(new AttributeInfo(att.GetNodeName(), attributeType, att.Value, att.GetLocation(), 0));
                if (gatherAttributeNamespaces && !(att.GetNodeName().GetPrefix().Length == 0))
                {
                    nsMap = nsMap.Put(att.GetNodeName().GetPrefix(), att.GetNodeName().GetNamespaceUri());
                }
            }

            int receiverOptions = ReceiverOption.BEQUEATH_INHERITED_NAMESPACES_ONLY | ReceiverOption.NAMESPACE_OK;
            if (copyForUpdate)
            {
                receiverOptions |= ReceiverOption.MUTABLE_TREE;
            }

            @out.StartElement(NameOfNode.MakeName(this), typeCode, SequenceTool.AttributeMapFromList(atts), nsMap, location, receiverOptions);
            return location;
        }

        protected virtual void CheckNotNamespaceSensitiveElement(ISchemaType type)
        {
            if (type is ISimpleType && ((ISimpleType)type).IsNamespaceSensitive())
            {
                if (type.IsAtomicType())
                {
                    throw new CopyNamespaceSensitiveException("Cannot copy QName or NOTATION values without copying namespaces");
                }
                else
                {

                    // For a union or list type, we need to check whether the actual value is namespace-sensitive
                    IAtomicSequence value = Atomize();
                    foreach (AtomicValue val in value)
                    {
                        if (val.PrimitiveType.IsNamespaceSensitive())
                        {
                            throw new CopyNamespaceSensitiveException("Cannot copy QName or NOTATION values without copying namespaces");
                        }
                    }
                }
            }
        }

        private void CheckNotNamespaceSensitiveAttribute(ISimpleType type, AttributeInfo att)
        {
            if (type.IsNamespaceSensitive())
            {
                if (type.IsAtomicType())
                {
                    throw new CopyNamespaceSensitiveException("Cannot copy QName or NOTATION values without copying namespaces");
                }
                else
                {

                    // For a union or list type, we need to check whether the actual value is namespace-sensitive
                    IAtomicSequence value = type.GetTypedValue(att.XdmStringValue.UnicodeStringValue, namespaceMap, GetConfiguration().GetConversionRules());
                    foreach (AtomicValue val in value)
                    {
                        if (val.PrimitiveType.IsNamespaceSensitive())
                        {
                            throw new CopyNamespaceSensitiveException("Cannot copy QName or NOTATION values without copying namespaces");
                        }
                    }
                }
            }
        }

        public override void Delete()
        {
            DocumentImpl root = PhysicalRoot;
            base.Delete();
            if (root != null)
            {
                IAxisIterator iter = IterateAxis(AxisInfo.DESCENDANT_OR_SELF, NodeKindTest.ELEMENT);
                while (true)
                {
                    ElementImpl n = (ElementImpl)iter.Next();
                    foreach (AttributeInfo att in attributeMap)
                    {
                        if (att.IsId())
                        {
                            root.DeregisterID(att.Value);
                        }
                    }

                    if (n == null)
                    {
                        break;
                    }

                    root.DeIndex(n);
                }
            }
        }

        public override void Rename(INodeName newName, bool inherit)
        {
            string prefix = newName.GetPrefix();
            NamespaceUri uri = newName.GetNamespaceUri();
            NamespaceBinding ns = new NamespaceBinding(prefix, uri);
            NamespaceUri uc = GetURIForPrefix(prefix, true);
            if (uc == null)
            {
                uc = NamespaceUri.NULL;
            }

            if (!uc.Equals(uri))
            {
                if (uc.IsEmpty())
                {
                    AddNamespace(ns, inherit);
                }
                else
                {
                    throw new ArgumentException("Namespace binding of new name conflicts with existing namespace binding");
                }
            }

            nodeName = newName;
        }

        public override void AddNamespace(NamespaceBinding binding, bool inherit)
        {
            if (binding.GetNamespaceUri().IsEmpty())
            {
                throw new ArgumentException("Cannot add a namespace undeclaration");
            }

            NamespaceUri existing = namespaceMap.GetNamespaceUri(binding.GetPrefix());
            if (existing != null)
            {
                if (!existing.Equals(binding.GetNamespaceUri()))
                {
                    throw new ArgumentException("New namespace conflicts with existing namespace binding");
                }
            }
            else
            {
                NamespaceMap oldMap = namespaceMap;
                namespaceMap = namespaceMap.Put(binding.GetPrefix(), binding.GetNamespaceUri());
                if (inherit && namespaceMap != oldMap)
                {
                    foreach (NodeInfo child in Children(NodeKindTest.ELEMENT))
                    {
                        ((ElementImpl)child).InheritParentNamespaces(binding, oldMap, namespaceMap);
                    }
                }
            }
        }

        private void InheritParentNamespaces(NamespaceBinding binding, NamespaceMap oldParentMap, NamespaceMap newParentMap)
        {
            NamespaceMap oldMap = namespaceMap;
            if (oldMap.GetURIForPrefix(binding.GetPrefix(), false) == null)
            {
                if (namespaceMap == oldParentMap)
                {
                    namespaceMap = newParentMap;
                }
                else
                {
                    namespaceMap = namespaceMap.Put(binding.GetPrefix(), binding.GetNamespaceUri());
                }

                foreach (NodeInfo child in Children(NodeKindTest.ELEMENT))
                {
                    ((ElementImpl)child).InheritParentNamespaces(binding, oldMap, namespaceMap);
                }
            }
        }

        public override void ReplaceStringValue(UnicodeString stringValue)
        {
            if (stringValue.IsEmpty())
            {
                SetChildren(null);
            }
            else
            {
                TextImpl text = new TextImpl(stringValue);
                text.SetRawParent(this);
                SetChildren(text);
            }
        }

        public virtual void SetAttributeInfo(int index, AttributeInfo attInfo)
        {
            AttributeMapWithIdentity attMap = PrepareAttributesForUpdate();
            attMap = attMap.Set(index, attInfo);
            SetAttributes(attMap);
        }

        private AttributeMapWithIdentity PrepareAttributesForUpdate()
        {
            if (Attributes() is AttributeMapWithIdentity)
            {
                return (AttributeMapWithIdentity)Attributes();
            }
            else
            {
                AttributeMapWithIdentity newAtts = new AttributeMapWithIdentity(Attributes().AsList());
                SetAttributes(newAtts);
                return newAtts;
            }
        }

        public override void AddAttribute(INodeName nodeName, ISimpleType attType, string value, int properties, bool inheritNamespaces)
        {
            AttributeMapWithIdentity atts = PrepareAttributesForUpdate();
            atts = atts.Add(new AttributeInfo(nodeName, attType, value, Loc.NONE, ReceiverOption.NONE));
            SetAttributes(atts);
            if (!nodeName.HasURI(NamespaceUri.NULL))
            {

                // The new attribute name @is in a namespace
                NamespaceBinding binding = nodeName.GetNamespaceBinding();
                string prefix = binding.GetPrefix();
                NamespaceUri uc = GetURIForPrefix(prefix, false);
                if (uc == null)
                {

                    // The namespace is not already declared on the element
                    AddNamespace(binding, inheritNamespaces);
                }
                else if (!uc.Equals(binding.GetNamespaceUri()))
                {
                    throw new InvalidOperationException("Namespace binding of new name conflicts with existing namespace binding");
                }
            }

            if (ReceiverOption.Contains(properties, ReceiverOption.IS_ID))
            {
                DocumentImpl root = PhysicalRoot;
                if (root != null)
                {
                    root.RegisterID(this, Whitespace.Trim(value));
                }
            }
        }

        public override void RemoveAttribute(NodeInfo attribute)
        {
            if (!(attribute is AttributeImpl))
            {
                return; // no action
            }

            int index = ((AttributeImpl)attribute).GetSiblingPosition();
            AttributeInfo info = Attributes().ItemAt(index);
            AttributeMapWithIdentity atts = PrepareAttributesForUpdate();
            atts = atts.Remove(index);
            SetAttributes(atts);
            if (index >= 0 && info.IsId())
            {
                DocumentImpl root = PhysicalRoot;
                root.DeregisterID(info.Value);
            }

            ((AttributeImpl)attribute).SetRawParent(null);
        }

        public override void RemoveNamespace(string prefix)
        {
            if (prefix == null)
                throw new NullReferenceException();
            if (prefix.Equals(GetPrefix()))
            {
                throw new InvalidOperationException("Cannot remove binding of namespace prefix used on the element name");
            }

            foreach (AttributeInfo att in attributeMap)
            {
                if (att.GetNodeName().GetPrefix().Equals(prefix))
                {
                    throw new InvalidOperationException("Cannot remove binding of namespace prefix used on an existing attribute name");
                }
            }

            namespaceMap = namespaceMap.Remove(prefix);
        }

        public override void AddNamespace(string prefix, NamespaceUri uri)
        {
            NamespaceUri existingURI = namespaceMap.GetNamespaceUri(prefix);
            if (existingURI == null)
            {
                namespaceMap = namespaceMap.Put(prefix, uri);
            }
            else if (!existingURI.Equals(uri))
            {
                throw new InvalidOperationException("New namespace binding conflicts with existing namespace binding");
            }
        }

        public override void RemoveTypeAnnotation()
        {
            if (GetSchemaType() != Untyped.INSTANCE)
            {
                type = AnyType.INSTANCE;
                GetRawParent().RemoveTypeAnnotation();
            }
        }

        public virtual void SetNamespaceMap(NamespaceMap map)
        {
            namespaceMap = map;
        }

        public NamespaceUri GetURIForPrefix(string prefix, bool useDefault)
        {
            if ((prefix.Length == 0))
            {
                if (useDefault)
                {
                    return namespaceMap.DefaultNamespace;
                }
                else
                {
                    return NamespaceUri.NULL;
                }
            }
            else
            {
                return namespaceMap.GetNamespaceUri(prefix);
            }
        }

        public IEnumerator<string> IteratePrefixes()
        {
            return namespaceMap.IteratePrefixes();
        }

        public virtual bool IsInScopeNamespace(NamespaceUri uri)
        {
            foreach (NamespaceBinding b in namespaceMap)
            {
                if (b.GetNamespaceUri().Equals(uri))
                {
                    return true;
                }
            }

            return false;
        }

        public override NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer)
        {
            IList<NamespaceBinding> bindings = new List<NamespaceBinding>();
            foreach (NamespaceBinding nb in namespaceMap)
            {
                bindings.Add(nb);
            }

            return bindings.ToArray();
        }

        public virtual void FixupInsertedNamespaces(bool inherit)
        {
            if (GetRawParent().GetNodeKind() == Types.Type.DOCUMENT)
            {
                return;
            }

            ElementImpl parent = (ElementImpl)GetRawParent();
            NamespaceMap parentNamespaces = parent.namespaceMap;

            // Namespaces present on the parent but not on the child should be undeclared (if requested)
            if (inherit)
            {
                DeepAddNamespaces(parentNamespaces);
            }
        }

        private void DeepAddNamespaces(NamespaceMap inheritedNamespaces)
        {
            NamespaceMap childNamespaces = namespaceMap;
            foreach (NamespaceBinding binding in inheritedNamespaces)
            {
                if (childNamespaces.GetNamespaceUri(binding.GetPrefix()) == null)
                {
                    childNamespaces = childNamespaces.Put(binding.GetPrefix(), binding.GetNamespaceUri());
                }
                else
                {
                    inheritedNamespaces = inheritedNamespaces.Remove(binding.GetPrefix());
                }
            }

            namespaceMap = childNamespaces;
            foreach (NodeInfo child in Children(NodeSelector.Of(new TypeIsInstancePredicate(typeof(ElementImpl)))))
            {
                ((ElementImpl)child).DeepAddNamespaces(inheritedNamespaces);
            }
        }

        public override string GetAttributeValue(NamespaceUri uri, string localName)
        {
            return attributeMap == null ? null : attributeMap.GetValue(uri, localName);
        }

        public override bool IsId()
        {

            // This is an approximation. For a union type, we check that the actual value is a valid NCName,
            // but we don't check that it was validated against the member type of the union that is an ID type.
            try
            {
                ISchemaType type = GetSchemaType();
                return type.Fingerprint == StandardNames.XS_ID || type.IsIdType() && NameChecker.IsValidNCName(UnicodeStringValue.CodePoints());
            }
            catch (MissingComponentException e)
            {
                return false;
            }
        }

        public override bool IsIdref()
        {
            return IsIdRefNode(this);
        }

        public static bool IsIdRefNode(NodeImpl node)
        {
            ISchemaType type = node.GetSchemaType();
            try
            {
                if (type.IsIdRefType())
                {
                    if (type == BuiltInAtomicType.IDREF || type == BuiltInListType.IDREFS)
                    {
                        return true;
                    }

                    try
                    {
                        foreach (AtomicValue av in node.Atomize())
                        {
                            if (av.GetItemType().IsIdRefType())
                            {
                                return true;
                            }
                        }
                    }
                    catch (XPathException err)
                    {
                    }
                }
            }
            catch (MissingComponentException e)
            {
                return false;
            }

            return false;
        }
    }
}
