////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

// Saxon-internal stub namespaces — sub-packages permanently excluded for now.
// Stubs only what's needed for top-level references to resolve.

namespace OutSmart.DAXon.Types
{

    // Extension methods for ItemType -- provide stubs that callers can invoke
    // without each implementer having to declare them. Lives in OutSmart.DAXon.Internal (which
    // is implicitly imported by every Saxon file).
    internal static class ItemTypeExtensions
    {
        // GetUType moved to Saxon side (excluded stubs.cs) where UType is accessible.
        // Removed `public static object GetUType(this ItemType t) => null;` -- object return broke 42 sites.
        // ItemType has no instance GetPrimitiveItemType, so BuiltInAtomicType's own recursive reduction
        // (walk to the base type until a Saxon-primitive) resolves THROUGH this extension whenever it
        // recurses on an ItemType-typed base (BuiltInAtomicType.cs:809). The old `=> t` returned the base
        // unchanged, so reduction stopped one level up: xs:int->xs:long instead of xs:int->xs:integer, and
        // arithmetic/comparison on any integer subtype below xs:long then failed to find a calculator
        // (XPTY0004). Delegate to the real covariant impl so the recursion actually reduces; non-atomic
        // item types (node/function/union tests) keep the identity behaviour they had before.
        // Union types (xs:numeric and other pure unions) have their own primitive item type (ANY_ATOMIC);
        // route them through the instance impl too. The old identity leaked the NumericType itself where callers
        // cast the result to BuiltInAtomicType (ValueComparison.OperandType) -> InvalidCastException on `+`/`eq`
        // whose static operand type is xs:numeric (e.g. remove((1,'two'),2)+1).
        public static ItemType GetPrimitiveItemType(this ItemType t) =>
            t is BuiltInAtomicType b ? (ItemType)b.GetPrimitiveItemType()
            : t is LocalUnionType u ? (ItemType)u.GetPrimitiveItemType()
            : t;
        // Java's String.matches(regex) moved to Extensions/JavaApiExtensions.cs
        // because resolution from ItemTypeExtensions class was unreliable across callsites.
        // GetBasicAlphaCode promoted to a real ItemType interface member (above) so interface-typed calls in
        // TypeHierarchy.Relationship virtual-dispatch to each item type's real impl instead of this ""-stub
        // (the "" stub made "".StartsWith("")==true -> every atomic pair returned SUBSUMED_BY -> spurious XPTY0004).
        public static string GetFullAlphaCode(this ItemType t) => "";
        public static double GetNormalizedDefaultPriority(this ItemType t) => 0.0;
        // 1-arg form taking TypeHierarchy (TypeChecker, Atomizer). Each type's own answer: as a stub that said true for
        // all, a map reached the static conversion rules as xs:error (XPTY0004) where atomizing it is FOTY0013.
        public static bool IsAtomizable(this ItemType t, object th)
        {
            TypeHierarchy h = th as TypeHierarchy;
            switch (t)
            {
                case OutSmart.DAXon.Values.Maps.IRecordType _:
                    return false;   // a map
                case AnyFunctionType f:
                    return f.IsAtomizable(h);
                case OutSmart.DAXon.Patterns.NodeTest n:
                    return n.IsAtomizable(h);
                case BuiltInAtomicType a:
                    return a.IsAtomizable(h);
                case EnumerationType e:
                    return e.IsAtomizable(h);
                case AnyExternalObjectType x:
                    return x.IsAtomizable(h);
                default:
                    return true;
            }
        }
        // A value nested thousands deep is explained a few levels down: each level depicts and matches all that lies under it,
        // and the whole was seconds and hundreds of MB past every limit for 2500 levels.
        private const int ExplainedLevels = 4;

        [System.ThreadStatic]
        private static int explaining;

        // What more can be said of an item that does not match, null when nothing. Each type answers for itself.
        public static string ExplainMismatch(this ItemType t, OutSmart.DAXon.Model.IItem item, TypeHierarchy th)
        {
            if (explaining >= ExplainedLevels)
            {
                return null;
            }

            explaining++;
            try
            {
                return ExplainOneLevel(t, item, th);
            }
            finally
            {
                explaining--;
            }
        }

        private static string ExplainOneLevel(ItemType t, OutSmart.DAXon.Model.IItem item, TypeHierarchy th)
        {
            switch (t)
            {
                case OutSmart.DAXon.Patterns.NodeTest node:
                    return node.ExplainMismatch(item, th);
                case IUnionType union:
                    return union.ExplainMismatch(item, th);
                case IAtomicType atomic:
                    return atomic.ExplainMismatch(item, th);
                case OutSmart.DAXon.Values.Maps.RecordTest record:
                    return record.ExplainMismatch(item, th);
                case OutSmart.DAXon.Values.Maps.MapType map:
                    return map.ExplainMismatch(item, th);
                case OutSmart.DAXon.Values.Arrays.ArrayItemType array:
                    return array.ExplainMismatch(item, th);
                default:
                    return null;
            }
        }

        public static string ToExportString(this ItemType t) => "";
    }
}
