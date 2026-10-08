////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    internal sealed class FunctionSequenceCoercer : UnaryExpression
    {
        private readonly SpecificFunctionType requiredItemType;
        private readonly Func<RoleDiagnostic> roleSupplier;
        private readonly bool allow40;

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string ExpressionName => "fnCoercer";
        public FunctionSequenceCoercer(Expression sequence, SpecificFunctionType requiredItemType, Func<RoleDiagnostic> role, bool allow40) : base(sequence)
        {
            this.requiredItemType = requiredItemType;
            this.roleSupplier = role;
            this.allow40 = allow40;
            ExpressionTool.CopyLocationInfo(sequence, this);
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.INSPECT;
        }

        public override Expression Simplify()
        {
            try
            {
                BaseExpression = BaseExpression.Simplify();
                if (BaseExpression is Literal)
                {
                    IGroundedValue val = SequenceTool.ToGroundedValue(Iterate(new EarlyEvaluationContext(GetConfiguration())));
                    return Literal.MakeLiteral(val, this);
                }

                return this;
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            if (th.IsSubType(BaseExpression.GetItemType(), requiredItemType))
            {
                return BaseExpression;
            }
            else
            {
                return this;
            }
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            return p | StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            FunctionSequenceCoercer fsc2 = new FunctionSequenceCoercer(BaseExpression.Copy(rebindings), requiredItemType, roleSupplier, allow40);
            ExpressionTool.CopyLocationInfo(this, fsc2);
            return fsc2;
        }

        /// <summary>
        /// Iterate over the sequence of functions, wrapping each one in a CoercedFunction object
        /// </summary>
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return (IFunctionItem)MakeElaborator().ElaborateForItem().Eval(context);
        }

        public override Types.ItemType GetItemType()
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

        public override bool Equals(object other)
        {
            return base.Equals(other) && requiredItemType.Equals(((FunctionSequenceCoercer)other).requiredItemType);
        }

        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode() ^ requiredItemType.GetHashCode();
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("fnCoercer", this);
            Values.SequenceType st = Values.SequenceType.MakeSequenceType(requiredItemType, StaticProperty.EXACTLY_ONE);
            destination.EmitAttribute("to", st.ToAlphaCode());
            destination.EmitAttribute("diag", roleSupplier().Save());
            if (allow40)
            {
                destination.EmitAttribute("flags", "4");
            }

            BaseExpression.Export(destination);
            destination.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new FunctionSequenceCoercerElaborator();
        }

        private static void CheckAnnotations(IFunctionItem item, IFunctionItemType requiredItemType, Configuration config)
        {
            foreach (Annotation ann in requiredItemType.AnnotationAssertions)
            {
                IFunctionAnnotationHandler handler = config.GetFunctionAnnotationHandler(ann.AnnotationQName.GetNamespaceUri());
                if (handler != null && !handler.SatisfiesAssertion(ann, item.GetAnnotations()))
                {
                    throw new XPathException("Supplied function does not satisfy the annotation assertions of the required function type", "XPTY0004");
                }
            }
        }

        internal sealed class Coercer : IItemMappingFunction
        {
            private readonly SpecificFunctionType requiredItemType;
            private readonly Configuration config;
            private readonly ILocation locator;
            private readonly bool allow40;
            public Coercer(SpecificFunctionType requiredItemType, Configuration config, ILocation locator, bool allow40)
            {
                this.requiredItemType = requiredItemType;
                this.config = config;
                this.locator = locator;
                this.allow40 = allow40;
            }

            public IItem MapItem(IItem item) /*Java covariant IFunctionItem widened (C# 7.3)*/
            {
                if (!(item is IFunctionItem))
                {
                    throw new XPathException("Function coercion attempted on an item (" + item.ToShortString() + ") which is not a function", "XPTY0004", locator);
                }

                try
                {
                    CheckAnnotations((IFunctionItem)item, requiredItemType, config);
                    return new CoercedFunction((IFunctionItem)item, requiredItemType, allow40);
                }
                catch (XPathException err)
                {
                    throw err.MaybeWithLocation(locator);
                }
            }
            IItem IItemMappingFunction.MapItem(IItem arg0) => MapItem(arg0); // covariant bridge
        }

        private sealed class FunctionSequenceCoercerElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                FunctionSequenceCoercer expr = (FunctionSequenceCoercer)GetExpression();
                IPullEvaluator @base = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                Coercer coercer = new Coercer(expr.requiredItemType, expr.GetConfiguration(), expr.GetLocation(), expr.allow40);
                return (context) => new ItemMappingIterator(@base.Iterate(context), coercer, true);
            }

            public override IItemEvaluator ElaborateForItem()
            {
                FunctionSequenceCoercer expr = (FunctionSequenceCoercer)GetExpression();
                IItemEvaluator @base = expr.BaseExpression.MakeElaborator().ElaborateForItem();
                Coercer coercer = new Coercer(expr.requiredItemType, expr.GetConfiguration(), expr.GetLocation(), expr.allow40);
                return (context) =>
                {
                    IItem item = @base.Eval(context);
                    if (item == null)
                    {
                        return null;
                    }

                    return coercer.MapItem(item);
                };
            }
        }
    }
}

