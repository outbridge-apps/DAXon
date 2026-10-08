////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    public class XPathContextMinor : IXPathContext
    {
        public Controller controller;
        public IFocusIterator currentIterator;
        public LastValue last = null;
        public IXPathContext caller = null;
        public StackFrame stackFrame;
        public string currentDestination = "";
        internal int temporaryOutputState = 0;   // TemporaryOutputState is the public spelling

        public XPathContextMajor MajorContext
        {
            get
            {
                IXPathContext c = this;
                while (c != null && !(c is XPathContextMajor))
                {
                    c = c.GetCaller();
                }

                return (XPathContextMajor)c;
            }
        }

        public virtual int TemporaryOutputState
        {
            get => temporaryOutputState; set
            {
                temporaryOutputState = value;
            }
        }

        public virtual string CurrentOutputUri
        {
            get => currentDestination; set
            {
                currentDestination = value;
            }
        }
        protected XPathContextMinor()
        {
        }

        public virtual XPathContextMajor NewContext()
        {
            return XPathContextMajor.NewContext(this);
        }

        public virtual XPathContextMinor NewMinorContext()
        {
            XPathContextMinor c = new XPathContextMinor();

            c.controller = controller;
            c.caller = this;
            c.currentIterator = currentIterator;
            c.last = last;
            c.stackFrame = stackFrame;
            c.currentDestination = currentDestination;
            c.temporaryOutputState = temporaryOutputState;
            return c;
        }

        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        public virtual void SetCaller(IXPathContext caller)
        {
            this.caller = caller;
        }

        public virtual XPathContextMajor NewCleanContext()
        {
            XPathContextMajor c = new XPathContextMajor(GetController());
            c.SetCaller(this);
            return c;
        }

        public virtual ParameterSet GetLocalParameters()
        {
            return MajorContext.GetLocalParameters();
        }

        public virtual ParameterSet GetTunnelParameters()
        {
            return MajorContext.GetTunnelParameters();
        }

        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public Controller GetController()
        {
            return controller;
        }

        /// <summary>
        /// Get the Configuration
        /// </summary>
        public Configuration GetConfiguration()
        {
            return controller.GetConfiguration();
        }

        public NamePool GetNamePool()
        {
            return controller.GetConfiguration().GetNamePool();
        }

        public IXPathContext GetCaller()
        {
            return caller;
        }

        public virtual void SetCurrentIterator(IFocusIterator iter)
        {
            currentIterator = iter;
            last = null;
        }

        public virtual IFocusIterator TrackFocus(ISequenceIterator iter)
        {
            IFocusIterator fit = controller.MakeFocusTracker(iter, false);
            SetCurrentIterator(fit);
            return fit;
        }

        public virtual IFocusIterator TrackFocusMultithreaded(ISequenceIterator iter)
        {
            IFocusIterator fit = controller.MakeFocusTracker(iter, true);
            SetCurrentIterator(fit);
            return fit;
        }

        public IFocusIterator GetCurrentIterator()
        {
            return currentIterator;
        }

        public IItem GetContextItem()
        {
            if (currentIterator == null)
            {
                return null;
            }

            if (currentIterator is FocusTrackingIterator)
            {

                // Common case extracted to reduce overhead of megamorphism
                return ((FocusTrackingIterator)currentIterator).Current();
            }
            else
            {
                return currentIterator.Current();
            }
        }

        public int GetLast()
        {
            if (currentIterator == null)
            {
                throw new UncheckedXPathException(new XPathException("The context item is absent, so last() is undefined").WithXPathContext(this).WithErrorCode("XPDY0002"));
            }

            if (last != null)
            {
                return last.value;
            }

            try
            {
                int length = currentIterator.GetLength();
                last = new LastValue(length);
                return length;
            }
            catch (XPathException err)
            {
                throw new UncheckedXPathException(err);
            }
        }

        public bool IsAtLast()
        {
            if (currentIterator == null)
            {
                throw new XPathException("Cannot evaluate position()=last() because there is no context item", "XPDY0002");
            }

            if (currentIterator is ILookaheadIterator && ((ILookaheadIterator)currentIterator).SupportsHasNext())
            {
                return !((ILookaheadIterator)currentIterator).HasNext;
            }

            try
            {
                return currentIterator.Position() == GetLast();
            }
            catch (UncheckedXPathException e)
            {
                throw XPathException.MakeXPathException(e);
            }
        }

        public virtual IResourceResolver GetResourceResolver()
        {
            return caller.GetResourceResolver();
        }

        public virtual IErrorReporter GetErrorReporter()
        {
            return caller.GetErrorReporter();
        }

        public virtual XPathException GetCurrentException()
        {
            return caller.GetCurrentException();
        }

        public virtual XPathContextMajor.ThreadManager GetThreadManager()
        {
            return caller.GetThreadManager();
        }

        public virtual Component GetCurrentComponent()
        {
            return caller.GetCurrentComponent();
        }

        public virtual StackFrame GetStackFrame()
        {
            return stackFrame;
        }

        public virtual void MakeStackFrameMutable()
        {
            if (stackFrame == StackFrame.EMPTY)
            {
                stackFrame = new StackFrame(null, SequenceTool.MakeSequenceArray(0));
            }
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        public ISequence EvaluateLocalVariable(int slot)
        {
            return stackFrame.slots[slot];
        }

        public void SetLocalVariable(int slotNumber, ISequence value)
        {

            // The following code is deep defence against attempting to store a non-memo Closure in a variable.
            // This should not happen, and if it does, it means that the evaluation mode has been miscalculated.
            // But if it does happen, we recover by wrapping the Closure in a MemoSequence which remembers the
            // value as it is calculated.
            value = value.MakeRepeatable();
            try
            {
                stackFrame.slots[slotNumber] = value;
            }
            catch (IndexOutOfRangeException e)
            {
                if (slotNumber == -999)
                {
                    throw new InvalidOperationException("Internal error: Cannot set local variable: no slot allocated");
                }
                else
                {
                    throw new InvalidOperationException("Internal error: Cannot set local variable (slot " + slotNumber + " of " + GetStackFrame().StackFrameValues.Length + ")");
                }
            }
        }

        public virtual void WaitForChildThreads()
        {
            lock (this)
            {
                MajorContext.WaitForChildThreads();
            }
        }

        public virtual int UseLocalParameter(StructuredQName parameterId, int slotNumber, bool isTunnel)
        {
            return MajorContext.UseLocalParameter(parameterId, slotNumber, isTunnel);
        }

        public virtual Component.M GetCurrentMode()
        {
            return MajorContext.GetCurrentMode();
        }

        public virtual Rule GetCurrentTemplateRule()
        {

            // In a minor context, the current template rule is always null. This is a consequence
            // of the way they are used.
            return null; //return getCaller().getCurrentTemplateRule();
        }

        public virtual IGroupIterator GetCurrentGroupIterator()
        {
            return MajorContext.GetCurrentGroupIterator();
        }

        public virtual IGroupIterator GetCurrentMergeGroupIterator()
        {
            return MajorContext.GetCurrentMergeGroupIterator();
        }

        public virtual IRegexIterator GetCurrentRegexIterator()
        {
            return MajorContext.GetCurrentRegexIterator();
        }

        public virtual DateTimeValue GetCurrentDateTime()
        {
            return controller.GetCurrentDateTime();
        }

        public int GetImplicitTimezone()
        {
            return controller.GetImplicitTimezone();
        }

        public virtual Component GetTargetComponent(int bindingSlot)
        {
            return MajorContext.GetTargetComponent(bindingSlot);
        }

        // Note: consider eliminating this class. A new XPathContextMinor is created under two circumstances,
        // (a) when the focus changes (i.e., a new current iterator), and (b) when the current
        // receiver changes. We could handle these by maintaining a stack of iterators and a stack of
        // receivers in the XPathContextMajor object. Adding a new iterator or receiver to the stack would
        // generally be cheaper than creating the new XPathContextMinor object. The main difficulty (in the
        // case of iterators) is knowing when to pop the stack: currently we rely on the garbage collector.
        // We can only really do this when the iterator comes to its end, which is difficult to detect.
        // Perhaps we should try to do static allocation, so that fixed slots are allocated for different
        // minor-contexts within a Procedure, and a compiled expression that uses the focus knows which
        // slot to look in.
        // Investigated the above Sept 2008. On xmark, with a 100Mb input, the path expression
        // count(site/people/person/watches/watch) takes just 13ms to execute (compared with 6500ms for building
        // the tree). Only 6 context objects are created while doing this. This doesn't appear to be a productive
        // area to look for new optimizations.
        public class LastValue
        {
            public readonly int value;
            public LastValue(int count)
            {

                value = count;
            }
        }
    }
}
