////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// A lookup expression is an expression of the form A?*, where A must be a map or an array
    /// </summary>
    internal sealed class LookupAllExpression : UnaryExpression
    {

        public override double Cost => BaseExpression.Cost + 1;

        public override int ImplementationMethod => ITERATE_METHOD;
        public LookupAllExpression(Expression lhs) : base(lhs)
        {
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.INSPECT;
        }

        public override ItemType GetItemType()
        {
            ItemType lhs = BaseExpression.GetItemType();
            if (lhs is MapType)
            {
                return ((MapType)lhs).ValueType.PrimaryType;
            }
            else if (lhs is ArrayItemType)
            {
                return ((ArrayItemType)lhs).MemberType.PrimaryType;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return GetItemType().GetUType();
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();

            // Check the first operand
            GetOperand().TypeCheck(visitor, contextInfo);
            ItemType containerType = BaseExpression.GetItemType();
            bool isArrayLookup = containerType is ArrayItemType;
            bool isMapLookup = containerType is MapType || containerType is IRecordType;
            if (!isArrayLookup && !isMapLookup)
            {
                if (th.Relationship(containerType, MapType.ANY_MAP_TYPE) == Affinity.DISJOINT && th.Relationship(containerType, ArrayItemType.GetInstance()) == Affinity.DISJOINT)
                {
                    if (Cardinality.AllowsZero(BaseExpression.GetCardinality()))
                    {
                        visitor.IssueWarning("The left-hand operand of '?' must be a map or an array; the expression can succeed only if the operand is an empty sequence " + containerType, DAXonErrorCode.SXWN9026, GetLocation());
                    }
                    else
                    {
                        throw new XPathException("The left-hand operand of '?' must be a map or an array; " + "the supplied expression is of type " + containerType, "XPTY0004").WithLocation(GetLocation()).AsTypeError().WithFailingExpression(this);
                    }
                }
            }

            if (BaseExpression is Literal)
            {
                try
                {
                    return new Literal(SequenceTool.ToGroundedValue(Iterate(visitor.MakeDynamicContext())));
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            GetOperand().Optimize(visitor, contextItemType);
            if (BaseExpression is Literal)
            {
                try
                {
                    return new Literal(SequenceTool.ToGroundedValue(Iterate(visitor.MakeDynamicContext())));
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }


            // See W3C bug 30228. In the interests of keeping certain tests streamable, we do a rewrite of [A,B,C]?*
            // to (A, B, C).
            if (BaseExpression is SquareArrayConstructor)
            {
                IList<Expression> children = new List<Expression>();
                foreach (Operand o in BaseExpression.Operands())
                {
                    children.Add(o.GetChildExpression().Copy(new RebindingMap()));
                }

                Expression[] childExpressions = children.ToArray();
                Block block = new Block(childExpressions);
                ExpressionTool.CopyLocationInfo(this, block);
                return block;
            }

            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            return new LookupAllExpression(BaseExpression.Copy(rebindings));
        }

        /// <summary>
        /// Determine the static cardinality of the expression
        /// </summary>
        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        public override bool Equals(object other)
        {
            if (!(other is LookupAllExpression))
            {
                return false;
            }

            LookupAllExpression p = (LookupAllExpression)other;
            return BaseExpression.IsEqual(p.BaseExpression);
        }

        protected override int ComputeHashCode()
        {
            return "LookupAll".GetHashCode() ^ BaseExpression.GetHashCode();
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return new LookupAllIterator(this, BaseExpression.Iterate(context));
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("lookupAll", this);
            BaseExpression.Export(destination);
            destination.EndElement();
        }

        public override string ToString()
        {
            return ExpressionTool.Parenthesize(BaseExpression) + "?*";
        }

        public override string ToShortString()
        {
            return BaseExpression.ToShortString() + "?*";
        }

        public override Elaborator GetElaborator()
        {
            return new LookupAllElaborator();
        }

        internal sealed class LookupAllElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                LookupAllExpression expr = (LookupAllExpression)GetExpression();
                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) => new LookupAllIterator(expr, baseEval.Iterate(context));
            }
        }

        private sealed class LookupAllIterator : ISequenceIterator
        {
            readonly LookupAllExpression expr;
            readonly ISequenceIterator level0;
            IEnumerator<IGroundedValue> level1forArrays;
            IEnumerator<OutSmart.DAXon.Values.Maps.KeyValuePair> level1forMaps;
            ISequenceIterator level2;
            public LookupAllIterator(LookupAllExpression expr, ISequenceIterator baseIterator)
            {
                level0 = baseIterator;
                level1forArrays = null;
                level1forMaps = null;
                level2 = null;
                this.expr = expr;
            }

            public IItem Next()
            {
                // A loop, not a call per entry: a map or array of empty values once cost a stack frame each.
                while (true)
                {
                    if (level2 != null)
                    {
                        IItem nextItem = level2.Next();
                        if (nextItem != null)
                        {
                            return nextItem;
                        }

                        level2 = null;
                    }
                    else if (level1forArrays != null)
                    {
                        if (level1forArrays.MoveNext())
                        {
                            level2 = level1forArrays.Current.Iterate();
                        }
                        else
                        {
                            level1forArrays = null;
                        }
                    }
                    else if (level1forMaps != null)
                    {
                        if (level1forMaps.MoveNext())
                        {
                            level2 = level1forMaps.Current.value.Iterate();
                        }
                        else
                        {
                            level1forMaps = null;
                        }
                    }
                    else
                    {
                        IItem lhs = level0.Next();
                        if (lhs == null)
                        {
                            return null;
                        }
                        else if (lhs is ArrayItem array)
                        {
                            level1forArrays = array.Members().GetEnumerator();
                        }
                        else if (lhs is MapItem map)
                        {
                            level1forMaps = map.KeyValuePairs().GetEnumerator();
                        }
                        else
                        {
                            try
                            {
                                LookupExpression.MustBeArrayOrMap(expr, lhs);
                            }
                            catch (XPathException e)
                            {
                                throw new UncheckedXPathException(e);
                            }

                            return null;
                        }
                    }
                }
            }

            public void Dispose()
            {
                if (level0 != null)
                {
                    level0.Dispose();
                }

                if (level2 != null)
                {
                    level2.Dispose();
                }
            }
        }
    }
}
