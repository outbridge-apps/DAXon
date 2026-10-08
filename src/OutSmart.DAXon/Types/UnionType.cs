////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Types
{
    internal interface IUnionType : ItemType, ICastingTarget
    {
        StructuredQName TypeName { get; }
        StructuredQName GetStructuredQName();
        IList<IPlainType> PlainMemberTypes { get; }
        SequenceType ResultTypeOfCast { get; }
        IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules);
        ValidationFailure CheckAgainstFacets(AtomicValue value, ConversionRules rules);
        string ExplainMismatch(IItem item, TypeHierarchy th);











        string Description { get; }









    }
}
