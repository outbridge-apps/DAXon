////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
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
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal class AtomicSequenceConverter : UnaryExpression
    {
        public static ToStringMappingFunction TO_STRING_MAPPER = new ToStringMappingFunction();
        protected IPlainType requiredItemType;
        protected Converter converter;
        private Func<RoleDiagnostic> roleSupplier; // may be null

        public virtual IPlainType RequiredItemType => requiredItemType;

        public virtual Func<RoleDiagnostic> RoleSupplier => this.roleSupplier;

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string StreamerName => "AtomicSequenceConverter";

        public override string ExpressionName => "convert";
        public AtomicSequenceConverter(Expression sequence, IPlainType requiredItemType) : base(sequence)
        {
            this.requiredItemType = requiredItemType;
        }

        public static AtomicSequenceConverter MakeDownCaster(Expression sequence, IAtomicType requiredItemType, Configuration config)
        {
            AtomicSequenceConverter asc = new AtomicSequenceConverter(sequence, requiredItemType);
            asc.SetConverter(new UnfailingConverter.DownCastingConverter(requiredItemType, config.GetConversionRules(), "XPTY0004"));
            return asc;
        }

        public virtual void AllocateConverterStatically(Configuration config, bool allowNull)
        {
            converter = AllocateConverter(config, allowNull, BaseExpression.GetItemType());
        }

        public virtual Converter AllocateConverter(Configuration config, bool allowNull)
        {
            return AllocateConverter(config, allowNull, BaseExpression.GetItemType());
        }

        protected virtual Converter GetConverterDynamically(IXPathContext context)
        {
            if (converter != null)
            {
                return converter;
            }

            return AllocateConverter(context.GetConfiguration(), false);
        }

        public virtual Converter AllocateConverter(Configuration config, bool allowNull, ItemType sourceType)
        {
            ConversionRules rules = config.GetConversionRules();
            Converter converter = null;
            if (sourceType is ErrorType)
            {
                converter = StringConverter.IdentityConverter.INSTANCE;
            }
            else if (!(sourceType is IAtomicType))
            {
                converter = null;
            }
            else if (requiredItemType is IAtomicType)
            {
                converter = rules.GetConverter((IAtomicType)sourceType, (IAtomicType)requiredItemType);
            }
            else if (((ISimpleType)requiredItemType).IsUnionType())
            {
                converter = new StringConverter.StringToUnionConverter(requiredItemType, rules);
            }

            if (converter == null && !allowNull)
            {

                // source type not known statically; create a converter that decides at run-time
                converter = new AnonymousConverter(this, rules);
            }

            return converter;
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.ATOMIC_SEQUENCE;
        }

        public virtual Converter GetConverter()
        {
            return converter;
        }

        public virtual void SetConverter(Converter converter)
        {
            this.converter = converter;
        }

        public virtual void SetRoleDiagnostic(Func<RoleDiagnostic> roleSupplier)
        {
            if (roleSupplier != null)
            {
                this.roleSupplier = roleSupplier;
            }
        }

        public override Expression Simplify()
        {
            Expression operand = BaseExpression.Simplify();
            BaseExpression = operand;
            if (operand is Literal && requiredItemType is IAtomicType)
            {
                if (Literal.IsEmptySequence(operand))
                {
                    return operand;
                }

                Configuration config = GetConfiguration();
                if (converter == null)
                {
                    AllocateConverterStatically(config, true);
                }

                if (converter != null)
                {
                    try
                    {
                        IGroundedValue val = SequenceTool.ToGroundedValue(Iterate(new EarlyEvaluationContext(config)));
                        return Literal.MakeLiteral(val, operand);
                    }
                    catch (UncheckedXPathException e)
                    {
                        throw e.GetXPathException();
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            Expression operand = BaseExpression;
            if (th.IsSubType(operand.GetItemType(), requiredItemType))
            {
                return operand;
            }
            else
            {
                if (converter == null)
                {
                    AllocateConverterStatically(config, true);
                }

                return this;
            }
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e = base.Optimize(visitor, contextInfo);
            if (e != this)
            {
                return e;
            }

            if (BaseExpression is UntypedSequenceConverter)
            {
                UntypedSequenceConverter asc = (UntypedSequenceConverter)BaseExpression;
                ItemType ascType = asc.GetItemType();
                if (ascType == requiredItemType)
                {
                    return BaseExpression;
                }
                else if ((requiredItemType == BuiltInAtomicType.STRING || requiredItemType == BuiltInAtomicType.UNTYPED_ATOMIC) && (ascType == BuiltInAtomicType.STRING || ascType == BuiltInAtomicType.UNTYPED_ATOMIC))
                {
                    UntypedSequenceConverter old = (UntypedSequenceConverter)BaseExpression;
                    UntypedSequenceConverter asc2 = new UntypedSequenceConverter(old.BaseExpression, requiredItemType);
                    return asc2.TypeCheck(visitor, contextInfo).Optimize(visitor, contextInfo);
                }
            }
            else if (BaseExpression is AtomicSequenceConverter)
            {
                AtomicSequenceConverter asc = (AtomicSequenceConverter)BaseExpression;
                ItemType ascType = asc.GetItemType();
                if (ascType == requiredItemType)
                {
                    return BaseExpression;
                }
                else if ((requiredItemType == BuiltInAtomicType.STRING || requiredItemType == BuiltInAtomicType.UNTYPED_ATOMIC) && (ascType == BuiltInAtomicType.STRING || ascType == BuiltInAtomicType.UNTYPED_ATOMIC))
                {
                    AtomicSequenceConverter old = (AtomicSequenceConverter)BaseExpression;
                    AtomicSequenceConverter asc2 = new AtomicSequenceConverter(old.BaseExpression, requiredItemType);
                    return asc2.TypeCheck(visitor, contextInfo).Optimize(visitor, contextInfo);
                }
            }

            return this;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties() | StaticProperty.NO_NODES_NEWLY_CREATED;
            if (requiredItemType == BuiltInAtomicType.UNTYPED_ATOMIC)
            {
                p &= ~StaticProperty.NOT_UNTYPED_ATOMIC;
            }
            else
            {
                p |= StaticProperty.NOT_UNTYPED_ATOMIC;
            }

            return p;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            AtomicSequenceConverter atomicConverter = new AtomicSequenceConverter(BaseExpression.Copy(rebindings), requiredItemType);
            ExpressionTool.CopyLocationInfo(this, atomicConverter);
            atomicConverter.SetConverter(converter);
            atomicConverter.SetRoleDiagnostic(RoleSupplier);
            return atomicConverter;
        }

        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        // -2 = not yet checked, -1 = not the fused child shape, >=0 = child fingerprint (benign-race
        // int cache: the expression tree is immutable after compile, every thread computes the same fp).
        private int fusedChildFp = -2;

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            int fp = fusedChildFp;
            if (fp == -2)
            {
                fusedChildFp = fp = Elaboration.FusedChildAtomizer.Match(this, out int m) ? m : -1;
            }

            // Fused string(child::NAME): walk the Tiny child array directly instead of building an
            // axis + atomizing + converting iterator stack per evaluation. Off the fast path (non-Tiny
            // context item or schema-typed tree) fall through to the generic conversion.
            if (fp >= 0 && Elaboration.FusedChildAtomizer.CanFuse(context))
            {
                return new Elaboration.FusedChildAtomizer.ChildStringIterator((Trees.Tiny.TinyParentNodeImpl)context.GetContextItem(), fp);
            }

            ISequenceIterator @base = BaseExpression.Iterate(context);
            return GetConvertingIterator(context, @base);
        }

        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        public virtual ItemMappingIterator GetConvertingIterator(IXPathContext context, ISequenceIterator @base)
        {
            Converter conv = GetConverterDynamically(context);
            if (conv == Converter.ToStringConverter.INSTANCE)
            {
                return new ItemMappingIterator(@base, TO_STRING_MAPPER, true);
            }
            else
            {
                AtomicSequenceMappingFunction mapper = new AtomicSequenceMappingFunction();
                mapper.SetConverter(conv);
                if (roleSupplier != null)
                {
                    string errorCode = roleSupplier().ErrorCode;
                    if (!"XPTY0004".Equals(errorCode))
                    {
                        mapper.SetErrorCode(errorCode);
                    }
                }

                return new ItemMappingIterator(@base, mapper, true);
            }
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return (AtomicValue)MakeElaborator().ElaborateForItem().Eval(context);
        }

        public virtual AtomicValue ConvertItem(AtomicValue item, IXPathContext context)
        {
            if (item == null)
            {
                return null;
            }

            Converter conv = GetConverterDynamically(context);
            IConversionResult result = PhaseBConverters.Convert(conv, item);
            if (result is ValidationFailure && roleSupplier != null)
            {

                // TODO: use more of the information in the roleDiagnostic to form the error message
                ((ValidationFailure)result).SetErrorCode(roleSupplier().ErrorCode);
            }

            return result.AsAtomic();
        }

        public override ItemType GetItemType()
        {
            return requiredItemType;
        }

        /// <summary>
        /// Determine the static cardinality of the expression
        /// </summary>
        protected override int ComputeCardinality()
        {
            return BaseExpression.GetCardinality();
        }

        /// <summary>
        /// Determine the static cardinality of the expression
        /// </summary>
        public override string ToString()
        {
            return "convertTo_" + RequiredItemType.ToString() + "(" + BaseExpression.ToString() + ")";
        }

        public override bool Equals(object other)
        {
            return base.Equals(other) && requiredItemType.Equals(((AtomicSequenceConverter)other).requiredItemType);
        }

        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode() ^ requiredItemType.GetHashCode();
        }

        protected override string DisplayOperator(Configuration config)
        {
            return "convert";
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("convert", this);
            destination.EmitAttribute("from", AlphaCode.FromItemType(BaseExpression.GetItemType()));
            destination.EmitAttribute("to", AlphaCode.FromItemType(requiredItemType));
            string flags = "";
            if (converter.IsPromoter())
            {
                flags = "p";
            }

            if (converter is UnfailingConverter.DownCastingConverter)
            {
                flags = "d";
            }

            if (!(flags.Length == 0))
            {
                destination.EmitAttribute("flags", flags);
            }

            if (RoleSupplier != null)
            {
                destination.EmitAttribute("diag", RoleSupplier().Save());
            }

            if (converter.IsPromoter() && "JS".Equals(destination.GetOptions().target) && destination.GetOptions().targetVersion >= 2)
            {

                // See bug 6239. For backwards compatibility, output a cvUntyped instruction. This is no longer needed for SaxonJ
                // because the promoting converter does promotion and conversion from untypedAtomic in a single operation.
                destination.StartElement("cvUntyped");
                destination.EmitAttribute("to", AlphaCode.FromItemType(requiredItemType));
                if (RoleSupplier != null)
                {
                    destination.EmitAttribute("diag", RoleSupplier().Save());
                }

                BaseExpression.Export(destination);
                destination.EndElement();
            }
            else
            {
                BaseExpression.Export(destination);
            }

            destination.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new AtomicSequenceConverterElaborator();
        }

        private sealed class AnonymousConverter : Converter
        {

            private readonly AtomicSequenceConverter parent;
            public AnonymousConverter(AtomicSequenceConverter parent, ConversionRules rules) : base(rules)
            {
                this.parent = parent;
            }
            // override of the base Convert(object) — a same-name overload taking AtomicValue never
            // dispatched (Converter-typed callers hit the base null stub; match-276 NRE'd on the result).
            public override IConversionResult Convert(object value)
            {
                AtomicValue input = (AtomicValue)value;
                Converter converter = GetConversionRules().GetConverter(input.PrimitiveType, (IAtomicType)parent.requiredItemType);
                if (converter == null)
                {
                    return new ValidationFailure("Cannot convert value from " + input.PrimitiveType + " to " + parent.requiredItemType);
                }
                else
                {
                    return converter.Convert(input);
                }
            }
        }

        /// <summary>
        /// Mapping function wrapped around a converter
        /// </summary>
        internal sealed class AtomicSequenceMappingFunction : IItemMappingFunction
        {
            private Converter converter;
            private string errorCode;
            public void SetConverter(Converter converter)
            {
                this.converter = converter;
            }

            public void SetErrorCode(string code)
            {
                this.errorCode = code;
            }

            public IItem MapItem(IItem item) /* net472: no covariant returns -> declare IItem (was AtomicValue) for IItemMappingFunction.MapItem */
            {
                IConversionResult result = converter.Convert((AtomicValue)item);
                if (errorCode != null && result is ValidationFailure)
                {
                    ((ValidationFailure)result).SetErrorCode(errorCode);
                }

                if (result == null)
                {
                    return item;
                }
                return result.AsAtomic();
            }
            IItem IItemMappingFunction.MapItem(IItem arg0) => MapItem(arg0);
        }

        /// <summary>
        /// Mapping function that converts every item in a sequence to a string
        /// </summary>
        internal sealed class ToStringMappingFunction : IItemMappingFunction
        {
            public IItem MapItem(IItem item) /* net472: no covariant returns -> declare IItem (was StringValue) */
            {
                return new StringValue(item.UnicodeStringValue);
            }
            IItem IItemMappingFunction.MapItem(IItem arg0) => MapItem(arg0);
        }

        internal sealed class AtomicSequenceConverterElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                AtomicSequenceConverter expr = (AtomicSequenceConverter)GetExpression();
                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                IPullEvaluator generic = (context) =>
                {
                    ISequenceIterator @base = baseEval.Iterate(context);
                    return expr.GetConvertingIterator(context, @base);
                };
                // Fused string(child::NAME) sequence: skip the axis/atomize/convert iterator stack on a
                // Tiny untyped tree; off the fast path defer to the generic converting iterator.
                if (Elaboration.FusedChildAtomizer.Match(expr, out int fp))
                {
                    return (context) => Elaboration.FusedChildAtomizer.CanFuse(context)
                        ? new Elaboration.FusedChildAtomizer.ChildStringIterator((Trees.Tiny.TinyParentNodeImpl)context.GetContextItem(), fp)
                        : generic(context);
                }

                // General node-stream form: xs:string* from atomize(NODES) on an untyped source
                // (the as="xs:string*" coercion of any node sequence) — each node's string value
                // becomes the xs:string directly, skipping the untypedAtomic intermediate and the
                // converting-iterator layer. IsUntyped guards against typed-tree atomization, where
                // promotion to string is not value-preserving (or not allowed at all).
                if (BuiltInAtomicType.STRING.Equals(expr.RequiredItemType)
                    && expr.BaseExpression is Atomizer atomBase
                    && atomBase.IsUntyped()
                    && atomBase.BaseExpression.GetItemType() is Patterns.NodeTest)
                {
                    // Value-lane sort: when the nodes come from a sort whose only key is the atomized
                    // context item, convert BEFORE sorting — each node's key equals its converted value
                    // and equal keys keep input order in both forms, so the order is byte-identical
                    // while the sorter's array holds strings instead of retained node wrappers and the
                    // separate per-node key-atomization pass disappears.
                    if (atomBase.BaseExpression is Sorting.SortExpression se && se.HasSingleSelfAtomizedKey())
                    {
                        IPullEvaluator selEval = se.BaseExpression.MakeElaborator().ElaborateForPull();
                        return (context) => se.IterateSorted(
                            new Elaboration.FusedChildAtomizer.NodeToStringIterator(selEval.Iterate(context)), context);
                    }

                    IPullEvaluator nodesEval = atomBase.BaseExpression.MakeElaborator().ElaborateForPull();
                    return (context) => new Elaboration.FusedChildAtomizer.NodeToStringIterator(nodesEval.Iterate(context));
                }

                return generic;
            }

            public override IItemEvaluator ElaborateForItem()
            {
                AtomicSequenceConverter expr = (AtomicSequenceConverter)GetExpression();
                IItemEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForItem();
                IItemEvaluator generic = (context) =>
                {
                    AtomicValue @base = (AtomicValue)baseEval.Eval(context);
                    return expr.ConvertItem(@base, context);
                };
                // Fused string(child::NAME) head read (upper-case(X) etc.): direct TinyTree read.
                if (Elaboration.FusedChildAtomizer.Match(expr, out int fp))
                {
                    return (context) =>
                    {
                        Values.StringValue s = Elaboration.FusedChildAtomizer.ReadFirstChildString(context, fp);
                        return s != null ? (IItem)s : generic(context);
                    };
                }

                // General node form (singleton): xs:string from atomize(NODE) on an untyped source.
                if (BuiltInAtomicType.STRING.Equals(expr.RequiredItemType)
                    && expr.BaseExpression is Atomizer atomBase
                    && atomBase.IsUntyped()
                    && atomBase.BaseExpression.GetItemType() is Patterns.NodeTest)
                {
                    IItemEvaluator nodeEval = atomBase.BaseExpression.MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        IItem n = nodeEval.Eval(context);
                        return n == null ? null : (IItem)new Values.StringValue(((NodeInfo)n).UnicodeStringValue);
                    };
                }

                return generic;
            }
        }
    }
}

