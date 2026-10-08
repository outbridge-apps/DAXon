////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Function to perform a cast to a union type
    /// </summary>
    internal class UnionConstructorFunction : AbstractFunction
    {
        protected IUnionType targetType;
        protected INamespaceResolver resolver;
        protected bool allowEmpty;

        public virtual IUnionType TargetType => targetType;

        // net472: these were declared virtual/plain, shadowing (not overriding) the AbstractFunction base, so
        // base-typed dispatch (StaticFunctionCall ctor -> IFunctionItem.GetArity, type checking) hit the throwing
        // AbstractFunction stubs. Mark them override so xs:numeric(...)/xs:error(...) union constructors work.
        public override IFunctionItemType FunctionItemType
        {
            get
            {
                SequenceType resultType = targetType.ResultTypeOfCast;
                SequenceType argType = allowEmpty ? SequenceType.OPTIONAL_ATOMIC : SequenceType.SINGLE_ATOMIC;
                return new SpecificFunctionType(new SequenceType[] { argType }, resultType);
            }
        }

        public override string Description => GetFunctionName().DisplayName;
        public UnionConstructorFunction(IUnionType targetType, INamespaceResolver resolver, bool allowEmpty)
        {
            this.targetType = targetType;
            this.resolver = resolver;
            this.allowEmpty = allowEmpty;
        }

        public virtual bool IsAllowEmpty()
        {
            return allowEmpty;
        }

        public override StructuredQName GetFunctionName()
        {
            return targetType.TypeName;
        }

        public override int GetArity()
        {
            return 1;
        }

        public virtual IAtomicSequence Cast(AtomicValue value, IXPathContext context)
        {
            ConversionRules rules = context.GetConfiguration().GetConversionRules();
            if (value == null)
            {
                throw new NullReferenceException();
            }


            // 1. If the value is a string or untypedAtomic, try casting to each of the member types
            if (value is StringValue && !(value is AnyURIValue))
            {
                try
                {
                    return targetType.GetTypedValue(value.UnicodeStringValue, resolver, rules);
                }
                catch (ValidationException e)
                {
                    throw e.WithErrorCode("FORG0001");
                }
            }


            // 2. If the value is an instance of a type in the transitive membership of the union, return it unchanged
            IAtomicType label = value.GetItemType();
            IEnumerable<IPlainType> memberTypes = ((IUnionType)targetType).PlainMemberTypes;

            // 2a. Is the type annotation itself a member type of the union, and of the union type itself?
            if (targetType.IsPlainType())
            {
                foreach (IPlainType member in memberTypes)
                {
                    if (label.Equals(member))
                    {
                        return value;
                    }
                }


                // 2b. Failing that, is some supertype of the type annotation a member type of the union?
                foreach (IPlainType member in memberTypes)
                {
                    IAtomicType t = label;
                    while (t != null)
                    {
                        if (t.Equals(member))
                        {
                            return value;
                        }
                        else
                        {
                            t = t.BaseType is IAtomicType ? (IAtomicType)t.BaseType : null;
                        }
                    }
                }
            }


            // 3. if the value can be cast to any of the member types, return the result of that cast
            foreach (IPlainType type in memberTypes)
            {
                if (type is IAtomicType)
                {
                    Converter c = rules.GetConverter(value.GetItemType(), (IAtomicType)type);
                    if (c != null)
                    {
                        IConversionResult result = c.Convert(value);
                        if (result is AtomicValue)
                        {

                            // 3b. if the union type has constraining facets then the value must satisfy these
                            if (!targetType.IsPlainType())
                            {
                                ValidationFailure vf = targetType.CheckAgainstFacets((AtomicValue)result, rules);
                                if (vf == null)
                                {
                                    return (AtomicValue)result;
                                }
                            }
                            else
                            {
                                return (AtomicValue)result;
                            }
                        }
                    }
                }
            }

            throw new XPathException("Cannot convert the supplied value to " + targetType.Description, "FORG0001");
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

            return Cast(val, context);
        }

        public static IAtomicSequence Cast(AtomicValue value, IUnionType targetType, INamespaceResolver nsResolver, ConversionRules rules)
        {

            if (value == null)
            {
                throw new NullReferenceException();
            }


            // 1. If the value is a string or untypedAtomic, try casting to each of the member types
            if (value is StringValue && !(value is AnyURIValue))
            {
                try
                {
                    return targetType.GetTypedValue(value.UnicodeStringValue, nsResolver, rules);
                }
                catch (ValidationException e)
                {
                    e.SetErrorCode("FORG0001");
                    throw;
                }
            }


            // 2. If the value is an instance of a type in the transitive membership of the union, return it unchanged
            IAtomicType label = value.GetItemType();
            IEnumerable<IPlainType> memberTypes = targetType.PlainMemberTypes;

            // 2a. Is the type annotation itself a member type of the union?
            foreach (IPlainType member in memberTypes)
            {
                if (label.Equals(member))
                {
                    return value;
                }
            }


            // 2b. Failing that, is some supertype of the type annotation a member type of the union?
            foreach (IPlainType member in memberTypes)
            {
                IAtomicType t = label;
                while (t != null)
                {
                    if (t.Equals(member))
                    {
                        return value;
                    }
                    else
                    {
                        t = t.BaseType is IAtomicType ? (IAtomicType)t.BaseType : null;
                    }
                }
            }


            // 3. if the value can be cast to any of the member types, return the result of that cast
            foreach (IPlainType type in memberTypes)
            {
                if (type is IAtomicType)
                {
                    Converter c = rules.GetConverter(value.GetItemType(), (IAtomicType)type);
                    if (c != null)
                    {
                        IConversionResult result = c.Convert(value);
                        if (result is AtomicValue)
                        {
                            return (AtomicValue)result;
                        }
                    }
                }
            }

            throw new XPathException("Cannot convert the supplied value to " + targetType.Description, "FORG0001");
        }
    }
}
