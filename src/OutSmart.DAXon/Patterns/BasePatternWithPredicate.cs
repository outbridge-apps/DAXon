////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.stream.Streamability;
//import com.saxonica.ee.stream.Sweep;
//import com.saxonica.ee.trans.ContextItemStaticInfoEE;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Patterns
{
    /// <summary>
    /// Class for handling patterns with simple non-positional boolean predicates
    /// </summary>
    internal sealed class BasePatternWithPredicate : Pattern, IPatternWithPredicate
    {
        Operand basePatternOp;
        Operand predicateOp;
        volatile IBooleanEvaluator predicateEvaluator;   // volatile: published once, read per node match (round 11)

        public Expression Predicate => predicateOp.GetChildExpression();

        public Pattern BasePattern => (Pattern)basePatternOp.GetChildExpression();

        //try {
        public override int Fingerprint => BasePattern.Fingerprint;

        //try {
        public override int Dependencies => Predicate.Dependencies;
        public BasePatternWithPredicate(Pattern basePattern, Expression predicate)
        {
            basePatternOp = new Operand(this, basePattern, OperandRole.ATOMIC_SEQUENCE);
            predicateOp = new Operand(this, predicate, OperandRole.FOCUS_CONTROLLED_ACTION);
            if (basePattern is ItemTypePattern)
            {

                // TODO: this is a pragmatic approximation to the actual rules
                SetPriority(basePattern.DefaultPriority - 1E-12);
            }

            AdoptChildExpression(BasePattern);
            AdoptChildExpression(Predicate);
        }

        public override void BindCurrent(ILocalBinding binding)
        {
            Expression predicate = Predicate;
            if (predicate.IsCallOn(typeof(Current)))
            {
                predicateOp.SetChildExpression(new LocalVariableReference(binding));
            }
            else if (ExpressionTool.CallsFunction(predicate, Current.FN_CURRENT, false))
            {
                ReplaceCurrent(predicate, binding);
            }

            BasePattern.BindCurrent(binding);
        }

        public override bool MatchesCurrentGroup()
        {
            return BasePattern.MatchesCurrentGroup();
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(basePatternOp, predicateOp);
        }

        public override int AllocateSlots(SlotManager slotManager, int nextFree)
        {
            int n = ExpressionTool.AllocateSlots(Predicate, nextFree, slotManager);
            return BasePattern.AllocateSlots(slotManager, n);
        }

        public override bool Matches(IItem item, IXPathContext context)
        {

            if (!BasePattern.Matches(item, context))
            {
                return false;
            }

            return MatchesPredicate(item, context);
        }

        // Reusable single-item focus for predicate probing. Pattern objects are SHARED across
        // concurrent transforms, so this scratch is [ThreadStatic] (per-thread, never shared) and
        // guarded by a busy flag: a predicate whose own evaluation re-enters pattern matching gets a
        // fresh iterator instead of clobbering the outer one. Saves a ManualIterator + a `()=>1`
        // closure per predicate attempt (~1.5M on a template-dispatch pass over columns of nodes).
        [ThreadStatic] private static ManualIterator _scratchIter;
        [ThreadStatic] private static bool _scratchBusy;
        private static readonly Func<int> ONE = () => 1;

        // Fused predicate evaluation (W3). A template-rule predicate over an untyped Tiny context node,
        // in one of a few very common shapes, is evaluated per probe (~1.5M on the EDI dispatch shape)
        // through the generic pipeline: atomize the context item (Atomizer iterator), box an
        // xs:untypedAtomic, coerce it, and run the operation. The fused path reads the node's string
        // value straight from the Tiny arrays and runs the operation inline, byte-identically. Any other
        // shape, a schema-typed tree, or a non-Tiny context item falls through to the generic evaluator.
        //
        //   A. [number(.) op numlit]           -> StringToDouble11 (the routine fn:number uses; NaN on a
        //                                          non-lexical string, number('')=NaN) + IEEE compare.
        //                                          Collation-independent, so unconditionally fusable.
        //   B. [. eq/ne 'lit']  (codepoint)    -> ordinal System.String equality.
        //   C. [starts-with(.,'lit')] / [contains(.,'lit')]  (codepoint)
        //                                       -> ordinal System.String StartsWith / Contains.
        //
        // Forms B and C only fuse under the default codepoint collation (a non-codepoint collation ->
        // generic path): equal code-point sequences are equal code-unit sequences, so ordinal
        // System.String operations reproduce CodepointCollator's boolean result exactly, matching what
        // the generic ValueComparison / ContainsFnElaborator do for codepoint collation. IEEE compare in
        // A reproduces ValueComparison.Compare's op:numeric-* / NaN semantics exactly (NaN op y false for
        // every ordered/eq operator, true only for ne).
        //
        // The recognised shape is immutable and published through a single volatile reference (a
        // reference write is atomic; the volatile release/acquire pair guarantees a reader that sees
        // the non-null reference sees a fully-constructed object). Two threads may both recognise it
        // on the first probes -- they produce equal shapes, so the benign race is harmless, matching
        // the existing lazy predicateEvaluator.
        private enum FusedKind { Numeric, StrEq, StrNe, StartsWith, Contains }

        private sealed class FusedPredicate
        {
            internal readonly FusedKind Kind;
            internal readonly int Op;          // Numeric only: Token.FEQ..FLE
            internal readonly double NumLit;    // Numeric only: rhs numeric literal promoted to xs:double
            internal readonly UnicodeString StrLit;  // string forms: the literal operand (compared as-is, no System.String)
            internal FusedPredicate(FusedKind kind, int op, double numLit, UnicodeString strLit)
            {
                Kind = kind;
                Op = op;
                NumLit = numLit;
                StrLit = strLit;
            }
        }

        private static readonly object NOT_FUSABLE = new object();
        private volatile object fusedShape;   // null = not yet recognised

        private object RecognizeFusedShape()
        {
            Expression pred = Predicate;
            if (pred is ValueComparison vc)
            {
                int op = vc.SingletonOperator;
                if (op >= Token.FEQ && op <= Token.FLE)
                {
                    Expression lhs = vc.GetLhsExpression();
                    Expression rhs = vc.GetRhsExpression();

                    // A. [number(.) op numlit]
                    if (IsNumberOfContextItem(lhs) && rhs is Literal nlit && nlit.GroundedValue is NumericValue nv)
                    {
                        return new FusedPredicate(FusedKind.Numeric, op, nv.GetDoubleValue(), null);
                    }

                    // B. [. eq/ne 'lit'] under the default codepoint collation
                    if ((op == Token.FEQ || op == Token.FNE)
                        && vc.StringCollator == CodepointCollator.GetInstance()
                        && IsStringOfContextItem(lhs)
                        && rhs is Literal slit && slit.GroundedValue is StringValue sv && !(sv is AnyURIValue))
                    {
                        return new FusedPredicate(op == Token.FEQ ? FusedKind.StrEq : FusedKind.StrNe, op, 0, sv.UnicodeStringValue);
                    }
                }

                return NOT_FUSABLE;
            }

            // C. [starts-with(.,'lit')] / [contains(.,'lit')] under the default codepoint collation.
            // IsCallOn resolves through the optimizer's Optimized wrapper (target function is unchanged).
            FusedKind ckind;
            if (pred.IsCallOn(typeof(StartsWith)))
            {
                ckind = FusedKind.StartsWith;
            }
            else if (pred.IsCallOn(typeof(Contains)))
            {
                ckind = FusedKind.Contains;
            }
            else
            {
                return NOT_FUSABLE;
            }

            SystemFunctionCall fnc = (SystemFunctionCall)pred;
            if (((CollatingFunctionFixed)fnc.TargetFunction).StringCollator == CodepointCollator.GetInstance()
                && IsStringOfContextItem(fnc.GetArg(0))
                && fnc.GetArg(1) is Literal clit && clit.GroundedValue is StringValue csv && !(csv is AnyURIValue))
            {
                return new FusedPredicate(ckind, 0, 0, csv.UnicodeStringValue);
            }

            return NOT_FUSABLE;
        }

        // fn:number(atomize(context-item)): a SystemFunctionCall on Number_1 whose sole argument
        // atomizes the context item.
        private static bool IsNumberOfContextItem(Expression e)
        {
            if (!e.IsCallOn(typeof(Number_1)))
            {
                return false;
            }

            return UnwrapsToAtomizeContextItem(((SystemFunctionCall)e).GetArg(0));
        }

        // An expression that yields the context node's xs:string value: cast(atomize(dot) as xs:string)
        // for `. = 'lit'`, or convert(atomize(dot)) for starts-with/contains. The xs:string item-type
        // check guarantees the coercion target is string (never untypedAtomic-vs-untypedAtomic etc.).
        private static bool IsStringOfContextItem(Expression e)
        {
            return BuiltInAtomicType.STRING.Equals(e.GetItemType()) && UnwrapsToAtomizeContextItem(e);
        }

        private static bool UnwrapsToAtomizeContextItem(Expression e)
        {
            if (e is CastExpression cast)
            {
                e = cast.BaseExpression;
            }
            else if (e is AtomicSequenceConverter conv)   // covers UntypedSequenceConverter (subclass)
            {
                e = conv.BaseExpression;
            }

            if (e is Atomizer atom)
            {
                e = atom.BaseExpression;
            }

            return e is ContextItemExpression;
        }

        private static bool CompareFusedNumeric(double d, int op, double lit)
        {
            // Mirrors ValueComparison.Compare: for NaN every operator is false except ne. Plain IEEE
            // comparison already yields exactly this (NaN == x is false, NaN != x is true, NaN </<=/>/>=
            // x is false), so no special-casing is needed -- the switch is written straight.
            switch (op)
            {
                case Token.FEQ: return d == lit;
                case Token.FNE: return d != lit;
                case Token.FGT: return d > lit;
                case Token.FLT: return d < lit;
                case Token.FGE: return d >= lit;
                case Token.FLE: return d <= lit;
                default: return false;   // unreachable: op guarded to FEQ..FLE in RecognizeFusedShape
            }
        }

        // Mirror StartsWith.StartsWithFn / Contains.ContainsFn under the codepoint collation: an empty
        // search string always matches; otherwise scan the node's UnicodeString directly (no System.String).
        private static bool StartsWithFused(UnicodeString s, UnicodeString lit)
        {
            if (lit.IsEmpty())
            {
                return true;
            }

            return !s.IsEmpty() && s.HasSubstring(lit, 0);
        }

        private static bool ContainsFused(UnicodeString s, UnicodeString lit)
        {
            if (lit.IsEmpty())
            {
                return true;
            }

            return !s.IsEmpty() && s.IndexOf(lit, 0) >= 0;
        }

        private bool MatchesPredicate(IItem item, IXPathContext context)
        {
            object shape = fusedShape;
            if (shape == null)
            {
                shape = RecognizeFusedShape();
                fusedShape = shape;
            }

            if (shape != NOT_FUSABLE
                && item is TinyParentNodeImpl tiny && tiny.tree.TypeArray == null)
            {
                FusedPredicate fp = (FusedPredicate)shape;
                UnicodeString us = TinyParentNodeImpl.GetStringValue(tiny.tree, tiny.nodeNr);
                if (fp.Kind == FusedKind.Numeric)
                {
                    double d = StringToDouble11.GetInstance().TryStringToNumber(us, out double parsed) ? parsed : double.NaN;
                    return CompareFusedNumeric(d, fp.Op, fp.NumLit);
                }

                // Compare on the UnicodeString directly -- these are exactly CodepointCollator's operations
                // (ComparesEqual/StartsWith/Contains), so byte-identical under codepoint collation, and
                // without materialising a System.String per probe (~1.8M/transform saved on the EDI shape).
                switch (fp.Kind)
                {
                    case FusedKind.StrEq: return us.Equals(fp.StrLit);
                    case FusedKind.StrNe: return !us.Equals(fp.StrLit);
                    case FusedKind.StartsWith: return StartsWithFused(us, fp.StrLit);
                    case FusedKind.Contains: return ContainsFused(us, fp.StrLit);
                }
            }

            if (predicateEvaluator == null)
            {
                predicateEvaluator = Predicate.MakeElaborator().ElaborateForBoolean();
            }

            IXPathContext c2 = context.NewMinorContext();
            bool useScratch = !_scratchBusy;
            ManualIterator si;
            if (useScratch)
            {
                _scratchBusy = true;
                si = _scratchIter ?? (_scratchIter = new ManualIterator());
                si.SetContextItem(item);
                si.SetPosition(1);
                si.SetLengthFinder(ONE);
            }
            else
            {
                si = new ManualIterator(item);
            }

            c2.SetCurrentIterator(si);
            c2.CurrentOutputUri = null;

            // NOTE: Java wrapped Eval in catch(XPathException)->handleDynamicError->false; the port
            // deliberately lets it propagate (matches current QT3 behaviour) — do not reinstate.
            try
            {
                return predicateEvaluator.Eval(c2);
            }
            finally
            {
                if (useScratch)
                {
                    // Release the context item: the scratch iterator is [ThreadStatic] and nothing
                    // resets it between runs, so a parked item kept its whole tree reachable from
                    // every pool thread that ever matched a predicate pattern - one input document
                    // retained per thread, for as long as the thread lives (round AX: 78.6 MB held
                    // across 8 threads after the host had dropped everything).
                    si.SetContextItem(null);
                    _scratchBusy = false;
                }
            }
        }

        //try {
        public override bool MatchesBeneathAnchor(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {
            return BasePattern.MatchesBeneathAnchor(node, anchor, context) && MatchesPredicate(node, context);
        }

        //try {
        public override UType GetUType()
        {
            return BasePattern.GetUType();
        }

        //try {
        public override ItemType GetItemType()
        {
            return BasePattern.GetItemType();
        }

        //try {
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            basePatternOp.SetChildExpression(BasePattern.TypeCheck(visitor, contextItemType));
            ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(BasePattern.GetItemType(), false);
            predicateOp.SetChildExpression(Predicate.TypeCheck(visitor, cit));
            return this;
        }

        //try {
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            basePatternOp.SetChildExpression(BasePattern.Optimize(visitor, contextInfo));
            ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(BasePattern.GetItemType(), false);
            predicateOp.SetChildExpression(Predicate.Optimize(visitor, cit));
            predicateOp.SetChildExpression(visitor.ObtainOptimizer().EliminateCommonSubexpressions(Predicate));
            return this;
        }

        //try {
        public override Pattern ConvertToTypedPattern(string val)
        {
            Pattern b2 = BasePattern.ConvertToTypedPattern(val);
            if (b2 == BasePattern)
            {
                return this;
            }
            else
            {
                return new BasePatternWithPredicate(b2, Predicate);
            }
        }

        //try {
        public override string Reconstruct()
        {
            return BasePattern + "[" + Predicate + "]";
        }

        //try {
        public override string ToShortString()
        {
            return BasePattern.ToShortString() + "[" + Predicate.ToShortString() + "]";
        }

        //try {
        public override Expression Copy(RebindingMap rebindings)
        {
            BasePatternWithPredicate n = new BasePatternWithPredicate((Pattern)BasePattern.Copy(rebindings), Predicate.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;

            return n;
        }

        //try {
        public override bool Equals(object obj)
        {
            return obj is BasePatternWithPredicate && ((BasePatternWithPredicate)obj).BasePattern.IsEqual(BasePattern) && ((BasePatternWithPredicate)obj).Predicate.IsEqual(Predicate);
        }

        //try {
        protected override int ComputeHashCode()
        {
            return BasePattern.GetHashCode() ^ Predicate.GetHashCode();
        }

        //try {
        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("p.withPredicate");
            BasePattern.Export(presenter);
            Predicate.Export(presenter);
            presenter.EndElement();
        }
    }
}
