////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Tracing;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public interface ICodeInjector
    {
        Expression Inject(Expression exp);



        void Process(ITraceableComponent component);


        Clause InjectClause(FLWORExpression expression, Clause clause);


    }
}
