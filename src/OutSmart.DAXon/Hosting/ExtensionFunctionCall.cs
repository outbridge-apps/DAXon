////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Lib
{
    public abstract class ExtensionFunctionCall : ICallable
    {
        ExtensionFunctionDefinition definition;

        public ExtensionFunctionDefinition Definition { get => definition; set => this.definition = value; }

        public virtual object StreamingImplementation => null;

        public virtual void SupplyStaticContext(IStaticContext context, int locationId, Expression[] arguments)
        {
        }

        public virtual Expression Rewrite(IStaticContext context, Expression[] arguments)
        {

            // default implementation does nothing
            return null;
        }

        public virtual void CopyLocalData(ExtensionFunctionCall destination)
        {
        }

        public abstract ISequence Call(IXPathContext context, ISequence[] arguments);
        public virtual bool EffectiveBooleanValue(IXPathContext context, ISequence[] arguments)
        {
            return ExpressionTool.EffectiveBooleanValue(Call(context, arguments).Iterate());
        }
    }
}