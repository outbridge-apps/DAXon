////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Json;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Values.Arrays;
namespace OutSmart.DAXon.Functions
{
    // XPath 3.1 additions on top of 2.0+3.0 (upstream registry/XPath31FunctionSet.java). This is the set the
    // XQuery 3.1 static context binds; it is also consulted by the XPath parser when it rewrites a named
    // reference to a context-dependent function (fn:string#0 etc.) into function-lookup#2 — that entry arrives
    // via the XPath30 import. NOT registered (implementing classes not ported): copy-of, snapshot, transform,
    // load-xquery-module — they keep raising XPST0017 exactly as before.
    internal sealed class XPath31FunctionSet : BuiltInFunctionSet
    {
        private static readonly XPath31FunctionSet _i = new XPath31FunctionSet();
        public XPath31FunctionSet()
        {
            ImportFunctionSet(XPath20FunctionSet.GetInstance());
            ImportFunctionSet(XPath30FunctionSet.GetInstance());
            Register("parse-ietf-date", 1, (e) => e.Populate(() => new ParseIetfDate(), BuiltInAtomicType.DATE_TIME, OPT, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));

            // fn:copy-of / fn:snapshot (XSLT 3.0 F&O, XPath31FunctionSet.java:73-78/160-164) — classes were
            // missing from the port; now backed by the real VirtualCopy/SnapshotNode.
            Register("copy-of", 0, (e) => e.Populate(() => new CopyOfFn(), AnyItemType.GetInstance(), STAR, NEW));
            Register("copy-of", 1, (e) => e.Populate(() => new CopyOfFn(), AnyItemType.GetInstance(), STAR, NEW).Arg(0, AnyItemType.GetInstance(), STAR | ABS, EMPTY));
            Register("snapshot", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), AnyItemType.GetInstance(), STAR, CITEM | LATE | NEW));
            Register("snapshot", 1, (e) => e.Populate(() => new SnapshotFn(), AnyNodeTest.GetInstance(), STAR, NEW).Arg(0, AnyItemType.GetInstance(), STAR | ABS, EMPTY));

            Register("collation-key", 1, (e) => e.Populate(() => new CollationKeyFn(), BuiltInAtomicType.BASE64_BINARY, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, ONE, null));

            Register("collation-key", 2, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.BASE64_BINARY, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("xml-to-json", 1, (e) => e.Populate(() => new XMLToJsonFn(), BuiltInAtomicType.STRING, OPT, LATE).Arg(0, AnyNodeTest.GetInstance(), OPT | ABS, EMPTY)); // runtime: lambda form (not .New()) so Fix-Phase7-CtorRef-To-Lambda doesn't botch the global:: prefix on early-probe CS0117

            Register("xml-to-json", 2, (e) => e.Populate(() => new XMLToJsonFn(), BuiltInAtomicType.STRING, OPT, LATE).Arg(0, AnyNodeTest.GetInstance(), OPT | ABS, EMPTY).Arg(1, MapType.ANY_MAP_TYPE, ONE | ABS, null).SetOptionDetails(XMLToJsonFn.MakeOptionsParameter()));

            Register("default-language", 0, (e) => e.Populate(() => new DynamicContextAccessor.DefaultLanguage(), BuiltInAtomicType.LANGUAGE, ONE, LATE));

            // fn:transform (XPath 3.1) — sig from XPath31FunctionSet.java:197-199
            Register("transform", 1, (e) => e.Populate(() => new TransformFn(), MapType.ANY_MAP_TYPE, ONE, LATE).Arg(0, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(TransformFn.MakeOptionsParameter()));

            // fn:load-xquery-module (XPath 3.1) — sig from XPath31FunctionSet.java:210-214
            Register("load-xquery-module", 1, (e) => e.Populate(() => new OutSmart.DAXon.Functions.HigherOrder.LoadXqueryModule(), MapType.ANY_MAP_TYPE, ONE, LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("load-xquery-module", 2, (e) => e.Populate(() => new OutSmart.DAXon.Functions.HigherOrder.LoadXqueryModule(), MapType.ANY_MAP_TYPE, ONE, LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(OutSmart.DAXon.Functions.HigherOrder.LoadXqueryModule.MakeOptionsParameter()));

            Register("random-number-generator", 0, (e) => e.Populate(() => new OutSmart.DAXon.Functions.HigherOrder.RandomNumberGenerator(), OutSmart.DAXon.Functions.HigherOrder.RandomNumberGenerator.RETURN_TYPE, ONE, LATE));

            Register("random-number-generator", 1, (e) => e.Populate(() => new OutSmart.DAXon.Functions.HigherOrder.RandomNumberGenerator(), OutSmart.DAXon.Functions.HigherOrder.RandomNumberGenerator.RETURN_TYPE, ONE, LATE).Arg(0, BuiltInAtomicType.ANY_ATOMIC, OPT, null));

            Register("json-to-xml", 1, (e) => e.Populate(() => new JsonToXMLFn(), AnyItemType.GetInstance(), OPT, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, null));

            Register("json-to-xml", 2, (e) => e.Populate(() => new JsonToXMLFn(), AnyItemType.GetInstance(), OPT, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(JsonToXMLFn.OPTION_DETAILS));

            Register("parse-json", 1, (e) => e.Populate(() => new ParseJsonFn(), AnyItemType.GetInstance(), OPT, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));

            Register("parse-json", 2, (e) => e.Populate(() => new ParseJsonFn(), AnyItemType.GetInstance(), OPT, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(ParseJsonFn.OPTION_DETAILS));

            Register("json-doc", 1, (e) => e.Populate(() => new JsonDoc(), AnyItemType.GetInstance(), OPT, LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));

            Register("json-doc", 2, (e) => e.Populate(() => new JsonDoc(), AnyItemType.GetInstance(), OPT, LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(ParseJsonFn.OPTION_DETAILS));

            Register("contains-token", 2, (e) => e.Populate(() => new ContainsToken(), BuiltInAtomicType.BOOLEAN, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, STAR, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("contains-token", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.BOOLEAN, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, STAR, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));

        }
        public static XPath31FunctionSet GetInstance() => _i;
    }
}
