////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using static OutSmart.DAXon.Events.RegularSequenceChecker.State;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;

using OutSmart.DAXon.Serialization;
namespace OutSmart.DAXon.Events
{
    public sealed class ComplexContentOutputter : Outputter, IReceiver, IResultTarget
    {
        private IReceiver nextReceiver;
        private INodeName pendingStartTag = null;
        private int level = -1;
        private bool[] currentLevelIsDocument = new bool[20];
        private readonly IList<AttributeInfo> pendingAttributes = new List<AttributeInfo>();
        private NamespaceMap pendingNSMap;
        private readonly Stack<NamespaceMap> inheritedNamespaces = new Stack<NamespaceMap>();
        private ISchemaType currentSimpleType = null; // any other value means we are currently writing an
        private int startElementProperties;
        private ILocation startElementLocationId = Loc.NONE;
        private HostLanguage hostLanguage = HostLanguage.XSLT;
        private RegularSequenceChecker.State state = INITIAL;
        private bool previousAtomic = false;
        private UnicodeStringReceiver cachedStringReceiver;

        // Direct-mapped memo of the StartContent namespace pipeline. A repeating instruction
        // fires with the same NodeName / constant namespace-map / inherited-map objects every
        // time, so reference compares replace the prefix check + merge and their per-element
        // allocations. Keyed (not just indexed) by the object identities: the slot index only
        // spreads distinct instructions across the table, a collision merely recomputes.
        // Allocated lazily after NS_MEMO_WARMUP flushes: a CCO is constructed per evaluation
        // on the push-elaborator paths, so a small variable tree must not pay ~3KB of arrays.
        private const int NS_MEMO_MASK = 63;
        private const int NS_MEMO_WARMUP = 64;
        private byte nsMemoFlushes;
        private INodeName[] nsMemoNameIn;
        private INodeName[] nsMemoNameOut;
        private NamespaceMap[] nsMemoMapIn;
        private NamespaceMap[] nsMemoInherited;
        private NamespaceMap[] nsMemoMapOut;
        private int[] nsMemoProps;

        public IReceiver Receiver { get => nextReceiver; set => this.nextReceiver = value; }

        /**/
        /**/
        private string ErrorCodeForDecomposingFunctionItems => GetPipelineConfiguration().IsXSLT() ? "XTDE0450" : "XQTY0105";
        public ComplexContentOutputter(IReceiver next)
        {

            PipelineConfiguration pipe = next.GetPipelineConfiguration();
            SetPipelineConfiguration(pipe);
            Receiver = next;
            if (pipe == null)
                throw new NullReferenceException();
            SetHostLanguage(pipe.GetHostLanguage());
            inheritedNamespaces.Push(NamespaceMap.EmptyMap());
        }

        public static ComplexContentOutputter MakeComplexContentReceiver(IReceiver receiver, ParseOptions options)
        {

            string systemId = receiver.GetSystemId();
            bool validate = options != null && options.GetSchemaValidationMode() != Validation.PRESERVE;

            // add a validator to the pipeline if required
            if (validate)
            {
                Configuration config = receiver.GetPipelineConfiguration().GetConfiguration();
                receiver = config.GetDocumentValidator(receiver, systemId, options, null);
            }

            ComplexContentOutputter result = new ComplexContentOutputter(receiver);
            result.SetSystemId(systemId);
            return result;
        }

        public override void SetPipelineConfiguration(PipelineConfiguration pipe)
        {
            if (pipelineConfiguration != pipe)
            {
                pipelineConfiguration = pipe;
                if (nextReceiver != null)
                {
                    nextReceiver.SetPipelineConfiguration(pipe);
                }
            }
        }

        public override void SetSystemId(string systemId)
        {
            base.SetSystemId(systemId);
            nextReceiver.SetSystemId(systemId);
        }

        public void SetHostLanguage(HostLanguage language)
        {
            hostLanguage = language;
        }

        /// <summary>
        /// Start the output process
        /// </summary>
        public override void Open()
        {
            nextReceiver.Open();
            previousAtomic = false;
            state = OPEN;
        }

