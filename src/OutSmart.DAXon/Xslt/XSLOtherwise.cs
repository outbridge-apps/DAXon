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
    /// Handler for xsl:otherwise elements in stylesheet.
    /// </summary>
    internal sealed class XSLOtherwise : StyleElement
    {
        private Expression select;
        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                if (f.Equals("select"))
                {

                    // XSLT 4.0 proposed extension
                    if (RequireXslt40Attribute("select"))
                    {
                        select = MakeExpression(att.Value, att);
                    }
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (!(GetParent() is XSLChooseOrSwitch))
            {
                CompileError("xsl:otherwise must be immediately within xsl:choose or xsl:switch", "XTSE0010");
            }

            if (select != null && HasChildNodes())
            {
                CompileError("xsl:otherwise element must be empty if @select is present", "XTSE0010");
            }
        }

        public override bool MarkTailCalls()
        {
            StyleElement last = LastChildInstruction;
            return last != null && last.MarkTailCalls();
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            throw new NotSupportedException("XSLOtherwise#compile() should not be called");
        }

        public override Expression CompileSequenceConstructor(Compilation compilation, ComponentDeclaration decl, bool includeParams)
        {
            if (select == null)
            {
                return base.CompileSequenceConstructor(compilation, decl, includeParams);
            }
            else
            {
                return select;
            }
        }
    }
}