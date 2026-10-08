////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation.Rules;
using System;
using System.Collections.Generic;

namespace OutSmart.DAXon.Transformation
{
    // Faithful port of net.sf.saxon.trans.CompoundMode (Saxon 12.9). Was a hollow stub with an
    // implicit-conversion-to-Mode operator that threw — overriding template rules in a mode via
    // xsl:use-package/xsl:override crashed with NotImplementedException.
    // A mode representing the templates within an xsl:override element of a using package
    // together with the rules in the corresponding mode of the base (used) package.
    internal sealed class CompoundMode : Mode
    {
        private readonly Mode @base;
        private readonly SimpleMode overrides;
        private readonly int overridingPrecedence;

        /// <summary>
        /// Get the active component of this mode: for a compound mode, the "overriding" part.
        /// </summary>
        public override SimpleMode ActivePart => overrides;

        public override int MaxPrecedence => overridingPrecedence;

        public override int MaxRank => overrides.MaxRank;

        /// <summary>
        /// Create a compound Mode from the base (used) package's mode and the mode containing
        /// (only) the overriding template rules from the using package.
        /// </summary>
        public CompoundMode(Mode @base, SimpleMode overrides) : base(@base.ModeName)
        {
            if (!@base.ModeName.Equals(overrides.ModeName))
            {
                throw new InvalidOperationException("Base and overriding modes must have the same name");
            }

            if (@base.ModeName.Equals(Mode.UNNAMED_MODE_NAME))
            {
                throw new InvalidOperationException("Cannot override an unnamed mode");
            }

            if (@base.ModeName.Equals(Mode.OMNI_MODE_NAME))
            {
                throw new InvalidOperationException("Cannot override mode='#all'");
            }

            this.@base = @base;
            this.overrides = overrides;
            this.mustBeTyped = @base.mustBeTyped;
            this.mustBeUntyped = @base.mustBeUntyped;
            this.overridingPrecedence = @base.MaxPrecedence + 1;
        }

        public override IBuiltInRuleSet GetBuiltInRuleSet()
        {
            return @base.GetBuiltInRuleSet();
        }

        public override bool IsEmpty()
        {
            return @base.IsEmpty() && overrides.IsEmpty();
        }

        public override void ComputeRankings(int start)
        {
            overrides.ComputeRankings(@base.MaxRank + 1);
        }

        public override void ProcessRules(IRuleAction action)
        {
            overrides.ProcessRules(action);
            @base.ProcessRules(action);
        }

        public override HashSet<NamespaceUri> GetExplicitNamespaces(NamePool pool)
        {
            HashSet<NamespaceUri> r = new HashSet<NamespaceUri>();
            r.UnionWith(@base.GetExplicitNamespaces(pool));
            r.UnionWith(overrides.GetExplicitNamespaces(pool));
            return r;
        }

        public override void AllocateAllBindingSlots(StylesheetPackage pack)
        {
            if (!bindingSlotsAllocated)
            {
                IList<ComponentBinding> baseBindings = @base.GetDeclaringComponent().ComponentBindings;
                IList<ComponentBinding> newBindings = new List<ComponentBinding>(baseBindings);
                Component comp = GetDeclaringComponent();
                comp.ComponentBindings = newBindings;
                SimpleMode.ForceAllocateAllBindingSlots(pack, overrides, newBindings);
                bindingSlotsAllocated = true;
            }
        }

        public override Rule GetRule(IItem item, IXPathContext context)
        {
            Rule r = overrides.GetRule(item, context);
            if (r == null)
            {
                r = @base.GetRule(item, context);
            }

            return r;
        }

        public override int GetStackFrameSlotsNeeded()
        {
            return Math.Max(@base.GetStackFrameSlotsNeeded(), overrides.GetStackFrameSlotsNeeded());
        }

        public override Rule GetRule(IItem item, IXPathContext context, Func<Rule, bool> filter)
        {
            Rule r = overrides.GetRule(item, context, filter);
            if (r == null)
            {
                r = @base.GetRule(item, context, filter);
            }

            return r;
        }

        public override void ExportTemplateRules(ExpressionPresenter presenter)
        {
            overrides.ExportTemplateRules(presenter);
            @base.ExportTemplateRules(presenter);
        }

        public override void ExplainTemplateRules(ExpressionPresenter presenter)
        {
            overrides.ExplainTemplateRules(presenter);
            @base.ExplainTemplateRules(presenter);
        }
    }
}