        /// <summary>
        /// Start the output process
        /// </summary>
        public override void StartDocument(int properties)
        {
            level++;
            if (level == 0)
            {
                nextReceiver.StartDocument(properties);
            }
            else if (state == START_TAG)
            {
                StartContent();
            }

            previousAtomic = false;
            if (currentLevelIsDocument.Length < level + 1)
            {
                Array.Resize(ref currentLevelIsDocument, level * 2);
            }

            currentLevelIsDocument[level] = true;
            state = CONTENT;
        }

        public override void EndDocument()
        {
            if (level == 0)
            {
                nextReceiver.EndDocument();
            }

            previousAtomic = false;
            level--;
            state = level < 0 ? OPEN : CONTENT;
        }

        public override void SetUnparsedEntity(string name, string systemID, string publicID)
        {
            nextReceiver.SetUnparsedEntity(name, systemID, publicID);
        }

        public override void Characters(UnicodeString s, ILocation locationId, int properties)
        {
            if (level >= 0)
            {
                previousAtomic = false;
                if (s == null)
                {
                    return;
                }

                if (s.IsEmpty())
                {
                    return;
                }

                if (state == START_TAG)
                {
                    StartContent();
                }
            }

            nextReceiver.Characters(s, locationId, properties);
        }

        public override void StartElement(INodeName elemName, ISchemaType typeCode, ILocation location, int properties)
        {

            level++;
            if (state == START_TAG)
            {
                StartContent();
            }

            startElementProperties = properties;
            startElementLocationId = location.SaveLocation();
            pendingAttributes.Clear();
            pendingNSMap = NamespaceMap.EmptyMap();
            pendingStartTag = elemName;
            currentSimpleType = typeCode;
            previousAtomic = false;
            if (currentLevelIsDocument.Length < level + 1)
            {
                Array.Resize(ref currentLevelIsDocument, level * 2);
            }

            currentLevelIsDocument[level] = false;
            state = START_TAG;
        }

        public override void Namespace(string prefix, NamespaceUri namespaceUri, int properties)
        {
            if (prefix == null)
                throw new NullReferenceException();
            if (namespaceUri == null)
                throw new NullReferenceException();
            if (ReceiverOption.Contains(properties, ReceiverOption.NAMESPACE_OK))
            {
                pendingNSMap = pendingNSMap.Put(prefix, namespaceUri);
            }
            else if (level >= 0)
            {
                if (state != START_TAG)
                {
                    throw NoOpenStartTagException.MakeNoOpenStartTagException(Types.Type.NAMESPACE, prefix, (int)hostLanguage, currentLevelIsDocument[level], startElementLocationId);
                }


                // It is an error to output a namespace node for the default namespace if the element
                // itself @is in the null @namespace, as the resulting element could not be serialized
                if ((prefix.Length == 0) && !namespaceUri.IsEmpty())
                {
                    if (pendingStartTag.HasURI(NamespaceUri.NULL))
                    {
                        throw new XPathException("Cannot output a namespace node for the default namespace (" + namespaceUri + ") when the element @is in no namespace").WithErrorCode(hostLanguage == HostLanguage.XSLT ? "XTDE0440" : "XQDY0102");
                    }
                }

                bool rejectDuplicates = ReceiverOption.Contains(properties, ReceiverOption.REJECT_DUPLICATES);
                if (rejectDuplicates)
                {

                    // Handle declarations whose prefix is duplicated for this element.
                    NamespaceUri uri = pendingNSMap.GetNamespaceUri(prefix);
                    if (uri != null && !uri.Equals(namespaceUri))
                    {
                        throw new XPathException("Cannot create two namespace nodes with the same prefix " + "mapped to different URIs (prefix=\"" + prefix + "\", URIs=(" + uri + "\", \"" + namespaceUri + "\")").WithErrorCode(hostLanguage == HostLanguage.XSLT ? "XTDE0430" : "XQDY0102");
                    }
                }

                pendingNSMap = pendingNSMap.Put(prefix, namespaceUri);
            }
            else
            {

                // push top-level namespace nodes down the pipeline
                Orphan orphan = new Orphan(GetConfiguration());
                orphan.SetNodeKind(Types.Type.NAMESPACE);
                orphan.SetNodeName(new NoNamespaceName(prefix));
                orphan.SetStringValue(namespaceUri.ToUnicodeString());
                nextReceiver.Append(orphan, Loc.NONE, properties);
            }

            previousAtomic = false;
        }

