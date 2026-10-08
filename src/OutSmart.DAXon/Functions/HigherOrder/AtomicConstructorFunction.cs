////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// A function item representing a constructor function for an atomic type (e.g. xs:integer#1).
    /// The type is never anonymous. Reached when a constructor is referenced dynamically —
    /// `xs:integer#1`, `function-lookup(xs:QName('...','date'),1)`, or passed as a function item.
    /// </summary>
    internal sealed class AtomicConstructorFunction : AbstractFunction
    {
        private readonly IAtomicType targetType;
        private readonly INamespaceResolver nsResolver;

        public override IFunctionItemType FunctionItemType => new SpecificFunctionType(
                new SequenceType[] { SequenceType.OPTIONAL_ATOMIC },
                SequenceType.MakeSequenceType(targetType, StaticProperty.ALLOWS_ZERO_OR_ONE));

        public override string Description => GetFunctionName().DisplayName;

        public AtomicConstructorFunction(IAtomicType targetType, INamespaceResolver resolver)
        {
            this.targetType = targetType;
            this.nsResolver = resolver;
        }

        public override StructuredQName GetFunctionName()
        {
            return targetType.TypeName;
        }

        public override int GetArity()
        {
            return 1;
        }

        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            AtomicValue val = (AtomicValue)args[0].Head();
            if (val == null)
            {
                return EmptySequence.GetInstance();
            }

            ConversionRules rules = context.GetConfiguration().GetConversionRules();
            Converter converter = rules.GetConverter(val.GetItemType(), targetType);
            if (converter == null)
            {
                XPathException ex = new XPathException("Cannot convert " + val.GetItemType() + " to " + targetType, "XPTY0004");
                ex.SetIsTypeError(true);
                throw ex;
            }

            converter = converter.SetNamespaceResolver(nsResolver);
            return converter.Convert(val).AsAtomic();
        }

        public override bool IsTrustedResultType()
        {
            return true;
        }
    }
}
