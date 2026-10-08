////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Threading;

// Stub for net.sf.saxon.value.SequenceType (transpiler collision with s9api.SequenceType lost the Value one).
// All 56 static field constants from Saxon's value/SequenceType.java + factory methods.
// Placeholders for now -- functional behaviour to be wired in Phase 4.10+.
namespace OutSmart.DAXon.Values
{
    public class SequenceType
    {
        // ANY_SEQUENCE / SINGLE_ITEM / OPTIONAL_ITEM were hollow (new SequenceType() -> null _primaryType), so
        // req.GetPrimaryType() returned null and TypeChecker.StrictTypeCheck's th.Relationship NRE'd when a for/let
        // range-variable requiredType is one of these (ForExpression.TypeCheck -> StrictTypeCheck; Invoice's
        // serialize(array{...}, map{...})). Same reflective-lazy fix as NODE_SEQUENCE/ATOMIC_SEQUENCE below:
        // primary type item() (AnyItemType, resolved from the loaded Saxon assembly on first use -- no
        // static-init-ordering hazard), with the matching cardinality.
        private static SequenceType _anySequence;
        private static SequenceType _singleItem;
        private static SequenceType _optionalItem;
        private static SequenceType _singleAtomic;
        private static SequenceType _optionalAtomic;
        // ATOMIC_SEQUENCE was hollow (new SequenceType() -> null _primaryType); GeneralComparison.TypeCheck
        // static-type-checks each operand against xs:anyAtomicType* via TypeChecker.StaticTypeCheck, where
        // req.GetPrimaryType() returned null -> NullReferenceException (FinDim xsl:if "DIMENSIONVALUE != ''",
        // Trans likewise). Resolve BuiltInAtomicType.ANY_ATOMIC reflectively (lazy; Saxon assembly is loaded by
        // first use, no static-init-ordering hazard); cardinality ALLOWS_ZERO_OR_MORE (57344).
        private static SequenceType _atomicSequence;
        private static SequenceType _singleString;
        private static SequenceType _singleUntypedAtomic;
        private static SequenceType _optionalString;
        private static SequenceType _singleBoolean;
        private static SequenceType _optionalBoolean;
        private static SequenceType _singleInteger;
        private static SequenceType _singleDecimal;
        private static SequenceType _optionalInteger;
        private static SequenceType _integerSequence;
        private static SequenceType _singleShort;
        private static SequenceType _optionalShort;
        private static SequenceType _singleByte;
        private static SequenceType _optionalByte;
        private static SequenceType _singleDouble;
        private static SequenceType _optionalDouble;
        private static SequenceType _singleFloat;
        private static SequenceType _optionalFloat;
        private static SequenceType _optionalDecimal;
        // XR9: this whole block was hollow (new SequenceType() -> null _primaryType) — the same bomb
        // class as ANY_SEQUENCE above. SINGLE_NUMERIC alone killed every `.[predicate]` match pattern
        // (PatternParser builds `$P instance of xs:numeric` -> InstanceOfExpression ctor throw,
        // match-240*). Lazy-reflective like the rest; NUMERIC resolves via NumericType.GetInstance().
        private static SequenceType _optAnyUri;
        private static SequenceType _optDate;
        private static SequenceType _optTime;
        private static SequenceType _optGYear;
        private static SequenceType _optGYearMonth;
        private static SequenceType _optGMonth;
        private static SequenceType _optGMonthDay;
        private static SequenceType _optGDay;
        private static SequenceType _optDateTime;
        private static SequenceType _optDuration;
        private static SequenceType _optYMDuration;
        private static SequenceType _optDTDuration;
        private static SequenceType _singleQName;
        private static SequenceType _optQName;
        private static SequenceType _optNotation;
        private static SequenceType _optBase64;
        private static SequenceType _optHexBinary;
        private static SequenceType _optNumeric;
        private static SequenceType _singleNumeric;
        // batch6d: OPTIONAL_NODE/SINGLE_NODE were hollow (null _primaryType) -> the same TypeChecker
        // null-guard NRE as NODE_SEQUENCE, hit by xsl:number's select check against node(). Lazy-reflective.
        private static SequenceType _optionalNode;
        private static SequenceType _singleNode;
        public static readonly SequenceType OPTIONAL_DOCUMENT_NODE = new SequenceType();
        // NODE_SEQUENCE was hollow (`new SequenceType()` -> null _primaryType), so TypeChecker.StaticTypeCheck's
        // `req.GetPrimaryType()` returned null and the transpile-added null-guard threw NullReferenceException on
        // the xsl:for-each select's static type-check against node()* (SlashExpression.TypeCheck -> StaticTypeCheck).
        // OutSmart.DAXon.Internal (the lower layer) cannot reference Saxon's AnyNodeTest at compile time; resolve it
        // reflectively from the already-loaded Saxon assembly on FIRST ACCESS (lazy property runs on use, so no
        // static-init-ordering hazard). node() with cardinality zero-or-more.
        private static SequenceType _nodeSequence;
        private static SequenceType _stringSequence;
        // SINGLE_FUNCTION was hollow (`new SequenceType()` -> null _primaryType) -> req.GetPrimaryType() null ->
        // the transpile-added null-guard threw NullReferenceException in TypeChecker.StaticTypeCheck on EVERY
        // dynamic function call `$f(args)` / `map{...}('k')` (DynamicFunctionCall.TypeCheck checks the call
        // target against function(*)). Same lazy-reflective fix as NODE_SEQUENCE: function(*)
        // (AnyFunctionType.GetInstance()) with cardinality EXACTLY_ONE.
        private static SequenceType _singleFunction;
        public static readonly SequenceType OPTIONAL_FUNCTION_ITEM = new SequenceType();
        public static readonly SequenceType FUNCTION_ITEM_SEQUENCE = new SequenceType();
        public static readonly SequenceType EMPTY_SEQUENCE = new SequenceType();
        public static readonly SequenceType NON_EMPTY_SEQUENCE = new SequenceType();
        public static readonly SequenceType VOID = new SequenceType();
        // Functional core (2026-06-03): store primaryType + cardinality. Previously hollow -> every internal
        // SequenceType (incl. every function-argument type built by BuiltInFunctionSet.Arg) had a null primary
        // type, so TypeChecker.StaticTypeCheck's `req.GetPrimaryType()` returned null and the transpile-added
        // null guard threw NullReferenceException on EVERY function-call static type-check. OutSmart.DAXon.Types.ItemType
        // is the compat-side interface that Saxon's real item types (BuiltInAtomicType etc.) implement, so the
        // `as` cast preserves the real instance. StaticProperty cardinality is Saxon-side (not referenceable here)
        // so the int cardinality is stored verbatim from the caller.
        private readonly OutSmart.DAXon.Types.ItemType _primaryType;
        private readonly int _cardinality;
        public static SequenceType ANY_SEQUENCE => MakeItemSeqType(ref _anySequence, "OutSmart.DAXon.Types.AnyItemType", 57344 /* ALLOWS_ZERO_OR_MORE */);
        public static SequenceType SINGLE_ITEM => MakeItemSeqType(ref _singleItem, "OutSmart.DAXon.Types.AnyItemType", 16384 /* EXACTLY_ONE */);
        public static SequenceType OPTIONAL_ITEM => MakeItemSeqType(ref _optionalItem, "OutSmart.DAXon.Types.AnyItemType", 24576 /* ALLOWS_ZERO_OR_ONE */);
        public static SequenceType SINGLE_ATOMIC => MakeAtomicSeqType(ref _singleAtomic, "ANY_ATOMIC", 16384);
        public static SequenceType OPTIONAL_ATOMIC => MakeAtomicSeqType(ref _optionalAtomic, "ANY_ATOMIC", 24576);
        public static SequenceType ATOMIC_SEQUENCE => MakeAtomicSeqType(ref _atomicSequence, "ANY_ATOMIC", 57344 /* ALLOWS_ZERO_OR_MORE */);
        public static SequenceType SINGLE_STRING => MakeAtomicSeqType(ref _singleString, "STRING", 16384);
        public static SequenceType SINGLE_UNTYPED_ATOMIC => MakeAtomicSeqType(ref _singleUntypedAtomic, "UNTYPED_ATOMIC", 16384);
        public static SequenceType OPTIONAL_STRING => MakeAtomicSeqType(ref _optionalString, "STRING", 24576);
        public static SequenceType SINGLE_BOOLEAN => MakeAtomicSeqType(ref _singleBoolean, "BOOLEAN", 16384);
        public static SequenceType OPTIONAL_BOOLEAN => MakeAtomicSeqType(ref _optionalBoolean, "BOOLEAN", 24576);
        public static SequenceType SINGLE_INTEGER => MakeAtomicSeqType(ref _singleInteger, "INTEGER", 16384);
        public static SequenceType SINGLE_DECIMAL => MakeAtomicSeqType(ref _singleDecimal, "DECIMAL", 16384);
        public static SequenceType OPTIONAL_INTEGER => MakeAtomicSeqType(ref _optionalInteger, "INTEGER", 24576);
        public static SequenceType INTEGER_SEQUENCE => MakeAtomicSeqType(ref _integerSequence, "INTEGER", 57344);
        public static SequenceType SINGLE_SHORT => MakeAtomicSeqType(ref _singleShort, "SHORT", 16384);
        public static SequenceType OPTIONAL_SHORT => MakeAtomicSeqType(ref _optionalShort, "SHORT", 24576);
        public static SequenceType SINGLE_BYTE => MakeAtomicSeqType(ref _singleByte, "BYTE", 16384);
        public static SequenceType OPTIONAL_BYTE => MakeAtomicSeqType(ref _optionalByte, "BYTE", 24576);
        public static SequenceType SINGLE_DOUBLE => MakeAtomicSeqType(ref _singleDouble, "DOUBLE", 16384);
        public static SequenceType OPTIONAL_DOUBLE => MakeAtomicSeqType(ref _optionalDouble, "DOUBLE", 24576);
        public static SequenceType SINGLE_FLOAT => MakeAtomicSeqType(ref _singleFloat, "FLOAT", 16384);
        public static SequenceType OPTIONAL_FLOAT => MakeAtomicSeqType(ref _optionalFloat, "FLOAT", 24576);
        public static SequenceType OPTIONAL_DECIMAL => MakeAtomicSeqType(ref _optionalDecimal, "DECIMAL", 24576);
        public static SequenceType OPTIONAL_ANY_URI => MakeAtomicSeqType(ref _optAnyUri, "ANY_URI", 24576);
        public static SequenceType OPTIONAL_DATE => MakeAtomicSeqType(ref _optDate, "DATE", 24576);
        public static SequenceType OPTIONAL_TIME => MakeAtomicSeqType(ref _optTime, "TIME", 24576);
        public static SequenceType OPTIONAL_G_YEAR => MakeAtomicSeqType(ref _optGYear, "G_YEAR", 24576);
        public static SequenceType OPTIONAL_G_YEAR_MONTH => MakeAtomicSeqType(ref _optGYearMonth, "G_YEAR_MONTH", 24576);
        public static SequenceType OPTIONAL_G_MONTH => MakeAtomicSeqType(ref _optGMonth, "G_MONTH", 24576);
        public static SequenceType OPTIONAL_G_MONTH_DAY => MakeAtomicSeqType(ref _optGMonthDay, "G_MONTH_DAY", 24576);
        public static SequenceType OPTIONAL_G_DAY => MakeAtomicSeqType(ref _optGDay, "G_DAY", 24576);
        public static SequenceType OPTIONAL_DATE_TIME => MakeAtomicSeqType(ref _optDateTime, "DATE_TIME", 24576);
        public static SequenceType OPTIONAL_DURATION => MakeAtomicSeqType(ref _optDuration, "DURATION", 24576);
        public static SequenceType OPTIONAL_YEAR_MONTH_DURATION => MakeAtomicSeqType(ref _optYMDuration, "YEAR_MONTH_DURATION", 24576);
        public static SequenceType OPTIONAL_DAY_TIME_DURATION => MakeAtomicSeqType(ref _optDTDuration, "DAY_TIME_DURATION", 24576);
        public static SequenceType SINGLE_QNAME => MakeAtomicSeqType(ref _singleQName, "QNAME", 16384);
        public static SequenceType OPTIONAL_QNAME => MakeAtomicSeqType(ref _optQName, "QNAME", 24576);
        public static SequenceType OPTIONAL_NOTATION => MakeAtomicSeqType(ref _optNotation, "NOTATION", 24576);
        public static SequenceType OPTIONAL_BASE64_BINARY => MakeAtomicSeqType(ref _optBase64, "BASE64_BINARY", 24576);
        public static SequenceType OPTIONAL_HEX_BINARY => MakeAtomicSeqType(ref _optHexBinary, "HEX_BINARY", 24576);
        public static SequenceType OPTIONAL_NUMERIC => MakeItemSeqType(ref _optNumeric, "OutSmart.DAXon.Types.NumericType", 24576 /* ALLOWS_ZERO_OR_ONE */);
        public static SequenceType SINGLE_NUMERIC => MakeItemSeqType(ref _singleNumeric, "OutSmart.DAXon.Types.NumericType", 16384 /* EXACTLY_ONE */);
        public static SequenceType OPTIONAL_NODE => MakeItemSeqType(ref _optionalNode, "OutSmart.DAXon.Patterns.AnyNodeTest", 24576 /* ALLOWS_ZERO_OR_ONE */);
        public static SequenceType SINGLE_NODE => MakeItemSeqType(ref _singleNode, "OutSmart.DAXon.Patterns.AnyNodeTest", 16384 /* EXACTLY_ONE */);
        public static SequenceType NODE_SEQUENCE => MakeItemSeqType(ref _nodeSequence, "OutSmart.DAXon.Patterns.AnyNodeTest", 57344 /* ALLOWS_ZERO_OR_MORE */);
        public static SequenceType STRING_SEQUENCE => MakeAtomicSeqType(ref _stringSequence, "STRING", 57344);
        public static SequenceType SINGLE_FUNCTION => MakeItemSeqType(ref _singleFunction, "OutSmart.DAXon.Types.AnyFunctionType", 16384 /* EXACTLY_ONE */);

