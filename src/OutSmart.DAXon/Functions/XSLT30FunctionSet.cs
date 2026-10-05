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
    internal sealed class XSLT30FunctionSet : BuiltInFunctionSet
    {
        private static readonly XSLT30FunctionSet _i = new XSLT30FunctionSet();
        // Runtime bring-up: scoped registration of the bootstrap functions KeyManager.RegisterIdrefKey needs
        // (fn:string#1 + fn:tokenize#1), pointing at the real elaborator-free function classes. This is the
        // documented "tactical unblock" (runtime-function-library-plan.md) — it lets compiler startup proceed
        // and reveals the next frontier WITHOUT the full XPath20FunctionSet re-include (which drags the
        // StringElaborator compile cluster that explodes the pipeline). Mirrors the real Register signatures.
        public XSLT30FunctionSet()
        {
            Register("string", 1, (e) => e.Populate(String_1.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, Types.Type.ITEM_TYPE, OPT | ABS, StringValue.EMPTY_STRING));
            Register("tokenize", 1, (e) => e.Populate(Tokenize_1.New(), BuiltInAtomicType.STRING, STAR, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));
            // string-to-codepoints#1 — exact signature from XPath20FunctionSet:485 (integer*, arg string? default EMPTY).
            Register("string-to-codepoints", 1, (e) => e.Populate(() => new StringToCodepoints(), BuiltInAtomicType.INTEGER, STAR, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY)); // runtime: lambda form (not .New()) so Fix-Phase7-CtorRef-To-Lambda doesn't botch the global:: prefix
            // QName family - exact signatures from XPath20FunctionSet:289/360/398/401 (lambda form, same CtorRef trap).
            Register("QName", 2, (e) => e.Populate(() => new QNameFn(), BuiltInAtomicType.QNAME, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("local-name-from-QName", 1, (e) => e.Populate(() => new AccessorFn.LocalNameFromQName(), BuiltInAtomicType.NCNAME, OPT, 0).Arg(0, BuiltInAtomicType.QNAME, OPT, EMPTY));
            Register("namespace-uri-from-QName", 1, (e) => e.Populate(() => new AccessorFn.NamespaceUriFromQName(), BuiltInAtomicType.ANY_URI, OPT, 0).Arg(0, BuiltInAtomicType.QNAME, OPT, EMPTY));
            Register("prefix-from-QName", 1, (e) => e.Populate(() => new AccessorFn.PrefixFromQName(), BuiltInAtomicType.NCNAME, OPT, 0).Arg(0, BuiltInAtomicType.QNAME, OPT, EMPTY));
            // resolve-QName (re-included 2026-06-17) - exact signature from XPath20FunctionSet:170.
            Register("resolve-QName", 2, (e) => e.Populate(() => new ResolveQName(), BuiltInAtomicType.QNAME, OPT, CARD0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, NodeKindTest.ELEMENT, ONE | INS, null));
            // fn:error (re-included 2026-06-17) - raises XPathException with code (default FOER0000); arities 0-3 per XPath20FunctionSet:99-106. Lambda form: real Error has no New() (the registry's Exception.New() is a transpiler class-literal mangle).
            Register("error", 0, (e) => e.Populate(() => new Error(), Types.Type.ITEM_TYPE, OPT, LATE));
            Register("error", 1, (e) => e.Populate(() => new Error(), Types.Type.ITEM_TYPE, OPT, LATE).Arg(0, BuiltInAtomicType.QNAME, OPT, null));
            Register("error", 2, (e) => e.Populate(() => new Error(), Types.Type.ITEM_TYPE, OPT, LATE).Arg(0, BuiltInAtomicType.QNAME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("error", 3, (e) => e.Populate(() => new Error(), Types.Type.ITEM_TYPE, OPT, LATE).Arg(0, BuiltInAtomicType.QNAME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, Types.Type.ITEM_TYPE, STAR, null));
            // fn:trace (re-included 2026-06-17) - value passthrough + TraceListener side-effect; arities 1-2 per XPath31FunctionSet:82 / XPath20FunctionSet:208.
            Register("trace", 1, (e) => e.Populate(() => new Trace(), Types.Type.ITEM_TYPE, STAR, AS_ARG0 | LATE).Arg(0, Types.Type.ITEM_TYPE, STAR | TRA, null));
            Register("trace", 2, (e) => e.Populate(() => new Trace(), Types.Type.ITEM_TYPE, STAR, AS_ARG0 | LATE).Arg(0, Types.Type.ITEM_TYPE, STAR | TRA, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            // fn:normalize-unicode#1/#2 + fn:iri-to-uri#1 (re-included 2026-06-17) - exact signatures from XPath20FunctionSet:159-160/123.
            Register("normalize-unicode", 1, (e) => e.Populate(() => new NormalizeUnicode(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));
            Register("normalize-unicode", 2, (e) => e.Populate(() => new NormalizeUnicode(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("iri-to-uri", 1, (e) => e.Populate(() => new IriToUri(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));
            // fn:escape-html-uri (F&O 6.4) - class was missing from the port, so it was unregistered.
            Register("escape-html-uri", 1, (e) => e.Populate(() => new EscapeHtmlUri(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));
            // fn:parse-ietf-date (2026-06-18) - XPath 3.1 fn; registered in the XSLT set (the actual XSLT
            // resolution path is GetXSLTFunctionSet -> XSLT30FunctionSet, NOT GetXPathFunctionSet). Faithful sig per XPath31FunctionSet.java.
            Register("parse-ietf-date", 1, (e) => e.Populate(() => new ParseIetfDate(), BuiltInAtomicType.DATE_TIME, OPT, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));
            // Date/time component accessors (2026-06-17) - 21 AccessorFn subclasses, already compiled; exact sigs from XPath20FunctionSet:73-215.
            Register("year-from-date", 1, (e) => e.Populate(() => new AccessorFn.YearFromDate(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, EMPTY));
            Register("year-from-dateTime", 1, (e) => e.Populate(() => new AccessorFn.YearFromDateTime(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY));
            Register("month-from-date", 1, (e) => e.Populate(() => new AccessorFn.MonthFromDate(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, EMPTY));
            Register("month-from-dateTime", 1, (e) => e.Populate(() => new AccessorFn.MonthFromDateTime(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY));
            Register("day-from-date", 1, (e) => e.Populate(() => new AccessorFn.DayFromDate(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, EMPTY));
            Register("day-from-dateTime", 1, (e) => e.Populate(() => new AccessorFn.DayFromDateTime(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY));
            Register("hours-from-dateTime", 1, (e) => e.Populate(() => new AccessorFn.HoursFromDateTime(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY));
            Register("hours-from-time", 1, (e) => e.Populate(() => new AccessorFn.HoursFromTime(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, EMPTY));
            Register("minutes-from-dateTime", 1, (e) => e.Populate(() => new AccessorFn.MinutesFromDateTime(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY));
            Register("minutes-from-time", 1, (e) => e.Populate(() => new AccessorFn.MinutesFromTime(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, EMPTY));
            Register("seconds-from-dateTime", 1, (e) => e.Populate(() => new AccessorFn.SecondsFromDateTime(), BuiltInAtomicType.DECIMAL, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY));
            Register("seconds-from-time", 1, (e) => e.Populate(() => new AccessorFn.SecondsFromTime(), BuiltInAtomicType.DECIMAL, OPT, CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, EMPTY));
            Register("timezone-from-date", 1, (e) => e.Populate(() => new AccessorFn.TimezoneFromDate(), BuiltInAtomicType.DAY_TIME_DURATION, OPT, 0).Arg(0, BuiltInAtomicType.DATE, OPT, EMPTY));
            Register("timezone-from-dateTime", 1, (e) => e.Populate(() => new AccessorFn.TimezoneFromDateTime(), BuiltInAtomicType.DAY_TIME_DURATION, OPT, 0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY));
            Register("timezone-from-time", 1, (e) => e.Populate(() => new AccessorFn.TimezoneFromTime(), BuiltInAtomicType.DAY_TIME_DURATION, OPT, 0).Arg(0, BuiltInAtomicType.TIME, OPT, EMPTY));
            Register("years-from-duration", 1, (e) => e.Populate(() => new AccessorFn.YearsFromDuration(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DURATION, OPT, EMPTY));
            Register("months-from-duration", 1, (e) => e.Populate(() => new AccessorFn.MonthsFromDuration(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DURATION, OPT, EMPTY));
            Register("days-from-duration", 1, (e) => e.Populate(() => new AccessorFn.DaysFromDuration(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DURATION, OPT, EMPTY));
            Register("hours-from-duration", 1, (e) => e.Populate(() => new AccessorFn.HoursFromDuration(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DURATION, OPT, EMPTY));
            Register("minutes-from-duration", 1, (e) => e.Populate(() => new AccessorFn.MinutesFromDuration(), BuiltInAtomicType.INTEGER, OPT, CARD0).Arg(0, BuiltInAtomicType.DURATION, OPT, EMPTY));
            Register("seconds-from-duration", 1, (e) => e.Populate(() => new AccessorFn.SecondsFromDuration(), BuiltInAtomicType.DECIMAL, OPT, CARD0).Arg(0, BuiltInAtomicType.DURATION, OPT, EMPTY));
            // adjust-*-to-timezone + dateTime() constructor (re-included 2026-06-17) - sigs from XPath20FunctionSet:42-47,72.
            Register("adjust-date-to-timezone", 1, (e) => e.Populate(() => new Adjust_1(), BuiltInAtomicType.DATE, OPT, LATE | CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, EMPTY));
            Register("adjust-date-to-timezone", 2, (e) => e.Populate(() => new Adjust_2(), BuiltInAtomicType.DATE, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, EMPTY).Arg(1, BuiltInAtomicType.DAY_TIME_DURATION, OPT, null));
            Register("adjust-dateTime-to-timezone", 1, (e) => e.Populate(() => new Adjust_1(), BuiltInAtomicType.DATE_TIME, OPT, LATE | CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY));
            Register("adjust-dateTime-to-timezone", 2, (e) => e.Populate(() => new Adjust_2(), BuiltInAtomicType.DATE_TIME, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY).Arg(1, BuiltInAtomicType.DAY_TIME_DURATION, OPT, null));
            Register("adjust-time-to-timezone", 1, (e) => e.Populate(() => new Adjust_1(), BuiltInAtomicType.TIME, OPT, LATE | CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, EMPTY));
            Register("adjust-time-to-timezone", 2, (e) => e.Populate(() => new Adjust_2(), BuiltInAtomicType.TIME, OPT, CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, EMPTY).Arg(1, BuiltInAtomicType.DAY_TIME_DURATION, OPT, null));
            Register("dateTime", 2, (e) => e.Populate(() => new DateTimeConstructor(), BuiltInAtomicType.DATE_TIME, OPT, 0).Arg(0, BuiltInAtomicType.DATE, OPT, EMPTY).Arg(1, BuiltInAtomicType.TIME, OPT, EMPTY));
            // fn:number#1 - exact signature from XPath20FunctionSet:390 (double, arg anyAtomic? default NaN). #0 needs excluded ContextItemAccessorFunction.
            Register("number", 1, (e) => e.Populate(() => new Number_1(), BuiltInAtomicType.DOUBLE, ONE, 0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, OPT, DoubleValue.NaN));
            // Core XPath batch 1 (2026-06-10) - exact XPath20FunctionSet signatures. concat deferred (registerVariadic/SEQV machinery).
            Register("not", 1, (e) => e.Populate(() => new NotFn(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, Types.Type.ITEM_TYPE, STAR | INS, BooleanValue.TRUE));
            Register("boolean", 1, (e) => e.Populate(() => new BooleanFn(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, Types.Type.ITEM_TYPE, STAR | INS, null));
            Register("exists", 1, (e) => e.Populate(() => new Exists(), BuiltInAtomicType.BOOLEAN, ONE, UO).Arg(0, Types.Type.ITEM_TYPE, STAR | INS, BooleanValue.FALSE));
            Register("empty", 1, (e) => e.Populate(() => new Empty(), BuiltInAtomicType.BOOLEAN, ONE, UO).Arg(0, Types.Type.ITEM_TYPE, STAR | INS, BooleanValue.TRUE));
            Register("string-length", 1, (e) => e.Populate(() => new StringLength_1(), BuiltInAtomicType.INTEGER, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("upper-case", 1, (e) => e.Populate(UpperLowerCaseFn.NewUpper(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));
            Register("lower-case", 1, (e) => e.Populate(UpperLowerCaseFn.NewLower(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));
            Register("contains", 2, (e) => e.Populate(() => new Contains(), BuiltInAtomicType.BOOLEAN, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, BooleanValue.TRUE));
            Register("starts-with", 2, (e) => e.Populate(() => new StartsWith(), BuiltInAtomicType.BOOLEAN, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, BooleanValue.TRUE));
            Register("ends-with", 2, (e) => e.Populate(() => new EndsWith(), BuiltInAtomicType.BOOLEAN, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, BooleanValue.TRUE));
            // #3 collation forms: CollatingFunctionFree resolves arg2 then delegates to the fixed 2-arg impl (see compare#3). BASE = static-base-uri dependency for relative collation URIs.
            Register("contains", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.BOOLEAN, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, BooleanValue.TRUE).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            Register("starts-with", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.BOOLEAN, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, BooleanValue.TRUE).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            Register("ends-with", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.BOOLEAN, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, BooleanValue.TRUE).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            // collation-key#1/#2 — XPath31FunctionSet:53-60. #2 delegates via CollatingFunctionFree to the fixed #1.
            Register("collation-key", 1, (e) => e.Populate(() => new CollationKeyFn(), BuiltInAtomicType.BASE64_BINARY, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("collation-key", 2, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.BASE64_BINARY, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            // normalize-space#1 / xml-to-json#1 — exact signatures from XPath20FunctionSet:158 / XPath31FunctionSet:84.
            Register("normalize-space", 1, (e) => e.Populate(NormalizeSpace_1.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("xml-to-json", 1, (e) => e.Populate(() => new XMLToJsonFn(), BuiltInAtomicType.STRING, OPT, LATE).Arg(0, AnyNodeTest.GetInstance(), OPT | ABS, EMPTY)); // runtime: lambda form (not .New()) so Fix-Phase7-CtorRef-To-Lambda doesn't botch the global:: prefix on early-probe CS0117
            // xml-to-json#2 (value, options-map) — exact signature from XPath31FunctionSet:205-209 (2nd arg = options map; SetOptionDetails reads 'indent' etc).
            Register("xml-to-json", 2, (e) => e.Populate(() => new XMLToJsonFn(), BuiltInAtomicType.STRING, OPT, LATE).Arg(0, AnyNodeTest.GetInstance(), OPT | ABS, EMPTY).Arg(1, MapType.ANY_MAP_TYPE, ONE | ABS, null).SetOptionDetails(XMLToJsonFn.MakeOptionsParameter()));
            // string-join#1/#2 — exact signatures from XPath20FunctionSet:227 / XPath31FunctionSet:114-115.
            Register("string-join", 1, (e) => e.Populate(() => new StringJoin(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, StringValue.EMPTY_STRING));
            Register("string-join", 2, (e) => e.Populate(() => new StringJoin(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, StringValue.EMPTY_STRING).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            // substring-before#2 — exact signature from XPath20FunctionSet:199 (DCOLL = default collation; SubstringBefore stub above).
            Register("substring-before", 2, (e) => e.Populate(SubstringBefore.New(), BuiltInAtomicType.STRING, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));
            Register("substring-before", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.STRING, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            // substring-after#2 / substring#2 / substring#3 - exact signatures from XPath20FunctionSet:497-509.
            Register("substring-after", 2, (e) => e.Populate(SubstringAfter.New(), BuiltInAtomicType.STRING, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, null));
            Register("substring-after", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.STRING, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            Register("substring", 2, (e) => e.Populate(() => new Substring(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, NumericType.GetInstance(), ONE, null));
            Register("substring", 3, (e) => e.Populate(() => new Substring(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, NumericType.GetInstance(), ONE, null).Arg(2, NumericType.GetInstance(), OPT, null));
            // true#0 / false#0 — exact signatures from XPath20FunctionSet:210/109 (ConstantFunction.True/.False above). Invoice's serialize map uses 'indent':true().
            Register("true", 0, (e) => e.Populate(() => new ConstantFunction.True(), BuiltInAtomicType.BOOLEAN, ONE, 0));
            Register("false", 0, (e) => e.Populate(() => new ConstantFunction.False(), BuiltInAtomicType.BOOLEAN, ONE, 0));
            // serialize#2 — minimal Serialize stub (above); positional map arg, SetOptionDetails dropped (not needed for a positional map). Invoice: serialize(array{...}, map{'method':'json','indent':true()}).
            Register("serialize", 2, (e) => e.Populate(Serialize.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, AnyItemType.GetInstance(), STAR, null).Arg(1, Types.Type.ITEM_TYPE, OPT, null));
            // count#1 — exact signature from XPath20FunctionSet:116-117 (result INTEGER/ONE; arg item()* default 0). Real Count.cs excluded + name occupied by hollow stub -> bind to the real Count_1 impl above. UO/INS optimizer flags -> 0/ABS per the ctor convention.
            Register("count", 1, (e) => e.Populate(() => new Count_1(), BuiltInAtomicType.INTEGER, ONE, 0).Arg(0, Types.Type.ITEM_TYPE, STAR | ABS, Int64Value.ZERO));
            // Core XPath batch 2 (2026-06-10) - numeric family + translate, exact XPath20FunctionSet sigs (:40/:76/:224/:440/:443-448/:524-530/:64/:305-313/:555). max#2/min#2/sum-collation arities deferred (CollatingFunctionFree is a hollow stub).
            Register("abs", 1, (e) => e.Populate(() => new Abs(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY));
            Register("ceiling", 1, (e) => e.Populate(() => new Ceiling(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY));
            Register("floor", 1, (e) => e.Populate(() => new Floor(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY));
            // fn:head / fn:tail (F&O 14.1) - classes were missing from the port, so these were unregistered.
            Register("head", 1, (e) => e.Populate(() => new HeadFn(), AnyItemType.GetInstance(), OPT, FILTER).Arg(0, AnyItemType.GetInstance(), STAR | TRA, null));
            Register("tail", 1, (e) => e.Populate(() => new TailFn(), AnyItemType.GetInstance(), STAR, AS_ARG0 | FILTER).Arg(0, AnyItemType.GetInstance(), STAR | TRA, null));
            Register("round", 1, (e) => e.Populate(() => new Round(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY));
            // fn:round#2 (value, precision) - XPath 3.0 F&O 4.4.3; Round already reads arguments[1]. Was unregistered.
            Register("round", 2, (e) => e.Populate(() => new Round(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY).Arg(1, BuiltInAtomicType.INTEGER, ONE, null));
            // fn:format-number (Tier-1) — mirrors upstream XPath30FunctionSet:158 (arity 2 = LATE, arity 3 = NS|LATE for the decimal-format QName resolution).
            Register("format-number", 2, (e) => e.Populate(() => new FormatNumber(), BuiltInAtomicType.STRING, ONE, LATE).Arg(0, NumericType.GetInstance(), OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("format-number", 3, (e) => e.Populate(() => new FormatNumber(), BuiltInAtomicType.STRING, ONE, NS | LATE).Arg(0, NumericType.GetInstance(), OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, OPT, null));
            // date/time cluster — mirrors upstream XPath20FunctionSet:119-253 (dynamic context accessors)
            // and XPath30FunctionSet:123-178 (format-date family + format-integer).
            Register("current-date", 0, (e) => e.Populate(() => new DynamicContextAccessor.CurrentDate(), BuiltInAtomicType.DATE, ONE, LATE));
            Register("current-dateTime", 0, (e) => e.Populate(() => new DynamicContextAccessor.CurrentDateTime(), BuiltInAtomicType.DATE_TIME_STAMP, ONE, LATE));
            Register("current-time", 0, (e) => e.Populate(() => new DynamicContextAccessor.CurrentTime(), BuiltInAtomicType.TIME, ONE, LATE));
            Register("implicit-timezone", 0, (e) => e.Populate(() => new DynamicContextAccessor.ImplicitTimezone(), BuiltInAtomicType.DAY_TIME_DURATION, ONE, LATE));
            Register("format-date", 2, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("format-date", 5, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, OPT, null).Arg(3, BuiltInAtomicType.STRING, OPT, null).Arg(4, BuiltInAtomicType.STRING, OPT, null));
            Register("format-dateTime", 2, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("format-dateTime", 5, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, OPT, null).Arg(3, BuiltInAtomicType.STRING, OPT, null).Arg(4, BuiltInAtomicType.STRING, OPT, null));
            Register("format-time", 2, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("format-time", 5, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, OPT, null).Arg(3, BuiltInAtomicType.STRING, OPT, null).Arg(4, BuiltInAtomicType.STRING, OPT, null));
            Register("format-integer", 2, (e) => e.Populate(() => new FormatInteger(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.INTEGER, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("format-integer", 3, (e) => e.Populate(() => new FormatInteger(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.INTEGER, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, OPT, null));
            // fn:concat - upstream XPath20FunctionSet registerVariadic("concat",1,...,SEQV)
            // (the || operator compiles to fn:concat; Concat31 = the 3.1 sequence-variadic implementation).
            RegisterVariadic("concat", 1, (e) => e.Populate(() => new Concat31(), BuiltInAtomicType.STRING, ONE, SEQV).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, null));
            // fn:key (XSLT30FunctionSet:88) + fn:id (XPath20FunctionSet:239).
            Register("key", 2, (e) => e.Populate(() => new KeyFn(), Types.Type.NODE_TYPE, STAR, CDOC | NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY));
            Register("key", 3, (e) => e.Populate(() => new KeyFn(), Types.Type.NODE_TYPE, STAR, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY).Arg(2, Types.Type.NODE_TYPE, ONE, null));
            Register("id", 1, (e) => e.Populate(() => new SuperId.Id(), NodeKindTest.ELEMENT, STAR, CDOC | LATE | UO).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY));
            Register("id", 2, (e) => e.Populate(() => new SuperId.Id(), NodeKindTest.ELEMENT, STAR, LATE | UO).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY).Arg(1, Types.Type.NODE_TYPE, ONE | NAV, null));
            // idref#1/#2 — XPath20FunctionSet:246-250 (Idref, keyed on XS_IDREFS via KeyManager).
            Register("idref", 1, (e) => e.Populate(() => new Idref(), Types.Type.NODE_TYPE, STAR, CDOC | LATE).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY));
            Register("idref", 2, (e) => e.Populate(() => new Idref(), Types.Type.NODE_TYPE, STAR, LATE).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY).Arg(1, Types.Type.NODE_TYPE, ONE | NAV, null));
            // element-with-id#1/#2 (SuperId.ElementWithId), collection#0/#1 (CollectionFn), data#0 (context item).
            Register("element-with-id", 1, (e) => e.Populate(() => new SuperId.ElementWithId(), NodeKindTest.ELEMENT, STAR, CDOC | LATE | UO).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY));
            Register("element-with-id", 2, (e) => e.Populate(() => new SuperId.ElementWithId(), NodeKindTest.ELEMENT, STAR, UO).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY).Arg(1, Types.Type.NODE_TYPE, ONE, null));
            Register("data", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.ANY_ATOMIC, STAR, CITEM | LATE));
            // fn:parse-xml + fn:parse-xml-fragment (XPath30FunctionSet:221/224).
            Register("parse-xml", 1, (e) => e.Populate(() => new ParseXml(), new DocumentNodeTest(NodeKindTest.ELEMENT), OPT, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));
            Register("parse-xml-fragment", 1, (e) => e.Populate(() => new ParseXmlFragment(), NodeKindTest.DOCUMENT, OPT, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));
            // accumulator-after/before#1 (XSLT30FunctionSet:42/:46).
            Register("accumulator-after", 1, (e) => e.Populate(() => new AccumulatorFn.AccumulatorAfter(), AnyItemType.GetInstance(), STAR, LATE | CITEM).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("accumulator-before", 1, (e) => e.Populate(() => new AccumulatorFn.AccumulatorBefore(), AnyItemType.GetInstance(), STAR, LATE | CITEM).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            // system-property#1 (XSLT30FunctionSet:105) +
            // regex-group#1 (XSLT30FunctionSet:97; SIDE prevents loop-lifting).
            Register("system-property", 1, (e) => e.Populate(() => new SystemProperty(), BuiltInAtomicType.STRING, ONE, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("regex-group", 1, (e) => e.Populate(() => new RegexGroup(), BuiltInAtomicType.STRING, ONE, LATE | SIDE).Arg(0, BuiltInAtomicType.INTEGER, ONE, null));
            // HOF quartet (XPath30FunctionSet:81-110 sigs verbatim).
            {
                var __predicate = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_ITEM }, SequenceType.SINGLE_BOOLEAN);
                Register("filter", 2, (e) => e.Populate(() => new FilterFn(), AnyItemType.GetInstance(), STAR, AS_ARG0 | LATE).Arg(0, AnyItemType.GetInstance(), STAR | TRA, EMPTY).Arg(1, __predicate, ONE, null));
                var __foldLeftArg = new SpecificFunctionType(new SequenceType[] { SequenceType.ANY_SEQUENCE, SequenceType.SINGLE_ITEM }, SequenceType.ANY_SEQUENCE);
                Register("fold-left", 3, (e) => e.Populate(() => new FoldLeftFn(), AnyItemType.GetInstance(), STAR, LATE).Arg(0, AnyItemType.GetInstance(), STAR, null).Arg(1, AnyItemType.GetInstance(), STAR, null).Arg(2, __foldLeftArg, ONE, null));
                var __foldRightArg = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_ITEM, SequenceType.ANY_SEQUENCE }, SequenceType.ANY_SEQUENCE);
                Register("fold-right", 3, (e) => e.Populate(() => new FoldRightFn(), AnyItemType.GetInstance(), STAR, LATE).Arg(0, AnyItemType.GetInstance(), STAR, null).Arg(1, AnyItemType.GetInstance(), STAR, null).Arg(2, __foldRightArg, ONE, null));
                var __forEachArg = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_ITEM }, SequenceType.ANY_SEQUENCE);
                Register("for-each", 2, (e) => e.Populate(() => new ForEachFn(), AnyItemType.GetInstance(), STAR, LATE).Arg(0, AnyItemType.GetInstance(), STAR, EMPTY).Arg(1, __forEachArg, ONE, null));
            }
            // fn:sort (upstream XPath31FunctionSet) + current-group/current-grouping-key (XSLT30FunctionSet, LATE).
            Register("sort", 1, (e) => e.Populate(() => new Sort_1(), AnyItemType.GetInstance(), STAR, 0).Arg(0, AnyItemType.GetInstance(), STAR, null));
            Register("sort", 2, (e) => e.Populate(() => new Sort_2(), AnyItemType.GetInstance(), STAR, 0).Arg(0, AnyItemType.GetInstance(), STAR, null).Arg(1, BuiltInAtomicType.STRING, OPT, null));
            // function(item()) as xs:anyAtomicType*: the coercion to it is what checks the key function's arity and arguments
            var __sortKey = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_ITEM }, SequenceType.ATOMIC_SEQUENCE);
            Register("sort", 3, (e) => e.Populate(() => new Sort_3(), AnyItemType.GetInstance(), STAR, 0).Arg(0, AnyItemType.GetInstance(), STAR, null).Arg(1, BuiltInAtomicType.STRING, OPT, null).Arg(2, __sortKey, ONE, null));
            Register("current-group", 0, (e) => e.Populate(() => new CurrentGroup(), Types.Type.ITEM_TYPE, STAR, LATE));
            Register("current-grouping-key", 0, (e) => e.Populate(() => new CurrentGroupingKey(), BuiltInAtomicType.ANY_ATOMIC, STAR, LATE));
            // XSLT30FunctionSet.java:53-118 tail the bring-up list missed: current() (rewritten at compile time
            // by the style compiler, but must RESOLVE or every use is XPST0017), merge accessors,
            // stream-available, unparsed-entity accessors.
            Register("current", 0, (e) => e.Populate(() => new Current(), Types.Type.ITEM_TYPE, ONE, LATE));
            Register("current-merge-group", 0, (e) => e.Populate(() => new CurrentMergeGroup(), AnyItemType.GetInstance(), STAR, LATE));
            Register("current-merge-group", 1, (e) => e.Populate(() => new CurrentMergeGroup(), AnyItemType.GetInstance(), STAR, LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("current-merge-key", 0, (e) => e.Populate(() => new CurrentMergeKey(), BuiltInAtomicType.ANY_ATOMIC, STAR, LATE));
            Register("stream-available", 1, (e) => e.Populate(() => new StreamAvailable(), BuiltInAtomicType.BOOLEAN, ONE, LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("unparsed-entity-public-id", 1, (e) => e.Populate(() => new UnparsedEntity.UnparsedEntityPublicId(), BuiltInAtomicType.STRING, ONE, CDOC | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("unparsed-entity-public-id", 2, (e) => e.Populate(() => new UnparsedEntity.UnparsedEntityPublicId(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, Types.Type.NODE_TYPE, ONE, null));
            Register("unparsed-entity-uri", 1, (e) => e.Populate(() => new UnparsedEntity.UnparsedEntityUri(), BuiltInAtomicType.ANY_URI, ONE, CDOC | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("unparsed-entity-uri", 2, (e) => e.Populate(() => new UnparsedEntity.UnparsedEntityUri(), BuiltInAtomicType.ANY_URI, ONE, 0).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, Types.Type.NODE_TYPE, ONE, null));
            // fn:copy-of / fn:snapshot (XPath31FunctionSet.java:73-78/160-164; this flattened set must carry
            // them too — XSLT resolution goes through GetXSLTFunctionSet, which serves this set directly).
            Register("copy-of", 0, (e) => e.Populate(() => new CopyOfFn(), AnyItemType.GetInstance(), STAR, NEW));
            Register("copy-of", 1, (e) => e.Populate(() => new CopyOfFn(), AnyItemType.GetInstance(), STAR, NEW).Arg(0, AnyItemType.GetInstance(), STAR | ABS, EMPTY));
            Register("snapshot", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), AnyItemType.GetInstance(), STAR, CITEM | LATE | NEW));
            Register("snapshot", 1, (e) => e.Populate(() => new SnapshotFn(), AnyNodeTest.GetInstance(), STAR, NEW).Arg(0, AnyItemType.GetInstance(), STAR | ABS, EMPTY));
            Register("round-half-to-even", 1, (e) => e.Populate(() => new RoundHalfToEven(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY));
            Register("round-half-to-even", 2, (e) => e.Populate(() => new RoundHalfToEven(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY).Arg(1, BuiltInAtomicType.INTEGER, ONE, null));
            // sum MUST be the Java range-form register("sum",1,2): Sum.MakeFunctionCall does SetArity(2) on the
            // arity-1 call - a plain arity-1 Entry has usage[1]/paramTypes[1] and GetOperandRoles leaves roles[1]
            // null (Java-swallowed) -> NRE in OperandArray. The range Entry sizes arrays to maxArity=2.
            Register("sum", 1, 2, (e) => e.Populate(() => new Sum(), BuiltInAtomicType.ANY_ATOMIC, OPT, UO).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, OPT, null));
            Register("avg", 1, (e) => e.Populate(() => new Average(), BuiltInAtomicType.ANY_ATOMIC, OPT, UO).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY));
            Register("max", 1, (e) => e.Populate(() => new Minimax.Max(), BuiltInAtomicType.ANY_ATOMIC, OPT, DCOLL | UO | CARD0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY));
            Register("max", 2, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.ANY_ATOMIC, OPT, BASE | UO | CARD0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("min", 1, (e) => e.Populate(() => new Minimax.Min(), BuiltInAtomicType.ANY_ATOMIC, OPT, DCOLL | UO | CARD0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY));
            Register("min", 2, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.ANY_ATOMIC, OPT, BASE | UO | CARD0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("translate", 3, (e) => e.Populate(() => new Translate(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            // Core XPath batch 3 (2026-06-10) - context-item #0-arities + name family + position/last/root.
            // ContextItemAccessorFunction wraps f#0 -> f#1(.) so the #1 arity must be registered too. Exact
            // XPath20FunctionSet sigs (:68-72/:282-288/:343-356/:372/:388/:396/:437/:470-477).
            Register("string", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.STRING, ONE, CITEM | LATE));
            Register("normalize-space", 0, (e) => e.Populate(() => new ContextItemAccessorFunction.StringAccessor(), BuiltInAtomicType.STRING, ONE, CITEM | LATE));
            Register("string-length", 0, (e) => e.Populate(() => new ContextItemAccessorFunction.StringAccessor(), BuiltInAtomicType.INTEGER, ONE, CITEM | LATE));
            Register("number", 0, (e) => e.Populate(() => new ContextItemAccessorFunction.Number_0(), BuiltInAtomicType.DOUBLE, ONE, CITEM | LATE));
            Register("name", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.STRING, ONE, CITEM | LATE));
            Register("name", 1, (e) => e.Populate(NameFn1.NewName(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, Types.Type.NODE_TYPE, OPT | INS, StringValue.EMPTY_STRING));
            Register("local-name", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.STRING, ONE, CITEM | LATE));
            Register("local-name", 1, (e) => e.Populate(LocalNameFn1.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, Types.Type.NODE_TYPE, OPT | INS, StringValue.EMPTY_STRING));
            Register("namespace-uri", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.ANY_URI, ONE, CITEM | LATE));
            Register("namespace-uri", 1, (e) => e.Populate(() => new NamespaceUriFn_1(), BuiltInAtomicType.ANY_URI, ONE, 0).Arg(0, Types.Type.NODE_TYPE, OPT | INS, StringValue.EMPTY_STRING));
            Register("base-uri", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.ANY_URI, OPT, CITEM | BASE | LATE));
            Register("base-uri", 1, (e) => e.Populate(() => new BaseUri_1(), BuiltInAtomicType.ANY_URI, OPT, BASE).Arg(0, Types.Type.NODE_TYPE, OPT | INS, EMPTY));
            Register("root", 1, (e) => e.Populate(() => new Root_1(), Types.Type.NODE_TYPE, OPT, CARD0).Arg(0, Types.Type.NODE_TYPE, OPT | NAV, EMPTY));
            Register("data", 1, (e) => e.Populate(() => new Data_1(), BuiltInAtomicType.ANY_ATOMIC, STAR, 0).Arg(0, Types.Type.ITEM_TYPE, STAR | ABS, EMPTY));
            // Core XPath batch 4 (2026-06-10) - sequence family, exact XPath20FunctionSet sigs (:150/:158/:216/:258/:267/:393/:405/:432/:488-495/:562/:580). Collation arities (distinct-values#2/index-of#3) deferred - CollatingFunctionFree is hollow.
            Register("distinct-values", 1, (e) => e.Populate(() => new DistinctValues(), BuiltInAtomicType.ANY_ATOMIC, STAR, DCOLL | UO).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY));
            Register("distinct-values", 2, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.ANY_ATOMIC, STAR, BASE | UO).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("index-of", 2, (e) => e.Populate(() => new IndexOf(), BuiltInAtomicType.INTEGER, STAR, DCOLL).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY).Arg(1, BuiltInAtomicType.ANY_ATOMIC, ONE, null));
            Register("index-of", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.INTEGER, STAR, BASE).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY).Arg(1, BuiltInAtomicType.ANY_ATOMIC, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            Register("insert-before", 3, (e) => e.Populate(() => new InsertBefore(), Types.Type.ITEM_TYPE, STAR, 0).Arg(0, Types.Type.ITEM_TYPE, STAR | TRA, null).Arg(1, BuiltInAtomicType.INTEGER, ONE, null).Arg(2, Types.Type.ITEM_TYPE, STAR | TRA, null));
            Register("remove", 2, (e) => e.Populate(() => new Remove(), Types.Type.ITEM_TYPE, STAR, AS_ARG0 | FILTER).Arg(0, Types.Type.ITEM_TYPE, STAR | TRA, EMPTY).Arg(1, BuiltInAtomicType.INTEGER, ONE, null));
            Register("reverse", 1, (e) => e.Populate(() => new Reverse(), Types.Type.ITEM_TYPE, STAR, AS_ARG0 | FILTER).Arg(0, Types.Type.ITEM_TYPE, STAR | NAV, EMPTY));
            Register("subsequence", 2, (e) => e.Populate(() => new Subsequence_2(), Types.Type.ITEM_TYPE, STAR, AS_ARG0 | FILTER).Arg(0, Types.Type.ITEM_TYPE, STAR | TRA, EMPTY).Arg(1, NumericType.GetInstance(), ONE, null));
            Register("subsequence", 3, (e) => e.Populate(() => new Subsequence_3(), Types.Type.ITEM_TYPE, STAR, AS_ARG0 | FILTER).Arg(0, Types.Type.ITEM_TYPE, STAR | TRA, EMPTY).Arg(1, NumericType.GetInstance(), ONE, null).Arg(2, NumericType.GetInstance(), ONE, null));
            Register("unordered", 1, (e) => e.Populate(() => new Unordered(), Types.Type.ITEM_TYPE, STAR, AS_ARG0 | FILTER | UO).Arg(0, Types.Type.ITEM_TYPE, STAR, EMPTY));
            Register("zero-or-one", 1, (e) => e.Populate(() => new TreatFn.ZeroOrOne(), Types.Type.ITEM_TYPE, OPT, AS_ARG0 | FILTER).Arg(0, Types.Type.ITEM_TYPE, STAR, EMPTY));
            Register("one-or-more", 1, (e) => e.Populate(() => new TreatFn.OneOrMore(), Types.Type.ITEM_TYPE, PLUS, AS_ARG0 | FILTER).Arg(0, Types.Type.ITEM_TYPE, STAR, EMPTY));
            Register("exactly-one", 1, (e) => e.Populate(() => new TreatFn.ExactlyOne(), Types.Type.ITEM_TYPE, ONE, AS_ARG0 | FILTER).Arg(0, Types.Type.ITEM_TYPE, STAR, EMPTY));
            Register("deep-equal", 2, 4, (e) => e.Populate(() => new DeepEqual(), BuiltInAtomicType.BOOLEAN, ONE, BASE).Arg(0, Types.Type.ITEM_TYPE, STAR | ABS, null).Arg(1, Types.Type.ITEM_TYPE, STAR | ABS, null).Arg(2, BuiltInAtomicType.STRING, OPT | ABS, null).Arg(3, MapType.ANY_MAP_TYPE, OPT | ABS, null));
            Register("position", 0, (e) => e.Populate(() => new PositionAndLast.Position(), BuiltInAtomicType.INTEGER, ONE, POSN | LATE));
            Register("last", 0, (e) => e.Populate(() => new PositionAndLast.Last(), BuiltInAtomicType.INTEGER, ONE, LAST | LATE));
            // Core XPath batch 5 (2026-06-10) - node/string accessors, exact sigs (XPath20:79-95/156/171/275-281/363-368/435/468; XPath30:63/194-205). compare#3/collection deferred (CollatingFunctionFree hollow / I-O).
            Register("codepoint-equal", 2, (e) => e.Populate(() => new CodepointEqual(), BuiltInAtomicType.BOOLEAN, OPT, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, BuiltInAtomicType.STRING, OPT, EMPTY));
            Register("codepoints-to-string", 1, (e) => e.Populate(() => new CodepointsToString(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.INTEGER, STAR, null));
            Register("compare", 2, (e) => e.Populate(() => new Compare(), BuiltInAtomicType.INTEGER, OPT, DCOLL).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, BuiltInAtomicType.STRING, OPT, EMPTY));
            // collation-aware fn:compare#3 (2026-06-22): mirrors XPath20FunctionSet:58. The 3rd arg is a collation URI;
            // CollatingFunctionFree (re-included) resolves it at runtime via StandardCollationURIResolver -> DotNetPlatform.MakeCollation
            // and delegates to compare#2 on the resolved collator. Result depends on the base URI (BASE) for relative collation URIs.
            Register("compare", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.INTEGER, OPT, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            Register("default-collation", 0, (e) => e.Populate(() => new StaticContextAccessor.DefaultCollation(), BuiltInAtomicType.STRING, ONE, DCOLL));
            // fn:default-language() (XPath 3.1). The DynamicContextAccessor.DefaultLanguage impl already existed
            // but was never registered → XPST0017. LATE (this port has no DLANG dependency bit) keeps it from
            // being constant-folded away from the dynamic context.
            Register("default-language", 0, (e) => e.Populate(() => new DynamicContextAccessor.DefaultLanguage(), BuiltInAtomicType.LANGUAGE, ONE, LATE));
            Register("lang", 1, (e) => e.Populate(() => new Lang(), BuiltInAtomicType.BOOLEAN, ONE, CITEM | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("lang", 2, (e) => e.Populate(() => new Lang(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, Types.Type.NODE_TYPE, ONE | INS, null));
            // 0-arity (context-item) forms — upstream registers these in XPath30FunctionSet; the port dropped them.
            Register("nilled", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.BOOLEAN, OPT, CITEM | LATE));
            Register("node-name", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.QNAME, OPT, CITEM | LATE));
            Register("nilled", 1, (e) => e.Populate(() => new Nilled_1(), BuiltInAtomicType.BOOLEAN, OPT, 0).Arg(0, Types.Type.NODE_TYPE, OPT | INS, EMPTY));
            Register("node-name", 1, (e) => e.Populate(() => new NodeName_1(), BuiltInAtomicType.QNAME, OPT, 0).Arg(0, Types.Type.NODE_TYPE, OPT | INS, EMPTY));
            Register("root", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), Types.Type.NODE_TYPE, ONE, CITEM | LATE));
            Register("static-base-uri", 0, (e) => e.Populate(() => new StaticBaseUri(), BuiltInAtomicType.ANY_URI, OPT, BASE | LATE));
            Register("generate-id", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.STRING, ONE, CITEM | LATE));
            Register("generate-id", 1, (e) => e.Populate(GenerateIdFn1.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, Types.Type.NODE_TYPE, OPT | INS, StringValue.EMPTY_STRING));
            Register("has-children", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.BOOLEAN, ONE, CITEM | LATE));
            Register("has-children", 1, (e) => e.Populate(() => new HasChildren_1(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, AnyNodeTest.GetInstance(), OPT | INS, null));
            Register("document-uri", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.ANY_URI, OPT, CITEM | LATE));
            Register("document-uri", 1, (e) => e.Populate(() => new DocumentUri_1(), BuiltInAtomicType.ANY_URI, OPT, LATE).Arg(0, Types.Type.NODE_TYPE, OPT | INS, EMPTY));
            // Regex cluster (2026-06-10) - exact XPath20FunctionSet sigs (:296-303/:409-414/:542-549). replace#4 deferred (CSharp.staticRef(Replace::make20) factory).
            Register("matches", 2, (e) => e.Populate(() => new RegexFunctionSansFlags(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("matches", 3, (e) => e.Populate(() => new Matches(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            Register("replace", 3, (e) => e.Populate(() => new RegexFunctionSansFlags(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            Register("tokenize", 2, (e) => e.Populate(() => new RegexFunctionSansFlags(), BuiltInAtomicType.STRING, STAR, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("tokenize", 3, (e) => e.Populate(() => new Tokenize_3(), BuiltInAtomicType.STRING, STAR, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            // I/O family (2026-06-10) - fn:doc/doc-available/document; exact sigs XPath20:165-169 / XSLT30:71-77.
            Register("doc", 1, (e) => e.Populate(() => new Doc(), NodeKindTest.DOCUMENT, OPT, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));
            Register("doc-available", 1, (e) => e.Populate(() => new DocAvailable(), BuiltInAtomicType.BOOLEAN, ONE, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, BooleanValue.FALSE));
            Register("document", 1, (e) => e.Populate(() => new DocumentFn(), Types.Type.NODE_TYPE, STAR, BASE | LATE | UO).Arg(0, Types.Type.ITEM_TYPE, STAR, null));
            Register("document", 2, (e) => e.Populate(() => new DocumentFn(), Types.Type.NODE_TYPE, STAR, BASE | LATE | UO).Arg(0, Types.Type.ITEM_TYPE, STAR, null).Arg(1, Types.Type.NODE_TYPE, ONE, null));
            // collection / uri-collection - exact sigs XPath20FunctionSet.java:86-89 / XPath30FunctionSet.java:272-275.
            Register("collection", 0, (e) => e.Populate(() => new CollectionFn(), Types.Type.ITEM_TYPE, STAR, BASE | LATE));
            Register("collection", 1, (e) => e.Populate(() => new CollectionFn(), Types.Type.ITEM_TYPE, STAR, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("uri-collection", 0, (e) => e.Populate(() => new UriCollection(), BuiltInAtomicType.ANY_URI, STAR, LATE));
            Register("uri-collection", 1, (e) => e.Populate(() => new UriCollection(), BuiltInAtomicType.ANY_URI, STAR, LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            // URI/namespace cluster (2026-06-10 evening) - exact sigs XPath20FunctionSet.java:255-256/356-358/425-430.
            Register("resolve-uri", 1, (e) => e.Populate(() => new ResolveURI(), BuiltInAtomicType.ANY_URI, OPT, CARD0 | BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("resolve-uri", 2, (e) => e.Populate(() => new ResolveURI(), BuiltInAtomicType.ANY_URI, OPT, CARD0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("namespace-uri-for-prefix", 2, (e) => e.Populate(() => new NamespaceForPrefix(), BuiltInAtomicType.ANY_URI, OPT, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, NodeKindTest.ELEMENT, ONE | INS, null));
            Register("in-scope-prefixes", 1, (e) => e.Populate(() => new InScopePrefixes(), BuiltInAtomicType.STRING, STAR, 0).Arg(0, NodeKindTest.ELEMENT, ONE | INS, null));
            // encode-for-uri#1 (2026-06-10) - real EncodeForUri re-included (also supplies the static
            // CheckPercentEncoding that UnparsedTextFunction.GetAbsoluteURI calls). Sig XPath20FunctionSet.java:184.
            Register("encode-for-uri", 1, (e) => e.Populate(() => new EncodeForUri(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));
            // unparsed-text family (2026-06-10) - real classes re-included; exact sigs XPath30FunctionSet.java:248-263.
            Register("unparsed-text", 1, (e) => e.Populate(() => new UnparsedText(), BuiltInAtomicType.STRING, OPT, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("unparsed-text", 2, (e) => e.Populate(() => new UnparsedText(), BuiltInAtomicType.STRING, OPT, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            // unparsed-text-lines#1/#2 — returns the resource split into lines (STAR).
            Register("unparsed-text-lines", 1, (e) => e.Populate(() => new UnparsedTextLines(), BuiltInAtomicType.STRING, STAR, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("unparsed-text-lines", 2, (e) => e.Populate(() => new UnparsedTextLines(), BuiltInAtomicType.STRING, STAR, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            // random-number-generator#0/#1 — XPath31FunctionSet:145. Returns a map{number,next,permute}.
            Register("random-number-generator", 0, (e) => e.Populate(() => new OutSmart.DAXon.Functions.HigherOrder.RandomNumberGenerator(), OutSmart.DAXon.Functions.HigherOrder.RandomNumberGenerator.RETURN_TYPE, ONE, LATE));
            Register("random-number-generator", 1, (e) => e.Populate(() => new OutSmart.DAXon.Functions.HigherOrder.RandomNumberGenerator(), OutSmart.DAXon.Functions.HigherOrder.RandomNumberGenerator.RETURN_TYPE, ONE, LATE).Arg(0, BuiltInAtomicType.ANY_ATOMIC, OPT, null));
            Register("unparsed-text-available", 1, (e) => e.Populate(() => new UnparsedTextAvailable(), BuiltInAtomicType.BOOLEAN, ONE, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, BooleanValue.FALSE));
            Register("unparsed-text-available", 2, (e) => e.Populate(() => new UnparsedTextAvailable(), BuiltInAtomicType.BOOLEAN, ONE, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, BooleanValue.FALSE).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            // replace#4 - the SansFlags wrapper converts replace#3 -> #4, so #4 MUST exist (NRE otherwise). Java: CSharp.staticRef(Replace::make20).
            Register("replace", 4, (e) => e.Populate(() => Replace.Make20(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null).Arg(3, BuiltInAtomicType.STRING, ONE, null));
            // json-to-xml#1/#2 — reverse of xml-to-json (XSLT 3.1). Real JsonToXMLFn+JsonParser+JsonHandler+JsonHandlerXML re-included in csproj. Result AnyItemType/OPT/LATE|NEW (builds a fresh tree); arg0 = JSON string, arg1 = options map. Exact sigs from XPath31FunctionSet:94-95. Lambda form (parity w/ xml-to-json, dodges Fix-Phase7-CtorRef-To-Lambda).
            Register("json-to-xml", 1, (e) => e.Populate(() => new JsonToXMLFn(), AnyItemType.GetInstance(), OPT, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("json-to-xml", 2, (e) => e.Populate(() => new JsonToXMLFn(), AnyItemType.GetInstance(), OPT, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(JsonToXMLFn.OPTION_DETAILS));
            // parse-json#1/#2 (XSLT/XPath 3.1) — real ParseJsonFn (: JsonToXMLFn) + JsonHandlerMap re-included (csproj); hollow ParseJsonFn stub deleted. Sigs from XPath31FunctionSet:64-65 (AnyItemType/OPT/0; arg0 STRING/OPT default EMPTY; #2 options map ANY_MAP_TYPE/ONE + ParseJsonFn.OPTION_DETAILS).
            Register("parse-json", 1, (e) => e.Populate(() => new ParseJsonFn(), AnyItemType.GetInstance(), OPT, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));
            Register("parse-json", 2, (e) => e.Populate(() => new ParseJsonFn(), AnyItemType.GetInstance(), OPT, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(ParseJsonFn.OPTION_DETAILS));
            // json-doc#1/#2 (XSLT/XPath 3.1) — real JsonDoc (: SystemFunction) re-included (csproj); UnparsedTextFunction stub given functional GetAbsoluteURI/HandleIOError/ReadFile. Reads JSON from a URI then parse-json. Sigs from XPath31FunctionSet:57-58 (AnyItemType/OPT/LATE; arg0 STRING/OPT default null; #2 options map ANY_MAP_TYPE/ONE + ParseJsonFn.OPTION_DETAILS).
            Register("json-doc", 1, (e) => e.Populate(() => new JsonDoc(), AnyItemType.GetInstance(), OPT, LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("json-doc", 2, (e) => e.Populate(() => new JsonDoc(), AnyItemType.GetInstance(), OPT, LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(ParseJsonFn.OPTION_DETAILS));
            // Availability family (TIER-2 2026-06-17) - re-included real fn:function-available/type-available/element-available. Exact sigs from XSLT30FunctionSet.java:78-108 (BOOLEAN ONE; NS|LATE for function-available, NS for type/element).
            Register("function-available", 1, (e) => e.Populate(() => new FunctionAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("function-available", 2, (e) => e.Populate(() => new FunctionAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, BuiltInAtomicType.INTEGER, ONE, null));
            Register("type-available", 1, (e) => e.Populate(() => new TypeAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("element-available", 1, (e) => e.Populate(() => new ElementAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            // Config-introspection family (TIER-2 2026-06-17). available-system-properties from XSLT30FunctionSet.java:50
            // (QNAME* LATE); environment-variable#1 + available-environment-variables#0 from XPath30FunctionSet.java:58/73
            // (STRING OPT/STAR LATE) -- in XSLT 3.0 via the inherited XPath 3.0 library.
            Register("available-system-properties", 0, (e) => e.Populate(() => new AvailableSystemProperties(), BuiltInAtomicType.QNAME, STAR, LATE));
            Register("environment-variable", 1, (e) => e.Populate(() => new EnvironmentVariable(), BuiltInAtomicType.STRING, OPT, LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("available-environment-variables", 0, (e) => e.Populate(() => new AvailableEnvironmentVariables(), BuiltInAtomicType.STRING, STAR, LATE));
            // current-output-uri (TIER-2 2026-06-17) - XSLT30FunctionSet.java:69 (ANY_URI? LATE); used inside xsl:result-document.
            Register("current-output-uri", 0, (e) => e.Populate(() => new CurrentOutputUri(), BuiltInAtomicType.ANY_URI, OPT, LATE));
            // === TIER-3 function-registry batch 1 (probe-confirmed genuinely-missing 2026-06-23; byte-verified vs live Java). ===
            // Sigs verbatim from the real (excluded) XPath20/30/31FunctionSet.cs registry sources.
            Register("serialize", 1, (e) => e.Populate(Serialize.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, AnyItemType.GetInstance(), STAR, null));
            // fn:transform (XPath 3.1) — sig from XPath31FunctionSet.java:197-199
            Register("transform", 1, (e) => e.Populate(() => new TransformFn(), MapType.ANY_MAP_TYPE, ONE, LATE).Arg(0, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(TransformFn.MakeOptionsParameter()));
            // fn:load-xquery-module (XPath 3.1)
            Register("load-xquery-module", 1, (e) => e.Populate(() => new OutSmart.DAXon.Functions.HigherOrder.LoadXqueryModule(), MapType.ANY_MAP_TYPE, ONE, LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("load-xquery-module", 2, (e) => e.Populate(() => new OutSmart.DAXon.Functions.HigherOrder.LoadXqueryModule(), MapType.ANY_MAP_TYPE, ONE, LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(OutSmart.DAXon.Functions.HigherOrder.LoadXqueryModule.MakeOptionsParameter()));
            Register("contains-token", 2, (e) => e.Populate(() => new ContainsToken(), BuiltInAtomicType.BOOLEAN, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, STAR, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("contains-token", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.BOOLEAN, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, STAR, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            Register("analyze-string", 2, (e) => e.Populate(() => new RegexFunctionSansFlags(), NodeKindTest.ELEMENT, ONE, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));
            Register("analyze-string", 3, (e) => e.Populate(() => new AnalyzeStringFn(), NodeKindTest.ELEMENT, ONE, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));
            // batch 1b (2026-06-23): named-function-reference HOF reflection, unblocked by the AbstractFunction.Reduce()=>this
            // fix (Fix-PhaseB-FnBatch1-Reinclude). function-arity/function-name take a function item, typically a named-fn-ref `fn#n`.
            Register("function-arity", 1, (e) => e.Populate(() => new FunctionArity(), BuiltInAtomicType.INTEGER, ONE, 0).Arg(0, AnyFunctionType.GetInstance(), ONE, null));
            Register("function-name", 1, (e) => e.Populate(() => new FunctionName(), BuiltInAtomicType.QNAME, OPT, 0).Arg(0, AnyFunctionType.GetInstance(), ONE, null));
            // batch 1d (2026-06-23): re-registered after the grounded-contract (1b) + array Call-override (1c) fixes,
            // which likely unblocked the HOF ones (function items are now working grounded values; arrays work).
            {
                var __forEachPairArg = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_ITEM, SequenceType.SINGLE_ITEM }, SequenceType.ANY_SEQUENCE);
                Register("for-each-pair", 3, (e) => e.Populate(() => new ForEachPairFn(), AnyItemType.GetInstance(), STAR, LATE).Arg(0, AnyItemType.GetInstance(), STAR, EMPTY).Arg(1, AnyItemType.GetInstance(), STAR, EMPTY).Arg(2, __forEachPairArg, ONE, null));
            }
            Register("apply", 2, (e) => e.Populate(() => new ApplyFn(), AnyItemType.GetInstance(), STAR, LATE).Arg(0, AnyFunctionType.GetInstance(), ONE, null).Arg(1, ArrayItemType.ANY_ARRAY_TYPE, ONE, null));
            Register("innermost", 1, (e) => e.Populate(() => new Innermost(), AnyNodeTest.GetInstance(), STAR, 0).Arg(0, AnyNodeTest.GetInstance(), STAR | NAV, null));
            Register("outermost", 1, (e) => e.Populate(() => new Outermost(), AnyNodeTest.GetInstance(), STAR, AS_ARG0 | FILTER).Arg(0, AnyNodeTest.GetInstance(), STAR | TRA, null));
            Register("path", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.STRING, OPT, CITEM | LATE));
            Register("path", 1, (e) => e.Populate(() => new Path_1(), BuiltInAtomicType.STRING, OPT, 0).Arg(0, AnyNodeTest.GetInstance(), OPT | NAV, null));
            // function-lookup#2 ported (2026-07-06): FunctionLookup + the CallableWithBoundFocus /
            // SystemFunctionWithBoundContextItem cascade (BoundUserFunction was already present).
            Register("function-lookup", 2, (e) => e.Populate(() => new FunctionLookup(), AnyFunctionType.GetInstance(), OPT, FOCUS | DEPENDS_ON_STATIC_CONTEXT | LATE).Arg(0, BuiltInAtomicType.QNAME, ONE, null).Arg(1, BuiltInAtomicType.INTEGER, ONE, null));
        }
        public static XSLT30FunctionSet GetInstance() => _i;
    }
}
