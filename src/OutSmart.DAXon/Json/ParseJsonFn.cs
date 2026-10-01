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
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Json
{
    internal sealed class ParseJsonFn : JsonToXMLFn
    {
        public static OptionsParameter OPTION_DETAILS;
        static ParseJsonFn()
        {
            SpecificFunctionType fallbackType = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_STRING }, SequenceType.SINGLE_STRING);
            SpecificFunctionType parserType = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_STRING }, SequenceType.SINGLE_ATOMIC);
            OptionsParameter parseJsonOptions = new OptionsParameter();
            parseJsonOptions.AddAllowedOption("liberal", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            parseJsonOptions.AddAllowedOption("duplicates", SequenceType.SINGLE_STRING, StringValue.Bmp("use-first"));
            parseJsonOptions.SetAllowedValues("duplicates", "FOJS0005", "reject", "use-first", "use-last");
            parseJsonOptions.AddAllowedOption("escape", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            parseJsonOptions.AddAllowedOption("fallback", SequenceType.MakeSequenceType(fallbackType, StaticProperty.EXACTLY_ONE), null);
            parseJsonOptions.AddAllowedOption("number-parser", SequenceType.MakeSequenceType(parserType, StaticProperty.EXACTLY_ONE), null);
            OPTION_DETAILS = parseJsonOptions;
        }

        protected override IItem Eval(string input, MapItem options, IXPathContext context)
        {
            Dictionary<string, IGroundedValue> checkedOptions = null;
            if (options != null)
            {
                checkedOptions = Details.optionDetails.ProcessSuppliedOptions(options, context);
            }

            return Parse(input, checkedOptions, context);
        }

        public static IItem Parse(string input, Dictionary<string, IGroundedValue> options, IXPathContext context)
        {
            JsonParser parser = new JsonParser();
            int flags = 0;
            if (options != null)
            {
                flags = JsonParser.GetFlags(options, false, false);
            }

            JsonHandlerMap handler = new JsonHandlerMap(context, flags);
            if ((flags & JsonParser.DUPLICATES_RETAINED) != 0)
            {
                throw new XPathException("parse-json: duplicates=retain is not allowed", "FOJS0005");
            }

            if ((flags & JsonParser.DUPLICATES_SPECIFIED) == 0)
            {
                flags |= JsonParser.DUPLICATES_FIRST;
            }

            if (options != null)
            {
                handler.SetFallbackFunction(options, context);
                parser.SetNumberParser(options, context);
            }

            parser.Parse(input, flags, handler, context);
            return handler.GetResult().Head();
        }
    }
}