////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class UntypedSequenceConverter : AtomicSequenceConverter
    {

        public override string ExpressionName => "convertUntyped";
        public UntypedSequenceConverter(Expression sequence, IPlainType requiredItemType) : base(sequence, requiredItemType)
        {
        }

        public static UntypedSequenceConverter MakeUntypedSequenceConverter(Configuration config, Expression operand, IPlainType requiredItemType)
        {
            UntypedSequenceConverter atomicSeqConverter = new UntypedSequenceConverter(operand, requiredItemType);
            ConversionRules rules = config.GetConversionRules();
            Converter untypedConverter;
            if (requiredItemType.IsNamespaceSensitive())
            {
                throw new XPathException("Cannot convert untyped atomic values to a namespace-sensitive type", "XPTY0117");
            }

            if (requiredItemType.IsAtomicType())
            {
                untypedConverter = rules.GetConverter(BuiltInAtomicType.UNTYPED_ATOMIC, (IAtomicType)requiredItemType);
            }
            else if (requiredItemType == NumericType.GetInstance())
            {

                // converting untyped to numeric is common, and is effectively the same as converting to double
                untypedConverter = rules.GetConverter(BuiltInAtomicType.UNTYPED_ATOMIC, BuiltInAtomicType.DOUBLE);
                atomicSeqConverter.requiredItemType = BuiltInAtomicType.DOUBLE;
            }
            else
            {
                // Union required item type: Java converts via StringToUnionConverter (not ported). The
                // conversion only runs if a non-empty untypedAtomic value actually reaches it at runtime —
                // an empty sequence (as-0201: templates with as="empty-sequence()") converts nothing — so
                // defer the "unsupported" failure to conversion time instead of failing to compile.
                untypedConverter = new UnionUnsupportedConverter(rules);
            }


            // source type not known statically; create a converter that decides at run-time
            Converter converter = new UntypedConverter(rules, untypedConverter);
            atomicSeqConverter.SetConverter(converter);
            return atomicSeqConverter;
        }

        public static UntypedSequenceConverter MakeUntypedSequenceRejector(Configuration config, Expression operand, IPlainType requiredItemType)
        {
            UntypedSequenceConverter atomicSeqConverter = new UntypedSequenceConverter(operand, requiredItemType);
            ConversionRules rules = config.GetConversionRules();
            Converter untypedConverter = new AnonymousConverter(rules, operand, requiredItemType);

            // source type not known statically; create a converter that decides at run-time
            Converter converter = new UntypedConverter(rules, untypedConverter);
            atomicSeqConverter.SetConverter(converter);
            return atomicSeqConverter;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e2 = base.TypeCheck(visitor, contextInfo);
            if (e2 != this)
            {
                return e2;
            }

            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            Expression @base = BaseExpression;
            if (th.Relationship(@base.GetItemType(), BuiltInAtomicType.UNTYPED_ATOMIC) == Affinity.DISJOINT || @base.HasSpecialProperty(StaticProperty.NOT_UNTYPED_ATOMIC))
            {

                // operand cannot return untyped atomic values, so there's nothing to convert
                return BaseExpression;
            }

            return this;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            return p | StaticProperty.NO_NODES_NEWLY_CREATED | StaticProperty.NOT_UNTYPED_ATOMIC;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            UntypedSequenceConverter atomicConverter = new UntypedSequenceConverter(BaseExpression.Copy(rebindings), RequiredItemType);
            ExpressionTool.CopyLocationInfo(this, atomicConverter);
            atomicConverter.SetConverter(converter);
            atomicConverter.SetRoleDiagnostic(RoleSupplier);
            return atomicConverter;
        }

        public override ItemType GetItemType()
        {
            if (BaseExpression.GetItemType() == BuiltInAtomicType.UNTYPED_ATOMIC)
            {
                return RequiredItemType;
            }
            else
            {
                TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                return Types.Type.GetCommonSuperType(RequiredItemType, BaseExpression.GetItemType(), th);
            }
        }

        /// <summary>
        /// Determine the static cardinality of the expression
        /// </summary>
        protected override int ComputeCardinality()
        {
            return BaseExpression.GetCardinality();
        }

        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        public override bool Equals(object other)
        {
            return other is UntypedSequenceConverter && BaseExpression.IsEqual(((UntypedSequenceConverter)other).BaseExpression);
        }

        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode();
        }

        protected override string DisplayOperator(Configuration config)
        {
            return "convertUntyped";
        }

        public override string ToShortString()
        {
            return BaseExpression.ToShortString();
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("cvUntyped", this);
            destination.EmitAttribute("to", AlphaCode.FromItemType(RequiredItemType));
            if (RoleSupplier != null)
            {
                destination.EmitAttribute("diag", RoleSupplier().Save());
            }

            BaseExpression.Export(destination);
            destination.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new UntypedSequenceConverterElaborator();
        }

        // Deferred failure for untypedAtomic -> union type (StringToUnionConverter is not ported). Fails only
        // if actually applied to a value; an empty sequence never invokes it.
        private sealed class UnionUnsupportedConverter : Converter
        {
            public UnionUnsupportedConverter(ConversionRules rules) : base(rules) { }
            public override IConversionResult Convert(object value)
                => new ValidationFailure("Implicit conversion of untypedAtomic to a union type is not supported in this port build", "XPTY0117");
        }

        internal sealed class UntypedConverter : Converter
        {
            Converter untypedConverter = null;
            public UntypedConverter(ConversionRules rules, Converter converter) : base(rules)
            {
                untypedConverter = converter; //untypedConverter.setConversionRules(rules);
            }

            public override IConversionResult Convert(object input) // compat base Converter.Convert(object): adapt
            {
                if (((AtomicValue)input).IsUntypedAtomic())
                {
                    return untypedConverter.Convert(input);
                }
                else
                {
                    return (IConversionResult)input;
                }
            }
        }

        private sealed class AnonymousConverter : Converter
        {

            private readonly Expression operand;
            private readonly IPlainType requiredItemType;
            public AnonymousConverter(ConversionRules rules, Expression operand, IPlainType requiredItemType) : base(rules)
            {
                this.operand = operand;
                this.requiredItemType = requiredItemType;
            }
            public override IConversionResult Convert(object input) // compat base Converter.Convert(object): adapt
            {
                ValidationFailure vf = new ValidationFailure("Implicit conversion of untypedAtomic value to " + requiredItemType.ToString() + " is not allowed");
                vf.SetErrorCode("XPTY0117");
                vf.Locator = operand.GetLocation();
                return vf;
            }
        }

        /// <summary>
        /// Elaborator for an UntypedSequenceConverter
        /// </summary>
        internal sealed class UntypedSequenceConverterElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                UntypedSequenceConverter expr = (UntypedSequenceConverter)GetExpression();
                UntypedConverter converter = (UntypedConverter)expr.GetConverter();
                AtomicSequenceMappingFunction mapper = new AtomicSequenceMappingFunction();
                mapper.SetConverter(converter);
                if (expr.RoleSupplier != null)
                {
                    string errorCode = expr.RoleSupplier().ErrorCode;
                    mapper.SetErrorCode("XPTY0004".Equals(errorCode) ? "FORG0001" : errorCode);
                }

                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    ISequenceIterator @base = baseEval.Iterate(context);
                    return new ItemMappingIterator(@base, mapper, true);
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                AtomicSequenceConverter expr = (AtomicSequenceConverter)GetExpression();
                IItemEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForItem();
                Converter converter = expr.GetConverter();
                bool nullable = Cardinality.AllowsZero(expr.BaseExpression.GetCardinality());
                return (context) =>
                {
                    AtomicValue baseValue = (AtomicValue)baseEval.Eval(context);
                    if (nullable && baseValue == null)
                    {
                        return null;
                    }

                    IConversionResult result = converter.Convert(baseValue);
                    if (result is ValidationFailure)
                    {
                        if (expr.RoleSupplier != null)
                        {
                            string errorCode = expr.RoleSupplier().ErrorCode;
                            throw new XPathException(((ValidationFailure)result).GetMessage(), errorCode);
                        }
                        else
                        {
                            throw ((ValidationFailure)result).MakeException();
                        }
                    }

                    return result.AsAtomic();
                };
            }
        }
    }
}
