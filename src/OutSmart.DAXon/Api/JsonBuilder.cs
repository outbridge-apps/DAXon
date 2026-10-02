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

        public virtual XdmValue ParseJson(TextReader jsonReader)
        {
            // A standalone parse runs outside any transformation, so it must claim the Processor's
            // budget for itself - every other API entry does (see Controller.ArmThreadDeadline).
            // Without this the JSON path was the one entry point with no time limit at all.
            Controller.DeadlineToken prevDeadline = Controller.ArmThreadDeadline(config, "Parsing");
            try
            {
                IXPathContext context = new Controller(config).NewXPathContext();
                IIntPredicateProxy checker = IntSetPredicate.ALWAYS_TRUE;
                UnicodeString content = UnparsedTextFunction.ReadFile(checker, jsonReader);
                Dictionary<string, IGroundedValue> options = new Dictionary<string, IGroundedValue>();
                options["liberal"] = BooleanValue.Get(liberal);
                options["escape"] = BooleanValue.TRUE;
                string json = content.ToString();
                InputSizeLimit.CheckString(json, InputSizeLimit.MaxFor(config), "urn:json-input", "FODC0002");
                IItem result = ParseJsonFn.Parse(json, options, context);
                return XdmValue.Wrap(result);
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
                IXPathContext context = new Controller(config).NewXPathContext();
                Dictionary<string, IGroundedValue> options = new Dictionary<string, IGroundedValue>();
                options["liberal"] = BooleanValue.Get(liberal);
                options["escape"] = BooleanValue.TRUE;
                InputSizeLimit.CheckString(json, InputSizeLimit.MaxFor(config), "urn:json-input", "FODC0002");
                return XdmValue.Wrap(ParseJsonFn.Parse(json, options, context));
            }
            catch (XPathException e) { throw new DAXonApiException(e); }
            catch (RecursionDepthError e) { throw new DAXonApiException(e.ToXPathException()); }
            finally
            {
                Controller.RestoreThreadDeadline(prevDeadline);
            }
        }

        // Consumer-compat alias: a JSON document is always one item (map/array/atomic).
        public virtual XdmItem Build(string json) => (XdmItem)ParseJson(json);
    }
}