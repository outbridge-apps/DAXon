////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLLocalVariable : XSLGeneralVariable
    {
        private static readonly HashSet<SourceBinding.BindingProperty> permittedAttributes = new HashSet<SourceBinding.BindingProperty> { SourceBinding.BindingProperty.SELECT, SourceBinding.BindingProperty.AS };
        public override SourceBinding GetBindingInformation(StructuredQName name)
        {
            if (name.Equals(sourceBinding.VariableQName))
            {
                return sourceBinding;
            }
            else
            {
                return null;
            }
        }

        public override bool IsInstruction()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            sourceBinding.PrepareAttributes(permittedAttributes);
        }

        public SequenceType GetRequiredType()
        {
            return sourceBinding.GetInferredType(true);
        }

        public override void FixupReferences()
        {
            sourceBinding.FixupReferences(null);
            base.FixupReferences();
        }

        public void CompileLocalVariable(Compilation exec, ComponentDeclaration decl)
        {

            sourceBinding.HandleSequenceConstructor(exec, decl); //}
        }
    }
}