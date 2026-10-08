////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Values
{
    internal abstract class Closure : ISequence, IContextOriginator
    {
        protected IPullEvaluator inputEvaluator;
        protected XPathContextMajor savedXPathContext;
        protected int depth;
        protected LearningEvaluator learningEvaluator;
        protected int serialNumber;
        protected Expression expression; // for diagnostics only
        protected ISequenceIterator inputIterator;

        public virtual XPathContextMajor SavedXPathContext
        {
            get => savedXPathContext; set
            {
                this.savedXPathContext = value;
            }
        }
        public Closure()
        {
        }

        public virtual void SaveContext(Expression expression, IXPathContext context)
        {

            // Make a copy of all local variables. If the value of any local variable is a closure
            // whose depth exceeds a certain threshold, we evaluate the closure eagerly to avoid
            // creating deeply nested lists of Closures, which consume memory unnecessarily
            // We only copy the local variables if the expression has dependencies on local variables.
            // What's more, we only copy those variables that the expression actually depends on.
            this.expression = expression; // for diagnostics only
            if ((expression.Dependencies & StaticProperty.DEPENDS_ON_LOCAL_VARIABLES) != 0)
            {
                StackFrame localStackFrame = context.GetStackFrame();
                ISequence[] local = localStackFrame.StackFrameValues;
                int[] slotsUsed = expression.SlotsUsed; // computed on first call
                if (local != null)
                {
                    SlotManager stackFrameMap = localStackFrame.GetStackFrameMap();
                    ISequence[] savedStackFrame = new ISequence[stackFrameMap.NumberOfVariables];
                    foreach (int i in slotsUsed)
                    {
                        if (local[i] is Closure)
                        {
                            int cdepth = ((Closure)local[i]).depth;
                            if (cdepth >= 10)
                            {
                                try
                                {
                                    local[i] = SequenceTool.ToGroundedValue(local[i].Iterate());
                                }
                                catch (UncheckedXPathException e)
                                {
                                    throw e.GetXPathException();
                                }
                            }
                            else if (cdepth + 1 > depth)
                            {
                                depth = cdepth + 1;
                            }
                        }

                        savedStackFrame[i] = local[i];
                    }

                    savedXPathContext.SetStackFrame(stackFrameMap, savedStackFrame);
                }
            }
            else if ((expression.Dependencies & StaticProperty.DEPENDS_ON_OWN_RANGE_VARIABLES) != 0)
            {

                // Bug 5913: if the expression does not access external local variables, but uses the stackframe
                // for range variables declared within the expression itself, we need to allocate a clean
                // stack frame for use during lazy evaluation
                StackFrame localStackFrame = context.GetStackFrame();
                SlotManager stackFrameMap = localStackFrame.GetStackFrameMap();
                ISequence[] savedStackFrame = new ISequence[stackFrameMap.NumberOfVariables];
                savedXPathContext.SetStackFrame(stackFrameMap, savedStackFrame);
            }


            // Make a copy of the context item
            IFocusIterator currentIterator = context.GetCurrentIterator();
            if (currentIterator != null)
            {
                IItem contextItem = currentIterator.Current();
                ManualIterator single = new ManualIterator(contextItem);
                savedXPathContext.SetCurrentIterator(single); // we don't save position() and last() because we have no way
                // of restoring them. So the caller must ensure that a Closure is not
                // created if the expression depends on position() or last()
            }
        }

        public virtual void SetLearningEvaluator(LearningEvaluator learningEvaluator, int serialNumber)
        {
            this.learningEvaluator = learningEvaluator;
            this.serialNumber = serialNumber;
        }

        public virtual IItem Head()
        {
            try
            {
                return Iterate().Next();
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        public virtual void SetInputEvaluator(IPullEvaluator inputEvaluator)
        {
            this.inputEvaluator = inputEvaluator;
        }

        public abstract ISequenceIterator Iterate();
        public virtual IGroundedValue Reduce()
        {
            return SequenceTool.ToGroundedValue(Iterate());
        }

        public virtual ISequence MakeRepeatable()
        {
            return Materialize();
        }

        public virtual Expression GetExpression()
        {
            return expression;
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual IGroundedValue Materialize() => SequenceTool.ToGroundedValue(Iterate()); // StubGen NIE -> faithful Java Sequence.materialize() default
    }
}