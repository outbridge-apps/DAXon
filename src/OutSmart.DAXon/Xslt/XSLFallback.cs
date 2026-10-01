////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// xsl:fallback element in stylesheet. <br>
    /// </summary>
    internal sealed class XSLFallback : StyleElement
    {

        public override int EffectiveVersion
        {
            get
            {
                if (GetAttributeValue("version") != null)
                {
                    return base.EffectiveVersion;
                }
                else
                {
                    return GetCompilation().GetCompilerInfo().XsltVersion;
                }
            }
        }
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        protected override bool SeesAvuncularVariables()
        {
            return false;
        }

        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                CheckUnknownAttribute(attName);
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {

            // if we get here, then the parent instruction is OK, so the fallback is not activated
            return null;
        }
    }
}
