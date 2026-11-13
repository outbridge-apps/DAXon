////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Lib;
namespace OutSmart.DAXon.XQuery
{
    public class DynamicQueryContext
    {
        private IItem contextItem;
        private GlobalParameterSet parameters = new GlobalParameterSet();
        private readonly Configuration config;
        private IResourceResolver resourceResolver;
        private IErrorReporter errorReporter;
        private ITraceListener traceListener;
        private IUnparsedTextURIResolver unparsedTextURIResolver;
        private DateTimeValue currentDateTime;
        private Logger traceFunctionDestination;
        private int validationMode = Validation.DEFAULT;
        private bool applyConversionRules = true;

        public virtual int SchemaValidationMode
        {
            get => validationMode; set
            {
                this.validationMode = value;
            }
        }

        public virtual IItem ContextItem
        {
            get => contextItem; set
            {
                if (value == null)
                {
                    throw new NullReferenceException("Context item cannot be null");
                }

                if (config.HoldsForeignNode(value))   // a node, or a map or an array holding one
                {
                    throw new ArgumentException("Supplied node must be built using the same or a compatible Configuration");
                }

                contextItem = value; //parameters.put(StandardNames.SAXON_CONTEXT_ITEM, item);
            }
        }

        public virtual GlobalParameterSet Parameters
        {
            get
            {
                if (parameters == null)
                {
                    return new GlobalParameterSet();
                }
                else
                {
                    return parameters;
                }
            }
        }

        public virtual IResourceResolver ResourceResolver
        {
            get => resourceResolver; set
            {

                resourceResolver = value;
            }
        }

        public virtual IUnparsedTextURIResolver UnparsedTextURIResolver
        {
            get => unparsedTextURIResolver; set
            {

                unparsedTextURIResolver = value;
            }
        }

        public virtual IErrorReporter ErrorReporter
        {
            get => errorReporter; set
            {
                errorReporter = value;
            }
        }

        public virtual Logger TraceFunctionDestination
        {
            get => traceFunctionDestination; set
            {
                traceFunctionDestination = value;
            }
        }
        public DynamicQueryContext(Configuration config)
        {
            this.config = config;
            errorReporter = config.MakeErrorReporter();
            traceFunctionDestination = config.Logger;
        }

        public virtual void SetApplyFunctionConversionRulesToExternalVariables(bool convert)
        {
            applyConversionRules = convert;
        }

        public virtual bool IsApplyFunctionConversionRulesToExternalVariables()
        {
            return applyConversionRules;
        }

        public virtual void SetParameter(StructuredQName expandedName, IGroundedValue value)
        {
            if (parameters == null)
            {
                parameters = new GlobalParameterSet();
            }

            parameters.Put(expandedName, value);
        }

        /// <summary>
        /// Reset the parameters to an empty list.
        /// </summary>
        public virtual void ClearParameters()
        {
            parameters = new GlobalParameterSet();
        }

        public virtual IGroundedValue GetParameter(StructuredQName expandedName)
        {
            if (parameters == null)
            {
                return null;
            }

            return parameters[expandedName];
        }

        public virtual void SetTraceListener(ITraceListener listener)
        {
            traceListener = listener;
        }

        public virtual ITraceListener GetTraceListener()
        {
            return traceListener;
        }

        public virtual DateTimeValue GetCurrentDateTime()
        {
            return currentDateTime;
        }

        public virtual void SetCurrentDateTime(DateTimeValue dateTime)
        {
            currentDateTime = dateTime;
            if (dateTime.GetComponent(AccessorFn.Component.TIMEZONE) == null)
            {
                throw new XPathException("Supplied date/time must include a timezone");
            }
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        private ITraceListener installedListener;   // what InitializeController last added to a controller

        public virtual void InitializeController(Controller controller)
        {
            controller.ResourceResolver = ResourceResolver;
            controller.ErrorReporter = ErrorReporter;
            // A controller reused across XQueryEvaluator.CallFunction calls is initialized again for each: the listener
            // this context put there before goes (it was never taken off: switching to B, or to none, kept A), and the
            // resolver is the context's or else the configuration's.
            ITraceListener listener = GetTraceListener();
            if (installedListener != null && installedListener != listener)
            {
                controller.SetTraceListener(TraceEventMulticaster.Remove(controller.GetTraceListener(), installedListener));
            }

            if (!TraceEventMulticaster.Contains(controller.GetTraceListener(), listener))
            {
                controller.AddTraceListener(listener);
            }

            installedListener = listener;
            controller.UnparsedTextURIResolver = unparsedTextURIResolver ?? config.UnparsedTextURIResolver;

            controller.TraceFunctionDestination = TraceFunctionDestination;
            controller.SchemaValidationMode = SchemaValidationMode;
            DateTimeValue currentDateTime = GetCurrentDateTime();
            if (currentDateTime != null)
            {
                try
                {
                    controller.SetCurrentDateTime(currentDateTime);
                }
                catch (XPathException e)
                {
                    throw new InvalidOperationException(e.Message, e); // the value should already have been checked
                }
            }

            controller.GlobalContextItem = contextItem;
            controller.InitializeController(parameters);
            controller.SetApplyFunctionConversionRulesToExternalVariables(applyConversionRules); //controller.getExecutable().checkAllRequiredParamsArePresent(parameters);
        }
    }
}
