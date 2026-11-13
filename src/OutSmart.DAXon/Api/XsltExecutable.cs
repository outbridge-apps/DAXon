////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using OutSmart.DAXon.Internal;
using System.IO;
using System;
namespace OutSmart.DAXon.Api
{
    public class XsltExecutable
    {
        private readonly Processor processor;
        private readonly PreparedStylesheet preparedStylesheet;

        public virtual Dictionary<QName, ParameterDetails> GlobalParameters
        {
            get
            {
                Dictionary<StructuredQName, GlobalParam> globals = preparedStylesheet.GlobalParameters;
                Dictionary<QName, ParameterDetails> @params = new Dictionary<QName, ParameterDetails>();
                foreach (GlobalParam v in globals.Values)
                {
                    ParameterDetails details = new ParameterDetails(processor, v.GetRequiredType(), v.IsRequiredParam());
                    @params.PutAndGetPrevious(new QName(v.GetVariableQName()), details);
                }

                return @params;
            }
        }

        public virtual PreparedStylesheet UnderlyingCompiledStylesheet => preparedStylesheet;
        public XsltExecutable(Processor processor, PreparedStylesheet preparedStylesheet)
        {
            ArgumentCheck.NotNull(processor, nameof(processor));
            ArgumentCheck.NotNull(preparedStylesheet, nameof(preparedStylesheet));
            this.processor = processor;
            this.preparedStylesheet = preparedStylesheet;
        }

        public virtual Processor GetProcessor()
        {
            return processor;
        }

        public virtual XsltTransformer Load()
        {
            XsltTransformer xt = new XsltTransformer(processor, preparedStylesheet.NewController(), preparedStylesheet.CompileTimeParams);
            StructuredQName initialTemplate = preparedStylesheet.DefaultInitialTemplateName;
            if (initialTemplate != null)
            {
                xt.InitialTemplate = new QName(initialTemplate);
            }

            return xt;
        }

        public virtual Xslt30Transformer Load30()
        {
            return new Xslt30Transformer(processor, preparedStylesheet.NewController(), preparedStylesheet.CompileTimeParams);
        }

        public virtual void Explain(IDestination destination)
        {
            ArgumentCheck.NotNull(destination, nameof(destination));
            Configuration config = processor.UnderlyingConfiguration;
            try
            {
                IReceiver @out = destination.GetReceiver(config.MakePipelineConfiguration(), config.ObtainDefaultSerializationProperties());
                preparedStylesheet.Explain(new ExpressionPresenter(config, @out));
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public virtual void Export(System.IO.Stream destination)
        {
            string target = preparedStylesheet.GetTopLevelPackage().TargetEdition;
            if (target == null)
            {
                target = GetProcessor().DAXonEdition;
            }

            Export(destination, target);
        }

        public virtual void Export(System.IO.Stream destination, string target)
        {
            Configuration config = processor.UnderlyingConfiguration;
            try
            {
                StylesheetPackage topLevelPackage = preparedStylesheet.GetTopLevelPackage();
                ExpressionPresenter presenter = config.NewExpressionExporter(target, destination, topLevelPackage);
                presenter.GetOptions().relocatable = topLevelPackage.IsRelocatable();
                topLevelPackage.Export(presenter);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }

            try
            {
                destination.Dispose();
            }
            catch (IOException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public virtual WhitespaceStrippingPolicy GetWhitespaceStrippingPolicy()
        {
            StylesheetPackage top = preparedStylesheet.GetTopLevelPackage();
            if (top.IsStripsWhitespace())
            {
                return new WhitespaceStrippingPolicy(top);
            }
            else
            {
                return WhitespaceStrippingPolicy.UNSPECIFIED;
            }
        }

        public class ParameterDetails
        {
            private readonly Processor processor;
            private readonly Values.SequenceType type;
            private readonly bool required;

            public virtual ItemType DeclaredItemType => new ConstructedItemType(type.PrimaryType, processor.UnderlyingConfiguration);

            public virtual OccurrenceIndicator DeclaredCardinality => OccurrenceIndicatorHelper.GetOccurrenceIndicator(type.GetCardinality());

            public virtual Values.SequenceType UnderlyingDeclaredType => type;
            public ParameterDetails(Processor processor, Values.SequenceType type, bool isRequired)
            {
                this.processor = processor;
                this.type = type;
                this.required = isRequired;
            }

            public virtual bool IsRequired()
            {
                return this.required;
            }
        }
    }
}