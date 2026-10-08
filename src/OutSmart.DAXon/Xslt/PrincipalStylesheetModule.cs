////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    public class PrincipalStylesheetModule : StylesheetModule, IGlobalVariableManager
    {
        private readonly StylesheetPackage stylesheetPackage;
        private readonly bool declaredModes;
        private readonly Dictionary<StructuredQName, ComponentDeclaration> globalVariableIndex = new Dictionary<StructuredQName, ComponentDeclaration>(20);
        private readonly Dictionary<StructuredQName, ComponentDeclaration> templateIndex = new Dictionary<StructuredQName, ComponentDeclaration>(20);
        private readonly Dictionary<SymbolicName, ComponentDeclaration> functionIndex = new Dictionary<SymbolicName, ComponentDeclaration>(8);
        private readonly KeyManager keyManager;
        private readonly DecimalFormatManager decimalFormatManager;
        private readonly RuleManager ruleManager;
        private AccumulatorRegistry accumulatorManager = null;
        private int numberOfAliases = 0;
        private IList<ComponentDeclaration> namespaceAliasList = new List<ComponentDeclaration>(5);
        private Dictionary<NamespaceUri, NamespaceBinding> namespaceAliasMap;
        private HashSet<NamespaceUri> aliasResultUriSet;
        private readonly Dictionary<StructuredQName, IList<ComponentDeclaration>> attributeSetDeclarations = new Dictionary<StructuredQName, IList<ComponentDeclaration>>(); // HashMap-typed: Java null-returning indexer, not Dictionary throw (binderies twin)
        private readonly Dictionary<DocumentKey, XSLModuleRoot> moduleCache = new Dictionary<DocumentKey, XSLModuleRoot>(4);
        private readonly TypeAliasManager typeAliasManager;
        private readonly CharacterMapIndex characterMapIndex;
        private readonly IList<IAction> fixupActions = new List<IAction>();
        private bool needsDynamicOutputProperties = false;

        public virtual HashSet<NamespaceUri> ImportedSchemaTable => stylesheetPackage.SchemaNamespaces;
        public PrincipalStylesheetModule(XSLPackage sourceElement) : base(sourceElement, 0)
        {
            declaredModes = sourceElement.IsDeclaredModes();
            stylesheetPackage = GetConfiguration().MakeStylesheetPackage();
            CompilerInfo compilerInfo = sourceElement.GetCompilation().GetCompilerInfo();
            stylesheetPackage.TargetEdition = compilerInfo.TargetEdition;
            stylesheetPackage.SetRelocatable(compilerInfo.IsRelocatable());
            stylesheetPackage.SetJustInTimeCompilation(compilerInfo.IsJustInTimeCompilation());
            stylesheetPackage.SetImplicitPackage(!sourceElement.GetLocalPart().Equals("package"));
            keyManager = stylesheetPackage.GetKeyManager();
            decimalFormatManager = stylesheetPackage.GetDecimalFormatManager();
            ruleManager = new RuleManager(stylesheetPackage, compilerInfo);
            ruleManager.UnnamedMode.MakeDeclaringComponent(Visibility.PRIVATE, stylesheetPackage);
            stylesheetPackage.SetRuleManager(ruleManager);
            stylesheetPackage.SetDeclaredModes(declaredModes);
            StructuredQName defaultMode = sourceElement.DefaultMode;
            stylesheetPackage.DefaultMode = sourceElement.DefaultMode;
            if (defaultMode != null)
            {
                ruleManager.ObtainMode(defaultMode, !declaredModes);
            }

            characterMapIndex = new CharacterMapIndex();
            stylesheetPackage.SetCharacterMapIndex(characterMapIndex);
            typeAliasManager = GetConfiguration().MakeTypeAliasManager();
            stylesheetPackage.SetTypeAliasManager(typeAliasManager);
            try
            {
                InputTypeAnnotations = sourceElement.InputTypeAnnotationsAttribute;
            }
            catch (XPathException err)
            {
            }
        }

        public virtual Component GetComponent(SymbolicName name)
        {
            return stylesheetPackage.ComponentIndex.GetOrDefault(name);
        }

        public override PrincipalStylesheetModule GetPrincipalStylesheetModule()
        {
            return this;
        }

        public virtual StylesheetPackage GetStylesheetPackage()
        {
            return stylesheetPackage;
        }

        public virtual KeyManager GetKeyManager()
        {
            return keyManager;
        }

        public virtual DecimalFormatManager GetDecimalFormatManager()
        {
            return decimalFormatManager;
        }

        public virtual RuleManager GetRuleManager()
        {
            return ruleManager;
        }

        public virtual bool IsDeclaredModes()
        {
            return declaredModes;
        }

        public virtual void AddFixupAction(IAction action)
        {
            fixupActions.Add(action);
        }

        public virtual void SetNeedsDynamicOutputProperties(bool b)
        {
            needsDynamicOutputProperties = b;
        }

        public virtual CharacterMapIndex GetCharacterMapIndex()
        {
            return characterMapIndex;
        }

        public virtual TypeAliasManager GetTypeAliasManager()
        {
            return typeAliasManager;
        }

        public virtual void DeclareXQueryFunction(XQueryFunction function)
        {
            XQueryFunctionLibrary lib = GetStylesheetPackage().GetXQueryFunctionLibrary();
            if (GetStylesheetPackage().GetFunction((SymbolicName.F)(function.GetUserFunction().GetSymbolicName())) != null)
            {
                throw new XPathException("Duplicate declaration of " + function.GetUserFunction().GetSymbolicName(), "XQST0034");
            }

            lib.DeclareFunction(function);
        }

        public virtual void PutStylesheetDocument(DocumentKey key, XSLStylesheet module)
        {
            moduleCache[key] = module;
        }

        public virtual XSLModuleRoot GetStylesheetDocument(DocumentKey key)
        {
            XSLModuleRoot sheet = moduleCache.GetOrDefault(key);
            if (sheet != null)
            {
                sheet.IssueWarning("Stylesheet module " + key + " is included or imported more than once. " + "This is permitted, but may lead to errors or unexpected behavior", DAXonErrorCode.SXWN9019);
            }

            return sheet;
        }

        public virtual void Preprocess(Compilation compilation)
        {
            Timer timer = compilation.timer;

            // process any xsl:use-package, xsl:include and xsl:import elements
            SpliceUsePackages((XSLPackage)RootElement, RootElement.GetCompilation());
            if (Compilation.TIMING)
            {
                timer.Report("spliceIncludes");
            }


            // import schema documents
            ImportSchemata();
            if (Compilation.TIMING)
            {
                timer.Report("importSchemata");
            }


            // process type aliases
            GetTypeAliasManager().ProcessAllDeclarations(topLevel);

            // build indexes for selected top-level elements
            BuildIndexes();
            if (Compilation.TIMING)
            {
                timer.Report("buildIndexes");
            }


            // check for use of schema-aware constructs
            CheckForSchemaAwareness();
            if (Compilation.TIMING)
            {
                timer.Report("checkForSchemaAwareness");
            }


            // process the attributes of every node in the tree
            ProcessAllAttributes();
            if (Compilation.TIMING)
            {
                timer.Report("processAllAttributes");
            }


            // collect any namespace aliases
            CollectNamespaceAliases();
            if (Compilation.TIMING)
            {
                timer.Report("collectNamespaceAliases");
            }


            // fix up references from XPath expressions to variables and functions, for static typing
            foreach (ComponentDeclaration topLevelDecl in topLevel)
            {
                StyleElement inst = topLevelDecl.SourceElement;
                if (!inst.IsActionCompleted(StyleElement.ACTION_FIXUP))
                {
                    inst.SetActionCompleted(StyleElement.ACTION_FIXUP);
                    inst.FixupReferences();
                }
            }

            if (Compilation.TIMING)
            {
                timer.Report("fixupReferences");
            }


            // Validate the whole package (i.e. with included and imported stylesheet modules)
            XSLPackage top = (XSLPackage)StylesheetElement;
            if (!top.IsActionCompleted(StyleElement.ACTION_VALIDATE))
            {
                top.SetActionCompleted(StyleElement.ACTION_VALIDATE);
                top.Validate(null);
                foreach (ComponentDeclaration d in topLevel)
                {
                    d.SourceElement.ValidateSubtree(d, false);
                }
            }

            if (Compilation.TIMING)
            {
                timer.Report("validate");
            }


            // Gather the output properties
            Properties props = GatherOutputProperties(null);
            props.SetProperty(DAXonOutputKeys.STYLESHEET_VERSION, top.EffectiveVersion + "");
            GetStylesheetPackage().SetDefaultOutputProperties(props);

            // Handle named output formats for use at run-time
            HashSet<StructuredQName> outputNames = new HashSet<StructuredQName>();
            foreach (ComponentDeclaration outputDecl in topLevel)
            {
                if (outputDecl.SourceElement is XSLOutput)
                {
                    XSLOutput @out = (XSLOutput)outputDecl.SourceElement;
                    StructuredQName qName = @out.FormatQName;
                    if (qName != null)
                    {
                        outputNames.Add(qName);
                    }
                }
            }

            if (outputNames.Count == 0)
            {
                if (needsDynamicOutputProperties)
                {
                    throw new XPathException("The stylesheet contains xsl:result-document instructions that calculate the output " + "format name at run-time, but there are no named xsl:output declarations", "XTDE1460");
                }
            }
            else
            {
                foreach (StructuredQName qName in outputNames)
                {
                    Properties oprops = GatherOutputProperties(qName);

                    //if (needsDynamicOutputProperties) {  // needed for saxon:serialize
                    GetStylesheetPackage().SetNamedOutputProperties(qName, oprops); //}
                }
            }

            if (Compilation.TIMING)
            {
                timer.Report("Register output formats");
            }


            // Index the character maps
            foreach (ComponentDeclaration d in topLevel)
            {
                StyleElement inst = d.SourceElement;
                if (inst is XSLCharacterMap)
                {
                    XSLCharacterMap xcm = (XSLCharacterMap)inst;
                    StructuredQName qn = xcm.CharacterMapName;
                    IntHashMap<string> map = new IntHashMap<string>();
                    xcm.Assemble(map);
                    characterMapIndex.PutCharacterMap(xcm.CharacterMapName, new CharacterMap(qn, map));
                }
            }

            if (Compilation.TIMING)
            {
                timer.Report("Index character maps");
            }
        }

        //}
        protected virtual void SpliceUsePackages(XSLPackage xslpackage, Compilation compilation)
        {

            // Warning message deleted by bug 3278
            IList<XSLUsePackage> useDeclarations = new List<XSLUsePackage>();
            GatherUsePackageDeclarations(compilation, xslpackage, useDeclarations);

            // First pass: gather all the named overriding declarations and add them to the topLevel declaration list
            HashSet<SymbolicName> overrides = new HashSet<SymbolicName>();
            foreach (XSLUsePackage use in useDeclarations)
            {
                GatherOverridingDeclarations(use, compilation, overrides);
            }


            // Second pass: make modified copies of the named components in the used packages
            StylesheetPackage thisPackage = GetStylesheetPackage();
            foreach (XSLUsePackage use in useDeclarations)
            {
                IList<XSLAccept> acceptors = use.Acceptors;
                thisPackage.AddComponentsFromUsedPackage(use.UsedPackage, acceptors, overrides);
            }


            // Third pass: process the overriding template rules, creating new mode objects
            foreach (XSLUsePackage use in useDeclarations)
            {
                use.GatherRuleOverrides(this, overrides);
            }


            // Now process the declarations contained within this package, both in the top-level module
            // and within its included and imported modules
            SpliceIncludes();
        }

        //}
        private static void GatherUsePackageDeclarations(Compilation compilation, StyleElement wrapper, IList<XSLUsePackage> declarations)
        {
            foreach (NodeInfo use in wrapper.Children())
            {
                if (use is XSLUsePackage)
                {
                    declarations.Add((XSLUsePackage)use);
                }
                else if (use is XSLInclude)
                {
                    string href = Whitespace.Trim(use.GetAttributeValue(NamespaceUri.NULL, "href"));
                    DocumentKey key = DocumentFn.ComputeDocumentKey(href, use.GetBaseURI(), compilation.GetPackageData(), false);
                    ITreeInfo includedTree = compilation.StylesheetModules.GetOrDefault(key);
                    if (includedTree == null)
                    {
                        throw new XPathException("Internal problem: the included stylesheet module '" + href + "' should be in the compiler's module store, but was not found");
                    }

                    StyleElement incWrapper = (StyleElement)((DocumentImpl)includedTree.GetRootNode()).DocumentElement;
                    GatherUsePackageDeclarations(compilation, incWrapper, declarations);
                }
            }
        }

        private void GatherOverridingDeclarations(XSLUsePackage use, Compilation compilation, HashSet<SymbolicName> overrides)
        {
            use.FindUsedPackage(compilation.GetCompilerInfo());
            use.GatherNamedOverrides(this, topLevel, overrides);
        }

        protected virtual void ImportSchemata()
        {

            // Outside Saxon-EE, xsl:import-schemas are an error
            for (int i = topLevel.Count - 1; i >= 0; i--)
            {
                ComponentDeclaration decl = topLevel[i];
                if (decl.SourceElement is XSLImportSchema)
                {
                    throw new XPathException("xsl:import-schema requires Saxon-EE").WithErrorCode("XTSE1650").WithLocation(decl.SourceElement);
                }
            }
        }

        //}
        private void BuildIndexes()
        {

            // Scan the declarations in reverse order, that @is, highest precedence first
            for (int i = topLevel.Count - 1; i >= 0; i--)
            {
                ComponentDeclaration decl = topLevel[i];
                decl.SourceElement.Index(decl, this);
            }
        }

        //}
        public virtual void ProcessAllAttributes()
        {
            RootElement.ProcessDefaultCollationAttribute();
            RootElement.ProcessDefaultMode();
            RootElement.PrepareAttributes();
            foreach (XSLModuleRoot xss in moduleCache.Values)
            {
                xss.PrepareAttributes();
            }

            foreach (ComponentDeclaration decl in topLevel)
            {
                StyleElement inst = decl.SourceElement;
                if (!inst.IsActionCompleted(StyleElement.ACTION_PROCESS_ATTRIBUTES))
                {
                    inst.SetActionCompleted(StyleElement.ACTION_PROCESS_ATTRIBUTES);
                    try
                    {
                        inst.ProcessAllAttributes();
                    }
                    catch (XPathException err)
                    {
                        decl.SourceElement.CompileError(err);
                    }
                }
            }
        }

        public virtual void IndexFunction(ComponentDeclaration decl)
        {
            Dictionary<SymbolicName, Component> componentIndex = stylesheetPackage.ComponentIndex;
            XSLFunction sourceFunction = (XSLFunction)decl.SourceElement;
            UserFunction compiledFunction = sourceFunction.GetCompiledFunction();
            Component declaringComponent = compiledFunction.ObtainDeclaringComponent(sourceFunction);
            int maxArity = sourceFunction.NumberOfParameters;
            int minArity = maxArity - sourceFunction.NumberOfOptionalParameters;
            for (int arity = minArity; arity <= maxArity; arity++)
            {
                SymbolicName.F sName = new SymbolicName.F(sourceFunction.GetObjectName(), arity);

                // see if there is already a named function with this precedence
                ComponentDeclaration otherDecl = functionIndex.GetOrDefault(sName);
                Component otherComp = componentIndex.GetOrDefault(sName);
                if (otherDecl == null && otherComp == null)
                {

                    // this is the first one in this stylesheet; and there is none in a used package
                    if (arity == maxArity)
                    {
                        componentIndex[sName] = declaringComponent;
                    }

                    functionIndex[sName] = decl;
                }
                else if (otherDecl != null)
                {

                    // there is another function in this stylesheet
                    int thisPrecedence = decl.Precedence;
                    int otherPrecedence = otherDecl.Precedence;
                    if (thisPrecedence == otherPrecedence)
                    {
                        UserFunction otherFunction = ((XSLFunction)otherDecl.SourceElement).GetCompiledFunction();
                        if (minArity == maxArity && otherFunction.GetMinimumArity() == otherFunction.GetArity())
                        {
                            sourceFunction.CompileError("Function " + sName.ShortName + " is declared twice - see " + Err.Show(otherFunction.GetLocation()), "XTSE0770");
                        }
                        else
                        {
                            sourceFunction.CompileError("Function " + sName.ComponentName.DisplayName + " has overlapping arity range " + ShowArityRanges(compiledFunction, otherFunction) + " with another function of the same name - see " + Err.Show(otherFunction.GetLocation()), "XTSE0770");
                        }
                    }

                    break;
                }
                else
                {
                    Component other = componentIndex.GetOrDefault(new SymbolicName.F(sourceFunction.GetObjectName(), maxArity));
                    if (other != null && other.DeclaringPackage == GetStylesheetPackage())
                    {

                        // check the precedences
                        int thisPrecedence = decl.Precedence;
                        ComponentDeclaration otherFunction = functionIndex.GetOrDefault(sName);
                        int otherPrecedence = otherFunction.Precedence;
                        if (thisPrecedence == otherPrecedence)
                        {
                            string message = "Duplicate named function (see line " + otherFunction.SourceElement.GetLineNumber() + " of " + otherFunction.SourceElement.GetSystemId() + ')';
                            if (maxArity != ((UserFunction)other.GetActor()).NumberOfParameters)
                            {
                                message += ". The arity ranges of the two functions overlap";
                            }

                            sourceFunction.CompileError(message, "XTSE0770");
                            break;
                        }
                        else if (thisPrecedence < otherPrecedence)
                        {
                        }
                        else
                        {

                            // can't happen, but we'll play safe
                            componentIndex.PutAndGetPrevious(sName, declaringComponent);
                            functionIndex[sName] = decl;
                        }
                    }
                    else if (sourceFunction.FindAncestorElement(StandardNames.XSL_OVERRIDE) != null)
                    {

                        // the new one wins
                        componentIndex.PutAndGetPrevious(sName, declaringComponent);
                        functionIndex[sName] = decl;
                    }
                    else
                    {
                        sourceFunction.CompileError("Function " + sName.ShortName + " conflicts with a public function in package " + other.DeclaringPackage.PackageName, "XTSE3050");
                    }
                }
            }
        }

        //}
        private static string ShowArityRanges(UserFunction fn1, UserFunction fn2)
        {
            return "(" + ShowArityRange(fn1) + "; " + ShowArityRange(fn2) + ")";
        }

        private static string ShowArityRange(UserFunction fn)
        {
            return fn.GetMinimumArity() + "-" + fn.GetArity();
        }

        public virtual void IndexVariableDeclaration(ComponentDeclaration decl)
        {
            XSLGlobalVariable varDecl = (XSLGlobalVariable)decl.SourceElement;
            StructuredQName qName = varDecl.GetSourceBinding().VariableQName;
            GlobalVariable compiledVariable = (GlobalVariable)varDecl.GetActor();
            Component declaringComponent = compiledVariable.ObtainDeclaringComponent(varDecl);
            Dictionary<SymbolicName, Component> componentIndex = stylesheetPackage.ComponentIndex;
            if (qName != null)
            {

                // see if there is already a global variable with this precedence
                SymbolicName sName = varDecl.GetSymbolicName();
                Component other = componentIndex.GetOrDefault(sName);
                if (other == null)
                {

                    // this is the first
                    globalVariableIndex.PutAndGetPrevious(qName, decl);
                    componentIndex[new SymbolicName(StandardNames.XSL_VARIABLE, qName)] = varDecl.GetActor().DeclaringComponent;
                }
                else
                {
                    if (other.DeclaringPackage == GetStylesheetPackage())
                    {

                        // check the precedences
                        int thisPrecedence = decl.Precedence;
                        ComponentDeclaration otherVarDecl = globalVariableIndex.GetOrDefault(sName.ComponentName);
                        int otherPrecedence = otherVarDecl.Precedence;
                        if (thisPrecedence == otherPrecedence)
                        {
                            StyleElement v2 = otherVarDecl.SourceElement;
                            if (v2 == varDecl)
                            {
                                varDecl.CompileError("Global variable or parameter $" + qName.DisplayName + " is declared more than once " + "(caused by including the containing module more than once)", "XTSE0630");
                            }
                            else
                            {
                                varDecl.CompileError("Duplicate global variable/parameter $" + qName.DisplayName + " (see line " + v2.GetLineNumber() + " of " + v2.GetSystemId() + ')', "XTSE0630");
                            }
                        }
                        else if (thisPrecedence < otherPrecedence && varDecl != otherVarDecl.SourceElement)
                        {
                            varDecl.SetRedundant(true);
                        }
                        else if (varDecl != otherVarDecl.SourceElement)
                        {
                            ((XSLGlobalVariable)otherVarDecl.SourceElement).SetRedundant(true);
                            globalVariableIndex[qName] = decl;
                            componentIndex[new SymbolicName(StandardNames.XSL_VARIABLE, qName)] = varDecl.GetActor().DeclaringComponent;
                        }
                    }
                    else if (varDecl.FindAncestorElement(StandardNames.XSL_OVERRIDE) != null)
                    {

                        // the new one wins
                        componentIndex.PutAndGetPrevious(sName, declaringComponent);
                        globalVariableIndex[sName.ComponentName] = decl;
                    }
                    else
                    {
                        string kind = varDecl is XSLGlobalParam ? "parameter" : "variable";
                        varDecl.CompileError("Global " + kind + " $" + sName.ComponentName.DisplayName + " conflicts with a public variable/parameter in package " + other.DeclaringPackage.PackageName, "XTSE3050");
                    }
                }
            }
        }

        //}
        public virtual SourceBinding GetGlobalVariableBinding(StructuredQName qName)
        {
            ComponentDeclaration decl = globalVariableIndex.GetOrDefault(qName);
            return decl == null ? null : ((XSLGlobalVariable)decl.SourceElement).GetSourceBinding();
        }

        public virtual void IndexNamedTemplate(ComponentDeclaration decl)
        {
            Dictionary<SymbolicName, Component> componentIndex = stylesheetPackage.ComponentIndex;
            XSLTemplate sourceTemplate = (XSLTemplate)decl.SourceElement;
            SymbolicName sName = sourceTemplate.GetSymbolicName();
            if (sName != null)
            {

                // see if there is already a named template with this precedence
                Component other = componentIndex.GetOrDefault(sName);
                if (other == null)
                {

                    // this is the first
                    //NamedTemplate compiledTemplate = new NamedTemplate();
                    NamedTemplate compiledTemplate = ((XSLTemplate)decl.SourceElement).CompiledNamedTemplate;
                    Component declaringComponent = compiledTemplate.ObtainDeclaringComponent(sourceTemplate);
                    componentIndex[sName] = declaringComponent;
                    SetLocalParamDetails(sourceTemplate, compiledTemplate);
                    templateIndex[sName.ComponentName] = decl;
                }
                else
                {
                    if (other.DeclaringPackage == GetStylesheetPackage())
                    {

                        // check the precedences
                        int thisPrecedence = decl.Precedence;
                        ComponentDeclaration otherTemplate = templateIndex.GetOrDefault(sName.ComponentName);
                        int otherPrecedence = otherTemplate.Precedence;
                        if (thisPrecedence == otherPrecedence)
                        {
                            string errorCode = sourceTemplate.GetParent() is XSLOverride ? "XTSE3055" : "XTSE0660";
                            sourceTemplate.CompileError("Duplicate named template (see line " + otherTemplate.SourceElement.GetLineNumber() + " of " + otherTemplate.SourceElement.GetSystemId() + ')', errorCode);
                        } //noinspection StatementWithEmptyBody
                        else if (thisPrecedence < otherPrecedence)
                        {
                        }
                        else
                        {

                            NamedTemplate compiledTemplate = new NamedTemplate(sName.ComponentName, GetConfiguration());
                            Component declaringComponent = compiledTemplate.ObtainDeclaringComponent(sourceTemplate);
                            componentIndex[sName] = declaringComponent;
                            templateIndex[sName.ComponentName] = decl;
                            SetLocalParamDetails(sourceTemplate, compiledTemplate);
                        }
                    }
                    else if (sourceTemplate.FindAncestorElement(StandardNames.XSL_OVERRIDE) != null)
                    {

                        // the new one wins
                        NamedTemplate compiledTemplate = sourceTemplate.CompiledNamedTemplate; //new NamedTemplate();
                        Component declaringComponent = compiledTemplate.ObtainDeclaringComponent(sourceTemplate);
                        componentIndex[sName] = declaringComponent;
                        templateIndex[sName.ComponentName] = decl;
                    }
                    else
                    {
                        sourceTemplate.CompileError("Named template " + sName.ComponentName.DisplayName + " conflicts with a public named template in package " + other.DeclaringPackage.PackageName, "XTSE3050");
                    }
                }
            }
        }

        private static void SetLocalParamDetails(XSLTemplate source, NamedTemplate nt)
        {
            IAxisIterator kids = source.IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT);
            List<NamedTemplate.LocalParamInfo> details = new List<NamedTemplate.LocalParamInfo>();
            SequenceTool.Supply(kids, (child) =>
            {
                if (child is XSLLocalParam)
                {
                    XSLLocalParam lp = (XSLLocalParam)child;
                    lp.PrepareTemplateSignatureAttributes();
                    NamedTemplate.LocalParamInfo info = new NamedTemplate.LocalParamInfo();
                    info.name = lp.GetVariableQName();
                    info.requiredType = lp.GetRequiredType();
                    info.isRequired = lp.IsRequiredParam();
                    info.isTunnel = lp.IsTunnelParam();
                    details.Add(info);
                }
            });
            nt.LocalParamDetails = details;
        }

        public virtual NamedTemplate GetNamedTemplate(StructuredQName name)
        {
            Dictionary<SymbolicName, Component> componentIndex = stylesheetPackage.ComponentIndex;
            Component component = componentIndex.GetOrDefault(new SymbolicName(StandardNames.XSL_TEMPLATE, name));
            return component == null ? null : (NamedTemplate)component.GetActor();
        }

        public virtual void IndexAttributeSet(ComponentDeclaration decl)
        {

            XSLAttributeSet sourceAttributeSet = (XSLAttributeSet)decl.SourceElement;
            StructuredQName name = sourceAttributeSet.AttributeSetName;
            IList<ComponentDeclaration> entries = attributeSetDeclarations.GetOrDefault(name);
            if (entries == null)
            {
                entries = new List<ComponentDeclaration>();
                attributeSetDeclarations[name] = entries;
            }
            else
            {
                string thisVis = Whitespace.Trim(sourceAttributeSet.GetAttributeValue(NamespaceUri.NULL, "visibility"));
                string firstVis = Whitespace.Trim(entries[0].SourceElement.GetAttributeValue(NamespaceUri.NULL, "visibility"));
                if (thisVis == null ? firstVis != null : !thisVis.Equals(firstVis))
                {
                    throw new XPathException("Visibility attributes on attribute-sets sharing the same name must all be the same", "XTSE0010");
                }
            }

            entries.Insert(0,decl);
        }

        public virtual IList<ComponentDeclaration> GetAttributeSetDeclarations(StructuredQName name)
        {
            return attributeSetDeclarations.GetOrDefault(name);
        }

        public virtual void CombineAttributeSets(Compilation compilation)
        {
            Dictionary<StructuredQName, AttributeSet> index = new Dictionary<StructuredQName, AttributeSet>();
            foreach (KeyValuePair<StructuredQName, IList<ComponentDeclaration>> entry in attributeSetDeclarations)
            {
                AttributeSet @as = new AttributeSet();
                @as.SetName(entry.Key);
                @as.SetPackageData(stylesheetPackage);
                StyleElement firstDecl = entry.Value[0].SourceElement;
                @as.SetSystemId(firstDecl.GetSystemId());
                @as.SetLineNumber(firstDecl.GetLineNumber());
                @as.SetColumnNumber(firstDecl.GetColumnNumber());
                index[entry.Key] = @as;
                Component declaringComponent = @as.DeclaringComponent;
                if (declaringComponent == null)
                {
                    declaringComponent = @as.MakeDeclaringComponent(Visibility.PRIVATE, stylesheetPackage);
                }

                stylesheetPackage.AddComponent(declaringComponent);
            }

            foreach (KeyValuePair<StructuredQName, IList<ComponentDeclaration>> entry in attributeSetDeclarations)
            {
                IList<Expression> content = new List<Expression>();
                Visibility vis = Visibility.UNDEFINED;
                bool explicitVisibility = false;
                bool streamable = false;

                // Bug 3195. If the same xsl:attribute-set element is present more than
                // once in the list, we need to remove all but the last occurrence, otherwise
                // the same expression will be present more than once in the tree. This can
                // happen when a stylesheet module is included/imported more than once.
                IList<ComponentDeclaration> entries = new List<ComponentDeclaration>();
                HashSet<XSLAttributeSet> elements = new HashSet<XSLAttributeSet>();
                for (int i = entry.Value.Count - 1; i >= 0; i--)
                {
                    ComponentDeclaration attSetDecl = entry.Value[i];
                    XSLAttributeSet src = (XSLAttributeSet)attSetDecl.SourceElement;
                    if (!elements.Contains(src))
                    {
                        entries.Insert(0,attSetDecl);
                        elements.Add(src);
                    }
                }

                foreach (ComponentDeclaration decl in entries)
                {
                    XSLAttributeSet src = (XSLAttributeSet)decl.SourceElement;
                    streamable |= src.IsDeclaredStreamable();
                    src.CompileDeclaration(compilation, decl);
                    content.AddRange(src.ContainedInstructions);
                    vis = src.GetVisibility();
                    explicitVisibility = explicitVisibility || src.GetAttributeValue(NamespaceUri.NULL, "visibility") != null;
                }

                AttributeSet aSet = index.GetOrDefault(entry.Key);
                aSet.SetDeclaredStreamable(streamable);
                Expression block = Block.MakeBlock(content);
                aSet.SetBody(block);
                SlotManager frame = GetConfiguration().MakeSlotManager();
                ExpressionTool.AllocateSlots(block, 0, frame);
                aSet.SetStackFrameMap(frame);
                VisibilityProvenance provenance = explicitVisibility ? VisibilityProvenance.EXPLICIT : VisibilityProvenance.DEFAULTED;
                aSet.DeclaringComponent.SetVisibility(vis, provenance);
                if (streamable)
                {
                    CheckStreamability(aSet);
                }
            }
        }

        protected virtual void CheckStreamability(AttributeSet aSet)
        {
        }

        public virtual void GetAttributeSets(StructuredQName name, IList<ComponentDeclaration> list)
        {

            // search for the named attribute set, using all of them if there are several with the
            // same name
            foreach (ComponentDeclaration decl in topLevel)
            {
                if (decl.SourceElement is XSLAttributeSet)
                {
                    XSLAttributeSet t = (XSLAttributeSet)decl.SourceElement;
                    if (t.AttributeSetName.Equals(name))
                    {
                        list.Add(decl);
                    }
                }
            }
        }

        public virtual void IndexMode(ComponentDeclaration decl)
        {
            XSLMode sourceMode = (XSLMode)decl.SourceElement;
            StructuredQName modeName = sourceMode.GetObjectName();
            if (modeName == null)
            {
                return; // Not a named mode
            }

            SymbolicName sName = new SymbolicName(StandardNames.XSL_MODE, modeName);

            // see if there is already a named mode with this precedence
            Mode other = GetStylesheetPackage().GetRuleManager().ObtainMode(modeName, false);
            if (other != null && other.GetDeclaringComponent().DeclaringPackage != GetStylesheetPackage())
            {
                sourceMode.CompileError("Mode " + sName.ComponentName.DisplayName + " conflicts with a public mode declared in package " + other.GetDeclaringComponent().DeclaringPackage.PackageName, "XTSE3050");
            }
        }

        public virtual bool CheckAcceptableModeForPackage(XSLTemplate template, Mode mode)
        {
            StylesheetPackage templatePack = template.GetPackageData();
            if (mode.GetDeclaringComponent() == null)
            {
                return true;
            }

            StylesheetPackage modePack = mode.GetDeclaringComponent().DeclaringPackage;
            if (templatePack != modePack)
            {
                NodeInfo parent = template.GetParent();
                bool bad = false;
                if (!(parent is XSLOverride))
                {
                    bad = true;
                }
                else
                {
                    NodeInfo grandParent = parent.GetParent();
                    if (!(grandParent is XSLUsePackage))
                    {
                        bad = true;
                    }
                    else
                    {
                        SymbolicName modeName = mode.GetSymbolicName();
                        Component.M usedMode = (Component.M)((XSLUsePackage)grandParent).UsedPackage.GetComponent(modeName);
                        if (usedMode == null)
                        {
                            bad = true;
                        }
                        else if (usedMode.GetVisibility() == Visibility.FINAL)
                        {
                            bad = true;
                        }
                    }
                }

                if (bad)
                {
                    template.CompileError("A template rule cannot be added to a mode declared in a used package " + "unless the xsl:template declaration appears within an xsl:override child of the appropriate xsl:use-package element", "XTSE3050");
                    return false;
                }
            }

            return true;
        }

        private void CheckForSchemaAwareness()
        {
            Compilation compilation = RootElement.GetCompilation();
            if (!compilation.IsSchemaAware() && GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XSLT))
            {
                foreach (ComponentDeclaration decl in topLevel)
                {
                    StyleElement node = decl.SourceElement;
                    if (node is XSLImportSchema)
                    {
                        compilation.SetSchemaAware(true);
                        return;
                    }
                }
            }
        }

        public virtual AccumulatorRegistry GetAccumulatorManager()
        {
            return accumulatorManager;
        }

        public virtual void SetAccumulatorManager(AccumulatorRegistry accumulatorManager)
        {
            this.accumulatorManager = accumulatorManager;
            stylesheetPackage.AccumulatorRegistry = accumulatorManager;
        }

        public virtual void AddNamespaceAlias(ComponentDeclaration node)
        {
            namespaceAliasList.Add(node);
            numberOfAliases++;
        }

        public virtual NamespaceBinding GetNamespaceAlias(NamespaceUri uri)
        {
            return namespaceAliasMap.GetOrDefault(uri);
        }

        public virtual bool IsAliasResultNamespace(NamespaceUri uri)
        {
            return aliasResultUriSet.Contains(uri);
        }

        private void CollectNamespaceAliases()
        {
            namespaceAliasMap = new Dictionary<NamespaceUri, NamespaceBinding>(numberOfAliases);
            aliasResultUriSet = new HashSet<NamespaceUri>();
            HashSet<NamespaceUri> aliasesAtThisPrecedence = new HashSet<NamespaceUri>();
            int currentPrecedence = -1;

            // Note that we are processing the list in reverse stylesheet order,
            // that @is, highest precedence first.
            for (int i = 0; i < numberOfAliases; i++)
            {
                ComponentDeclaration decl = namespaceAliasList[i];
                XSLNamespaceAlias xna = (XSLNamespaceAlias)decl.SourceElement;
                NamespaceUri scode = xna.StylesheetURI;
                NamespaceBinding resultBinding = xna.ResultNamespaceBinding;
                int prec = decl.Precedence;

                // check that there isn't a conflict with another xsl:namespace-alias
                // at the same precedence
                if (currentPrecedence != prec)
                {
                    currentPrecedence = prec;
                    aliasesAtThisPrecedence.Clear(); //precedenceBoundary = i;
                }

                if (aliasesAtThisPrecedence.Contains(scode))
                {
                    if (!namespaceAliasMap.GetOrDefault(scode).GetNamespaceUri().Equals(resultBinding.GetNamespaceUri()))
                    {
                        xna.CompileError("More than one alias is defined for the same namespace", "XTSE0810");
                    }
                }

                if (namespaceAliasMap.GetOrDefault(scode) == null)
                {
                    namespaceAliasMap[scode] = resultBinding;
                    aliasResultUriSet.Add(resultBinding.GetNamespaceUri());
                }

                aliasesAtThisPrecedence.Add(scode);
            }

            namespaceAliasList = null; // throw it in the garbage
        }

        public virtual bool HasNamespaceAliases()
        {
            return numberOfAliases > 0;
        }

        public virtual Properties GatherOutputProperties(StructuredQName formatQName)
        {
            bool found = formatQName == null;
            Configuration config = GetConfiguration();
            Properties details = new Properties(config.DefaultSerializationProperties);
            Dictionary<string, int> precedences = new Dictionary<string, int>(10);
            for (int i = topLevel.Count - 1; i >= 0; i--)
            {
                ComponentDeclaration decl = topLevel[i];
                if (decl.SourceElement is XSLOutput)
                {
                    XSLOutput xo = (XSLOutput)decl.SourceElement;
                    if (formatQName == null ? xo.FormatQName == null : formatQName.Equals(xo.FormatQName))
                    {
                        found = true;
                        xo.GatherOutputProperties(details, precedences, decl.Precedence);
                    }
                }
            }

            if (!found)
            {
                CompileError("Requested output format " + formatQName.DisplayName + " has not been defined", "XTDE1460");
            }

            return details;
        }

        public virtual void Compile(Compilation compilation)
        {
            try
            {
                Timer timer = compilation.timer;

                //PreparedStylesheet pss = getPreparedStylesheet();
                Configuration config = GetConfiguration();

                // If any XQuery functions were imported, fix up all function calls
                // registered against these functions.
                XQueryFunctionLibrary queryFunctions = stylesheetPackage.GetXQueryFunctionLibrary();
                foreach (XQueryFunction f in queryFunctions.FunctionDefinitions)
                {
                    f.FixupReferences();
                }

                if (Compilation.TIMING)
                {
                    timer.Report("fixup Query functions");
                }


                // Register all modes with the rule manager
                bool allowImplicit = !GetStylesheetPackage().IsDeclaredModes();
                foreach (ComponentDeclaration decl in topLevel)
                {
                    StyleElement snode = decl.SourceElement;
                    if (snode is XSLMode)
                    {
                        GetRuleManager().ObtainMode(snode.GetObjectName(), true);
                    }

                    if (allowImplicit)
                    {
                        RegisterImplicitModes(snode, GetRuleManager());
                    }
                }

                GetRuleManager().CheckConsistency();

                // Register template rules with the rule manager
                foreach (ComponentDeclaration decl in topLevel)
                {
                    StyleElement snode = decl.SourceElement;
                    if (snode is XSLTemplate)
                    {
                        ((XSLTemplate)snode).Register(decl);
                    }

                    if (snode is XSLMode)
                    {

                        // XSLT 4.0 enclosing modes
                        foreach (NodeInfo n in snode.Children())
                        {
                            if (n is XSLTemplate)
                            {
                                ((XSLTemplate)n).Register(decl);
                            }
                        }
                    }
                }

                if (Compilation.TIMING)
                {
                    timer.Report("register templates");
                }


                // Adjust the visibility of components based on xsl:expose declarations
                AdjustExposedVisibility();
                if (Compilation.TIMING)
                {
                    timer.Report("adjust exposed visibility");
                }


                // Call compile method for each top-level object in the stylesheet
                // Note, some declarations (templates) need to be compiled repeatedly if the module
                // is imported repeatedly; others (variables, functions) do not
                foreach (ComponentDeclaration decl in topLevel)
                {
                    StyleElement snode = decl.SourceElement;
                    if (!snode.IsActionCompleted(StyleElement.ACTION_COMPILE))
                    {
                        snode.SetActionCompleted(StyleElement.ACTION_COMPILE);
                        snode.CompileDeclaration(compilation, decl);
                    }
                }

                if (Compilation.TIMING)
                {
                    timer.Report("compile top-level objects (" + topLevel.Count + ")");
                }


                // Call type-check method for each user-defined function in the stylesheet. This is no longer
                // done during the optimize step, to avoid functions being inlined before they are type-checked.
                foreach (ComponentDeclaration decl in functionIndex.Values)
                {
                    StyleElement node = decl.SourceElement;
                    if (!node.IsActionCompleted(StyleElement.ACTION_TYPECHECK))
                    {
                        node.SetActionCompleted(StyleElement.ACTION_TYPECHECK);
                        if (node.GetVisibility() != Visibility.ABSTRACT)
                        {
                            ((XSLFunction)node).GetCompiledFunction().TypeCheck(node.MakeExpressionVisitor());
                        }
                    }
                }

                if (Compilation.TIMING)
                {
                    timer.Report("typeCheck functions (" + functionIndex.Count + ")");
                }

                if (compilation.ErrorCount > 0)
                {

                    // not much point carrying on
                    return;
                }


                // Call optimize() method for each top-level declaration
                OptimizeTopLevel();
                if (Compilation.TIMING)
                {
                    timer.Report("optimize top level");
                }


                // optimize functions that aren't overridden
                foreach (ComponentDeclaration decl in functionIndex.Values)
                {
                    StyleElement node = decl.SourceElement;
                    if (!node.IsActionCompleted(StyleElement.ACTION_OPTIMIZE))
                    {
                        node.SetActionCompleted(StyleElement.ACTION_OPTIMIZE);
                        ((IStylesheetComponent)node).Optimize(decl);
                    }
                }

                if (Compilation.TIMING)
                {
                    timer.Report("optimize functions");
                }


                // Check consistency of decimal formats
                GetDecimalFormatManager().CheckConsistency();
                if (Compilation.TIMING)
                {
                    timer.Report("check decimal formats");
                }


                // Check consistency of modes
                RuleManager ruleManager = GetRuleManager();

                //ruleManager.checkConsistency();   Now done earlier
                ruleManager.ComputeRankings();
                if (!compilation.IsFallbackToNonStreaming())
                {
                    ruleManager.InvertStreamableTemplates();
                }

                if (config.ObtainOptimizer().IsOptionSet(OptimizerOptions.RULE_SET))
                {
                    ruleManager.OptimizeRules();
                }

                if (Compilation.TIMING)
                {
                    timer.Report("build template rule tables");
                }


                // Build a run-time function library. This supports the use of function-available()
                // with a dynamic argument, and extensions such as saxon:evaluate(). The run-time
                // function library differs from the compile-time function library in that both
                // the StylesheetFunctionLibrary's on the library list are replaced by equivalent
                // ExecutableFunctionLibrary's. This is to prevent the retaining of run-time links
                // to the stylesheet document tree.
                ExecutableFunctionLibrary overriding = new ExecutableFunctionLibrary(config);
                ExecutableFunctionLibrary underriding = new ExecutableFunctionLibrary(config);
                foreach (Component decl in stylesheetPackage.ComponentIndex.Values)
                {
                    if (decl.GetActor() is UserFunction)
                    {
                        UserFunction f = (UserFunction)decl.GetActor();
                        if (f.IsOverrideExtensionFunction())
                        {
                            overriding.AddFunction(f);
                        }
                        else
                        {
                            underriding.AddFunction(f);
                        }
                    }
                }

                GetStylesheetPackage().SetFunctionLibraryDetails(null, overriding, underriding);
                if (Compilation.TIMING)
                {
                    timer.Report("build runtime function tables");
                }


                // Allocate binding slots to named templates
                foreach (ComponentDeclaration decl in topLevel)
                {
                    StyleElement inst = decl.SourceElement;
                    if (inst is XSLTemplate)
                    {
                        NamedTemplate proc = ((XSLTemplate)inst).GetActor();
                        if (proc != null && proc.TemplateName == null)
                        {
                            proc.AllocateAllBindingSlots(stylesheetPackage);
                        }
                    }
                }

                if (Compilation.TIMING)
                {
                    timer.Report("allocate binding slots to named templates");
                }


                // Allocate binding slots to component reference expressions
                Dictionary<SymbolicName, Component> componentIndex = stylesheetPackage.ComponentIndex;
                foreach (Component decl in componentIndex.Values)
                {
                    Actor proc = decl.GetActor();
                    if (proc != null)
                    {
                        proc.AllocateAllBindingSlots(stylesheetPackage);
                    }
                }

                if (Compilation.TIMING)
                {
                    timer.Report("allocate binding slots to component references");
                }


                // Allocate binding slots in key definitions
                KeyManager keyMan = GetKeyManager();
                foreach (KeyDefinitionSet keySet in keyMan.AllKeyDefinitionSets)
                {
                    foreach (KeyDefinition keyDef in keySet.KeyDefinitions)
                    {
                        keyDef.MakeDeclaringComponent(Visibility.PRIVATE, GetStylesheetPackage());
                        keyDef.AllocateAllBindingSlots(stylesheetPackage);
                    }
                }

                if (Compilation.TIMING)
                {
                    timer.Report("allocate binding slots to key definitions");
                }


                // Allocate binding slots in accumulators
                AccumulatorRegistry accMan = GetAccumulatorManager();
                if (accMan != null)
                {
                    foreach (Accumulator acc in accMan.AllAccumulators)
                    {
                        acc.AllocateAllBindingSlots(stylesheetPackage);
                    }
                }

                if (Compilation.TIMING)
                {
                    timer.Report("allocate binding slots to accumulators");
                }
            }
            catch (Exception err)
            {

                // if syntax errors were reported earlier, then exceptions may occur during this phase
                // due to inconsistency of data structures. We can ignore these exceptions as they
                // will go away when the user corrects the stylesheet
                if (compilation.ErrorCount == 0)
                {

                    // rethrow the exception (bare throw preserves the original stack -- Java rethrow semantics)
                    throw;
                }
            }
        }

        private void RegisterImplicitModes(StyleElement element, RuleManager manager)
        {
            if (element is XSLApplyTemplates || element is XSLTemplate)
            {
                string modeAtt = element.GetAttributeValue("mode");
                if (modeAtt != null)
                {
                    string[] tokens = Whitespace.Trim(modeAtt).SplitRegex("[ \t\n\r]+");
                    foreach (string s in tokens)
                    {
                        if (!s.StartsWith("#", StringComparison.Ordinal))
                        {
                            StructuredQName modeName = element.MakeQName(s, null, "mode");
                            SymbolicName sName = new SymbolicName(StandardNames.XSL_MODE, modeName);
                            Dictionary<SymbolicName, Component> componentIndex = GetStylesheetPackage().ComponentIndex;
                            Component existing = componentIndex.GetOrDefault(sName);
                            if (existing != null && existing.DeclaringPackage != GetStylesheetPackage())
                            {
                                if (element is XSLTemplate && !(element.GetParent() is XSLOverride))
                                {
                                    element.CompileError("A template rule cannot be added to a mode declared in a used package " + "unless the xsl:template declaration appears within an xsl:override child of the appropriate xsl:use-package element", "XTSE3050");
                                }
                            }
                            else
                            {
                                manager.ObtainMode(modeName, true);
                            }
                        }
                    }
                }
            }

            NodeInfo child;
            IAxisIterator kids = element.IterateAxis(AxisInfo.CHILD);
            while ((child = kids.Next()) != null)
            {
                if (child is StyleElement)
                {
                    RegisterImplicitModes((StyleElement)child, manager);
                }
            }
        }

        public virtual void OptimizeTopLevel()
        {

            // Call optimize method for each top-level object in the stylesheet
            // But for functions, do it only for those of highest precedence.
            foreach (ComponentDeclaration decl in topLevel)
            {
                StyleElement node = decl.SourceElement;
                if (node is IStylesheetComponent && !(node is XSLFunction) && !node.IsActionCompleted(StyleElement.ACTION_OPTIMIZE))
                {
                    node.SetActionCompleted(StyleElement.ACTION_OPTIMIZE);
                    ((IStylesheetComponent)node).Optimize(decl);
                }

                if (node is XSLTemplate)
                {
                    ((XSLTemplate)node).AllocatePatternSlotNumbers();
                }
            }
        }

        public virtual bool IsImportedSchema(NamespaceUri targetNamespace)
        {
            return stylesheetPackage.SchemaNamespaces.Contains(targetNamespace);
        }

        public virtual void AddImportedSchema(NamespaceUri targetNamespace)
        {
            stylesheetPackage.SchemaNamespaces.Add(targetNamespace);
        }

        public virtual ComponentDeclaration GetCharacterMap(StructuredQName name)
        {
            for (int i = topLevel.Count - 1; i >= 0; i--)
            {
                ComponentDeclaration decl = topLevel[i];
                if (decl.SourceElement is XSLCharacterMap)
                {
                    XSLCharacterMap t = (XSLCharacterMap)decl.SourceElement;
                    if (t.CharacterMapName.Equals(name))
                    {
                        return decl;
                    }
                }
            }

            return null;
        }

        public virtual void AdjustExposedVisibility()
        {
            IList<XSLExpose> exposeDeclarations = new List<XSLExpose>(); // xsl:expose declarations in reverse order
            foreach (ComponentDeclaration decl in topLevel)
            {
                if (decl.SourceElement is XSLExpose)
                {
                    exposeDeclarations.Insert(0,(XSLExpose)decl.SourceElement);
                }
            }

            if (exposeDeclarations.Count == 0)
            {
                return;
            }

            NamePool pool = GetConfiguration().GetNamePool();
            Dictionary<SymbolicName, Component> componentIndex = stylesheetPackage.ComponentIndex;
            foreach (Component component in componentIndex.Values)
            {
                int fp = component.ComponentKind;
                if (fp == StandardNames.XSL_MODE && ((Mode)component.GetActor()).IsUnnamedMode())
                {
                    continue;
                }

                ComponentTest exactNameTest = new ComponentTest(fp, new NameTest(Types.Type.ELEMENT, new FingerprintedQName(component.GetActor().ComponentName, pool), pool), -1);
                ComponentTest exactFunctionTest = null;
                if (fp == StandardNames.XSL_FUNCTION)
                {
                    IFunctionItem fn = (IFunctionItem)component.GetActor();
                    exactFunctionTest = new ComponentTest(fp, new NameTest(Types.Type.ELEMENT, new FingerprintedQName(fn.GetFunctionName(), pool), pool), fn.GetArity());
                }

                bool matched = false;
                foreach (XSLExpose exposure in exposeDeclarations)
                {
                    HashSet<ComponentTest> explicitComponentTests = exposure.ExplicitComponentTests;
                    if (explicitComponentTests.Contains(exactNameTest) || (exactFunctionTest != null && explicitComponentTests.Contains(exactFunctionTest)))
                    {
                        component.SetVisibility(exposure.GetVisibility(), VisibilityProvenance.EXPOSED);
                        matched = true;
                        break;
                    }
                }

                if (!matched && component.GetVisibilityProvenance() == VisibilityProvenance.DEFAULTED)
                {
                    matched = LookForMatchingWildcard(exposeDeclarations, component, matched);
                    if (!matched)
                    {
                        LookForAnyWildcard(exposeDeclarations, component);
                    }
                }
            }
        }

        private void LookForAnyWildcard(IList<XSLExpose> exposeDeclarations, Component component)
        {
            foreach (XSLExpose exposure in exposeDeclarations)
            {
                foreach (ComponentTest test in exposure.WildcardComponentTests)
                {
                    if (test.Matches(component.GetActor()))
                    {
                        if (exposure.GetVisibility() == Visibility.ABSTRACT && component.GetVisibility() != Visibility.ABSTRACT)
                        {
                            throw new XPathException("The non-abstract component " + component.GetActor().GetSymbolicName() + " cannot be made abstract by means of xsl:expose", "XTSE3025").WithLocation(exposure);
                        }

                        component.SetVisibility(exposure.GetVisibility(), VisibilityProvenance.EXPOSED);
                        return;
                    }
                }
            }
        }

        private bool LookForMatchingWildcard(IList<XSLExpose> exposeDeclarations, Component component, bool matched)
        {

            // Look for a matching wildcard
            foreach (XSLExpose exposure in exposeDeclarations)
            {
                foreach (ComponentTest test in exposure.WildcardComponentTests)
                {
                    if (test.IsPartialWildcard() && test.Matches(component.GetActor()))
                    {
                        if (exposure.GetVisibility() == Visibility.ABSTRACT && component.GetVisibility() != Visibility.ABSTRACT)
                        {
                            throw new XPathException("The non-abstract component " + component.GetActor().GetSymbolicName() + " cannot be made abstract by means of xsl:expose", "XTSE3025").WithLocation(exposure);
                        }

                        component.SetVisibility(exposure.GetVisibility(), VisibilityProvenance.EXPOSED);
                        return true;
                    }
                }
            }

            return matched;
        }

        public virtual void CompileError(string message, string errorCode)
        {
            XPathException tce = new XPathException(message, errorCode);
            CompileError(tce);
        }

        public virtual void CompileError(XPathException error)
        {
            error.SetIsStaticError(true);
            RootElement.CompileError(error);
        }

        public virtual void Fixup()
        {

            // Perform the fixup actions
            foreach (IAction a in fixupActions)
            {
                a.DoAction();
            }
        }

        public virtual void Complete()
        {
            stylesheetPackage.Complete();
        }

        public virtual SlotManager GetSlotManager()
        {
            return null;
        }

        public GlobalVariable GetEquivalentVariable(Expression select)
        {
            return null; // implemented in Saxon-EE
        }

        public void AddGlobalVariable(GlobalVariable variable)
        {
            AddGlobalVariable(variable, Visibility.PRIVATE);
        }

        public virtual void AddGlobalVariable(GlobalVariable variable, Visibility visibility)
        {
            Component component = variable.MakeDeclaringComponent(visibility, GetStylesheetPackage());
            if (variable.GetPackageData() == null)
            {
                variable.SetPackageData(stylesheetPackage);
            }

            if (visibility == Visibility.HIDDEN)
            {
                stylesheetPackage.AddHiddenComponent(component);
            }
            else
            {
                stylesheetPackage.ComponentIndex[new SymbolicName(StandardNames.XSL_VARIABLE, variable.GetVariableQName())] = component;
            }
        }
    }
}
