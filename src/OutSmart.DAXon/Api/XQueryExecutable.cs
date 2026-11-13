////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using System;
namespace OutSmart.DAXon.Api
{
    public class XQueryExecutable
    {
        readonly Processor processor;
        readonly XQueryExpression exp;

        public virtual ItemType ResultItemType
        {
            get
            {
                Types.ItemType it = exp.GetExpression().GetItemType();
                return new ConstructedItemType(it, processor.UnderlyingConfiguration);
            }
        }

        public virtual OccurrenceIndicator ResultCardinality
        {
            get
            {
                int card = exp.GetExpression().GetCardinality();
                return OccurrenceIndicatorHelper.GetOccurrenceIndicator(card);
            }
        }

        public virtual XQueryExpression UnderlyingCompiledQuery => exp;
        public XQueryExecutable(Processor processor, XQueryExpression exp)
        {
            ArgumentCheck.NotNull(processor, nameof(processor));
            ArgumentCheck.NotNull(exp, nameof(exp));
            this.processor = processor;
            this.exp = exp;
        }

        public virtual XQueryEvaluator Load()
        {
            return new XQueryEvaluator(processor, exp);
        }

        public virtual bool IsUpdateQuery()
        {
            return exp.IsUpdateQuery();
        }

        public virtual void Explain(IDestination destination)
        {
            ArgumentCheck.NotNull(destination, nameof(destination));
            Configuration config = processor.UnderlyingConfiguration;
            try
            {
                PipelineConfiguration pipe = config.MakePipelineConfiguration();
                exp.Explain(new ExpressionPresenter(config, destination.GetReceiver(pipe, config.ObtainDefaultSerializationProperties())));
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }
    }
}