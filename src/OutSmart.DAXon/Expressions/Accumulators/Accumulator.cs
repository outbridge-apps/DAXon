////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Accumulators
{
    /// <summary>
    /// Represents a single accumulator declared in an XSLT 3.0 stylesheet
    /// </summary>
    public class Accumulator : Actor
    {
        private StructuredQName accumulatorName;
        private SimpleMode preDescentRules;
        private SimpleMode postDescentRules;
        private Expression initialValueExpression;
        private SequenceType type;
        private bool streamable;
        private bool universallyApplicable;
        private int importPrecedence;
        private bool tracing;
        private SlotManager slotManagerForInitialValueExpression;

        public virtual StructuredQName AccumulatorName
        {
            get => accumulatorName; set
            {
                this.accumulatorName = value;
            }
        }

        public virtual int ImportPrecedence
        {
            get => importPrecedence; set
            {
                this.importPrecedence = value;
            }
        }

        public virtual SlotManager SlotManagerForInitialValueExpression
        {
            get => slotManagerForInitialValueExpression; set
            {
                this.slotManagerForInitialValueExpression = value;
            }
        }

        public virtual SimpleMode PreDescentRules
        {
            get => preDescentRules; set
            {
                this.preDescentRules = value;
            }
        }

        public virtual SimpleMode PostDescentRules
        {
            get => postDescentRules; set
            {
                this.postDescentRules = value;
            }
        }

        public virtual Expression InitialValueExpression
        {
            get => initialValueExpression; set
            {
                this.initialValueExpression = value;
            }
        }
        public Accumulator()
        {
            preDescentRules = new SimpleMode(new StructuredQName("saxon", NamespaceUri.SAXON, "preDescent"));
            postDescentRules = new SimpleMode(new StructuredQName("saxon", NamespaceUri.SAXON, "postDescent"));

            // The "body" of an accumulator is an artificial expression that contains all the constituent expressions, for ease of management.
            body = Literal.MakeEmptySequence();
        }

        public override SymbolicName GetSymbolicName()
        {
            return new SymbolicName(StandardNames.XSL_ACCUMULATOR, AccumulatorName);
        }

        public virtual bool IsDeclaredStreamable()
        {
            return streamable;
        }

        public virtual void SetDeclaredStreamable(bool streamable)
        {
            this.streamable = streamable;
        }

        public virtual void SetUniversallyApplicable(bool universal)
        {
            this.universallyApplicable = universal;
        }

        public virtual bool IsUniversallyApplicable()
        {
            return universallyApplicable;
        }

        public virtual bool IsTracing()
        {
            return tracing;
        }

        public virtual void SetTracing(bool tracing)
        {
            this.tracing = tracing;
        }

        public virtual void AddChildExpression(Expression expression)
        {
            Expression e = Block.MakeBlock(GetBody(), expression);
            SetBody(e);
        }

        public virtual SequenceType GetType()
        {
            return type;
        }

        public virtual void SetType(SequenceType type)
        {
            this.type = type;
        }

        public virtual bool IsCompatible(Accumulator other)
        {
            return AccumulatorName.Equals(other.AccumulatorName);
        }

        public virtual StructuredQName GetObjectName()
        {
            return accumulatorName;
        }

        public override void Export(ExpressionPresenter presenter)
        {
            Export(presenter, null);
        }

        public virtual void Export(ExpressionPresenter @out, Dictionary<Component, int> componentIdMap)
        {
            @out.StartElement("accumulator");
            @out.EmitAttribute("name", GetObjectName());
            @out.EmitAttribute("line", GetLineNumber() + "");
            @out.EmitAttribute("module", GetSystemId());
            @out.EmitAttribute("as", type.ToAlphaCode());
            @out.EmitAttribute("streamable", streamable ? "1" : "0");
            @out.EmitAttribute("slots", SlotManagerForInitialValueExpression.NumberOfVariables + "");
            if (componentIdMap != null)
            {
                @out.EmitAttribute("binds", "" + DeclaringComponent.ListComponentReferences(componentIdMap));
            }

            if (IsUniversallyApplicable())
            {
                @out.EmitAttribute("flags", "u");
            }

            @out.SetChildRole("init");
            initialValueExpression.Export(@out);
            Mode.IRuleAction action = (r) =>
            {
                @out.StartElement("accRule");
                @out.EmitAttribute("slots", ((AccumulatorRule)r.GetAction()).GetStackFrameMap().NumberOfVariables + "");
                @out.EmitAttribute("rank", "" + r.Rank);
                if (((AccumulatorRule)r.GetAction()).IsCapturing())
                {
                    @out.EmitAttribute("flags", "c");
                }

                r.Pattern.Export(@out);
                r.GetAction().Export(@out);
                @out.EndElement();
            };
            try
            {
                @out.StartElement("pre");
                @out.EmitAttribute("slots", preDescentRules.GetStackFrameSlotsNeeded() + "");
                preDescentRules.ProcessRules(action);
                @out.EndElement();
                @out.StartElement("post");
                @out.EmitAttribute("slots", postDescentRules.GetStackFrameSlotsNeeded() + "");
                postDescentRules.ProcessRules(action);
                @out.EndElement();
            }
            catch (XPathException e)
            {
                throw new InvalidOperationException(e.Message, e);
            }

            @out.EndElement();
        }
    }
}
