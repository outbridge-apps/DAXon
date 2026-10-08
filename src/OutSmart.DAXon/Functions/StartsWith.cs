////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the fn:starts-with() function, with the collation already known
    /// </summary>
    internal sealed class StartsWith : CollatingFunctionFixed
    {
        public override bool IsSubstringMatchingFunction()
        {
            return true;
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            if (StringCollator == CodepointCollator.GetInstance())
            {

                // Performance fast path: bug 3209
                return new AnonymousOptimized(this, arguments);
            }
            else
            {
                return base.MakeOptimizedFunctionCall(visitor, contextInfo, arguments);
            }
        }

        public static bool StartsWithFn(UnicodeString arg0, UnicodeString arg1, ISubstringMatcher collator)
        {
            if (arg1 == null || arg1.IsEmpty() || collator.IsEqualToEmpty(arg1))
            {
                return true;
            }

            if (arg0 == null || arg0.IsEmpty())
            {
                return false;
            }

            return collator.StartsWith(arg0, arg1);
        }

        /// <summary>
        /// Evaluate the function
        /// </summary>
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            UnicodeString s0 = GetUniStringArg(arguments[0]);
            UnicodeString s1 = GetUniStringArg(arguments[1]);
            return BooleanValue.Get(StartsWithFn(s0, s1, (ISubstringMatcher)StringCollator));
        }

        private sealed class AnonymousOptimized : SystemFunctionCall.Optimized
        {

            private readonly StartsWith parent;
            public AnonymousOptimized(StartsWith parent, Expression[] arguments) : base(parent, arguments)
            {
                this.parent = parent;
            }
            public override bool EffectiveBooleanValue(IXPathContext context)
            {
                string s0 = GetArg(0).EvaluateAsString(context).ToString();
                string s1 = GetArg(1).EvaluateAsString(context).ToString();
                return s0.StartsWith(s1, StringComparison.Ordinal);
            }
        }
    }
}