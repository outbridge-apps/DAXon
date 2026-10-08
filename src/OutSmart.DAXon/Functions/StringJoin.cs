////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Text;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// fn:string-join(string* $sequence, string $separator)
    /// </summary>
    internal sealed class StringJoin : FoldingFunction, IPushableFunction
    {
        private bool returnEmptyIfEmpty;
        public void SetReturnEmptyIfEmpty(bool option)
        {
            returnEmptyIfEmpty = option;
        }

        /// <summary>
        /// Determine the cardinality of the function.
        /// </summary>
        public override int GetCardinality(Expression[] arguments)
        {
            if (returnEmptyIfEmpty)
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }
            else
            {
                return StaticProperty.EXACTLY_ONE;
            }
        }

        public override bool Equals(object o)
        {
            return (o is StringJoin) && base.Equals(o) && returnEmptyIfEmpty == ((StringJoin)o).returnEmptyIfEmpty;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() | (returnEmptyIfEmpty ? 0x05000000 : 0);
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            Expression e2 = base.MakeOptimizedFunctionCall(visitor, contextInfo, arguments);
            if (e2 != null)
            {
                return e2;
            }

            int card = arguments[0].GetCardinality();
            if (!Cardinality.AllowsMany(card))
            {
                if (Cardinality.AllowsZero(card) || arguments[0].GetItemType().GetPrimitiveItemType() != BuiltInAtomicType.STRING)
                {
                    if (returnEmptyIfEmpty)
                    {
                        return new CastExpression(arguments[0], BuiltInAtomicType.STRING, true);
                    }
                    else
                    {
                        return SystemFunction.MakeCall("string", GetRetainedStaticContext(), arguments[0]);
                    }
                }
                else
                {
                    return arguments[0];
                }
            }

            return null;
        }

        public override Elaborator GetElaborator()
        {
            return new StringJoinFnElaborator();
        }

        // Fused row-joiner for `string-join((childA, childB, ...), 'lit')` over an untyped Tiny
        // context item: the operand fingerprints resolve once, each row walks its child array
        // straight into the collector - no per-operand axis/atomizing iterators, no fold ceremony.
        // Any other shape (or an off-path context item at runtime) uses the generic evaluator.
        internal sealed class StringJoinFnElaborator : Expressions.Elaboration.ItemElaborator
        {
            private static bool MatchChildBlock(Expression arg, out int[] fps, out NodeTest[] tests)
            {
                fps = null;
                tests = null;
                Expression e = Expressions.Elaboration.TransparentWrappers.Unwrap(arg,
                    Expressions.Elaboration.Peel.StringConverter | Expressions.Elaboration.Peel.Atomizer);
                if (!(e is Block block))
                {
                    return false;
                }

                Operand[] ops = block.GetOperanda();
                int[] f = new int[ops.Length];
                NodeTest[] t = new NodeTest[ops.Length];
                for (int i = 0; i < ops.Length; i++)
                {
                    Expression child = Expressions.Elaboration.TransparentWrappers.Unwrap(
                        ops[i].GetChildExpression(), Expressions.Elaboration.Peel.Atomizer);
                    if (!Expressions.Elaboration.FusedChildAtomizer.MatchAxis(child, out f[i]))
                    {
                        return false;
                    }

                    t[i] = ((AxisExpression)child).GetNodeTest();
                }

                fps = f;
                tests = t;
                return true;
            }

            // `for $i in RANGE return 'lit'` / `RANGE!'lit'` with a literal folded range and a
            // string-literal body; the separator must be absent/defaulted or a string literal.
            private static bool MatchLiteralRepeat(SystemFunctionCall fnc, out string lit, out string sep, out long n)
            {
                lit = null;
                sep = "";
                n = 0;
                if (fnc.GetArity() >= 2)
                {
                    if (fnc.GetArg(1) is StringLiteral sl)
                    {
                        sep = sl.GroundedValue.GetStringValue();
                    }
                    else if (!(fnc.GetArg(1) is DefaultedArgumentExpression))
                    {
                        return false;
                    }
                }

                Expression seq;
                Expression body;
                if (fnc.GetArg(0) is ForExpression fx)
                {
                    seq = fx.Sequence;
                    body = fx.GetAction();
                }
                else if (fnc.GetArg(0) is Expressions.Instructions.ForEach fe && fe.SeparatorExpression == null)
                {
                    seq = fe.GetSelectExpression();
                    body = fe.GetActionExpression();
                }
                else
                {
                    return false;
                }

                body = Expressions.Elaboration.TransparentWrappers.Unwrap(body,
                    Expressions.Elaboration.Peel.StringConverter | Expressions.Elaboration.Peel.Atomizer);
                if (!(body is StringLiteral bl) || !(seq is Literal rl)
                    || !(rl.GroundedValue is IntegerRange ir) || ir.step != 1)
                {
                    return false;
                }

                lit = bl.GroundedValue.GetStringValue();
                // 1..2^31: IntegerRange construction enforces the XPDY0130 count cap, so neither
                // this subtraction nor the totalLen products below can overflow a long.
                n = ir.end - ir.start + 1;
                return true;
            }

            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                StringJoin fn = (StringJoin)fnc.TargetFunction;
                IItemEvaluator generic = null;
                IItemEvaluator Generic()
                {
                    if (generic == null)
                    {
                        var g = new SystemFunctionCall.SystemFunctionCallElaborator();
                        g.SetExpression(fnc);
                        generic = g.ElaborateForItem();
                    }

                    return generic;
                }

                // string-join over a literal repeated by a literal range — `for $i in (1 to N)
                // return 'lit'` / `(1 to N)!'lit'` (padding generation): the result is fully
                // determined at elaboration time, so it is built by block-doubling copies instead
                // of a million-iteration for/join pipeline. A range literal is never empty, so
                // the empty-sequence semantics of either string-join variant cannot be observed.
                if (MatchLiteralRepeat(fnc, out string repLit, out string repSep, out long repN))
                {
                    long totalLen = repLit.Length * repN + repSep.Length * (repN - 1);
                    if (totalLen <= 64_000_000)
                    {
                        int litLen = repLit.Length;
                        int unitLen = repSep.Length + litLen;
                        int total = (int)totalLen;
                        int reps = unitLen == 0 ? 0 : (int)(repN - 1);   // unit "" repeats to "" — count is moot
                        return (context) =>
                        {
                            if (unitLen == 0 || reps == 0)
                            {
                                return new StringValue(repLit);
                            }

                            char[] buf = new char[total];
                            repLit.CopyTo(0, buf, 0, litLen);
                            repSep.CopyTo(0, buf, litLen, repSep.Length);
                            repLit.CopyTo(0, buf, litLen + repSep.Length, litLen);
                            long done = 1;   // units written after the leading literal
                            while (done < reps)
                            {
                                long chunk = Math.Min(done, reps - done);
                                Array.Copy(buf, litLen, buf, litLen + done * unitLen, chunk * unitLen);
                                done += chunk;
                            }

                            // One step builds up to 64M characters, and a query can end on it: look at the limits after it.
                            StringValue joined = new StringValue(new string(buf));
                            Core.Controller.CheckActiveTimeoutNow();
                            return joined;
                        };
                    }
                }

                if (fn.returnEmptyIfEmpty || fnc.GetArity() != 2
                    || !(fnc.GetArg(1) is StringLiteral sepLit)
                    || !MatchChildBlock(fnc.GetArg(0), out int[] fps, out NodeTest[] _))
                {
                    return Generic();
                }

                UnicodeString separator = sepLit.GroundedValue.Content;
                IItemEvaluator fallback = Generic();
                return (context) =>
                {
                    if (!(context.GetContextItem() is TinyParentNodeImpl tiny) || tiny.tree.TypeArray != null)
                    {
                        return fallback.Eval(context);
                    }

                    TinyTree tree = tiny.tree;
                    int p = tiny.nodeNr;
                    int firstChild = p + 1;
                    bool hasChildren = firstChild < tree.numberOfNodes && tree.depth[firstChild] == tree.depth[p] + 1;
                    UniStringCollector coll = new UniStringCollector();
                    bool first = true;
                    if (hasChildren)
                    {
                        byte[] kinds = tree.nodeKind;
                        int[] nextArr = tree.next;
                        int[] nameCodes = tree.nameCode;
                        foreach (int fp in fps)
                        {
                            int n = firstChild;
                            while (n >= 0)
                            {
                                int cur = n;
                                int n2 = nextArr[cur];
                                n = n2 > cur ? n2 : -1;
                                int k = kinds[cur];
                                if ((k == Types.Type.ELEMENT || k == Types.Type.TEXTUAL_ELEMENT) && (nameCodes[cur] & NamePool.FP_MASK) == fp)
                                {
                                    if (first)
                                    {
                                        first = false;
                                    }
                                    else
                                    {
                                        coll.Accept(separator);
                                    }

                                    coll.Accept(TinyParentNodeImpl.GetStringValue(tree, cur));
                                }
                            }
                        }
                    }

                    return new StringValue(coll.ToUnicodeString());
                };
            }
        }

        public override IFold GetFold(IXPathContext context, params ISequence[] additionalArguments)
        {
            UnicodeString separator = EmptyUnicodeString.GetInstance();
            if (additionalArguments.Length > 0)
            {
                separator = ((IGroundedValue)additionalArguments[0].Head()).UnicodeStringValue;
            }

            return new StringJoinFold(separator, returnEmptyIfEmpty);
        }

        public void Process(Outputter destination, IXPathContext context, ISequence[] arguments)
        {
            UnicodeString separator = arguments.Length > 1 ? ((IGroundedValue)arguments[1].Head()).UnicodeStringValue : EmptyUnicodeString.GetInstance();
            IUniStringConsumer output = destination.GetStringReceiver(false, Loc.NONE);
            output.Open();
            bool first = true;
            ISequenceIterator iter = arguments[0].Iterate();
            IItem it;
            try
            {
                int steps = 0;
                while ((it = iter.Next()) != null)
                {
                    if ((++steps & 1023) == 0)
                    {
                        Core.Controller.CheckActiveTimeout();
                    }

                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        output.Accept(separator);
                    }

                    output.Accept(it.UnicodeStringValue);
                }
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }

            output.Close();
        }

        private sealed class StringJoinFold : IFold
        {
            private int position = 0;
            private readonly UnicodeString separator;
            private readonly UniStringCollector data;
            private readonly bool returnEmptyIfEmpty;
            public StringJoinFold(UnicodeString separator, bool returnEmptyIfEmpty)
            {
                this.separator = separator;
                // Byte (Latin1) collector, not an int[] UnicodeBuilder: a large join (string-join of
                // millions of tokens into one big string) held 4 bytes per codepoint plus a doubling
                // ladder; the collector keeps Latin1 on the byte path and switches to a char buffer only
                // on the first codepoint > 0xFF. Same string value, so byte-identical output.
                this.data = new UniStringCollector();
                this.returnEmptyIfEmpty = returnEmptyIfEmpty;
            }

            public void ProcessItem(IItem item)
            {
                if (position == 0)
                {
                    data.Accept(item.UnicodeStringValue);
                    position = 1;
                }
                else
                {
                    data.Accept(separator).Accept(item.UnicodeStringValue);
                }
            }

            public bool IsFinished()
            {
                return false;
            }

            public ISequence Result()
            {
                if (position == 0 && returnEmptyIfEmpty)
                {
                    return EmptySequence.GetInstance();
                }
                else
                {
                    return new StringValue(data.ToUnicodeString());
                }
            }
        }
    }
}
