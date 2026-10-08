////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    // Function signatures of the XPath 2.0 core function library (upstream registry/XPath20FunctionSet.java).
    // Was a 1-function stub (concat only) — the XQuery static context (QueryModule.GetBuiltInFunctionSet →
    // Configuration.GetXPathFunctionSet) binds these sets, so nearly every function raised XPST0017 under
    // XQuery while working under XSLT (whose flattened XSLT30FunctionSet registers everything). Registration
    // lines are shared verbatim with XSLT30FunctionSet.cs (same Entry shapes as upstream, where the XSLT set
    // imports this one).
    internal sealed class XPath20FunctionSet : BuiltInFunctionSet
    {
        private static readonly XPath20FunctionSet _i = new XPath20FunctionSet();
        public XPath20FunctionSet()
        {
            Register("string", 1, (e) => e.Populate(String_1.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, Types.Type.ITEM_TYPE, OPT | ABS, StringValue.EMPTY_STRING));

            Register("tokenize", 1, (e) => e.Populate(Tokenize_1.New(), BuiltInAtomicType.STRING, STAR, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));

            Register("string-to-codepoints", 1, (e) => e.Populate(() => new StringToCodepoints(), BuiltInAtomicType.INTEGER, STAR, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY)); // runtime: lambda form (not .New()) so Fix-Phase7-CtorRef-To-Lambda doesn't botch the global:: prefix

            Register("QName", 2, (e) => e.Populate(() => new QNameFn(), BuiltInAtomicType.QNAME, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("local-name-from-QName", 1, (e) => e.Populate(() => new AccessorFn.LocalNameFromQName(), BuiltInAtomicType.NCNAME, OPT, 0).Arg(0, BuiltInAtomicType.QNAME, OPT, EMPTY));

            Register("namespace-uri-from-QName", 1, (e) => e.Populate(() => new AccessorFn.NamespaceUriFromQName(), BuiltInAtomicType.ANY_URI, OPT, 0).Arg(0, BuiltInAtomicType.QNAME, OPT, EMPTY));

            Register("prefix-from-QName", 1, (e) => e.Populate(() => new AccessorFn.PrefixFromQName(), BuiltInAtomicType.NCNAME, OPT, 0).Arg(0, BuiltInAtomicType.QNAME, OPT, EMPTY));

            Register("resolve-QName", 2, (e) => e.Populate(() => new ResolveQName(), BuiltInAtomicType.QNAME, OPT, CARD0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, NodeKindTest.ELEMENT, ONE | INS, null));

            Register("error", 0, (e) => e.Populate(() => new Error(), Types.Type.ITEM_TYPE, OPT, LATE));

            Register("error", 1, (e) => e.Populate(() => new Error(), Types.Type.ITEM_TYPE, OPT, LATE).Arg(0, BuiltInAtomicType.QNAME, OPT, null));

            Register("error", 2, (e) => e.Populate(() => new Error(), Types.Type.ITEM_TYPE, OPT, LATE).Arg(0, BuiltInAtomicType.QNAME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("error", 3, (e) => e.Populate(() => new Error(), Types.Type.ITEM_TYPE, OPT, LATE).Arg(0, BuiltInAtomicType.QNAME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, Types.Type.ITEM_TYPE, STAR, null));

            Register("trace", 1, (e) => e.Populate(() => new Trace(), Types.Type.ITEM_TYPE, STAR, AS_ARG0 | LATE).Arg(0, Types.Type.ITEM_TYPE, STAR | TRA, null));

            Register("trace", 2, (e) => e.Populate(() => new Trace(), Types.Type.ITEM_TYPE, STAR, AS_ARG0 | LATE).Arg(0, Types.Type.ITEM_TYPE, STAR | TRA, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("normalize-unicode", 1, (e) => e.Populate(() => new NormalizeUnicode(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));

            Register("normalize-unicode", 2, (e) => e.Populate(() => new NormalizeUnicode(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("iri-to-uri", 1, (e) => e.Populate(() => new IriToUri(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));

            Register("escape-html-uri", 1, (e) => e.Populate(() => new EscapeHtmlUri(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));

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

            Register("adjust-date-to-timezone", 1, (e) => e.Populate(() => new Adjust_1(), BuiltInAtomicType.DATE, OPT, LATE | CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, EMPTY));

            Register("adjust-date-to-timezone", 2, (e) => e.Populate(() => new Adjust_2(), BuiltInAtomicType.DATE, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, EMPTY).Arg(1, BuiltInAtomicType.DAY_TIME_DURATION, OPT, null));

            Register("adjust-dateTime-to-timezone", 1, (e) => e.Populate(() => new Adjust_1(), BuiltInAtomicType.DATE_TIME, OPT, LATE | CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY));

            Register("adjust-dateTime-to-timezone", 2, (e) => e.Populate(() => new Adjust_2(), BuiltInAtomicType.DATE_TIME, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, EMPTY).Arg(1, BuiltInAtomicType.DAY_TIME_DURATION, OPT, null));

            Register("adjust-time-to-timezone", 1, (e) => e.Populate(() => new Adjust_1(), BuiltInAtomicType.TIME, OPT, LATE | CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, EMPTY));

            Register("adjust-time-to-timezone", 2, (e) => e.Populate(() => new Adjust_2(), BuiltInAtomicType.TIME, OPT, CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, EMPTY).Arg(1, BuiltInAtomicType.DAY_TIME_DURATION, OPT, null));

            Register("dateTime", 2, (e) => e.Populate(() => new DateTimeConstructor(), BuiltInAtomicType.DATE_TIME, OPT, 0).Arg(0, BuiltInAtomicType.DATE, OPT, EMPTY).Arg(1, BuiltInAtomicType.TIME, OPT, EMPTY));

            Register("number", 1, (e) => e.Populate(() => new Number_1(), BuiltInAtomicType.DOUBLE, ONE, 0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, OPT, DoubleValue.NaN));

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

            Register("contains", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.BOOLEAN, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, BooleanValue.TRUE).Arg(2, BuiltInAtomicType.STRING, ONE, null));

            Register("starts-with", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.BOOLEAN, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, BooleanValue.TRUE).Arg(2, BuiltInAtomicType.STRING, ONE, null));

            Register("ends-with", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.BOOLEAN, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, BooleanValue.TRUE).Arg(2, BuiltInAtomicType.STRING, ONE, null));

            Register("normalize-space", 1, (e) => e.Populate(NormalizeSpace_1.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null));

            Register("string-join", 1, (e) => e.Populate(() => new StringJoin(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, StringValue.EMPTY_STRING));

            Register("string-join", 2, (e) => e.Populate(() => new StringJoin(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, StringValue.EMPTY_STRING).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("substring-before", 2, (e) => e.Populate(SubstringBefore.New(), BuiltInAtomicType.STRING, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));

            Register("substring-before", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.STRING, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(2, BuiltInAtomicType.STRING, ONE, null));

            Register("substring-after", 2, (e) => e.Populate(SubstringAfter.New(), BuiltInAtomicType.STRING, ONE, DCOLL).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, null));

            Register("substring-after", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.STRING, ONE, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, OPT, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));

            Register("substring", 2, (e) => e.Populate(() => new Substring(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, NumericType.GetInstance(), ONE, null));

            Register("substring", 3, (e) => e.Populate(() => new Substring(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, NumericType.GetInstance(), ONE, null).Arg(2, NumericType.GetInstance(), OPT, null));

            Register("true", 0, (e) => e.Populate(() => new ConstantFunction.True(), BuiltInAtomicType.BOOLEAN, ONE, 0));

            Register("false", 0, (e) => e.Populate(() => new ConstantFunction.False(), BuiltInAtomicType.BOOLEAN, ONE, 0));

            Register("count", 1, (e) => e.Populate(() => new Count_1(), BuiltInAtomicType.INTEGER, ONE, 0).Arg(0, Types.Type.ITEM_TYPE, STAR | ABS, Int64Value.ZERO));

            Register("abs", 1, (e) => e.Populate(() => new Abs(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY));

            Register("ceiling", 1, (e) => e.Populate(() => new Ceiling(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY));

            Register("floor", 1, (e) => e.Populate(() => new Floor(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY));

            Register("round", 1, (e) => e.Populate(() => new Round(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY));

            Register("round", 2, (e) => e.Populate(() => new Round(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY).Arg(1, BuiltInAtomicType.INTEGER, ONE, null));

            Register("current-date", 0, (e) => e.Populate(() => new DynamicContextAccessor.CurrentDate(), BuiltInAtomicType.DATE, ONE, LATE));

            Register("current-dateTime", 0, (e) => e.Populate(() => new DynamicContextAccessor.CurrentDateTime(), BuiltInAtomicType.DATE_TIME_STAMP, ONE, LATE));

            Register("current-time", 0, (e) => e.Populate(() => new DynamicContextAccessor.CurrentTime(), BuiltInAtomicType.TIME, ONE, LATE));

            Register("implicit-timezone", 0, (e) => e.Populate(() => new DynamicContextAccessor.ImplicitTimezone(), BuiltInAtomicType.DAY_TIME_DURATION, ONE, LATE));

            RegisterVariadic("concat", 1, (e) => e.Populate(() => new Concat31(), BuiltInAtomicType.STRING, ONE, SEQV).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, null));

            Register("id", 1, (e) => e.Populate(() => new SuperId.Id(), NodeKindTest.ELEMENT, STAR, CDOC | LATE | UO).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY));

            Register("id", 2, (e) => e.Populate(() => new SuperId.Id(), NodeKindTest.ELEMENT, STAR, LATE | UO).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY).Arg(1, Types.Type.NODE_TYPE, ONE | NAV, null));

            Register("idref", 1, (e) => e.Populate(() => new Idref(), Types.Type.NODE_TYPE, STAR, CDOC | LATE).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY));

            Register("idref", 2, (e) => e.Populate(() => new Idref(), Types.Type.NODE_TYPE, STAR, LATE).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY).Arg(1, Types.Type.NODE_TYPE, ONE | NAV, null));

            Register("element-with-id", 1, (e) => e.Populate(() => new SuperId.ElementWithId(), NodeKindTest.ELEMENT, STAR, CDOC | LATE | UO).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY));

            Register("element-with-id", 2, (e) => e.Populate(() => new SuperId.ElementWithId(), NodeKindTest.ELEMENT, STAR, UO).Arg(0, BuiltInAtomicType.STRING, STAR, EMPTY).Arg(1, Types.Type.NODE_TYPE, ONE, null));

            Register("data", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.ANY_ATOMIC, STAR, CITEM | LATE));

            Register("round-half-to-even", 1, (e) => e.Populate(() => new RoundHalfToEven(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY));

            Register("round-half-to-even", 2, (e) => e.Populate(() => new RoundHalfToEven(), NumericType.GetInstance(), OPT, AS_PRIM_ARG0).Arg(0, NumericType.GetInstance(), OPT, EMPTY).Arg(1, BuiltInAtomicType.INTEGER, ONE, null));

            Register("sum", 1, 2, (e) => e.Populate(() => new Sum(), BuiltInAtomicType.ANY_ATOMIC, OPT, UO).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, OPT, null));

            Register("avg", 1, (e) => e.Populate(() => new Average(), BuiltInAtomicType.ANY_ATOMIC, OPT, UO).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY));

            Register("max", 1, (e) => e.Populate(() => new Minimax.Max(), BuiltInAtomicType.ANY_ATOMIC, OPT, DCOLL | UO | CARD0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY));

            Register("max", 2, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.ANY_ATOMIC, OPT, BASE | UO | CARD0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("min", 1, (e) => e.Populate(() => new Minimax.Min(), BuiltInAtomicType.ANY_ATOMIC, OPT, DCOLL | UO | CARD0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY));

            Register("min", 2, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.ANY_ATOMIC, OPT, BASE | UO | CARD0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("translate", 3, (e) => e.Populate(() => new Translate(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));

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

            Register("codepoint-equal", 2, (e) => e.Populate(() => new CodepointEqual(), BuiltInAtomicType.BOOLEAN, OPT, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, BuiltInAtomicType.STRING, OPT, EMPTY));

            Register("codepoints-to-string", 1, (e) => e.Populate(() => new CodepointsToString(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.INTEGER, STAR, null));

            Register("compare", 2, (e) => e.Populate(() => new Compare(), BuiltInAtomicType.INTEGER, OPT, DCOLL).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, BuiltInAtomicType.STRING, OPT, EMPTY));

            Register("compare", 3, (e) => e.Populate(() => new CollatingFunctionFree(), BuiltInAtomicType.INTEGER, OPT, BASE).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(2, BuiltInAtomicType.STRING, ONE, null));

            Register("default-collation", 0, (e) => e.Populate(() => new StaticContextAccessor.DefaultCollation(), BuiltInAtomicType.STRING, ONE, DCOLL));

            Register("lang", 1, (e) => e.Populate(() => new Lang(), BuiltInAtomicType.BOOLEAN, ONE, CITEM | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));

            Register("lang", 2, (e) => e.Populate(() => new Lang(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, Types.Type.NODE_TYPE, ONE | INS, null));

            Register("nilled", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.BOOLEAN, OPT, CITEM | LATE));

            Register("node-name", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.QNAME, OPT, CITEM | LATE));

            Register("nilled", 1, (e) => e.Populate(() => new Nilled_1(), BuiltInAtomicType.BOOLEAN, OPT, 0).Arg(0, Types.Type.NODE_TYPE, OPT | INS, EMPTY));

            Register("node-name", 1, (e) => e.Populate(() => new NodeName_1(), BuiltInAtomicType.QNAME, OPT, 0).Arg(0, Types.Type.NODE_TYPE, OPT | INS, EMPTY));

            Register("root", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), Types.Type.NODE_TYPE, ONE, CITEM | LATE));

            Register("static-base-uri", 0, (e) => e.Populate(() => new StaticBaseUri(), BuiltInAtomicType.ANY_URI, OPT, BASE | LATE));

            Register("document-uri", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.ANY_URI, OPT, CITEM | LATE));

            Register("document-uri", 1, (e) => e.Populate(() => new DocumentUri_1(), BuiltInAtomicType.ANY_URI, OPT, LATE).Arg(0, Types.Type.NODE_TYPE, OPT | INS, EMPTY));

            Register("matches", 2, (e) => e.Populate(() => new RegexFunctionSansFlags(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("matches", 3, (e) => e.Populate(() => new Matches(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));

            Register("replace", 3, (e) => e.Populate(() => new RegexFunctionSansFlags(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));

            Register("tokenize", 2, (e) => e.Populate(() => new RegexFunctionSansFlags(), BuiltInAtomicType.STRING, STAR, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("tokenize", 3, (e) => e.Populate(() => new Tokenize_3(), BuiltInAtomicType.STRING, STAR, 0).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));

            Register("doc", 1, (e) => e.Populate(() => new Doc(), NodeKindTest.DOCUMENT, OPT, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));

            Register("doc-available", 1, (e) => e.Populate(() => new DocAvailable(), BuiltInAtomicType.BOOLEAN, ONE, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, BooleanValue.FALSE));

            Register("collection", 0, (e) => e.Populate(() => new CollectionFn(), Types.Type.ITEM_TYPE, STAR, BASE | LATE));

            Register("collection", 1, (e) => e.Populate(() => new CollectionFn(), Types.Type.ITEM_TYPE, STAR, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));

            Register("resolve-uri", 1, (e) => e.Populate(() => new ResolveURI(), BuiltInAtomicType.ANY_URI, OPT, CARD0 | BASE).Arg(0, BuiltInAtomicType.STRING, OPT, null));

            Register("resolve-uri", 2, (e) => e.Populate(() => new ResolveURI(), BuiltInAtomicType.ANY_URI, OPT, CARD0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("namespace-uri-for-prefix", 2, (e) => e.Populate(() => new NamespaceForPrefix(), BuiltInAtomicType.ANY_URI, OPT, 0).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, NodeKindTest.ELEMENT, ONE | INS, null));

            Register("in-scope-prefixes", 1, (e) => e.Populate(() => new InScopePrefixes(), BuiltInAtomicType.STRING, STAR, 0).Arg(0, NodeKindTest.ELEMENT, ONE | INS, null));

            Register("encode-for-uri", 1, (e) => e.Populate(() => new EncodeForUri(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING));

            Register("replace", 4, (e) => e.Populate(() => Replace.Make20(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, OPT, StringValue.EMPTY_STRING).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null).Arg(3, BuiltInAtomicType.STRING, ONE, null));

        }
        public static XPath20FunctionSet GetInstance() => _i;
    }
}
