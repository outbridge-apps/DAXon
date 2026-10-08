////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// A function item representing a constructor function for a list type
    /// </summary>
    internal class ListConstructorFunction : AbstractFunction
    {
        protected IListType targetType;
        protected INamespaceResolver nsResolver;
        protected bool allowEmpty;
        protected ISimpleType memberType;

        public virtual IListType TargetType => targetType;

        public override IFunctionItemType FunctionItemType
        {
            get
            {
                IAtomicType resultType = BuiltInAtomicType.ANY_ATOMIC;
                if (memberType.IsAtomicType())
                {
                    resultType = (IAtomicType)memberType;
                }

                SequenceType argType = allowEmpty ? SequenceType.OPTIONAL_ATOMIC : SequenceType.SINGLE_ATOMIC;
                return new SpecificFunctionType(new SequenceType[] { argType }, SequenceType.MakeSequenceType(resultType, StaticProperty.ALLOWS_ZERO_OR_MORE));
            }
        }

        public override string Description => GetFunctionName().DisplayName;
        public ListConstructorFunction(IListType targetType, INamespaceResolver resolver, bool allowEmpty)
        {
            this.targetType = targetType;
            this.nsResolver = resolver;
            this.allowEmpty = allowEmpty;
            this.memberType = targetType.GetItemType();
        }

        public virtual bool IsAllowEmpty()
        {
            return allowEmpty;
        }

        public override StructuredQName GetFunctionName()
        {
            return targetType.GetStructuredQName();
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
                if (allowEmpty)
                {
                    return EmptyAtomicSequence.GetInstance();
                }
                else
                {
                    XPathException e = new XPathException("Cast expression does not allow an empty sequence to be supplied", "XPTY0004");
                    e.SetIsTypeError(true);
                    throw e;
                }
            }

            if (!(val is StringValue) || val is AnyURIValue)
            {
                XPathException e = new XPathException("Only xs:string and xs:untypedAtomic can be cast to a list type", "XPTY0004");
                e.SetIsTypeError(true);
                throw e;
            }

            ConversionRules rules = context.GetConfiguration().GetConversionRules();
            UnicodeString cs = val.UnicodeStringValue;
            ValidationFailure failure = targetType.ValidateContent(cs, nsResolver, rules);
            if (failure != null)
            {
                throw failure.MakeException();
            }

            return targetType.GetTypedValue(cs, nsResolver, rules);
        }
    }
}
