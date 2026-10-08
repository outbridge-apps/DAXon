////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.XQuery
{
    public class SequenceWrapper : SequenceReceiver
    {
        public static readonly NamespaceUri RESULT_NS = NamespaceUri.Of(QueryResult.RESULT_NS);
        private readonly ComplexContentOutputter @out;
        private int depth;
        private FingerprintedQName resultDocument;
        private FingerprintedQName resultElement;
        private FingerprintedQName resultAttribute;
        private FingerprintedQName resultText;
        private FingerprintedQName resultComment;
        private FingerprintedQName resultPI;
        private FingerprintedQName resultNamespace;
        private FingerprintedQName resultAtomicValue;
        private FingerprintedQName resultFunction;
        private FingerprintedQName resultMap;
        private FingerprintedQName resultMapEntry;
        private FingerprintedQName resultMapKey;
        private FingerprintedQName resultMapValue;
        private FingerprintedQName resultArray;
        private FingerprintedQName resultArrayMember;
        private FingerprintedQName resultExternalValue;
        private FingerprintedQName xsiType;

        public virtual ComplexContentOutputter Destination => @out;
        public SequenceWrapper(IReceiver destination) : base(destination.GetPipelineConfiguration())
        {
            @out = new ComplexContentOutputter(destination); // @out = new TracingFilter(@out);
        }

        private void StartWrapper(INodeName name)
        {
            @out.StartElement(name, Untyped.INSTANCE, Loc.NONE, ReceiverOption.NONE);
            @out.Namespace("xs", NamespaceUri.SCHEMA, ReceiverOption.NONE);
            @out.Namespace("xsi", NamespaceUri.SCHEMA_INSTANCE, ReceiverOption.NONE);
            @out.StartContent();
        }

        private void EndWrapper()
        {
            @out.EndElement();
        }

        public override void Open()
        {

            //@SuppressWarnings({"FieldCanBeLocal"})
            FingerprintedQName resultSequence = new FingerprintedQName("result", RESULT_NS, "sequence");
            resultDocument = new FingerprintedQName("result", RESULT_NS, "document");
            resultElement = new FingerprintedQName("result", RESULT_NS, "element");
            resultAttribute = new FingerprintedQName("result", RESULT_NS, "attribute");
            resultText = new FingerprintedQName("result", RESULT_NS, "text");
            resultComment = new FingerprintedQName("result", RESULT_NS, "comment");
            resultPI = new FingerprintedQName("result", RESULT_NS, "processing-instruction");
            resultNamespace = new FingerprintedQName("result", RESULT_NS, "namespace");
            resultAtomicValue = new FingerprintedQName("result", RESULT_NS, "atomic-value");
            resultFunction = new FingerprintedQName("result", RESULT_NS, "function");
            resultArray = new FingerprintedQName("result", RESULT_NS, "array");
            resultArrayMember = new FingerprintedQName("result", RESULT_NS, "array-member");
            resultMap = new FingerprintedQName("result", RESULT_NS, "map");
            resultMapEntry = new FingerprintedQName("result", RESULT_NS, "map-entry");
            resultMapKey = new FingerprintedQName("result", RESULT_NS, "map-key");
            resultMapValue = new FingerprintedQName("result", RESULT_NS, "map-value");
            resultExternalValue = new FingerprintedQName("result", RESULT_NS, "external-object");
            xsiType = new FingerprintedQName("xsi", NamespaceUri.SCHEMA_INSTANCE, "type");
            @out.Open();
            @out.StartDocument(ReceiverOption.NONE);
            StartWrapper(resultSequence);
        }

        public override void StartDocument(int properties)
        {
            StartWrapper(resultDocument);
            depth++;
        }

        /// <summary>
        /// Notify the end of a document node
        /// </summary>
        public override void EndDocument()
        {
            EndWrapper();
            depth--;
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            if (depth++ == 0)
            {
                StartWrapper(resultElement);
            }

            @out.StartElement(elemName, type, location, properties);
            foreach (AttributeInfo att in attributes)
            {
                @out.Attribute(att.GetNodeName(), att.GetType(), att.Value, att.GetLocation(), att.GetProperties());
            }

            @out.StartContent();
        }

        /// <summary>
        /// End of element
        /// </summary>
        public override void EndElement()
        {
            @out.EndElement();
            if (--depth == 0)
            {
                EndWrapper();
            }
        }

        /// <summary>
        /// Character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (depth == 0)
            {
                StartWrapper(resultText);
                @out.Characters(chars, locationId, properties);
                EndWrapper();
            }
            else
            {
                @out.Characters(chars, locationId, properties);
            }
        }

        /// <summary>
        /// Output a comment
        /// </summary>
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            if (depth == 0)
            {
                StartWrapper(resultComment);
                @out.Comment(chars, locationId, properties);
                EndWrapper();
            }
            else
            {
                @out.Comment(chars, locationId, properties);
            }
        }

        /// <summary>
        /// Processing Instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (depth == 0)
            {
                StartWrapper(resultPI);
                @out.ProcessingInstruction(target, data, locationId, properties);
                EndWrapper();
            }
            else
            {
                @out.ProcessingInstruction(target, data, locationId, properties);
            }
        }

        /// <summary>
        /// Output an item (atomic value or node) to the sequence
        /// </summary>
        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (item is AtomicValue)
            {
                NamePool pool = GetNamePool();
                @out.StartElement(resultAtomicValue, Untyped.INSTANCE, Loc.NONE, ReceiverOption.NONE);
                IAtomicType type = ((AtomicValue)item).GetItemType();
                StructuredQName name = type.GetStructuredQName();
                string prefix = name.GetPrefix();
                string localName = name.GetLocalPart();
                NamespaceUri uri = name.GetNamespaceUri();
                if ((prefix.Length == 0))
                {
                    prefix = pool.SuggestPrefixForURI(uri);
                    if (prefix == null)
                    {
                        prefix = "p" + uri.GetHashCode();
                    }
                }

                string displayName = prefix + ':' + localName;
                @out.Namespace(prefix, uri, ReceiverOption.NONE);
                @out.Attribute(xsiType, BuiltInAtomicType.UNTYPED_ATOMIC, displayName, locationId, ReceiverOption.NONE);
                @out.StartContent();
                @out.Characters(item.UnicodeStringValue, locationId, ReceiverOption.NONE);
                @out.EndElement();
            }
            else if (item is NodeInfo)
            {
                NodeInfo node = (NodeInfo)item;
                int kind = node.GetNodeKind();
                if (kind == Types.Type.ATTRIBUTE)
                {
                    Attribute(NameOfNode.MakeName(node), (ISimpleType)node.GetSchemaType(), node.GetStringValue(), Loc.NONE, 0);
                }
                else if (kind == Types.Type.NAMESPACE)
                {
                    Namespace(new NamespaceBinding(node.GetLocalPart(), NamespaceUri.Of(node.GetStringValue())), 0);
                }
                else
                {
                    ((NodeInfo)item).Copy(this, CopyOptions.ALL_NAMESPACES | CopyOptions.TYPE_ANNOTATIONS, locationId);
                }
            }
            else if (item is MapItem)
            {
                ComplexContentOutputter @out = Destination;
                @out.StartElement(resultMap, Untyped.INSTANCE, locationId, ReceiverOption.NONE);
                MapItem map = (MapItem)item;
                foreach (OutSmart.DAXon.Values.Maps.KeyValuePair pair in map.KeyValuePairs())
                {
                    @out.StartElement(resultMapEntry, Untyped.INSTANCE, locationId, ReceiverOption.NONE);
                    @out.StartElement(resultMapKey, Untyped.INSTANCE, locationId, ReceiverOption.NONE);
                    Append(pair.key);
                    @out.EndElement();
                    @out.StartElement(resultMapValue, Untyped.INSTANCE, locationId, ReceiverOption.NONE);
                    ISequenceIterator value = pair.value.Iterate();
                    IItem valItem;
                    while ((valItem = value.Next()) != null)
                    {
                        Append(valItem);
                    }

                    @out.EndElement();
                    @out.EndElement();
                }

                @out.EndElement();
            }
            else if (item is ArrayItem)
            {
                @out.StartElement(resultArray, Untyped.INSTANCE, Loc.NONE, ReceiverOption.NONE);
                @out.StartContent();
                foreach (IGroundedValue mem in ((ArrayItem)item).Members())
                {
                    @out.StartElement(resultArrayMember, Untyped.INSTANCE, Loc.NONE, ReceiverOption.NONE);
                    ISequenceIterator value = mem.Iterate();
                    IItem valItem;
                    while ((valItem = value.Next()) != null)
                    {
                        Append(valItem);
                    }

                    @out.EndElement();
                }

                @out.EndElement();
            }
            else if (item is IFunctionItem)
            {
                @out.StartElement(resultFunction, Untyped.INSTANCE, Loc.NONE, ReceiverOption.NONE);
                @out.StartContent();
                @out.Characters(StringView.Of(((IFunctionItem)item).Description), locationId, ReceiverOption.NONE);
                @out.EndElement();
            }
            else if (item.GetGenre() == Genre.EXTERNAL)
            {
                object obj = ((ObjectValue<object>)item).GetObject();
                @out.StartElement(resultExternalValue, Untyped.INSTANCE, Loc.NONE, ReceiverOption.NONE);
                @out.Attribute(new NoNamespaceName("class"), BuiltInAtomicType.UNTYPED_ATOMIC, obj.GetType().FullName, Loc.NONE, ReceiverOption.NONE);
                @out.StartContent();
                @out.Characters(StringView.Of(obj.ToString()), locationId, ReceiverOption.NONE);
                @out.EndElement();
            }
        }

        public override void Close()
        {
            EndWrapper(); // close the result:sequence element
            @out.EndDocument();
            @out.Close();
        }

        public override bool UsesTypeAnnotations()
        {
            return true;
        }

        private void Attribute(INodeName attName, ISimpleType typeCode, string value, ILocation locationId, int properties)
        {
            IAttributeMap atts = SingletonAttributeMap.Of(new AttributeInfo(attName, typeCode, value.ToString(), locationId, properties));
            NamespaceMap ns = NamespaceMap.EmptyMap();
            if (!attName.HasURI(NamespaceUri.NULL))
            {
                ns = ns.Put(attName.GetPrefix(), attName.GetNamespaceUri());
            }

            @out.StartElement(resultAttribute, Untyped.INSTANCE, atts, ns, Loc.NONE, 0);
            @out.StartContent();
            @out.EndElement();
        }

        private void Namespace(INamespaceBindingSet namespaceBindings, int properties)
        {
            NamespaceMap ns = NamespaceMap.EmptyMap();
            ns = ns.AddAll(namespaceBindings);
            @out.StartElement(resultNamespace, Untyped.INSTANCE, EmptyAttributeMap.GetInstance(), ns, Loc.NONE, 0);
            @out.StartContent();
            @out.EndElement();
        }
    }
}
