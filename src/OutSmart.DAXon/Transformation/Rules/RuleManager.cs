////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Transformation.Rules
{
    /// <summary>
    /// <B>RuleManager</B> maintains a set of template rules, one set for each mode
    /// </summary>
    public sealed class RuleManager
    {
        private readonly StylesheetPackage stylesheetPackage;
        private readonly Configuration config;
        private readonly SimpleMode unnamedMode; // template rules with default mode
        private readonly Dictionary<StructuredQName, Mode> modes;
        // tables of rules for non-default modes
        //private SimpleMode omniMode = null;       //template rules that specify mode="all"
        private bool unnamedModeExplicit;
        private CompilerInfo compilerInfo; // We may need access to information on the compilation as distinct from the configuration
        private int nextSequenceNumber;

        public ICollection<Mode> AllNamedModes => modes.Values;

        public SimpleMode UnnamedMode => unnamedMode;
        /// <summary>
        /// create a RuleManager and initialise variables.
        /// </summary>
        public RuleManager(StylesheetPackage pack) : this(pack, pack.GetConfiguration().DefaultXsltCompilerInfo)
        {
        }

        public RuleManager(StylesheetPackage pack, CompilerInfo compilerInfo)
        {
            this.stylesheetPackage = pack;
            this.config = pack.GetConfiguration();
            this.compilerInfo = compilerInfo;
            this.unnamedMode = config.MakeMode(Mode.UNNAMED_MODE_NAME, this.compilerInfo);
            Component c = unnamedMode.MakeDeclaringComponent(Visibility.PRIVATE, stylesheetPackage);
            c.SetVisibility(Visibility.PRIVATE, VisibilityProvenance.DEFAULTED);
            this.stylesheetPackage.AddComponent(c);
            this.modes = new Dictionary<StructuredQName, Mode>(5);
        }

        public void SetUnnamedModeExplicit(bool declared)
        {
            unnamedModeExplicit = declared;
        }

        public bool IsUnnamedModeExplicit()
        {
            return unnamedModeExplicit;
        }

        public void SetCompilerInfo(CompilerInfo compilerInfo)
        {
            this.compilerInfo = compilerInfo;
        }

        public StylesheetPackage GetStylesheetPackage()
        {
            return stylesheetPackage;
        }

        public Mode ObtainMode(StructuredQName modeName, bool createIfAbsent)
        {
            if (modeName == null || modeName.Equals(Mode.UNNAMED_MODE_NAME))
            {
                return unnamedMode;
            }

            if (modeName.Equals(Mode.OMNI_MODE_NAME))
            {
                throw new ArgumentException("#all is not a real mode");
            }

            Mode m = modes.GetOrDefault(modeName);
            if (m == null && createIfAbsent)
            {
                m = config.MakeMode(modeName, compilerInfo);
                modes[modeName] = m;
                Component c = m.MakeDeclaringComponent(Visibility.PRIVATE, stylesheetPackage);
                c.SetVisibility(Visibility.PRIVATE, VisibilityProvenance.DEFAULTED);
                stylesheetPackage.AddComponent(c);
            }

            return m;
        }

        public void RegisterMode(Mode mode)
        {
            modes[mode.ModeName] = mode;
        }

        public int AllocateSequenceNumber()
        {
            return nextSequenceNumber++;
        }

        public int RegisterRule(Patterns.Pattern pattern, TemplateRule eh, Mode mode, StylesheetModule module, double priority, int position, int part)
        {
            if (double.IsNaN(priority))
            {
                priority = pattern.DefaultPriority;
            }

            if (mode is SimpleMode)
            {
                ((SimpleMode)mode).AddRule(pattern, eh, module, module.Precedence, priority, position, part);
            }
            else
            {
                mode.ActivePart.AddRule(pattern, eh, module, mode.MaxPrecedence, priority, position, part);
            }

            return 1;
        }

        public void ComputeRankings()
        {
            unnamedMode.ComputeRankings(0);
            foreach (Mode mode in modes.Values)
            {
                mode.ComputeRankings(0);
            }
        }

        public void InvertStreamableTemplates()
        {
            unnamedMode.InvertStreamableTemplates();
            foreach (Mode mode in modes.Values)
            {
                mode.ActivePart.InvertStreamableTemplates();
            }
        }

        public void CheckConsistency()
        {
            unnamedMode.ResolveProperties(this);
            foreach (Mode mode in modes.Values)
            {
                mode.ActivePart.ResolveProperties(this);
            }
        }

        public void ExplainTemplateRules(ExpressionPresenter presenter)
        {
            unnamedMode.Explain(presenter);
            foreach (Mode mode in modes.Values)
            {
                mode.Explain(presenter);
            }
        }

        public void OptimizeRules()
        {
            unnamedMode.OptimizeRules();
            foreach (Mode mode in modes.Values)
            {
                mode.ActivePart.OptimizeRules();
            }
        }
    }
}