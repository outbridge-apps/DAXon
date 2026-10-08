////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Expressions.Instructions;
namespace OutSmart.DAXon.Tracing
{
    internal sealed class XSLTTraceCodeInjector : TraceCodeInjector
    {
        protected override bool IsApplicable(Expression exp)
        {
            if (traceLevel == TraceLevel.HIGH)
            {
                return !(exp is TraceExpression || exp is OnEmptyExpr || exp is OnNonEmptyExpr);
            }

            return IsTraceableExpression(exp);
        }

        /// <summary>
        /// Decide whether a particular expression should be traced when tracing XSLT stylesheet execution.
        /// </summary>
        public static bool IsTraceableExpression(Expression exp)
        {

            // Never trace an empty sequence (it's often an empty sequence constructor, for example <lre/>
            if (Literal.IsEmptySequence(exp))
            {
                return false;
            }


            // Don't trace an on-empty or on-non-empty expression (bug #6428)
            if (exp is OnEmptyExpr || exp is OnNonEmptyExpr)
            {
                return false;
            }


            // Don't trace an expression if its parent is an XPath expression (as distinct from an XSLT instruction)
            Expression parent = exp.ParentExpression;
            if (parent is TraceExpression)
            {
                parent = parent.ParentExpression;
            }

            if (exp.IsCallOn(typeof(TransformFn)))
            {
                return true;
            }

            if (exp.IsInstruction())
            {
                return true;
            }

            if (parent != null && parent.GetLocation() is XPathParser.NestedLocation)
            {
                return false;
            }


            // Do trace an expression if it's the direct content of an `xsl:sequence` instruction (which doesn't
            // actually appear on the expression tree in its own right)
            ILocation loc = exp.GetLocation();
            if (loc is XPathParser.NestedLocation)
            {
                loc = ((XPathParser.NestedLocation)loc).GetContainingLocation();
            }

            if (loc is AttributeLocation)
            {
                StructuredQName elementName = ((AttributeLocation)loc).ElementName;
                return elementName.HasURI(NamespaceUri.XSLT) && (elementName.GetLocalPart().Equals("sequence"));
            }


            // Otherwise trace the expression if it has a known location which differs from the parent expression
            // except in the case where the parent expression is a sequence constructor (`Block`)
            // or a type-checking instruction
            return loc != null && loc.GetLineNumber() != -1 && !(parent != null && loc == parent.GetLocation() && !(parent is Block || parent is ComponentTracer || parent is ItemChecker || parent is CardinalityChecker));
        }
    }
}