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
    // XPath 3.0 additions on top of XPath 2.0 (upstream registry/XPath30FunctionSet.java).
    internal sealed class XPath30FunctionSet : BuiltInFunctionSet
    {
        private static readonly XPath30FunctionSet _i = new XPath30FunctionSet();
        public XPath30FunctionSet()
        {
            ImportFunctionSet(XPath20FunctionSet.GetInstance());
            Register("serialize", 2, (e) => e.Populate(Serialize.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, AnyItemType.GetInstance(), STAR, null).Arg(1, Types.Type.ITEM_TYPE, OPT, null));

            Register("head", 1, (e) => e.Populate(() => new HeadFn(), AnyItemType.GetInstance(), OPT, FILTER).Arg(0, AnyItemType.GetInstance(), STAR | TRA, null));

            Register("tail", 1, (e) => e.Populate(() => new TailFn(), AnyItemType.GetInstance(), STAR, AS_ARG0 | FILTER).Arg(0, AnyItemType.GetInstance(), STAR | TRA, null));

            Register("format-number", 2, (e) => e.Populate(() => new FormatNumber(), BuiltInAtomicType.STRING, ONE, LATE).Arg(0, NumericType.GetInstance(), OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("format-number", 3, (e) => e.Populate(() => new FormatNumber(), BuiltInAtomicType.STRING, ONE, NS | LATE).Arg(0, NumericType.GetInstance(), OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, OPT, null));

            Register("format-date", 2, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("format-date", 5, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, OPT, null).Arg(3, BuiltInAtomicType.STRING, OPT, null).Arg(4, BuiltInAtomicType.STRING, OPT, null));

            Register("format-dateTime", 2, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("format-dateTime", 5, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.DATE_TIME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, OPT, null).Arg(3, BuiltInAtomicType.STRING, OPT, null).Arg(4, BuiltInAtomicType.STRING, OPT, null));

            Register("format-time", 2, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("format-time", 5, (e) => e.Populate(() => new FormatDate(), BuiltInAtomicType.STRING, OPT, CARD0).Arg(0, BuiltInAtomicType.TIME, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, OPT, null).Arg(3, BuiltInAtomicType.STRING, OPT, null).Arg(4, BuiltInAtomicType.STRING, OPT, null));

            Register("format-integer", 2, (e) => e.Populate(() => new FormatInteger(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.INTEGER, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("format-integer", 3, (e) => e.Populate(() => new FormatInteger(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.INTEGER, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, OPT, null));

            Register("parse-xml", 1, (e) => e.Populate(() => new ParseXml(), new DocumentNodeTest(NodeKindTest.ELEMENT), OPT, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));

            Register("parse-xml-fragment", 1, (e) => e.Populate(() => new ParseXmlFragment(), NodeKindTest.DOCUMENT, OPT, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));

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

            Register("sort", 1, (e) => e.Populate(() => new Sort_1(), AnyItemType.GetInstance(), STAR, 0).Arg(0, AnyItemType.GetInstance(), STAR, null));

            Register("sort", 2, (e) => e.Populate(() => new Sort_2(), AnyItemType.GetInstance(), STAR, 0).Arg(0, AnyItemType.GetInstance(), STAR, null).Arg(1, BuiltInAtomicType.STRING, OPT, null));

            // function(item()) as xs:anyAtomicType*: the coercion to it is what checks the key function's arity and arguments
            var __sortKey = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_ITEM }, SequenceType.ATOMIC_SEQUENCE);
            Register("sort", 3, (e) => e.Populate(() => new Sort_3(), AnyItemType.GetInstance(), STAR, 0).Arg(0, AnyItemType.GetInstance(), STAR, null).Arg(1, BuiltInAtomicType.STRING, OPT, null).Arg(2, __sortKey, ONE, null));

            Register("generate-id", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.STRING, ONE, CITEM | LATE));

            Register("generate-id", 1, (e) => e.Populate(GenerateIdFn1.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, Types.Type.NODE_TYPE, OPT | INS, StringValue.EMPTY_STRING));

            Register("has-children", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.BOOLEAN, ONE, CITEM | LATE));

            Register("has-children", 1, (e) => e.Populate(() => new HasChildren_1(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, AnyNodeTest.GetInstance(), OPT | INS, null));

            Register("uri-collection", 0, (e) => e.Populate(() => new UriCollection(), BuiltInAtomicType.ANY_URI, STAR, LATE));

            Register("uri-collection", 1, (e) => e.Populate(() => new UriCollection(), BuiltInAtomicType.ANY_URI, STAR, LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));

            Register("unparsed-text", 1, (e) => e.Populate(() => new UnparsedText(), BuiltInAtomicType.STRING, OPT, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));

            Register("unparsed-text", 2, (e) => e.Populate(() => new UnparsedText(), BuiltInAtomicType.STRING, OPT, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("unparsed-text-lines", 1, (e) => e.Populate(() => new UnparsedTextLines(), BuiltInAtomicType.STRING, STAR, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));

            Register("unparsed-text-lines", 2, (e) => e.Populate(() => new UnparsedTextLines(), BuiltInAtomicType.STRING, STAR, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("unparsed-text-available", 1, (e) => e.Populate(() => new UnparsedTextAvailable(), BuiltInAtomicType.BOOLEAN, ONE, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, BooleanValue.FALSE));

            Register("unparsed-text-available", 2, (e) => e.Populate(() => new UnparsedTextAvailable(), BuiltInAtomicType.BOOLEAN, ONE, BASE | LATE).Arg(0, BuiltInAtomicType.STRING, OPT, BooleanValue.FALSE).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("environment-variable", 1, (e) => e.Populate(() => new EnvironmentVariable(), BuiltInAtomicType.STRING, OPT, LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));

            Register("available-environment-variables", 0, (e) => e.Populate(() => new AvailableEnvironmentVariables(), BuiltInAtomicType.STRING, STAR, LATE));

            Register("serialize", 1, (e) => e.Populate(Serialize.New(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, AnyItemType.GetInstance(), STAR, null));

            Register("analyze-string", 2, (e) => e.Populate(() => new RegexFunctionSansFlags(), NodeKindTest.ELEMENT, ONE, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null));

            Register("analyze-string", 3, (e) => e.Populate(() => new AnalyzeStringFn(), NodeKindTest.ELEMENT, ONE, LATE | NEW).Arg(0, BuiltInAtomicType.STRING, OPT, null).Arg(1, BuiltInAtomicType.STRING, ONE, null).Arg(2, BuiltInAtomicType.STRING, ONE, null));

            Register("function-arity", 1, (e) => e.Populate(() => new FunctionArity(), BuiltInAtomicType.INTEGER, ONE, 0).Arg(0, AnyFunctionType.GetInstance(), ONE, null));

            Register("function-name", 1, (e) => e.Populate(() => new FunctionName(), BuiltInAtomicType.QNAME, OPT, 0).Arg(0, AnyFunctionType.GetInstance(), ONE, null));

            {
                var __forEachPairArg = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_ITEM, SequenceType.SINGLE_ITEM }, SequenceType.ANY_SEQUENCE);
                Register("for-each-pair", 3, (e) => e.Populate(() => new ForEachPairFn(), AnyItemType.GetInstance(), STAR, LATE).Arg(0, AnyItemType.GetInstance(), STAR, EMPTY).Arg(1, AnyItemType.GetInstance(), STAR, EMPTY).Arg(2, __forEachPairArg, ONE, null));
            }

            Register("apply", 2, (e) => e.Populate(() => new ApplyFn(), AnyItemType.GetInstance(), STAR, LATE).Arg(0, AnyFunctionType.GetInstance(), ONE, null).Arg(1, ArrayItemType.ANY_ARRAY_TYPE, ONE, null));

            Register("innermost", 1, (e) => e.Populate(() => new Innermost(), AnyNodeTest.GetInstance(), STAR, 0).Arg(0, AnyNodeTest.GetInstance(), STAR | NAV, null));

            Register("outermost", 1, (e) => e.Populate(() => new Outermost(), AnyNodeTest.GetInstance(), STAR, AS_ARG0 | FILTER).Arg(0, AnyNodeTest.GetInstance(), STAR | TRA, null));

            Register("path", 0, (e) => e.Populate(() => new ContextItemAccessorFunction(), BuiltInAtomicType.STRING, OPT, CITEM | LATE));

            Register("path", 1, (e) => e.Populate(() => new Path_1(), BuiltInAtomicType.STRING, OPT, 0).Arg(0, AnyNodeTest.GetInstance(), OPT | NAV, null));

            Register("function-lookup", 2, (e) => e.Populate(() => new FunctionLookup(), AnyFunctionType.GetInstance(), OPT, FOCUS | DEPENDS_ON_STATIC_CONTEXT | LATE).Arg(0, BuiltInAtomicType.QNAME, ONE, null).Arg(1, BuiltInAtomicType.INTEGER, ONE, null));

        }
        public static XPath30FunctionSet GetInstance() => _i;
    }
}
