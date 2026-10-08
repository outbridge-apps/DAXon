////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    internal sealed class UnresolvedXQueryFunctionItem : AbstractFunction
    {
        private readonly XQueryFunction fd;
        private readonly SymbolicName.F functionName;
        private readonly UserFunctionReference @ref;

        public override IFunctionItemType FunctionItemType => new SpecificFunctionType(fd.ArgumentTypes, fd.ResultType);

        public override string Description => functionName.ToString();

        public UserFunctionReference FunctionReference => @ref;
        public UnresolvedXQueryFunctionItem(XQueryFunction fd, SymbolicName.F functionName, UserFunctionReference @ref)
        {
            this.fd = fd;
            this.functionName = functionName;
            this.@ref = @ref;
        }

        public override StructuredQName GetFunctionName()
        {
            return functionName.ComponentName;
        }

        public override int GetArity()
        {
            return functionName.GetArity();
        }

        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            return ((IFunctionItem)@ref.EvaluateItem(context)).Call(context, args);
        }
    }
}
