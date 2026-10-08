////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
namespace OutSmart.DAXon.Api
{
    public abstract class SchemaValidator : AbstractDestination
    {
        public abstract IInvalidityHandler InvalidityHandler { get; set; }
        public abstract IDestination Destination { get; set; }
        public abstract QName DocumentElementName { get; set; }
        public abstract QName DocumentElementTypeName { get; set; }
        public abstract ISchemaType DocumentElementType { get; }
        public abstract ValidationParams ValidationParameters { get; }
        public abstract void SetLax(bool lax);
        public abstract bool IsLax();
        public abstract void SetCollectStatistics(bool collect);
        public abstract bool IsCollectStatistics();
        public abstract void ReportValidationStatistics(IDestination destination);
        public abstract void SetValidityReporting(IDestination destination);
        public abstract void SetUseXsiSchemaLocation(bool recognize);
        public abstract bool IsUseXsiSchemaLocation();
        public abstract void SetExpandAttributeDefaults(bool expand);
        public abstract bool IsExpandAttributeDefaults();
        public abstract void SetParameter(QName name, XdmValue value);
        public abstract XdmValue GetParameter(QName name);
        public abstract void Validate(ResolvedResource source);
        public abstract void ValidateMultiple(IEnumerable<ResolvedResource> sources);
        public virtual IActiveSource AsSource(ResolvedResource input)
        {
            return new AnonymousEventSource(this, input);
        }

        public override abstract IReceiver GetReceiver(PipelineConfiguration pipe, SerializationProperties @params);
        public override abstract void Close();

        private sealed class AnonymousEventSource : EventSource
        {

            private readonly SchemaValidator parent;
            private readonly ResolvedResource input;
            public AnonymousEventSource(SchemaValidator parent, ResolvedResource input)
            {
                this.parent = parent;
                this.input = input;
            }
            public override void Deliver(IReceiver @out, ParseOptions options)
            {
                parent.Destination = (IDestination)(new ReceivingDestination(@out));
                try
                {
                    parent.Validate(input);
                }
                catch (DAXonApiException e)
                {
                    throw XPathException.MakeXPathException(e);
                }
            }
        }
    }
}
