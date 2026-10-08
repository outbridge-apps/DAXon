////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:choose element in the stylesheet.
    /// </summary>
    internal sealed class XSLChoose : XSLChooseOrSwitch
    {
        protected override void CompileConditions(Compilation exec, ComponentDeclaration decl, Expression[] conditions)
        {
            int w = 0;
            foreach (NodeInfo curr in Children())
            {
                if (curr is XSLWhen)
                {
                    conditions[w] = ((XSLWhen)curr).Condition;
                    w++;
                }
                else if (curr is XSLOtherwise)
                {
                    Expression otherwise = Literal.MakeLiteral(BooleanValue.TRUE);
                    otherwise.SetRetainedStaticContext(MakeRetainedStaticContext());
                    conditions[w] = otherwise;
                    w++;
                }
            }
        }
    }
}