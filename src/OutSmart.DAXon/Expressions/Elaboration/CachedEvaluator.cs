////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Threading;

namespace OutSmart.DAXon.Expressions.Elaboration
{
    /// <summary>
    /// An evaluator elaborated once, for callers that evaluate an expression per item outside
    /// elaborated code (pattern matching, accumulator rules, FLWOR clauses, merge keys). Through the
    /// interpreted EvaluateItem/Iterate, most expressions elaborate themselves anew on every call.
    /// </summary>
    internal sealed class CachedEvaluator<T> where T : class
    {
        private readonly Expression source;
        private readonly T evaluator;

        private CachedEvaluator(Expression source, T evaluator)
        {
            this.source = source;
            this.evaluator = evaluator;
        }

        /// <summary>
        /// The evaluator in the slot, built for <paramref name="expr"/> if the slot is empty or was built
        /// for another expression (an optimizer rewrite replaced it). Racing threads build equal ones.
        /// </summary>
        internal static T Get(ref CachedEvaluator<T> slot, Expression expr, Func<Elaborator, T> build)
        {
            CachedEvaluator<T> cached = Volatile.Read(ref slot);
            if (cached == null || cached.source != expr)
            {
                cached = new CachedEvaluator<T>(expr, build(expr.MakeElaborator()));
                Volatile.Write(ref slot, cached);
            }

            return cached.evaluator;
        }
    }
}
