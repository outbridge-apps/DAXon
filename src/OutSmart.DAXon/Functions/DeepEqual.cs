////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    public class DeepEqual : CollatingFunctionFixed
    {
        public static OptionsParameter OPTION_DETAILS;

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        public override string StreamerName => "DeepEqual";
        static DeepEqual()
        {
            OptionsParameter o = new OptionsParameter();
            o.AddAllowedOption("base-uri", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("comments", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("debug", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("false-on-error", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("id-property", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("idrefs-property", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("@in-scope-namespaces", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("namespace-prefixes", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("nilled-property", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("normalize-space", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("preserve-space", SequenceType.SINGLE_BOOLEAN, BooleanValue.TRUE);
            o.AddAllowedOption("processing-instructions", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("text-boundaries", SequenceType.SINGLE_BOOLEAN, BooleanValue.TRUE);
            o.AddAllowedOption("timezones", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("type-annotations", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            o.AddAllowedOption("type-variety", SequenceType.SINGLE_BOOLEAN, BooleanValue.TRUE);
            o.AddAllowedOption("typed-values", SequenceType.SINGLE_BOOLEAN, BooleanValue.TRUE);
            o.AddAllowedOption("normalization-form", SequenceType.OPTIONAL_STRING, EmptySequence.GetInstance());
            o.SetAllowedValues("normalization-form", "FOJS0005", "NFC", "NFD", "NFKC", "NFKD");
            o.AddAllowedOption("unordered-elements", BuiltInAtomicType.QNAME.ZeroOrMore(), EmptySequence.GetInstance());
            OPTION_DETAILS = o;
        }

        public static Func<DeepEqual> New() => () => new DeepEqual();

        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            Expression[] newArgs = new Expression[4];
            newArgs[0] = arguments[0];
            newArgs[1] = arguments[1];
            if (arguments.Length < 3 || arguments[2] is DefaultedArgumentExpression)
            {
                newArgs[2] = new StringLiteral(GetRetainedStaticContext().DefaultCollationName);
            }
            else
            {
                newArgs[2] = arguments[2];
            }

            if (arguments.Length < 4 || arguments[3] is DefaultedArgumentExpression)
            {
                newArgs[3] = Literal.MakeLiteral(new Values.Maps.DictionaryMap());
            }
            else
            {
                newArgs[3] = arguments[3];
            }

            SetArity(4);
            return base.MakeFunctionCall(newArgs);
        }

        public static bool DeepEqualFn(ISequenceIterator op1, ISequenceIterator op2, IXPathContext context, DeepEqualOptions options)
        {
            // Nested arrays and maps recurse back through here (AbstractArrayItem/MapItem call it
            // per member); the node-vs-node overload carries the matching probe for trees.
            StackGuard.Probe();
            bool result = true;
            string reason = null;
            IErrorReporter reporter = context.GetErrorReporter();
            try
            {
                if (!options.textBoundariesSignificant)
                {
                    op1 = MergeAdjacentTextNodes(op1);
                    op2 = MergeAdjacentTextNodes(op2);
                }

                // codepoints-vs-range (the astral round-trip shape): both operands are primitive
                // integer streams, so compare the ints directly — the generic loop below boxes an
                // Int64Value PAIR per position. Exact for the shape: integers have no NaN,
                // collation does not apply to numbers, and both item types are xs:integer so the
                // annotation gate cannot fire. RawCodepoints is valid: no Next() was issued yet.
                {
                    Text.CodepointIterator cpSide = op1 as Text.CodepointIterator ?? op2 as Text.CodepointIterator;
                    Expressions.AscendingRangeIterator rangeSide =
                        op2 as Expressions.AscendingRangeIterator ?? op1 as Expressions.AscendingRangeIterator;
                    if (cpSide != null && rangeSide != null && rangeSide.GetStep().LongValue() == 1)
                    {
                        Collections.IIntIterator raw = cpSide.RawCodepoints;
                        long v = rangeSide.GetMin().LongValue();
                        long limit = rangeSide.GetMax().LongValue();
                        long steps = 0;
                        while (raw.MoveNext())
                        {
                            if ((++steps & 0xffff) == 0)
                            {
                                Core.Controller.CheckActiveTimeout();
                            }

                            if (v > limit || raw.Current != v)
                            {
                                Explain(reporter, "codepoint sequence and range differ", options, null, null);
                                return false;
                            }

                            v++;
                        }

                        if (v <= limit)
                        {
                            Explain(reporter, "range is longer than the codepoint sequence", options, null, null);
                            return false;
                        }

                        return true;
                    }
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
                        bool fe = ((IFunctionItem)item1).DeepEqual40((IFunctionItem)item2, context, options);
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
                            string message = DeepEqualFn((NodeInfo)item1, (NodeInfo)item2, context, options);
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
                            // Plain-integer pair: eq is one long compare. NaN is impossible,
                            // collation does not apply to numbers, and the annotation gate
                            // cannot fire (both item types are xs:integer by the guard).
                            if (item1 is Int64Value i1 && item2 is Int64Value i2
                                && ReferenceEquals(i1.GetItemType(), BuiltInAtomicType.INTEGER)
                                && ReferenceEquals(i2.GetItemType(), BuiltInAtomicType.INTEGER))
                            {
                                if (i1.LongValue() != i2.LongValue())
                                {
                                    result = false;
                                    reason = "atomic values at position " + pos1 + " differ";
                                    break;
                                }

                                continue;
                            }

                            AtomicValue av1 = (AtomicValue)item1;
                            AtomicValue av2 = (AtomicValue)item2;
                            if (av1.IsNaN() && av2.IsNaN())
                            {
                            }
                            else if (!options.comparer.ComparesEqual(av1, av2))
                            {
                                result = false;
                                reason = "atomic values at position " + pos1 + " differ";
                                break;
                            }

                            if (options.typeAnnotationsSignificant && !av1.GetItemType().Equals(av2.GetItemType()))
                            {
                                result = false;
                                reason = "atomic values at position " + pos1 + " have different type annotations";
                                break;
                            }

                            if (options.namespacePrefixesSignificant && av1 is QualifiedNameValue && av2 is QualifiedNameValue && !((QualifiedNameValue)av1).GetPrefix().Equals(((QualifiedNameValue)av2).GetPrefix()))
                            {
                                result = false;
                                reason = "QName values at position " + pos1 + " have different namespace prefixes";
                                break;
                            }

                            if (options.timezonesSignificant && av1 is CalendarValue && av2 is CalendarValue && ((CalendarValue)av1).TimezoneInMinutes != ((CalendarValue)av2).TimezoneInMinutes)
                            {
                                result = false;
                                reason = "Values at position " + pos1 + " have different timezone";
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
                Explain(reporter, reason, options, null, null);
            }

            return result;
        }

        // treat as equal, no action
        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        public static string DeepEqualFn(NodeInfo n1, NodeInfo n2, IXPathContext context, DeepEqualOptions options)
        {
            // Descends child-by-child, so the recursion depth is the INPUT TREE's depth: unlike a
            // looping stylesheet this is driven by data the host did not write. Converted to
            // SXLM0001 once, at the fn:deep-equal entry.
            StackGuard.Probe();

            // shortcut: a node is always deep-equal to itself
            if (n1.Equals(n2))
            {
                return null;
            }

            IErrorReporter reporter = context.GetErrorReporter();
            if (n1.GetNodeKind() != n2.GetNodeKind())
            {
                string reason = "node kinds differ: comparing " + ShowKind(n1) + " to " + ShowKind(n2);
                Explain(reporter, reason, options, n1, n2);
                return reason;
            }

            if (options.baseUriSignificant && !object.Equals(n1.GetBaseURI(), n2.GetBaseURI()))
            {
                string reason = "base URIs differ: comparing " + n1.GetBaseURI() + " to " + n2.GetBaseURI();
                Explain(reporter, reason, options, n1, n2);
                return reason;
            }

            if (options.typeAnnotationsSignificant && !n1.GetSchemaType().Equals(n2.GetSchemaType()))
            {
                string reason = "nodes have different type annotations";
                Explain(reporter, reason, options, n1, n2);
                return reason;
            }

            switch (n1.GetNodeKind())
            {
                case Types.Type.ELEMENT:
                    string elementResult = CompareElementNode(n1, n2, context, options, reporter, out bool elementHandled);
                    if (elementHandled)
                    {
                        return elementResult;
                    }

                    goto case Types.Type.DOCUMENT;
                case Types.Type.DOCUMENT:
                    ISequenceIterator c1 = n1.IterateAxis(AxisInfo.CHILD, NodeSelector.Of((node) => !IsIgnorable(node, options)));
                    ISequenceIterator c2 = n2.IterateAxis(AxisInfo.CHILD, NodeSelector.Of((node) => !IsIgnorable(node, options)));
                    if (!options.textBoundariesSignificant)
                    {
                        c1 = MergeAdjacentTextNodes(c1);
                        c2 = MergeAdjacentTextNodes(c2);
                    }

                    int childSteps = 0;
                    while (true)
                    {
                        if ((++childSteps & 1023) == 0)
                        {
                            Core.Controller.CheckActiveTimeout();
                        }

                        NodeInfo d1 = (NodeInfo)c1.Next();
                        NodeInfo d2 = (NodeInfo)c2.Next();
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

                                Explain(reporter, message, options, n1, n2);
                                return message;
                            }

                            return null;
                        }

                        string recursiveResult = DeepEqualFn(d1, d2, context, options);
                        if (recursiveResult != null)
                        {
                            return recursiveResult;
                        }
                    }

                case Types.Type.ATTRIBUTE:
                    if (!Navigator.HaveSameName(n1, n2))
                    {
                        string reason = "attribute names differ: " + NameOfNode.MakeName(n1).GetStructuredQName().EQName + " != " + NameOfNode.MakeName(n1).GetStructuredQName().EQName;
                        Explain(reporter, reason, options, n1, n2);
                        return reason;
                    }

                    if (options.namespacePrefixesSignificant && !n1.GetPrefix().Equals(n2.GetPrefix()))
                    {
                        string reason = "attribute prefixes differ: " + n1.GetPrefix() + " != " + n2.GetPrefix();
                        Explain(reporter, reason, options, n1, n2);
                        return reason;
                    }

                    if (options.typeAnnotationsSignificant)
                    {
                        if (!n1.GetSchemaType().Equals(n2.GetSchemaType()))
                        {
                            string reason = "attributes have different type annotations";
                            Explain(reporter, reason, options, n1, n2);
                            return reason;
                        }
                    }

                    bool ar;
                    if (options.typedValuesSignificant)
                    {
                        ar = DeepEqualFn(n1.Atomize().Iterate(), n2.Atomize().Iterate(), context, options);
                    }
                    else
                    {
                        ar = options.comparer.ComparesEqual(new StringValue(n1.UnicodeStringValue), new StringValue(n2.UnicodeStringValue));
                    }

                    if (!ar)
                    {
                        string reason = "attribute values differ";
                        Explain(reporter, reason, options, n1, n2);
                        return reason;
                    }

                    if (options.idSignificant && n1.IsId() != n2.IsId())
                    {
                        string reason = "one attribute is an ID, the other is not";
                        Explain(reporter, reason, options, n1, n2);
                        return reason;
                    }

                    if (options.idrefSignificant && n1.IsIdref() != n2.IsIdref())
                    {
                        string reason = "one attribute is an IDREF, the other is not";
                        Explain(reporter, reason, options, n1, n2);
                        return reason;
                    }

                    return null;
                case Types.Type.PROCESSING_INSTRUCTION:
                case Types.Type.NAMESPACE:
                    if (!n1.GetLocalPart().Equals(n2.GetLocalPart()))
                    {
                        string reason = Types.Type.DisplayTypeName(n1) + " names differ";
                        Explain(reporter, reason, options, n1, n2);
                        return reason;
                    }

                    goto case Types.Type.TEXT;
                case Types.Type.TEXT:
                case Types.Type.COMMENT:
                    bool vr = CompareStrings(n1.GetStringValue(), n2.GetStringValue(), options, context);

                    if (!vr)
                    {
                        if (options.debug)
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
                                            message += " different at char " + (i - 1) + "(\"" + v1.Substring(i - 1, Math.Min(v1.Length, i + 10) - i + 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/ + "\", \"" + v2.Substring(i - 1, Math.Min(v2.Length, i + 10) - i + 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/ + "\")";
                                            break;
                                        }
                                    }
                                }
                            }

                            Explain(reporter, Types.Type.DisplayTypeName(n1) + " values differ (" + Navigator.GetPath(n1) + ", " + Navigator.GetPath(n2) + "): " + message, options, n1, n2);
                            return message;
                        }
                        else
                        {
                            return "atomized values differ";
                        }
                    }

                    return null;
                default:
                    throw new ArgumentException("Unknown node kind");
            }
        }

        // Compare the element-specific properties of two element nodes (name, prefix, namespaces,
        // attributes, type annotation/variety, typed value, id/idref/nilled). handled=true means the
        // returned string is the final verdict (a difference reason, or null when typed values / an
        // unordered-element comparison already settle it); handled=false means no difference was found
        // here and the caller must go on to compare children.
        private static string CompareElementNode(NodeInfo n1, NodeInfo n2, IXPathContext context, DeepEqualOptions options, IErrorReporter reporter, out bool handled)
        {
            handled = true;
            if (!Navigator.HaveSameName(n1, n2))
            {
                string reason = "element names differ: " + NameOfNode.MakeName(n1).GetStructuredQName().EQName + " != " + NameOfNode.MakeName(n2).GetStructuredQName().EQName;
                Explain(reporter, reason, options, n1, n2);
                return reason;
            }

            if (options.namespacePrefixesSignificant && !n1.GetPrefix().Equals(n2.GetPrefix()))
            {
                string reason = "element prefixes differ: " + n1.GetPrefix() + " != " + n2.GetPrefix();
                Explain(reporter, reason, options, n1, n2);
                return reason;
            }

            if (options.inScopeNamespacesSignificant && !n1.AllNamespaces.Equals(n2.AllNamespaces))
            {
                string reason = "@in-scope namespaces differ: " + n1.AllNamespaces + " versus " + n2.AllNamespaces;
                Explain(reporter, reason, options, n1, n2);
                return reason;
            }

            IAxisIterator a1 = n1.IterateAxis(AxisInfo.ATTRIBUTE);
            IAxisIterator a2 = n2.IterateAxis(AxisInfo.ATTRIBUTE);
            if (!SequenceTool.SameLength(a1, a2))
            {
                string reason = "elements have different number of attributes";
                Explain(reporter, reason, options, n1, n2);
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
                    Explain(reporter, reason, options, n1, n2);
                    return reason;
                }

                string attReason = DeepEqualFn(att1, att2, context, options);
                if (attReason != null)
                {
                    string reason = "elements have different values for the attribute " + NameOfNode.MakeName(att1).GetStructuredQName().EQName + " - " + attReason;
                    Explain(reporter, reason, options, n1, n2);
                    return reason;
                }
            }

            if (options.inScopeNamespacesSignificant)
            {
                NamespaceMap nm1 = n1.AllNamespaces;
                NamespaceMap nm2 = n2.AllNamespaces;
                if (!nm1.Equals(nm2))
                {
                    string reason = "elements have different @in-scope namespaces: " + nm1 + " versus " + nm2;
                    Explain(reporter, reason, options, n1, n2);
                    return reason;
                }
            }

            if (options.typeAnnotationsSignificant)
            {
                if (!n1.GetSchemaType().Equals(n2.GetSchemaType()))
                {
                    string reason = "elements have different type annotation";
                    Explain(reporter, reason, options, n1, n2);
                    return reason;
                }
            }

            if (options.typeVarietySignificant)
            {
                if (n1.GetSchemaType().IsComplexType() != n2.GetSchemaType().IsComplexType())
                {
                    string reason = "one element has complex type, the other simple";
                    Explain(reporter, reason, options, n1, n2);
                    return reason;
                }

                if (n1.GetSchemaType().IsComplexType())
                {
                    ComplexVariety variety1 = ((IComplexType)n1.GetSchemaType()).Variety;
                    ComplexVariety variety2 = ((IComplexType)n2.GetSchemaType()).Variety;
                    if (variety1 != variety2)
                    {
                        string reason = "both elements have complex type, but a different variety";
                        Explain(reporter, reason, options, n1, n2);
                        return reason;
                    }
                }
            }

            if (options.typedValuesSignificant)
            {
                ISchemaType type1 = n1.GetSchemaType();
                ISchemaType type2 = n2.GetSchemaType();
                bool isSimple1 = type1.IsSimpleType() || ((IComplexType)type1).IsSimpleContent();
                bool isSimple2 = type2.IsSimpleType() || ((IComplexType)type2).IsSimpleContent();
                if (options.typeVarietySignificant && isSimple1 != isSimple2)
                {
                    string reason = "one element has a simple type, the other does not";
                    Explain(reporter, reason, options, n1, n2);
                    return reason;
                }

                if (isSimple1 && isSimple2)
                {
                    IAtomicIterator v1 = n1.Atomize().Iterate();
                    IAtomicIterator v2 = n2.Atomize().Iterate();
                    bool typedValueComparison = DeepEqualFn(v1, v2, context, options);
                    return typedValueComparison ? null : "typed values of elements differ";
                }
            }

            if (options.idSignificant && n1.IsId() != n2.IsId())
            {
                string reason = "one element is an ID, the other is not";
                Explain(reporter, reason, options, n1, n2);
                return reason;
            }

            if (options.idrefSignificant && n1.IsIdref() != n2.IsIdref())
            {
                string reason = "one element is an IDREF, the other is not";
                Explain(reporter, reason, options, n1, n2);
                return reason;
            }

            if (options.nilledSignificant && n1.IsNilled() != n2.IsNilled())
            {
                string reason = "one element is nilled, the other is not";
                Explain(reporter, reason, options, n1, n2);
                return reason;
            }

            if (options.unorderedElements.Contains(NameOfNode.MakeName(n1).GetStructuredQName()))
            {
                return HasSameChildrenUnordered(n1, n2, options, context);
            }

            handled = false;
            return null;
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        private static string HasSameChildrenUnordered(NodeInfo e0, NodeInfo e1, DeepEqualOptions options, IXPathContext context)
        {
            IList<NodeInfo> children0 = new List<NodeInfo>();
            IList<NodeInfo> children1 = new List<NodeInfo>();
            foreach (NodeInfo c0 in e0.Children())
            {
                if (!IsIgnorable(c0, options))
                {
                    children0.Add(c0);
                }
            }

            foreach (NodeInfo c1 in e1.Children())
            {
                if (!IsIgnorable(c1, options))
                {
                    children1.Add(c1);
                }
            }

            if (children0.Count != children1.Count)
            {
                return "Number of children differs: " + children0.Count + " vs. " + children1.Count;
            }

            IList<int> hashcodes1 = new List<int>(children1.Count);
            IntSet hashSet = new IntHashSet();
            foreach (NodeInfo nodeInfo in children1)
            {
                int hash = ComputeHashCode(nodeInfo, options);
                hashSet.Add(hash);
                hashcodes1.Add(hash);
            }

            foreach (NodeInfo c0 in children0)
            {
                int hash = ComputeHashCode(c0, options);
                if (!hashSet.Contains(hash))
                {
                    return "Node found among first node's children with no counterpart among the second node's children";
                }

                int found = -1;
                for (int j = 0; j < hashcodes1.Count; j++)
                {
                    if (hash == hashcodes1[j] && DeepEqualFn(c0, children1[j], context, options) == null)
                    {
                        found = j;
                        break;
                    }
                }

                if (found >= 0)
                {
                    children1.RemoveAt(found);
                    hashcodes1.Remove(found);
                }
                else
                {
                    return "Node found among first node's children with no counterpart among the second node's children";
                }
            }

            return null;
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        private static int ComputeHashCode(NodeInfo node, DeepEqualOptions options)
        {

            // Keep it simple for now - independent of the options
            return node.GetNodeKind() << 24 ^ node.Fingerprint ^ (node.Attributes().Size() << 10);
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        private static bool CompareStrings(string s1, string s2, DeepEqualOptions options, IXPathContext context)
        {
            if (options.normalizeSpace)
            {
                s1 = Whitespace.CollapseWhitespace(s1);
                s2 = Whitespace.CollapseWhitespace(s2);
            }

            if (options.normalizationForm != null)
            {
                try
                {
                    s1 = NormalizeUnicode.Normalize(s1, options.normalizationForm);
                    s2 = NormalizeUnicode.Normalize(s2, options.normalizationForm);
                }
                catch (XPathException)
                {
                    return false;
                }
            }

            return options.stringCollator.ComparesEqual(StringView.Of(s1), StringView.Of(s2));
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        private static bool IsIgnorable(NodeInfo node, DeepEqualOptions options)
        {
            int kind = node.GetNodeKind();
            if (kind == Types.Type.COMMENT)
            {
                return !options.commentsSignificant;
            }
            else if (kind == Types.Type.PROCESSING_INSTRUCTION)
            {
                return !options.processingInstructionsSignificant;
            }
            else if (kind == Types.Type.TEXT)
            {
                return (!options.preserveSpace) && Whitespace.IsAllWhite(node.UnicodeStringValue);
            }

            return false;
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        private static void Explain(IErrorReporter reporter, string message, DeepEqualOptions options, NodeInfo n1, NodeInfo n2)
        {
            if (options.debug)
            {
                reporter.Report(new XmlProcessingIncident("deep-equal() " + (n1 != null && n2 != null ? "comparing " + Navigator.GetPath(n1) + " to " + Navigator.GetPath(n2) + ": " : ": ") + message).AsWarning());
            }
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        internal static string ShowKind(IItem item)
        {
            if (item is NodeInfo && ((NodeInfo)item).GetNodeKind() == Types.Type.TEXT && Whitespace.IsAllWhite(item.UnicodeStringValue))
            {
                return "whitespace text() node";
            }
            else
            {
                return Types.Type.DisplayTypeName(item);
            }
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        internal static ISequenceIterator MergeAdjacentTextNodes(ISequenceIterator @in)
        {
            IList<IItem> items = new List<IItem>(20);
            bool prevIsText = false;
            UnicodeBuilder textBuffer = new UnicodeBuilder();
            while (true)
            {
                IItem next = @in.Next();
                if (next == null)
                {
                    break;
                }

                if (next is NodeInfo && ((NodeInfo)next).GetNodeKind() == Types.Type.TEXT)
                {
                    textBuffer.Accept(next.UnicodeStringValue);
                    prevIsText = true;
                }
                else
                {
                    if (prevIsText)
                    {
                        Orphan textNode = new Orphan(null);
                        textNode.SetNodeKind(Types.Type.TEXT);
                        textNode.SetStringValue(textBuffer.ToUnicodeString());
                        items.Add(textNode);
                        textBuffer.Clear();
                    }

                    prevIsText = false;
                    items.Add(next);
                }
            }

            if (prevIsText)
            {
                Orphan textNode = new Orphan(null);
                textNode.SetNodeKind(Types.Type.TEXT);
                textNode.SetStringValue(textBuffer.ToUnicodeString());
                items.Add(textNode);
            }

            return new ListIterator.Of<IItem>(items);
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IItem arg3 = arguments.Length >= 3 ? arguments[2].Head() : null;
            string collationName = arg3 == null ? GetRetainedStaticContext().DefaultCollationName : arg3.GetStringValue();
            MapItem options = new Values.Maps.DictionaryMap();
            if (arguments.Length >= 4)
            {
                MapItem suppliedOptions = (MapItem)arguments[3].Head();
                if (suppliedOptions != null)
                {
                    options = suppliedOptions;
                }
            }


            //GenericAtomicComparer comparer = new GenericAtomicComparer(getStringCollator(), context);
            DeepEqualOptions eqOptions = new DeepEqualOptions(options, collationName, context);
            bool b;
            try
            {
                b = DeepEqualFn(arguments[0].Iterate(), arguments[1].Iterate(), context, eqOptions);
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                throw e.Describe(
                    "Too many nested levels in deep-equal. The compared input is too deeply nested.",
                    DAXonErrorCode.SXLM0001, null);
            }

            return BooleanValue.Get(b);
        }

        public class DeepEqualOptions
        {
            private static readonly string[] booleanOptions = new string[]
            {
                "base-uri",
                "comments",
                "debug",
                "false-on-error",
                "id-property",
                "idrefs-property",
                "@in-scope-namespaces",
                "namespace-prefixes",
                "nilled-property",
                "normalize-space",
                "preserve-space",
                "processing-instructions",
                "text-boundaries",
                "timezones",
                "type-annotations",
                "type-variety",
                "typed-values"
            };
            public bool baseUriSignificant;
            public bool commentsSignificant;
            public bool debug;
            public bool falseOnError;
            public bool idSignificant;
            public bool idrefSignificant;
            public bool inScopeNamespacesSignificant;
            public bool namespacePrefixesSignificant;
            public string normalizationForm;
            public bool nilledSignificant;
            public bool normalizeSpace;
            public bool processingInstructionsSignificant;
            public bool textBoundariesSignificant = true;
            public bool timezonesSignificant;
            public bool typeAnnotationsSignificant;
            public bool typeVarietySignificant = true;
            public bool typedValuesSignificant = true;
            public HashSet<StructuredQName> unorderedElements = new HashSet<StructuredQName>();
            public bool preserveSpace = true;
            public string collationName;
            public IStringCollator stringCollator;
            public IAtomicComparer comparer;
            public DeepEqualOptions()
            {
            }

            public DeepEqualOptions(MapItem map, string collationName, IXPathContext context)
            {
                // Empty options map (every fn:deep-equal call without options): the registered
                // OPTION_DETAILS defaults are exactly these field initializers, so applying them is a
                // no-op — skip the per-call ProcessSuppliedOptions walk + 17 SetBooleanOption lookups.
                // (Keep the registration in the static ctor and these initializers in sync.)
                if (map.Size() != 0)
                {
                    Dictionary<string, IGroundedValue> values = OPTION_DETAILS.ProcessSuppliedOptions(map, context);
                    foreach (string option in DeepEqualOptions.booleanOptions)
                    {
                        SetBooleanOption(values, option);
                    }

                    IGroundedValue normForm = map[new StringValue("normalization-form")];
                    if (normForm != null)
                    {
                        normalizationForm = normForm.GetStringValue();
                    }

                    IGroundedValue listedElements = map[new StringValue("unordered-elements")];
                    if (listedElements != null)
                    {
                        unorderedElements = new HashSet<StructuredQName>();
                        foreach (IItem item in listedElements.AsIterable())
                        {
                            if (item is QNameValue)
                            {
                                unorderedElements.Add(((QNameValue)item).GetStructuredQName());
                            }
                        }
                    }
                }

                this.collationName = collationName;
                stringCollator = context.GetConfiguration().GetCollation(collationName);
                if (stringCollator == null)
                {
                    throw new XPathException("Unknown collation " + collationName, "FOCH0002");
                }

                comparer = GenericAtomicComparer.MakeAtomicComparer(BuiltInAtomicType.ANY_ATOMIC, BuiltInAtomicType.ANY_ATOMIC, stringCollator, context);
                if (normalizeSpace || normalizationForm != null)
                {
                    comparer = new NormalizingComparer(comparer, this);
                }
            }

            public static DeepEqualOptions DefaultOptions()
            {
                return new DeepEqualOptions();
            }

            private void SetBooleanOption(Dictionary<string, IGroundedValue> map, string optionName)
            {
                ISequence value = map.GetOrDefault(optionName);
                if (value != null)
                {
                    bool booleanValue = ExpressionTool.EffectiveBooleanValue(value.Iterate());
                    switch (optionName)
                    {
                        case "base-uri":
                            baseUriSignificant = booleanValue;
                            return;
                        case "comments":
                            commentsSignificant = booleanValue;
                            return;
                        case "debug":
                            debug = booleanValue;
                            return;
                        case "false-on-error":
                            falseOnError = booleanValue;
                            return;
                        case "id-property":
                            idSignificant = booleanValue;
                            return;
                        case "idrefs-property":
                            idrefSignificant = booleanValue;
                            return;
                        case "@in-scope-namespaces":
                            inScopeNamespacesSignificant = booleanValue;
                            return;
                        case "namespace-prefixes":
                            namespacePrefixesSignificant = booleanValue;
                            return;
                        case "nilled-property":
                            nilledSignificant = booleanValue;
                            return;
                        case "normalize-space":
                            normalizeSpace = booleanValue;
                            return;
                        case "preserve-space":
                            preserveSpace = booleanValue;
                            return;
                        case "processing-instructions":
                            processingInstructionsSignificant = booleanValue;
                            return;
                        case "text-boundaries":
                            textBoundariesSignificant = booleanValue;
                            return;
                        case "timezones":
                            timezonesSignificant = booleanValue;
                            return;
                        case "type-annotations":
                            typeAnnotationsSignificant = booleanValue;
                            return;
                        case "type-variety":
                            typeVarietySignificant = booleanValue;
                            return;
                        case "typed-values":
                            typedValuesSignificant = booleanValue;
                            return;
                        default:
                            throw new ArgumentException();
                    }
                }
            }
        }

        /*
     * Determine whether two nodes are deep-equal
     * @return null if they are deep equal, or an explanation of the reason if not
     */
        private sealed class NormalizingComparer : IAtomicComparer
        {
            private IAtomicComparer baseComparer;
            private readonly DeepEqualOptions options;

            public IStringCollator Collator => baseComparer.Collator;
            public NormalizingComparer(IAtomicComparer baseComparer, DeepEqualOptions options)
            {
                this.baseComparer = baseComparer;
                this.options = options;
            }

            public IAtomicComparer ProvideContext(IXPathContext context)
            {
                baseComparer = baseComparer.ProvideContext(context);
                return this; // TODO: thread safety?
            }

            public int CompareAtomicValues(AtomicValue v0, AtomicValue v1)
            {
                return baseComparer.CompareAtomicValues(v0, v1);
            }

            public bool ComparesEqual(AtomicValue v0, AtomicValue v1)
            {
                if (v0 is StringValue && v1 is StringValue)
                {
                    UnicodeString u0 = v0.UnicodeStringValue;
                    UnicodeString u1 = v1.UnicodeStringValue;
                    if (options.normalizeSpace)
                    {
                        u0 = Whitespace.CollapseWhitespace(u0);
                        u1 = Whitespace.CollapseWhitespace(u1);
                    }

                    if (options.normalizationForm != null)
                    {
                        try
                        {
                            u0 = StringView.Of(NormalizeUnicode.Normalize(u0.ToString(), options.normalizationForm));
                        }
                        catch (XPathException e)
                        {
                            throw new ArgumentException(e.Message, e);
                        }

                        try
                        {
                            u1 = StringView.Of(NormalizeUnicode.Normalize(u1.ToString(), options.normalizationForm));
                        }
                        catch (XPathException)
                        {
                            throw new ArgumentException();
                        }
                    }

                    return Collator.ComparesEqual(u0, u1);
                }
                else
                {
                    return baseComparer.ComparesEqual(v0, v1);
                }
            }

            public string Save()
            {
                return null;
            }
        }
    }
}