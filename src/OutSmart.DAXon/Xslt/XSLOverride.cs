////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;

using OutSmart.DAXon.Api;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Represents an xsl:override element in a package manifest.
    /// </summary>
    internal sealed class XSLOverride : StyleElement
    {
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
            foreach (NodeInfo curr in Children())
            {
                if (curr.GetNodeKind() == Types.Type.TEXT)
                {
                    CompileError("Character content is not allowed as a child of xsl:override", "XTSE0010");
                }
                else if (curr is XSLFunction || curr is XSLTemplate || curr is XSLGlobalVariable || curr is XSLAttributeSet)
                {
                }
                else
                {
                    ((StyleElement)curr).CompileError("Element " + curr.DisplayName + " is not allowed as a child of xsl:override", "XTSE0010");
                }
            }
        }

        // OK
        public override void PostValidate()
        {
            XSLUsePackage parent = (XSLUsePackage)GetParent();
            if (parent.UsedPackage != null)
            {
                foreach (NodeInfo curr in Children())
                {
                    if (curr is XSLFunction || curr is XSLTemplate || curr is XSLGlobalVariable || curr is XSLAttributeSet)
                    {
                        IStylesheetComponent procedure = (IStylesheetComponent)curr;
                        SymbolicName name = procedure.GetSymbolicName();
                        if (name == null)
                        {
                            if (curr is XSLTemplate)
                            {
                                XSLTemplate decl = (XSLTemplate)curr;
                                if (decl.Match == null)
                                {
                                    decl.CompileError("An overriding template with no name must have a match pattern");
                                }

                                StructuredQName[] modeNames = decl.ModeNames;
                                foreach (StructuredQName modeName in modeNames)
                                {
                                    if (modeName.Equals(Mode.OMNI_MODE_NAME))
                                    {
                                        ((StyleElement)curr).CompileError("An overriding template rule must not specify mode=\"#all\"", "XTSE3440");
                                    }
                                    else if (modeName.Equals(Mode.UNNAMED_MODE_NAME))
                                    {
                                        if (decl.DefaultMode.Equals(Mode.UNNAMED_MODE_NAME))
                                        {
                                            ((StyleElement)curr).CompileError("An overriding template rule must not belong to the unnamed mode", "XTSE3440");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                ((StyleElement)curr).CompileError("An overriding component (other than a template rule) must have a name", "XTSE3440");
                                return;
                            }
                        }
                        else
                        {
                            Component overridden = parent.UsedPackage.GetComponent(name);
                            if (overridden == null)
                            {
                                ((StyleElement)curr).CompileError("There is no " + StandardNames.GetLocalName(name.ComponentKind) + " named " + name.ShortName + " in the used package", "XTSE3058");
                                return;
                            }

                            Visibility overriddenVis = overridden.GetVisibility();
                            if (overriddenVis == Visibility.UNDEFINED)
                            {
                                overriddenVis = Visibility.PRIVATE;
                            }

                            if (overriddenVis == Visibility.FINAL || overriddenVis == Visibility.PRIVATE)
                            {
                                ((StyleElement)curr).CompileError("The " + StandardNames.GetLocalName(name.ComponentKind) + " named " + name.ShortName + " in the used package cannot be overridden because its visibility is " + Err.DescribeVisibility(overriddenVis), "XTSE3060");
                                return;
                            }

                            procedure.CheckCompatibility(overridden);
                        }
                    }
                }
            }
        }
    }
}