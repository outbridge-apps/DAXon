////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class ErrorExpression : Expression
    {
        private readonly IXmlProcessingError exception;
        private Expression original;

        public string ErrorCodeLocalPart => exception.GetErrorCode().LocalName;

        public override int ImplementationMethod => EVALUATE_METHOD | ITERATE_METHOD;

        public override string ExpressionName => "errorExpr";
        public ErrorExpression() : this("Unspecified error", "XXXX9999", false)
        {
        }

        public ErrorExpression(string message, string errorCode, bool isTypeError) : this(new XmlProcessingIncident(message, errorCode))
        {
            ((XmlProcessingIncident)exception).SetTypeError(isTypeError);
        }

        public ErrorExpression(IXmlProcessingError exception)
        {
            this.exception = exception;
        }

        public IXmlProcessingError GetException()
        {
            return exception;
        }

        public bool IsTypeError()
        {
            return exception.IsTypeError();
        }

        public string GetMessage()
        {
            return exception.GetMessage();
        }

        public void SetOriginalExpression(Expression original)
        {
            this.original = original;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            return this;
        }

        public override IItem EvaluateItem(IXPathContext context)
        {

            // copy the exception for thread-safety, because we want to add context information
            XPathException err = new XPathException(exception.GetMessage()).WithLocation(exception.GetLocation()).MaybeWithLocation(GetLocation()).MaybeWithContext(context).AsTypeErrorIf(exception.IsTypeError());
            if (exception.GetErrorCode() != null)
            {
                err.ErrorCodeQName = exception.GetErrorCode().GetStructuredQName();
            }

            throw err;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            EvaluateItem(context);
            return null; // to fool the compiler
        }

        public override Types.ItemType GetItemType()
        {
            return AnyItemType.GetInstance();
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_MORE; // we return a liberal value, so that we never get a type error reported
            // statically
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ErrorExpression e2 = new ErrorExpression(exception);
            e2.SetOriginalExpression(original);
            ExpressionTool.CopyLocationInfo(this, e2);
            return e2;
        }

        public override string ToString()
        {
            if (original != null)
            {
                return original.ToString();
            }
            else
            {
                return "error(\"" + GetMessage() + "\")";
            }
        }

        public override string ToShortString()
        {
            if (original != null)
            {
                return original.ToShortString();
            }
            else
            {
                return "error(\"" + GetMessage() + "\")";
            }
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("error", this);
            destination.EmitAttribute("message", exception.GetMessage());
            destination.EmitAttribute("code", exception.GetErrorCode().LocalName);
            destination.EmitAttribute("isTypeErr", exception.IsTypeError() ? "0" : "1");
            destination.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new ErrorExpressionElaborator();
        }

        // An ErrorExpression throws when its pull ITERATOR IS CONSTRUCTED, i.e. inside
        // LazyPullEvaluator.Evaluate at argument-evaluation time — before the function body runs
        // and regardless of whether the argument is ever consumed. Fused eager-item call paths
        // cannot reproduce that timing, so they gate on this test and fall back to the generic
        // elaborator when a (constant-folded) error sits anywhere in an argument subtree.
        internal static bool IsContainedIn(Expression e)
        {
            if (e is ErrorExpression)
            {
                return true;
            }

            foreach (Operand o in e.Operands())
            {
                if (IsContainedIn(o.GetChildExpression()))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ErrorExpressionElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {

                // copy the exception for thread-safety, because we want to add context information
                ErrorExpression expr = (ErrorExpression)GetExpression();
                IXmlProcessingError exception = expr.GetException();
                return (context) =>
                {
                    XPathException err = new XPathException(exception.GetMessage()).WithLocation(exception.GetLocation()).MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context).AsTypeErrorIf(exception.IsTypeError());
                    if (exception.GetErrorCode() != null)
                    {
                        err.ErrorCodeQName = exception.GetErrorCode().GetStructuredQName();
                    }

                    throw err;
                };
            }
        }
    }
}