        public override void Namespaces(INamespaceBindingSet bindings, int properties)
        {
            if (bindings is NamespaceMap && pendingNSMap.IsEmpty() && ReceiverOption.Contains(properties, ReceiverOption.NAMESPACE_OK))
            {
                pendingNSMap = (NamespaceMap)bindings;
            }
            else
            {
                base.Namespaces(bindings, properties);
            }
        }

        public override void Attribute(INodeName attName, ISimpleType typeCode, string value, ILocation locationId, int properties)
        {

            if (level >= 0 && state != START_TAG)
            {

                // The complexity here @is in identifying the right error message and error code
                XPathException err = NoOpenStartTagException.MakeNoOpenStartTagException(Types.Type.ATTRIBUTE, attName.DisplayName, (int)hostLanguage, currentLevelIsDocument[level], startElementLocationId);
                err.SetLocator(locationId);
                throw err;
            }


            // if this is a duplicate attribute, overwrite the original in XSLT; throw an error in XQuery.
            // No check needed if the NOT_A_DUPLICATE property is set (typically, during a deep copy operation)
            // Allocated as SingletonAttributeMap (an AttributeInfo) so the single-attribute
            // element case reuses this object as its attribute map in StartContent
            AttributeInfo attInfo = new SingletonAttributeMap(attName, typeCode, value, locationId, properties);
            if (level >= 0 && !ReceiverOption.Contains(properties, ReceiverOption.NOT_A_DUPLICATE))
            {
                for (int a = 0; a < pendingAttributes.Count; a++)
                {
                    if (pendingAttributes[a].GetNodeName().Equals(attName))
                    {
                        if (hostLanguage == HostLanguage.XSLT)
                        {
                            pendingAttributes[a] = attInfo;
                            return;
                        }
                        else
                        {
                            throw new XPathException("Cannot create an element having two attributes with the same name: " + Err.Wrap(attName.DisplayName, Err.ATTRIBUTE)).WithErrorCode("XQDY0025");
                        }
                    }
                }
            }


            // for top-level attributes (attributes whose parent element is not being copied),
            // check that the type annotation is not @namespace-sensitive (because the namespace context might
            // be different, and we don't do namespace fixup for prefixes in content: see bug 4151
            if (level == 0 && !typeCode.Equals(BuiltInAtomicType.UNTYPED_ATOMIC) && currentLevelIsDocument[0])
            {

                // commenting-out in line above done MHK 22 Jul 2011 to pass test Constr-cont-nsmode-8
                // reverted 2011-07-27 to pass tests in qischema family
                if (typeCode.IsNamespaceSensitive())
                {
                    throw new XPathException("Cannot copy attributes whose type is @namespace-sensitive (QName or NOTATION): " + Err.Wrap(attName.DisplayName, Err.ATTRIBUTE)).WithErrorCode(hostLanguage == HostLanguage.XSLT ? "XTTE0950" : "XQTY0086");
                }
            }


            // push top-level attribute nodes down the pipeline
            if (level < 0)
            {
                Orphan orphan = new Orphan(GetConfiguration());
                ((GenericTreeInfo)orphan.GetTreeInfo()).SetDurability(Durability.MUTABLE);
                orphan.SetNodeKind(Types.Type.ATTRIBUTE);
                orphan.SetNodeName(attName);
                orphan.SetTypeAnnotation(typeCode);
                orphan.SetStringValue(StringView.Tidy(value));
                nextReceiver.Append(orphan, locationId, properties);
            }


            // otherwise, add this one to the list
            pendingAttributes.Add(attInfo);
            previousAtomic = false;
        }

