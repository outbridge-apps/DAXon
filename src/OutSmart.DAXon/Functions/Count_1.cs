////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{

    // Count_1 (fn:count#1) -- REAL elaborator-free impl ported from the excluded Count.cs:69-74 + SteppingCount:57-66.
    // The real Count.cs is excluded (csproj) AND its class name 'Count' is occupied here by a hollow static-helper
    // stub (not a SystemFunction), so this is registered as a distinct class on the compiled SystemFunction base.
    // GetElaborator() (CountFnElaborator) intentionally omitted (String_1/Tokenize_1 pattern): correctness from Call
    // (interpreter path); the optimizer elaborator is deferred. The grounded fast-path uses IGroundedValue.GetLength();
    // otherwise count by iterating (faithful to the real SteppingCount loop). UO/INS upstream flags are non-correctness.
    internal sealed class Count_1 : SystemFunction
    {
        public Count_1() { }
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            ISequence arg = arguments[0];
            int size;
            if (arg is IGroundedValue)
            {
                size = ((IGroundedValue)arg).GetLength();
            }
            else
            {
                size = CountIterator(arg.Iterate());
            }
            return Int64Value.MakeIntegerValue(size);
        }

        // Java's Count.count(iter): a LAST_POSITION_FINDER iterator yields its length without
        // iterating. This makes count(reverse(X)), count(subsequence(...)) etc. O(1)/O(base-length)
        // instead of walking every item. Byte-identical: GetLength() is the item count.
        internal static int CountIterator(ISequenceIterator it)
        {
            if (it is ILastPositionFinder lpf && lpf.SupportsGetLength())
            {
                return lpf.GetLength();
            }

            if (it is Trees.Iterators.IFastCountable fc && fc.TryFastCount(out int fcount))
            {
                // TinyTree axis iterators count array entries directly - no node objects for
                // count(//*) / count(//text()).
                return fcount;
            }

            int size = 0;
            while (it.Next() != null)
            {
                size++;
            }

            return size;
        }

        public override Expressions.Elaboration.Elaborator GetElaborator()
        {
            return new CountFnElaborator();
        }

        internal sealed class CountFnElaborator : Expressions.Elaboration.ItemElaborator
        {
            public override Expressions.Elaboration.IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                Expression arg = fnc.GetArg(0);
                Expressions.Elaboration.IPullEvaluator puller = arg.MakeElaborator().ElaborateForPull();

                // count(//@*) rooted at a document: the optimizer compiles it to
                // descendant(-or-self)::element()/attribute::*, and every attribute in a TinyTree
                // belongs to that document, so the answer is the tree's attribute total — no
                // iteration, no wrappers. Any other context item runs the generic count.
                if (arg is SlashExpression slash
                    && slash.GetSelectExpression() is AxisExpression selAxis
                    && (selAxis.Axis == AxisInfo.DESCENDANT || selAxis.Axis == AxisInfo.DESCENDANT_OR_SELF)
                    && selAxis.GetNodeTest() is Patterns.NodeKindTest ekt && ekt.PrimitiveType == Types.Type.ELEMENT
                    && slash.GetActionExpression() is AxisExpression actAxis
                    && actAxis.Axis == AxisInfo.ATTRIBUTE
                    && actAxis.GetNodeTest() is Patterns.NodeKindTest akt && akt.PrimitiveType == Types.Type.ATTRIBUTE)
                {
                    return (context) => context.GetContextItem() is Trees.Tiny.TinyDocumentImpl doc
                        ? Int64Value.MakeIntegerValue(doc.tree.numberOfAttributes)
                        : Int64Value.MakeIntegerValue(CountIterator(puller(context)));
                }

                return (context) => Int64Value.MakeIntegerValue(CountIterator(puller(context)));
            }
        }
    }
}
