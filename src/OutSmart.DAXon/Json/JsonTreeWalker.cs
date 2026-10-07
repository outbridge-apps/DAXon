////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;

namespace OutSmart.DAXon.Json
{
    /// <summary>
    /// Direct TinyTree feed for xml-to-json. The generic NodeInfo.Copy replay allocates a
    /// CodedName + NamespaceMap + AttributeInfo list + attribute map per element just to hand
    /// JsonReceiver a name it switches on and up to three attribute strings it extracts back
    /// out; this walker reads the tree arrays and calls JsonReceiver.StartEntryDirect with the
    /// values themselves. Event order, validation order and every FOJS0006 condition match the
    /// Copy + DocumentValidator path; non-Tiny inputs fall back to that path unchanged.
    /// </summary>
    internal static class JsonTreeWalker
    {
        internal static bool TryWalk(NodeInfo xml, JsonReceiver receiver, Core.Controller controller)
        {
            if (!(xml is TinyNodeImpl tiny))
            {
                return false;
            }

            TinyTree tree = tiny.tree;
            int root = tiny.nodeNr;
            int rootKind = tree.nodeKind[root];
            if (rootKind != Types.Type.DOCUMENT && rootKind != Types.Type.ELEMENT && rootKind != Types.Type.TEXTUAL_ELEMENT)
            {
                return false;
            }

            receiver.Open();
            if (rootKind == Types.Type.DOCUMENT)
            {
                WalkDocument(tree, root, receiver, controller);
            }
            else
            {
                WalkElement(tree, root, receiver, controller);
            }

            receiver.Close();
            return true;
        }

        // Replicates DocumentValidator("FOJS0006"): exactly one child element, whitespace-only
        // text permitted (ignored), comments/PIs ignored (JsonReceiver no-ops them anyway).
        private static void WalkDocument(TinyTree tree, int root, JsonReceiver receiver, Core.Controller controller)
        {
            short rootDepth = tree.depth[root];
            bool foundElement = false;
            int n = root + 1;
            while (n < tree.numberOfNodes && tree.depth[n] > rootDepth)
            {
                switch (tree.nodeKind[n])
                {
                    case Types.Type.ELEMENT:
                    case Types.Type.TEXTUAL_ELEMENT:
                        if (foundElement)
                        {
                            throw new XPathException("A valid document must have only one child element", "FOJS0006");
                        }

                        foundElement = true;
                        WalkElement(tree, n, receiver, controller);
                        break;
                    case Types.Type.TEXT:
                        UnicodeString text = TinyTextImpl.GetStringValue(tree, n);
                        if (!Values.Whitespace.IsAllWhite(text))
                        {
                            throw new XPathException("A valid document must contain no text outside the outermost element (found \"" + Err.Truncate30(text.Tidy()) + "\")", "FOJS0006");
                        }

                        break;
                }

                // advance past this child's whole subtree
                short childDepth = tree.depth[n];
                n++;
                while (n < tree.numberOfNodes && tree.depth[n] > childDepth)
                {
                    n++;
                }
            }

            if (!foundElement)
            {
                throw new XPathException("A valid document must have a child element", "FOJS0006");
            }
        }

