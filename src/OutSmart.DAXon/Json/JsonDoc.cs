////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.IO;
namespace OutSmart.DAXon.Json
{
    /// <summary>
    /// Implements the json-to-xml function defined in XSLT 3.0.
    /// </summary>
    internal sealed class JsonDoc : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IItem arg0 = arguments[0].Head();
            if (arg0 == null)
            {
                return EmptySequence.GetInstance();
            }

            string href = arg0.GetStringValue();
            // json-doc applies no XML-character validation (bug 3911) and needs a string for the
            // parser, so it reads straight to a string (ReadFileToString) instead of building a
            // codepoint UnicodeString and converting it back.

            // Use the URI machinery to validate and resolve the URIs
            URI absoluteURI = UnparsedTextFunction.GetAbsoluteURI(href, StaticBaseUriString, context);
            // Encoding is INFERRED for json-doc (JSON is UTF-8, or UTF-16/32 by BOM — never user-supplied), so
            // pass null: a decode failure is then reported as FOUT1200 (inferred), not FOUT1190 (explicit).
            // JSONTestSuite i_string_* accept FOUT1200 but not FOUT1190.
            string encoding = null;
            TextReader reader;
            try
            {
                // As unparsed-text() opens it: through the resolvers, and a file that cannot be read says why.
                reader = UnparsedTextFunction.OpenText(absoluteURI, encoding, context, "json-doc()");
            }
            catch (XPathException err)
            {
                // json-doc's encoding is inferred, so a malformed-bytes failure the resolver reports as
                // FOUT1190 (its default for an explicit encoding) must surface as FOUT1200 for json-doc
                // (JSONTestSuite i_string_* accept FOUT1200, not FOUT1190).
                if (err.HasErrorCode("FOUT1190"))
                {
                    throw new XPathException(err.Message, "FOUT1200");
                }

                err.MaybeSetErrorCode("FOUT1170");
                throw;
            }

            string content;
            try
            {
                content = UnparsedTextFunction.ReadFileToString(reader);
            }
            catch (ArgumentException encErr)
            {
                throw new XPathException("Unknown encoding " + Err.Wrap(encoding), encErr).WithErrorCode("FOUT1190");
            }
            catch (IOException ioErr)
            {

                throw UnparsedTextFunction.HandleIOError(absoluteURI, ioErr);
            }

            Dictionary<string, IGroundedValue> checkedOptions;
            if (GetArity() == 2)
            {
                MapItem options = (MapItem)arguments[1].Head();
                checkedOptions = Details.optionDetails.ProcessSuppliedOptions(options, context);
            }
            else
            {
                checkedOptions = ParseJsonFn.OPTION_DETAILS.DefaultOptions;
            }

            IItem result = ParseJsonFn.Parse(content, checkedOptions, context);
            return SequenceTool.ItemOrEmpty(result);
        }
    }
}
