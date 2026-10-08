////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;

namespace OutSmart.DAXon.Events
{
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(Action<Saxon.Hej.s9api.IXmlProcessingError> reporter) {"
    //                + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //                + "    }"
    //})
    public class PipelineConfiguration
    {
        private Configuration config;
        private Controller controller;
        private ParseOptions parseOptions;
        private HostLanguage hostLanguage = HostLanguage.UNKNOWN;
        private Dictionary<string, object> components;
        private IXPathContext context;
        private Func<NodeInfo, Object> copyInformee;

        public virtual Func<NodeInfo, Object> CopyInformee
        {
            get => this.copyInformee; set
            {
                this.copyInformee = value;
            }
        }

        public virtual IXPathContext XPathContext
        {
            get => context; set
            {
                this.context = value;
            }
        }
        public PipelineConfiguration(Configuration config)
        {
            this.config = config;
            parseOptions = new ParseOptions();
        }

        public PipelineConfiguration(Configuration config, ParseOptions parseOptions)
        {
            this.config = config;
            this.parseOptions = parseOptions;
        }

        public PipelineConfiguration(PipelineConfiguration p)
        {
            config = p.config;
            controller = p.controller;
            parseOptions = p.parseOptions;
            hostLanguage = p.hostLanguage;
            if (p.components != null)
            {
                components = new Dictionary<string, object>(p.components);
            }

            context = p.context;
            copyInformee = null;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual void SetConfiguration(Configuration config)
        {
            this.config = config;
        }

        public virtual IErrorReporter GetErrorReporter()
        {
            IErrorReporter reporter = GetParseOptions().GetErrorReporter();
            if (reporter == null)
            {
                reporter = controller == null ? config.MakeErrorReporter() : controller.ErrorReporter;
            }

            return reporter;
        }

        public virtual void SetErrorReporter(IErrorReporter errorReporter)
        {
            parseOptions = GetParseOptions().WithErrorReporter(errorReporter);
        }

        public virtual void SetParseOptions(ParseOptions options)
        {
            parseOptions = options;
        }

        public virtual ParseOptions GetParseOptions()
        {
            if (parseOptions == null)
            {
                parseOptions = config.GetParseOptions();
            }

            return parseOptions;
        }

        public virtual void SetUseXsiSchemaLocation(bool recognize)
        {
            parseOptions = GetParseOptions().WithUseXsiSchemaLocation(recognize);
        }

        public virtual void SetRecoverFromValidationErrors(bool recover)
        {
            parseOptions = GetParseOptions().WithContinueAfterValidationErrors(recover);
        }

        public virtual bool IsRecoverFromValidationErrors()
        {
            return GetParseOptions().IsContinueAfterValidationErrors();
        }

        public virtual Controller GetController()
        {
            return controller;
        }

        public virtual void SetController(Controller controller)
        {
            this.controller = controller;
        }

        public virtual HostLanguage GetHostLanguage()
        {
            if (hostLanguage == HostLanguage.UNKNOWN)
            {
                hostLanguage = controller == null ? HostLanguage.UNKNOWN : controller.GetExecutable().GetHostLanguage();
            }

            return hostLanguage;
        }

        public virtual bool IsXSLT()
        {
            return GetHostLanguage() == HostLanguage.XSLT;
        }

        public virtual void SetHostLanguage(HostLanguage language)
        {
            hostLanguage = language;
        }

        public virtual void SetComponent(string name, object value)
        {
            if (components == null)
            {
                components = new Dictionary<string, object>();
            }

            components[name] = value;
        }

        public virtual object GetComponent(string name)
        {
            if (components == null)
            {
                return null;
            }
            else
            {
                return components.GetOrDefault(name);
            }
        }
    }
}