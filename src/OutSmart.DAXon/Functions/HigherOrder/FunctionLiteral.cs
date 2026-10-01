////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    internal sealed class FunctionLiteral : Literal
    {

        public override IGroundedValue GroundedValue => (IFunctionItem)base.GroundedValue;

        public override string ExpressionName => "namedFunctionRef";
        public FunctionLiteral(IFunctionItem value) : base(value)
        {
        }

        public override Expression Simplify()
        {
            if (GroundedValue is AbstractFunction)
            {
                ((AbstractFunction)GroundedValue).Simplify();
            }

            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            if (GroundedValue is AbstractFunction)
            {
                ((AbstractFunction)GroundedValue).TypeCheck(visitor, contextInfo);
            }

            return this;
        }

        public override ItemType GetItemType()
        {
            return ((IFunctionItem)GroundedValue).FunctionItemType;
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        protected override int ComputeSpecialProperties()
        {
            return StaticProperty.NO_NODES_NEWLY_CREATED | StaticProperty.COMPUTED_FUNCTION;
        }

        public override bool IsVacuousExpression()
        {
            return false;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            FunctionLiteral fl2 = new FunctionLiteral((IFunctionItem)GroundedValue);
            ExpressionTool.CopyLocationInfo(this, fl2);
            return fl2;
        }

        public override void SetRetainedStaticContext(RetainedStaticContext rsc)
        {
            base.SetRetainedStaticContext(rsc);
        }

        public override bool Equals(object obj)
        {
            return obj is FunctionLiteral && ((FunctionLiteral)obj).GroundedValue == GroundedValue;
        }

        protected override int ComputeHashCode()
        {
            return GroundedValue.GetHashCode();
        }

        public override void Export(ExpressionPresenter @out)
        {
            IFunctionItem f = (IFunctionItem)GroundedValue;
            if (f is UserFunction)
            {
                new UserFunctionReference((UserFunction)f).Export(@out);
            }
            else
            {
                f.Export(@out);
            }
        }
    }
}
