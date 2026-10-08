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
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class EarlyEvaluationContext : IXPathContext
    {
        private readonly Configuration config;

        public XPathContextMajor MajorContext => null;

        // no-op
        public int TemporaryOutputState
        {
            get => 0; set
            {
            }
        }

        // no-op
        public string CurrentOutputUri
        {
            get => null; set
            {
            }
        }
        public EarlyEvaluationContext(Configuration config)
        {
            this.config = config;
        }

        public ISequence EvaluateLocalVariable(int slotnumber)
        {
            NotAllowed();
            return null;
        }

        public IXPathContext GetCaller()
        {
            return null;
        }

        public IResourceResolver GetResourceResolver()
        {
            return config.GetResourceResolver();
        }

        public IErrorReporter GetErrorReporter()
        {
            return config.MakeErrorReporter();
        }

        /// <summary>
        /// Get the current component
        /// </summary>
        public Component GetCurrentComponent()
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the Configuration
        /// </summary>
        public Configuration GetConfiguration()
        {
            return config;
        }

        /// <summary>
        /// Get the Configuration
        /// </summary>
        public IItem GetContextItem()
        {
            return null;
        }

        public Controller GetController()
        {
            return null;
        }

        public IGroupIterator GetCurrentGroupIterator()
        {
            NotAllowed();
            return null;
        }

        public IGroupIterator GetCurrentMergeGroupIterator()
        {
            NotAllowed();
            return null;
        }

        public Component.M GetCurrentMode()
        {
            NotAllowed();
            return null;
        }

        public IRegexIterator GetCurrentRegexIterator()
        {
            return null;
        }

        public Rule GetCurrentTemplateRule()
        {
            return null;
        }

        public int GetLast()
        {
            XPathException err = new XPathException("The context item is absent", "XPDY0002");
            throw new UncheckedXPathException(err);
        }

        public ParameterSet GetLocalParameters()
        {
            NotAllowed();
            return null;
        }

        public NamePool GetNamePool()
        {
            return config.GetNamePool();
        }

        public StackFrame GetStackFrame()
        {
            NotAllowed();
            return null;
        }

        public ParameterSet GetTunnelParameters()
        {
            NotAllowed();
            return null;
        }

        public bool IsAtLast()
        {
            XPathException err = new XPathException("The context item is absent");
            err.SetErrorCode("XPDY0002");
            throw err;
        }

        public XPathContextMajor NewCleanContext()
        {
            NotAllowed();
            return null;
        }

        public XPathContextMajor NewContext()
        {
            Controller controller = new Controller(config);
            return controller.NewXPathContext();
        }

        public XPathContextMinor NewMinorContext()
        {
            return NewContext().NewMinorContext();
        }

        public void SetCaller(IXPathContext caller)
        {
        }

        // no-op
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public void SetCurrentIterator(IFocusIterator iter)
        {
            NotAllowed();
        }

        // no-op
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public IFocusIterator TrackFocus(ISequenceIterator iter)
        {
            NotAllowed();
            return null;
        }

        // no-op
        public void SetLocalVariable(int slotNumber, ISequence value)
        {
            NotAllowed();
        }

        // no-op
        public int UseLocalParameter(StructuredQName parameterId, int slotNumber, bool isTunnel)
        {
            return ParameterSet.NOT_SUPPLIED;
        }

        // no-op
        public DateTimeValue GetCurrentDateTime()
        {
            throw new NoDynamicContextException("current-dateTime");
        }

        // no-op
        public int GetImplicitTimezone()
        {
            return CalendarValue.MISSING_TIMEZONE;
        }

        // no-op
        public XPathException GetCurrentException()
        {
            return null;
        }

        // no-op
        public void WaitForChildThreads()
        {
            GetCaller().WaitForChildThreads();
        }

        // no-op
        private void NotAllowed()
        {
            throw new NotSupportedException((new NoDynamicContextException("Internal error: early evaluation of subexpression with no context")).ToString());
        }

        // no-op
        public XPathContextMajor.ThreadManager GetThreadManager()
        {
            return null;
        }

        // no-op
        public Component GetTargetComponent(int bindingSlot)
        {
            return null;
        }
        IFocusIterator IXPathContext.GetCurrentIterator() => default;
    }
}
