////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Json
{
    /// <summary>
    /// Implements the json-to-xml function defined in XSLT 3.0.
    /// </summary>
    internal class JsonToXMLFn : SystemFunction
    {
        public static OptionsParameter OPTION_DETAILS;
        static JsonToXMLFn()
        {
            SpecificFunctionType fallbackType = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_STRING }, SequenceType.SINGLE_STRING);
            OptionsParameter jsonToXmlOptions = new OptionsParameter();
            jsonToXmlOptions.AddAllowedOption("liberal", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            jsonToXmlOptions.AddAllowedOption("duplicates", SequenceType.SINGLE_STRING, null);
            jsonToXmlOptions.SetAllowedValues("duplicates", "FOJS0005", "reject", "use-first", "retain");
            jsonToXmlOptions.AddAllowedOption("validate", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            jsonToXmlOptions.AddAllowedOption("escape", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            jsonToXmlOptions.AddAllowedOption("fallback", SequenceType.MakeSequenceType(fallbackType, StaticProperty.EXACTLY_ONE), null);
            OPTION_DETAILS = jsonToXmlOptions;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IItem arg0 = arguments[0].Head();
            if (arg0 == null)
            {
                return EmptySequence.GetInstance();
            }

            string input = arg0.GetStringValue();
            MapItem options = null;
            if (GetArity() == 2)
            {
                options = (MapItem)arguments[1].Head();
            }

            IItem result = Eval(input, options, context);
            return SequenceTool.ItemOrEmpty(result);
        }

        protected virtual IItem Eval(string input, MapItem options, IXPathContext context)
        {
            JsonParser parser = new JsonParser();
            Dictionary<string, IGroundedValue> checkedOptions = null;
            int flags;
            if (options != null)
            {
                checkedOptions = Details.optionDetails.ProcessSuppliedOptions(options, context);
                flags = JsonParser.GetFlags(checkedOptions, true, context.GetController().GetExecutable().IsSchemaAware());
                if ((flags & JsonParser.DUPLICATES_LAST) != 0)
                {
                    throw new XPathException("json-to-xml: duplicates=use-last is not allowed", "FOJS0005");
                }

                if ((flags & JsonParser.DUPLICATES_SPECIFIED) == 0)
                {
                    if ((flags & JsonParser.VALIDATE) != 0)
                    {
                        flags |= JsonParser.DUPLICATES_REJECTED;
                    }
                    else
                    {
                        flags |= JsonParser.DUPLICATES_RETAINED;
                    }
                }
            }
            else
            {
                flags = JsonParser.DUPLICATES_RETAINED;
            }

            JsonHandlerXML handler = new JsonHandlerXML(context, StaticBaseUriString, flags);
            if (options != null)
            {
                handler.SetFallbackFunction(checkedOptions, context);
            }

            parser.Parse(input, flags, handler, context);
            return (IItem)handler.GetResult();
        }
    }
}