        /**/
        /**/
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {

            if (state == START_TAG)
            {
                StartContent();
            }

            level++;
            startElementLocationId = location.SaveLocation();
            if (currentLevelIsDocument.Length < level + 1)
            {
                Array.Resize(ref currentLevelIsDocument, level * 2);
            }

            currentLevelIsDocument[level] = false;
            if (elemName.HasURI(NamespaceUri.NULL) && !namespaces.DefaultNamespace.IsEmpty())
            {
                namespaces = namespaces.Remove("");
            }

            bool inherit = !ReceiverOption.Contains(properties, ReceiverOption.DISINHERIT_NAMESPACES);
            NamespaceMap ns2;
            if (inherit)
            {
                NamespaceMap inherited = inheritedNamespaces.Peek();
                if (!inherited.DefaultNamespace.IsEmpty() && elemName.GetNamespaceUri().IsEmpty())
                {
                    inherited = inherited.Remove("");
                }

                ns2 = inherited.PutAll(namespaces);
                if (ReceiverOption.Contains(properties, ReceiverOption.BEQUEATH_INHERITED_NAMESPACES_ONLY))
                {
                    inheritedNamespaces.Push(inherited);
                }
                else
                {
                    inheritedNamespaces.Push(ns2);
                }
            }
            else
            {
                ns2 = namespaces;
                inheritedNamespaces.Push(NamespaceMap.EmptyMap());
            }

            bool refuseInheritedNamespaces = ReceiverOption.Contains(properties, ReceiverOption.REFUSE_NAMESPACES);
            NamespaceMap ns3 = refuseInheritedNamespaces ? namespaces : ns2;
            nextReceiver.StartElement(elemName, type, attributes, ns3, location, properties);
            state = CONTENT;
        }

        /**/
        /**/
        private INodeName CheckProposedPrefix(INodeName nodeName, int seq)
        {
            string nodePrefix = nodeName.GetPrefix();
            NamespaceUri nodeURI = nodeName.GetNamespaceUri();
            if (nodeURI.IsEmpty())
            {
                return nodeName;
            }
            else
            {
                NamespaceUri uri = pendingNSMap.GetNamespaceUri(nodePrefix);
                if (uri == null)
                {
                    if (nodeURI.Equals(NamespaceUri.XML))
                    {
                        // Only the prefix xml names the XML namespace, and it needs no declaration: a name that came
                        // with another prefix is given it, as a name whose prefix is taken is given a substitute.
                        return new FingerprintedQName("xml", nodeURI, nodeName.GetLocalPart());
                    }

                    pendingNSMap = pendingNSMap.Put(nodePrefix, nodeURI);
                    return nodeName;
                }
                else if (nodeURI.Equals(uri))
                {
                    return nodeName; // all is well
                }
                else
                {
                    string newPrefix = GetSubstitutePrefix(nodePrefix, nodeURI, seq);
                    INodeName newName = new FingerprintedQName(newPrefix, nodeURI, nodeName.GetLocalPart());
                    pendingNSMap = pendingNSMap.Put(newPrefix, nodeURI);
                    return newName;
                }
            }
        }

        /**/
        /**/
        private string GetSubstitutePrefix(string prefix, NamespaceUri uri, int seq)
        {
            if (uri.Equals(NamespaceUri.XML))
            {
                return "xml";
            }

            return prefix + '_' + seq;
        }

        /**/
        /**/
        /// <summary>
        /// Output an element end tag.
        /// </summary>
        public override void EndElement()
        {

            if (state == START_TAG)
            {
                StartContent();
            }
            else
            {

                //pendingStartTagDepth = -2;
                pendingStartTag = null;
            }


            // write the end tag
            nextReceiver.EndElement();
            level--;
            previousAtomic = false;
            state = level < 0 ? OPEN : CONTENT;
            inheritedNamespaces.Pop();
        }

