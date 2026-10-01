////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Values;

// Stub OutSmart.DAXon.Types.Type class -- Type.cs is excluded but
// XPathParser/SequenceTool/ValueComparison call Types.GetItemType etc. as
// static methods. Add a public static facade in the OutSmart.DAXon.Types namespace.
namespace OutSmart.DAXon.Types
{
    using OutSmart.DAXon.Model;
    internal static class Type
    {
        public const int ITEM = 88;
        // NODE is the "any node kind" sentinel = 0 (per real Type.cs, compat JavaInternals, and upstream
        // Saxon Types.NODE). It was erroneously 9, colliding with DOCUMENT=9 and producing CS0152 duplicate
        // switch labels in NodeKindTest/NodeTest/UType (which case on both Types.DOCUMENT and Types.NODE).
        public const int NODE = 0;
        public const int ELEMENT = 1;
        public const int ATTRIBUTE = 2;
        public const int TEXT = 3;
        public const int COMMENT = 8;
        public const int DOCUMENT = 9;
        public const int PROCESSING_INSTRUCTION = 7;
        public const int NAMESPACE = 13;
        public const int FUNCTION = 11;
        public const int WHITESPACE_TEXT = 4;
        // MUST be 17: the TinyTree invariant is "kind & 0x0f == ELEMENT" (17 & 0xf == 1 == ELEMENT), relied on
        // by NamedChildIterator/NodeKindTest/etc to recognise a collapsed text-only element as an element.
        // It was erroneously 5 here (5 & 0xf == 5 != ELEMENT), so TinyBuilder wrote kind 5 for every text-only
        // element and child-axis iterators rejected them -> child::NAME of any text-only element selected nothing
        // (FinDim Bug A: DIMENSIONVALUE etc. invisible). Matches the real Type.cs / OutSmart.DAXon.Internal value.
        public const int TEXTUAL_ELEMENT = 17;
        public const int PARENT_POINTER = 100;
        public const int STOPPER = 101;
        // NODE_TYPE / ITEM_TYPE are ItemType-typed statics (Saxon Java: Types.NODE_TYPE = AnyNodeTest,
        // Types.ITEM_TYPE = AnyItemType — they are NOT the same value).
        // LAZY properties, not eager static readonly fields: a static-init cycle would leave them NULL at
        // runtime (same lazy pattern as the compat SequenceType constants).
        // NODE_TYPE MUST be AnyNodeTest (node()), not AnyItemType: it is the declared arg type of the
        // node-argument builtins (fn:root/local-name/namespace-uri/nilled/generate-id/id/idref/lang/base-uri/
        // document-uri/node-name/name, key/document node args). When it was AnyItemType, TypeChecker.StaticTypeCheck
        // treated `reqItemType is AnyItemType` as "no constraint" and never inserted the node check, so those
        // functions failed with a raw InvalidCastException instead of XPTY0004. It also disabled the FORG0006
        // effective-boolean-value check for maps/functions (TypeChecker.EbvError).
        private static ItemType _nodeType;
        private static ItemType _itemType;
        public static ItemType NODE_TYPE { get { if (_nodeType == null) { _nodeType = AnyNodeTest.GetInstance(); } return _nodeType; } }
        public static ItemType ITEM_TYPE { get { if (_itemType == null) { _itemType = AnyItemType.GetInstance(); } return _itemType; } }
        // net472 port: real OutSmart.DAXon.Types.Type is excluded; this facade was hollow (=> null), which
        // handed a null ItemType to SequenceTool/Block/ValueComparison for every map/array/node/function
        // value -> NullReferenceException in TypeHierarchy.Relationship during Block.NeverReturnsTypedNodes
        // (Invoice's map{...} literals). Faithful port of Type.getItemType(Item, TypeHierarchy):
        // atomic/map/array/function branches are exact; nodes use kind-level NodeKindTest (a correct
        // supertype) instead of the full SameName/content-type test, to avoid re-including the excluded
        // node-test cascade. The kind-level node type is sufficient for type-relationship queries.
        public static ItemType GetItemType(IItem item, TypeHierarchy th)
        {
            if (item == null)
            {
                return AnyItemType.GetInstance();
            }
            else if (item is AtomicValue)
            {
                return ((AtomicValue)item).GetItemType();
            }
            else if (item is NodeInfo)
            {
                return NodeKindTest.MakeNodeKindTest(((NodeInfo)item).GetNodeKind());
            }
            else if (item is MapItem)
            {
                return th == null ? (ItemType)MapType.ANY_MAP_TYPE : ((MapItem)item).GetItemType(th);
            }
            else if (item is ArrayItem)
            {
                return th == null ? (ItemType)ArrayItemType.ANY_ARRAY_TYPE : new ArrayItemType(((ArrayItem)item).GetMemberType(th));
            }
            else
            {
                return ((IFunctionItem)item).FunctionItemType;
            }
        }
        // Ported from upstream net/sf/saxon/type/Type.getCommonSuperType(ItemType,ItemType,TypeHierarchy):
        // least common supertype using the type-hierarchy relationship, falling back to the union of the
        // two UTypes for disjoint types (e.g. a union pattern chap|sec -> a node test matching both).
        public static ItemType GetCommonSuperType(ItemType t1, ItemType t2, TypeHierarchy th)
        {
            if (t1 == t2)
            {
                return t1;
            }
            if (t1 is ErrorType)
            {
                return t2;
            }
            if (t2 is ErrorType)
            {
                return t1;
            }
            if (t1 is MapType && t2 is MapType)
            {
                if (t1 == MapType.EMPTY_MAP_TYPE)
                {
                    return t2;
                }
                if (t2 == MapType.EMPTY_MAP_TYPE)
                {
                    return t1;
                }
                return MapType.ANY_MAP_TYPE;
            }
            Affinity r = th.Relationship(t1, t2);
            if (r == Affinity.SAME_TYPE)
            {
                return t1;
            }
            else if (r == Affinity.SUBSUMED_BY)
            {
                return t2;
            }
            else if (r == Affinity.SUBSUMES)
            {
                return t1;
            }
            else
            {
                return t1.GetUType().Union(t2.GetUType()).ToItemType();
            }
        }

