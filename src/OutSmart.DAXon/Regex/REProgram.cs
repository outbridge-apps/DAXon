////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
/*
 * Originally part of Apache's Jakarta project (downloaded January 2012),
 * this file has been extensively modified for integration into Saxon by
 * Michael Kay, Saxonica.
 */
using OutSmart.DAXon.Text;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// A class that holds compiled regular expressions.
    /// </summary>
    internal sealed class REProgram
    {
        public const int OPT_HASBACKREFS = 1;
        public const int OPT_HASBOL = 2;
        public Operation operation;
        public REFlags flags;
        public UnicodeString prefix; // Prefix string optimization
        public IIntPredicateProxy initialCharClass;
        public IList<RegexPrecondition> preconditions = new List<RegexPrecondition>();
        public int minimumLength = 0;
        protected int fixedLength = -1;
        public int optimizationFlags; // Optimization flags (REProgram.OPT_*)
        public int maxParens = -1;
        protected int backtrackingLimit = -1;

        // Fast-path shape for a bare character-class pattern or a greedy character-class repeat (e.g.
        // [0-9]+ / \d{2,5}). Computed lazily on the first Match and published through ONE volatile
        // reference to an immutable object: a torn read is impossible (a concurrent reader either sees
        // null and recomputes, or sees a fully-constructed shape), so a shared REProgram is MT-safe.
        // REMatcher runs these as a flat scan+run instead of the backtracking NFA -- the whole pattern is
        // a single class (optionally repeated), so the maximal run is the match, no State/iterator
        // allocation per attempt.
        internal sealed class FastShape
        {
            internal readonly IIntPredicateProxy Pred;
            internal readonly int Min;
            internal readonly int Max;
            internal readonly bool Enabled;   // false = pattern not eligible for the flat executor

            // Anchored two-segment shape ^C1 C2{m,n}$ (single-line): the repeat must run exactly to
            // the end of the string, so the check is a deterministic linear scan — no disjointness
            // condition and no backtracking is possible (a shorter repeat leaves characters before $).
            internal readonly bool Anchored2;
            internal readonly IIntPredicateProxy Pred1;
            internal readonly IIntPredicateProxy Pred2;
            internal readonly int Min2;
            internal readonly int Max2;

            internal FastShape(bool enabled, IIntPredicateProxy pred, int min, int max)
            {
                Enabled = enabled;
                Pred = pred;
                Min = min;
                Max = max;
            }

            internal FastShape(IIntPredicateProxy pred1, IIntPredicateProxy pred2, int min2, int max2)
            {
                Anchored2 = true;
                Pred1 = pred1;
                Pred2 = pred2;
                Min2 = min2;
                Max2 = max2;
            }

            // Unanchored two-group capture shape (C1+)(C2+) with DISJOINT classes (e.g.
            // analyze-string regex="([A-Z]+)([0-9]+)"): a C1 run contains no C2 character, so
            // backtracking the greedy C1 repeat can never enable C2 — determinism comes from the
            // disjointness, not from anchoring.
            internal readonly bool Captures2;

            internal FastShape(IIntPredicateProxy pred1, IIntPredicateProxy pred2)
            {
                Captures2 = true;
                Pred1 = pred1;
                Pred2 = pred2;
            }

            // Unanchored two-segment shape C1 C2{m,n}: C1 consumes exactly one codepoint, so no
            // backtracking exists — an attempt at i either extends into a C2 run of >= min or the
            // scan advances to i+1; no disjointness condition needed. With Seq2Captures the two
            // segments are capture groups 1 and 2 ("([A-Z])([0-9]+)"), else bare ("[A-Z][0-9]+").
            internal readonly bool Seq2;
            internal readonly bool Seq2Captures;

            internal FastShape(IIntPredicateProxy pred1, IIntPredicateProxy pred2, int min2, int max2, bool captures)
            {
                Seq2 = true;
                Seq2Captures = captures;
                Pred1 = pred1;
                Pred2 = pred2;
                Min2 = min2;
                Max2 = max2;
            }
        }

        private static readonly FastShape NotFast = new FastShape(false, null, 0, 0);
        private volatile FastShape fastShape;   // null until computed

        internal FastShape GetFastShape()
        {
            FastShape fs = fastShape;
            if (fs != null)
            {
                return fs;
            }

            FastShape result = NotFast;

            // Only a self-contained class pattern with no capture, no ^-anchor, no backrefs, and no
            // case-folding: every other feature adds an op to the sequence or changes match semantics.
            if (maxParens <= 1
                && (optimizationFlags & (OPT_HASBACKREFS | OPT_HASBOL)) == 0
                && !flags.IsCaseIndependent()
                && !flags.IsLiteral())
            {
                Operation root = operation;
                if (root is OpSequence seq && seq.Operations.Count == 2 && seq.Operations[1] is OpEndProgram)
                {
                    root = seq.Operations[0];
                }

                if (root is OpCharClass cc)
                {
                    result = new FastShape(true, cc.Predicate, 1, 1);
                }
                else if (root is OpRepeat rep && rep.greedy && rep.min >= 1 && rep.op is OpCharClass rcc)
                {
                    // OpUnambiguousRepeat / OpGreedyFixed / OpRepeat over a single class: as the whole
                    // pattern (trailing OpEndProgram accepts anywhere) the greedy longest run IS the match.
                    // min >= 1 excludes '*'/{0,n} whose empty match has separate progress semantics.
                    result = new FastShape(true, rcc.Predicate, rep.min, rep.max);
                }
                else if (root is OpSequence seq2 && seq2.Operations.Count == 3
                    && seq2.Operations[0] is OpCharClass s1
                    && seq2.Operations[1] is OpRepeat s2 && s2.greedy && s2.min >= 1 && s2.op is OpCharClass s2c
                    && seq2.Operations[2] is OpEndProgram)
                {
                    // C1 C2{m,n} (e.g. replace($s, '[A-Z][0-9]+', ...)).
                    result = new FastShape(s1.Predicate, s2c.Predicate, s2.min, s2.max, false);
                }
            }
            else if (maxParens <= 1
                && (optimizationFlags & OPT_HASBACKREFS) == 0
                && (optimizationFlags & OPT_HASBOL) != 0
                && !flags.IsCaseIndependent()
                && !flags.IsLiteral()
                && !flags.IsMultiLine()
                && operation is OpSequence aseq
                && aseq.Operations.Count == 5
                && aseq.Operations[0] is OpBOL
                && aseq.Operations[1] is OpCharClass ac1
                && aseq.Operations[2] is OpRepeat ar2 && ar2.greedy && ar2.min >= 1 && ar2.op is OpCharClass ac2
                && aseq.Operations[3] is OpEOL
                && aseq.Operations[4] is OpEndProgram)
            {
                // ^C1 C2{m,n}$ single-line (e.g. matches($s, '^[A-Z][0-9]{3,}$')).
                result = new FastShape(ac1.Predicate, ac2.Predicate, ar2.min, ar2.max);
            }
            else if (maxParens == 3
                && (optimizationFlags & (OPT_HASBACKREFS | OPT_HASBOL)) == 0
                && !flags.IsCaseIndependent()
                && !flags.IsLiteral()
                && operation is OpSequence cseq
                && cseq.Operations.Count == 3
                && cseq.Operations[0] is OpCapture cp1 && cp1.groupNr == 1
                && cseq.Operations[1] is OpCapture cp2 && cp2.groupNr == 2
                && cseq.Operations[2] is OpEndProgram
                && cp2.childOp is OpRepeat cr2 && cr2.greedy && cr2.min >= 1 && cr2.op is OpCharClass ccl2)
            {
                if (cp1.childOp is OpRepeat cr1 && cr1.greedy && cr1.min == 1 && cr1.max == int.MaxValue && cr1.op is OpCharClass ccl1
                    && cr2.min == 1 && cr2.max == int.MaxValue
                    && ccl1.Predicate is CharClass.ICharacterClass ch1 && ccl2.Predicate is CharClass.ICharacterClass ch2
                    && ch1.IsDisjoint(ch2))
                {
                    // (C1+)(C2+) with disjoint classes, e.g. analyze-string "([A-Z]+)([0-9]+)".
                    result = new FastShape(ccl1.Predicate, ccl2.Predicate);
                }
                else if (cp1.childOp is OpCharClass single)
                {
                    // (C1)(C2{m,n}), e.g. replace($s, '([A-Z])([0-9]+)', '$2-$1').
                    result = new FastShape(single.Predicate, ccl2.Predicate, cr2.min, cr2.max, true);
                }
            }

            fastShape = result;   // volatile write publishes the fully-constructed shape
            return result;
        }

        public int BacktrackingLimit
        {
            get => backtrackingLimit; set
            {
                this.backtrackingLimit = value;
            }
        }
        public REProgram(Operation operation, int parens, REFlags flags)
        {
            this.flags = flags;
            SetOperation(operation);
            this.maxParens = parens;
        }

        private void SetOperation(Operation operation)
        {

            // Save reference to instruction array
            this.operation = operation;

            // Initialize other program-related variables
            this.optimizationFlags = 0;
            this.prefix = null;
            this.operation = operation.Optimize(this, flags);

            // Try various compile-time optimizations
            if (operation is OpSequence)
            {
                Operation first = ((OpSequence)operation).Operations[0];
                if (first is OpBOL)
                {
                    optimizationFlags |= REProgram.OPT_HASBOL;
                }
                else if (first is OpAtom)
                {
                    prefix = ((OpAtom)first).Atom;
                }
                else if (first is OpCharClass)
                {
                    initialCharClass = ((OpCharClass)first).Predicate;
                }

                AddPrecondition(operation, -1, 0);
            }

            minimumLength = operation.MinimumMatchLength;
            fixedLength = operation.MatchLength;
        }

        private void AddPrecondition(Operation op, int fixedPosition, int minPosition)
        {
            if (op is OpAtom || op is OpCharClass)
            {
                preconditions.Add(new RegexPrecondition(op, fixedPosition, minPosition));
            }
            else if (op is OpRepeat && ((OpRepeat)op).min >= 1)
            {
                OpRepeat parent = (OpRepeat)op;
                Operation child = parent.op;
                if (child is OpAtom || child is OpCharClass)
                {
                    if (parent.min == 1)
                    {
                        preconditions.Add(new RegexPrecondition(parent, fixedPosition, minPosition));
                    }
                    else
                    {
                        OpRepeat parent2 = new OpRepeat(child, parent.min, parent.min, true);
                        preconditions.Add(new RegexPrecondition(parent2, fixedPosition, minPosition));
                    }
                }
                else
                {
                    AddPrecondition(child, fixedPosition, minPosition);
                }
            }
            else if (op is OpCapture)
            {
                AddPrecondition(((OpCapture)op).childOp, fixedPosition, minPosition);
            }
            else if (op is OpSequence)
            {
                int fp = fixedPosition;
                int mp = minPosition;
                foreach (Operation o in ((OpSequence)op).Operations)
                {
                    if (o is OpBOL)
                    {
                        fp = 0;
                    }

                    AddPrecondition(o, fp, mp);
                    if (fp != -1 && o.MatchLength != -1)
                    {
                        fp += o.MatchLength;
                    }
                    else
                    {
                        fp = -1;
                    }

                    mp += o.MinimumMatchLength;
                }
            }
        }

        public bool IsNullable()
        {
            int m = operation.MatchesEmptyString();
            return (m & Operation.MATCHES_ZLS_ANYWHERE) != 0;
        }
    }
}