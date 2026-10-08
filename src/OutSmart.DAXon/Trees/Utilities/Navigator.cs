////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Trees.Utilities
{
    internal sealed class Navigator
    {

        private static readonly int[] nodeCategories = new[]
        {
            -1,
            3,
            2,
            3,
            -1,
            -1,
            -1,
            3,
            3,
            0,
            -1,
            -1,
            -1,
            1
        };

        public static StructuredQName GetNodeName(NodeInfo node)
        {
            if (node.GetLocalPart() != null)
            {
                return new StructuredQName(node.GetPrefix(), node.GetNamespaceUri(), node.GetLocalPart());
            }
            else
            {
                return null;
            }
        }

        public static NodeInfo GetOutermostElement(ITreeInfo doc)
        {
            return doc.GetRootNode().IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT).Next();
        }

        public static string GetBaseURI(NodeInfo node)
        {
            return GetBaseURI(node, IsTopByDefault);
        }

        internal static bool IsTopByDefault(NodeInfo n)
        {
            NodeInfo parent = n.GetParent();
            return parent == null || !parent.GetSystemId().Equals(n.GetSystemId());
        }

        // A level whose base URI is its parent's, re-resolved against a relative xml:base when it has one.
        private struct BaseUriStep
        {
            public NodeInfo Node;
            public string XmlBase;
            public URI Relative;
        }

        public static string GetBaseURI(NodeInfo node, Func<NodeInfo, bool> isTopElementWithinEntity)
        {
            // Climbs while the answer is the parent's base URI, then resolves back down: recursing once per
            // level overflowed the stack on deep input (base-uri(), document(@href)) and in deep stylesheets.
            List<BaseUriStep> chain = null;
            NodeInfo n = node;
            IInheritedBaseUri peer = null;   // null for the start node, which uses the caller's predicate
            string result;
            while (true)
            {
                NodeInfo parent;
                string xmlBase = n is TinyElementImpl tiny ? tiny.GetAttributeValue(StandardNames.XML_BASE) : n.GetAttributeValue(NamespaceUri.XML, "base");
                URI relative = null;
                if (xmlBase != null)
                {
                    try
                    {
                        relative = new URI(xmlBase);
                        if (relative.IsAbsolute())
                        {
                            result = xmlBase;
                            break;
                        }

                        parent = n.GetParent();
                        if (parent == null)
                        {
                            // We have a parentless element with a relative xml:base attribute.
                            // See for example test XQTS fn-@base-uri-10 and @base-uri-27
                            URI base2 = new URI(n.GetSystemId());
                            result = ((xmlBase.Length == 0) ? base2 : base2.Resolve(relative)).ToString();
                            break;
                        }

                        string startSysId = n.GetSystemId();
                        if (startSysId == null)
                        {
                            result = null;
                            break;
                        }

                        if (peer == null ? isTopElementWithinEntity(n) : peer.IsTopWithinEntity())
                        {
                            URI @base = new URI(startSysId);
                            result = ((xmlBase.Length == 0) ? @base : @base.Resolve(relative)).ToString();
                            break;
                        }
                    }
                    catch (Exception e) when (e is URISyntaxException || e is ArgumentException)
                    {
                        // xml:base is an invalid URI. Just return it as is: the operation that needs the base URI
                        // will probably fail as a result. (ArgumentException: System.Uri reads a resolved base
                        // over 65519 chars as relative, and resolving against it throws.)
                        result = xmlBase;
                        break;
                    }
                }
                else
                {
                    string startSystemId = n.GetSystemId();
                    if (startSystemId == null)
                    {
                        result = null;
                        break;
                    }

                    parent = n.GetParent();
                    if (parent == null)
                    {
                        result = startSystemId;
                        break;
                    }

                    string parentSystemId = parent.GetSystemId();
                    if (!startSystemId.Equals(parentSystemId) && parentSystemId.Length != 0)
                    {
                        result = startSystemId;
                        break;
                    }
                }

                (chain ??= new List<BaseUriStep>()).Add(new BaseUriStep { Node = n, XmlBase = xmlBase, Relative = relative });
                peer = parent as IInheritedBaseUri;
                string known = peer?.KnownBaseUri;
                if (peer == null || known != null)
                {
                    result = peer == null ? parent.GetBaseURI() : known;
                    break;
                }

                n = parent;
            }

            if (chain != null)
            {
                for (int i = chain.Count - 1; i >= 0; i--)
                {
                    BaseUriStep step = chain[i];
                    if (step.XmlBase != null)
                    {
                        try
                        {
                            URI @base = new URI(result);
                            result = ((step.XmlBase.Length == 0) ? @base : @base.Resolve(step.Relative)).ToString();
                        }
                        catch (Exception e) when (e is URISyntaxException || e is ArgumentException)
                        {
                            result = step.XmlBase;
                        }
                    }

                    if (i > 0)
                    {
                        ((IInheritedBaseUri)step.Node).RememberBaseUri(result);
                    }
                }
            }

            return result;
        }

        public static string GetPath(NodeInfo node)
        {
            return GetPath(node, null);
        }

        public static string GetPath(NodeInfo node, IXPathContext context)
        {
            if (node == null)
            {
                return "";
            }

            string pre;
            bool streamed = node.GetConfiguration().IsStreamedNode(node);
            NodeInfo parent = node.GetParent();

            switch (node.GetNodeKind())
            {
                case Types.Type.DOCUMENT:
                    return "/";
                case Types.Type.ELEMENT:
                    return parent == null ? node.DisplayName : ElementPath(node, context);
                case Types.Type.ATTRIBUTE:
                    return GetPath(parent, context) + "/@" + node.DisplayName;
                case Types.Type.TEXT:
                    pre = GetPath(parent, context);
                    return (pre.Equals("/") ? "" : pre) + "/text()" + (streamed ? "" : "[" + GetNumberSimple(node, context) + "]");
                case Types.Type.COMMENT:
                    pre = GetPath(parent, context);
                    return (pre.Equals("/") ? "" : pre) + "/comment()" + (streamed ? "" : "[" + GetNumberSimple(node, context) + "]");
                case Types.Type.PROCESSING_INSTRUCTION:
                    pre = GetPath(parent, context);
                    return (pre.Equals("/") ? "" : pre) + "/processing-instruction()" + (streamed ? "" : "[" + GetNumberSimple(node, context) + "]");
                case Types.Type.NAMESPACE:
                    string test = node.GetLocalPart();
                    if ((test.Length == 0))
                    {

                        // default namespace: need a node-test that selects unnamed nodes only
                        test = "*[not(local-name()]";
                    }

                    return GetPath(parent, context) + "/namespace::" + test;
                default:
                    return "";
            }
        }

        /// <summary>
        /// The path of an element that has a parent. The ancestor chain is the only unbounded part
        /// of a path - every other node kind steps to its parent once and lands here - so it is
        /// walked iteratively: GetPath BUILDS error messages (a runtime error reports the path of
        /// its context node), and a document deep enough to overflow would replace the real
        /// diagnosis with a stack error.
        /// </summary>
        private static string ElementPath(NodeInfo node, IXPathContext context)
        {
            List<NodeInfo> chain = new List<NodeInfo>();
            for (NodeInfo n = node; n != null && n.GetNodeKind() == Types.Type.ELEMENT; n = n.GetParent())
            {
                chain.Add(n);
            }

            // the outermost element carries no position: rooted at its document if it has one
            NodeInfo top = chain[chain.Count - 1];
            StringBuilder sb = new StringBuilder();
            if (top.GetParent() != null)
            {
                sb.Append('/');
            }

            sb.Append(top.DisplayName);
            for (int i = chain.Count - 2; i >= 0; i--)
            {
                NodeInfo e = chain[i];
                sb.Append('/').Append(e.DisplayName);
                if (!e.GetConfiguration().IsStreamedNode(e))
                {
                    sb.Append('[').Append(GetNumberSimple(e, context)).Append(']');
                }
            }

            return sb.ToString();
        }

        public static bool HaveSameName(NodeInfo n1, NodeInfo n2)
        {
            if (n1.HasFingerprint() && n2.HasFingerprint())
            {
                return n1.Fingerprint == n2.Fingerprint;
            }
            else
            {
                return n1.GetLocalPart().Equals(n2.GetLocalPart()) && n1.GetNamespaceUri().Equals(n2.GetNamespaceUri());
            }
        }

        public static int GetNumberSimple(NodeInfo node, IXPathContext context)
        {
            NodeTest same;
            if ((node.GetLocalPart().Length == 0))
            {
                same = NodeKindTest.MakeNodeKindTest(node.GetNodeKind());
            }
            else
            {
                same = new SameNameTest(node);
            }

            Controller controller = context == null ? null : context.GetController();
            IAxisIterator preceding = node.IterateAxis(AxisInfo.PRECEDING_SIBLING, same);
            int i = 1;
            while (true)
            {
                NodeInfo prev = preceding.Next();
                if (prev == null)
                {
                    break;
                }

                if (controller != null)
                {
                    int memo = controller.GetRememberedNumber(prev);
                    if (memo > 0)
                    {
                        memo += i;
                        controller.SetRememberedNumber(node, memo);
                        return memo;
                    }
                }

                i++;
            }

            if (controller != null)
            {
                controller.SetRememberedNumber(node, i);
            }

            return i;
        }

        public static int GetNumberSingle(NodeInfo node, Patterns.Pattern count, Patterns.Pattern from, IXPathContext context)
        {
            if (count == null && from == null)
            {
                return GetNumberSimple(node, context);
            }

            bool knownToMatch = false;
            if (count == null)
            {
                if ((node.GetLocalPart().Length == 0))
                {

                    // unnamed node
                    count = new NodeTestPattern(NodeKindTest.MakeNodeKindTest(node.GetNodeKind()));
                }
                else
                {
                    count = new NodeTestPattern(new SameNameTest(node));
                }

                knownToMatch = true;
            }

            NodeInfo target = node;

            // code changed in 9.5 to fix issue described in spec bug 9840
            if (!knownToMatch)
            {
                while (true)
                {
                    if (count.MatchesItem(target, context))
                    {
                        if (from == null)
                        {
                            break;
                        }
                        else
                        {

                            // see whether there is an ancestor node that matches the from pattern
                            NodeInfo anc = target;
                            while (!from.MatchesItem(anc, context))
                            {
                                anc = anc.GetParent();
                                if (anc == null)
                                {

                                    // there's no ancestor that matches the "from" pattern
                                    return 0;
                                }
                            }


                            // we've found the node to be counted
                            break;
                        }
                    }
                    else if (from != null && from.MatchesItem(target, context))
                    {

                        // if we find something that matches "from" before we find something that matches "count", exit
                        return 0;
                    }
                    else
                    {
                        target = target.GetParent();
                        if (target == null)
                        {

                            // found the root before finding a match on either "count" or "from"
                            return 0;
                        }
                    }
                }
            }


            // we've found the ancestor to count from
            ISequenceIterator preceding = target.IterateAxis(AxisInfo.PRECEDING_SIBLING, GetNodeTestForPattern(count));

            // pass the filter condition down to the axis enumeration where possible
            bool alreadyChecked = count is NodeTestPattern;
            int i = 1;
            while (true)
            {
                NodeInfo p = (NodeInfo)preceding.Next();
                if (p == null)
                {
                    return i;
                }

                if (alreadyChecked || count.MatchesItem(p, context))
                {
                    i++;
                }
            }
        }

        public static int GetNumberAny(Expression inst, NodeInfo node, Patterns.Pattern count, Patterns.Pattern from, IXPathContext context, bool hasVariablesInPatterns)
        {
            NodeInfo memoNode = null;
            int memoNumber = 0;
            Controller controller = context.GetController();
            bool memoise = !hasVariablesInPatterns && from == null;
            if (memoise)
            {
                object[] memo = (Object[])controller.GetUserData(inst.GetLocation(), "xsl:number");
                if (memo != null)
                {
                    memoNode = (NodeInfo)memo[0];
                    memoNumber = (int)memo[1];
                }
            }

            int num = 0;
            if (count == null)
            {
                if ((node.GetLocalPart().Length == 0))
                {

                    // unnamed node
                    count = new NodeTestPattern(NodeKindTest.MakeNodeKindTest(node.GetNodeKind()));
                }
                else
                {
                    count = new NodeTestPattern(new SameNameTest(node));
                }

                num = 1;
            }
            else if (count.MatchesItem(node, context))
            {
                num = 1;
            }


            // We use a special axis invented for the purpose: the union of the preceding and
            // ancestor axes, but in reverse document order
            // Pass part of the filtering down to the axis iterator if possible
            NodeTest filter;
            if (from == null)
            {
                filter = GetNodeTestForPattern(count);
            }
            else if (from.GetUType() == UType.ELEMENT && count.GetUType() == UType.ELEMENT)
            {
                filter = NodeKindTest.ELEMENT;
            }
            else
            {
                filter = AnyNodeTest.GetInstance();
            }

            if (from != null && from.MatchesItem(node, context))
            {
                return num;
            }

            ISequenceIterator preceding = node.IterateAxis(AxisInfo.PRECEDING_OR_ANCESTOR, filter);
            while (true)
            {
                NodeInfo prev = (NodeInfo)preceding.Next();
                if (prev == null)
                {
                    break;
                }

                if (count.MatchesItem(prev, context))
                {
                    if (num == 1 && prev.Equals(memoNode))
                    {
                        num = memoNumber + 1;
                        break;
                    }

                    num++;
                }

                if (from != null && from.MatchesItem(prev, context))
                {
                    break;
                }
            }

            if (memoise)
            {
                object[] memo = new object[2];
                memo[0] = node;
                memo[1] = num;
                controller.SetUserData(inst.GetLocation(), "xsl:number", memo);
            }

            return num;
        }

        public static IList<long> GetNumberMulti(NodeInfo node, Patterns.Pattern count, Patterns.Pattern from, IXPathContext context)
        {
            List<long> v = new List<long>(5);
            if (count == null)
            {
                if ((node.GetLocalPart().Length == 0))
                {

                    // unnamed node
                    count = new NodeTestPattern(NodeKindTest.MakeNodeKindTest(node.GetNodeKind()));
                }
                else
                {
                    count = new NodeTestPattern(new SameNameTest(node));
                }
            }

            NodeInfo curr = node;
            while (true)
            {
                if (count.MatchesItem(curr, context))
                {
                    int num = GetNumberSingle(curr, count, null, context);
                    v.Insert(0,(long)num);
                }

                if (from != null && from.MatchesItem(curr, context))
                {
                    break;
                }

                curr = curr.GetParent();
                if (curr == null)
                {
                    break;
                }
            }

            return v;
        }

        /// <summary>
        /// Get a NodeTest to use as a filter for nodes, given a pattern.
        /// </summary>
        private static NodeTest GetNodeTestForPattern(Patterns.Pattern pattern)
        {
            Types.ItemType type = pattern.GetItemType();
            if (type is NodeTest)
            {
                return (NodeTest)type;
            }
            else if (pattern.GetUType().Overlaps(UType.ANY_NODE))
            {
                return AnyNodeTest.GetInstance();
            }
            else
            {
                return ErrorType.GetInstance();
            }
        }

        public static void Copy(NodeInfo node, IReceiver @out, int copyOptions, ILocation locationId)
        {
            switch (node.GetNodeKind())
            {
                case Types.Type.DOCUMENT:
                case Types.Type.ELEMENT:
                    {
                        CopyTree(node, @out, copyOptions, locationId);
                        return;
                    }

                case Types.Type.ATTRIBUTE:
                    {
                        throw new ArgumentException("Cannot copy attribute to IReceiver");
                    }

                case Types.Type.TEXT:
                    {
                        UnicodeString value = node.UnicodeStringValue;
                        if (value.Length() != 0)
                        {

                            // zero-length text nodes can arise from external model wrappers
                            @out.Characters(value, locationId, ReceiverOption.NONE);
                        }

                        return;
                    }

                case Types.Type.COMMENT:
                    {
                        @out.Comment(node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        return;
                    }

                case Types.Type.PROCESSING_INSTRUCTION:
                    {
                        @out.ProcessingInstruction(node.GetLocalPart(), node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        return;
                    }

                case Types.Type.NAMESPACE:
                    {
                        throw new ArgumentException("Cannot copy namespace to IReceiver");
                    }

                default:
                    break;
            }
        }

        public static void Copy(NodeInfo node, Outputter @out, int copyOptions, ILocation locationId)
        {
            bool keepTypes = (copyOptions & CopyOptions.TYPE_ANNOTATIONS) != 0;
            switch (node.GetNodeKind())
            {
                case Types.Type.DOCUMENT:
                case Types.Type.ELEMENT:
                    {
                        CopyTree(node, @out, copyOptions, locationId);
                        return;
                    }

                case Types.Type.ATTRIBUTE:
                    {
                        ISimpleType attType = keepTypes ? (ISimpleType)node.GetSchemaType() : BuiltInAtomicType.UNTYPED_ATOMIC;
                        @out.Attribute(NameOfNode.MakeName(node), attType, node.GetStringValue(), locationId, ReceiverOption.NONE);
                        return;
                    }

                case Types.Type.TEXT:
                    {
                        UnicodeString value = node.UnicodeStringValue;
                        if (value.Length() != 0)
                        {

                            // zero-length text nodes can arise from external model wrappers
                            @out.Characters(value, locationId, ReceiverOption.NONE);
                        }

                        return;
                    }

                case Types.Type.COMMENT:
                    {
                        @out.Comment(node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        return;
                    }

                case Types.Type.PROCESSING_INSTRUCTION:
                    {
                        @out.ProcessingInstruction(node.GetLocalPart(), node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        return;
                    }

                case Types.Type.NAMESPACE:
                    {
                        @out.Namespace(node.GetLocalPart(), NamespaceUri.Of(node.GetStringValue()), ReceiverOption.NONE);
                        return;
                    }

                default:
                    break;
            }
        }

        // A walk, not a recursion per level whose depth is the input document's: copying a snapshot
        // 1000 levels deep refused on a 256KB thread. Only snapshot nodes copy through here; any other
        // child copies itself (the tiny and linked trees without recursing).
        private static void CopyTree(NodeInfo root, IReceiver @out, int copyOptions, ILocation locationId)
        {
            Controller run = @out.GetPipelineConfiguration().GetController();   // its own deadline, not the thread slot
            StartCopy(root, @out, copyOptions, locationId);
            var open = new Stack<IEnumerator<NodeInfo>>();
            open.Push(root.Children().GetEnumerator());
            while (open.Count > 0)
            {
                run?.CheckTimeout();   // a copy is as long as its input: per node, as the parser
                IEnumerator<NodeInfo> children = open.Peek();
                if (!children.MoveNext())
                {
                    open.Pop().Dispose();
                    EndCopy(root, open.Count, @out);
                }
                else if (children.Current is SnapshotNode child && child.GetNodeKind() == Types.Type.ELEMENT)
                {
                    StartCopy(child, @out, copyOptions, locationId);
                    open.Push(child.Children().GetEnumerator());
                }
                else
                {
                    children.Current.Copy(@out, copyOptions, locationId);
                }
            }
        }

        private static void StartCopy(NodeInfo node, IReceiver @out, int copyOptions, ILocation locationId)
        {
            if (node.GetNodeKind() == Types.Type.DOCUMENT)
            {
                @out.StartDocument(CopyOptions.GetStartDocumentProperties(copyOptions));
                return;
            }

            ISchemaType annotation = (copyOptions & CopyOptions.TYPE_ANNOTATIONS) != 0 ? node.GetSchemaType() : Untyped.INSTANCE;
            INodeName elementName = NameOfNode.MakeName(node);
            NamespaceMap nsMap;
            if (CopyOptions.Includes(copyOptions, CopyOptions.ALL_NAMESPACES))
            {
                nsMap = node.AllNamespaces;
            }
            else
            {

                // Bug #5861 - we need to ensure the namespaces used in element and attribute names are declared
                if ((elementName.GetPrefix().Length == 0) && elementName.HasURI(NamespaceUri.NULL))
                {
                    nsMap = NamespaceMap.EmptyMap(); // Bug 6866
                }
                else
                {
                    nsMap = NamespaceMap.Of(elementName.GetPrefix(), elementName.GetNamespaceUri());
                }

                foreach (AttributeInfo att in node.Attributes())
                {
                    INodeName attName = att.GetNodeName();
                    if (!(attName.GetPrefix().Length == 0))
                    {
                        nsMap = nsMap.Put(attName.GetPrefix(), attName.GetNamespaceUri());
                    }
                }
            }

            @out.StartElement(elementName, annotation, node.Attributes(), nsMap, locationId, ReceiverOption.BEQUEATH_INHERITED_NAMESPACES_ONLY | ReceiverOption.NAMESPACE_OK);
        }

        // As the IReceiver walk; this overload copies every element itself, so it walks into all of them.
        private static void CopyTree(NodeInfo root, Outputter @out, int copyOptions, ILocation locationId)
        {
            Controller run = @out.GetPipelineConfiguration().GetController();
            StartCopy(root, @out, copyOptions, locationId);
            var open = new Stack<IEnumerator<NodeInfo>>();
            open.Push(root.Children().GetEnumerator());
            while (open.Count > 0)
            {
                run?.CheckTimeout();
                IEnumerator<NodeInfo> children = open.Peek();
                if (!children.MoveNext())
                {
                    open.Pop().Dispose();
                    EndCopy(root, open.Count, @out);
                }
                else if (children.Current.GetNodeKind() == Types.Type.ELEMENT)
                {
                    StartCopy(children.Current, @out, copyOptions, locationId);
                    open.Push(children.Current.Children().GetEnumerator());
                }
                else
                {
                    Copy(children.Current, @out, copyOptions, locationId);
                }
            }
        }

        private static void StartCopy(NodeInfo node, Outputter @out, int copyOptions, ILocation locationId)
        {
            if (node.GetNodeKind() == Types.Type.DOCUMENT)
            {
                @out.StartDocument(CopyOptions.GetStartDocumentProperties(copyOptions));
                return;
            }

            bool keepTypes = (copyOptions & CopyOptions.TYPE_ANNOTATIONS) != 0;
            ISchemaType annotation = keepTypes ? node.GetSchemaType() : Untyped.INSTANCE;
            @out.StartElement(NameOfNode.MakeName(node), annotation, locationId, ReceiverOption.DISINHERIT_NAMESPACES | ReceiverOption.NAMESPACE_OK);
            if ((copyOptions & CopyOptions.ALL_NAMESPACES) != 0)
            {
                foreach (NamespaceBinding ns in node.AllNamespaces)
                {
                    @out.Namespace(ns.GetPrefix(), ns.GetNamespaceUri(), ReceiverOption.NONE);
                }
            }

            foreach (AttributeInfo attr in node.Attributes())
            {
                ISimpleType attType = keepTypes ? attr.GetType() : BuiltInAtomicType.UNTYPED_ATOMIC;
                @out.Attribute(attr.GetNodeName(), attType, attr.Value, attr.GetLocation(), attr.GetProperties());
            }
        }

        // The end event of the node whose children ran out; only the outermost can be a document.
        private static void EndCopy(NodeInfo root, int stillOpen, IReceiver @out)
        {
            if (stillOpen == 0 && root.GetNodeKind() == Types.Type.DOCUMENT)
            {
                @out.EndDocument();
            }
            else
            {
                @out.EndElement();
            }
        }

        public static int CompareOrder(ISiblingCountingNode first, ISiblingCountingNode second)
        {

            // are they the same node?
            if (first.Equals(second))
            {
                return 0;
            }

            NodeInfo firstParent = first.GetParent();
            if (firstParent == null)
            {

                // first node is the root
                return -1;
            }

            NodeInfo secondParent = second.GetParent();
            if (secondParent == null)
            {

                // second node is the root
                return +1;
            }


            // do they have the same parent (common case)?
            if (firstParent.Equals(secondParent))
            {
                int cat1 = nodeCategories[first.GetNodeKind()];
                int cat2 = nodeCategories[second.GetNodeKind()];
                if (cat1 == cat2)
                {
                    return first.GetSiblingPosition() - second.GetSiblingPosition();
                }
                else
                {
                    return cat1 - cat2;
                }
            }


            // find the depths of both nodes in the tree
            int depth1 = 0;
            int depth2 = 0;
            NodeInfo p1 = first;
            NodeInfo p2 = second;
            while (p1 != null)
            {
                depth1++;
                p1 = p1.GetParent();
            }

            while (p2 != null)
            {
                depth2++;
                p2 = p2.GetParent();
            }


            // move up one branch of the tree so we have two nodes on the same level
            p1 = first;
            while (depth1 > depth2)
            {
                p1 = p1.GetParent();
                if (p1.Equals(second))
                {
                    return +1;
                }

                depth1--;
            }

            p2 = second;
            while (depth2 > depth1)
            {
                p2 = p2.GetParent();
                if (p2.Equals(first))
                {
                    return -1;
                }

                depth2--;
            }


            // now move up both branches in sync until we find a common parent
            while (true)
            {
                NodeInfo par1 = p1.GetParent();
                NodeInfo par2 = p2.GetParent();
                if (par1 == null || par2 == null)
                {
                    throw new NullReferenceException("Node order comparison - internal error");
                }

                if (par1.Equals(par2))
                {
                    if (p1.GetNodeKind() == Types.Type.ATTRIBUTE && p2.GetNodeKind() != Types.Type.ATTRIBUTE)
                    {
                        return -1; // attributes first
                    }

                    if (p1.GetNodeKind() != Types.Type.ATTRIBUTE && p2.GetNodeKind() == Types.Type.ATTRIBUTE)
                    {
                        return +1; // attributes first
                    }

                    return ((ISiblingCountingNode)p1).GetSiblingPosition() - ((ISiblingCountingNode)p2).GetSiblingPosition();
                }

                p1 = par1;
                p2 = par2;
            }
        }

        public static int ComparePosition(NodeInfo first, NodeInfo second)
        {
            if (first.GetNodeKind() == Types.Type.ATTRIBUTE || first.GetNodeKind() == Types.Type.NAMESPACE || second.GetNodeKind() == Types.Type.ATTRIBUTE || second.GetNodeKind() == Types.Type.NAMESPACE)
            {
                throw new NotSupportedException();
            }


            // are they the same node?
            if (first.Equals(second))
            {
                return AxisInfo.SELF;
            }

            NodeInfo firstParent = first.GetParent();
            if (firstParent == null)
            {

                // first node is the root
                return AxisInfo.ANCESTOR;
            }

            NodeInfo secondParent = second.GetParent();
            if (secondParent == null)
            {

                // second node is the root
                return AxisInfo.DESCENDANT;
            }


            // do they have the same parent (common case)?
            if (firstParent.Equals(secondParent))
            {
                if (first.CompareOrder(second) < 0)
                {
                    return AxisInfo.PRECEDING;
                }
                else
                {
                    return AxisInfo.FOLLOWING;
                }
            }


            // find the depths of both nodes in the tree
            int depth1 = 0;
            int depth2 = 0;
            NodeInfo p1 = first;
            NodeInfo p2 = second;
            while (p1 != null)
            {
                depth1++;
                p1 = p1.GetParent();
            }

            while (p2 != null)
            {
                depth2++;
                p2 = p2.GetParent();
            }


            // Test if either node is an ancestor of the other
            p1 = first;
            while (depth1 > depth2)
            {
                p1 = p1.GetParent();
                if (p1.Equals(second))
                {
                    return AxisInfo.DESCENDANT;
                }

                depth1--;
            }

            p2 = second;
            while (depth2 > depth1)
            {
                p2 = p2.GetParent();
                if (p2.Equals(first))
                {
                    return AxisInfo.ANCESTOR;
                }

                depth2--;
            }


            // now delegate to compareOrder()
            if (first.CompareOrder(second) < 0)
            {
                return AxisInfo.PRECEDING;
            }
            else
            {
                return AxisInfo.FOLLOWING;
            }
        }

        public static bool IsAncestorOrSelf(NodeInfo a, NodeInfo d)
        {
            int k = a.GetNodeKind();
            if (k != Types.Type.ELEMENT && k != Types.Type.DOCUMENT)
            {
                return a.Equals(d);
            }


            // Fast path for the TinyTree implementation
            if (a is TinyNodeImpl)
            {
                if (d is TinyNodeImpl)
                {
                    return ((TinyNodeImpl)a).IsAncestorOrSelf((TinyNodeImpl)d);
                }
                else if (d is OutSmart.DAXon.Trees.Tiny.TinyTextualElement.TinyTextualElementText)
                {
                    // d is the synthetic inline-text child of a TEXTUAL_ELEMENT (not a TinyNodeImpl), so the
                    // fast path above missed it. An element is the ancestor of its own inline text; without
                    // this, fn:innermost/outermost over //node() wrongly kept every text-content element.
                    return a.Equals(d) || IsAncestorOrSelf(a, d.GetParent());
                }
                else if (d.GetNodeKind() == Types.Type.NAMESPACE)
                {
                }
                else if (d is Wrappers.VirtualCopy)
                {
                }
                else
                {
                    return false;
                }
            }


            // Generic implementation
            NodeInfo p = d;
            while (p != null)
            {
                if (a.Equals(p))
                {
                    return true;
                }

                p = p.GetParent();
            }

            return false;
        }

        // Helper classes to support axis iteration
        public static NodeTest NodeTestFromPredicate(INodePredicate predicate)
        {
            if (predicate is NodeTest)
            {
                return (NodeTest)predicate;
            }
            else
            {
                return NodeSelector.Of(predicate.Test);
            }
        }

        public static IAxisIterator FilteredSingleton(NodeInfo node, INodePredicate nodeTest)
        {
            if (node != null && nodeTest.Test(node))
            {
                return SingleNodeIterator.MakeIterator(node);
            }
            else
            {
                return EmptyIterator.OfNodes();
            }
        }

        public static int GetSiblingPosition(NodeInfo node, NodeTest nodeTest, int max)
        {
            if (node is ISiblingCountingNode && nodeTest is AnyNodeTest)
            {
                return ((ISiblingCountingNode)node).GetSiblingPosition();
            }

            IAxisIterator prev = node.IterateAxis(AxisInfo.PRECEDING_SIBLING, nodeTest);
            int count = 1;
            while (prev.Next() != null)
            {
                if (++count > max)
                {
                    return count;
                }
            }

            return count;
        }

        /// <summary>
        /// A class that delivers the children of a node as a Java Iterable
        /// </summary>
        internal sealed class ChildrenAsIterable : IEnumerable<NodeInfo>
        {
            private readonly NodeInfo parent;
            private INodePredicate filter = null;
            public ChildrenAsIterable(NodeInfo parent)
            {
                this.parent = parent;
            }

            public ChildrenAsIterable(NodeInfo parent, INodePredicate filter)
            {
                this.parent = parent;
                this.filter = filter;
            }

            public IEnumerator<NodeInfo> GetEnumerator()
            {
                IAxisIterator basis;
                if (filter == null)
                {
                    basis = parent.IterateAxis(AxisInfo.CHILD);
                }
                else
                {
                    basis = parent.IterateAxis(AxisInfo.CHILD, filter);
                }

                for (NodeInfo node; (node = basis.Next()) != null;)
                {
                    yield return node;
                }
            }
            // r1-injected NIE GetEnumerator removed (the renamed real GetEnumerator above implements IEnumerable<NodeInfo>)
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        internal sealed class AxisFilter : IAxisIterator
        {
            private readonly IAxisIterator @base;
            private readonly INodePredicate nodeTest;
            public AxisFilter(IAxisIterator @base, INodePredicate test)
            {
                this.@base = @base;
                nodeTest = test;
            }

            public NodeInfo Next()
            {
                while (true)
                {
                    NodeInfo next = @base.Next();
                    if (next == null)
                    {
                        return null;
                    }

                    if (nodeTest.Test(next))
                    {
                        return next;
                    }
                }
            }
            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
            public void Dispose() { }
        }

        /// <summary>
        /// General-purpose implementation of the ancestor and ancestor-or-self axes
        /// </summary>
        internal sealed class AncestorEnumeration : IAxisIterator
        {
            private readonly bool includeSelf;
            private bool atStart;
            private NodeInfo current;
            public AncestorEnumeration(NodeInfo start, bool includeSelf)
            {
                this.includeSelf = includeSelf;
                current = start;
                atStart = true;
            }

            public NodeInfo Next()
            {
                if (atStart)
                {
                    atStart = false;
                    if (includeSelf)
                    {
                        return current;
                    }
                }

                return current = current == null ? null : current.GetParent();
            }
            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
            public void Dispose() { }
        } // end of class AncestorEnumeration

        /// <summary>
        /// The descendant (or descendant-or-self) axis, in document order or reverse. Held as an
        /// explicit stack of child iterators rather than a chain of nested enumerations: nesting
        /// cost one stack frame per tree level on EVERY Next(), so a deep document was quadratic
        /// in time and unbounded in stack - and the depth is the input's, not the stylesheet's.
        /// </summary>
        internal sealed class DescendantEnumeration : IAxisIterator
        {
            // Backwards, a node is emitted AFTER its descendants, so each open level remembers the
            // node it still owes. Forwards nothing is owed - the node was emitted on the way down.
            private struct Level
            {
                internal ISequenceIterator Children;
                internal NodeInfo Owed;
            }

            private readonly List<Level> open = new List<Level>();
            private readonly NodeInfo start;
            private readonly bool includeSelf;
            private readonly bool forwards;
            private bool started = false;
            public DescendantEnumeration(NodeInfo start, bool includeSelf, bool forwards)
            {
                this.start = start;
                this.includeSelf = includeSelf;
                this.forwards = forwards;
            }

            private void Descend(NodeInfo node, NodeInfo owed)
            {
                ISequenceIterator children = EmptyIterator.OfNodes();
                if (node.HasChildNodes())
                {
                    children = node.IterateAxis(AxisInfo.CHILD);
                    if (!forwards)
                    {
                        children = Reverse.GetReverseIterator(children);
                    }
                }

                open.Add(new Level { Children = children, Owed = owed });
            }

            public NodeInfo Next()
            {
                if (!started)
                {
                    started = true;
                    Descend(start, !forwards && includeSelf ? start : null);
                    if (forwards && includeSelf)
                    {
                        return start;
                    }
                }

                while (open.Count != 0)
                {
                    Level level = open[open.Count - 1];
                    NodeInfo n = (NodeInfo)level.Children.Next();
                    if (n == null)
                    {
                        open.RemoveAt(open.Count - 1);
                        if (level.Owed != null)
                        {
                            return level.Owed;
                        }

                        continue;
                    }

                    if (!n.HasChildNodes())
                    {
                        return n;
                    }

                    Descend(n, forwards ? null : n);
                    if (forwards)
                    {
                        return n;
                    }
                }

                return null;
            }
            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
            public void Dispose() { }
        } // end of class DescendantEnumeration

        internal sealed class FollowingEnumeration : IAxisIterator
        {
            private readonly IAxisIterator ancestorEnum;
            private IAxisIterator siblingEnum;
            private IAxisIterator descendEnum = null;
            public FollowingEnumeration(NodeInfo start)
            {
                ancestorEnum = new AncestorEnumeration(start, false);
                switch (start.GetNodeKind())
                {
                    case Types.Type.ELEMENT:
                    case Types.Type.TEXT:
                    case Types.Type.COMMENT:
                    case Types.Type.PROCESSING_INSTRUCTION:
                        // gets following siblings
                        siblingEnum = start.IterateAxis(AxisInfo.FOLLOWING_SIBLING);
                        break;
                    case Types.Type.ATTRIBUTE:
                    case Types.Type.NAMESPACE:
                        // gets children of the attribute's parent node
                        NodeInfo parent = start.GetParent();
                        if (parent == null)
                        {
                            siblingEnum = EmptyIterator.OfNodes();
                        }
                        else
                        {
                            siblingEnum = parent.IterateAxis(AxisInfo.CHILD);
                        }

                        break;
                    default:
                        siblingEnum = EmptyIterator.OfNodes();
                        break;
                }
            }

            // Looped rather than self-calling on the ancestor step: that recursion was one frame
            // per level of the start node's depth, which the input document chooses.
            public NodeInfo Next()
            {
                while (true)
                {
                    if (descendEnum != null)
                    {
                        NodeInfo nextd = descendEnum.Next();
                        if (nextd != null)
                        {
                            return nextd;
                        }
                        else
                        {
                            descendEnum = null;
                        }
                    }

                    if (siblingEnum != null)
                    {
                        NodeInfo nexts = siblingEnum.Next();
                        if (nexts != null)
                        {
                            if (nexts.HasChildNodes())
                            {
                                descendEnum = new DescendantEnumeration(nexts, false, true);
                            }
                            else
                            {
                                descendEnum = null;
                            }

                            return nexts;
                        }
                        else
                        {
                            descendEnum = null;
                            siblingEnum = null;
                        }
                    }

                    NodeInfo nexta = ancestorEnum.Next();
                    if (nexta == null)
                    {
                        return null;
                    }

                    if (nexta.GetNodeKind() == Types.Type.DOCUMENT)
                    {
                        siblingEnum = EmptyIterator.OfNodes();
                    }
                    else
                    {
                        siblingEnum = nexta.IterateAxis(AxisInfo.FOLLOWING_SIBLING);
                    }
                }
            }
            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
            public void Dispose() { }
        } // end of class FollowingEnumeration

        internal sealed class PrecedingEnumeration : IAxisIterator
        {
            private readonly IAxisIterator ancestorEnum;
            private IAxisIterator siblingEnum;
            private IAxisIterator descendEnum = null;
            private readonly bool includeAncestors;
            public PrecedingEnumeration(NodeInfo start, bool includeAncestors)
            {
                this.includeAncestors = includeAncestors;
                ancestorEnum = new AncestorEnumeration(start, false);
                switch (start.GetNodeKind())
                {
                    case Types.Type.ELEMENT:
                    case Types.Type.TEXT:
                    case Types.Type.COMMENT:
                    case Types.Type.PROCESSING_INSTRUCTION:

                        // get preceding-sibling enumeration
                        siblingEnum = start.IterateAxis(AxisInfo.PRECEDING_SIBLING);
                        break;
                    default:
                        siblingEnum = EmptyIterator.OfNodes();
                        break;
                }
            }

            // Looped for the same reason as FollowingEnumeration.Next.
            public NodeInfo Next()
            {
                while (true)
                {
                    if (descendEnum != null)
                    {
                        NodeInfo nextd = descendEnum.Next();
                        if (nextd != null)
                        {
                            return nextd;
                        }
                        else
                        {
                            descendEnum = null;
                        }
                    }

                    if (siblingEnum != null)
                    {
                        NodeInfo nexts = siblingEnum.Next();
                        if (nexts != null)
                        {
                            if (nexts.HasChildNodes())
                            {
                                descendEnum = new DescendantEnumeration(nexts, true, false);
                                continue;
                            }
                            else
                            {
                                descendEnum = null;
                                return nexts;
                            }
                        }
                        else
                        {
                            descendEnum = null;
                            siblingEnum = null;
                        }
                    }

                    NodeInfo nexta = ancestorEnum.Next();
                    if (nexta == null)
                    {
                        return null;
                    }

                    if (nexta.GetNodeKind() == Types.Type.DOCUMENT)
                    {
                        siblingEnum = EmptyIterator.OfNodes();
                    }
                    else
                    {
                        siblingEnum = nexta.IterateAxis(AxisInfo.PRECEDING_SIBLING);
                    }

                    if (includeAncestors)
                    {
                        return nexta;
                    }
                }
            }
            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
            public void Dispose() { }
        } // end of class PrecedingEnumeration
    }
}