        // Ported from upstream Type.getCommonSuperType(ItemType,ItemType) (no TypeHierarchy cache):
        // a coarser least-common-supertype used, among other things, by UnionPattern.GetItemType so that
        // xsl:number count="a|b" filters preceding siblings by a node test matching both branches.
        public static ItemType GetCommonSuperType(ItemType t1, ItemType t2)
        {
            if (t1 == t2)
            {
                return t1;
            }
            if (t1 is ErrorType)
            {
                return t2;
            }
            if (t2 is ErrorType)
            {
                return t1;
            }
            if (t1 == AnyItemType.GetInstance() || t2 == AnyItemType.GetInstance())
            {
                return AnyItemType.GetInstance();
            }
            ItemType p1 = t1.GetPrimitiveItemType();
            ItemType p2 = t2.GetPrimitiveItemType();
            if (p1 == p2)
            {
                if ((Genre)t1.GetGenre() == Genre.ARRAY && (Genre)t2.GetGenre() == Genre.ARRAY)
                {
                    return ArrayItemType.ANY_ARRAY_TYPE;
                }
                if ((Genre)t1.GetGenre() == Genre.MAP && (Genre)t2.GetGenre() == Genre.MAP)
                {
                    return MapType.ANY_MAP_TYPE;
                }
                return p1;
            }
            if ((p1 == BuiltInAtomicType.DECIMAL && p2 == BuiltInAtomicType.INTEGER) ||
                (p2 == BuiltInAtomicType.DECIMAL && p1 == BuiltInAtomicType.INTEGER))
            {
                return BuiltInAtomicType.DECIMAL;
            }
            if (p1 is BuiltInAtomicType && ((BuiltInAtomicType)p1).IsNumericType() &&
                p2 is BuiltInAtomicType && ((BuiltInAtomicType)p2).IsNumericType())
            {
                return NumericType.GetInstance();
            }
            if (t1.IsAtomicType() && t2.IsAtomicType())
            {
                return BuiltInAtomicType.ANY_ATOMIC;
            }
            if (t1 is NodeTest && t2 is NodeTest)
            {
                return AnyNodeTest.GetInstance();
            }
            return AnyItemType.GetInstance();
        }
        // Mirror upstream Types.getBuiltInItemType: fingerprint -> the functional BuiltInType registry below
        // (same path the xs:* constructor functions use via GetBuiltInSimpleType).
        public static ItemType GetBuiltInItemType(string ns, string local)
        {
            var t = BuiltInType.GetSchemaType(StandardNames.GetFingerprint(NamespaceUri.Of(ns), local));
            return t as ItemType;
        }
        // net472 port: real OutSmart.DAXon.Types.Type is excluded; these were hollow (=> null), which made
        // ConstructorFunctionLibrary.Bind report "Unknown constructor function" for xs:integer/xs:decimal/etc
        // (XPST0017) at compile. Mirror the real Types.GetBuiltInSimpleType: look up the built-in registry
        // (BuiltInType.GetSchemaType only ever returns built-in types, so the IsBuiltInType() filter is implicit).
        public static object GetBuiltInSimpleType(int fingerprint)
        {
            var t = BuiltInType.GetSchemaType(fingerprint);
            return t is ISimpleType ? (object)t : null;
        }
        public static object GetBuiltInSimpleType(string ns, string local) => GetBuiltInSimpleType(StandardNames.GetFingerprint(NamespaceUri.Of(ns), local));
        // Int-version overload (paulirwin passes XPathVersion int).
        public static bool IsPossiblyComparable(ItemType t1, ItemType t2, int xpathVersion) => true;
        // Faithful port of upstream Type.isSubType(AtomicType,AtomicType): walk the atomic base-type
        // chain. Was a hollow `=> false`, which defeated BuiltInAtomicType.Matches so every atomic
        // instance-of/treat-as gave false positives. Non-atomic operands keep the old `false` (unchanged).
        public static bool IsSubType(ItemType subtype, ItemType supertype)
        {
            if (subtype is IAtomicType one && supertype is IAtomicType two)
            {
                while (true)
                {
                    if (one.Fingerprint == two.Fingerprint)
                    {
                        return true;
                    }

                    ISchemaType s = one.BaseType;
                    if (s is IAtomicType b)
                    {
                        one = b;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return false;
        }
        // Ported verbatim from the excluded real Type.cs:508. The hollow `=> false` made
        // BuiltInAtomicType.IsPrimitiveType() always false, so the static ctor's first type
        // (xs:anyAtomicType, base = AnySimpleType) took the non-primitive branch and cast
        // AnySimpleType to IAtomicType -> InvalidCastException, throwing the whole .cctor.
        public static bool IsPrimitiveAtomicType(int fingerprint) =>
            fingerprint >= 0 && (fingerprint <= StandardNames.XS_INTEGER
                || fingerprint == StandardNames.XS_NUMERIC
                || fingerprint == StandardNames.XS_UNTYPED_ATOMIC
                || fingerprint == StandardNames.XS_ANY_ATOMIC_TYPE
                || fingerprint == StandardNames.XS_DAY_TIME_DURATION
                || fingerprint == StandardNames.XS_YEAR_MONTH_DURATION
                || fingerprint == StandardNames.XS_ANY_SIMPLE_TYPE);
        // Faithful port of upstream Type.isGuaranteedComparable (Type.java:520): the per-pair runtime guard
        // behind ValueComparison.Compare(checkTypes:true) plus the index-of/switch equality paths. Args are
        // the operands' PRIMITIVE types (AtomicValue.GetPrimitiveType); untypedAtomic counts as string here
        // because GeneralComparison converts untyped operands BEFORE this check. Was a hollow `=> true`, so
        // incomparable pairs (e.g. 6 = remove(('a',6),2)) silently compared false instead of XPTY0004.
        public static bool IsGuaranteedComparable(ItemType t1, ItemType t2, bool ordered)
        {
            // Upstream signature is (BuiltInAtomicType, BuiltInAtomicType) — non-atomic statics can't be
            // proven incomparable, and returning false here would THROW, so they keep the old `true`.
            if (!(t1 is BuiltInAtomicType a1) || !(t2 is BuiltInAtomicType a2))
                return true;
            if (a1 == a2)
                return true;
            if (a1.IsPrimitiveNumeric())
                return a2.IsPrimitiveNumeric();
            if (a1 == BuiltInAtomicType.UNTYPED_ATOMIC || a1 == BuiltInAtomicType.ANY_URI)
                a1 = BuiltInAtomicType.STRING;
            if (a2 == BuiltInAtomicType.UNTYPED_ATOMIC || a2 == BuiltInAtomicType.ANY_URI)
                a2 = BuiltInAtomicType.STRING;
            if (!ordered)
            {
                if (a1 == BuiltInAtomicType.DAY_TIME_DURATION || a1 == BuiltInAtomicType.YEAR_MONTH_DURATION)
                    a1 = BuiltInAtomicType.DURATION;
                if (a2 == BuiltInAtomicType.DAY_TIME_DURATION || a2 == BuiltInAtomicType.YEAR_MONTH_DURATION)
                    a2 = BuiltInAtomicType.DURATION;
            }
            return a1 == a2;
        }
        // Port of upstream Type.isGuaranteedComparable(primitive t1, primitive t2). GeneralComparison uses
        // !IsGuaranteedGenerallyComparable(...) as `runtimeCheckNeeded`; the hollow `=> true` made it ALWAYS
        // false, so a general comparison of a computed sequence with incomparable pairs (e.g. 6 = "a string",
        // where $x=$y over mixed int/string) silently returned false instead of XPTY0004. Guaranteed-comparable
        // only when the two primitive types need no per-pair check: identical, both numeric, or both string/anyURI.
        // Everything else falls to the runtime check (ValueComparison.Compare with checkTypes=true), which
        // compares comparable pairs correctly and raises XPTY0004 on incomparable ones — so a conservative
        // false here is always safe (never skips a needed check; at worst costs an unnecessary runtime check).
        public static bool IsGuaranteedGenerallyComparable(ItemType t1, ItemType t2, bool ordered)
        {
            if (!(t1 is BuiltInAtomicType a1) || !(t2 is BuiltInAtomicType a2))
                return false;
            // xs:anyAtomicType is "unknown type" and xs:untypedAtomic needs a runtime conversion — neither is
            // guaranteed comparable, so both must fall to the runtime check (this is the case that matters:
            // `for $x in (1,'a') for $y in (1,'a') where $x=$y` has both operands typed anyAtomicType).
            if (a1 == BuiltInAtomicType.ANY_ATOMIC || a2 == BuiltInAtomicType.ANY_ATOMIC)
                return false;
            if (a1 == BuiltInAtomicType.UNTYPED_ATOMIC || a2 == BuiltInAtomicType.UNTYPED_ATOMIC)
                return false;
            if (a1.Equals(a2))
                return true;
            if (a1.IsPrimitiveNumeric())
                return a2.IsPrimitiveNumeric();
            if (a1 == BuiltInAtomicType.STRING || a1 == BuiltInAtomicType.ANY_URI)
            {
                return a2 == BuiltInAtomicType.STRING || a2 == BuiltInAtomicType.ANY_URI;
            }
            return false;
        }
        public static string DisplayTypeName(IItem item) => item?.GetType().Name ?? "item";
    }
}
