////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLWithParam : XSLGeneralVariable
    {
        private readonly HashSet<SourceBinding.BindingProperty> allowedAttributes = new HashSet<SourceBinding.BindingProperty> { SourceBinding.BindingProperty.SELECT, SourceBinding.BindingProperty.AS, SourceBinding.BindingProperty.TUNNEL };
        public override void PrepareAttributes()
        {
            sourceBinding.PrepareAttributes(allowedAttributes);
        }

        public bool IsTunnelParam()
        {
            return sourceBinding.HasProperty(SourceBinding.BindingProperty.TUNNEL);
        }

        public override void Validate(ComponentDeclaration decl)
        {
            base.Validate(decl);

            // Check for duplicate parameter names
            IAxisIterator iter = IterateAxis(AxisInfo.PRECEDING_SIBLING);
            IItem prev;
            while ((prev = iter.Next()) != null)
            {
                if (prev is XSLWithParam)
                {
                    if (sourceBinding.VariableQName.Equals(((XSLWithParam)prev).sourceBinding.VariableQName))
                    {
                        CompileError("Duplicate parameter name", "XTSE0670");
                    }
                }
            }
        }

        public void CheckAgainstRequiredType(SequenceType required)
        {
            sourceBinding.CheckAgainstRequiredType(required);
        }

        public WithParam CompileWithParam(Expression parent, Compilation exec, ComponentDeclaration decl)
        {
            sourceBinding.HandleSequenceConstructor(exec, decl);
            WithParam inst = new WithParam();
            inst.SetSelectExpression(parent, sourceBinding.GetSelectExpression());
            inst.VariableQName = sourceBinding.VariableQName;
            inst.RequiredType = sourceBinding.GetInferredType(true);
            return inst;
        }
    }
}