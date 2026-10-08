////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// A text node in an XSLT 3.0 stylesheet that may or may not contain a text value template
    /// </summary>
    internal sealed class TextValueTemplateNode : TextImpl
    {
        private Expression contentExp;
        private TextValueTemplateContext staticContext;
        public TextValueTemplateNode(UnicodeString value) : base(value)
        {
        }

        public Expression GetContentExpression()
        {
            return contentExp;
        }

        public TextValueTemplateContext GetStaticContext()
        {
            if (staticContext == null)
            {
                staticContext = new TextValueTemplateContext((StyleElement)GetParent(), this);
            }

            return staticContext;
        }

        public void Parse()
        {
            bool disable = false;
            NodeInfo parent = GetParent();
            if (parent is XSLText && StyleElement.IsYes(Whitespace.Trim(parent.GetAttributeValue(NamespaceUri.NULL, "disable-output-escaping"))))
            {
                disable = true;
            }

            try
            {
                contentExp = AttributeValueTemplate.Make(UnicodeStringValue.ToString(), GetStaticContext());
            }
            catch (XPathException e)
            {
                ((StyleElement)GetParent()).CompileError(e.WithLocation(this));
                contentExp = new StringLiteral(GetStringValue());
            }

            contentExp = new ValueOf(contentExp, disable, false);
            contentExp.SetRetainedStaticContext(((StyleElement)GetParent()).MakeRetainedStaticContext());
        }

        public void Validate()
        {
            contentExp = ((StyleElement)GetParent()).TypeCheck("tvt", contentExp);
        }
    }
}