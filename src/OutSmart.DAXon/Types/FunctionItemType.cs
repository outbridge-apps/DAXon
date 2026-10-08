////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// An ItemType representing the type of a function item (subclasses are used for maps and arrays)
    /// </summary>
    public interface IFunctionItemType : ItemType
    {
        Genre GetGenre();



        SequenceType[] ArgumentTypes { get; }
        SequenceType ResultType { get; }
        Affinity Relationship(IFunctionItemType other, TypeHierarchy th);
        AnnotationList AnnotationAssertions { get; }
        Expression MakeFunctionSequenceCoercer(Expression exp, Func<RoleDiagnostic> role, bool allow40);
        bool IsMapType();
        bool IsArrayType();
    }
}
