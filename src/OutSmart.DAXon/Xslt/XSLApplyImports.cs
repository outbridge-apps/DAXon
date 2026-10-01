////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:apply-imports element in the stylesheet
    /// </summary>
    internal sealed class XSLApplyImports : StyleElement
    {
        public override bool IsInstruction()
        {
            return true;
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

            foreach (NodeInfo child in Children())
            {
                if (child is XSLWithParam)
                {
                }
                else if (child.GetNodeKind() == Types.Type.TEXT)
                {

                    // with xml:space=preserve, white space nodes may still be there
                    if (!Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {
                        CompileError("No character data is allowed within xsl:apply-imports", "XTSE0010");
                    }
                }
                else
                {
                    CompileError("Child element " + child.DisplayName + " is not allowed as a child of xsl:apply-imports", "XTSE0010");
                }
            }

            NodeImpl parent = GetParent();
            while (parent != null)
            {
                if (parent is XSLOverride)
                {
                    CompileError("xsl:apply-imports cannot be used in a template rule declared within xsl:override", "XTSE3460");
                }

                parent = parent.GetParent();
            }
        }

        // OK;
        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            ApplyImports inst = new ApplyImports();
            WithParam[] nonTunnels = GetWithParamInstructions(inst, exec, decl, false);
            WithParam[] tunnels = GetWithParamInstructions(inst, exec, decl, true);
            inst.SetActualParams(nonTunnels);
            inst.SetTunnelParams(tunnels);
            inst.SetLocation(SaveLocation());
            return inst;
        }
    }
}