        /**/
        /**/
        /// <summary>
        /// Write a comment
        /// </summary>
        public override void Comment(UnicodeString comment, ILocation locationId, int properties)
        {
            if (level >= 0)
            {
                if (state == START_TAG)
                {
                    StartContent();
                }

                previousAtomic = false;
            }

            nextReceiver.Comment(comment, locationId, properties);
        }

        /**/
        /**/
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (level >= 0)
            {
                if (state == START_TAG)
                {
                    StartContent();
                }

                previousAtomic = false;
            }

            nextReceiver.ProcessingInstruction(target, data, locationId, properties);
        }

        /**/
        /**/
        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {

            // Decompose the item into a sequence of node events if we're within a start/end element/document
            // pair. Otherwise, send the item down the pipeline unchanged: it's the job of the IDestination
            // to deal with it (inserting item separators if appropriate)
            if (level >= 0)
            {
                Decompose(item, locationId, copyNamespaces);
            }
            else
            {
                nextReceiver.Append(item, locationId, copyNamespaces);
            }
        }

        /**/
        /**/
        public override IUniStringConsumer GetStringReceiver(bool asTextNode, ILocation loc)
        {
            if (level >= 0)
            {
                // One receiver is live at a time on this outputter (single-threaded pipeline);
                // reuse a cached instance, falling back to a fresh one if a caller nests
                UnicodeStringReceiver r = cachedStringReceiver;
                if (r == null || r.inUse)
                {
                    r = new UnicodeStringReceiver(this);
                    if (cachedStringReceiver == null)
                    {
                        cachedStringReceiver = r;
                    }
                }

                r.Reset(previousAtomic, asTextNode, loc);
                return r;
            }
            else
            {
                return base.GetStringReceiver(asTextNode, loc);
            }
        }

        /**/
        /**/
        /// <summary>
        /// Close the output
        /// </summary>
        public override void Close()
        {

            nextReceiver.Close();
            previousAtomic = false;
            state = FINAL;
        }

        public override void Dispose()
        {
            nextReceiver?.Dispose();
        }

