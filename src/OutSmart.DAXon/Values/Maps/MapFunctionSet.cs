////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Collections.Zeno;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values.Maps
{
    internal sealed class MapFunctionSet : BuiltInFunctionSet
    {
        private static readonly MapFunctionSet instance31 = new MapFunctionSet(31);
        private static readonly MapFunctionSet instance40 = new MapFunctionSet(40);

        public override string ConventionalPrefix => "map";
        private MapFunctionSet(int version)
        {
            Init(version);
        }

        public static MapFunctionSet GetInstance(int version)
        {
            return version >= 40 ? instance40 : instance31;
        }

        private void Init(int version)
        {
            Register("merge", 1, (e) => e.Populate(() => new MapMerge(), MapType.ANY_MAP_TYPE, ONE, 0).Arg(0, MapType.ANY_MAP_TYPE, STAR | INS, null));
            SpecificFunctionType ON_DUPLICATES_CALLBACK_TYPE = new SpecificFunctionType(new SequenceType[] { SequenceType.ANY_SEQUENCE, SequenceType.ANY_SEQUENCE }, SequenceType.ANY_SEQUENCE);
            SequenceType oneOnDuplicatesFunction = SequenceType.MakeSequenceType(ON_DUPLICATES_CALLBACK_TYPE, StaticProperty.EXACTLY_ONE);
            RecordTest KVP_TYPE_EXTENSIBLE = RecordTest.Extensible(Field("key", SequenceType.SINGLE_ATOMIC, false), Field("value", SequenceType.ANY_SEQUENCE, false));
            RecordTest KVP_TYPE_INEXTENSIBLE = RecordTest.NonExtensible(Field("key", SequenceType.SINGLE_ATOMIC, false), Field("value", SequenceType.ANY_SEQUENCE, false));
            OptionsParameter mergeOptionDetails = new OptionsParameter();
            mergeOptionDetails.AddAllowedOption("duplicates", SequenceType.SINGLE_STRING, StringValue.Bmp("use-first"));

            // duplicates=unspecified is retained because that's what the XSLT 3.0 Rec incorrectly uses
            mergeOptionDetails.SetAllowedValues("duplicates", "FOJS0005", "use-first", "use-last", "combine", "reject", "unspecified", "use-any", "use-callback");
            mergeOptionDetails.AddAllowedOption(MapMerge.errorCodeKey, SequenceType.SINGLE_STRING, StringValue.Bmp("FOJS0003"));
            mergeOptionDetails.AddAllowedOption(MapMerge.keyTypeKey, SequenceType.SINGLE_STRING, StringValue.Bmp("anyAtomicType"));
            mergeOptionDetails.AddAllowedOption(MapMerge.finalKey, SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            mergeOptionDetails.AddAllowedOption(MapMerge.onDuplicatesKey, oneOnDuplicatesFunction, null);
            Register("merge", 2, (e) => e.Populate(() => new MapMerge(), MapType.ANY_MAP_TYPE, ONE, 0).Arg(0, MapType.ANY_MAP_TYPE, STAR, null).Arg(1, MapType.ANY_MAP_TYPE, ONE, null).SetOptionDetails(mergeOptionDetails));
            Register("put", 3, (e) => e.Populate(() => new MapPut(), MapType.ANY_MAP_TYPE, ONE, 0).Arg(0, MapType.ANY_MAP_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, ONE | ABS, null).Arg(2, AnyItemType.GetInstance(), STAR | NAV, null));
            Register("contains", 2, (e) => e.Populate(() => new MapContains(), BuiltInAtomicType.BOOLEAN, ONE, 0).Arg(0, MapType.ANY_MAP_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, ONE | ABS, null));
            Register("remove", 2, (e) => e.Populate(() => new MapRemove(), MapType.ANY_MAP_TYPE, ONE, 0).Arg(0, MapType.ANY_MAP_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, STAR | ABS, null));
            Register("keys", 1, (e) => e.Populate(() => new MapKeys(), BuiltInAtomicType.ANY_ATOMIC, STAR, 0).Arg(0, MapType.ANY_MAP_TYPE, ONE | INS, null));
            Register("size", 1, (e) => e.Populate(() => new MapSize(), BuiltInAtomicType.INTEGER, ONE, 0).Arg(0, MapType.ANY_MAP_TYPE, ONE | INS, null));
            Register("entry", 2, (e) => e.Populate(() => new MapEntry(), MapType.ANY_MAP_TYPE, ONE, 0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, ONE | ABS, null).Arg(1, AnyItemType.GetInstance(), STAR | NAV, null));
            Register("find", 2, (e) => e.Populate(() => new MapFind(), ArrayItemType.ANY_ARRAY_TYPE, ONE, 0).Arg(0, AnyItemType.GetInstance(), STAR | INS, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, ONE | ABS, null));
            ItemType actionType = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_ATOMIC, SequenceType.ANY_SEQUENCE }, SequenceType.ANY_SEQUENCE);
            Register("for-each", 2, (e) => e.Populate(() => new MapForEach(), AnyItemType.GetInstance(), STAR, 0).Arg(0, MapType.ANY_MAP_TYPE, ONE | INS, null).Arg(1, actionType, ONE | INS, null));
            Register("get", 2, (e) => e.Populate(() => new MapGet(), AnyItemType.GetInstance(), STAR, 0).Arg(0, MapType.ANY_MAP_TYPE, ONE | INS, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, ONE | ABS, null));
            if (version >= 40)
            {
                // Produced pairs are exactly {key, value}; accepted ones may carry extra fields.
                ItemType filterPredicateType = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_ATOMIC, SequenceType.ANY_SEQUENCE }, SequenceType.SINGLE_BOOLEAN);
                Register("entries", 1, (e) => e.Populate(() => new MapEntries(), MapType.ANY_MAP_TYPE, STAR, 0).Arg(0, MapType.ANY_MAP_TYPE, ONE | INS, null));
                Register("filter", 2, (e) => e.Populate(() => new MapFilter(), MapType.ANY_MAP_TYPE, ONE, 0).Arg(0, MapType.ANY_MAP_TYPE, ONE | INS, null).Arg(1, filterPredicateType, ONE | INS, null));
                Register("pair", 2, (e) => e.Populate(() => new MapPair(), KVP_TYPE_INEXTENSIBLE, ONE, 0).Arg(0, BuiltInAtomicType.ANY_ATOMIC, ONE | ABS, null).Arg(1, AnyItemType.GetInstance(), STAR | NAV, null));
                Register("pairs", 1, (e) => e.Populate(() => new MapPairs(), KVP_TYPE_INEXTENSIBLE, STAR, 0).Arg(0, MapType.ANY_MAP_TYPE, ONE | INS, null));
                Register("of-pairs", 1, 2, (e) => e.Populate(() => new MapOfPairs(), MapType.ANY_MAP_TYPE, ONE, 0).Arg(0, KVP_TYPE_EXTENSIBLE, STAR | INS, null).Arg(1, ON_DUPLICATES_CALLBACK_TYPE, OPT | INS, null));

                // map:build stays unregistered: its defaulted $key/$value resolve fn:identity through
                // MakeFunction40, and every 4.0 fn: set throws "requires Saxon-PE or higher" here.
            }
        }

        public override NamespaceUri GetNamespace()
        {
            return NamespaceUri.MAP_FUNCTIONS;
        }

        /// <summary>
        /// Implementation of the XPath 3.1 function map:contains(IMap, key) =&gt; boolean
        /// </summary>
        internal sealed class MapContains : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                // The static map(*) argument check isn't inserted for a non-map function item, so guard the
                // cast and raise XPTY0004 (upstream relies on the type-checker to produce the same code).
                // Read Head() once — arguments[0] may be a LazySequence (single-read).
                IItem arg0Head = arguments[0].Head();
                if (!(arg0Head is MapItem))
                    throw new XPathException("The first argument of map:contains() must be a map", "XPTY0004");
                MapItem map = (MapItem)arg0Head;
                AtomicValue key = (AtomicValue)arguments[1].Head();
                return BooleanValue.Get(map[key] != null);
            }
        }

        /// <summary>
        /// Implementation of the proposed XPath 4.0 function map:filter(IMap, function(*)) =&gt; IMap
        /// </summary>
        internal sealed class MapFilter : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                MapItem map = (MapItem)arguments[0].Head();
                IFunctionItem fn = (IFunctionItem)arguments[1].Head();
                MapItem result = new HashTrieMap();
                foreach (KeyValuePair pair in map.KeyValuePairs())
                {
                    Core.Controller.CheckActiveTimeoutPerStep();
                    BooleanValue match = (BooleanValue)DynamicCall(fn, context, new ISequence[] { pair.key, pair.value }).Head();
                    if (match.GetBooleanValue())
                    {
                        result = result.AddEntry(pair.key, pair.value);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Implementation of the XPath 3.1 function map:get(IMap, key) =&gt; value
        /// </summary>
        internal sealed class MapGet : SystemFunction
        {
            string pendingWarning = null;
            public override void SupplyTypeInformation(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType, Expression[] arguments)
            {
                ItemType it = arguments[0].GetItemType();
                if (it is IRecordType && arguments.Length == 2)
                {
                    if (arguments[1] is StringLiteral)
                    {
                        string key = ((StringLiteral)arguments[1]).Stringify();
                        if (((IRecordType)it).GetFieldType(key) == null)
                        {
                            XPathException xe = new XPathException("Field " + key + " is not defined for tuple type " + it, DAXonErrorCode.SXTT0001);
                            xe.SetIsTypeError(true);
                            throw xe;
                        }
                    }

                    TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
                    Affinity relation = th.Relationship(arguments[1].GetItemType(), BuiltInAtomicType.STRING);
                    if (relation == Affinity.DISJOINT)
                    {
                        XPathException xe = new XPathException("Key for tuple type must be a string (actual type is " + arguments[1].GetItemType(), "XPTY0004");
                        xe.SetIsTypeError(true);
                        throw xe;
                    }
                }
            }

            public override ItemType GetResultItemType(Expression[] args)
            {
                if (args.Length == 2)
                {
                    ItemType mapType = args[0].GetItemType();
                    if (mapType is RecordTest && args[1] is StringLiteral)
                    {
                        string key = ((StringLiteral)args[1]).Stringify();
                        RecordTest tit = (RecordTest)mapType;
                        SequenceType valueType = tit.GetFieldType(key);
                        if (valueType == null)
                        {
                            Warning("Field " + key + " is not defined in record type");
                            return AnyItemType.GetInstance();
                        }
                        else
                        {
                            return valueType.PrimaryType;
                        }
                    }
                    else if (mapType is MapType)
                    {
                        return ((MapType)mapType).ValueType.PrimaryType;
                    }
                }

                return base.GetResultItemType(args);
            }

            public override int GetCardinality(Expression[] args)
            {
                ItemType mapType = args[0].GetItemType();
                if (mapType is RecordTest && args[1] is StringLiteral)
                {
                    string key = ((StringLiteral)args[1]).Stringify();
                    RecordTest tit = (RecordTest)mapType;
                    SequenceType valueType = tit.GetFieldType(key);
                    if (valueType == null)
                    {
                        Warning("Field " + key + " is not defined in record type");
                        return StaticProperty.ALLOWS_MANY;
                    }
                    else
                    {
                        return valueType.GetCardinality();
                    }
                }
                else if (mapType is MapType)
                {
                    return Cardinality.Union(((MapType)mapType).ValueType.GetCardinality(), StaticProperty.ALLOWS_ZERO);
                }
                else
                {
                    return base.GetCardinality(args);
                }
            }

            public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
            {
                if (pendingWarning != null && !pendingWarning.Equals("DONE"))
                {
                    visitor.IssueWarning(pendingWarning, DAXonErrorCode.SXWN9038, arguments[0].GetLocation());
                    pendingWarning = "DONE";
                }

                return null;
            }

            private void Warning(string message)
            {
                if (pendingWarning != "DONE")
                {
                    pendingWarning = message;
                }
            }

            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                IItem arg0Head = arguments[0].Head();
                if (!(arg0Head is MapItem))
                    throw new XPathException("The first argument of map:get() must be a map", "XPTY0004");
                MapItem map = (MapItem)arg0Head;
                AtomicValue key = (AtomicValue)arguments[1].Head();
                ISequence value = map[key];
                if (value == null)
                {
                    if (arguments.Length > 2)
                    {
                        IFunctionItem fn = (IFunctionItem)arguments[2].Head();
                        return DynamicCall(fn, context, key);
                    }
                    else
                    {
                        return EmptySequence.GetInstance();
                    }
                }
                else
                {
                    return value;
                }
            }

            public override Elaborator GetElaborator()
            {
                return GetArity() == 2 ? new MapGetElaborator() : null;
            }

            // Two-argument map:get consumes both arguments unconditionally, so they are read as eager
            // items instead of per-call LazySequence wrappers (the type-checker's converters/checkers
            // sit inside the argument expressions and still run). Anything not statically exactly-one
            // falls back to the generic function-call elaborator.
            private sealed class MapGetElaborator : PullElaborator
            {
                public override IPullEvaluator ElaborateForPull()
                {
                    SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                    int c0 = expr.GetArg(0).GetCardinality();
                    int c1 = expr.GetArg(1).GetCardinality();
                    if (Cardinality.AllowsZero(c0) || Cardinality.AllowsMany(c0) || Cardinality.AllowsZero(c1) || Cardinality.AllowsMany(c1)
                        || ErrorExpression.IsContainedIn(expr.GetArg(0)) || ErrorExpression.IsContainedIn(expr.GetArg(1)))
                    {
                        SystemFunctionCall.SystemFunctionCallElaborator generic = new SystemFunctionCall.SystemFunctionCallElaborator();
                        generic.SetExpression(expr);
                        return generic.ElaborateForPull();
                    }

                    // A non-lazy argument (focus-dependent, constant-folded error) runs up-front in
                    // argument order, exactly where the generic path's Eagerly() evaluator runs it —
                    // notably BEFORE the map-type check.
                    bool eager0 = !expr.GetArg(0).SupportsLazyEvaluation();
                    bool eager1 = !expr.GetArg(1).SupportsLazyEvaluation();
                    IItemEvaluator mapEval = expr.GetArg(0).MakeElaborator().ElaborateForItem();
                    IItemEvaluator keyEval = expr.GetArg(1).MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        try
                        {
                            IItem pre0 = eager0 ? mapEval.Eval(context) : null;
                            IItem pre1 = eager1 ? keyEval.Eval(context) : null;
                            IItem m = eager0 ? pre0 : mapEval.Eval(context);
                            if (!(m is MapItem map))
                                throw new XPathException("The first argument of map:get() must be a map", "XPTY0004");
                            AtomicValue key = (AtomicValue)(eager1 ? pre1 : keyEval.Eval(context));
                            ISequence value = map[key];
                            return (value ?? EmptySequence.GetInstance()).Iterate();
                        }
                        catch (XPathException err)
                        {
                            throw err.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                        }
                    };
                }
            }
        }

        /// <summary>
        /// Implementation of the XPath 3.1 function map:find(item()*, key) =&gt; array
        /// </summary>
        internal sealed class MapFind : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                IList<IGroundedValue> result = new List<IGroundedValue>();
                AtomicValue key = (AtomicValue)arguments[1].Head();
                try
                {
                    ProcessSequence(arguments[0], key, result);
                }
                catch (RecursionDepthError e) when (!e.Described)
                {
                    throw e.Describe("Too many nested levels in map:find. The searched input is too deeply nested.", DAXonErrorCode.SXLM0001, null);
                }

                return new SimpleArrayItem(result);
            }

            private void ProcessSequence(ISequence @in, AtomicValue key, IList<IGroundedValue> result)
            {
                // The depth is the input's: nested JSON reaches it.
                StackGuard.Probe();
                SequenceTool.Supply(@in.Iterate(), (item) =>
                {
                    if (item is ArrayItem)
                    {
                        foreach (ISequence sequence in ((ArrayItem)item).Members())
                        {
                            ProcessSequence(sequence, key, result);
                        }
                    }
                    else if (item is MapItem)
                    {
                        IGroundedValue value = ((MapItem)item)[key];
                        if (value != null)
                        {
                            result.Add(value);
                        }

                        foreach (KeyValuePair entry in ((MapItem)item).KeyValuePairs())
                        {
                            ProcessSequence(entry.value, key, result);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Implementation of the extension function map:entry(key, value) =&gt; IMap
        /// </summary>
        internal sealed class MapEntry : SystemFunction
        {

            public override string StreamerName => "MapEntry";
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                AtomicValue key = (AtomicValue)arguments[0].Head();
                if (arguments[1] is IItem)
                {
                    return new SingleEntryMap(key, (IItem)arguments[1]);
                }

                IGroundedValue value = arguments[1].Materialize();
                return new SingleEntryMap(key, value);
            }

            public override Elaborator GetElaborator()
            {
                return new MapEntryElaborator();
            }

            // map:entry consumes both arguments unconditionally (key.Head(), value.Materialize()),
            // so a statically singleton key and value are read as eager items with no per-call
            // LazySequence wrappers; any other cardinality falls back to the generic elaborator.
            private sealed class MapEntryElaborator : ItemElaborator
            {
                public override IItemEvaluator ElaborateForItem()
                {
                    SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                    int c0 = expr.GetArg(0).GetCardinality();
                    int c1 = expr.GetArg(1).GetCardinality();
                    if (Cardinality.AllowsZero(c0) || Cardinality.AllowsMany(c0) || Cardinality.AllowsZero(c1) || Cardinality.AllowsMany(c1)
                        || ErrorExpression.IsContainedIn(expr.GetArg(0)) || ErrorExpression.IsContainedIn(expr.GetArg(1)))
                    {
                        SystemFunctionCall.SystemFunctionCallElaborator generic = new SystemFunctionCall.SystemFunctionCallElaborator();
                        generic.SetExpression(expr);
                        return generic.ElaborateForItem();
                    }

                    // Non-lazy args run up-front in argument order, mirroring the generic Eagerly() path.
                    bool eager0 = !expr.GetArg(0).SupportsLazyEvaluation();
                    bool eager1 = !expr.GetArg(1).SupportsLazyEvaluation();
                    IItemEvaluator keyEval = expr.GetArg(0).MakeElaborator().ElaborateForItem();
                    IItemEvaluator valueEval = expr.GetArg(1).MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        try
                        {
                            IItem pre0 = eager0 ? keyEval.Eval(context) : null;
                            IItem pre1 = eager1 ? valueEval.Eval(context) : null;
                            AtomicValue key = (AtomicValue)(eager0 ? pre0 : keyEval.Eval(context));
                            IGroundedValue value = eager1 ? pre1 : valueEval.Eval(context);
                            return new SingleEntryMap(key, value ?? EmptySequence.GetInstance());
                        }
                        catch (XPathException err)
                        {
                            throw err.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                        }
                    };
                }
            }

            public override ItemType GetResultItemType(Expression[] args)
            {
                // The atomized key type is normally a plain (atomic/union) type; when the key expression's
                // static type doesn't atomize to a plain type (returns item()/AnyItemType), fall back to
                // xs:anyAtomicType rather than InvalidCast — a map key is always atomic. Static-inference only.
                IPlainType ku = args[0].GetItemType().GetAtomizedItemType() as IPlainType ?? BuiltInAtomicType.ANY_ATOMIC;
                IAtomicType ka;
                if (ku is IAtomicType)
                {
                    ka = (IAtomicType)ku;
                }
                else
                {
                    ka = ku.GetPrimitiveItemType();
                }

                return new MapType(ka, SequenceType.MakeSequenceType(args[1].GetItemType(), args[1].GetCardinality()));
            }
        }

        /// <summary>
        /// Implementation of the function map:for-each(IMap, Function) =&gt; item()*
        /// </summary>
        internal sealed class MapForEach : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                MapItem map = (MapItem)arguments[0].Head();
                IFunctionItem fn = (IFunctionItem)arguments[1].Head();
                // Plain list accumulator: the per-result ZenoSequence.Append is persistent (it
                // re-copies the master list and last segment on every call), which turns a large
                // map into quadratic-ish churn.
                List<IItem> results = new List<IItem>();
                foreach (KeyValuePair pair in map.KeyValuePairs())
                {
                    Core.Controller.CheckActiveTimeoutPerStep();
                    ISequence seq = DynamicCall(fn, context, new ISequence[] { pair.key, pair.value });
                    ISequenceIterator it = seq.Iterate();
                    for (IItem item; (item = it.Next()) != null;)
                    {
                        results.Add(item);
                    }
                }

                return new SequenceExtent.Of<IItem>(results);
            }
        }

        /// <summary>
        /// Implementation of the proposed 4.0 function map:entries(IMap) =&gt; map(*)*
        /// </summary>
        internal sealed class MapEntries : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                MapItem map = (MapItem)arguments[0].Head();
                List<IItem> results = new List<IItem>();
                foreach (KeyValuePair pair in map.KeyValuePairs())
                {
                    results.Add(new SingleEntryMap(pair.key, pair.value));
                }

                return new SequenceExtent.Of<IItem>(results);
            }
        }

        /// <summary>
        /// Implementation of the proposed 4.0 function map:pair(key, value) =&gt; record(key, value)
        /// </summary>
        internal sealed class MapPair : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                AtomicValue key = (AtomicValue)arguments[0].Head();
                IGroundedValue value = arguments[1].Materialize();
                DictionaryMap map = new DictionaryMap(2);
                map.InitialPut("key", key);
                map.InitialPut("value", value);
                return map;
            }
        }

        /// <summary>
        /// Implementation of the proposed 4.0 function map:pairs(IMap) =&gt; record(key, value)*
        /// </summary>
        internal sealed class MapPairs : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                MapItem map = (MapItem)arguments[0].Head();
                ZenoSequence results = new ZenoSequence();
                foreach (KeyValuePair pair in map.KeyValuePairs())
                {
                    DictionaryMap kvp = new DictionaryMap(2);
                    kvp.InitialPut("key", pair.key);
                    kvp.InitialPut("value", pair.value);
                    results = results.AppendSequence(kvp);
                }

                return results;
            }
        }

        internal sealed class MapKeys : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                MapItem map = (MapItem)arguments[0].Head();
                if (arguments.Length == 1)
                {
                    return SequenceTool.ToLazySequence(map.Keys());
                }
                else
                {
                    IFunctionItem fn = (IFunctionItem)arguments[1].Head();
                    ZenoSequence results = new ZenoSequence();
                    foreach (KeyValuePair pair in map.KeyValuePairs())
                    {
                        Core.Controller.CheckActiveTimeoutPerStep();
                        BooleanValue selected = (BooleanValue)fn.Call(context, new ISequence[] { pair.value }).Head();
                        if (selected.GetBooleanValue())
                        {
                            results = results.Append(pair.key);
                        }
                    }

                    return results;
                }
            }
        }

        internal sealed class MapMerge : SystemFunction
        {
            public static readonly string finalKey = "Q{" + NamespaceConstant.SAXON + "}final";
            public static readonly string keyTypeKey = "Q{" + NamespaceConstant.SAXON + "}key-type";
            public static readonly string onDuplicatesKey = "Q{" + NamespaceConstant.SAXON + "}on-duplicates";
            public static readonly string errorCodeKey = "Q{" + NamespaceConstant.SAXON + "}duplicates-error-code";
            private string duplicates = "use-first";
            private string duplicatesErrorCode = "FOJS0003";
            private IFunctionItem onDuplicates = null;
            private bool allStringKeys = false;
            private bool treatAsFinal = false;

            public override string StreamerName => "NewMap";
            public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
            {
                if (arguments.Length == 2 && arguments[1] is Literal)
                {
                    MapItem options = (MapItem)((Literal)arguments[1]).GroundedValue.Head();
                    Dictionary<string, IGroundedValue> values = Details.optionDetails.ProcessSuppliedOptions(options, visitor.StaticContext.MakeEarlyEvaluationContext());
                    string duplicates = ((StringValue)values.GetOrDefault("duplicates")).GetStringValue();
                    string duplicatesErrorCode = ((StringValue)values.GetOrDefault(errorCodeKey)).GetStringValue();
                    IFunctionItem onDuplicates = values.TryGetValue(onDuplicatesKey, out var __od1) ? (IFunctionItem)__od1 : null;
                    if (onDuplicates != null)
                    {
                        duplicates = "use-callback";
                    }

                    bool isFinal = ((BooleanValue)values.GetOrDefault(finalKey)).GetBooleanValue();
                    string keyType = ((StringValue)values.GetOrDefault(keyTypeKey)).GetStringValue();
                    MapMerge mm2 = (MapMerge)instance31.MakeFunction("merge", 1);
                    mm2.duplicates = duplicates;
                    mm2.duplicatesErrorCode = duplicatesErrorCode;
                    mm2.onDuplicates = onDuplicates;
                    mm2.allStringKeys = keyType.Equals("string");
                    mm2.treatAsFinal = isFinal;
                    return mm2.MakeFunctionCall(arguments[0]);
                }

                return base.MakeOptimizedFunctionCall(visitor, contextInfo, arguments);
            }

            public override ItemType GetResultItemType(Expression[] args)
            {
                ItemType it = args[0].GetItemType();
                if (it == ErrorType.GetInstance())
                {
                    return MapType.EMPTY_MAP_TYPE;
                }
                else if (it is MapType)
                {
                    bool maybeCombined = true; // see bug 3980
                    if (args.Length == 1)
                    {
                        // Single-argument (optimized) form: the merge options have been folded into
                        // instance fields by MakeOptimizedFunctionCall. Consult the stored duplicates
                        // policy so that the "combine" case still widens the value type to a sequence.
                        maybeCombined = this.duplicates == "combine";
                    }
                    else if (args[1] is Literal)
                    {
                        MapItem options = (MapItem)((Literal)args[1]).GroundedValue.Head();
                        if (options != null)
                        {
                            IGroundedValue dupes = options[StringValue.Bmp("duplicates")];
                            try
                            {
                                if (dupes != null && dupes.GetStringValue() != "combine")
                                {
                                    maybeCombined = false;
                                }
                            }
                            catch (XPathException)
                            {
                            }
                        }
                    }

                    if (maybeCombined)
                    {
                        return new MapType(((MapType)it).KeyType, SequenceType.MakeSequenceType(((MapType)it).ValueType.PrimaryType, StaticProperty.ALLOWS_ZERO_OR_MORE));
                    }
                    else
                    {
                        return it;
                    }
                }
                else
                {
                    return base.GetResultItemType(args);
                }
            }

            public override Elaborator GetElaborator()
            {
                return new MapMergeElaborator();
            }

            // The dominant merge shape is `map:merge(for $x in SEQ return map:entry(K, V))` with the
            // default keep-existing policy: pump (K, V) pairs straight into the owned accumulator,
            // skipping the per-pair SingleEntryMap allocation and its per-map pair extraction. Any
            // other shape or policy falls back to the generic function-call path.
            private sealed class MapMergeElaborator : ItemElaborator
            {
                public override IItemEvaluator ElaborateForItem()
                {
                    SystemFunctionCall call = (SystemFunctionCall)GetExpression();
                    MapMerge fn = call.TargetFunction as MapMerge;
                    if (fn != null
                        && call.GetArity() == 1
                        && (fn.duplicates == "use-first" || fn.duplicates == "unspecified" || fn.duplicates == "use-any")
                        && fn.onDuplicates == null
                        && !(fn.treatAsFinal && fn.allStringKeys)
                        && call.GetArg(0) is ForExpression forex
                        && forex.GetAction() is SystemFunctionCall entryCall
                        && entryCall.TargetFunction is MapEntry
                        && entryCall.GetArity() == 2
                        && entryCall.GetArg(0).GetCardinality() == StaticProperty.EXACTLY_ONE
                        && entryCall.GetArg(1).GetCardinality() == StaticProperty.EXACTLY_ONE
                        && !ErrorExpression.IsContainedIn(entryCall.GetArg(0))
                        && !ErrorExpression.IsContainedIn(entryCall.GetArg(1)))
                    {
                        IPullEvaluator baseEval = forex.Sequence.MakeElaborator().ElaborateForPull();
                        IItemEvaluator keyEval = entryCall.GetArg(0).MakeElaborator().ElaborateForItem();
                        IItemEvaluator valEval = entryCall.GetArg(1).MakeElaborator().ElaborateForItem();
                        int slot = forex.LocalSlotNumber;
                        return (context) =>
                        {
                            try
                            {
                                ISequenceIterator it = baseEval.Iterate(context);
                                IItem first = it.Next();
                                if (first == null)
                                {
                                    return new HashTrieMap();
                                }

                                context.SetLocalVariable(slot, first);
                                AtomicValue k = (AtomicValue)keyEval.Eval(context);
                                IGroundedValue v = (IGroundedValue)valEval.Eval(context);
                                IItem cur = it.Next();
                                if (cur == null)
                                {
                                    // Classic parity: a single input map is returned as-is.
                                    return new SingleEntryMap(k, v);
                                }

                                HashTrieMap.MergeBuilder acc = new HashTrieMap.MergeBuilder();
                                acc.PutFirst(k, v);
                                do
                                {
                                    context.SetLocalVariable(slot, cur);
                                    k = (AtomicValue)keyEval.Eval(context);
                                    v = (IGroundedValue)valEval.Eval(context);
                                    acc.PutFirst(k, v);
                                }
                                while ((cur = it.Next()) != null);

                                return acc.ToMap();
                            }
                            catch (UncheckedXPathException e)
                            {
                                throw e.GetXPathException().MaybeWithLocation(call.GetLocation()).MaybeWithContext(context);
                            }
                            catch (XPathException err)
                            {
                                throw err.MaybeWithLocation(call.GetLocation()).MaybeWithContext(context);
                            }
                        };
                    }

                    SystemFunctionCall.SystemFunctionCallElaborator generic = new SystemFunctionCall.SystemFunctionCallElaborator();
                    generic.SetExpression(call);
                    return generic.ElaborateForItem();
                }
            }

            //
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                try
                {
                    string duplicates = this.duplicates;
                    string duplicatesErrorCode = this.duplicatesErrorCode;
                    bool allStringKeys = this.allStringKeys;
                    bool treatAsFinal = this.treatAsFinal;
                    IFunctionItem onDuplicates = this.onDuplicates;
                    if (arguments.Length > 1)
                    {
                        MapItem options = (MapItem)arguments[1].Head();
                        Dictionary<string, IGroundedValue> values = Details.optionDetails.ProcessSuppliedOptions(options, context);
                        duplicates = ((StringValue)values.GetOrDefault("duplicates")).GetStringValue();
                        duplicatesErrorCode = ((StringValue)values.GetOrDefault(errorCodeKey)).GetStringValue();
                        treatAsFinal = ((BooleanValue)values.GetOrDefault(finalKey)).GetBooleanValue();
                        allStringKeys = (((StringValue)values.GetOrDefault(keyTypeKey)).GetStringValue()) == "string";
                        onDuplicates = values.TryGetValue(onDuplicatesKey, out var __od2) ? (IFunctionItem)__od2 : null;
                        if (onDuplicates != null)
                        {
                            duplicates = "use-callback";
                        }
                    }

                    if (treatAsFinal && allStringKeys)
                    {

                        // Optimize for a map with string-valued keys that's unlikely to be modified
                        ISequenceIterator iter = arguments[0].Iterate();
                        DictionaryMap baseMap = new DictionaryMap();
                        MapItem next;
                        switch (duplicates)
                        {
                            case "unspecified":
                            case "use-any":
                            case "use-last":
                                while ((next = (MapItem)iter.Next()) != null)
                                {
                                    foreach (KeyValuePair pair in next.KeyValuePairs())
                                    {
                                        if (!(pair.key is StringValue))
                                        {
                                            throw new XPathException("The keys in this map must all be strings (found " + pair.key.GetItemType() + ")");
                                        }

                                        baseMap.InitialPut(pair.key.GetStringValue(), pair.value);
                                    }
                                }

                                return baseMap;
                            default:
                                while ((next = (MapItem)iter.Next()) != null)
                                {
                                    foreach (KeyValuePair pair in next.KeyValuePairs())
                                    {
                                        if (!(pair.key is StringValue))
                                        {
                                            throw new XPathException("The keys in this map must all be strings (found " + pair.key.GetItemType() + ")");
                                        }

                                        ISequence existing = baseMap[pair.key];
                                        if (existing != null)
                                        {
                                            switch (duplicates)
                                            {
                                                case "use-first":

                                                    // no action
                                                    break;
                                                case "combine":
                                                    InsertBefore.InsertIterator combinedIter = new InsertBefore.InsertIterator(pair.value.Iterate(), existing.Iterate(), 1);
                                                    IGroundedValue combinedValue = SequenceTool.ToGroundedValue(combinedIter);
                                                    baseMap.InitialPut(pair.key.GetStringValue(), combinedValue);
                                                    break;
                                                case "use-callback":
                                                    ISequence[] args = new ISequence[]
                                                    {
                                                        existing,
                                                        pair.value
                                                    };
                                                    ISequence combined = onDuplicates.Call(context, args);
                                                    baseMap.InitialPut(pair.key.GetStringValue(), combined.Materialize());
                                                    break;
                                                default:
                                                    throw new XPathException("Duplicate key in constructed map: " + Err.Wrap(pair.key.GetStringValue()), duplicatesErrorCode);
                                            }
                                        }
                                        else
                                        {
                                            baseMap.InitialPut(pair.key.GetStringValue(), pair.value);
                                        }
                                    }
                                }

                                return baseMap;
                        }
                    }
                    else
                    {
                        ISequenceIterator iter = arguments[0].Iterate();
                        return MergeMaps(iter, context, duplicates, duplicatesErrorCode, onDuplicates);
                    }
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }

            //
            public static MapItem MergeMaps(ISequenceIterator iter, IXPathContext context, string duplicates, string duplicatesErrorCode, IFunctionItem onDuplicates)
            {
                MapItem baseMap = (MapItem)iter.Next();
                if (baseMap == null)
                {
                    return new HashTrieMap();
                }

                MapItem second = (MapItem)iter.Next();
                if (second == null)
                {
                    return baseMap;
                }

                // Owned-accumulator fast path for the dominant `map:merge($seq ! map:entry(k, v))`
                // stream: while every input map has at most one entry the classic loop below can
                // never take the bug-4865 inversion (next.Count > accumulated count is impossible),
                // so left-to-right accumulation is behaviour-identical — and the accumulator trie,
                // which nothing can observe until it is returned, is built with in-place child-slot
                // updates instead of a path copy per entry. A larger map in the stream materializes
                // the accumulator and continues on the classic path.
                if (baseMap.Count <= 1)
                {
                    HashTrieMap.MergeBuilder acc = new HashTrieMap.MergeBuilder();
                    foreach (KeyValuePair seed in baseMap.KeyValuePairs())
                    {
                        // keys within one map are unique, so the seed is plain inserts
                        acc.Put(seed.key.AsMapKey(), seed.key, seed.value, true);
                    }

                    MapItem pending = second;
                    while (pending != null)
                    {
                        // The dominant stream is map:entry results: read the single pair off the
                        // fields directly instead of allocating a KeyValuePairs list per map.
                        if (pending is SingleEntryMap sem)
                        {
                            MergeOnePair(acc, sem.key, sem.value, context, duplicates, duplicatesErrorCode, onDuplicates);
                        }
                        else
                        {
                            if (pending.Count > 1)
                            {
                                return MergeMapsClassic(acc.ToMap(), pending, iter, context, duplicates, duplicatesErrorCode, onDuplicates);
                            }

                            foreach (KeyValuePair pair in pending.KeyValuePairs())
                            {
                                MergeOnePair(acc, pair.key, pair.value, context, duplicates, duplicatesErrorCode, onDuplicates);
                            }
                        }

                        pending = (MapItem)iter.Next();
                    }

                    return acc.ToMap();
                }

                return MergeMapsClassic(baseMap, second, iter, context, duplicates, duplicatesErrorCode, onDuplicates);
            }

            // One (key, value) pair of a ≤1-entry input map, applied to the owned accumulator under
            // the per-pair duplicates policy. Extracted verbatim from the fast-path loop above.
            private static void MergeOnePair(HashTrieMap.MergeBuilder acc, AtomicValue key, IGroundedValue value, IXPathContext context, string duplicates, string duplicatesErrorCode, IFunctionItem onDuplicates)
            {
                switch (duplicates)
                {
                    case "use-first":
                    case "unspecified":
                    case "use-any":
                        // Keep-existing policies need no existing VALUE — put-if-absent folds the
                        // probe and the insert into one trie descent.
                        acc.PutFirst(key, value);
                        return;
                }

                ISequence existing = acc.GetExisting(key, out IAtomicMatchKey amk);
                if (existing != null)
                {
                    switch (duplicates)
                    {
                        case "use-first":
                        case "unspecified":
                        case "use-any":
                            break;
                        case "use-last":
                            acc.Put(amk, key, value, false);
                            break;
                        case "combine":
                            {
                                InsertBefore.InsertIterator combinedIter = new InsertBefore.InsertIterator(value.Iterate(), existing.Iterate(), 1);
                                try
                                {
                                    acc.Put(amk, key, SequenceTool.ToGroundedValue(combinedIter), false);
                                }
                                catch (UncheckedXPathException e)
                                {
                                    throw e.GetXPathException();
                                }

                                break;
                            }

                        case "combine-reverse":
                            {
                                InsertBefore.InsertIterator combinedIter = new InsertBefore.InsertIterator(existing.Iterate(), value.Iterate(), 1);
                                try
                                {
                                    acc.Put(amk, key, SequenceTool.ToGroundedValue(combinedIter), false);
                                }
                                catch (UncheckedXPathException e)
                                {
                                    throw e.GetXPathException();
                                }

                                break;
                            }

                        case "use-callback":
                            {
                                ISequence[] cbArgs = onDuplicates.GetArity() == 2 ? new ISequence[]
                                {
                                    existing,
                                    value
                                }

                                : new ISequence[]
                                {
                                    existing,
                                    value,
                                    key
                                };
                                ISequence combined = onDuplicates.Call(context, cbArgs);
                                acc.Put(amk, key, combined.Materialize(), false);
                                break;
                            }

                        default:
                            throw new XPathException("Duplicate key in constructed map: " + Err.Wrap(key.GetStringValue()), duplicatesErrorCode);
                    }
                }
                else
                {
                    acc.Put(amk, key, value, true);
                }
            }

            private static MapItem MergeMapsClassic(MapItem baseMap, MapItem first, ISequenceIterator iter, IXPathContext context, string duplicates, string duplicatesErrorCode, IFunctionItem onDuplicates)
            {
                {
                    MapItem next = first;
                    for (; next != null; next = (MapItem)iter.Next())
                    {

                        // Merge the next map and the base map. Merge the smaller of the two
                        // maps into the larger. The complication is that this affects duplicates handling.
                        // See bug #4865
                        bool inverse = next.Count > baseMap.Count;
                        MapItem larger = inverse ? next : baseMap;
                        MapItem smaller = inverse ? baseMap : next;
                        string dup = inverse ? InvertDuplicates(duplicates) : duplicates;
                        foreach (KeyValuePair pair in smaller.KeyValuePairs())
                        {
                            ISequence existing = larger[pair.key];
                            if (existing != null)
                            {
                                switch (dup)
                                {
                                    case "use-first":
                                    case "unspecified":
                                    case "use-any":

                                        // no action
                                        break;
                                    case "use-last":
                                        larger = larger.AddEntry(pair.key, pair.value);
                                        break;
                                    case "combine":
                                        {
                                            InsertBefore.InsertIterator combinedIter = new InsertBefore.InsertIterator(pair.value.Iterate(), existing.Iterate(), 1);
                                            try
                                            {
                                                IGroundedValue combinedValue = SequenceTool.ToGroundedValue(combinedIter);
                                                larger = larger.AddEntry(pair.key, combinedValue);
                                            }
                                            catch (UncheckedXPathException e)
                                            {
                                                throw e.GetXPathException();
                                            }

                                            break;
                                        }

                                    case "combine-reverse":
                                        {
                                            InsertBefore.InsertIterator combinedIter = new InsertBefore.InsertIterator(existing.Iterate(), pair.value.Iterate(), 1);
                                            try
                                            {
                                                IGroundedValue combinedValue = SequenceTool.ToGroundedValue(combinedIter);
                                                larger = larger.AddEntry(pair.key, combinedValue);
                                            }
                                            catch (UncheckedXPathException e)
                                            {
                                                throw e.GetXPathException();
                                            }

                                            break;
                                        }

                                    case "use-callback":
                                        ISequence[] args;
                                        if (inverse)
                                        {
                                            args = onDuplicates.GetArity() == 2 ? new ISequence[]
                                            {
                                                pair.value,
                                                existing
                                            }

                                            : new ISequence[]
                                            {
                                                pair.value,
                                                existing,
                                                pair.key
                                            };
                                        }
                                        else
                                        {
                                            args = onDuplicates.GetArity() == 2 ? new ISequence[]
                                            {
                                                existing,
                                                pair.value
                                            }

                                            : new ISequence[]
                                            {
                                                existing,
                                                pair.value,
                                                pair.key
                                            };
                                        }

                                        ISequence combined = onDuplicates.Call(context, args);
                                        larger = larger.AddEntry(pair.key, combined.Materialize());
                                        break;
                                    default:
                                        throw new XPathException("Duplicate key in constructed map: " + Err.Wrap(pair.key.GetStringValue()), duplicatesErrorCode);
                                }
                            }
                            else
                            {
                                larger = larger.AddEntry(pair.key, pair.value);
                            }
                        }

                        baseMap = larger;
                    }

                    return baseMap;
                }
            }

            //
            private static string InvertDuplicates(string duplicates)
            {
                switch (duplicates)
                {
                    case "use-first":
                    case "unspecified":
                    case "use-any":
                        return "use-last";
                    case "use-last":
                        return "use-first";
                    case "combine":
                        return "combine-reverse";
                    case "combine-reverse":
                        return "combine";
                    default:
                        return duplicates;
                }
            }

            public override void ExportAdditionalArguments(SystemFunctionCall call, ExpressionPresenter @out)
            {
                if (call.GetArity() == 1)
                {
                    HashTrieMap options = new HashTrieMap();
                    options.InitialPut(StringValue.Bmp("duplicates"), new StringValue(duplicates));
                    options.InitialPut(StringValue.Bmp("duplicates-error-code"), new StringValue(duplicatesErrorCode));
                    Literal.ExportValue(options, @out);
                }
            }
        }

        //
        /// <summary>
        /// Implementation of the function map:of-pairs() =&gt; IMap
        /// </summary>
        internal sealed class MapOfPairs : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                IFunctionItem onDuplicates = null;
                if (arguments.Length > 1)
                {
                    onDuplicates = (IFunctionItem)arguments[1].Head();
                }

                StringValue keyKey = new StringValue("key");
                StringValue valueKey = new StringValue("value");
                MapItem result = new HashTrieMap();
                ISequenceIterator iter = arguments[0].Iterate();
                for (IItem item; (item = iter.Next()) != null;)
                {
                    AtomicValue key = (AtomicValue)((MapItem)item)[keyKey];
                    IGroundedValue suppliedValue = ((MapItem)item)[valueKey];
                    IGroundedValue existingValue = result[key];
                    if (existingValue != null)
                    {
                        if (onDuplicates == null)
                        {
                            IGroundedValue newValue = existingValue.Concatenate(suppliedValue);
                            result = result.AddEntry(key, newValue);
                        }
                        else
                        {
                            IGroundedValue newValue = onDuplicates.Call(context, new ISequence[] { existingValue, suppliedValue }).Materialize();
                            result = result.AddEntry(key, newValue);
                        }
                    }
                    else
                    {
                        result = result.AddEntry(key, suppliedValue);
                    }
                }

                return result;
            }
        }

        //
        /// <summary>
        /// Implementation of the extension function map:put() =&gt; IMap
        /// </summary>
        internal sealed class MapPut : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                MapItem baseMap = (MapItem)arguments[0].Head();
                if (!(baseMap is HashTrieMap))
                {
                    baseMap = HashTrieMap.Copy(baseMap);
                }

                AtomicValue key = (AtomicValue)arguments[1].Head();
                IGroundedValue value = arguments[2].Materialize();
                return baseMap.AddEntry(key, value);
            }
        }

        //
        /// <summary>
        /// Implementation of the XPath 3.1 function map:remove(IMap, key) =&gt; value
        /// </summary>
        internal sealed class MapRemove : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                MapItem map = (MapItem)arguments[0].Head();
                ISequenceIterator iter = arguments[1].Iterate();
                AtomicValue key;
                while ((key = (AtomicValue)iter.Next()) != null)
                {
                    map = map.Remove(key);
                }

                return map;
            }
        }

        //
        /// <summary>
        /// Implementation of the extension function map:size(map) =&gt; integer
        /// </summary>
        internal sealed class MapSize : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                MapItem map = (MapItem)arguments[0].Head();
                return new Int64Value(map.Count);
            }
        }
    }
}
