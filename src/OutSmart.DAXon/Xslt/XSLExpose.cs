////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Represents an xsl:expose element in an XSLT 3.0 package manifest.
    /// </summary>
    internal sealed class XSLExpose : XSLAcceptExpose
    {
        protected void CheckCompatibility(SymbolicName name, Visibility declared, Visibility exposed)
        {
            if (exposed == Visibility.ABSTRACT && declared != Visibility.ABSTRACT)
            {
                CompileError("The " + name + " cannot be exposed as " + Err.DescribeVisibility(exposed) + " because it is not originally declared as abstract", "XTSE3025");
            }

            if (!IsCompatible(declared, exposed))
            {
                string code = "XTSE3010";
                CompileError("The " + name + " is declared as " + Err.DescribeVisibility(declared) + " and cannot be exposed as " + Err.DescribeVisibility(exposed), code);
            }
        }

        public static bool IsCompatible(Visibility declared, Visibility exposed)
        {
            if (declared == exposed || declared == Visibility.UNDEFINED)
            {
                return true;
            }

            switch (declared)
            {
                case Visibility.PUBLIC:
                    return exposed == Visibility.PRIVATE || exposed == Visibility.FINAL || exposed == Visibility.HIDDEN;
                case Visibility.ABSTRACT:
                    return exposed == Visibility.HIDDEN;
                case Visibility.FINAL:
                    return exposed == Visibility.PRIVATE || exposed == Visibility.HIDDEN;
                default:
                    return false;
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            PrincipalStylesheetModule psm = GetPrincipalStylesheetModule();
            Visibility exposedVisibility = GetVisibility();

            // The following code checks that explicit references to components (as distinct from
            // wildcards) refer to actual components, and that the exposed visibility is consistent
            // with the declared visibility. It doesn't actually change the component's visibility property.
            // This is done later, in PrincipalStylesheetModule#adjustExposedVisibility.
            foreach (ComponentTest test in ExplicitComponentTests)
            {
                IQNameTest nameTest = test.QNameTest;
                if (nameTest is NameTest)
                {
                    StructuredQName qName = ((NameTest)nameTest).MatchingNodeName;
                    int kind = test.ComponentKind;
                    SymbolicName sName = kind == StandardNames.XSL_FUNCTION ? new SymbolicName.F(((NameTest)nameTest).MatchingNodeName, test.GetArity()) : new SymbolicName(kind, ((NameTest)nameTest).MatchingNodeName);
                    bool found = false;
                    switch (kind)
                    {
                        case StandardNames.XSL_TEMPLATE:
                            {
                                NamedTemplate template = psm.GetNamedTemplate(qName);
                                found = template != null;
                                if (found)
                                {
                                    Visibility declared = template.DeclaredVisibility;
                                    CheckCompatibility(template.GetSymbolicName(), declared, exposedVisibility); //template.getDeclaringComponent().setVisibility(exposedVisibility, false);
                                }

                                break;
                            }

                        case StandardNames.XSL_VARIABLE:
                            {
                                SourceBinding binding = psm.GetGlobalVariableBinding(qName);
                                if (binding != null && !(binding.SourceElement is XSLGlobalParam))
                                {
                                    found = true;
                                }

                                if (found)
                                {
                                    GlobalVariable var = ((XSLGlobalVariable)binding.SourceElement).CompiledVariable;
                                    Visibility declared = var.DeclaredVisibility;
                                    CheckCompatibility(var.GetSymbolicName(), declared, GetVisibility()); //var.getDeclaringComponent().setVisibility(exposedVisibility, false);
                                }

                                break;
                            }

                        case StandardNames.XSL_ATTRIBUTE_SET:
                            {
                                IList<ComponentDeclaration> declarations = psm.GetAttributeSetDeclarations(qName);
                                found = declarations != null && declarations.Count > 0;
                                if (found)
                                {
                                    Visibility declared = declarations[0].SourceElement.DeclaredVisibility;
                                    CheckCompatibility(sName, declared, GetVisibility());
                                }

                                break;
                            }

                        case StandardNames.XSL_MODE:
                            Mode mode = psm.GetRuleManager().ObtainMode(qName, false);
                            found = mode != null;
                            if (found)
                            {
                                CheckCompatibility(sName, mode.DeclaredVisibility, GetVisibility());
                            }

                            if (GetVisibility() == Visibility.ABSTRACT)
                            {
                                CompileError("The visibility of a mode cannot be abstract", "XTSE3025");
                            }

                            break;
                        case StandardNames.XSL_FUNCTION:
                            StylesheetPackage pack = psm.GetStylesheetPackage();
                            if (test.GetArity() == -1)
                            {

                                // This will match any function of the required name, regardless of arity
                                for (int i = 0; i <= pack.MaxFunctionArity; i++)
                                {
                                    sName = new SymbolicName.F(((NameTest)nameTest).MatchingNodeName, i);
                                    Component fn = pack.GetComponent(sName);
                                    if (fn != null)
                                    {
                                        found = true;
                                        UserFunction userFunction = (UserFunction)fn.GetActor();
                                        CheckCompatibility(sName, userFunction.DeclaredVisibility, GetVisibility());
                                    }
                                }
                            }
                            else
                            {
                                Component fn = pack.GetComponent(sName);
                                found = fn != null;
                                if (found)
                                {
                                    UserFunction userFunction = (UserFunction)fn.GetActor();
                                    CheckCompatibility(sName, userFunction.DeclaredVisibility, GetVisibility());
                                }
                            }

                            break;
                    }

                    if (!found && !qName.Equals(new StructuredQName("saxon", NamespaceUri.SAXON, "error-name")))
                    {
                        CompileError("No " + sName + " exists in the containing package", "XTSE3020");
                    }
                }
            }
        }
    }
}