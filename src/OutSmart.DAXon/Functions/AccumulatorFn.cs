////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Non-streaming implementation of accumulator-before() and accumulator-after()
    /// </summary>
    public abstract class AccumulatorFn : SystemFunction
    {

        public abstract Phase GetPhase();
        private ISequence GetAccumulatorValue(string name, Phase phase, IXPathContext context)
        {
            AccumulatorRegistry registry = GetRetainedStaticContext().GetPackageData().AccumulatorRegistry;
            Accumulator accumulator = GetAccumulator(name, registry);
            IItem node = context.GetContextItem();
            if (node == null)
            {
                throw new XPathException("No context item for evaluation of accumulator function", "XTDE3350", context);
            }

            if (!(node is NodeInfo))
            {
                throw new XPathException("Context item for evaluation of accumulator function must be a node", "XTTE3360", context);
            }

            int kind = ((NodeInfo)node).GetNodeKind();
            if (kind == Types.Type.ATTRIBUTE || kind == Types.Type.NAMESPACE)
            {
                throw new XPathException("Context item for evaluation of accumulator function must not be an attribute or namespace node", "XTTE3360", context);
            }

            ISequence streamedAccVal = registry.GetStreamingAccumulatorValue((NodeInfo)node, accumulator, phase);
            if (streamedAccVal != null)
            {
                return streamedAccVal;
            }

            ITreeInfo root = ((NodeInfo)node).GetTreeInfo();
            XsltController controller = (XsltController)context.GetController();
            if (!accumulator.IsUniversallyApplicable() && !controller.GetAccumulatorManager().IsApplicable(root, accumulator))
            {
                throw new XPathException("Accumulator " + name + " is not applicable to the current document", "XTDE3362");
            }

            AccumulatorManager manager = controller.GetAccumulatorManager();
            IIAccumulatorData data = manager.GetAccumulatorData(root, accumulator, context);
            return data.GetValue((NodeInfo)node, phase == Phase.AFTER);
        }

        private Accumulator GetAccumulator(string name, AccumulatorRegistry registry)
        {
            StructuredQName qName;
            try
            {
                qName = StructuredQName.FromLexicalQName(name, false, true, GetRetainedStaticContext());
            }
            catch (XPathException err)
            {
                throw new XPathException("Invalid accumulator name: " + err.Message, "XTDE3340");
            }

            Accumulator accumulator = registry == null ? null : registry.GetAccumulator(qName);
            if (accumulator == null)
            {
                throw new XPathException("Accumulator " + name + " has not been declared", "XTDE3340");
            }

            return accumulator;
        }

        public override ItemType GetResultItemType(Expression[] args)
        {
            try
            {
                if (args[0] is StringLiteral)
                {
                    AccumulatorRegistry registry = GetRetainedStaticContext().GetPackageData().AccumulatorRegistry;
                    Accumulator accumulator = GetAccumulator(((StringLiteral)args[0]).Stringify(), registry);
                    return accumulator.GetType().PrimaryType;
                }
            }
            catch (Exception e)
            {
            }

            return base.GetResultItemType(args);
        }

        //
        public override int GetCardinality(Expression[] args)
        {
            try
            {
                if (args[0] is StringLiteral)
                {
                    AccumulatorRegistry registry = GetRetainedStaticContext().GetPackageData().AccumulatorRegistry;
                    Accumulator accumulator = GetAccumulator(((StringLiteral)args[0]).Stringify(), registry);
                    return accumulator.GetType().GetCardinality();
                }
            }
            catch (Exception e)
            {
            }

            return base.GetCardinality(args);
        }

        //
        //
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            string name = arguments[0].Head().GetStringValue();
            return GetAccumulatorValue(name, GetPhase(), context);
        }
        public enum Phase
        {
            AFTER,
            BEFORE,
            UNSPECIFIED
        }

        internal sealed class AccumulatorBefore : AccumulatorFn
        {
            public AccumulatorBefore()
            {
            }

            public override Phase GetPhase()
            {
                return Phase.BEFORE;
            }
        }

        //
        //
        internal sealed class AccumulatorAfter : AccumulatorFn
        {

            public override string StreamerName => "AccumulatorAfter";
            public override Phase GetPhase()
            {
                return Phase.AFTER;
            }
        }
    }
}
