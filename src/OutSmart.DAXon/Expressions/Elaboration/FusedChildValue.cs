////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions.Elaboration
{
    /// <summary>
    /// Fused evaluator for the two select shapes the XSLT compiler emits for text value
    /// templates over a child element - string-join(convert(data(child::NAME)), ' ') for
    /// {NAME} and normalize-space(check?(convert(data(child::NAME)))) for
    /// {normalize-space(NAME)}. The generic pipeline builds an axis iterator, an atomizing
    /// iterator, a converting iterator and an untypedAtomic box per evaluation just to
    /// produce the child's string value; this reads it straight off the TinyTree arrays.
    /// Anything off the fast path (non-Tiny item, annotated tree, two or more matching
    /// children) falls back to the generic evaluator, so join order, XPTY0004 diagnostics
    /// and typed-data semantics are untouched.
    /// </summary>
    internal static class FusedChildValue
    {
        /// <summary>
        /// Returns a fused evaluator for a recognized select shape, or null to use the
        /// generic elaboration.
        /// </summary>
        internal static IUnicodeStringEvaluator TryFuse(Expression select)
        {
            if (!Match(select, out int fp, out bool normalize))
            {
                return null;
            }

            IUnicodeStringEvaluator generic = select.MakeElaborator().ElaborateForUnicodeString(true);
            return (context) => Read(context, fp, normalize) ?? generic(context);
        }

        /// <summary>
        /// String-typed variant for consumers that store plain strings (attribute values).
        /// </summary>
        internal static IStringEvaluator TryFuseString(Expression select)
        {
            if (!Match(select, out int fp, out bool normalize))
            {
                return null;
            }

            IStringEvaluator generic = select.MakeElaborator().ElaborateForString(true);
            return (context) =>
            {
                UnicodeString value = Read(context, fp, normalize);
                return value != null ? value.ToString() : generic(context);
            };
        }

        private static bool Match(Expression select, out int fp, out bool normalize)
        {
            fp = -1;
            normalize = false;
            Expression core;
            if (!(select is SystemFunctionCall sfc))
            {
                return false;
            }

            if (sfc.TargetFunction is StringJoin && sfc.GetArity() == 2
                && sfc.GetArg(1) is Literal sep
                && sep.GroundedValue is StringValue sepValue
                && sepValue.UnicodeStringValue.ToString() == " ")
            {
                core = sfc.GetArg(0);
            }
            else if (sfc.TargetFunction is NormalizeSpace_1 && sfc.GetArity() == 1)
            {
                normalize = true;
                core = sfc.GetArg(0);
            }
            else
            {
                return false;
            }

            // the normalize-space arg carries a cardinality check (>1 child = XPTY0004);
            // the fused path never sees that case - two or more matches fall back
            if (core is CardinalityChecker cc)
            {
                core = cc.BaseExpression;
            }

            if (!(core is AtomicSequenceConverter conv) || !BuiltInAtomicType.STRING.Equals(conv.RequiredItemType))
            {
                return false;
            }

            if (!(conv.BaseExpression is Atomizer atom))
            {
                return false;
            }

            if (!(atom.BaseExpression is AxisExpression axis) || axis.Axis != AxisInfo.CHILD)
            {
                return false;
            }

            if (!(axis.GetNodeTest() is NameTest nameTest) || nameTest.PrimitiveType != Types.Type.ELEMENT)
            {
                return false;
            }

            fp = nameTest.Fingerprint;
            return true;
        }

        // The fused read; null means the generic evaluator must run (non-Tiny item,
        // annotated tree, or two matching children - join/error semantics stay generic).
        private static UnicodeString Read(IXPathContext context, int fp, bool normalize)
        {
            // TinyAttributeImpl aliases nodeNr into the attribute arrays, so only
            // parent nodes (element/document) may take the array walk
            if (!(context.GetContextItem() is TinyParentNodeImpl tiny) || tiny.tree.TypeArray != null)
            {
                return null;
            }

            TinyTree tree = tiny.tree;
            int p = tiny.nodeNr;
            byte[] kinds = tree.nodeKind;
            short[] depths = tree.depth;
            int first = -1;
            int child = p + 1;
            if (child < tree.numberOfNodes && depths[child] == depths[p] + 1)
            {
                // sibling chain: next[] links siblings (PARENT_POINTER pseudo-nodes
                // included), a backwards jump is the owner pointer = end
                int[] nextArr = tree.next;
                int[] nameCodes = tree.nameCode;
                int n = child;
                while (true)
                {
                    int k = kinds[n];
                    if ((k == Types.Type.ELEMENT || k == Types.Type.TEXTUAL_ELEMENT) && (nameCodes[n] & NamePool.FP_MASK) == fp)
                    {
                        if (first >= 0)
                        {
                            return null;
                        }

                        first = n;
                    }

                    int n2 = nextArr[n];
                    if (n2 <= n)
                    {
                        break;
                    }

                    n = n2;
                }
            }

            if (first < 0)
            {
                return EmptyUnicodeString.GetInstance();
            }

            UnicodeString value = TinyParentNodeImpl.GetStringValue(tree, first);
            return normalize ? NormalizeSpace_1.NormalizeSpace(value) : value;
        }
    }
}
