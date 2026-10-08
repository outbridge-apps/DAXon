////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Lib
{
    public abstract class ExtensionFunctionDefinition
    {
        public abstract StructuredQName FunctionQName { get; }
        public virtual int MinimumNumberOfArguments => ArgumentTypes.Length;

        public virtual int MaximumNumberOfArguments => MinimumNumberOfArguments;

        public abstract Values.SequenceType[] ArgumentTypes { get; }
        public virtual Values.SequenceType GetResultType(Values.SequenceType[] suppliedArgumentTypes) => null;
        public virtual bool TrustResultType()
        {
            return false;
        }

        public virtual bool DependsOnFocus()
        {
            return false;
        }

        public virtual bool HasSideEffects()
        {
            return false;
        }

        public abstract ExtensionFunctionCall MakeCallExpression();
        public IFunctionItem AsFunction(int arity)
        {
            return new IExtensionFunction(this, arity);
        }

        private sealed class IExtensionFunction : AbstractFunction
        {
            private readonly ExtensionFunctionDefinition definition;
            private readonly int arity;

            public override IFunctionItemType FunctionItemType => new SpecificFunctionType(definition.ArgumentTypes, definition.GetResultType(definition.ArgumentTypes));

            public override string Description => definition.FunctionQName.DisplayName;
            public IExtensionFunction(ExtensionFunctionDefinition definition, int arity)
            {
                this.definition = definition;
                this.arity = arity;
            }

            public override ISequence Call(IXPathContext context, ISequence[] args)
            {
                if (args.Length != arity)
                {

                    // can happen on a dynamic call
                    throw new XPathException("Wrong number of arguments in call to " + definition.FunctionQName.DisplayName, "XPTY0004");
                }

                return definition.MakeCallExpression().Call(context, args);
            }

            public override StructuredQName GetFunctionName()
            {
                return definition.FunctionQName;
            }

            public override int GetArity()
            {
                return this.arity;
            }

            public override bool IsTrustedResultType()
            {
                return definition.TrustResultType();
            }
        }
    }
}