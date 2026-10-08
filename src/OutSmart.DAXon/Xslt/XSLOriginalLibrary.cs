////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// A function library that recognizes the function name "xsl:original", which may appear within xsl:@override
    /// </summary>
    internal sealed class XSLOriginalLibrary : IFunctionLibrary
    {
        private static readonly XSLOriginalLibrary THE_INSTANCE = new XSLOriginalLibrary();
        private XSLOriginalLibrary()
        {
        }
        public static XSLOriginalLibrary GetInstance()
        {
            return THE_INSTANCE;
        }

        public Expression Bind(SymbolicName.F functionName, Expression[] staticArgs, Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            try
            {
                IFunctionItem target = GetFunctionItem(functionName, env);
                if (target == null)
                {
                    return null;
                }
                else
                {
                    return new StaticFunctionCall(target, staticArgs);
                }
            }
            catch (XPathException e)
            {
                reasons.Add(e.Message);
                return null;
            }
        }

        public bool IsAvailable(SymbolicName.F functionName, int languageLevel)
        {

            // xsl:original is not recognized by function-available() - W3C bug 28122
            return false;
        }

        public IFunctionLibrary Copy()
        {
            return this;
        }

        public IFunctionItem GetFunctionItem(SymbolicName.F functionName, IStaticContext env)
        {
            if (functionName.ComponentKind == StandardNames.XSL_FUNCTION && functionName.ComponentName.HasURI(NamespaceUri.XSLT) && functionName.ComponentName.GetLocalPart().Equals("original") && env is ExpressionContext)
            {
                ExpressionContext expressionContext = (ExpressionContext)env;
                StyleElement containingElement = expressionContext.GetStyleElement();
                XSLFunction overridingFunction = (XSLFunction)containingElement.FindAncestorElement(StandardNames.XSL_FUNCTION);
                if (overridingFunction == null)
                {
                    throw new XPathException("Function name xsl:original can only be used within xsl:function", "XTSE3058");
                }

                SymbolicName originalName = overridingFunction.GetSymbolicName();
                StyleElement @override = (StyleElement)overridingFunction.GetParent();
                if (!(@override is XSLOverride))
                {
                    throw new XPathException("Function name xsl:original can only be used within xsl:override", "XPST0017");
                }

                XSLUsePackage use = (XSLUsePackage)@override.GetParent();
                Component overridden = use.UsedPackage.GetComponent(originalName);
                if (overridden == null)
                {
                    throw new XPathException("Function " + originalName + " does not exist in used package", "XTSE3058");
                }

                return new OriginalFunction(overridden);
            }
            else
            {
                return null;
            }
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
    }
}