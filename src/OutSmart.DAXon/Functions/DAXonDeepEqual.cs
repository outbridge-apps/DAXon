////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    internal sealed class DAXonDeepEqual : SystemFunction
    {
        public const int INCLUDE_NAMESPACES = 1;
        public const int INCLUDE_PREFIXES = 1 << 1;
        /// <summary>
        /// Flag indicating that comment children are taken into account when comparing element or document nodes
        /// </summary>
        public const int INCLUDE_COMMENTS = 1 << 2;
        /// <summary>
        /// Flag indicating that processing instruction nodes are taken into account when comparing element or document nodes
        /// </summary>
        public const int INCLUDE_PROCESSING_INSTRUCTIONS = 1 << 3;
        public const int EXCLUDE_WHITESPACE_TEXT_NODES = 1 << 4;
        public const int COMPARE_STRING_VALUES = 1 << 5;
        public const int COMPARE_ANNOTATIONS = 1 << 6;
        public const int WARNING_IF_FALSE = 1 << 7;
        /// <summary>
        /// Flag indicating that adjacent text nodes in the top-level sequence are to be merged
        /// </summary>
        public const int JOIN_ADJACENT_TEXT_NODES = 1 << 8;
        public const int COMPARE_ID_FLAGS = 1 << 9;
        public const int EXCLUDE_VARIETY = 1 << 10;

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        public override string StreamerName => "DeepEqual";
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            string flags = arguments[3].Head().GetStringValue();
            if (flags.IndexOf('!') >= 0)
            {

                // undocumented diagnostic option
                Logger err = context.GetConfiguration().Logger;
                Properties indent = new Properties();
                indent.SetProperty(DAXonOutputKeys.METHOD, "xml");
                indent.SetProperty(DAXonOutputKeys.INDENT, "yes");
                err.Info("DeepEqual: first argument:");
                QueryResult.Serialize(QueryResult.Wrap(arguments[0].Iterate(), context.GetConfiguration()), err.AsStreamResult(), indent);
                err.Info("DeepEqual: second argument:");
                QueryResult.Serialize(QueryResult.Wrap(arguments[1].Iterate(), context.GetConfiguration()), err.AsStreamResult(), indent);
            }

            IItem collationValue = arguments[2].Head();
            Configuration config = context.GetConfiguration();
            string collation = collationValue == null ? config.GetDefaultCollationName() : collationValue.GetStringValue();
            IStringCollator collator = config.GetCollation(collation);
            if (collator == null)
            {
                throw new XPathException("Unknown collation " + collation, "FOCH0002");
            }

            GenericAtomicComparer comparer = new GenericAtomicComparer(collator, context);
            int flag = 0;
            if (flags.Contains("N"))
            {
                flag |= INCLUDE_NAMESPACES;
            }

            if (flags.Contains("J"))
            {
                flag |= JOIN_ADJACENT_TEXT_NODES;
            }

            if (flags.Contains("C"))
            {
                flag |= INCLUDE_COMMENTS;
            }

            if (flags.Contains("P"))
            {
                flag |= INCLUDE_PROCESSING_INSTRUCTIONS;
            }

            if (flags.Contains("F"))
            {
                flag |= INCLUDE_PREFIXES;
            }

            if (flags.Contains("S"))
            {
                flag |= COMPARE_STRING_VALUES;
            }

            if (flags.Contains("A"))
            {
                flag |= COMPARE_ANNOTATIONS;
            }

            if (flags.Contains("I"))
            {
                flag |= COMPARE_ID_FLAGS;
            }

            if (flags.Contains("v"))
            {
                flag |= EXCLUDE_VARIETY;
            }

            if (flags.Contains("w"))
            {
                flag |= EXCLUDE_WHITESPACE_TEXT_NODES;
            }

            if (flags.Contains("?"))
            {
                flag |= WARNING_IF_FALSE;
            }

            bool result = DeepEqual(arguments[0].Iterate(), arguments[1].Iterate(), comparer, context, flag);
            return BooleanValue.Get(result);
        }

        public static bool DeepEqual(ISequenceIterator op1, ISequenceIterator op2, IAtomicComparer comparer, IXPathContext context, int flags)
        {
            bool result = true;
            string reason = null;
            IErrorReporter reporter = context.GetErrorReporter();
            try
            {
                if ((flags & JOIN_ADJACENT_TEXT_NODES) != 0)
                {
                    op1 = OutSmart.DAXon.Functions.DeepEqual.MergeAdjacentTextNodes(op1);
                    op2 = OutSmart.DAXon.Functions.DeepEqual.MergeAdjacentTextNodes(op2);
                }

                int pos1 = 0;
                int pos2 = 0;
                int itemSteps = 0;
                while (true)
                {
                    if ((++itemSteps & 1023) == 0)
                    {
                        Core.Controller.CheckActiveTimeout();
                    }

                    IItem item1 = op1.Next();
                    IItem item2 = op2.Next();
                    if (item1 == null && item2 == null)
                    {
                        break;
                    }

                    pos1++;
                    pos2++;
                    if (item1 == null || item2 == null)
                    {
                        result = false;
                        if (item1 == null)
                        {
                            reason = "Second sequence is longer (first sequence length = " + pos2 + ")";
                        }
                        else
                        {
                            reason = "First sequence is longer (second sequence length = " + pos1 + ")";
                        }

                        if (item1 is WhitespaceTextImpl || item2 is WhitespaceTextImpl)
                        {
                            reason += " (the first extra node is whitespace text)";
                        }

                        break;
                    }

                    if (item1 is IFunctionItem || item2 is IFunctionItem)
                    {
                        if (!(item1 is IFunctionItem && item2 is IFunctionItem))
                        {
                            reason = "if one item is a function then both must be functions (position " + pos1 + ")";
                            return false;
                        }


                        // two maps or arrays can be deep-equal
                        bool fe = ((IFunctionItem)item1).DeepEquals((IFunctionItem)item2, context, comparer, flags);
                        if (!fe)
                        {
                            result = false;
                            reason = "functions at position " + pos1 + " differ";
                            break;
                        }

                        continue;
                    }

                    if (item1 is ObjectValue<object> || item2 is ObjectValue<object>)
                    {
                        if (!item1.Equals(item2))
                        {
                            return false;
                        }

                        continue;
                    }

                    if (item1 is NodeInfo)
                    {
                        if (item2 is NodeInfo)
                        {
                            string message = DeepEquals((NodeInfo)item1, (NodeInfo)item2, comparer, context, flags);
                            if (message != null)
                            {
                                result = false;
                                reason = "nodes at position " + pos1 + " differ: " + message;
                                break;
                            }
                        }
                        else
                        {
                            result = false;
                            reason = "comparing a node to an atomic value at position " + pos1;
                            break;
                        }
                    }
                    else
                    {
                        if (item2 is NodeInfo)
                        {
                            result = false;
                            reason = "comparing an atomic value to a node at position " + pos1;
                            break;
                        }
                        else
                        {
                            AtomicValue av1 = (AtomicValue)item1;
                            AtomicValue av2 = (AtomicValue)item2;
                            if (av1.IsNaN() && av2.IsNaN())
                            {
                            }
                            else if (!comparer.ComparesEqual(av1, av2))
                            {
                                result = false;
                                reason = "atomic values at position " + pos1 + " differ";
                                break;
                            }
                        }
                    }
                } // end while
            }
            catch (UncheckedXPathException uxe)
            {
                throw uxe.GetXPathException();
            }
            catch (InvalidCastException)
            {

                // this will happen if the sequences contain non-comparable values
                // comparison errors are masked
                result = false;
                reason = "sequences contain non-comparable values";
            }

            if (!result)
            {
                Explain(reporter, reason, flags, null, null); //                config.getErrorReporter().warning(
                //                        new XPathException("deep-equal(): " + reason)
                //                );
            }

            return result;
        }

        // treat as equal, no action
        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        public static string DeepEquals(NodeInfo n1, NodeInfo n2, IAtomicComparer comparer, IXPathContext context, int flags)
        {

            // shortcut: a node is always deep-equal to itself
            if (n1.Equals(n2))
            {
                return null;
            }

            IErrorReporter reporter = context.GetErrorReporter();
            if (n1.GetNodeKind() != n2.GetNodeKind())
            {
                string reason = "node kinds differ: comparing " + OutSmart.DAXon.Functions.DeepEqual.ShowKind(n1) + " to " + OutSmart.DAXon.Functions.DeepEqual.ShowKind(n2);
                Explain(reporter, reason, flags, n1, n2);
                return reason;
            }

            switch (n1.GetNodeKind())
            {
                case Types.Type.ELEMENT:
                    string elementResult = CompareElementNode(n1, n2, comparer, context, flags, reporter, out bool elementHandled);
                    if (elementHandled)
                    {
                        return elementResult;
                    }

                    goto case Types.Type.DOCUMENT;
                case Types.Type.DOCUMENT:
                    IAxisIterator c1 = n1.IterateAxis(AxisInfo.CHILD);
                    IAxisIterator c2 = n2.IterateAxis(AxisInfo.CHILD);
                    int childSteps = 0;
                    while (true)
                    {
                        if ((++childSteps & 1023) == 0)
                        {
                            Core.Controller.CheckActiveTimeout();
                        }

                        NodeInfo d1 = c1.Next();
                        while (d1 != null && IsIgnorable(d1, flags))
                        {
                            d1 = c1.Next();
                        }

                        NodeInfo d2 = c2.Next();
                        while (d2 != null && IsIgnorable(d2, flags))
                        {
                            d2 = c2.Next();
                        }

                        if (d1 == null || d2 == null)
                        {
                            bool r = d1 == d2;
                            if (!r)
                            {
                                string message = "the first operand contains a node with " + (d1 == null ? "fewer" : "more") + " children than the second";
                                if (d1 is WhitespaceTextImpl || d2 is WhitespaceTextImpl)
                                {
                                    message += " (the first extra child is whitespace text)";
                                }

                                Explain(reporter, message, flags, n1, n2);
                                return message;
                            }

                            return null;
                        }

                        string recursiveResult = DeepEquals(d1, d2, comparer, context, flags);
                        if (recursiveResult != null)
                        {
                            return recursiveResult;
                        }
                    }

                case Types.Type.ATTRIBUTE:
                    if (!Navigator.HaveSameName(n1, n2))
                    {
                        string reason = "attribute names differ: " + NameOfNode.MakeName(n1).GetStructuredQName().EQName + " != " + NameOfNode.MakeName(n1).GetStructuredQName().EQName;
                        Explain(reporter, reason, flags, n1, n2);
                        return reason;
                    }

                    if (((flags & INCLUDE_PREFIXES) != 0) && !n1.GetPrefix().Equals(n2.GetPrefix()))
                    {
                        string reason = "attribute prefixes differ: " + n1.GetPrefix() + " != " + n2.GetPrefix();
                        Explain(reporter, reason, flags, n1, n2);
                        return reason;
                    }

                    if ((flags & COMPARE_ANNOTATIONS) != 0)
                    {
                        if (!n1.GetSchemaType().Equals(n2.GetSchemaType()))
                        {
                            string reason = "attributes have different type annotations";
                            Explain(reporter, reason, flags, n1, n2);
                            return reason;
                        }
                    }

                    bool ar;
                    if ((flags & COMPARE_STRING_VALUES) == 0)
                    {
                        ar = DeepEqual(n1.Atomize().Iterate(), n2.Atomize().Iterate(), comparer, context, 0);
                    }
                    else
                    {
                        ar = comparer.ComparesEqual(new StringValue(n1.UnicodeStringValue), new StringValue(n2.UnicodeStringValue));
                    }

                    if (!ar)
                    {
                        string reason = "attribute values differ";
                        Explain(reporter, reason, flags, n1, n2);
                        return reason;
                    }

                    if ((flags & COMPARE_ID_FLAGS) != 0)
                    {
                        if (n1.IsId() != n2.IsId())
                        {
                            string reason = "one attribute is an ID, the other is not";
                            Explain(reporter, reason, flags, n1, n2);
                            return reason;
                        }

                        if (n1.IsIdref() != n2.IsIdref())
                        {
                            string reason = "one attribute is an IDREF, the other is not";
                            Explain(reporter, reason, flags, n1, n2);
                            return reason;
                        }
                    }

                    return null;
                case Types.Type.PROCESSING_INSTRUCTION:
                case Types.Type.NAMESPACE:
                    if (!n1.GetLocalPart().Equals(n2.GetLocalPart()))
                    {
                        string reason = Types.Type.DisplayTypeName(n1) + " names differ";
                        Explain(reporter, reason, flags, n1, n2);
                        return reason;
                    }

                    goto case Types.Type.TEXT;
                case Types.Type.TEXT:
                case Types.Type.COMMENT:
                    bool vr = comparer.ComparesEqual((AtomicValue)n1.Atomize(), (AtomicValue)n2.Atomize());
                    if (!vr)
                    {
                        if ((flags & WARNING_IF_FALSE) != 0)
                        {
                            string v1 = n1.GetStringValue();
                            string v2 = n2.GetStringValue();
                            string message = "";
                            if (v1.Length != v2.Length)
                            {
                                message = "lengths (" + v1.Length + "," + v2.Length + ")";
                            }

                            if (v1.Length < 10 && v2.Length < 10)
                            {
                                message = " (\"" + v1 + "\" vs \"" + v2 + "\")";
                            }
                            else
                            {
                                int min = Math.Min(v1.Length, v2.Length);
                                if (v1.Substring(0, min).Equals(v2.Substring(0, min)))
                                {
                                    message += " different at char " + min + "(\"" + StringTool.DiagnosticDisplay((v1.Length > v2.Length ? v1 : v2).Substring(min)) + "\")";
                                }
                                else if (v1[0] != v2[0])
                                {
                                    message += " different at start " + "(\"" + v1.Substring(0, Math.Min(v1.Length, 10)) + "\", \"" + v2.Substring(0, Math.Min(v2.Length, 10)) + "\")";
                                }
                                else
                                {
                                    for (int i = 1; i < min; i++)
                                    {
                                        if (!v1.Substring(0, i).Equals(v2.Substring(0, i)))
                                        {
                                            message += " different at char " + (i - 1) + "(\"" + v1.Substring(i - 1, Math.Min(v1.Length, i + 10)) + "\", \"" + v2.Substring(i - 1, Math.Min(v2.Length, i + 10)) + "\")";
                                            break;
                                        }
                                    }
                                }
                            }

                            Explain(reporter, Types.Type.DisplayTypeName(n1) + " values differ (" + Navigator.GetPath(n1) + ", " + Navigator.GetPath(n2) + "): " + message, flags, n1, n2);
                            return message;
                        }
                        else
                        {
                            return "atomized values differ";
                        }
                    }

                    return null;
                default:
                    throw new ArgumentException("Unknown node type");
            }
        }

        // Compare the element-specific properties of two element nodes. handled=true means the returned
        // string is the final verdict (a difference reason, or null when the typed-value comparison
        // already settles it); handled=false means no difference was found here and the caller must go
        // on to compare children.
        private static string CompareElementNode(NodeInfo n1, NodeInfo n2, IAtomicComparer comparer, IXPathContext context, int flags, IErrorReporter reporter, out bool handled)
        {
            handled = true;
            if (!Navigator.HaveSameName(n1, n2))
            {
                string reason = "element names differ: " + NameOfNode.MakeName(n1).GetStructuredQName().EQName + " != " + NameOfNode.MakeName(n2).GetStructuredQName().EQName;
                Explain(reporter, reason, flags, n1, n2);
                return reason;
            }

            if (((flags & INCLUDE_PREFIXES) != 0) && !n1.GetPrefix().Equals(n2.GetPrefix()))
            {
                string reason = "element prefixes differ: " + n1.GetPrefix() + " != " + n2.GetPrefix();
                Explain(reporter, reason, flags, n1, n2);
                return reason;
            }

            IAxisIterator a1 = n1.IterateAxis(AxisInfo.ATTRIBUTE);
            IAxisIterator a2 = n2.IterateAxis(AxisInfo.ATTRIBUTE);
            if (!SequenceTool.SameLength(a1, a2))
            {
                string reason = "elements have different number of attributes";
                Explain(reporter, reason, flags, n1, n2);
                return reason;
            }

            NodeInfo att1;
            a1 = n1.IterateAxis(AxisInfo.ATTRIBUTE);
            while ((att1 = a1.Next()) != null)
            {
                IAxisIterator a2iter = n2.IterateAxis(AxisInfo.ATTRIBUTE, new SameNameTest(att1));
                NodeInfo att2 = a2iter.Next();
                if (att2 == null)
                {
                    string reason = "one element has an attribute " + NameOfNode.MakeName(att1).GetStructuredQName().EQName + ", the other does not";
                    Explain(reporter, reason, flags, n1, n2);
                    return reason;
                }

                string attReason = DeepEquals(att1, att2, comparer, context, flags);
                if (attReason != null)
                {
                    string reason = "elements have different values for the attribute " + NameOfNode.MakeName(att1).GetStructuredQName().EQName + " - " + attReason;
                    Explain(reporter, reason, flags, n1, n2);
                    return reason;
                }
            }

            if ((flags & INCLUDE_NAMESPACES) != 0)
            {
                NamespaceMap nm1 = n1.AllNamespaces;
                NamespaceMap nm2 = n2.AllNamespaces;
                if (!nm1.Equals(nm2))
                {
                    string reason = "elements have different @in-scope namespaces: " + nm1 + " versus " + nm2;
                    Explain(reporter, reason, flags, n1, n2);
                    return reason;
                }
            }

            if ((flags & COMPARE_ANNOTATIONS) != 0)
            {
                if (!n1.GetSchemaType().Equals(n2.GetSchemaType()))
                {
                    string reason = "elements have different type annotation";
                    Explain(reporter, reason, flags, n1, n2);
                    return reason;
                }
            }

            if ((flags & EXCLUDE_VARIETY) == 0)
            {
                if (n1.GetSchemaType().IsComplexType() != n2.GetSchemaType().IsComplexType())
                {
                    string reason = "one element has complex type, the other simple";
                    Explain(reporter, reason, flags, n1, n2);
                    return reason;
                }

                if (n1.GetSchemaType().IsComplexType())
                {
                    ComplexVariety variety1 = ((IComplexType)n1.GetSchemaType()).Variety;
                    ComplexVariety variety2 = ((IComplexType)n2.GetSchemaType()).Variety;
                    if (variety1 != variety2)
                    {
                        string reason = "both elements have complex type, but a different variety";
                        Explain(reporter, reason, flags, n1, n2);
                        return reason;
                    }
                }
            }

            if ((flags & COMPARE_STRING_VALUES) == 0)
            {
                ISchemaType type1 = n1.GetSchemaType();
                ISchemaType type2 = n2.GetSchemaType();
                bool isSimple1 = type1.IsSimpleType() || ((IComplexType)type1).IsSimpleContent();
                bool isSimple2 = type2.IsSimpleType() || ((IComplexType)type2).IsSimpleContent();
                if (isSimple1 != isSimple2)
                {
                    string reason = "one element has a simple type, the other does not";
                    Explain(reporter, reason, flags, n1, n2);
                    return reason;
                }

                if (isSimple1)
                {
                    IAtomicIterator v1 = n1.Atomize().Iterate();
                    IAtomicIterator v2 = n2.Atomize().Iterate();
                    bool typedValueComparison = DeepEqual(v1, v2, comparer, context, flags);
                    return typedValueComparison ? null : "typed values of elements differ";
                }
            }

            if ((flags & COMPARE_ID_FLAGS) != 0)
            {
                if (n1.IsId() != n2.IsId())
                {
                    string reason = "one element is an ID, the other is not";
                    Explain(reporter, reason, flags, n1, n2);
                    return reason;
                }

                if (n1.IsIdref() != n2.IsIdref())
                {
                    string reason = "one element is an IDREF, the other is not";
                    Explain(reporter, reason, flags, n1, n2);
                    return reason;
                }
            }

            handled = false;
            return null;
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        private static bool IsIgnorable(NodeInfo node, int flags)
        {
            int kind = node.GetNodeKind();
            if (kind == Types.Type.COMMENT)
            {
                return (flags & INCLUDE_COMMENTS) == 0;
            }
            else if (kind == Types.Type.PROCESSING_INSTRUCTION)
            {
                return (flags & INCLUDE_PROCESSING_INSTRUCTIONS) == 0;
            }
            else if (kind == Types.Type.TEXT)
            {
                return ((flags & EXCLUDE_WHITESPACE_TEXT_NODES) != 0) && Whitespace.IsAllWhite(node.UnicodeStringValue);
            }

            return false;
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        private static void Explain(IErrorReporter reporter, string message, int flags, NodeInfo n1, NodeInfo n2)
        {
            if ((flags & WARNING_IF_FALSE) != 0)
            {
                reporter.Report(new XmlProcessingIncident("deep-equal() " + (n1 != null && n2 != null ? "comparing " + Navigator.GetPath(n1) + " to " + Navigator.GetPath(n2) + ": " : ": ") + message).AsWarning());
            }
        }

    }
}
