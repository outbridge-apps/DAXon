////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Types;
using static OutSmart.DAXon.Transformation.Visibility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
namespace OutSmart.DAXon.Xslt
{
    public class StylesheetPackage : PackageData
    {
        private PackageVersion packageVersion = null;
        private string packageName;
        private readonly IList<StylesheetPackage> usedPackages = new List<StylesheetPackage>();
        private RuleManager ruleManager;
        private CharacterMapIndex characterMapIndex;
        private bool createsSecondaryResultDocuments;
        private readonly IList<IAction> completionActions = new List<IAction>();
        protected GlobalContextRequirement globalContextRequirement = null;
        private bool containsGlobalContextItemDeclaration = false;
        protected ISpaceStrippingRule stripperRules;
        private bool stripsWhitespace = false;
        private bool stripsTypeAnnotations = false;
        protected Properties defaultOutputProperties;
        private StructuredQName defaultMode;
        private bool declaredModes;
        protected Dictionary<StructuredQName, Properties> namedOutputProperties = new Dictionary<StructuredQName, Properties>(4);
        protected HashSet<NamespaceUri> schemaIndex = new HashSet<NamespaceUri>();
        private FunctionLibraryList functionLibrary;
        private XQueryFunctionLibrary queryFunctions;
        private ExecutableFunctionLibrary overriding;
        private ExecutableFunctionLibrary underriding;
        private int maxFunctionArity = -1;
        private bool retainUnusedFunctions = false;
        private bool implicitPackage;
        private readonly Dictionary<SymbolicName, Component> componentIndex = new Dictionary<SymbolicName, Component>(20);
        protected IList<Component> hiddenComponents = new List<Component>();
        protected Dictionary<SymbolicName, Component> overriddenComponents = new Dictionary<SymbolicName, Component>();
        private readonly Dictionary<SymbolicName, Component> abstractComponents = new Dictionary<SymbolicName, Component>();

        public virtual Dictionary<SymbolicName, Component> ComponentIndex => componentIndex;

        public virtual IEnumerable<StylesheetPackage> UsedPackages => usedPackages;

        public virtual string PackageName
        {
            get => packageName; set
            {
                this.packageName = value;
            }
        }

        public virtual StructuredQName DefaultMode
        {
            get => defaultMode; set
            {
                this.defaultMode = value;
            }
        }

        public virtual ISpaceStrippingRule SpaceStrippingRule => stripperRules;

        public virtual ISpaceStrippingRule StripperRules
        {
            get => stripperRules; set
            {
                this.stripperRules = value;
            }
        }

        public virtual HashSet<NamespaceUri> SchemaNamespaces => schemaIndex;

        public virtual GlobalContextRequirement ContextItemRequirements
        {
            get => globalContextRequirement; set
            {
                if (containsGlobalContextItemDeclaration)
                {

                    // the new requirements must be consistent with the existing requirements
                    if ((!value.IsAbsentFocus() && globalContextRequirement.IsAbsentFocus()) || (value.IsMayBeOmitted() && !globalContextRequirement.IsMayBeOmitted()))
                    {
                        throw new XPathException("The package contains two xsl:global-context-item declarations with conflicting @use attributes", "XTSE3087");
                    }

                    TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                    if (th.Relationship(value.RequiredItemType, globalContextRequirement.RequiredItemType) != Affinity.SAME_TYPE)
                    {
                        throw new XPathException("The package contains two xsl:global-context-item declarations with conflicting item types", "XTSE3087");
                    }
                }

                containsGlobalContextItemDeclaration = true;
                globalContextRequirement = value;
            }
        }

        public virtual int MaxFunctionArity
        {
            get
            {
                if (maxFunctionArity == -1)
                {
                    foreach (Component c in componentIndex.Values)
                    {
                        if (c.GetActor() is UserFunction)
                        {
                            if (((UserFunction)c.GetActor()).GetArity() > maxFunctionArity)
                            {
                                maxFunctionArity = ((UserFunction)c.GetActor()).GetArity();
                            }
                        }
                    }
                }

                return maxFunctionArity;
            }
        }

        //    }
        public virtual IFunctionLibrary PublicFunctions => new PublicStylesheetFunctionLibrary(functionLibrary);

        //    }
        public virtual Dictionary<SymbolicName, Component> AbstractComponents => abstractComponents;
        public StylesheetPackage(Configuration config) : base(config)
        {
            SetHostLanguage(HostLanguage.XSLT, 30);
            AccumulatorRegistry = config.MakeAccumulatorRegistry();
        }