        public OutSmart.DAXon.Types.ItemType PrimaryType => _primaryType;
        public SequenceType() { }
        // upstream ctor: xs:error has no instances, so any zero-allowing cardinality collapses to EMPTY —
        // this is what makes "xs:error?" ≡ empty-sequence() (xs-error-007: the xs:error#1 constructor's
        // function type prints and matches as function(xs:anyAtomicType?) as empty-sequence())
        public SequenceType(object itemType, int cardinality)
        {
            _primaryType = itemType as OutSmart.DAXon.Types.ItemType;
            _cardinality = _primaryType is OutSmart.DAXon.Types.ErrorType && Cardinality.AllowsZero(cardinality)
                ? OutSmart.DAXon.Expressions.StaticProperty.EMPTY : cardinality;
        }
        public SequenceType(object itemType, object occurrenceIndicator) : this(itemType, occurrenceIndicator is int __c ? __c : 0) { }
        // Lazy reflective builder for the hollow atomic/typed SequenceType constants below. OutSmart.DAXon.Internal cannot
        // reference Saxon's BuiltInAtomicType at compile time; resolve from the loaded assembly on first use
        // (no static-init-ordering hazard). Fixes the same null-_primaryType NRE class as NODE_SEQUENCE/
        // ATOMIC_SEQUENCE, which bites pervasively in TypeChecker.Strict/StaticTypeCheck (ValueComparison,
        // arithmetic, conditions, function-arg checks, ...). Cardinality: SINGLE_=16384 (EXACTLY_ONE),
        // OPTIONAL_=24576 (ZERO_OR_ONE), *_SEQUENCE=57344 (ZERO_OR_MORE).
        // Publish-once, lock-free. These stay lazy because the primary types are resolved
        // reflectively (see below), but the plain `if (cache == null) cache = …` they used to
        // carry let concurrent first-touch publish one instance per thread, and 8 of these
        // singletons are compared by REFERENCE (`!= SequenceType.ANY_SEQUENCE` guards "was a
        // type declared" in LocalParam, NamedTemplate, UserFunction, WithParamPort,
        // LetExpression, UserFunctionCall, XPathDynamicContext, XSLTemplate). The loser of the
        // CAS is discarded; every caller ends up with the same object.
        private static SequenceType Publish(ref SequenceType cache, SequenceType candidate)
        {
            return Interlocked.CompareExchange(ref cache, candidate, null) ?? candidate;
        }

