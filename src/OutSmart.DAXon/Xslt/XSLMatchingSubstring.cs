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
    internal sealed class XSLMatchingSubstring : StyleElement
    {
        private Expression select = null;
        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                if (f.Equals("select"))
                {
                    if (RequireXslt40Attribute("select"))
                    {
                        select = MakeExpression(value, att);
                    }
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }
        }

        public Expression GetSelectExpression()
        {
            return select;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (!(GetParent() is XSLAnalyzeString))
            {
                CompileError(DisplayName + " must be immediately within xsl:analyze-string", "XTSE0010");
            }

            if (select != null)
            {
                foreach (NodeInfo child in Children())
                {
                    if (!(child is XSLFallback))
                    {
                        if (select != null)
                        {
                            CompileError("An " + DisplayName + " element with a select attribute must be empty", "XTSE3185");
                        }

                        break;
                    }
                }

                select = TypeCheck("select", select);
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            throw new NotSupportedException("XSLMatchingSubstring#compile() should not be called");
        }
    }
}