        public virtual void AddUsedPackage(StylesheetPackage pack)
        {
            usedPackages.Add(pack);
        }

        public virtual bool Contains(StylesheetPackage pack)
        {
            foreach (StylesheetPackage p in usedPackages)
            {
                if (p == pack || p.Contains(pack))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual void SetLanguageVersion(int version)
        {
            this.hostLanguageVersion = version;
        }

        public virtual PackageVersion GetPackageVersion()
        {
            return packageVersion;
        }

        public virtual void SetPackageVersion(PackageVersion version)
        {
            packageVersion = version;
        }

        public virtual bool IsImplicitPackage()
        {
            return implicitPackage;
        }

        public virtual void SetImplicitPackage(bool implicitPackage)
        {
            this.implicitPackage = implicitPackage;
        }

        public virtual bool IsJustInTimeCompilation()
        {
            return false;
        }

        public virtual void SetJustInTimeCompilation(bool justInTimeCompilation)
        {
        }

        public virtual RuleManager GetRuleManager()
        {
            return ruleManager;
        }

        public virtual void SetRuleManager(RuleManager ruleManager)
        {
            this.ruleManager = ruleManager;
        }

        public virtual void SetDeclaredModes(bool declared)
        {
            declaredModes = declared;
        }

        public virtual bool IsDeclaredModes()
        {
            return declaredModes;
        }

        public virtual CharacterMapIndex GetCharacterMapIndex()
        {
            return characterMapIndex;
        }

        public virtual void SetCharacterMapIndex(CharacterMapIndex characterMapIndex)
        {
            this.characterMapIndex = characterMapIndex;
        }

        public virtual bool IsCreatesSecondaryResultDocuments()
        {
            return createsSecondaryResultDocuments;
        }

        public virtual void SetCreatesSecondaryResultDocuments(bool createsSecondaryResultDocuments)
        {
            this.createsSecondaryResultDocuments = createsSecondaryResultDocuments;
        }

        public virtual bool IsStripsTypeAnnotations()
        {
            return stripsTypeAnnotations;
        }

        public virtual void SetStripsTypeAnnotations(bool stripsTypeAnnotations)
        {
            this.stripsTypeAnnotations = stripsTypeAnnotations;
        }

        public virtual void SetDefaultOutputProperties(Properties props)
        {
            defaultOutputProperties = props;
        }

        public virtual void SetNamedOutputProperties(StructuredQName name, Properties props)
        {
            namedOutputProperties[name] = props;
        }

        public virtual Properties GetNamedOutputProperties(StructuredQName name)
        {
            return namedOutputProperties.GetOrDefault(name);
        }

        public virtual void SetStripsWhitespace(bool strips)
        {
            this.stripsWhitespace = strips;
        }

        public virtual bool IsStripsWhitespace()
        {
            return this.stripsWhitespace;
        }

        public virtual void AddCompletionAction(IAction action)
        {
            completionActions.Add(action);
        }

        public virtual void Complete()
        {

            // Perform the completion actions
            foreach (IAction a in completionActions)
            {
                a.DoAction();
            }

            AllocateBinderySlots();
        }

        public virtual void AllocateBinderySlots()
        {
            SlotManager slotManager = GetConfiguration().MakeSlotManager();
            foreach (Component c in componentIndex.Values)
            {
                RegisterGlobalVariable(c, slotManager);
            }

            foreach (Component c in hiddenComponents)
            {
                RegisterGlobalVariable(c, slotManager);
            }

            GlobalSlotManager = slotManager;
        }

        private void RegisterGlobalVariable(Component c, SlotManager slotManager)
        {
            if (c.GetActor() is GlobalVariable)
            {
                GlobalVariable var = (GlobalVariable)c.GetActor();
                int slot = slotManager.AllocateSlotNumber(var.GetVariableQName(), null);
                var.SetPackageData(this);
                var.BinderySlotNumber = slot; //            if (c.getVisibility() != Visibility.HIDDEN) {
                //                addGlobalVariable(var);
                //            }
            }
        }

        public virtual void AddComponent(Component component)
        {
            SymbolicName name = component.GetActor().GetSymbolicName();
            componentIndex[name] = component;
            if (component.GetVisibility() == Visibility.ABSTRACT && component.ContainingPackage == this)
            {
                abstractComponents[component.GetActor().GetSymbolicName()] = component;
            }
        }

        public override void AddGlobalVariable(GlobalVariable variable)
        {
            base.AddGlobalVariable(variable);
            SymbolicName name = variable.GetSymbolicName();
            if (componentIndex.GetOrDefault(name) == null)
            {
                Component comp = variable.DeclaringComponent;
                if (comp == null)
                {
                    comp = variable.MakeDeclaringComponent(PRIVATE, this);
                }

                AddComponent(comp);
            }
        }

        public virtual Component GetComponent(SymbolicName name)
        {
            return componentIndex.GetOrDefault(name);
        }

        //    }
        public virtual void AddHiddenComponent(Component component)
        {
            hiddenComponents.Add(component);
        }

        //    }
        public virtual Component GetOverriddenComponent(SymbolicName name)
        {
            return overriddenComponents.GetOrDefault(name);
        }

        //    }
        public virtual void AddOverriddenComponent(Component comp)
        {
            overriddenComponents[comp.GetActor().GetSymbolicName()] = comp;
        }

        //    }
        public virtual void AddComponentsFromUsedPackage(StylesheetPackage usedPackage, IList<XSLAccept> acceptors, HashSet<SymbolicName> overrides)
        {
            usedPackages.Add(usedPackage);

            // Create copies of the components in the used package, with suitably adjusted visibility
            // Create a mapping from components in the used package to their corresponding components
            // in the using package, so that we can re-bind the component bindings
            Dictionary<Component, Component> correspondence = new Dictionary<Component, Component>();
            foreach (KeyValuePair<SymbolicName, Component> namedComponentEntry in usedPackage.componentIndex)
            {
                SymbolicName name = namedComponentEntry.Key;
                Component oldC = namedComponentEntry.Value;
                Visibility oldV = oldC.GetVisibility();
                Visibility newV = Visibility.UNDEFINED;
                if (overrides.Contains(name) && !(oldC.GetActor() is Mode))
                {
                    newV = HIDDEN;
                }
                else
                {
                    Visibility acceptedVisibility = ExplicitAcceptedVisibility(name, acceptors);
                    if (acceptedVisibility != Visibility.UNDEFINED)
                    {
                        if (!XSLAccept.IsCompatible(oldV, acceptedVisibility))
                        {
                            throw new XPathException("Cannot accept a " + Err.DescribeVisibility(oldV) + " component (" + name + ") from package " + usedPackage.PackageName + " with visibility " + Err.DescribeVisibility(acceptedVisibility), "XTSE3040").MaybeWithLocation(oldC.GetActor().GetLocation());
                        }

                        newV = acceptedVisibility;
                    }
                    else
                    {
                        acceptedVisibility = WildcardAcceptedVisibility(name, acceptors);
                        if (acceptedVisibility != Visibility.UNDEFINED)
                        {
                            if (XSLAccept.IsCompatible(oldV, acceptedVisibility))
                            {
                                newV = acceptedVisibility;
                            }
                        }
                    }

                    if (newV == Visibility.UNDEFINED)
                    {
                        if (oldV == Visibility.PUBLIC || oldV == Visibility.FINAL)
                        {
                            newV = Visibility.PRIVATE;
                        }
                        else
                        {
                            newV = HIDDEN;
                        }
                    }
                }

                Component newC = Component.MakeComponent(oldC.GetActor(), newV, VisibilityProvenance.DERIVED, this, oldC.DeclaringPackage);
                correspondence[oldC] = newC;
                newC.BaseComponent = oldC;
                if (overrides.Contains(name))
                {

                    // Note: overrides is all the overrides, not only those for this xsl:use-package;
                    // but we have already checked that xsl:override declarations match something in the
                    // right package.
                    overriddenComponents.PutAndGetPrevious(name, newC);
                    if (newV != Visibility.ABSTRACT)
                    {
                        abstractComponents.Remove(name);
                    }
                }

                if (newC.GetVisibility() == HIDDEN)
                {
                    hiddenComponents.Add(newC);
                }
                else if (componentIndex.GetOrDefault(name) != null)
                {
                    if (!(oldC.GetActor() is Mode))
                    {
                        throw new XPathException("Duplicate " + namedComponentEntry.Key, "XTSE3050", oldC.GetActor());
                    }
                }
                else
                {
                    componentIndex[name] = newC;
                    if (oldC.GetActor() is Mode && (oldV == Visibility.PUBLIC || oldV == Visibility.FINAL))
                    {
                        Mode existing = GetRuleManager().ObtainMode(name.ComponentName, false);
                        if (existing != null)
                        {
                            throw new XPathException("Duplicate " + namedComponentEntry.Key, "XTSE3050", oldC.GetActor());
                        }
                        else
                        {
                        }
                    }
                }

                if (newC.GetActor() is Mode && overrides.Contains(name) && !newC.GetVisibility().Equals(HIDDEN))
                {
                    AddCompletionAction(() =>
                    {
                        IList<ComponentBinding> oldBindings = newC.BaseComponent.ComponentBindings;
                        IList<ComponentBinding> newBindings = newC.ComponentBindings;

                        //assert newBindings.size() == oldBindings.size();
                        for (int i = 0; i < oldBindings.Count; i++)
                        {
                            SymbolicName name12 = oldBindings[i].GetSymbolicName();
                            Component target;
                            if (overrides.Contains(name12))
                            {

                                // if there is an @override in this package, we bind to it
                                target = GetComponent(name12);
                                if (target == null)
                                {
                                    throw new InvalidOperationException("We know there's an override for " + name12 + ", but we can't find it");
                                }
                            }
                            else
                            {

                                // otherwise we bind to the component in this package that corresponds to the component in the used package
                                target = correspondence.GetOrDefault(oldBindings[i].GetTarget());
                                if (target == null)
                                {
                                    throw new InvalidOperationException("Saxon can't find the new component corresponding to " + name12);
                                }
                            }

                            ComponentBinding newBinding = new ComponentBinding(name12, target);
                            newBindings[i] = newBinding;
                        }
                    });
                }
                else
                {
                    AddCompletionAction(() =>
                    {
                        IList<ComponentBinding> oldBindings = newC.BaseComponent.ComponentBindings;
                        IList<ComponentBinding> newBindings = new List<ComponentBinding>(oldBindings.Count);
                        MakeNewComponentBindings(overrides, correspondence, oldBindings, newBindings);
                        newC.ComponentBindings = newBindings;
                    });
                }
            }

            foreach (Component oldC in usedPackage.hiddenComponents)
            {
                Component newC = Component.MakeComponent(oldC.GetActor(), HIDDEN, VisibilityProvenance.DERIVED, this, oldC.DeclaringPackage);
                correspondence[oldC] = newC;
                newC.BaseComponent = oldC;
                hiddenComponents.Add(newC);
                AddCompletionAction(() =>
                {
                    IList<ComponentBinding> oldBindings = newC.BaseComponent.ComponentBindings;
                    IList<ComponentBinding> newBindings = new List<ComponentBinding>(oldBindings.Count);
                    MakeNewComponentBindings(overrides, correspondence, oldBindings, newBindings);
                    newC.ComponentBindings = newBindings;
                });
            }

            if (usedPackage.IsCreatesSecondaryResultDocuments())
            {
                SetCreatesSecondaryResultDocuments(true);
            }
        }

        //    }
        private void MakeNewComponentBindings(HashSet<SymbolicName> overrides, Dictionary<Component, Component> correspondence, IList<ComponentBinding> oldBindings, IList<ComponentBinding> newBindings)
        {
            foreach (ComponentBinding oldBinding in oldBindings)
            {
                SymbolicName name = oldBinding.GetSymbolicName();
                Component target;
                if (overrides.Contains(name))
                {

                    // if there is an @override in this package, we bind to it
                    target = GetComponent(name);
                    if (target == null)
                    {
                        throw new InvalidOperationException("We know there's an override for " + name + ", but we can't find it");
                    }
                }
                else
                {

                    // otherwise we bind to the component in this package that corresponds to the component in the used package
                    target = correspondence.GetOrDefault(oldBinding.GetTarget());
                    if (target == null)
                    {
                        throw new InvalidOperationException("Saxon can't find the new component corresponding to " + name);
                    }
                }

                ComponentBinding newBinding = new ComponentBinding(name, target);
                newBindings.Add(newBinding);
            }
        }

        //    }
        private Visibility ExplicitAcceptedVisibility(SymbolicName name, IList<XSLAccept> acceptors)
        {
            foreach (XSLAccept acceptor in acceptors)
            {
                foreach (ComponentTest test in acceptor.ExplicitComponentTests)
                {
                    if (test.Matches(name))
                    {
                        return acceptor.GetVisibility();
                    }
                }
            }

            return Visibility.UNDEFINED;
        }

        //    }
        private Visibility WildcardAcceptedVisibility(SymbolicName name, IList<XSLAccept> acceptors)
        {

            // Note: last one wins
            Visibility vis = Visibility.UNDEFINED;
            foreach (XSLAccept acceptor in acceptors)
            {
                foreach (ComponentTest test in acceptor.WildcardComponentTests)
                {
                    if (((NodeTest)test.QNameTest).DefaultPriority == -0.25 && test.Matches(name))
                    {
                        vis = acceptor.GetVisibility();
                    }
                }
            }

            if (vis != Visibility.UNDEFINED)
            {
                return vis;
            }

            foreach (XSLAccept acceptor in acceptors)
            {
                foreach (ComponentTest test in acceptor.WildcardComponentTests)
                {
                    if (test.Matches(name))
                    {
                        vis = acceptor.GetVisibility();
                    }
                }
            }

            return vis;
        }

        //    }
        public virtual void CreateFunctionLibrary()
        {
            FunctionLibraryList functionLibrary = new FunctionLibraryList();
            functionLibrary.AddFunctionLibrary(config.GetXSLTFunctionSet(hostLanguageVersion));
            functionLibrary.AddFunctionLibrary(new StylesheetFunctionLibrary(this, true));
            functionLibrary.AddFunctionLibrary(config.GetBuiltInExtensionLibraryList(hostLanguageVersion == 40 ? 40 : 31));
            functionLibrary.AddFunctionLibrary(new ConstructorFunctionLibrary(config));
            if (TargetEdition == "JS" || TargetEdition == "JS2")
            {
                AddIxslFunctionLibrary(functionLibrary);
            }

            if (TargetEdition == "JS3")
            {
                AddIxsl3FunctionLibrary(functionLibrary);
            }

            queryFunctions = new XQueryFunctionLibrary(config);
            functionLibrary.AddFunctionLibrary(queryFunctions);
            functionLibrary.AddFunctionLibrary(config.GetIntegratedFunctionLibrary());
            config.AddExtensionBinders(functionLibrary);
            functionLibrary.AddFunctionLibrary(XSLOriginalLibrary.GetInstance());
            functionLibrary.AddFunctionLibrary(new StylesheetFunctionLibrary(this, false));
            this.functionLibrary = functionLibrary;
        }

        //    }
        protected virtual void AddIxslFunctionLibrary(FunctionLibraryList functionLibrary)
        {
        }

        //    }
        protected virtual void AddIxsl3FunctionLibrary(FunctionLibraryList functionLibrary)
        {
        }

        //    }
        protected virtual void AddStubFunctionLibrary(IFunctionLibrary stubFunctions)
        {
            throw new NotSupportedException();
        }

        //    }
        public virtual FunctionLibraryList GetFunctionLibrary()
        {
            return functionLibrary;
        }

        //    }
        public virtual XQueryFunctionLibrary GetXQueryFunctionLibrary()
        {
            return queryFunctions;
        }

        //    }
        public virtual void SetFunctionLibraryDetails(FunctionLibraryList library, ExecutableFunctionLibrary overriding, ExecutableFunctionLibrary underriding)
        {
            if (library != null)
            {
                this.functionLibrary = library;
            }

            this.overriding = overriding;
            this.underriding = underriding;
        }

        //    }
        public virtual UserFunction GetFunction(SymbolicName.F name)
        {
            if (name.GetArity() == -1)
            {

                // supports the single-argument function-available() function
                int maximumArity = 20;
                for (int a = 0; a < maximumArity; a++)
                {
                    SymbolicName.F sn = new SymbolicName.F(name.ComponentName, a);
                    UserFunction uf = GetFunction(sn);
                    if (uf != null)
                    {
                        uf.IncrementReferenceCount();
                        return uf;
                    }
                }

                return null;
            }
            else
            {
                Component component = ComponentIndex.GetOrDefault(name);
                if (component != null)
                {
                    UserFunction uf = (UserFunction)component.GetActor();
                    uf.IncrementReferenceCount();
                    return uf;
                }
                else
                {
                    return null;
                }
            }
        }

        //    }
        public virtual bool IsRetainUnusedFunctions()
        {
            return retainUnusedFunctions;
        }

        //    }
        public virtual void SetRetainUnusedFunctions()
        {
            this.retainUnusedFunctions = true;
        }

        //    }
        public virtual void UpdatePreparedStylesheet(PreparedStylesheet pss)
        {
            foreach (KeyValuePair<SymbolicName, Component> entry in componentIndex)
            {
                if (entry.Value.GetVisibility() == Visibility.ABSTRACT)
                {
                    abstractComponents[entry.Key] = entry.Value;
                }
            }

            pss.TopLevelPackage = this;
            if (IsSchemaAware() || schemaIndex.Count > 0)
            {
                pss.SetSchemaAware(true);
            }

            pss.SetHostLanguage(HostLanguage.XSLT);

            FunctionLibraryList libraryList = new FunctionLibraryList();
            foreach (IFunctionLibrary lib in functionLibrary.LibraryList)
            {
                if (lib is StylesheetFunctionLibrary)
                {
                    if (((StylesheetFunctionLibrary)lib).IsOverrideExtensionFunction())
                    {
                        libraryList.AddFunctionLibrary(overriding); //pss.getStylesheetFunctions().addFunctionLibrary(overriding);
                    }
                    else
                    {
                        libraryList.AddFunctionLibrary(underriding); //pss.getStylesheetFunctions().addFunctionLibrary(underriding);
                    }
                }
                else
                {
                    libraryList.AddFunctionLibrary(lib);
                }
            }

            pss.FunctionLibrary = libraryList;
            if (!pss.CreatesSecondaryResult())
            {
                pss.SetCreatesSecondaryResult(MayCreateSecondaryResultDocuments());
            }

            pss.SetDefaultOutputProperties(defaultOutputProperties);
            foreach (KeyValuePair<StructuredQName, Properties> entry in namedOutputProperties)
            {
                pss.SetOutputProperties(entry.Key, entry.Value);
            }


            // Build the index of named character maps
            if (characterMapIndex != null)
            {
                foreach (CharacterMap cm in characterMapIndex)
                {
                    pss.GetCharacterMapIndex().PutCharacterMap(cm.Name, cm);
                }
            }


            // Finish off the lists of template rules
            pss.SetRuleManager(ruleManager);

            // Add named templates to the prepared stylesheet
            foreach (Component comp in componentIndex.Values)
            {
                if (comp.GetActor() is NamedTemplate)
                {
                    NamedTemplate t = (NamedTemplate)comp.GetActor();
                    pss.PutNamedTemplate(t.TemplateName, t);
                }
            }


            // Share the component index with the prepared stylesheet
            pss.SetComponentIndex(componentIndex);

            // Register stylesheet parameters
            foreach (Component comp in componentIndex.Values)
            {
                if (comp.GetActor() is GlobalParam)
                {
                    GlobalParam gv = (GlobalParam)comp.GetActor();
                    pss.RegisterGlobalParameter(gv);
                }
            }


            // Set the requirements for the global context item
            if (globalContextRequirement != null)
            {
                pss.GlobalContextRequirement = globalContextRequirement;
            }
        }

        //    }
        private bool MayCreateSecondaryResultDocuments()
        {
            if (createsSecondaryResultDocuments)
            {
                return true;
            }

            foreach (StylesheetPackage p in usedPackages)
            {
                if (p.MayCreateSecondaryResultDocuments())
                {
                    return true;
                }
            }

            return false;
        }

        //    }
        public virtual void MarkNonExportable(string message, string errorCode)
        {
        }

        //    }
        public virtual void Export(ExpressionPresenter presenter)
        {
            throw new XPathException("Exporting a stylesheet requires Saxon-EE");
        }

        //    }
        public virtual void CheckForAbstractComponents()
        {
            foreach (KeyValuePair<SymbolicName, Component> entry in componentIndex)
            {
                if (entry.Value.GetVisibility() == Visibility.ABSTRACT && entry.Value.ContainingPackage == this)
                {
                    abstractComponents[entry.Key] = entry.Value;
                }
            }

            if (abstractComponents.Count > 0)
            {
                StringBuilder buff = new StringBuilder(256);
                ILocation loc = null;
                int count = 0;
                foreach (SymbolicName name in abstractComponents.Keys)
                {
                    if (loc == null)
                    {
                        loc = abstractComponents.GetOrDefault(name).GetActor().GetLocation();
                    }

                    if (count++ > 0)
                    {
                        buff.Append(", ");
                    }

                    buff.Append(name.ToString());
                    if (buff.Length > 300)
                    {
                        buff.Append(" ...");
                        break;
                    }
                }

                throw new XPathException("The package is not executable, because it contains abstract components: " + buff, "XTSE3080").MaybeWithLocation(loc);
            }
        }

        //    }
        public virtual bool IsFallbackToNonStreaming()
        {
            return true;
        }

        //    }
        public virtual void SetFallbackToNonStreaming()
        {
        }
    }
}
