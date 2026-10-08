////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class HomogeneityChecker : UnaryExpression
    {

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string ExpressionName => "homCheck";
        public HomogeneityChecker(Expression @base) : base(@base)
        {
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.INSPECT;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            if (BaseExpression is HomogeneityChecker)
            {
                return BaseExpression.TypeCheck(visitor, contextInfo);
            }

            GetOperand().TypeCheck(visitor, contextInfo);
            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            Types.ItemType type = BaseExpression.GetItemType();
            if (type.Equals(ErrorType.GetInstance()))
            {
                return Literal.MakeEmptySequence();
            }

            Affinity rel = th.Relationship(type, AnyNodeTest.GetInstance());
            if (rel == Affinity.DISJOINT)
            {

                // expression cannot return nodes, so this checker is redundant
                // code deleted by bug 4298
                //            if (getBaseExpression() instanceof SlashExpression && ((SlashExpression) getBaseExpression()).getLeadingSteps() instanceof SlashExpression &&
                //                ExpressionTool.copyLocationInfo(this, se);
                //                return se;
                //            } else {
                return BaseExpression; //            }
            }
            else if (rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMED_BY)
            {

                // expression always returns nodes, so replace this expression with a DocumentSorter
                Expression savedBase = BaseExpression;
                Expression parent = ParentExpression;
                GetOperand().DetachChild();
                DocumentSorter ds = new DocumentSorter(savedBase);
                ExpressionTool.CopyLocationInfo(this, ds);
                ds.ParentExpression = parent;

                return ds;
            }

            return this;
        }

        public override Patterns.Pattern ToPattern(Configuration config)
        {
            return BaseExpression.ToPattern(config);
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            if (BaseExpression is HomogeneityChecker)
            {
                return BaseExpression.Optimize(visitor, contextInfo);
            }

            return base.Optimize(visitor, contextInfo);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            HomogeneityChecker hc = new HomogeneityChecker(BaseExpression.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, hc);
            return hc;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {

            // This class delivers the result of the path expression in unsorted order,
            // without removal of duplicates. If sorting and deduplication are needed,
            // this is achieved by wrapping the path expression in a DocumentSorter
            ISequenceIterator @base = BaseExpression.Iterate(context);
            return new HomogeneityCheckerIterator(@base, GetLocation());
        }

        public override Elaborator GetElaborator()
        {
            return new HomogeneityCheckerElaborator();
        }

        internal sealed class HomogeneityCheckerElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                HomogeneityChecker exp = (HomogeneityChecker)GetExpression();
                ILocation location = exp.GetLocation();
                Expression arg = exp.BaseExpression;
                IPullEvaluator argEval = arg.MakeElaborator().ElaborateForPull();
                return (context) => new HomogeneityCheckerIterator(argEval.Iterate(context), location);
            }
        }
    }
}