        /**/
        /**/
        public override void StartContent()
        {
            if (state != START_TAG)
            {

                // this can happen if the method is called from outside,
                // e.g. from a SequenceOutputter earlier in the pipeline
                return;
            }

            int props = startElementProperties | ReceiverOption.NAMESPACE_OK;
            NamespaceMap mapAtEntry = pendingNSMap;
            NamespaceMap inherited = inheritedNamespaces.Count == 0 ? NamespaceMap.EmptyMap() : inheritedNamespaces.Peek();

            // attributes in no namespace cannot affect the namespace pipeline; only then is the
            // (name, map, inherited, props) memo a complete key for its outcome
            bool nsCleanAttributes = true;
            for (int a = 0; a < pendingAttributes.Count; a++)
            {
                if (!pendingAttributes[a].GetNodeName().HasURI(NamespaceUri.NULL))
                {
                    nsCleanAttributes = false;
                    break;
                }
            }

            int slot = (RuntimeHelpers.GetHashCode(mapAtEntry)
                + 31 * RuntimeHelpers.GetHashCode(inherited)
                + 127 * RuntimeHelpers.GetHashCode(pendingStartTag)) & NS_MEMO_MASK;
            INodeName elcode;
            if (nsCleanAttributes && nsMemoNameIn != null
                && ReferenceEquals(nsMemoNameIn[slot], pendingStartTag)
                && ReferenceEquals(nsMemoMapIn[slot], mapAtEntry)
                && ReferenceEquals(nsMemoInherited[slot], inherited)
                && nsMemoProps[slot] == startElementProperties)
            {
                elcode = nsMemoNameOut[slot];
                pendingNSMap = nsMemoMapOut[slot];
            }
            else
            {
                elcode = CheckProposedPrefix(pendingStartTag, 0);
                if (!nsCleanAttributes)
                {
                    for (int a = 0; a < pendingAttributes.Count; a++)
                    {
                        INodeName oldName = pendingAttributes[a].GetNodeName();
                        if (!oldName.HasURI(NamespaceUri.NULL))
                        {

                            // non-null prefix
                            INodeName newName = CheckProposedPrefix(oldName, a + 1);
                            if (newName != oldName)
                            {
                                AttributeInfo newInfo = pendingAttributes[a].WithNodeName(newName);
                                pendingAttributes[a] = newInfo;
                            }
                        }
                    }
                }

                if (!ReceiverOption.Contains(startElementProperties, ReceiverOption.REFUSE_NAMESPACES))
                {
                    pendingNSMap = inherited.PutAll(pendingNSMap);
                }

                if (pendingStartTag.HasURI(NamespaceUri.NULL) && !pendingNSMap.DefaultNamespace.IsEmpty())
                {
                    pendingNSMap = pendingNSMap.Remove("");
                }

                if (nsCleanAttributes && (nsMemoNameIn != null || ++nsMemoFlushes >= NS_MEMO_WARMUP))
                {
                    if (nsMemoNameIn == null)
                    {
                        nsMemoNameIn = new INodeName[NS_MEMO_MASK + 1];
                        nsMemoNameOut = new INodeName[NS_MEMO_MASK + 1];
                        nsMemoMapIn = new NamespaceMap[NS_MEMO_MASK + 1];
                        nsMemoInherited = new NamespaceMap[NS_MEMO_MASK + 1];
                        nsMemoMapOut = new NamespaceMap[NS_MEMO_MASK + 1];
                        nsMemoProps = new int[NS_MEMO_MASK + 1];
                    }

                    nsMemoNameIn[slot] = pendingStartTag;
                    nsMemoNameOut[slot] = elcode;
                    nsMemoMapIn[slot] = mapAtEntry;
                    nsMemoInherited[slot] = inherited;
                    nsMemoMapOut[slot] = pendingNSMap;
                    nsMemoProps[slot] = startElementProperties;
                }
            }

            IAttributeMap attributes = SequenceTool.AttributeMapFromList(pendingAttributes);
            nextReceiver.StartElement(elcode, currentSimpleType, attributes, pendingNSMap, startElementLocationId, props);
            FinishStartContent(inherited);
        }

        /**/
        /**/
        private void FinishStartContent(NamespaceMap inherited)
        {
            bool inherit = !ReceiverOption.Contains(startElementProperties, ReceiverOption.DISINHERIT_NAMESPACES);
            inheritedNamespaces.Push(inherit ? pendingNSMap : inherited);
            pendingAttributes.Clear();
            pendingNSMap = NamespaceMap.EmptyMap();
            previousAtomic = false;
            state = CONTENT;
        }

        /**/
        /**/
        public override bool UsesTypeAnnotations()
        {
            return nextReceiver.UsesTypeAnnotations();
        }

        /**/
        /**/
        private void Flatten(ArrayItem array, ILocation locationId, int copyNamespaces)
        {
            foreach (ISequence member in array.Members())
            {
                SequenceTool.Supply(member.Iterate(), (it) => Append(it, locationId, copyNamespaces));
            }
        }

