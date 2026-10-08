////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:on-non-empty element in the stylesheet. The rules are identical to xsl:sequence
    /// </summary>
    internal sealed class XSLOnNonEmpty : XSLSequence
    {
        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            Expression e = base.Compile(exec, decl);
            return new OnNonEmptyExpr(e);
        }
    }
}