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
using OutSmart.DAXon.Internal.Collections;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    public class XPathContextMajor : XPathContextMinor
    {
        private ParameterSet localParameters;
        private ParameterSet tunnelParameters;
        private TailCallLoop.ITailCallInfo tailCallInfo;
        private Component.M currentMode;
        private Rule currentTemplate;
        private IGroupIterator currentGroupIterator;
        private IGroupIterator currentMergeGroupIterator;
        private IRegexIterator currentRegexIterator;
        private IContextOriginator origin;
        private ThreadManager threadManager = null;
        private IResourceResolver resourceResolver;
        private IErrorReporter errorReporter;
        private Component currentComponent;
        XPathException currentException;

        public virtual IContextOriginator Origin
        {
            get => origin; set
            {
                origin = value;
            }
        }

        public virtual ISequence[] AllVariableValues => stackFrame.StackFrameValues;

        public virtual TailCallLoop.ITailCallInfo TailCallInfo
        {
            get
            {
                TailCallLoop.ITailCallInfo fn = tailCallInfo;
                tailCallInfo = null;
                return fn;
            }
        }
        public XPathContextMajor(Controller controller)
        {
            this.controller = controller;
            stackFrame = StackFrame.EMPTY;
            origin = controller;
        }

        private XPathContextMajor()
        {
        }

        public XPathContextMajor(IItem item, Executable exec)
        {
            controller = exec is PreparedStylesheet ? new XsltController(exec.GetConfiguration(), (PreparedStylesheet)exec) : new Controller(exec.GetConfiguration(), exec);
            if (item != null)
            {
                ISequenceIterator iter = SingletonIterator.MakeIterator(item);
                currentIterator = SequenceTool.FocusTracker(iter);
                currentIterator.Next();
                last = new LastValue(1);
            }

            origin = controller;
        }

        public override XPathContextMajor NewContext()
        {
            XPathContextMajor c = new XPathContextMajor();
            c.controller = controller;
            c.currentIterator = currentIterator;
            c.stackFrame = stackFrame;
            c.localParameters = localParameters;
            c.tunnelParameters = tunnelParameters;
            c.last = last;
            c.currentDestination = currentDestination;
            c.currentMode = currentMode;
            c.currentTemplate = currentTemplate;
            c.currentRegexIterator = currentRegexIterator;
            c.currentGroupIterator = currentGroupIterator;
            c.currentMergeGroupIterator = currentMergeGroupIterator;
            c.currentException = currentException;
            c.caller = this;
            c.tailCallInfo = null;
            c.temporaryOutputState = temporaryOutputState;
            c.threadManager = threadManager;
            c.currentComponent = currentComponent;
            c.errorReporter = errorReporter;
            c.resourceResolver = resourceResolver;
            return c;
        }

        public static XPathContextMajor NewContext(XPathContextMinor prev)
        {
            XPathContextMajor c = new XPathContextMajor();
            XPathContextMajor p = prev.MajorContext;
            c.controller = p.GetController();
            c.currentIterator = prev.GetCurrentIterator();
            c.stackFrame = prev.GetStackFrame();
            c.localParameters = p.GetLocalParameters();
            c.tunnelParameters = p.GetTunnelParameters();
            c.last = prev.last;
            c.currentDestination = prev.currentDestination;
            c.currentMode = p.GetCurrentMode();
            c.currentTemplate = p.GetCurrentTemplateRule();
            c.currentRegexIterator = p.GetCurrentRegexIterator();
            c.currentGroupIterator = p.GetCurrentGroupIterator();
            c.currentMergeGroupIterator = p.GetCurrentMergeGroupIterator();
            c.caller = prev;
            c.tailCallInfo = null;
            c.threadManager = p.threadManager;
            c.currentComponent = p.currentComponent;
            c.errorReporter = p.errorReporter;
            c.currentException = p.currentException;
            c.resourceResolver = p.resourceResolver;
            c.temporaryOutputState = prev.temporaryOutputState;
            return c;
        }

        public static XPathContextMajor NewThreadContext(XPathContextMinor prev)
        {
            XPathContextMajor c = NewContext(prev);
            c.stackFrame = prev.stackFrame.Copy();
            return c;
        }

        public override ThreadManager GetThreadManager()
        {
            return threadManager;
        }

        public virtual void CreateThreadManager()
        {
            threadManager = GetConfiguration().MakeThreadManager();
        }

        public override void WaitForChildThreads()
        {
            if (threadManager != null)
            {
                threadManager.WaitForChildThreads();
            }
        }

        public override ParameterSet GetLocalParameters()
        {
            if (localParameters == null)
            {
                localParameters = new ParameterSet();
            }

            return localParameters;
        }

        public virtual void SetLocalParameters(ParameterSet localParameters)
        {
            this.localParameters = localParameters;
        }

        public override ParameterSet GetTunnelParameters()
        {
            return tunnelParameters;
        }

        public virtual void SetTunnelParameters(ParameterSet tunnelParameters)
        {
            this.tunnelParameters = tunnelParameters;
        }

        public virtual void SetStackFrame(SlotManager map, ISequence[] variables)
        {
            stackFrame = new StackFrame(map, variables);
            if (map != null && variables.Length != map.NumberOfVariables)
            {
                if (variables.Length > map.NumberOfVariables)
                {
                    throw new InvalidOperationException("Attempting to set more local variables (" + variables.Length + ") than the stackframe can accommodate (" + map.NumberOfVariables + ")");
                }

                stackFrame.slots = new ISequence[map.NumberOfVariables];
                Array.Copy(variables, 0, stackFrame.slots, 0, variables.Length);
            }
        }

        public virtual void ResetStackFrameMap(SlotManager map, int numberOfParams)
        {
            stackFrame.map = map;
            if (stackFrame.slots.Length != map.NumberOfVariables)
            {
                ISequence[] v2 = new ISequence[map.NumberOfVariables];
                Array.Copy(stackFrame.slots, 0, v2, 0, numberOfParams);
                stackFrame.slots = v2;
            }
            else
            {

                // not strictly necessary
                ArrayTools.Fill(stackFrame.slots, numberOfParams, stackFrame.slots.Length, null);
            }
        }

        public virtual void ResetAllVariableValues(ISequence[] values)
        {
            stackFrame.StackFrameValues = values;
        }

        public virtual void ResetParameterValues(ISequence[] values)
        {
            Array.Copy(values, 0, stackFrame.slots, 0, values.Length);
        }

        public virtual void RequestTailCall(TailCallLoop.ITailCallInfo targetFn, ISequence[] variables)
        {
            if (variables != null)
            {
                if (variables.Length > stackFrame.slots.Length)
                {
                    stackFrame.slots = ArrayTools.CopyOf(variables, variables.Length);
                }
                else
                {
                    Array.Copy(variables, 0, stackFrame.slots, 0, variables.Length);
                }
            }

            tailCallInfo = targetFn;
        }

        public virtual void OpenStackFrame(SlotManager map)
        {
            int numberOfSlots = map.NumberOfVariables;
            if (numberOfSlots == 0)
            {
                stackFrame = StackFrame.EMPTY;
            }
            else
            {
                stackFrame = new StackFrame(map, new ISequence[numberOfSlots]);
            }
        }

        public virtual void OpenStackFrame(int numberOfVariables)
        {
            // Same zero-slot shortcut as the SlotManager overload: this one runs per
            // apply-templates evaluation and allocated a SlotManager + frame even for 0 slots.
            if (numberOfVariables == 0)
            {
                stackFrame = StackFrame.EMPTY;
                return;
            }

            stackFrame = new StackFrame(new SlotManager(numberOfVariables), SequenceTool.MakeSequenceArray(numberOfVariables));
        }

        public virtual void SetCurrentMode(Component.M mode)
        {
            currentMode = mode;
        }

        public override Component.M GetCurrentMode()
        {
            Component.M m = currentMode;
            if (m == null)
            {
                RuleManager rm = GetController().GetRuleManager();
                if (rm != null)
                {
                    return rm.UnnamedMode.GetDeclaringComponent();
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return m;
            }
        }

        public virtual void SetCurrentTemplateRule(Rule rule)
        {
            currentTemplate = rule;
        }

        public override Rule GetCurrentTemplateRule()
        {
            return currentTemplate;
        }

        public virtual void SetCurrentGroupIterator(IGroupIterator iterator)
        {
            currentGroupIterator = iterator;
        }

        public override IGroupIterator GetCurrentGroupIterator()
        {
            return currentGroupIterator;
        }

        public virtual void SetCurrentMergeGroupIterator(IGroupIterator iterator)
        {
            currentMergeGroupIterator = iterator;
        }

        public override IGroupIterator GetCurrentMergeGroupIterator()
        {
            return currentMergeGroupIterator;
        }

        public virtual void SetCurrentRegexIterator(IRegexIterator iterator)
        {
            currentRegexIterator = iterator;
        }

        public override IRegexIterator GetCurrentRegexIterator()
        {
            return currentRegexIterator;
        }

        public override int UseLocalParameter(StructuredQName paramName, int slotNumber, bool isTunnel)
        {
            ParameterSet @params = isTunnel ? GetTunnelParameters() : localParameters;
            if (@params == null)
            {
                return ParameterSet.NOT_SUPPLIED;
            }

            int index = @params.GetIndex(paramName);
            if (index < 0)
            {
                return ParameterSet.NOT_SUPPLIED;
            }

            ISequence val = @params.GetValue(index);
            stackFrame.slots[slotNumber] = val;
            bool @checked = @params.IsTypeChecked(index);
            return @checked ? ParameterSet.SUPPLIED_AND_CHECKED : ParameterSet.SUPPLIED;
        }

        public virtual void SetResourceResolver(IResourceResolver resolver)
        {
            resourceResolver = resolver;
        }

        public override IResourceResolver GetResourceResolver()
        {
            return resourceResolver == null ? controller.ResourceResolver : resourceResolver;
        }

        public virtual void SetErrorReporter(IErrorReporter reporter)
        {
            errorReporter = reporter;
        }

        public override IErrorReporter GetErrorReporter()
        {
            return errorReporter == null ? controller.ErrorReporter : errorReporter;
        }

        public virtual void SetCurrentException(XPathException exception)
        {
            currentException = exception;
        }

        public override XPathException GetCurrentException()
        {
            return currentException;
        }

        public override Component GetCurrentComponent()
        {
            return currentComponent;
        }

        public virtual void SetCurrentComponent(Component component)
        {

            currentComponent = component;
        }

        public override Component GetTargetComponent(int bindingSlot)
        {
            try
            {
                ComponentBinding binding = currentComponent.ComponentBindings[bindingSlot];
                return binding.GetTarget();
            }
            catch (NullReferenceException)
            {

                // Suggests that the current component is null, which would be a bug
                throw;
            }
            catch (IndexOutOfRangeException)
            {

                // Suggests that the current component's binding vector is the wrong size, which would be a bug.
                throw;
            }
        }

        public abstract class ThreadManager
        {
            public abstract void WaitForChildThreads();
        }
    }
}