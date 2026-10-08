////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
namespace OutSmart.DAXon.Json
{
    /// <summary>
    /// Implement the XML to JSON conversion as a built-in function - fn:xml-to-json()
    /// </summary>
    internal sealed class XMLToJsonFn : SystemFunction, IPushableFunction
    {
        private static readonly IFunctionItemType formatterFunctionType = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_STRING }, SequenceType.SINGLE_STRING);

        public override string StreamerName => "XmlToJsonFn";
        public static OptionsParameter MakeOptionsParameter()
        {
            OptionsParameter xmlToJsonOptions = new OptionsParameter();
            xmlToJsonOptions.AddAllowedOption("indent", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            xmlToJsonOptions.AddAllowedOption("number-formatter", SequenceType.MakeSequenceType(formatterFunctionType, StaticProperty.ALLOWS_ZERO_OR_ONE), EmptySequence.GetInstance());
            return xmlToJsonOptions;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NodeInfo xml = (NodeInfo)arguments[0].Head();
            if (xml == null)
            {
                return EmptySequence.GetInstance();
            }

            Options options = GetOptions(context, arguments);
            PipelineConfiguration pipe = context.GetController().MakePipelineConfiguration();
            pipe.XPathContext = context;
            UniStringCollector uniBuffer = new UniStringCollector();
            ConvertToJson(xml, uniBuffer, options, context);
            return new StringValue(uniBuffer.ToUnicodeString());
        }

        private Options GetOptions(IXPathContext context, ISequence[] arguments)
        {
            if (GetArity() > 1)
            {
                MapItem suppliedOptions = (MapItem)arguments[1].Head();
                Dictionary<string, IGroundedValue> options = Details.optionDetails.ProcessSuppliedOptions(suppliedOptions, context);
                Options o = new Options();
                o.indent = ((BooleanValue)options.GetOrDefault("indent").Head()).GetBooleanValue();
                ISequence format = options.GetOrDefault("number-formatter");
                if (format != null)
                {
                    o.numberFormatter = (IFunctionItem)format.Head();
                }

                return o;
            }
            else
            {
                return new Options();
            }
        }

        public void Process(Outputter destination, IXPathContext context, ISequence[] arguments)
        {
            NodeInfo xml = (NodeInfo)arguments[0].Head();
            if (xml != null)
            {
                Options options = GetOptions(context, arguments);
                PipelineConfiguration pipe = context.GetController().MakePipelineConfiguration();
                pipe.XPathContext = context;
                ConvertToJson(xml, destination.GetStringReceiver(false, Loc.NONE), options, context);
            }
        }

        private void ConvertToJson(NodeInfo xml, IUniStringConsumer output, Options options, IXPathContext context)
        {
            PipelineConfiguration pipe = context.GetController().MakePipelineConfiguration();
            pipe.XPathContext = context;
            JsonReceiver receiver = new JsonReceiver(pipe, context, output);
            receiver.SetIndenting(options.indent);
            if (options.numberFormatter != null)
            {
                receiver.NumberFormatter = options.numberFormatter;
            }

            // TinyTree inputs walk directly (JsonTreeWalker) - same events and errors without the
            // generic Copy replay's per-element name/attribute-map/namespace allocations.
            if (JsonTreeWalker.TryWalk(xml, receiver, context.GetController()))
            {
                return;
            }

            IReceiver r = receiver;
            if (xml.GetNodeKind() == Types.Type.DOCUMENT)
            {
                r = new DocumentValidator(r, "FOJS0006");
            }

            r.Open();
            xml.Copy(r, 0, Loc.NONE);
            r.Close();
        }

        private sealed class Options
        {
            public bool indent;
            public IFunctionItem numberFormatter;
        }
    }
}