        private static void WalkElement(TinyTree tree, int start, JsonReceiver receiver, Core.Controller controller)
        {
            NamePool pool = tree.GetNamePool();
            int fpArray = pool.AllocateFingerprint(NamespaceUri.FN, "array");
            int fpMap = pool.AllocateFingerprint(NamespaceUri.FN, "map");
            int fpString = pool.AllocateFingerprint(NamespaceUri.FN, "string");
            int fpNumber = pool.AllocateFingerprint(NamespaceUri.FN, "number");
            int fpBoolean = pool.AllocateFingerprint(NamespaceUri.FN, "boolean");
            int fpNull = pool.AllocateFingerprint(NamespaceUri.FN, "null");
            int fpKey = pool.AllocateFingerprint(NamespaceUri.NULL, "key");
            int fpEscapedKey = pool.AllocateFingerprint(NamespaceUri.NULL, "escaped-key");
            int fpEscaped = pool.AllocateFingerprint(NamespaceUri.NULL, "escaped");

            short startLevel = tree.depth[start];
            short level = -1;
            bool closePending = false;
            int next = start;
            do
            {
                // The tree can be far larger than the input cap (temp trees are built
                // in memory), so this long loop honours the transformation deadline like every
                // other hot loop; CheckTimeout itself is stride-throttled.
                controller?.CheckTimeout();
                short nodeLevel = tree.depth[next];
                if (closePending)
                {
                    level++;
                }

                for (; level > nodeLevel; level--)
                {
                    receiver.EndElement();
                }

                level = nodeLevel;
                int kind = tree.nodeKind[next];
                switch (kind)
                {
                    case Types.Type.ELEMENT:
                    case Types.Type.TEXTUAL_ELEMENT:
                        {
                            int fp = tree.nameCode[next] & NamePool.FP_MASK;
                            string local;
                            if (fp == fpString)
                            {
                                local = "string";
                            }
                            else if (fp == fpNumber)
                            {
                                local = "number";
                            }
                            else if (fp == fpMap)
                            {
                                local = "map";
                            }
                            else if (fp == fpArray)
                            {
                                local = "array";
                            }
                            else if (fp == fpBoolean)
                            {
                                local = "boolean";
                            }
                            else if (fp == fpNull)
                            {
                                local = "null";
                            }
                            else
                            {
                                // rare: wrong namespace, or an unknown FN-namespace element (the
                                // latter falls through to StartEntryDirect's own error)
                                NamespaceUri uri = pool.GetURI(fp);
                                local = pool.GetLocalName(fp);
                                if (!NamespaceUri.FN.Equals(uri))
                                {
                                    throw new XPathException("xml-to-json: element found in wrong namespace: Q{" + uri + "}" + local, "FOJS0006");
                                }
                            }

                            string key = null;
                            string escapedAtt = null;
                            string escapedKey = null;
                            if (kind == Types.Type.ELEMENT)
                            {
                                int att = tree.alpha[next];
                                if (att >= 0)
                                {
                                    while (att < tree.numberOfAttributes && tree.attParent[att] == next)
                                    {
                                        int afp = tree.attCode[att] & NamePool.FP_MASK;
                                        if (afp == fpKey)
                                        {
                                            key = tree.attValue[att];
                                        }
                                        else if (afp == fpEscapedKey)
                                        {
                                            escapedKey = tree.attValue[att];
                                        }
                                        else if (afp == fpEscaped)
                                        {
                                            escapedAtt = tree.attValue[att];
                                        }
                                        else
                                        {
                                            NamespaceUri auri = pool.GetURI(afp);
                                            if (NamespaceUri.NULL.Equals(auri) || NamespaceUri.FN.Equals(auri))
                                            {
                                                throw new XPathException("xml-to-json: Disallowed attribute in input: " + pool.GetLocalName(afp), "FOJS0006");
                                            } // attributes in other namespaces are ignored
                                        }

                                        att++;
                                    }
                                }
                            }

                            if (kind == Types.Type.TEXTUAL_ELEMENT)
                            {
                                closePending = false;
                                receiver.StartEntryDirect(local, key, escapedAtt, escapedKey);
                                receiver.Characters(TinyTextImpl.GetStringValue(tree, next), Loc.NONE, ReceiverOption.WHOLE_TEXT_NODE);
                                receiver.EndElement();
                            }
                            else
                            {
                                closePending = true;
                                receiver.StartEntryDirect(local, key, escapedAtt, escapedKey);
                            }

                            break;
                        }

                    case Types.Type.TEXT:
                        closePending = false;
                        receiver.Characters(TinyTextImpl.GetStringValue(tree, next), Loc.NONE, ReceiverOption.WHOLE_TEXT_NODE);
                        break;
                    case Types.Type.WHITESPACE_TEXT:
                        {
                            closePending = false;
                            long compressedValue = ((long)tree.alpha[next] << 32) | ((long)tree.beta[next] & 0xffffffff);
                            receiver.Characters(new CompressedWhitespace(compressedValue), Loc.NONE, ReceiverOption.WHOLE_TEXT_NODE);
                            break;
                        }

                    default:
                        // comments, PIs, parent pointers: JsonReceiver ignores them
                        closePending = false;
                        break;
                }

                next++;
            }
            while (next < tree.numberOfNodes && tree.depth[next] > startLevel);

            if (closePending)
            {
                level++;
            }

            for (; level > startLevel; level--)
            {
                receiver.EndElement();
            }
        }
    }
}