        /**/
        /**/
        private void Decompose(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (item != null)
            {
                Genre genre = item.GetGenre();
                switch (genre)
                {
                    case Genre.ATOMIC:
                    case Genre.EXTERNAL:
                        if (previousAtomic)
                        {
                            Characters(StringConstants.SINGLE_SPACE, locationId, ReceiverOption.NONE);
                        }

                        Characters(item.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        previousAtomic = true;
                        break;
                    case Genre.ARRAY:
                        Flatten((ArrayItem)item, locationId, copyNamespaces);
                        break;
                    case Genre.FUNCTION:
                    case Genre.MAP:
                        string thing = item is MapItem ? "map" : "function item";
                        string errorCode = ErrorCodeForDecomposingFunctionItems;
                        if (errorCode.StartsWith("SENR", StringComparison.Ordinal))
                        {
                            throw new XPathException("Cannot serialize a " + thing + " using this output method", errorCode, locationId);
                        }
                        else
                        {
                            string msg = "Cannot add a " + thing + " (" + Err.Depict(item) + ") to an XDM node tree";
                            if (pendingStartTag != null)
                            {
                                msg += " (currently writing element " + pendingStartTag.DisplayName + ")";
                            }

                            throw new XPathException(msg, errorCode, locationId);
                        }

                    case Genre.NODE:
                    default:
                        DecomposeNodeOrDefault(item, locationId, copyNamespaces);
                        break;
                }
            }
        }

        /**/
        /**/
        private void DecomposeNodeOrDefault(IItem item, ILocation locationId, int copyNamespaces)
        {
            NodeInfo node = (NodeInfo)item;
            switch (node.GetNodeKind())
            {
                case Types.Type.TEXT:
                    int options = ReceiverOption.NONE;
                    if (node is Orphan && ((Orphan)node).IsDisableOutputEscaping())
                    {
                        options = ReceiverOption.DISABLE_ESCAPING;
                    }

                    Characters(item.UnicodeStringValue, locationId, options);
                    break;
                case Types.Type.ATTRIBUTE:
                    if (((ISimpleType)node.GetSchemaType()).IsNamespaceSensitive())
                    {
                        throw new XPathException("Cannot copy attributes whose type is @namespace-sensitive (QName or NOTATION): " + Err.Wrap(node.DisplayName, Err.ATTRIBUTE)).WithErrorCode(GetPipelineConfiguration().IsXSLT() ? "XTTE0950" : "XQTY0086");
                    }

                    Attribute(NameOfNode.MakeName(node), (ISimpleType)node.GetSchemaType(), node.GetStringValue(), locationId, ReceiverOption.NONE);
                    break;
                case Types.Type.NAMESPACE:
                    Namespace(node.GetLocalPart(), NamespaceUri.Of(node.GetStringValue()), ReceiverOption.NONE);
                    break;
                case Types.Type.DOCUMENT:
                    StartDocument(ReceiverOption.NONE); // needed to ensure that illegal namespaces or attributes in the content are caught
                    foreach (NodeInfo child in node.Children())
                    {
                        Append(child, locationId, copyNamespaces);
                    }

                    EndDocument();
                    break;
                default:
                    int copyOptions = CopyOptions.TYPE_ANNOTATIONS;
                    if (ReceiverOption.Contains(copyNamespaces, ReceiverOption.ALL_NAMESPACES))
                    {
                        copyOptions |= CopyOptions.ALL_NAMESPACES;
                    }

                    ((NodeInfo)item).Copy(this, copyOptions, locationId);
                    break;
            }

            previousAtomic = false;
        }

        /**/
        /**/
        private sealed class UnicodeStringReceiver : IUniStringConsumer
        {
            private readonly ComplexContentOutputter cco;
            private bool previousAtomic;
            private bool asTextNode;
            private ILocation location;
            internal bool inUse;
            public UnicodeStringReceiver(ComplexContentOutputter cco)
            {
                this.cco = cco;
            }

            internal void Reset(bool previousAtomic, bool asTextNode, ILocation loc)
            {
                this.previousAtomic = previousAtomic;
                this.asTextNode = asTextNode;
                this.location = loc;
                this.inUse = true;
            }

            public void Open()
            {
                if (previousAtomic && !asTextNode)
                {
                    cco.Characters(StringConstants.SINGLE_SPACE, location, ReceiverOption.NONE);
                }
            }

            public IUniStringConsumer Accept(UnicodeString chars)
            {
                cco.Characters(chars, location, ReceiverOption.NONE);
                return this;
            }

            // Abort-path release: the pooled slot is reclaimed by Close on the success path only;
            // an aborted run just abandons it (safe -- the pool re-arms on next acquire).
            public void Dispose()
            {
            }

            public void Close()
            {
                // Idempotent: a second Close from a misbehaving caller must not release the
                // cached instance while a subsequent caller is using it
                if (!inUse)
                {
                    return;
                }

                cco.previousAtomic = !asTextNode;
                inUse = false;
                location = null;
            }
        }
    }
}
