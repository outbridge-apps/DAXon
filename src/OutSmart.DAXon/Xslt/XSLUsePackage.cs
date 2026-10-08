////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation.Packages;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:use-package elements in stylesheet.
    /// </summary>
    internal sealed class XSLUsePackage : StyleElement
    {
        private string nameAtt = null;
        private PackageVersionRanges versionRanges = null;
        private StylesheetPackage usedPackage;
        private IList<XSLAccept> acceptors = null;

        public override StylesheetPackage UsedPackage => usedPackage;

        private HashSet<SymbolicName> ExplicitAcceptedComponentNames
        {
            get
            {
                HashSet<SymbolicName> explicitAccepts = new HashSet<SymbolicName>();
                foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(XSLAccept))))
                {
                    HashSet<ComponentTest> explicitComponentTests = ((XSLAccept)child).ExplicitComponentTests;
                    foreach (ComponentTest test in explicitComponentTests)
                    {
                        SymbolicName name = test.SymbolicNameIfExplicit;
                        explicitAccepts.Add(name);
                    }
                }

                return explicitAccepts;
            }
        }

        public IList<XSLAccept> Acceptors
        {
            get
            {
                if (this.acceptors == null)
                {
                    acceptors = new List<XSLAccept>();
                    foreach (NodeInfo decl in Children(new TypeIsInstancePredicate(typeof(XSLAccept))))
                    {
                        acceptors.Add((XSLAccept)decl);
                    }
                }

                return acceptors;
            }
        }

        private HashSet<SymbolicName> NamedOverrides
        {
            get
            {
                HashSet<SymbolicName> overrides = new HashSet<SymbolicName>();
                IAxisIterator kids = IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT);
                NodeInfo @override;
                while ((@override = kids.Next()) != null)
                {
                    if (@override is XSLOverride)
                    {
                        IAxisIterator overridings = @override.IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT);
                        NodeInfo overridingDeclaration;
                        while ((overridingDeclaration = overridings.Next()) != null)
                        {
                            if (overridingDeclaration is IStylesheetComponent)
                            {
                                SymbolicName name = ((IStylesheetComponent)overridingDeclaration).GetSymbolicName();
                                if (name != null)
                                {
                                    overrides.Add(name);
                                }
                            }
                        }
                    }
                }

                return overrides;
            }
        }
        public void FindUsedPackage(CompilerInfo info)
        {
            if (usedPackage == null)
            {
                if (nameAtt == null)
                {
                    nameAtt = Whitespace.Trim(GetAttributeValue(NamespaceUri.NULL, "name"));
                }

                if (nameAtt == null)
                {
                    ReportAbsence("name");
                    nameAtt = "unnamed-package";
                }

                PackageVersionRanges ranges = GetPackageVersionRanges();
                PackageDetails pack = ranges == null ? null : info.GetPackageLibrary().FindPackage(nameAtt, ranges);
                usedPackage = pack == null ? null : pack.loadedPackage;
                if (usedPackage == null)
                {
                    CompileErrorInAttribute("Package " + nameAtt + " could not be found", "XTSE3000", "name");

                    // For error recovery, create an empty package
                    usedPackage = GetConfiguration().MakeStylesheetPackage();
                    usedPackage.SetJustInTimeCompilation(info.IsJustInTimeCompilation());
                }

                GlobalContextRequirement gcr = usedPackage.ContextItemRequirements;
                if (gcr != null && !gcr.IsMayBeOmitted())
                {
                    CompileError("Package " + GetAttributeValue("name") + " requires a global context item, so it cannot be used as a library package", "XTTE0590");
                }
            }
        }

        private PackageVersionRanges GetPackageVersionRanges()
        {
            if (versionRanges == null)
            {
                PrepareAttributes();
            }

            return versionRanges;
        }

        public override void PrepareAttributes()
        {
            IAttributeMap atts = Attributes();
            string ranges = "*";
            foreach (AttributeInfo att in atts)
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                if (f.Equals("name"))
                {
                    nameAtt = Whitespace.Trim(att.Value);
                }
                else if (f.Equals("package-version"))
                {
                    ranges = Whitespace.Trim(att.Value).Replace("\\", "");
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            try
            {
                versionRanges = new PackageVersionRanges(ranges);
            }
            catch (XPathException e)
            {
                CompileError(e);
            }
        }

        public override bool IsDeclaration()
        {
            return true;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            foreach (NodeInfo child in Children())
            {
                if (child.GetNodeKind() == Types.Type.TEXT)
                {
                    CompileError("Character content is not allowed as a child of xsl:use-package");
                } //noinspection StatementWithEmptyBody
                else if (child is XSLAccept || child is XSLOverride)
                {
                }
                else
                {
                    CompileError("Child element " + Err.Wrap(child.DisplayName, Err.ELEMENT) + " is not allowed as a child of xsl:use-package", "XTSE0010");
                }
            }
        }

        public override void PostValidate()
        {
            foreach (NodeInfo curr in Children())
            {
                if (curr is XSLOverride || curr is XSLAccept)
                {
                    ((StyleElement)curr).PostValidate();
                }
            }

            HashSet<SymbolicName> accepts = ExplicitAcceptedComponentNames;
            HashSet<SymbolicName> overrides = NamedOverrides;
            if (accepts.Count > 0)
            {
                foreach (SymbolicName o in overrides)
                {
                    if (accepts.Contains(o))
                    {
                        CompileError("Cannot accept and override the same component (" + o + ")", "XTSE3051");
                    }

                    if (o.ComponentKind == StandardNames.XSL_FUNCTION)
                    {

                        // Bug 4326: where xsl:accept gives the function QName but not the arity, the entry will have an arity of -1
                        SymbolicName n = new SymbolicName.F(o.ComponentName, -1);
                        if (accepts.Contains(n))
                        {
                            CompileError("Cannot accept and override the same function (" + o + ")", "XTSE3051");
                        }
                    }
                }
            }
        }

        public void GatherNamedOverrides(PrincipalStylesheetModule module, IList<ComponentDeclaration> topLevel, HashSet<SymbolicName> overrides)
        {
            if (usedPackage == null)
            {
                return; // error already reported
            }

            foreach (NodeInfo @override in Children(new TypeIsInstancePredicate(typeof(XSLOverride))))
            {
                foreach (NodeInfo overridingDeclaration in @override.Children(NodeSelector.Of(new TypeIsInstancePredicate(typeof(IStylesheetComponent)))))
                {
                    ComponentDeclaration decl = new ComponentDeclaration(module, (StyleElement)overridingDeclaration);
                    topLevel.Add(decl);
                    SymbolicName name = ((IStylesheetComponent)overridingDeclaration).GetSymbolicName();
                    if (name != null)
                    {
                        overrides.Add(name);
                    }
                    else if (overridingDeclaration is XSLTemplate && overridingDeclaration.GetAttributeValue(NamespaceUri.NULL, "match") != null)
                    {
                        StructuredQName[] modeNames = ((XSLTemplate)overridingDeclaration).ModeNames;
                        foreach (StructuredQName m in modeNames)
                        {
                            overrides.Add(new SymbolicName(StandardNames.XSL_MODE, m));
                        }
                    }
                }
            }
        }

        public void GatherRuleOverrides(PrincipalStylesheetModule module, HashSet<SymbolicName> overrides)
        {
            StylesheetPackage thisPackage = module.GetStylesheetPackage();
            RuleManager ruleManager = module.GetRuleManager();
            IAxisIterator kids = IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT);
            HashSet<SymbolicName> overriddenModes = new HashSet<SymbolicName>();

            // Process all template rules within xsl:override elements
            NodeInfo @override;
            while ((@override = kids.Next()) != null)
            {
                if (@override is XSLOverride)
                {
                    IAxisIterator overridings = @override.IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT);
                    NodeInfo overridingDeclaration;
                    while ((overridingDeclaration = overridings.Next()) != null)
                    {
                        if (overridingDeclaration is XSLTemplate && overridingDeclaration.GetAttributeValue(NamespaceUri.NULL, "match") != null)
                        {
                            StructuredQName[] modeNames = ((XSLTemplate)overridingDeclaration).ModeNames;
                            foreach (StructuredQName modeName in modeNames)
                            {
                                if (modeName.Equals(Mode.OMNI_MODE_NAME))
                                {
                                    ((StyleElement)overridingDeclaration).CompileError("The mode name #all must not appear in an overriding template rule", "XTSE3440");
                                    continue;
                                }

                                SymbolicName symbolicName = new SymbolicName(StandardNames.XSL_MODE, modeName);
                                overrides.Add(symbolicName);
                                overriddenModes.Add(symbolicName);
                                Component.M derivedComponent = (Component.M)thisPackage.GetComponent(symbolicName);
                                if (derivedComponent == null)
                                {
                                    ((StyleElement)overridingDeclaration).CompileError("Mode " + modeName.DisplayName + " is not defined in the used package", "XTSE3060");
                                    continue;
                                }

                                if (derivedComponent.BaseComponent == null)
                                {
                                    ((StyleElement)overridingDeclaration).CompileError("Mode " + modeName.DisplayName + " cannot be overridden because it is local to this package", "XTSE3440");
                                    continue;
                                }

                                Component.M usedComponent = (Component.M)derivedComponent.BaseComponent;
                                if (usedComponent.GetVisibility() == Visibility.FINAL)
                                {
                                    ((StyleElement)overridingDeclaration).CompileError("Cannot define overriding template rules in mode " + modeName.DisplayName + " because it has visibility=final", "XTSE3060");
                                    continue;
                                }

                                Mode usedMode = usedComponent.GetActor();
                                if (usedComponent.GetVisibility() != Visibility.PUBLIC)
                                {
                                    ((StyleElement)overridingDeclaration).CompileError("Cannot override template rules in mode " + modeName.DisplayName + ", because the mode is not public", "XTSE3060");
                                    continue;
                                }

                                if (derivedComponent.GetActor() == usedMode)
                                {
                                    SimpleMode overridingMode = new SimpleMode(modeName);
                                    CompoundMode newCompoundMode = new CompoundMode(usedMode, overridingMode);
                                    newCompoundMode.DeclaringComponent = derivedComponent;
                                    ruleManager.RegisterMode(newCompoundMode);
                                    derivedComponent.SetActor(newCompoundMode);
                                }
                            }
                        }
                    }
                }
            }


            // Now process all public/final modes in the used package that have not been overridden by new template rules
            RuleManager usedPackageRuleManager = usedPackage.GetRuleManager();
            if (usedPackageRuleManager != null)
            {
                foreach (Mode m in usedPackageRuleManager.AllNamedModes)
                {
                    SymbolicName sn = m.GetSymbolicName();
                    if (!overriddenModes.Contains(sn))
                    {
                        Component c = thisPackage.GetComponent(sn);
                        if (c != null && c.GetVisibility() != Visibility.PRIVATE)
                        {
                            ruleManager.RegisterMode((Mode)c.GetActor());
                        }
                    }
                }
            }
        }
    }
}