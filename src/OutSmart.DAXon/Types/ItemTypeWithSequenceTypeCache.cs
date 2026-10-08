////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Types
{
    public interface IItemTypeWithSequenceTypeCache : ItemType
    {
        UType GetUType(); // runtime: restore polymorphic GetUType dispatch (hollow GetUType(this ItemType)=>VOID extension shadowed concrete impls)
        Genre GetGenre(); // runtime: same dispatch restore as GetUType -- the hollow GetGenre(this ItemType)=>null extension unboxed null at (Genre) casts (AxisExpression.TypeCheck:134)
        IPlainType GetAtomizedItemType(); // runtime: same dispatch restore -- the `=> t` extension fallback returned AnyItemType UNATOMIZED (upstream: ANY_ATOMIC), so SystemFunction.GetResultItemType's (IPlainType) cast threw for AS_PRIM_ARG0 functions
        SequenceType One();
        SequenceType ZeroOrOne();
        SequenceType OneOrMore();
        SequenceType ZeroOrMore();
    }
}