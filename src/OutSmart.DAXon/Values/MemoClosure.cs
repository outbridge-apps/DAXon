////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Values
{
    internal sealed class MemoClosure : Closure, IContextOriginator
    {
        private ISequence sequence;

        public ISequence SequenceAsIs => sequence;
        public MemoClosure(Expression expr, IPullEvaluator inputEvaluator, IXPathContext context)
        {

            SetInputEvaluator(inputEvaluator);
            XPathContextMajor c2 = context.NewContext();
            c2.Origin = this;
            SavedXPathContext = c2;
            SaveContext(expr, context);
        }

        public override ISequenceIterator Iterate()
        {
            lock (this)
            {
                try
                {
                    MakeSequence();
                }
                catch (XPathException e)
                {
                    throw new UncheckedXPathException(e);
                }

                return sequence.Iterate();
            }
        }

        private void MakeSequence()
        {
            if (sequence == null)
            {
                inputIterator = inputEvaluator.Iterate(savedXPathContext);
                if (inputIterator is IGroundedIterator && ((IGroundedIterator)inputIterator).IsActuallyGrounded())
                {
                    sequence = ((IGroundedIterator)inputIterator).Materialize();

                    // If we find that the input iterator is grounded, this means there was no point
                    // in doing lazy evaluation. If this happens once, it will probably happen all the time,
                    // so send a message back to the binding instruction encouraging it to use eager
                    // evaluation in future.
                    if (learningEvaluator != null)
                    {
                        learningEvaluator.ReportCompletion();
                    }
                }
                else
                {
                    sequence = SequenceTool.ToMemoSequence(inputIterator);
                    if (sequence is MemoSequence && learningEvaluator != null)
                    {
                        ((MemoSequence)sequence).SetLearningEvaluator(learningEvaluator, serialNumber);
                    }
                }
            }
        }

        public IItem ItemAt(int n)
        {
            lock (this)
            {
                MakeSequence();
                if (sequence is IGroundedValue)
                {
                    return ((IGroundedValue)sequence).ItemAt(n);
                }
                else if (sequence is MemoSequence)
                {
                    return ((MemoSequence)sequence).ItemAt(n);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public override IGroundedValue Reduce()
        {
            try
            {
                if (sequence is IGroundedValue)
                {
                    return (IGroundedValue)sequence;
                }
                else
                {
                    return SequenceTool.ToGroundedValue(Iterate());
                }
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        public override ISequence MakeRepeatable()
        {
            return this;
        }
    }
}