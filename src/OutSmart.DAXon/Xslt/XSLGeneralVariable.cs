////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// This class defines common behaviour across xsl:variable, xsl:param, and xsl:with-param
    /// </summary>
    public abstract class XSLGeneralVariable : StyleElement
    {
        public SourceBinding sourceBinding;
        public XSLGeneralVariable()
        {
            sourceBinding = new SourceBinding(this);
        }

        public virtual SourceBinding GetSourceBinding()
        {
            return sourceBinding;
        }

        public virtual StructuredQName GetVariableQName()
        {
            return sourceBinding.VariableQName;
        }

        public override StructuredQName GetObjectName()
        {
            return sourceBinding.VariableQName;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public virtual bool IsGlobal()
        {
            return IsTopLevel(); // might be called before the "global" field is initialized
        }

        public override void Validate(ComponentDeclaration decl)
        {
            sourceBinding.Validate();
        }

        public override void PostValidate()
        {
            sourceBinding.PostValidate();
        }
    }
}