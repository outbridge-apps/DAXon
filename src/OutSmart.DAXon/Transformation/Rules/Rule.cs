////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using System;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Transformation.Rules
{
    public class Rule
    {
        protected Patterns.Pattern pattern; // The pattern that fires this rule
        protected IRuleTarget action; // The action associated with this rule (a TemplateRule, accumulator rule, etc)
        protected int precedence; // The import precedence
        protected int minImportPrecedence; // The minimum import precedence to be considered by xsl:apply-imports
        protected double priority; // The priority of the rule
        protected Rule next; // The next rule after this one in the chain of rules
        protected int sequence; // The relative position of this rule, its position in declaration order
        protected int part; // The relative position of this rule relative to others formed by splitting
        // on a union pattern
        private bool alwaysMatches; // True if the pattern does not need to be tested, because the rule
        // is on a rule-chain such that the pattern is necessarily satisfied
        private int rank; // Indicates the relative precedence/priority of a rule within a mode;

        public virtual int Sequence => sequence;

        public virtual int PartNumber => part;

        public virtual Rule Next
        {
            get => next; set
            {
                this.next = value;
            }
        }

        public virtual Patterns.Pattern Pattern
        {
            get => pattern; set
            {
                this.pattern = value;
            }
        }

        public virtual int Precedence => precedence;

        public virtual int MinImportPrecedence => minImportPrecedence;

        public virtual double Priority => priority;

        public virtual int Rank
        {
            get => rank; set
            {
                this.rank = value;
            }
        }
        // used for quick comparison
        protected Rule()
        {
        }

        public Rule(Patterns.Pattern p, IRuleTarget o, int prec, int min, double prio, int seq, int part)
        {
            pattern = p;
            action = o;
            precedence = prec;
            minImportPrecedence = min;
            priority = prio;
            next = null;
            sequence = seq;
            this.part = part;
            o.RegisterRule(this);
        }

        public virtual void SetAction(IRuleTarget action)
        {
            this.action = action;
        }

        public virtual IRuleTarget GetAction()
        {
            return action;
        }

        public virtual void SetAlwaysMatches(bool matches)
        {
            alwaysMatches = matches;
        }

        public virtual bool IsAlwaysMatches()
        {
            return alwaysMatches;
        }

        public virtual void Export(ExpressionPresenter @out, bool modeStreamable)
        {
            IRuleTarget target = GetAction();
            TemplateRule template = null;
            if (target is TemplateRule)
            {
                template = (TemplateRule)target;
                int s = @out.StartElement("templateRule");
                @out.EmitAttribute("prec", Precedence + "");
                @out.EmitAttribute("prio", Priority + "");
                @out.EmitAttribute("seq", Sequence + "");
                if (part != 0)
                {
                    @out.EmitAttribute("part", "" + part);
                }

                @out.EmitAttribute("rank", "" + Rank);
                @out.EmitAttribute("minImp", MinImportPrecedence + "");
                @out.EmitAttribute("slots", template.StackFrameMap.NumberOfVariables + "");
                @out.EmitAttribute("matches", ItemTypeExtensions.GetFullAlphaCode(pattern.GetItemType()));
                template.ExplainProperties(@out);
                ExportOtherProperties(@out);
                @out.SetChildRole("match");
                Pattern.Export(@out);
                if (template.GetBody() != null)
                {
                    @out.SetChildRole("action");
                    template.GetBody().Export(@out);
                }

                int e = @out.EndElement();
                if (s != e)
                {
                    throw new InvalidOperationException("exported expression tree unbalanced in template at line " + (template != null ? template.GetLineNumber() + " of " + template.GetSystemId() : ""));
                }
            }
            else
            {
                target.Export(@out);
            }
        }

        public virtual void ExportOtherProperties(ExpressionPresenter @out)
        {
        }

        public virtual int CompareRank(Rule other)
        {
            return rank - other.rank;
        }

        public virtual int CompareComputedRank(Rule other)
        {
            if (precedence == other.precedence)
            {
                return priority.CompareTo(other.priority);
            }
            else if (precedence < other.precedence)
            {
                return -1;
            }
            else
            {
                return +1;
            }
        }

        public virtual bool Matches(IItem item, XPathContextMajor context)
        {
            return alwaysMatches || pattern.MatchesItem(item, context);
        }
    }
}