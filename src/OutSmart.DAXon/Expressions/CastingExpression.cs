////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Casting Expression: abstract superclass for "cast as X" and "castable as X", which share a good deal of logic
    /// </summary>
    internal abstract class CastingExpression : UnaryExpression
    {
        private IAtomicType targetType;
        private readonly IAtomicType targetPrimitiveType;
        private bool allowEmpty = false;
        protected Converter converter;
        private bool operandIsStringLiteral = false;

        public virtual IAtomicType TargetPrimitiveType => targetPrimitiveType;

        public virtual IAtomicType TargetType
        {
            get => targetType; set
            {
                targetType = value;
            }
        }

        public override int IntrinsicDependencies => TargetType.IsNamespaceSensitive() ? StaticProperty.DEPENDS_ON_STATIC_CONTEXT : 0;
        public CastingExpression(Expression source, IAtomicType target, bool allowEmpty) : base(source)
        {
            this.allowEmpty = allowEmpty;
            targetType = target;

            // Cast needed for C#
            targetPrimitiveType = (IAtomicType)target.GetPrimitiveItemType();
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SINGLE_ATOMIC;
        }

        public virtual void SetAllowEmpty(bool allow)
        {
            allowEmpty = allow;
        }

        public virtual bool AllowsEmpty()
        {
            return allowEmpty;
        }

        public virtual void SetOperandIsStringLiteral(bool option)
        {
            operandIsStringLiteral = option;
        }

        public virtual bool IsOperandIsStringLiteral()
        {
            return operandIsStringLiteral;
        }

        public virtual Converter GetConverter()
        {
            return converter;
        }

        public virtual INamespaceResolver GetNamespaceResolver()
        {
            return GetRetainedStaticContext();
        }

        public override Expression Simplify()
        {
            if (targetType is BuiltInAtomicType)
            {
                string s = XPathParser.WhyDisallowedType(GetPackageData(), (BuiltInAtomicType)targetType);
                if (s != null)
                {

                    // this is checked here because the ConstructorFunctionLibrary doesn't have access to the static
                    // context at bind time
                    XPathException err = new XPathException(s, "XPST0080", this.GetLocation());
                    err.SetIsStaticError(true);
                    throw err;
                }
            }

            BaseExpression = BaseExpression.Simplify();
            return this;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            return p | StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        protected virtual void Export(ExpressionPresenter @out, string elemName)
        {
            @out.StartElement(elemName, this);
            int card = AllowsEmpty() ? StaticProperty.ALLOWS_ZERO_OR_ONE : StaticProperty.EXACTLY_ONE;
            SequenceType st = SequenceType.MakeSequenceType(TargetType, card);
            @out.EmitAttribute("flags", "a" + (AllowsEmpty() ? "e" : ""));
            @out.EmitAttribute("as", st.ToAlphaCode());
            BaseExpression.Export(@out);
            @out.EndElement();
        }
    }
}