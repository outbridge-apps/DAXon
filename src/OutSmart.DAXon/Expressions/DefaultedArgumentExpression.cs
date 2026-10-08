////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Tracing;
using System;
namespace OutSmart.DAXon.Expressions
{
    internal class DefaultedArgumentExpression : PseudoExpression
    {

        public override string ExpressionName => "defaultValue";
        public DefaultedArgumentExpression()
        {
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            return this;
        }

        public override void Export(ExpressionPresenter destination)
        {
            throw new NotSupportedException();
        }

        public override Elaborator GetElaborator()
        {
            throw new NotSupportedException();
        }

        internal sealed class DefaultCollationArgument : DefaultedArgumentExpression
        {
            public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
            {
                return new StringLiteral(visitor.StaticContext.GetDefaultCollationName());
            }
        }
    }
}