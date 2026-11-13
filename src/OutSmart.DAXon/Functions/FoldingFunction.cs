////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
namespace OutSmart.DAXon.Functions
{
    internal abstract class FoldingFunction : SystemFunction
    {

        public override string StreamerName => "IFold";
        public abstract IFold GetFold(IXPathContext context, params ISequence[] additionalArguments);
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return RunFold(GetFold(context, TailArguments(arguments)), arguments[0].Iterate());
        }

        protected static ISequence[] TailArguments(ISequence[] arguments)
        {
            ISequence[] additionalArgs = new ISequence[arguments.Length - 1];
            Array.Copy(arguments, 1, additionalArgs, 0, additionalArgs.Length);
            return additionalArgs;
        }


        // A fold that takes the double an untypedAtomic value converts to, computed by the caller, as that value.
        internal interface IUntypedDoubleFold : IFold
        {
            void ProcessUntypedDouble(double d);
        }

        // The fold of data(NODES) node by node: a node of an untyped tree whose text is a plain number goes in as its
        // double, with no untypedAtomic value, string or converted double made for it; any other item atomizes as the
        // Atomizer would. stopWhenFinished: end at the fold's IsFinished, as its generic path does. Null when the argument is
        // not data() of nodes.
        internal static IItemEvaluator TryNodeFold(SystemFunctionCall fnc, Func<IXPathContext, IUntypedDoubleFold> makeFold, bool stopWhenFinished)
        {
            if (!(fnc.GetArg(0) is Atomizer atom) || !(atom.BaseExpression.GetItemType() is Patterns.NodeTest))
            {
                return null;
            }

            IPullEvaluator nodes = atom.BaseExpression.MakeElaborator().ElaborateForPull();
            int fp = Trees.Tiny.TinyText.DescendantName(atom.BaseExpression);
            int childFp = Trees.Tiny.TinyText.ChildName(atom.BaseExpression, out Expression start);
            IPullEvaluator starts = childFp >= 0 ? start.MakeElaborator().ElaborateForPull() : null;
            int startFp = childFp >= 0 ? Trees.Tiny.TinyText.DescendantName(start) : -1;
            return (context) =>
            {
                IUntypedDoubleFold fold = makeFold(context);
                int steps = 0;
                try
                {
                    // NODES/NAME: the children by number; starts the walk cannot take go the general way with a new fold.
                    if (childFp >= 0)
                    {
                        IUntypedDoubleFold f = fold;
                        if (Trees.Tiny.TinyText.WalkChildren(context, starts, startFp, childFp, (tree, c) =>
                        {
                            if (Trees.Tiny.TinyText.TrySpan(tree, c, out byte[] cb, out int coff, out int clen) && Trees.Tiny.TinyText.TryPlainDouble(cb, coff, clen, out double cd))
                            {
                                f.ProcessUntypedDouble(cd);
                            }
                            else
                            {
                                Atomize(tree.GetNode(c), f);
                            }

                            return !(stopWhenFinished && f.IsFinished());
                        }))
                        {
                            return fold.Result().Head();
                        }

                        fold = makeFold(context);
                    }

                    // descendant::NAME of a Tiny node: the nodes by number, a node object only for one off the fast path.
                    if (fp >= 0 && context.GetContextItem() is Trees.Tiny.TinyParentNodeImpl top && top.tree.TypeArray == null)
                    {
                        Trees.Tiny.TinyTree tree = top.tree;
                        int n = top.nodeNr;
                        for (int cur; (cur = Trees.Tiny.TinyText.NextDescendant(tree, top.nodeNr, fp, ref n)) >= 0;)
                        {
                            Core.Controller.CheckActiveTimeoutEvery64K(steps++);
                            if (Trees.Tiny.TinyText.TrySpan(tree, cur, out byte[] bytes, out int off, out int len) && Trees.Tiny.TinyText.TryPlainDouble(bytes, off, len, out double d))
                            {
                                fold.ProcessUntypedDouble(d);
                            }
                            else
                            {
                                Atomize(tree.GetNode(cur), fold);
                            }

                            if (stopWhenFinished && fold.IsFinished())
                            {
                                break;
                            }
                        }

                        return fold.Result().Head();
                    }

                    ISequenceIterator iter = nodes.Iterate(context);
                    for (IItem item; (item = iter.Next()) != null;)
                    {
                        Core.Controller.CheckActiveTimeoutEvery64K(steps++);
                        if (Trees.Tiny.TinyText.TryNodeDouble(item, out double d))
                        {
                            fold.ProcessUntypedDouble(d);
                        }
                        else
                        {
                            Atomize(item, fold);
                        }

                        if (stopWhenFinished && fold.IsFinished())
                        {
                            break;
                        }
                    }
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }

                return fold.Result().Head();
            };
        }

        private static void Atomize(IItem item, IFold fold)
        {
            // Most nodes have one value: no iterator for it.
            if (item.Atomize() is Values.AtomicValue one)
            {
                fold.ProcessItem(one);
                return;
            }

            ISequenceIterator values = Atomizer.GetAtomizingIterator(Trees.Iterators.SingletonIterator.MakeIterator(item), false);
            for (IItem v; (v = values.Next()) != null;)
            {
                fold.ProcessItem(v);
            }
        }

        protected static ISequence RunFold(IFold fold, ISequenceIterator iter)
        {
            try
            {
                int steps = 0;
                for (IItem item; (item = iter.Next()) != null;)
                {
                    // A stored input's iterator looks at no clock, and an item of a fold can be cheap.
                    if ((++steps & 1023) == 0)
                    {
                        Core.Controller.CheckActiveTimeout();
                    }

                    fold.ProcessItem(item);
                    if (fold.IsFinished())
                    {
                        break;
                    }
                }
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }

            return fold.Result();
        }
    }
}