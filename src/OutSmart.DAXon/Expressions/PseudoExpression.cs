////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2013-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;

using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
namespace OutSmart.DAXon.Expressions
{
    public abstract class PseudoExpression : Expression
    {

        public override int ImplementationMethod => 0;
        // net472 has no covariant returns: Pattern re-declares TypeCheck/Optimize/Simplify with a
        // Pattern return type, which HIDES (not overrides) the Expression-typed virtuals. A generic
        // tree walk through an Expression reference (Operand.TypeCheck during a containing function's
        // body type-check) then bypassed the pattern's own logic — e.g. a predicate was re-typechecked
        // with the function's ABSENT context info, turning '.' into ErrorExpression (number-0202).
        // These overrides route the Expression-typed virtuals through covariant hooks the subclass fills.
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return TypeCheckCovariant(visitor, contextInfo);
        }

        protected virtual Expression TypeCheckCovariant(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return base.TypeCheck(visitor, contextInfo);
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return OptimizeCovariant(visitor, contextInfo);
        }

        protected virtual Expression OptimizeCovariant(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return base.Optimize(visitor, contextInfo);
        }

        public override Expression Simplify()
        {
            return SimplifyCovariant();
        }

        protected virtual Expression SimplifyCovariant()
        {
            return base.Simplify();
        }

        private void CannotEvaluate()
        {
            throw new XPathException("Cannot evaluate " + GetType().FullName);
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        public override ItemType GetItemType()
        {
            return AnyItemType.GetInstance();
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            CannotEvaluate();
            return null;
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            CannotEvaluate();
            return false;
        }

        public override UnicodeString EvaluateAsString(IXPathContext context)
        {
            CannotEvaluate();
            return EmptyUnicodeString.GetInstance();
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            CannotEvaluate();
            return null;
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            CannotEvaluate();
        }

        public override Elaborator GetElaborator()
        {
            throw new NotSupportedException();
        }
    }
}
