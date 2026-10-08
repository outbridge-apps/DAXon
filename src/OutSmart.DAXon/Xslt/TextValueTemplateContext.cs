////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class TextValueTemplateContext : ExpressionContext
    {
        readonly TextValueTemplateNode textNode;
        public TextValueTemplateContext(StyleElement parent, TextValueTemplateNode textNode) : base(parent, null)
        {
            this.textNode = textNode;
        }

        public override Expression BindVariable(StructuredQName qName)
        {
            SourceBinding siblingVar = BindLocalVariable(qName);
            if (siblingVar == null)
            {
                return base.BindVariable(qName);
            }
            else
            {
                VariableReference var = new LocalVariableReference(qName);
                siblingVar.RegisterReference(var);
                return var;
            }
        }

        private SourceBinding BindLocalVariable(StructuredQName qName)
        {
            NodeInfo curr = textNode;

            // first search for a local variable declaration
            IAxisIterator preceding = curr.IterateAxis(AxisInfo.PRECEDING_SIBLING);
            while ((curr = preceding.Next()) != null)
            {
                if (curr is XSLGeneralVariable)
                {
                    SourceBinding sourceBinding = ((XSLGeneralVariable)curr).GetBindingInformation(qName);
                    if (sourceBinding != null)
                    {
                        return sourceBinding;
                    }
                }
            }

            return null;
        }
    }
}