        private static SequenceType MakeAtomicSeqType(ref SequenceType cache, string atomicField, int cardinality)
        {
            SequenceType known = Volatile.Read(ref cache);
            return known ?? Publish(ref cache, new SequenceType(ResolveDAXonStaticField("OutSmart.DAXon.Types.BuiltInAtomicType", atomicField), cardinality));
        }

        private static SequenceType MakeItemSeqType(ref SequenceType cache, string itemTypeName, int cardinality)
        {
            SequenceType known = Volatile.Read(ref cache);
            return known ?? Publish(ref cache, new SequenceType(ResolveDAXonItemType(itemTypeName), cardinality));
        }

        public static SequenceType MakeSequenceType(OutSmart.DAXon.Types.ItemType primaryType, int cardinality) => new SequenceType((object)primaryType, cardinality);
        public static SequenceType One(OutSmart.DAXon.Types.ItemType itemType) => new SequenceType((object)itemType, 16384 /* StaticProperty.EXACTLY_ONE (Saxon-side, not referenceable from compat) */);

        // Resolve a Saxon item-type singleton (e.g. AnyNodeTest.GetInstance()) reflectively. OutSmart.DAXon.Internal cannot
        // reference OutSmart.DAXon at compile time (one-way dependency), but by the time these named
        // SequenceType constants are first read (deep in stylesheet compile) the Saxon assembly is loaded.
        private static object ResolveDAXonItemType(string fullTypeName)
        {
            try
            {
                var t = Type.GetType(fullTypeName + ", OutSmart.DAXon");
                if (t == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        t = asm.GetType(fullTypeName);
                        if (t != null)
                            break;
                    }
                }
                return t == null ? null : t.GetMethod("GetInstance", Type.EmptyTypes)?.Invoke(null, null);
            }
            catch { return null; }
        }

