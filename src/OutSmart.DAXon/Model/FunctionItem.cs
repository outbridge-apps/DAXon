////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Model
{
    public interface IFunctionItem : IItem, ICallable, IGroundedValue
    {
        // TODO: Currently SystemFunction and UserFunction implement this interface, despite
        //  the fact that they support an arity range, which dynamic functions don't allow. In effect
        //  they act as function items for the function at the top end of the arity range. Need
        //  to better reflect the 4.0 data model when it's finalised.
        bool IsMap();
        bool IsArray();
        IFunctionItemType FunctionItemType { get; }
        StructuredQName GetFunctionName();
        int GetArity();
        bool IsSequenceVariadic();



        OperandRole[] OperandRoles { get; }
        AnnotationList GetAnnotations();
        IXPathContext MakeNewContext(IXPathContext callingContext, IContextOriginator originator);
        bool DeepEquals(IFunctionItem other, IXPathContext context, IAtomicComparer comparer, int flags);
        bool DeepEqual40(IFunctionItem other, IXPathContext context, DeepEqual.DeepEqualOptions options);
        string Description { get; }
        void Export(ExpressionPresenter @out);
        bool IsTrustedResultType();
        string ToShortString();



        Genre GetGenre();


    }
}
