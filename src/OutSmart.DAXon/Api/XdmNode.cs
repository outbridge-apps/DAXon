////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Net;
using System;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Api
{
    public class XdmNode : XdmItem
    {

        public NodeInfo UnderlyingValue => (NodeInfo)base.UnderlyingValue;

        public virtual XdmValue TypedValue
        {
            get
            {
                try
                {
                    IAtomicSequence v = UnderlyingNode.Atomize();
                    return XdmValue.Wrap(v);
                }
                catch (XPathException e)
                {
                    throw new DAXonApiException(e);
                }
                catch (RecursionDepthError e)
                {
                    throw new DAXonApiException(e.ToXPathException());
                }
            }
        }

        public virtual QName TypeAnnotationName
        {
            get
            {
                ISchemaType type = UnderlyingNode.GetSchemaType();
                return type == null ? null : new QName(type.GetStructuredQName());
            }
        }

        public virtual XdmNode Root
        {
            get
            {
                NodeInfo p = UnderlyingNode.Root;
                return p == null ? null : (XdmNode)XdmValue.Wrap(p);
            }
        }

        public virtual URI DocumentURI
        {
            get
            {
                try
                {
                    string systemId = UnderlyingNode.GetSystemId();
                    return systemId == null || (systemId.Length == 0) ? null : new URI(systemId);
                }
                catch (URISyntaxException e)
                {
                    throw new InvalidOperationException("documentURI", e);
                }
            }
        }

        public virtual NodeInfo UnderlyingNode => UnderlyingValue;

        public virtual object ExternalNode
        {
            get
            {
                NodeInfo saxonNode = UnderlyingNode;
                if (saxonNode is IVirtualNode)
                {
                    object externalNode = ((IVirtualNode)saxonNode).RealNode;
                    return externalNode is NodeInfo ? null : externalNode;
                }
                else
                {
                    return null;
                }
            }
        }
        public XdmNode(NodeInfo node) : base(node)
        {
        }

        public virtual XdmNodeKind GetNodeKind()
        {
            switch (UnderlyingNode.GetNodeKind())
            {
                case Types.Type.DOCUMENT:
                    return XdmNodeKind.DOCUMENT;
                case Types.Type.ELEMENT:
                    return XdmNodeKind.ELEMENT;
                case Types.Type.ATTRIBUTE:
                    return XdmNodeKind.ATTRIBUTE;
                case Types.Type.TEXT:
                    return XdmNodeKind.TEXT;
                case Types.Type.COMMENT:
                    return XdmNodeKind.COMMENT;
                case Types.Type.PROCESSING_INSTRUCTION:
                    return XdmNodeKind.PROCESSING_INSTRUCTION;
                case Types.Type.NAMESPACE:
                    return XdmNodeKind.NAMESPACE;
                default:
                    throw new InvalidOperationException("nodeKind");
            }
        }

        public virtual Processor GetProcessor()
        {
            Configuration config = UnderlyingNode.GetConfiguration();
            object originator = config.GetProcessor();
            if (originator is Processor)
            {
                return (Processor)originator;
            }
            else
            {
                return new Processor(new ProcessorOptions { Configuration = config });
            }
        }

        public virtual QName GetNodeName()
        {
            NodeInfo n = UnderlyingNode;
            switch (n.GetNodeKind())
            {
                case Types.Type.DOCUMENT:
                case Types.Type.TEXT:
                case Types.Type.COMMENT:
                    return null;
                case Types.Type.PROCESSING_INSTRUCTION:
                case Types.Type.NAMESPACE:
                    if ((n.GetLocalPart().Length == 0))
                    {
                        return null;
                    }
                    else
                    {
                        return new QName(new StructuredQName("", NamespaceUri.NULL, n.GetLocalPart()));
                    }

                case Types.Type.ELEMENT:
                case Types.Type.ATTRIBUTE:
                    return new QName(new StructuredQName(n.GetPrefix(), n.GetNamespaceUri(), n.GetLocalPart()));
                default:
                    return null;
            }
        }

        public virtual int GetLineNumber()
        {
            return UnderlyingNode.GetLineNumber();
        }

        public virtual int GetColumnNumber()
        {
            return UnderlyingNode.GetColumnNumber();
        }

        [Obsolete("Use AxisIterator(Axis): the same method, named as s9api names it (axisIterator).")]
        public virtual XdmSequenceIterator<XdmNode> IAxisIterator(Axis axis)
        {
            return AxisIterator(axis);
        }

        [Obsolete("Use AxisIterator(Axis, QName): the same method, named as s9api names it (axisIterator).")]
        public virtual XdmSequenceIterator<XdmNode> IAxisIterator(Axis axis, QName name)
        {
            return AxisIterator(axis, name);
        }

        public virtual XdmSequenceIterator<XdmNode> AxisIterator(Axis axis)
        {
            IAxisIterator @base = UnderlyingNode.IterateAxis(axis.GetAxisNumber());
            return XdmSequenceIterator<XdmNode>.OfNodes(@base);
        }

        public virtual XdmSequenceIterator<XdmNode> AxisIterator(Axis axis, QName name)
        {
            int kind;
            switch (axis)
            {
                case Axis.ATTRIBUTE:
                    kind = Types.Type.ATTRIBUTE;
                    break;
                case Axis.NAMESPACE:
                    kind = Types.Type.NAMESPACE;
                    break;
                default:
                    kind = Types.Type.ELEMENT;
                    break;
            }

            NodeInfo node = UnderlyingNode;
            NameTest test = new NameTest(kind, name.GetNamespaceUri(), name.LocalName, node.GetConfiguration().GetNamePool());
            IAxisIterator @base = node.IterateAxis(axis.GetAxisNumber(), test);
            return XdmSequenceIterator<XdmNode>.OfNodes(@base);
        }

        public virtual XdmNode GetParent()
        {
            NodeInfo p = UnderlyingNode.GetParent();
            return p == null ? null : (XdmNode)XdmValue.Wrap(p);
        }

        public virtual string GetAttributeValue(QName name)
        {
            NodeInfo node = UnderlyingNode;
            StructuredQName sq = name.GetStructuredQName();
            return node.GetAttributeValue(sq.GetNamespaceUri(), sq.GetLocalPart());
        }

        public virtual string Attribute(string name)
        {
            return UnderlyingNode.GetAttributeValue(NamespaceUri.NULL, name);
        }

        public virtual URI GetBaseURI()
        {
            try
            {
                string uri = UnderlyingNode.GetBaseURI();
                if (uri == null)
                {
                    return null;
                }

                return new URI(uri);
            }
            catch (URISyntaxException e)
            {
                throw new InvalidOperationException("baseURI", e);
            }
        }

        public override int GetHashCode()
        {
            return UnderlyingNode.GetHashCode();
        }

        public override bool Equals(object other)
        {
            return other is XdmNode && UnderlyingNode.Equals(((XdmNode)other).UnderlyingNode);
        }
    }
}
