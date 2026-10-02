////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Internal;

namespace OutSmart.DAXon.Expressions.Elaboration
{
    /// <summary>
    /// Elaborator of an expression nested deeper than <see cref="ProbeFreeDepth"/> in its tree: every
    /// evaluator it returns probes the stack first. The closures of nested instructions call each
    /// other one or more frames per level with no recursion entry in between, so a stylesheet
    /// compiled on a large stack overflowed a small one at run time. Shallow expressions pay nothing.
    /// </summary>
    internal sealed class StackProbingElaborator : Elaborator
    {
        // As the JSON serializer's gate: the unprobed levels above cost tens of KB at most.
        internal const int ProbeFreeDepth = 32;

        private readonly Elaborator inner;

        internal StackProbingElaborator(Elaborator inner)
        {
            this.inner = inner;
        }

        // For callers that need the expression's own elaborator type.
        internal static Elaborator Unwrap(Elaborator elaborator)
        {
            return elaborator is StackProbingElaborator p ? p.inner : elaborator;
        }

        // Where the error points: the deep instruction, not the template that happens to claim it.
        private ILocation Location => inner.GetExpression().GetLocation();

        public override Expression GetExpression() => inner.GetExpression();

        public override void SetExpression(Expression expr) => inner.SetExpression(expr);

        public override ISequenceEvaluator Eagerly()
        {
            StackGuard.ProbeNesting(Location);
            return inner.Eagerly();
        }

        public override ISequenceEvaluator Lazily(bool repeatable, bool lazyEvaluationRequired)
        {
            StackGuard.ProbeNesting(Location);
            return inner.Lazily(repeatable, lazyEvaluationRequired);
        }

        public override IPullEvaluator ElaborateForPull()
        {
            ILocation loc = Location;
            StackGuard.ProbeNesting(loc);   // elaboration recurses through the children too, on the first call
            IPullEvaluator e = inner.ElaborateForPull();
            return (context) =>
            {
                StackGuard.ProbeNesting(loc);
                return e(context);
            };
        }

        public override IPushEvaluator ElaborateForPush()
        {
            ILocation loc = Location;
            StackGuard.ProbeNesting(loc);
            IPushEvaluator e = inner.ElaborateForPush();
            return (output, context) =>
            {
                StackGuard.ProbeNesting(loc);
                return e(output, context);
            };
        }

        public override IItemEvaluator ElaborateForItem()
        {
            ILocation loc = Location;
            StackGuard.ProbeNesting(loc);
            IItemEvaluator e = inner.ElaborateForItem();
            return (context) =>
            {
                StackGuard.ProbeNesting(loc);
                return e(context);
            };
        }

        public override IBooleanEvaluator ElaborateForBoolean()
        {
            ILocation loc = Location;
            StackGuard.ProbeNesting(loc);
            IBooleanEvaluator e = inner.ElaborateForBoolean();
            return (context) =>
            {
                StackGuard.ProbeNesting(loc);
                return e(context);
            };
        }

        public override IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent)
        {
            ILocation loc = Location;
            StackGuard.ProbeNesting(loc);
            IUnicodeStringEvaluator e = inner.ElaborateForUnicodeString(zeroLengthWhenAbsent);
            return (context) =>
            {
                StackGuard.ProbeNesting(loc);
                return e(context);
            };
        }

        public override IStringEvaluator ElaborateForString(bool zeroLengthWhenAbsent)
        {
            ILocation loc = Location;
            StackGuard.ProbeNesting(loc);
            IStringEvaluator e = inner.ElaborateForString(zeroLengthWhenAbsent);
            return (context) =>
            {
                StackGuard.ProbeNesting(loc);
                return e(context);
            };
        }

        public override IUpdateEvaluator ElaborateForUpdate() => inner.ElaborateForUpdate();
    }
}
