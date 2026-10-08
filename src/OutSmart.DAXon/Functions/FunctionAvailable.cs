////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the XSLT function-available functions.
    /// </summary>
    internal sealed class FunctionAvailable : SystemFunction
    {
        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            PackageData pack = GetRetainedStaticContext().GetPackageData();
            if (pack is StylesheetPackage)
            {
                ((StylesheetPackage)pack).SetRetainUnusedFunctions();
            }

            return base.MakeFunctionCall(arguments);
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {

            // Note, the LATE property is set in the function details to avoid the function being evaluated by the preEvaluate() call.
            // This is because the full static context is needed, not the (smaller) RetainedStaticContext. Instead, pre-evaluation
            // for calls with fixed arguments is done during the optimization phase, which makes the full static context available.
            if (arguments[0] is Literal && (arguments.Length == 1 || arguments[1] is Literal))
            {
                string lexicalQName = ((StringLiteral)arguments[0]).Stringify();
                IStaticContext env = visitor.StaticContext;
                bool b = false;
                QNameParser qp = new QNameParser(GetRetainedStaticContext()).WithAcceptEQName(true).WithErrorOnBadSyntax("XTDE1400").WithErrorOnUnresolvedPrefix("XTDE1400");
                StructuredQName functionName = qp.Parse(lexicalQName, env.GetDefaultFunctionNamespace());
                int minArity = 0;
                int maxArity = 20;
                if (GetArity() == 2)
                {
                    minArity = (int)((NumericValue)arguments[1].EvaluateItem(env.MakeEarlyEvaluationContext())).LongValue();
                    maxArity = minArity;
                }

                for (int i = minArity; i <= maxArity; i++)
                {
                    SymbolicName.F sn = new SymbolicName.F(functionName, i);
                    if (env.GetFunctionLibrary().IsAvailable(sn, env.GetXPathVersion()))
                    {
                        b = true;
                        break;
                    }
                }

                return Literal.MakeLiteral(BooleanValue.Get(b));
            }
            else
            {
                return null;
            }
        }

        private bool IsFunctionAvailable(string lexicalName, RetainedStaticContext rsc, int arity, IXPathContext context)
        {
            if (arity == -1)
            {
                for (int i = 0; i < 20; i++)
                {
                    if (IsFunctionAvailable(lexicalName, rsc, i, context))
                    {
                        return true;
                    }
                }

                return false;
            }

            StructuredQName qName;
            try
            {
                if (NameChecker.IsValidNCName(StringTool.CodePoints(lexicalName)))
                {

                    // we're in XSLT, where the default namespace for functions can't be changed
                    qName = new StructuredQName("", NamespaceUri.FN, lexicalName);
                }
                else
                {
                    qName = StructuredQName.FromLexicalQName(lexicalName, false, true, GetRetainedStaticContext());
                }
            }
            catch (XPathException e)
            {
                throw e.WithErrorCode("XTDE1400").WithXPathContext(context);
            }

            IFunctionLibrary lib = context.GetController().GetExecutable().FunctionLibrary;
            SymbolicName.F sn = new SymbolicName.F(qName, arity);

            return lib.IsAvailable(sn, rsc.GetPackageData().HostLanguageVersion);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            string lexicalQName = arguments[0].Head().GetStringValue();
            int arity = -1;
            if (arguments.Length == 2)
            {
                arity = (int)((NumericValue)arguments[1].Head()).LongValue();
            }

            return BooleanValue.Get(IsFunctionAvailable(lexicalQName, GetRetainedStaticContext(), arity, context));
        }
    }
}