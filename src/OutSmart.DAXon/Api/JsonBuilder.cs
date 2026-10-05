////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Json;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Api
{
    public class JsonBuilder
    {
        private Configuration config;
        private bool liberal;
        private bool escaped;
        public JsonBuilder(Configuration config)
        {
            this.config = config;
        }

        public virtual void SetLiberal(bool liberal)
        {
            this.liberal = liberal;
        }

        public virtual bool IsLiberal()
        {
            return liberal;
        }

        // By default a JSON escape is the character it stands for, as fn:parse-json has it. true keeps the backslash and
        // what XML cannot hold as JSON escapes - all this builder did before 1.4, and what Saxon's does.
        public virtual void SetEscaped(bool escaped)
        {
            this.escaped = escaped;
        }

        public virtual bool IsEscaped()
        {
            return escaped;
        }

        public virtual XdmValue ParseJson(TextReader jsonReader)
        {
            // A standalone parse runs outside any transformation, so it must claim the Processor's
            // budget for itself - every other API entry does (see Controller.ArmThreadDeadline).
            // Without this the JSON path was the one entry point with no time limit at all.
            Controller.DeadlineToken prevDeadline = Controller.ArmThreadDeadline(config, "Parsing");
            try
            {
                // Capped as it is read: an oversized reader stops at the limit, not after a full read. Read
                // as json-doc() reads, without the XML character test ParseJson(string) never applied either.
                TextReader capped = InputSizeLimit.Apply(jsonReader, InputSizeLimit.MaxFor(config), "urn:json-input", "FODC0002");
                return Parse(UnparsedTextFunction.ReadFileToString(capped));
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
            catch (IOException e)
            {
                throw new DAXonApiException(e);
            }
            finally
            {
                Controller.RestoreThreadDeadline(prevDeadline);
            }
        }

        public virtual XdmValue ParseJson(string json)
        {
            Controller.DeadlineToken prevDeadline = Controller.ArmThreadDeadline(config, "Parsing");
            try
            {
                InputSizeLimit.CheckString(json, InputSizeLimit.MaxFor(config), "urn:json-input", "FODC0002");
                return Parse(json);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
            finally
            {
                Controller.RestoreThreadDeadline(prevDeadline);
            }
        }

        private XdmValue Parse(string json)
        {
            Dictionary<string, IGroundedValue> options = new Dictionary<string, IGroundedValue>();
            options["liberal"] = BooleanValue.Get(liberal);
            options["escape"] = BooleanValue.Get(escaped);
            // With no fallback or number-parser option the parse reads only the configuration: no Controller.
            return XdmValue.Wrap(ParseJsonFn.Parse(json, options, new EarlyEvaluationContext(config)));
        }

        // Consumer-compat alias: a JSON document is always one item (map/array/atomic).
        public virtual XdmItem Build(string json) => (XdmItem)ParseJson(json);
    }
}