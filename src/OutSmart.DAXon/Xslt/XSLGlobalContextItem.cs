////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:global-context-item declaration in the stylesheet
    /// </summary>
    internal sealed class XSLGlobalContextItem : XSLContextItem
    {
        public override bool IsDeclaration()
        {
            return true;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            IAxisIterator prior = IterateAxis(AxisInfo.PRECEDING_SIBLING, new SameNameTest(this));
            if (prior.Next() != null)
            {
                CompileError("xsl:global-context-item must not appear twice within the same stylesheet module", "XTSE3087");
            }
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            PrepareAttributes();
            GlobalContextRequirement req = new GlobalContextRequirement();
            req.SetMayBeOmitted(IsMayBeOmitted());
            req.SetAbsentFocus(IsAbsentFocus());
            req.AddRequiredItemType(GetRequiredContextItemType());
            try
            {
                top.GetStylesheetPackage().ContextItemRequirements = req;
            }
            catch (XPathException e)
            {
                throw e.WithLocation(decl.SourceElement);
            }
        }
    }
}