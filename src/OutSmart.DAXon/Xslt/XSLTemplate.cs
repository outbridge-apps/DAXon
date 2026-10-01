////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:template element in the style sheet.
    /// </summary>
    public sealed class XSLTemplate : StyleElement, IStylesheetComponent
    {
        private readonly object syncLock = new object();
        private string matchAtt = null;
        private string modeAtt = null;
        private string nameAtt = null;
        private string priorityAtt = null;
        private string asAtt = null;
        private string visibilityAtt = null;
        private StructuredQName[] modeNames;
        private string diagnosticId;
        private Patterns.Pattern match;
        private bool prioritySpecified;
        private double priority;
        private SlotManager stackFrameMap;
        private NamedTemplate compiledNamedTemplate;
        private readonly IList<TemplateRule> compiledTemplateRules = new List<TemplateRule>();
        private SequenceType requiredType = SequenceType.ANY_SEQUENCE;
        private bool declaresRequiredType = false;
        private Visibility visibility = Visibility.PRIVATE;
        private ItemType requiredContextItemType = AnyItemType.GetInstance();
        private bool mayOmitContextItem = true;
        private bool absentFocus = false;
        private bool jitCompilationDone = false;
        private bool explaining;
        private IList<Patterns.Pattern> subPatterns;

        public StructuredQName TemplateName
        {
            get
            {
                if (GetObjectName() == null)
                {

                    // allow for forwards references
                    string nameAtt = GetAttributeValue(NamespaceUri.NULL, "name");
                    if (nameAtt != null)
                    {
                        SetObjectName(MakeQName(nameAtt, null, "name"));
                    }
                }

                return GetObjectName();
            }
        }

        public StructuredQName[] ModeNames
        {
            get
            {
                if (modeNames == null)
                {

                    // modeAtt is a space-separated list of mode names, or "#default", or "#all"
                    if (modeAtt == null)
                    {
                        modeAtt = GetAttributeValue("mode");
                        if (modeAtt == null)
                        {
                            modeAtt = "#default";
                        }
                    }

                    bool allModes = false;
                    string[] tokens = Whitespace.Trim(modeAtt).SplitRegex("[ \t\n\r]+");
                    int count = tokens.Length;
                    modeNames = new StructuredQName[count];
                    count = 0;
                    foreach (string s in tokens)
                    {
                        StructuredQName mname;
                        if ("#default".Equals(s))
                        {
                            mname = DefaultMode;
                            if (mname == null)
                            {
                                mname = Mode.UNNAMED_MODE_NAME;
                            }
                        }
                        else if ("#unnamed".Equals(s))
                        {
                            mname = Mode.UNNAMED_MODE_NAME;
                        }
                        else if ("#all".Equals(s))
                        {
                            allModes = true;
                            mname = Mode.OMNI_MODE_NAME;
                        }
                        else
                        {
                            mname = MakeQName(s, "XTSE0550", "mode");
                        }

                        for (int e = 0; e < count; e++)
                        {
                            if (modeNames[e].Equals(mname))
                            {
                                CompileError("In the list of modes, the value " + s + " is duplicated", "XTSE0550");
                            }
                        }

                        modeNames[count++] = mname;
                    }

                    if (allModes && (count > 1))
                    {
                        CompileError("mode='#all' cannot be combined with other modes", "XTSE0550");
                    }
                }

                return modeNames;
            }
        }

        public HashSet<Mode> ApplicableModes
        {
            get
            {
                StructuredQName[] names = ModeNames;
                HashSet<Mode> modes = new HashSet<Mode>();
                RuleManager mgr = GetPrincipalStylesheetModule().GetRuleManager();
                foreach (StructuredQName name in names)
                {
                    if (name.Equals(Mode.OMNI_MODE_NAME))
                    {
                        modes.Add(mgr.UnnamedMode);
                        modes.AddRange(mgr.AllNamedModes);
                    }
                    else
                    {
                        Mode mode = mgr.ObtainMode(name, false);
                        if (mode != null)
                        {
                            modes.Add(mode);
                        }
                    }
                }

                return modes;
            }
        }

        public ItemType ContextItemTypeForTemplateRule
        {
            get
            {
                Configuration config = GetConfiguration();
                ItemType contextItemType = match.GetItemType();
                if (contextItemType.Equals(ErrorType.GetInstance()))
                {

                    // if the match pattern can't match anything, we produce a warning, not a hard error
                    contextItemType = AnyItemType.GetInstance();
                }

                if (requiredContextItemType != AnyItemType.GetInstance())
                {
                    Affinity rel = config.GetTypeHierarchy().Relationship(contextItemType, requiredContextItemType);
                    switch (rel)
                    {
                        case Affinity.DISJOINT:
                            XPathException e = new XPathException("The declared context item type is inconsistent with the match pattern", "XTTE0590", this);
                            e.SetIsTypeError(true);
                            throw e;
                        case Affinity.SUBSUMED_BY:
                        case Affinity.OVERLAPS:
                        case Affinity.SAME_TYPE:

                            // no action
                            break;
                        case Affinity.SUBSUMES:
                            contextItemType = requiredContextItemType;
                            break;
                    }
                }

                return contextItemType;
            }
        }

        public NamedTemplate CompiledNamedTemplate => compiledNamedTemplate;

        public Patterns.Pattern Match => match; //    public IMap<StructuredQName, TemplateRule> getTemplateRulesByMode() {
        public NamedTemplate GetActor()
        {
            return compiledNamedTemplate;
        }

        public override void SetCompilation(Compilation compilation)
        {
            base.SetCompilation(compilation); //compiledNamedTemplate.setPackageData(compilation.getPackageData());
        }

        public override bool IsDeclaration()
        {
            return true;
        }

        public bool IsDeferredCompilation(Compilation compilation)
        {
            return compilation.IsPreScan() && TemplateName == null && !compilation.IsLibraryPackage();
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override bool MayContainParam()
        {
            return true;
        }

        public override bool IsWithinDeclaredStreamableConstruct()
        {
            try
            {
                foreach (Mode m in ApplicableModes)
                {
                    if (m.IsDeclaredStreamable())
                    {
                        return true;
                    }
                }
            }
            catch (XPathException e)
            {
                return false;
            }

            return false;
        }

        public void SetContextItemRequirements(ItemType type, bool mayBeOmitted, bool absentFocus)
        {
            requiredContextItemType = type;
            mayOmitContextItem = mayBeOmitted;
            this.absentFocus = absentFocus;
        }

        protected override bool IsPermittedChild(StyleElement child)
        {
            return child is XSLLocalParam || child.Fingerprint == StandardNames.XSL_CONTEXT_ITEM;
        }

        public SymbolicName GetSymbolicName()
        {
            if (TemplateName == null)
            {
                return null;
            }
            else
            {
                return new SymbolicName(StandardNames.XSL_TEMPLATE, TemplateName);
            }
        }

        public ItemType GetRequiredContextItemType()
        {
            return requiredContextItemType;
        }

        public bool IsMayOmitContextItem()
        {
            return mayOmitContextItem;
        }

        public void CheckCompatibility(Component component)
        {
            NamedTemplate other = (NamedTemplate)component.GetActor();
            if (!object.Equals(GetSymbolicName(), other.GetSymbolicName()))
            {
                throw new ArgumentException();
            }

            SequenceType req = requiredType == null ? SequenceType.ANY_SEQUENCE : requiredType;
            if (!req.Equals(other.RequiredType))
            {
                CompileError("The overriding template has a different required type from the overridden template", "XTSE3070");
                return;
            }

            if (!requiredContextItemType.Equals(other.GetRequiredContextItemType()) || mayOmitContextItem != other.IsMayOmitContextItem() || absentFocus != other.IsAbsentFocus())
            {
                CompileError("The required context item for the overriding template differs from that of the overridden template", "XTSE3070");
                return;
            }

            IList<NamedTemplate.LocalParamInfo> otherParams = other.LocalParamDetails;
            HashSet<StructuredQName> overriddenParams = new HashSet<StructuredQName>();
            foreach (NamedTemplate.LocalParamInfo lp0 in otherParams)
            {
                XSLLocalParam lp1 = GetParam(lp0.name);
                if (lp1 == null)
                {
                    if (!lp0.isTunnel)
                    {
                        CompileError("The overridden template declares a parameter " + lp0.name.DisplayName + " which is not declared in the overriding template", "XTSE3070");
                    }

                    return;
                }

                if (!lp1.GetRequiredType().Equals(lp0.requiredType))
                {
                    lp1.CompileError("The parameter " + lp0.name.DisplayName + " has a different required type in the overridden template", "XTSE3070");
                    return;
                }

                if (lp1.IsRequiredParam() != lp0.isRequired && !lp0.isTunnel)
                {
                    lp1.CompileError("The parameter " + lp0.name.DisplayName + " is " + (lp1.IsRequiredParam() ? "required" : "optional") + " in the overriding template, but " + (lp0.isRequired ? "required" : "optional") + " in the overridden template", "XTSE3070");
                    return;
                }

                if (lp1.IsTunnelParam() != lp0.isTunnel)
                {
                    lp1.CompileError("The parameter " + lp0.name.DisplayName + " is a " + (lp1.IsTunnelParam() ? "tunnel" : "non-tunnel") + " parameter in the overriding template, but " + (lp0.isTunnel ? "tunnel" : "non-tunnel") + " parameter in the overridden template", "XTSE3070");
                    return;
                }

                overriddenParams.Add(lp0.name);
            }

            foreach (NodeInfo param in Children(new TypeIsInstancePredicate(typeof(XSLLocalParam))))
            {
                if (!overriddenParams.Contains(((XSLLocalParam)param).GetObjectName()) && ((XSLLocalParam)param).IsRequiredParam())
                {
                    ((XSLLocalParam)param).CompileError("An overriding template cannot introduce a required parameter that is not declared in the overridden template", "XTSE3070");
                }
            }
        }

        public XSLLocalParam GetParam(StructuredQName name)
        {
            foreach (NodeInfo param in Children(new TypeIsInstancePredicate(typeof(XSLLocalParam))))
            {
                if (name.Equals(((XSLLocalParam)param).GetObjectName()))
                {
                    return (XSLLocalParam)param;
                }
            }

            return null;
        }

        public override void PrepareAttributes()
        {
            IAttributeMap atts = Attributes();
            string extraAsAtt = null;
            foreach (AttributeInfo att in atts)
            {
                INodeName name = att.GetNodeName();
                string f = name.DisplayName;
                if (f.Equals("mode"))
                {
                    modeAtt = Whitespace.Trim(att.Value);
                }
                else if (f.Equals("name"))
                {
                    nameAtt = Whitespace.Trim(att.Value);
                }
                else if (f.Equals("match"))
                {
                    matchAtt = att.Value;
                }
                else if (f.Equals("priority"))
                {
                    priorityAtt = Whitespace.Trim(att.Value);
                }
                else if (f.Equals("as"))
                {
                    asAtt = att.Value;
                }
                else if (f.Equals("visibility"))
                {
                    visibilityAtt = Whitespace.Trim(att.Value);
                }
                else if (name.HasURI(NamespaceUri.SAXON))
                {
                    IsExtensionAttributeAllowed(name.DisplayName);
                    if (name.GetLocalPart().Equals("as"))
                    {
                        extraAsAtt = att.Value;
                    }
                    else if (name.GetLocalPart().Equals("explain"))
                    {
                        explaining = IsYes(Whitespace.Trim(att.Value));
                    }
                }
                else
                {
                    CheckUnknownAttribute(name);
                }
            }

            try
            {
                if (modeAtt == null)
                {
                    if (matchAtt != null)
                    {

                        // XSLT 3.0 allows the default mode to be specified on any element
                        StructuredQName defaultMode = DefaultMode;
                        if (defaultMode == null)
                        {
                            defaultMode = Mode.UNNAMED_MODE_NAME;
                        }

                        modeNames = new StructuredQName[1];
                        modeNames[0] = defaultMode;
                    }
                }
                else
                {
                    if (matchAtt == null)
                    {
                        CompileError("The mode attribute must be absent if the match attribute is absent", "XTSE0500");
                    }
                }
            }
            catch (XPathException err)
            {
                XPathException e2 = err.ReplacingErrorCode("XTSE0020", "XTSE0550");
                e2.MaybeSetErrorCode("XTSE0280");
                e2.SetIsStaticError(true);
                CompileError(e2);
            }

            if (nameAtt != null)
            {
                if (GetObjectName() == null)
                {
                    StructuredQName qName = MakeQName(nameAtt, "XTSE0280", "name");
                    SetObjectName(qName);
                }

                if (compiledNamedTemplate != null)
                {
                    compiledNamedTemplate.TemplateName = GetObjectName();
                }

                diagnosticId = nameAtt;
            }

            prioritySpecified = priorityAtt != null;
            if (prioritySpecified)
            {
                if (matchAtt == null)
                {
                    CompileError("The priority attribute must be absent if the match attribute is absent", "XTSE0500");
                }

                try
                {

                    // it's got to be a valid decimal, but we want it as a double, so parse it twice
                    if (!BigDecimalValue.CastableAsDecimal(priorityAtt))
                    {
                        CompileError("Invalid numeric value for priority (" + priority + ')', "XTSE0530");
                    }

                    priority = double.Parse(priorityAtt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (FormatException err)
                {

                    // shouldn't happen
                    CompileError("Invalid numeric value for priority (" + priority + ')', "XTSE0530");
                }
            }

            if (matchAtt != null)
            {
                match = MakePattern(matchAtt, "match");
                if (diagnosticId == null)
                {
                    diagnosticId = "match=\"" + matchAtt + '"';
                    if (modeAtt != null)
                    {
                        diagnosticId += " mode=\"" + modeAtt + '"';
                    }
                }
            }

            if (match == null && nameAtt == null)
            {
                CompileError("xsl:template must have a name or match attribute (or both)", "XTSE0500");
            }

            if (asAtt != null)
            {
                try
                {
                    requiredType = MakeSequenceType(asAtt);
                    declaresRequiredType = true;
                }
                catch (XPathException e)
                {
                    CompileErrorInAttribute(e, "as");
                }
            }

            if (extraAsAtt != null)
            {
                SequenceType extraResultType;
                declaresRequiredType = true;
                try
                {
                    extraResultType = MakeExtendedSequenceType(extraAsAtt);
                }
                catch (XPathException e)
                {
                    CompileErrorInAttribute(e, "saxon:as");
                    extraResultType = requiredType; // error recovery
                }

                if (asAtt != null)
                {
                    Affinity rel = GetConfiguration().GetTypeHierarchy().SequenceTypeRelationship(extraResultType, requiredType);
                    if (rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMED_BY)
                    {
                        requiredType = extraResultType;
                    }
                    else
                    {
                        CompileErrorInAttribute("When both are present, @saxon:as must be a subtype of @as", "SXER7TBA", "saxon:as");
                    }
                }
                else
                {
                    requiredType = extraResultType;
                }
            }

            if (visibilityAtt != null)
            {
                visibility = InterpretVisibilityValue(visibilityAtt, "");
                if (nameAtt == null)
                {
                    CompileError("xsl:template/@visibility can be specified only if the template has a @name attribute", "XTSE0500");
                }
                else
                {
                    compiledNamedTemplate.DeclaredVisibility = GetVisibility();
                }
            }
        }

        public override void ProcessAllAttributes()
        {

            // With JIT compilation enabled, we don't process the attributes of descendant elements
            if (!IsDeferredCompilation(GetCompilation()))
            {
                base.ProcessAllAttributes(); //TODO - sort out the duplicated code. This repeats the code below
            }
            else
            {
                ProcessDefaultCollationAttribute();
                ProcessDefaultMode();
                staticContext = new ExpressionContext(this, null);
                ProcessAttributes();
            }
        }

        public bool AppliesToAllModes()
        {
            foreach (StructuredQName name in ModeNames)
            {
                if (name.Equals(Mode.OMNI_MODE_NAME))
                {
                    return true;
                }
            }

            return false;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            stackFrameMap = GetConfiguration().MakeSlotManager();
            StyleElement enclosingMode = null;
            NodeImpl parent = GetParent();
            if (GetCompilation().GetCompilerInfo().XsltVersion >= 40 && parent.Fingerprint == StandardNames.XSL_MODE)
            {
                enclosingMode = (StyleElement)parent;
            }

            if (enclosingMode == null)
            {
                CheckTopLevel("XTSE0010", true);
            }
            else
            {
                if (matchAtt == null)
                {
                    CompileError("A template rule enclosed within xsl:mode must have a match attribute", "XTSE4010");
                }

                if (modeAtt != null)
                {
                    CompileError("A template rule enclosed within xsl:mode must not have a mode attribute", "XTSE4010");
                }

                if (nameAtt != null)
                {
                    CompileError("A template rule enclosed within xsl:mode must not have a name attribute", "XTSE4010");
                }

                modeNames = new StructuredQName[1];
                modeNames[0] = ((XSLMode)GetParent()).GetObjectName();
            }


            // the check for duplicates is now done in the buildIndexes() method of XSLStylesheet
            if (match != null)
            {
                match = TypeCheck("match", match);
                if (match.GetItemType() is ErrorType)
                {
                    IssueWarning("Pattern will never match anything", DAXonErrorCode.SXWN9015);
                }

                if (GetPrincipalStylesheetModule().IsDeclaredModes())
                {
                    RuleManager manager = GetPrincipalStylesheetModule().GetRuleManager();
                    StructuredQName[] modes = ModeNames;
                    if (modes != null)
                    {
                        foreach (StructuredQName name in modes)
                        {
                            if (name.Equals(Mode.UNNAMED_MODE_NAME) && !manager.IsUnnamedModeExplicit())
                            {
                                CompileError("The unnamed mode has not been declared in an xsl:mode declaration", "XTSE3085");
                            }

                            if (manager.ObtainMode(name, false) == null)
                            {
                                CompileError("Mode name " + name.DisplayName + " has not been declared in an xsl:mode declaration", "XTSE3085");
                            }
                        }
                    }
                    else
                    {
                        if (!manager.IsUnnamedModeExplicit())
                        {
                            CompileError("The unnamed mode has not been declared in an xsl:mode declaration", "XTSE3085");
                        }
                    }
                }

                if (visibility == Visibility.ABSTRACT)
                {
                    CompileError("An abstract template must have no match attribute");
                }
            }

            bool hasContent = false;
            foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(StyleElement))))
            {
                if (!(child.Fingerprint == StandardNames.XSL_CONTEXT_ITEM || child.Fingerprint == StandardNames.XSL_PARAM))
                {
                    hasContent = true;
                    break;
                }
            }

            if (visibility == Visibility.ABSTRACT && hasContent)
            {
                CompileError("A template with visibility='abstract' must have no body");
            }


            // If the pattern is a union pattern and there is no priority specified, split into
            // multiple template rules so each can be given its own priority.
            if (match is UnionPattern)
            {
                subPatterns = new List<Patterns.Pattern>(2);
                if (prioritySpecified)
                {
                    subPatterns.Add(match);
                }
                else
                {
                    GatherSubPatterns(match, subPatterns);
                }
            }
            else if (match != null)
            {
                subPatterns = new List<Patterns.Pattern>(1);
                subPatterns.Add(match);
            }
        }

        private void GatherSubPatterns(Patterns.Pattern match, IList<Patterns.Pattern> subPatterns)
        {
            if (match is UnionPattern)
            {
                UnionPattern up = (UnionPattern)match;
                GatherSubPatterns(up.LHS, subPatterns);
                GatherSubPatterns(up.RHS, subPatterns);
            }
            else if (match is NodeTestPattern && match.GetItemType() is CombinedNodeTest && ((CombinedNodeTest)match.GetItemType()).Operator == Token.UNION)
            {
                CombinedNodeTest cnt = (CombinedNodeTest)match.GetItemType();
                NodeTest[] nt = cnt.ComponentNodeTests;
                NodeTestPattern nt0 = new NodeTestPattern(nt[0]);
                subPatterns.Add(nt0);
                ExpressionTool.CopyLocationInfo(match, nt0);
                NodeTestPattern nt1 = new NodeTestPattern(nt[1]);
                ExpressionTool.CopyLocationInfo(match, nt1);
                subPatterns.Add(nt1);
            }
            else
            {
                subPatterns.Add(match);
            }
        }

        public override void ValidateSubtree(ComponentDeclaration decl, bool excludeStylesheet)
        {
            if (!IsDeferredCompilation(GetCompilation()))
            {
                base.ValidateSubtree(decl, excludeStylesheet);
            }
            else
            {
                try
                {
                    Validate(decl);
                }
                catch (XPathException err)
                {
                    CompileError(err);
                }
            }
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            if (TemplateName != null)
            {
                if (compiledNamedTemplate == null)
                {
                    compiledNamedTemplate = new NamedTemplate(TemplateName, GetConfiguration());
                }

                top.IndexNamedTemplate(decl);
            }
        }

        /// <summary>
        /// Mark tail-recursive calls on templates and functions.
        /// </summary>
        public override bool MarkTailCalls()
        {
            StyleElement last = LastChildInstruction;
            return last != null && last.MarkTailCalls();
        }

        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
            if (IsDeferredCompilation(compilation))
            {
                CreateSkeletonTemplate(compilation, decl);

                return;
            }

            if (compilation.GetCompilerInfo().GetOptimizerOptions().IsSet(OptimizerOptions.TAIL_CALLS))
            {
                MarkTailCalls();
            }

            Expression body = CompileSequenceConstructor(compilation, decl, true);
            body.RestoreParentPointers();
            RetainedStaticContext rsc = MakeRetainedStaticContext();
            if (body.LocalRetainedStaticContext == null)
            {
                body.SetRetainedStaticContext(rsc); // bug 2608
            }

            if (match != null && compilation.GetConfiguration().GetBooleanProperty(Feature<bool>.STRICT_STREAMABILITY) && IsWithinDeclaredStreamableConstruct())
            {
                CheckStrictStreamability(body);
            }

            if (TemplateName != null)
            {
                CompileNamedTemplate(body);
            }

            if (match != null)
            {

                CompileTemplateRule(compilation, body);
            }
        }

        private void CheckStrictStreamability(Expression body)
        {
            GetConfiguration().CheckStrictStreamability(this, body);
        }

        private void CompileNamedTemplate(Expression body)
        {
            RetainedStaticContext rsc = body.GetRetainedStaticContext();
            compiledNamedTemplate.SetPackageData(rsc.GetPackageData());
            compiledNamedTemplate.SetBody(body);
            compiledNamedTemplate.SetStackFrameMap(stackFrameMap);
            compiledNamedTemplate.SetSystemId(GetSystemId());
            compiledNamedTemplate.SetLineNumber(GetLineNumber());
            compiledNamedTemplate.SetColumnNumber(GetColumnNumber());
            compiledNamedTemplate.RequiredType = requiredType;
            compiledNamedTemplate.SetContextItemRequirements(requiredContextItemType, mayOmitContextItem, absentFocus);
            compiledNamedTemplate.SetRetainedStaticContext(rsc);
            compiledNamedTemplate.DeclaredVisibility = DeclaredVisibility;
            Component overridden = OverriddenComponent;
            if (overridden != null)
            {
                CheckCompatibility(overridden);
            }

            ContextItemStaticInfo cisi = GetConfiguration().MakeContextItemStaticInfo(requiredContextItemType, mayOmitContextItem);
            Expression body2 = RefineTemplateBody(body, cisi);
            compiledNamedTemplate.SetBody(body2);
            if (GetCompilation().GetCompilerInfo().CodeInjector != null)
            {
                GetCompilation().GetCompilerInfo().CodeInjector.Process(compiledNamedTemplate);
            }
        }

        private Expression RefineTemplateBody(Expression body, ContextItemStaticInfo cisi)
        {
            Expression old = body;
            try
            {
                body = body.Simplify();
            }
            catch (XPathException e)
            {
                if (e.IsReportableStatically())
                {
                    CompileError(e);
                }
                else
                {
                    body = new ErrorExpression(new XmlProcessingException(e));
                    ExpressionTool.CopyLocationInfo(old, body);
                }
            }

            Configuration config = GetConfiguration();
            if (visibility != Visibility.ABSTRACT)
            {
                try
                {
                    if (requiredType != null && requiredType != SequenceType.ANY_SEQUENCE)
                    {
                        Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.TEMPLATE_RESULT, diagnosticId, 0, "XTTE0505");
                        body = config.GetTypeChecker(false).StaticTypeCheck(body, requiredType, role, MakeExpressionVisitor());
                    }
                }
                catch (XPathException err)
                {
                    if (err.IsReportableStatically())
                    {
                        CompileError(err);
                    }

                    body = new ErrorExpression(new XmlProcessingException(err));
                    ExpressionTool.CopyLocationInfo(old, body);
                }
            }

            try
            {
                ExpressionVisitor visitor = MakeExpressionVisitor();
                body = body.TypeCheck(visitor, cisi);
            }
            catch (XPathException e)
            {
                CompileError(e);
            }

            return body;
        }

        public void CompileTemplateRule(Compilation compilation, Expression body)
        {
            Configuration config = GetConfiguration();
            if (TemplateName != null)
            {

                // If this is both a named template and a template rule, treat both as separate
                body = body.Copy(new RebindingMap());
            }

            ItemType contextItemType;
            ContextItemStaticInfo cisi;

            // the template can't be called by name, so the context item must match the match pattern
            contextItemType = match.GetItemType();
            if (contextItemType.Equals(ErrorType.GetInstance()))
            {

                // if the match pattern can't match anything, we produce a warning, not a hard error
                contextItemType = AnyItemType.GetInstance();
            }

            cisi = config.MakeContextItemStaticInfo(contextItemType, mayOmitContextItem);
            body = RefineTemplateBody(body, cisi);
            bool first = true;
            foreach (TemplateRule rule in compiledTemplateRules)
            {
                if (first)
                {

                    rule.SetBody(body);
                    if (compilation.GetCompilerInfo().CodeInjector != null)
                    {
                        compilation.GetCompilerInfo().CodeInjector.Process(rule);
                        body = rule.GetBody();
                    }

                    first = false;
                }
                else
                {
                    if (rule.GetBody() == null)
                    {
                        body = body.Copy(new RebindingMap());
                        if (body is ComponentTracer)
                        {
                            ((ComponentTracer)body).SetProperty("match", rule.MatchPattern);
                        }
                    }
                    else
                    {
                        body = rule.GetBody();
                    }
                }

                SetCompiledTemplateRuleProperties(rule, body);
            }
        }

        private void CreateSkeletonTemplate(Compilation compilation, ComponentDeclaration decl)
        {
            foreach (TemplateRule templateRule in compiledTemplateRules)
            {
                templateRule.PrepareInitializer(compilation, decl);
                RetainedStaticContext rsc = MakeRetainedStaticContext();
                templateRule.SetPackageData(rsc.GetPackageData());
                SetCompiledTemplateRuleProperties(templateRule, null);
            }
        }

        private void SetCompiledTemplateRuleProperties(TemplateRule templateRule, Expression body)
        {

            templateRule.SetBody(body);
            templateRule.StackFrameMap = stackFrameMap;
            templateRule.SetSystemId(GetSystemId());
            templateRule.SetLineNumber(GetLineNumber());
            templateRule.SetColumnNumber(GetColumnNumber());
            templateRule.RequiredType = requiredType;
            templateRule.SetContextItemRequirements(requiredContextItemType, absentFocus);
        }

        public void JitCompile(Compilation compilation, ComponentDeclaration decl)
        {
            lock (syncLock)
            {
                if (!jitCompilationDone)
                {
                    jitCompilationDone = true;
                    compilation.SetPreScan(false);
                    ProcessAllAttributes();
                    CheckForJitCompilationErrors(compilation);
                    ValidateSubtree(decl, false);
                    CheckForJitCompilationErrors(compilation);
                    CompileDeclaration(compilation, decl);
                    CheckForJitCompilationErrors(compilation);
                }
            }
        }

        private void CheckForJitCompilationErrors(Compilation compilation)
        {
            if (compilation.ErrorCount > 0)
            {
                XPathException e = new XPathException("Errors were reported during JIT compilation of template rule with match=\"" + matchAtt + "\"", DAXonErrorCode.SXST0001, this);
                e.SetHasBeenReported(true); // only intended as an exception message, not something to report to ErrorListener
                throw e;
            }
        }

        public void Register(ComponentDeclaration declaration)
        {
            if (match != null)
            {
                StylesheetModule module = declaration.Module;
                RuleManager mgr = GetCompilation().GetPrincipalStylesheetModule().GetRuleManager();
                ExpressionVisitor visitor = ExpressionVisitor.Make(GetStaticContext());
                IEnumerable<StructuredQName> modeNames = ModeNames.ToList();
                if (AppliesToAllModes())
                {
                    modeNames = GetCompilation().AllKnownModeNames;
                }

                foreach (StructuredQName modeName in modeNames)
                {
                    Mode mode = mgr.ObtainMode(modeName, true);
                    if (AppliesToAllModes() && mode.IsEnclosingMode())
                    {
                        continue;
                    }

                    bool ok = GetPrincipalStylesheetModule().CheckAcceptableModeForPackage(this, mode);
                    if (!ok)
                    {
                        return;
                    }

                    if (mode.IsEnclosingMode() && !(GetParent() is XSLMode && mode == ((XSLMode)GetParent()).GetMode()))
                    {
                        CompileError("An xsl:template rule must not refer to a mode that contains enclosed template rules " + "unless it is itself enclosed by that xsl:mode declaration", "XTSE4020");
                    }

                    int part = 0;
                    int seq = mgr.AllocateSequenceNumber();
                    TemplateRule rule = GetConfiguration().MakeTemplateRule();
                    rule.SetMode(mode);

                    // Copy the match pattern: in the case where a template rule belongs to multiple modes,
                    // the binding vector for any references to external functions or variables belongs
                    // to the mode, and the slot numbers for these references will vary from one mode to another.
                    // Also, the mode/@typed attribute comes into play.
                    Patterns.Pattern localPattern = (Patterns.Pattern)match.Copy(new RebindingMap());
                    rule.MatchPattern = localPattern;
                    compiledTemplateRules.Add(rule);
                    if (mode.IsDeclaredStreamable())
                    {
                        rule.SetDeclaredStreamable(true);
                        if (!match.IsMotionless())
                        {
                            bool fallback = GetConfiguration().GetBooleanProperty(Feature<bool>.STREAMING_FALLBACK);
                            string message = "Template rule is declared streamable but the match pattern is not motionless";
                            if (fallback)
                            {
                                message += "\n  * Falling back to non-streaming implementation";
                                GetStaticContext().IssueWarning(message, DAXonErrorCode.SXWN9024, this);
                                rule.SetDeclaredStreamable(false);
                                GetCompilation().SetFallbackToNonStreaming(true);
                            }
                            else
                            {
                                throw new XPathException(message, "XTSE3430", this);
                            }
                        }
                    }

                    if (mode.DefaultResultType != null)
                    {
                        if (!declaresRequiredType)
                        {
                            rule.RequiredType = mode.DefaultResultType;
                        }
                        else
                        {
                            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                            Affinity aff = th.SequenceTypeRelationship(requiredType, mode.DefaultResultType);
                            if (aff != Affinity.SAME_TYPE && aff != Affinity.SUBSUMED_BY)
                            {
                                CompileError("Type declared in xsl:template/@as must be a subtype of the type declared in xsl:mode/@as", "XTSE4040");
                            }
                        }
                    }

                    if (subPatterns.Count == 1)
                    {
                        string typed = mode.ActivePart.GetPropertyValue("typed");
                        if ("strict".Equals(typed) || "lax".Equals(typed))
                        {
                            Patterns.Pattern localPattern2;
                            try
                            {
                                localPattern2 = localPattern.ConvertToTypedPattern(typed);
                            }
                            catch (XPathException e)
                            {
                                throw e.MaybeWithLocation(this);
                            }

                            if (localPattern2 != match)
                            {
                                ContextItemStaticInfo info = GetConfiguration().MakeContextItemStaticInfo(AnyItemType.GetInstance(), mayOmitContextItem);
                                ExpressionTool.CopyLocationInfo(match, localPattern2);
                                localPattern2.OriginalText = match.ToString();
                                localPattern2 = (Patterns.Pattern)localPattern2.TypeCheck(visitor, info);
                                rule.MatchPattern = localPattern2;
                            }
                        }

                        double prio = prioritySpecified ? priority : double.NaN;
                        mgr.RegisterRule(rule.MatchPattern, rule, mode, module, prio, seq, part++);
                    }
                    else
                    {
                        foreach (Patterns.Pattern subPattern in subPatterns)
                        {
                            Patterns.Pattern localSubPattern1 = (Patterns.Pattern)subPattern.Copy(new RebindingMap());
                            string typed = mode.ActivePart.GetPropertyValue("typed");
                            if ("strict".Equals(typed) || "lax".Equals(typed))
                            {
                                Patterns.Pattern localSubPattern2;
                                try
                                {
                                    localSubPattern2 = localSubPattern1.ConvertToTypedPattern(typed);
                                }
                                catch (XPathException e)
                                {
                                    throw e.MaybeWithLocation(this);
                                }

                                if (localSubPattern2 != localSubPattern1)
                                {
                                    ContextItemStaticInfo info = GetConfiguration().MakeContextItemStaticInfo(AnyItemType.GetInstance(), mayOmitContextItem);
                                    ExpressionTool.CopyLocationInfo(match, localSubPattern2);
                                    localSubPattern2.OriginalText = match.ToString();
                                    localSubPattern2 = (Patterns.Pattern)localSubPattern2.TypeCheck(visitor, info);
                                    localSubPattern1 = localSubPattern2;
                                }
                            }

                            double prio = prioritySpecified ? priority : double.NaN;
                            mgr.RegisterRule(localSubPattern1, rule, mode, module, prio, seq, part++);
                        }
                    }
                }
            }
        }

        public void AllocatePatternSlotNumbers()
        {
            if (match != null)
            {
                foreach (TemplateRule templateRule in compiledTemplateRules)
                {

                    // In the case of a union pattern, allocate slots separately for each branch
                    Patterns.Pattern match = templateRule.MatchPattern;

                    // first slot in pattern is reserved for current()
                    int nextFree = 0;
                    if ((match.Dependencies & StaticProperty.DEPENDS_ON_CURRENT_ITEM) != 0)
                    {
                        nextFree = 1;
                    }


                    int slots = match.AllocateSlots(GetSlotManager(), nextFree);

                    // if the pattern calls user-defined functions, allocate at least one slot,
                    // to force a new context to be created for evaluating patterns (bug 3706)
                    if (slots == 0 && ((match.Dependencies & StaticProperty.DEPENDS_ON_USER_FUNCTIONS) != 0))
                    {
                        slots = 1;
                    }

                    if (slots > 0)
                    {
                        templateRule.GetMode().ActivePart.AllocatePatternSlots(slots);
                    }
                }
            }
        }

        public void Optimize(ComponentDeclaration declaration)
        {
            Configuration config = GetConfiguration();
            if (compiledNamedTemplate != null)
            {
                Expression body = compiledNamedTemplate.GetBody();
                ContextItemStaticInfo cisi = GetConfiguration().MakeContextItemStaticInfo(requiredContextItemType, mayOmitContextItem);
                ExpressionVisitor visitor = MakeExpressionVisitor();
                body = body.TypeCheck(visitor, cisi);
                body = ExpressionTool.OptimizeComponentBody(body, GetCompilation(), visitor, cisi, true);
                compiledNamedTemplate.SetBody(body);
                AllocateLocalSlots(body);
                if (explaining)
                {
                    Logger err = GetConfiguration().Logger;
                    err.Info("Optimized expression tree for named template at line " + GetLineNumber() + " in " + GetSystemId() + ':');
                    body.Explain(err);
                }

                body.RestoreParentPointers();
            }

            if (match != null)
            {
                ItemType contextItemType = ContextItemTypeForTemplateRule;
                ContextItemStaticInfo cisi = config.MakeContextItemStaticInfo(contextItemType, mayOmitContextItem);
                cisi.SetContextPostureStriding();
                ExpressionVisitor visitor = MakeExpressionVisitor();
                foreach (TemplateRule compiledTemplateRule in compiledTemplateRules)
                {
                    if (compiledTemplateRule.GetMode().ModeName.Equals(Mode.OMNI_MODE_NAME))
                    {
                        compiledTemplateRule.MatchPattern.ResetLocalStaticProperties();
                        Patterns.Pattern m2 = (Patterns.Pattern)compiledTemplateRule.MatchPattern.Optimize(visitor, cisi);
                        compiledTemplateRule.MatchPattern = m2;
                    }
                }

                if (!IsDeferredCompilation(GetCompilation()))
                {
                    Optimizer opt = GetConfiguration().ObtainOptimizer();
                    try
                    {
                        foreach (TemplateRule compiledTemplateRule in compiledTemplateRules)
                        {
                            if (!compiledTemplateRule.GetMode().ModeName.Equals(Mode.OMNI_MODE_NAME))
                            {
                                Expression templateRuleBody = compiledTemplateRule.GetBody();
                                visitor.SetOptimizeForStreaming(compiledTemplateRule.IsDeclaredStreamable());
                                templateRuleBody = templateRuleBody.TypeCheck(visitor, cisi);
                                templateRuleBody = ExpressionTool.OptimizeComponentBody(templateRuleBody, GetCompilation(), visitor, cisi, true);
                                compiledTemplateRule.SetBody(templateRuleBody);
                                opt.CheckStreamability(this, compiledTemplateRule);
                                AllocateLocalSlots(templateRuleBody);
                                foreach (Rule r in compiledTemplateRule.Rules)
                                {
                                    Patterns.Pattern match = r.Pattern;
                                    ContextItemStaticInfo info = GetConfiguration().MakeContextItemStaticInfo(match.GetItemType(), mayOmitContextItem);
                                    info.SetContextPostureStriding();
                                    Patterns.Pattern m2 = (Patterns.Pattern)match.Optimize(visitor, info);
                                    if (m2 != match)
                                    {
                                        r.Pattern = m2;
                                    }
                                }

                                if (explaining)
                                {
                                    Logger err = GetConfiguration().Logger;
                                    err.Info("Optimized expression tree for template rule at line " + GetLineNumber() + " in " + GetSystemId() + ':');
                                    templateRuleBody.Explain(err);
                                }
                            }
                        }
                    }
                    catch (XPathException e)
                    {
                        CompileError(e.MaybeWithLocation(this));
                    }
                }
            }
        }

        public SlotManager GetSlotManager()
        {
            return stackFrameMap;
        }
        Actor IStylesheetComponent.GetActor() => GetActor();
    }
}
