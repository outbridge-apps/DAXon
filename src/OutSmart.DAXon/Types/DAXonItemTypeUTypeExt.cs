////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;

// Replace OutSmart.DAXon.Internal's `GetUType(this ItemType) => object` (which broke 42 sites)
// with a UType-typed shim on the Saxon side. ItemType lives in OutSmart.DAXon.Types namespace
// so we put the extension there too. Callers already have `using OutSmart.DAXon.Types;`.
namespace OutSmart.DAXon.Types
{
    internal static class DAXonItemTypeUTypeExt
    {
        // A receiver typed as bare ItemType binds here, not to the interface member: route it to its own GetUType.
        // VOID for one would make every axis step from it "select nothing" (SXWN9037) and drop the step.
        public static UType GetUType(this ItemType t) => t is IItemTypeWithSequenceTypeCache __c ? __c.GetUType() : UType.VOID;
        // runtime 2026-06-04: Genre-typed shim replacing OutSmart.DAXon.Internal's hollow `GetGenre(this ItemType) => null`
        // (which made `(Genre)itemType.GetGenre()` unbox null -> NRE in AxisExpression.TypeCheck:134, hit by
        // every xsl:for-each / path-step type-check; FinDim/Trans). Dispatches to the concrete type's real
        // GetGenre via IItemTypeWithSequenceTypeCache (AnyItemType / NodeTest / atomic & function types all
        // implement it; the interface member beats this extension for interface-typed receivers). Bare or
        // unknown ItemTypes fall back to Genre.ANY (the node-bearing supertype -> safe for the axis-step check).
        public static Genre GetGenre(this ItemType t)
            => t is IItemTypeWithSequenceTypeCache __c ? __c.GetGenre() : Genre.ANY;
        // runtime 2026-06-05: dispatch shim replacing OutSmart.DAXon.Internal's hollow `GetAtomizedItemType(this ItemType)=>t`.
        // For a NodeTest the atomized item type is the content's atomic type (NodeTest.GetAtomizedItemType ->
        // ANY_ATOMIC for untyped), NOT the node test itself; the hollow returned the NameTest, so
        // TypeChecker.StaticTypeCheck rule-3 promotion threw "NameTest cannot be converted to BuiltInAtomicType"
        // for FinDim's `DIMENSIONVALUE != ''` (and any comparison/atomization of a node step). `__nt` is the
        // concrete NodeTest, so its real instance method runs (instance beats extension -> no recursion). Atomic
        // and other item types correctly atomize to themselves (=> t).
        // runtime 2026-07-11: the `=> t` fallback was only right for atomic types; AnyItemType must
        // atomize to ANY_ATOMIC (upstream AnyItemType.getAtomizedItemType) and union/function types have
        // their own impls. Route through IItemTypeWithSequenceTypeCache like GetUType/GetGenre above —
        // hit by SystemFunction.GetResultItemType AS_PRIM_ARG0 ((IPlainType) cast threw; xslt math-26xx).
        public static ItemType GetAtomizedItemType(this ItemType t)
            => t is NodeTest __nt ? __nt.GetAtomizedItemType()
             : t is IItemTypeWithSequenceTypeCache __c ? (ItemType)__c.GetAtomizedItemType()
             : t;
    }
}
