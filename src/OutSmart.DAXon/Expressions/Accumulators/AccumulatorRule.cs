////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation.Rules;
using System;
namespace OutSmart.DAXon.Expressions.Accumulators
{
    /// <summary>
    /// This class represents one of the rules making up the definition of an accumulator
    /// </summary>
    internal sealed class AccumulatorRule : IRuleTarget, ITraceableComponent
    {
        private Expression newValueExpression;
        private CachedEvaluator<ISequenceEvaluator> newValueEvaluator;
        private readonly SlotManager stackFrameMap;
        private readonly bool postDescent;
        private bool capturing;
        private ILocation location;
        private StructuredQName accumulatorName;

        public Expression NewValueExpression => newValueExpression;

        // Evaluated once per matching node: elaborated on first use, not on every firing.
        internal ISequenceEvaluator NewValueEvaluator => CachedEvaluator<ISequenceEvaluator>.Get(ref newValueEvaluator, newValueExpression, e => e.Eagerly());

        public string TracingTag => "xsl:accumulator-rule";
        public AccumulatorRule(Expression newValueExpression, SlotManager stackFrameMap, bool postDescent)
        {
            this.newValueExpression = newValueExpression;
            this.stackFrameMap = stackFrameMap;
            this.postDescent = postDescent;
        }

        public void Export(ExpressionPresenter @out)
        {
            newValueExpression.Export(@out);
        }

        public SlotManager GetStackFrameMap()
        {
            return stackFrameMap;
        }

        public void RegisterRule(Rule rule)
        {
        }

        public void SetCapturing(bool capturing)
        {
            this.capturing = capturing;
        }

        public bool IsCapturing()
        {
            return capturing;
        }

        public bool IsPostDescent()
        {
            return postDescent;
        }

        // ITraceableComponent interface
        public Expression GetBody()
        {
            return newValueExpression;
        }

        public void SetLocation(ILocation loc)
        {
            this.location = loc;
        }

        public ILocation GetLocation()
        {
            return location;
        }

        public StructuredQName GetObjectName()
        {
            return null;
        }

        public void SetBody(Expression expression)
        {
            newValueExpression = expression;
        }

        public void SetAccumulatorName(StructuredQName name)
        {
            this.accumulatorName = name;
        }

        public void GatherProperties(Action<string, object> consumer)
        {
            if (accumulatorName != null)
            {
                consumer("name",accumulatorName.DisplayName);
            }

            consumer("phase",IsPostDescent() ? "end" : "start");
        }
    }
}