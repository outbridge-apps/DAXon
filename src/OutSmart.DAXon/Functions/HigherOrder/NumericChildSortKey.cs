////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// Direct evaluator for the common fn:sort key shape function($x){number($x/CHILD)}: an
    /// untyped-tree child scan plus the same StringToDouble11 primitive number() uses, replacing
    /// the per-item dynamic call (clean context, stack frame, Slash pull with docOrder wrapper,
    /// SingletonAtomizer). TryExtract returns null for any item it cannot prove equivalent for
    /// (non-tiny/typed node, two matching children) — the caller must then evaluate the real key
    /// function so cardinality/type errors keep their exact code and message.
    /// </summary>
    internal sealed class NumericChildSortKey
    {
        private readonly int childFp;

        private NumericChildSortKey(int childFp)
        {
            this.childFp = childFp;
        }

        public static NumericChildSortKey TryMake(IFunctionItem key)
        {
            // The bare BoundUserFunction is what fn:sort receives when the inline function's type
            // is already a subtype of function(item()) as xs:anyAtomicType* (no CoercedFunction).
            UserFunctionReference.BoundUserFunction bound =
                key as UserFunctionReference.BoundUserFunction
                ?? (key as CoercedFunction)?.TargetFunction as UserFunctionReference.BoundUserFunction;
            if (bound == null || !(bound.TargetFunction is UserFunction uf)
                || uf.GetType() != typeof(UserFunction) || uf.GetArity() != 1)
            {
                return null;
            }

            if (!(uf.GetBody() is SystemFunctionCall call) || !(call.TargetFunction is Number_1)
                || !(call.GetArg(0) is SingletonAtomizer atomizer))
            {
                return null;
            }

            Expression path = atomizer.BaseExpression;
            if (path is DocumentSorter sorter)
            {
                path = sorter.BaseExpression;
            }

            if (!(path is SlashExpression slash))
            {
                return null;
            }

            Expression start = slash.GetLhsExpression();
            // item()/node() checks are subsumed: the fast path only accepts TinyParentNodeImpl,
            // and every other item falls back to the real function where the checker still runs.
            if (start is ItemChecker checker
                && (checker.GetRequiredType() is AnyItemType || checker.GetRequiredType() is AnyNodeTest))
            {
                start = checker.BaseExpression;
            }

            if (!(start is LocalVariableReference varRef)
                || !ReferenceEquals(varRef.GetBinding(), uf.GetParameterDefinitions()[0]))
            {
                return null;
            }

            int fp = TinyText.ElementStep(slash.GetRhsExpression(), AxisInfo.CHILD);
            return fp < 0 ? null : new NumericChildSortKey(fp);
        }

        public DoubleValue TryExtract(IItem item)
        {
            if (!(item is TinyParentNodeImpl parent) || parent.tree.TypeArray != null)
            {
                return null;
            }

            // the one child named childFp; two are SingletonAtomizer's error, not ours
            TinyTree tree = parent.tree;
            int n = TinyText.FirstChild(tree, parent.nodeNr);
            int found = TinyText.NextChildNamed(tree, childFp, ref n);
            if (found >= 0 && TinyText.NextChildNamed(tree, childFp, ref n) >= 0)
            {
                return null;
            }

            if (found < 0)
            {
                return DoubleValue.NaN;
            }

            IConversionResult cr = StringToDouble11.GetInstance()
                .ConvertString(TinyParentNodeImpl.GetStringValue(tree, found));
            return cr is ValidationFailure ? DoubleValue.NaN : (DoubleValue)cr;
        }
    }
}
