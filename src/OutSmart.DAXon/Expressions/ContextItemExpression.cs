////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal class ContextItemExpression : Expression
    {
        private ContextItemStaticInfo staticInfo = ContextItemStaticInfo.DEFAULT;
        private string errorCodeForAbsentContext = "XPDY0002";
        private bool absentContextIsTypeError = false; // absurdly, but that's what the spec says

        public override string ExpressionName => "dot";

        public virtual string ErrorCodeForUndefinedContext => errorCodeForAbsentContext;

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_CONTEXT_ITEM;

        public override int NetCost => 0;

        public override string StreamerName => "ContextItemExpr";
        public ContextItemExpression()
        {
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ContextItemExpression cie2 = new ContextItemExpression();
            cie2.staticInfo = staticInfo;
            cie2.SetErrorCodeForUndefinedContext(errorCodeForAbsentContext, false);
            ExpressionTool.CopyLocationInfo(this, cie2);
            return cie2;
        }

        public virtual void SetErrorCodeForUndefinedContext(string errorCode, bool isTypeError)
        {
            errorCodeForAbsentContext = errorCode;
            absentContextIsTypeError = isTypeError;
        }

        public virtual void SetStaticInfo(ContextItemStaticInfo info)
        {
            staticInfo = info;
        }

        /// <summary>
        /// Type-check the expression.
        /// </summary>
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            if (contextInfo.GetItemType() == ErrorType.GetInstance())
            {
                visitor.IssueWarning("Evaluation will always fail: there is no context item", DAXonErrorCode.SXWN9027, GetLocation());
                ErrorExpression ee = new ErrorExpression("There is no context item", ErrorCodeForUndefinedContext, absentContextIsTypeError);
                ee.SetOriginalExpression(this);
                ExpressionTool.CopyLocationInfo(this, ee);
                return ee;
            }
            else
            {
                staticInfo = contextInfo;
            }

            return this;
        }

        /// <summary>
        /// Type-check the expression.
        /// </summary>
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {

            // In XSLT, we don't catch this error at the typeCheck() phase because it's done one XPath expression
            // at a time. So we repeat the check here.
            if (contextItemType == null)
            {
                throw new XPathException("The context item is undefined at this point").WithErrorCode(ErrorCodeForUndefinedContext).WithLocation(GetLocation()).AsTypeErrorIf(absentContextIsTypeError);
            }

            return this;
        }

        public override ItemType GetItemType()
        {
            return staticInfo.GetItemType();
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return contextItemType;
        }

        public virtual bool IsContextPossiblyUndefined()
        {
            return staticInfo.IsPossiblyAbsent();
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            return p | StaticProperty.NO_NODES_NEWLY_CREATED | StaticProperty.CONTEXT_DOCUMENT_NODESET;
        }

        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        public override bool Equals(object other)
        {
            return other is ContextItemExpression;
        }

        protected override int ComputeHashCode()
        {
            return "ContextItemExpression".GetHashCode();
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            if (pathMapNodeSet == null)
            {
                pathMapNodeSet = new PathMap.PathMapNodeSet(pathMap.MakeNewRoot(this));
            }

            return pathMapNodeSet;
        }

        public override bool IsSubtreeExpression()
        {
            return true;
        }

        public override Patterns.Pattern ToPattern(Configuration config)
        {
            return AnchorPattern.GetInstance();
        }

        /// <summary>
        /// Iterate over the value of the expression
        /// </summary>
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            IItem item = context.GetContextItem();
            if (item == null)
            {
                ReportAbsentContext(context);
            }

            return SingletonIterator.MakeIterator(item);
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            IItem item = context.GetContextItem();
            if (item == null)
            {
                ReportAbsentContext(context);
            }

            return item;
        }

        public virtual void ReportAbsentContext(IXPathContext context)
        {
            if (absentContextIsTypeError)
            {
                TypeError("The context item is absent", ErrorCodeForUndefinedContext, context);
            }
            else
            {
                DynamicError("The context item is absent", ErrorCodeForUndefinedContext, context);
            }
        }

        public override string ToString()
        {
            return ".";
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("dot", this);
            ItemType type = GetItemType();
            if (!(type == AnyItemType.GetInstance()))
            {
                SequenceType st = SequenceType.MakeSequenceType(type, StaticProperty.EXACTLY_ONE);
                destination.EmitAttribute("type", st.ToAlphaCode());
            }

            if (staticInfo.IsPossiblyAbsent())
            {
                destination.EmitAttribute("flags", "a");
            }

            destination.EndElement();
        }

        public override string ToShortString()
        {
            return ".";
        }

        public override Elaborator GetElaborator()
        {
            return new ContextItemElaborator();
        }

        /// <summary>
        /// Elaborator for the context item expression, "dot".
        /// </summary>
        internal sealed class ContextItemElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                ContextItemExpression cie = (ContextItemExpression)GetExpression();
                if (cie.IsContextPossiblyUndefined())
                {
                    return (context) =>
                    {
                        IItem current = context.GetContextItem();
                        if (current == null)
                        {
                            cie.ReportAbsentContext(context);
                        }

                        return current;
                    };
                }
                else
                {
                    return (context) => context.GetContextItem();
                }
            }

            public override IPullEvaluator ElaborateForPull()
            {
                ContextItemExpression cie = (ContextItemExpression)GetExpression();
                if (cie.IsContextPossiblyUndefined())
                {
                    return (context) =>
                    {
                        IItem current = context.GetContextItem();
                        if (current == null)
                        {
                            cie.ReportAbsentContext(context);
                        }

                        return SingletonIterator.MakeIterator(current);
                    };
                }
                else
                {
                    return (context) => SingletonIterator.MakeIterator(context.GetContextItem());
                }
            }
        }
    }
}
