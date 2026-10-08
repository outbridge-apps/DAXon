////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Represents an xsl:accept element in an XSLT 3.0 package manifest.
    /// </summary>
    public class XSLAccept : XSLAcceptExpose
    {
        public override void PrepareAttributes()
        {
            base.PrepareAttributes();
        }

        public override void Validate(ComponentDeclaration decl)
        {
            NodeInfo parent = GetParent();
            if (!(parent is XSLUsePackage))
            {
                CompileError("Parent of xsl:accept must be xsl:use-package");
                return;
            }

            StylesheetPackage pack = ((XSLUsePackage)parent).UsedPackage;
            if (pack != null)
            {
                foreach (ComponentTest test in ExplicitComponentTests)
                {
                    IQNameTest nameTest = test.QNameTest;
                    if (nameTest is NameTest)
                    {
                        int kind = test.ComponentKind;
                        SymbolicName sName = kind == StandardNames.XSL_FUNCTION ? new SymbolicName.F(((NameTest)nameTest).MatchingNodeName, test.GetArity()) : new SymbolicName(kind, ((NameTest)nameTest).MatchingNodeName);
                        Component comp = pack.GetComponent(sName);
                        bool found = false;
                        if (comp == null)
                        {
                            if (kind == StandardNames.XSL_FUNCTION && test.GetArity() == -1)
                            {

                                // This will match any function of the required name, regardless of arity
                                for (int i = 0; i <= pack.MaxFunctionArity; i++)
                                {
                                    sName = new SymbolicName.F(((NameTest)nameTest).MatchingNodeName, i);
                                    comp = pack.GetComponent(sName);
                                    if (comp != null)
                                    {
                                        CheckCompatibility(sName, comp.GetVisibility(), GetVisibility());
                                        found = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            CheckCompatibility(sName, comp.GetVisibility(), GetVisibility());
                            found = true;
                        }

                        if (!found)
                        {
                            CompileError("No " + sName.ToString() + " exists in the used package", "XTSE3030");
                        }
                    }
                }
            }
        }

        protected virtual void CheckCompatibility(SymbolicName name, Visibility declared, Visibility exposed)
        {
            if (!IsCompatible(declared, exposed))
            {
                string code = "XTSE3040";
                CompileError("The " + name + " is declared as " + Err.DescribeVisibility(declared) + " and cannot be accepted as " + Err.DescribeVisibility(exposed), code);
            }
        }

        public static bool IsCompatible(Visibility declared, Visibility exposed)
        {

            switch (declared)
            {
                case Visibility.PUBLIC:
                    return exposed == Visibility.PUBLIC || exposed == Visibility.PRIVATE || exposed == Visibility.FINAL || exposed == Visibility.HIDDEN;
                case Visibility.ABSTRACT:
                    return exposed == Visibility.ABSTRACT || exposed == Visibility.HIDDEN;
                case Visibility.FINAL:
                    return exposed == Visibility.PRIVATE || exposed == Visibility.FINAL || exposed == Visibility.HIDDEN;
                case Visibility.UNDEFINED:
                    return true;
                default:
                    return false;
            }
        }
    }
}