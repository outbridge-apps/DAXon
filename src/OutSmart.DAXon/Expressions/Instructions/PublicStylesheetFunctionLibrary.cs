////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using System.Collections.Generic;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// A PublicStylesheetFunctionLibrary filters a StylesheetFunctionLibrary to include only those functions
    /// whose visibility is final or public. Used by xsl:evaluate
    /// </summary>
    internal sealed class PublicStylesheetFunctionLibrary : IFunctionLibrary
    {
        private readonly IFunctionLibrary @base;

        public PublicStylesheetFunctionLibrary(IFunctionLibrary @base)
        {
            this.@base = @base;
        }

        public Expression Bind(SymbolicName.F functionName, Expression[] staticArgs,
                               Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            Expression baseCall = @base.Bind(functionName, staticArgs, null, env, reasons);
            if (baseCall is UserFunctionCall)
            {
                Component target = ((UserFunctionCall)baseCall).GetTarget();
                Visibility v = target.GetVisibility();
                if (v == Visibility.PUBLIC || v == Visibility.FINAL)
                {
                    return baseCall;
                }
                else
                {
                    reasons.Add("The function exists, but does not have public visibility");
                }
            }

            return null;
        }

        public IFunctionItem GetFunctionItem(SymbolicName.F functionName, IStaticContext staticContext)
        {
            IFunctionItem baseFunction = @base.GetFunctionItem(functionName, staticContext);
            if (baseFunction is UserFunction)
            {
                Visibility v = ((UserFunction)baseFunction).DeclaredVisibility;
                if (v == Visibility.PUBLIC || v == Visibility.FINAL)
                {
                    return baseFunction;
                }
            }

            return null;
        }

        public bool IsAvailable(SymbolicName.F functionName, int languageLevel)
        {
            if (@base is StylesheetFunctionLibrary)
            {
                StylesheetPackage pack = ((StylesheetFunctionLibrary)@base).GetStylesheetPackage();
                UserFunction fn = pack.GetFunction(functionName);
                if (fn != null)
                {
                    Visibility v = fn.DeclaredVisibility;
                    return v == Visibility.PUBLIC || v == Visibility.FINAL;
                }

                return false;
            }

            return @base.IsAvailable(functionName, languageLevel);
        }

        public IFunctionLibrary Copy()
        {
            return this;
        }
    }
}