        // Resolve a Saxon item-type held in a public static FIELD (e.g. BuiltInAtomicType.ANY_ATOMIC) reflectively
        // -- the GetInstance() variant above does not cover field-held singletons.
        private static object ResolveDAXonStaticField(string fullTypeName, string fieldName)
        {
            try
            {
                var t = Type.GetType(fullTypeName + ", OutSmart.DAXon");
                if (t == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        t = asm.GetType(fullTypeName);
                        if (t != null)
                            break;
                    }
                }
                return t?.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
            }
            catch { return null; }
        }
        public int GetCardinality() => _cardinality;

        // Upstream overrides equals/hashCode on (primaryType, cardinality); without them C# used
        // REFERENCE equality — two templates declaring the same as="..." in different packages
        // compared unequal, so xsl:accept/xsl:override raised bogus XTSE3070 "different required
        // type" (accept-04x, override-t-00x).
        public override bool Equals(object obj)
        {
            return obj is SequenceType other
                && object.Equals(_primaryType, other._primaryType)
                && _cardinality == other._cardinality;
        }

        public override int GetHashCode()
        {
            return (_primaryType?.GetHashCode() ?? 0) ^ _cardinality;
        }
        // Ported from upstream value/SequenceType.matches(GroundedValue, TypeHierarchy): iterate the value,
        // check every item against the primary type, then verify cardinality. Previously a hollow `=> true`,
        // which made TypeHierarchy.ApplyFunctionConversionRules' fast-path accept ANY value for a typed
        // function parameter (e.g. passing 'abc' to an xs:integer parameter) -> the coercion/type-error
        // check was skipped and a raw StringValue reached the body -> InvalidCastException instead of XPTY0004.
        // Only concrete primary types whose real Matches is reachable are evaluated; for any other kind the
        // items pass here (the downstream ItemTypeCheckingFunction tests them) and only their number is checked.
        public bool Matches(object value, object th)
        {
            var groundedValue = value as OutSmart.DAXon.Model.IGroundedValue;
            var typeHierarchy = th as OutSmart.DAXon.Types.TypeHierarchy;
            if (groundedValue == null || _primaryType == null)
            {
                return true;
            }

            // Dispatch through the concrete ItemType.Matches for atomic/union (IPlainType), node (NodeTest),
            // and ALL function item types (specific/any function, MAP, ARRAY) — upstream has no carve-outs.
            // FUNCTION types were previously permissive because the port's signature Matches over-rejected;
            // that is fixed (bare `floor#1 instance of function(xs:numeric) as xs:numeric` is correct), and the
            // permissive `true` here skipped both the array member signature check (ArrayTest-064/084) and the
            // HOF coercion fast-path mismatch (a false just routes to ApplyFunctionCoercion, like Java).
            if (!(_primaryType is OutSmart.DAXon.Types.IPlainType
                  || _primaryType is OutSmart.DAXon.Patterns.NodeTest
                  || _primaryType is OutSmart.DAXon.Types.IFunctionItemType))
            {
                // Every item is an item(): what is left is how many there are, which upstream checks too
                // (item()? took two items and item() none).
                return OutSmart.DAXon.Values.Cardinality.Allows(_cardinality, groundedValue.GetLength());
            }

            // Singleton fast path: one item satisfies every cardinality clause below, and the HOF
            // coercion checks (FusedArity2Caller, ApplyFunctionConversionRules) pass one item per
            // call — skip the AsIterable wrapper and its enumerator.
            if (groundedValue is OutSmart.DAXon.Model.IItem single)
            {
                return _primaryType.Matches(single, typeHierarchy);
            }

            int count = 0;
            foreach (OutSmart.DAXon.Model.IItem item in groundedValue.AsIterable())
            {
                count++;
                if (!_primaryType.Matches(item, typeHierarchy))
                {
                    return false;
                }
            }

            return !((count == 0 && !OutSmart.DAXon.Values.Cardinality.AllowsZero(_cardinality))
                     || (count > 1 && !OutSmart.DAXon.Values.Cardinality.AllowsMany(_cardinality)));
        }
        /// <summary>
        /// The XPath syntax, as upstream prints it in messages: "xs:string+", "array(xs:integer)?", "empty-sequence()".
        /// A computed type is as deep as its value (array(array(...))), so the depth is capped like a depiction's.
        /// </summary>
        public override string ToString()
        {
            if (_cardinality == OutSmart.DAXon.Expressions.StaticProperty.EMPTY)
            {
                return "empty-sequence()";
            }

            if (_primaryType == null)
            {
                return "*";
            }

            OutSmart.DAXon.Transformation.Err.EnterDepiction();
            try
            {
                return OutSmart.DAXon.Transformation.Err.DepictionTooDeep
                    ? "..."
                    : _primaryType.ToString() + Cardinality.GetOccurrenceIndicator(_cardinality);
            }
            finally
            {
                OutSmart.DAXon.Transformation.Err.LeaveDepiction();
            }
        }

        public string ToAlphaCode() => string.Empty;
        public string ToExportString() => string.Empty;
        public string ToString(object config) => string.Empty;
        public bool IsStreaming() => false;
        // Value.SequenceType methods used by RecordTest etc.
        public string ExplainMismatch(object value, object th) => null;
        public bool IsSameType(SequenceType other) => this == other;
        // Upstream isSameType(SequenceType, TypeHierarchy). Was `this == other` (reference equality) —
        // xsl:override compatibility checks (XSLFunction/XSLTemplate CheckCompatibility) compare
        // independently-parsed declarations, so every override raised a spurious XTSE3070.
        public bool IsSameType(SequenceType other, object th) =>
            _cardinality == other._cardinality
            && ((OutSmart.DAXon.Types.TypeHierarchy)th).Relationship(_primaryType, other._primaryType) == OutSmart.DAXon.Types.Affinity.SAME_TYPE;
    }
}
