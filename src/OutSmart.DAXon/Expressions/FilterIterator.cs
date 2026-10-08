////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// PHASE-7-EXCLUDED-CLASS-STUBS-2-BLOCK
namespace OutSmart.DAXon.Expressions
{
    using OutSmart.DAXon.Model;
    internal static class FilterIterator
    {

        // Ported from Saxon FilterIterator.testPredicateValue: evaluate a predicate value against the
        // current position. A node-set predicate is an EBV existence test (non-empty => keep). A numeric
        // predicate is positional (value == position). Boolean/string use EBV. (The all-false hollow stub
        // silently filtered out every item on the non-boolean predicate path, e.g. the descendant::y[parent::x]
        // form produced by the //x/y rewrite, so any such path expression returned empty.)
        public static bool TestPredicateValue(object iter, int pos)
        {
            ISequenceIterator iterator = (ISequenceIterator)iter;
            IItem first = iterator.Next();
            if (first == null)
            {
                return false;
            }

            if (first is NodeInfo)
            {
                iterator.Dispose();
                return true;
            }

            if (first is OutSmart.DAXon.Values.BooleanValue bv)
            {
                if (iterator.Next() != null) { iterator.Dispose(); OutSmart.DAXon.Expressions.Parsing.ExpressionTool.EbvError("a sequence of two or more items starting with a boolean value"); }
                iterator.Dispose();
                return bv.GetBooleanValue();
            }

            if (first is OutSmart.DAXon.Values.StringValue sv)
            {
                if (iterator.Next() != null) { iterator.Dispose(); OutSmart.DAXon.Expressions.Parsing.ExpressionTool.EbvError("a sequence of two or more items starting with a string value"); }
                return !sv.IsEmpty();
            }

            if (first is OutSmart.DAXon.Values.NumericValue nv)
            {
                if (iterator.Next() != null) { iterator.Dispose(); OutSmart.DAXon.Expressions.Parsing.ExpressionTool.EbvError("a sequence of two or more items starting with a numeric value"); }
                return nv.CompareTo((long)pos) == 0;
            }

            iterator.Dispose();
            OutSmart.DAXon.Expressions.Parsing.ExpressionTool.EbvError("a sequence starting with an atomic value that is not a boolean, string, or number");
            return false;
        }

    }
}
