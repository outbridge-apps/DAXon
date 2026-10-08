////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the fn:doc() function - a simplified form of the IDocument function
    /// </summary>
    internal sealed class Doc : SystemFunction, ICallable
    {
        private readonly ParseOptions parseOptions;
        public ParseOptions GetParseOptions()
        {
            return parseOptions;
        }

        public override int GetCardinality(Expression[] arguments)
        {
            return arguments[0].GetCardinality() & ~StaticProperty.ALLOWS_MANY;
        }

        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            Expression expr = MaybePreEvaluate(this, arguments);
            return expr == null ? base.MakeFunctionCall(arguments) : expr;
        }

        public static Expression MaybePreEvaluate(SystemFunction sf, Expression[] arguments)
        {
            if (arguments.Length > 1 || !sf.GetRetainedStaticContext().GetConfiguration().GetBooleanProperty(Feature<bool>.PRE_EVALUATE_DOC_FUNCTION))
            {
                sf.Details.properties = sf.Details.properties | BuiltInFunctionSet.LATE;
                return null;
            }
            else
            {

                // allow early evaluation
                return new AnonymousSystemFunctionCall(sf, arguments);
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            AtomicValue hrefVal = (AtomicValue)arguments[0].Head();
            if (hrefVal == null)
            {
                return EmptySequence.GetInstance();
            }

            string href = hrefVal.GetStringValue();
            PackageData packageData = GetRetainedStaticContext().GetPackageData();
            NodeInfo item = DocumentFn.MakeDoc(href, GetRetainedStaticContext().StaticBaseUriString, packageData, GetParseOptions(), context, null);
            if (item == null)
            {

                // we failed to read the document
                throw new XPathException("Failed to load document " + href, "FODC0002", context);
            }

            Controller controller = context.GetController();
            if (parseOptions != null && controller is XsltController)
            {
                ((XsltController)controller).GetAccumulatorManager().SetApplicableAccumulators(item.GetTreeInfo(), parseOptions.ApplicableAccumulators);
            }

            return item;
        }

        public override int GetSpecialProperties(Expression[] arguments)
        {
            return StaticProperty.ORDERED_NODESET | StaticProperty.PEER_NODESET | StaticProperty.NO_NODES_NEWLY_CREATED | StaticProperty.SINGLE_DOCUMENT_NODESET; // Declaring it as a peer node-set expression avoids sorting of expressions such as
            // doc(XXX)/a/b/c
            // The doc() function might appear to be creative: but it isn't, because multiple calls
            // with the same arguments will produce identical results.
        }

        private sealed class AnonymousSystemFunctionCall : SystemFunctionCall
        {

            private readonly SystemFunction sf;
            public AnonymousSystemFunctionCall(SystemFunction sf, Expression[] arguments) : base(sf, arguments)
            {
                this.sf = sf;
            }
            public override Expression PreEvaluate(ExpressionVisitor visitor)
            {
                Configuration config = visitor.GetConfiguration();
                try
                {
                    IGroundedValue firstArg = ((Literal)this.GetArg(0)).GroundedValue;
                    if (firstArg.GetLength() == 0)
                    {
                        return null;
                    }
                    else if (firstArg.GetLength() > 1)
                    {
                        return this;
                    }

                    string href = firstArg.Head().GetStringValue();
                    if (href.IndexOf('#') >= 0)
                    {
                        return this;
                    }

                    NodeInfo item = DocumentFn.PreLoadDoc(href, sf.StaticBaseUriString, sf.GetRetainedStaticContext().GetPackageData(), config, GetLocation());
                    if (item != null)
                    {
                        Expression constant = Literal.MakeLiteral(item);
                        ExpressionTool.CopyLocationInfo(this.GetArg(0), constant);
                        return constant;
                    }
                }
                catch (Exception)
                {

                    // ignore the exception and try again at run-time
                    return this;
                }

                return this;
            }

            public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
            {
                OptimizeChildren(visitor, contextItemType);
                if (GetArg(0) is StringLiteral)
                {
                    return PreEvaluate(visitor);
                }

                return this;
            }
        }
    }
}
