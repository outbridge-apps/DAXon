////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    // Enable the C# code to accept a lambda expression in calls to children()
    public abstract class StyleElement : ElementImpl
    {
        public const int ACTION_VALIDATE = 1;
        public const int ACTION_COMPILE = 2;
        public const int ACTION_TYPECHECK = 4;
        public const int ACTION_OPTIMIZE = 8;
        public const int ACTION_FIXUP = 16;
        public const int ACTION_PROCESS_ATTRIBUTES = 32;

        public static readonly string[] YES_NO = new[]
        {
            "0",
            "1",
            "false",
            "no",
            "true",
            "yes"
        };
        protected NamespaceUri[] extensionNamespaces = null; // a list of URIs
        private NamespaceUri[] excludedNamespaces = null; // a list of URIs
        protected int version = -1; // the effective version of this element
        protected ExpressionContext staticContext = null;
        public XmlProcessingIncident validationError = null;
        public OnFailure reportingCircumstances = OnFailure.REPORT_ALWAYS;
        protected NamespaceUri defaultXPathNamespace = null;
        protected string defaultCollationName = null;
        protected StructuredQName defaultMode;
        protected bool expandText = false;
        private StructuredQName objectName; // for instructions that define an XSLT named object, the name of that object
        private string baseURI;
        private Compilation compilation;
        private Loc savedLocation = null;
        private int defaultValidation = Validation.DEFAULT;

        protected int actionsCompleted = 0;

        public virtual Visibility DeclaredVisibility
        {
            get
            {
                string vis = GetAttributeValue(NamespaceUri.NULL, "visibility");
                if (vis == null)
                {
                    return Visibility.UNDEFINED;
                }
                else
                {
                    return InterpretVisibilityValue(vis, "");
                }
            }
        }

        protected virtual int DefaultValidation
        {
            get
            {
                int v = defaultValidation;
                NodeInfo p = this;
                while (v == Validation.DEFAULT)
                {
                    p = p.GetParent();
                    if (!(p is StyleElement))
                    {
                        return Validation.STRIP; //return getCompilation().isSchemaAware() ? Validation.PRESERVE : Validation.STRIP;
                    }

                    v = ((StyleElement)p).defaultValidation;
                }

                return v;
            }
        }

        public virtual StylesheetPackage UsedPackage => null;

        protected virtual Component OverriddenComponent
        {
            get
            {
                if (!(this is IStylesheetComponent))
                {
                    return null;
                }

                SymbolicName originalName = ((IStylesheetComponent)this).GetSymbolicName();
                StyleElement xslOverride = FindAncestorElement(StandardNames.XSL_OVERRIDE);
                if (xslOverride == null)
                {
                    return null;
                }

                StyleElement usePackage = xslOverride.FindAncestorElement(StandardNames.XSL_USE_PACKAGE);
                if (usePackage == null)
                {
                    return null;
                }

                return usePackage.UsedPackage.GetComponent(originalName);
            }
        }
        protected virtual StyleElement LastChildInstruction
        {
            get
            {
                StyleElement last = null;
                foreach (NodeInfo child in Children())
                {
                    if (child is StyleElement)
                    {
                        last = (StyleElement)child;
                    }
                    else
                    {
                        last = null;
                    }
                }

                return last;
            }
        }

        // Inherited properties are cached per element but resolved through the parent, and the first lookup can
        // come from the deepest element of a deep stylesheet: one frame per level then overflowed below the
        // stack guard. Resolving the uncached ancestors root-first leaves every lookup one level deep.
        internal static void ResolveAncestorsFirst(StyleElement from, Func<StyleElement, bool> unresolved, Action<StyleElement> resolve)
        {
            List<StyleElement> chain = null;
            for (NodeInfo p = from; p is StyleElement s && unresolved(s); p = s.GetParent())
            {
                (chain ??= new List<StyleElement>()).Add(s);
            }

            if (chain != null)
            {
                for (int i = chain.Count - 1; i >= 0; i--)
                {
                    resolve(chain[i]);
                }
            }
        }

        public virtual int EffectiveVersion
        {
            get
            {
                if (version == -1)
                {
                    NodeInfo node = GetParent();
                    if (node is StyleElement)
                    {
                        ResolveAncestorsFirst((StyleElement)node, s => s.version == -1, s => _ = s.EffectiveVersion);
                        version = ((StyleElement)node).EffectiveVersion;
                    }
                    else
                    {
                        return 20; // defensive programming
                    }
                }

                return version;
            }
        }

        public virtual StructuredQName DefaultMode
        {
            get
            {
                if (defaultMode == null)
                {
                    ProcessDefaultMode();
                    if (defaultMode == null)
                    {
                        NodeInfo p = GetParent();
                        if (p is XSLMode)
                        {

                            // 4.0 enclosing modes
                            return defaultMode = ((XSLMode)p).GetObjectName();
                        }
                        else if (p is StyleElement)
                        {
                            ResolveAncestorsFirst((StyleElement)p, s => s.defaultMode == null, s => _ = s.DefaultMode);
                            return defaultMode = ((StyleElement)p).DefaultMode;
                        }
                        else
                        {
                            return defaultMode = Mode.UNNAMED_MODE_NAME;
                        }
                    }
                }

                return defaultMode;
            }
        }

        public virtual NamespaceUri DefaultXPathNamespace
        {
            get
            {
                NodeInfo anc = this;
                while (anc is StyleElement)
                {
                    NamespaceUri x = ((StyleElement)anc).defaultXPathNamespace;
                    if (x != null)
                    {
                        return x;
                    }

                    anc = anc.GetParent();
                }

                return compilation.GetCompilerInfo().DefaultElementNamespace;
            }
        }

        public virtual SlotManager ContainingSlotManager
        {
            get
            {
                NodeImpl node = this;
                while (true)
                {
                    NodeImpl next = node.GetParent();
                    if (next is XSLModuleRoot || next.Fingerprint == StandardNames.XSL_OVERRIDE)
                    {
                        if (node is IStylesheetComponent)
                        {
                            return ((IStylesheetComponent)node).GetSlotManager();
                        }
                        else
                        {
                            return null;
                        }
                    }

                    node = next;
                }
            }
        }

        public virtual StylesheetPackage ContainingPackage
        {
            get
            {
                PrincipalStylesheetModule psm = GetPrincipalStylesheetModule();
                return psm == null ? null : psm.GetStylesheetPackage();
            }
        }
        public StyleElement()
        {
        }

        public virtual Compilation GetCompilation()
        {
            return compilation;
        }

        public virtual void SetCompilation(Compilation compilation)
        {
            this.compilation = compilation;
        }

        public virtual StylesheetPackage GetPackageData()
        {
            return GetPrincipalStylesheetModule().GetStylesheetPackage();
        }

        public override Configuration GetConfiguration()
        {
            return compilation.GetConfiguration();
        }

        public virtual ExpressionContext GetStaticContext()
        {
            if (staticContext == null)
            {
                staticContext = new ExpressionContext(this, null);
            }

            return staticContext;
        }

        public virtual ExpressionContext GetStaticContext(StructuredQName attributeName)
        {
            return new ExpressionContext(this, attributeName);
        }

        public virtual bool IsInXsltNamespace()
        {
            return true; // unless specified otherwise in a subclass
        }

        public override string GetBaseURI()
        {
            if (baseURI == null)
            {
                baseURI = base.GetBaseURI();
            }

            return baseURI;
        }

        internal override string KnownBaseUri => baseURI;

        internal override void RememberBaseUri(string uri)
        {
            baseURI = uri;
        }

        public virtual ExpressionVisitor MakeExpressionVisitor()
        {
            return ExpressionVisitor.Make(GetStaticContext());
        }

        public virtual bool IsSchemaAware()
        {
            return GetCompilation().IsSchemaAware();
        }

        public virtual void SubstituteFor(StyleElement temp)
        {
            SetRawParent(temp.GetRawParent());
            SetAttributes(temp.Attributes());

            SetNamespaceMap(temp.AllNamespaces);
            SetNodeName(temp.GetNodeName());
            SetRawSequenceNumber(temp.GetRawSequenceNumber());
            extensionNamespaces = temp.extensionNamespaces;
            excludedNamespaces = temp.excludedNamespaces;
            version = temp.version;
            staticContext = temp.staticContext;
            validationError = temp.validationError;
            reportingCircumstances = temp.reportingCircumstances;
            compilation = temp.compilation; //lineNumber = temp.lineNumber;
        }

        public virtual void SetValidationError(XmlProcessingIncident reason, OnFailure circumstances)
        {
            validationError = reason;
            reportingCircumstances = circumstances;
        }

        public virtual void SetIgnoreInstruction()
        {
            reportingCircumstances = OnFailure.IGNORED_INSTRUCTION;
        }

        public virtual bool IsInstruction()
        {
            return false;
        }

        public virtual bool IsDeclaration()
        {
            return false;
        }

        public virtual Visibility GetVisibility()
        {
            string vis = GetAttributeValue(NamespaceUri.NULL, "visibility");
            if (vis == null)
            {
                return Visibility.PRIVATE;
            }
            else
            {
                return InterpretVisibilityValue(vis, "");
            }
        }

        public virtual bool MarkTailCalls()
        {
            return false;
        }

        protected virtual bool MayContainSequenceConstructor()
        {
            return false;
        }

        protected virtual bool MayContainFallback()
        {
            return MayContainSequenceConstructor();
        }

        public virtual bool MayContainParam()
        {
            return false;
        }

        public StructuredQName MakeQName(string lexicalQName, string errorCode, string attributeName)
        {
            StructuredQName qName;
            try
            {
                qName = StructuredQName.FromLexicalQName((lexicalQName), false, true, this);
            }
            catch (XPathException e)
            {
                string requestedError = errorCode == null ? "XTSE0020" : errorCode;
                XPathException e2 = e.AsStaticError().ReplacingErrorCode("FONS0004", "XTSE0280").ReplacingErrorCode("FOCA0002", requestedError).MaybeWithErrorCode(requestedError).WithLocation(attributeName == null ? this : new AttributeLocation(this, StructuredQName.FromEQName((attributeName))));
                CompileError(e2);
                qName = new StructuredQName("saxon", NamespaceUri.SAXON, "error-name");
            }

            if (NamespaceUri.IsReserved(qName.GetNamespaceUri()))
            {
                if (qName.HasURI(NamespaceUri.XSLT))
                {
                    if (qName.GetLocalPart().Equals("initial-template") && (this is XSLTemplate || this is XSLCallTemplate || this is XSLAcceptExpose))
                    {
                        return qName;
                    }

                    if (qName.GetLocalPart().Equals("original"))
                    {

                        // OK if within xsl:override
                        if (FindAncestorElement(StandardNames.XSL_OVERRIDE) != null)
                        {
                            return qName;
                        }
                    }
                }

                XmlProcessingIncident err = new XmlProcessingIncident("Namespace prefix " + qName.GetPrefix() + " refers to a reserved namespace", "XTSE0080");
                err.SetLocation(this);
                CompileError(err);
                qName = new StructuredQName("saxon", NamespaceUri.SAXON, "error-name");
            }

            return qName;
        }

        public virtual StyleElement FindAncestorElement(int fingerprint)
        {
            NodeInfo parent = GetParent();
            while (true)
            {
                if (parent is StyleElement)
                {
                    if (parent.Fingerprint == fingerprint)
                    {
                        return (StyleElement)parent;
                    }
                    else
                    {
                        parent = parent.GetParent();
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        public virtual Actor GetXslOriginal(int componentKind)
        {
            StyleElement container = componentKind == Fingerprint ? this : FindAncestorElement(componentKind);
            if (!(container is IStylesheetComponent))
            {
                throw new XPathException("A reference to xsl:original appears within the wrong kind of component: in this case" + ", it must be within xsl:" + GetNamePool().GetLocalName(componentKind), "XTSE0650", this);
            }

            SymbolicName originalName = ((IStylesheetComponent)container).GetSymbolicName();
            StyleElement xslOverride = container.FindAncestorElement(StandardNames.XSL_OVERRIDE);
            if (xslOverride == null)
            {
                throw new XPathException("A reference to xsl:original can be used only within an xsl:override element");
            }

            StyleElement usePackage = xslOverride.FindAncestorElement(StandardNames.XSL_USE_PACKAGE);
            if (usePackage == null)
            {
                throw new XPathException("The parent of xsl:override must be an xsl:use-package element", "XTSE0010", xslOverride);
            }

            Component overridden = usePackage.UsedPackage.GetComponent(originalName);
            if (overridden == null)
            {

                // the error will be detected and reported elsewhere
                return null;
            }

            return overridden.GetActor();
        }

        public virtual RetainedStaticContext MakeRetainedStaticContext()
        {
            return GetStaticContext().MakeRetainedStaticContext();
        }

        public virtual bool ChangesRetainedStaticContext()
        {
            NodeImpl parent = GetParent();
            return parent == null || !ExpressionTool.EqualOrNull(GetBaseURI(), parent.GetBaseURI()) || defaultCollationName != null || defaultXPathNamespace != null || !(parent is StyleElement) || AllNamespaces != parent.AllNamespaces || EffectiveVersion != ((StyleElement)parent).EffectiveVersion;
        }

        public virtual INamespaceResolver GetNamespaceResolver()
        {
            return this;
        }

        public virtual void ProcessAllAttributes()
        {
            ProbeStylesheetDepth();
            ProcessDefaultCollationAttribute();
            ProcessDefaultMode();
            staticContext = new ExpressionContext(this, null);
            ProcessAttributes();
            foreach (NodeInfo child in Children())
            {
                if (child is StyleElement)
                {
                    ((StyleElement)child).ProcessAllAttributes();
                }
                else if (child is TextValueTemplateNode)
                {
                    ((TextValueTemplateNode)child).Parse();
                }
            }
        }

        public virtual void ProcessStandardAttributes(NamespaceUri @namespace)
        {
            ProcessExtensionElementAttribute(@namespace);
            ProcessExcludedNamespaces(@namespace);
            ProcessVersionAttribute(@namespace);
            ProcessDefaultXPathNamespaceAttribute(@namespace);
            ProcessDefaultValidationAttribute(@namespace);
            ProcessExpandTextAttribute(@namespace);
        }

        public virtual string GetAttributeValue(string clarkName)
        {
            INodeName nn = FingerprintedQName.FromClarkName(clarkName);
            return GetAttributeValue(nn.GetNamespaceUri(), nn.GetLocalPart());
        }

        public void ProcessAttributes()
        {
            PrepareAttributes();
        }

        public virtual void CheckUnknownAttribute(INodeName nc)
        {
            NamespaceUri attributeURI = nc.GetNamespaceUri();
            string clarkName = nc.GetStructuredQName().ClarkName;
            if (ForwardsCompatibleModeIsEnabled())
            {

                // then unknown attributes are permitted and ignored
                return;
            }


            // allow xsl:extension-element-prefixes etc on an extension element
            if (IsInstruction() && attributeURI.Equals(NamespaceUri.XSLT) && !IsInXsltNamespace() && (clarkName.EndsWith("}default-collation", StringComparison.Ordinal) || clarkName.EndsWith("}default-mode", StringComparison.Ordinal) || clarkName.EndsWith("}xpath-default-namespace", StringComparison.Ordinal) || clarkName.EndsWith("}expand-text", StringComparison.Ordinal) || clarkName.EndsWith("}extension-element-prefixes", StringComparison.Ordinal) || clarkName.EndsWith("}exclude-result-prefixes", StringComparison.Ordinal) || clarkName.EndsWith("}version", StringComparison.Ordinal) || clarkName.EndsWith("}default-validation", StringComparison.Ordinal) || clarkName.EndsWith("}use-when", StringComparison.Ordinal)))
            {
                return;
            }


            // allow standard attributes on an XSLT element
            if (IsInXsltNamespace() && (clarkName.Equals("default-collation") || clarkName.Equals("default-mode") || clarkName.Equals("expand-text") || clarkName.Equals("xpath-default-namespace") || clarkName.Equals("extension-element-prefixes") || clarkName.Equals("exclude-result-prefixes") || clarkName.Equals("version") || clarkName.Equals("default-validation") || clarkName.Equals("use-when")))
            {
                return;
            }

            if (attributeURI.IsEmpty() || NamespaceUri.XSLT.Equals(attributeURI))
            {
                CompileErrorInAttribute("Attribute " + Err.Wrap(nc.DisplayName, Err.ATTRIBUTE) + " is not allowed on element " + Err.Wrap(DisplayName, Err.ELEMENT), "XTSE0090", clarkName);
            }
            else if (NamespaceUri.SAXON.Equals(attributeURI))
            {
                IssueWarning("Unrecognized attribute in Saxon namespace: " + nc.DisplayName, "XTSE0090");
            }
        }

        public abstract void PrepareAttributes();

        public virtual Expression MakeExpression(string expression, AttributeInfo att)
        {
            try
            {
                IStaticContext env = staticContext;
                if (att != null)
                {
                    StructuredQName attName = att.GetNodeName().GetStructuredQName();
                    env = GetStaticContext(attName);
                }

                return ExpressionTool.Make(expression, env, 0, Token.EOF, GetCompilation().GetCompilerInfo().CodeInjector);
            }
            catch (XPathException err)
            {
                err.MaybeSetLocation(AllocateLocation());
                if (err.IsReportableStatically())
                {
                    CompileError(err);
                }

                ErrorExpression erexp = new ErrorExpression(new XmlProcessingException(err));
                erexp.SetRetainedStaticContext(MakeRetainedStaticContext());
                erexp.SetLocation(AllocateLocation());
                return erexp;
            }
        }

        protected virtual Patterns.Pattern MakePattern(string pattern, string attributeName)
        {
            try
            {
                IStaticContext env = GetStaticContext(new StructuredQName("", NamespaceUri.NULL, attributeName));
                Patterns.Pattern p = Patterns.Pattern.Make(pattern, env, GetCompilation().GetPackageData());
                p.SetLocation(AllocateLocation());
                return p;
            }
            catch (XPathException err)
            {
                err.MaybeSetErrorCode("XTSE0340");
                XPathException err2 = err.ReplacingErrorCode("XPST0003", "XTSE0340");
                CompileError(err2);
                NodeTestPattern nsp = new NodeTestPattern(AnyNodeTest.GetInstance());
                nsp.SetLocation(AllocateLocation());
                return nsp;
            }
        }

        protected virtual Expression MakeAttributeValueTemplate(string expression, AttributeInfo att)
        {
            IStaticContext env = att == null ? staticContext : GetStaticContext(att.GetNodeName().GetStructuredQName());
            if (att != null)
            {
                StructuredQName attName = att.GetNodeName().GetStructuredQName();
                env = GetStaticContext(attName);
            }

            try
            {
                return AttributeValueTemplate.Make(expression, env);
            }
            catch (XPathException err)
            {
                CompileError(err);
                return new StringLiteral(expression);
            }
        }

        protected virtual void CheckAttributeValue(string name, string value, bool avt, string[] allowed)
        {
            if (avt && value.Contains("{"))
            {
                return;
            }

            if (Array.BinarySearch(allowed, value, StringComparer.Ordinal) < 0)
            {
                StringBuilder sb = new StringBuilder(64);
                sb.Append("Invalid value for ");
                sb.Append('@');
                sb.Append(name);
                sb.Append(". Value must be one of (");
                for (int i = 0; i < allowed.Length; i++)
                {
                    sb.Append(i == 0 ? "" : "|");
                    sb.Append(allowed[i]);
                }

                sb.Append(')');
                CompileError(sb.ToString(), "XTSE0020");
            }
        }
        public virtual bool ProcessBooleanAttribute(string name, string value)
        {
            string s = Whitespace.Trim(value);
            if (IsYes(s))
            {
                return true;
            }
            else if (IsNo(s))
            {
                return false;
            }
            else
            {
                InvalidAttribute(name, "yes|no | true|false | 1|0");
                return false; // never get here
            }
        }

        public static bool IsYes(string s)
        {
            return s == "yes" || s == "true" || s == "1";
        }

        public static bool IsNo(string s)
        {
            return s == "no" || s == "false" || s == "0";
        }

        protected virtual bool ProcessStreamableAtt(string streamableAtt)
        {
            bool streamable = ProcessBooleanAttribute("streamable", streamableAtt);
            if (streamable)
            {
                if (!GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XSLT))
                {
                    IssueWarning("Request for streaming ignored: this Saxon configuration does not support streaming", DAXonErrorCode.SXST0068);
                    return false;
                }

                if (GetConfiguration().GetConfigurationProperty(Feature<string>.STREAMABILITY) == "off")
                {
                    IssueWarning("Request for streaming ignored: streaming is disabled in this Saxon configuration", DAXonErrorCode.SXST0068);
                    return false;
                }
            }

            return streamable;
        }

        public virtual Values.SequenceType MakeSequenceType(string sequenceType)
        {
            GetStaticContext();
            XPathParser parser = GetConfiguration().NewExpressionParser("XP", false, staticContext);
            QNameParser qp = new QNameParser(staticContext.GetNamespaceResolver()).WithAcceptEQName(staticContext.GetXPathVersion() >= 30).WithErrorOnBadSyntax("XPST0003").WithErrorOnUnresolvedPrefix("XPST0081");
            parser.SetQNameParser(qp);
            return parser.ParseSequenceType(sequenceType, staticContext);
        }

        public virtual Values.SequenceType MakeExtendedSequenceType(string sequenceType)
        {
            ExpressionContext env = GetStaticContext(new StructuredQName("saxon", NamespaceUri.SAXON, "as"));
            XPathParser parser = GetConfiguration().NewExpressionParser("XP", false, env);
            QNameParser qp = new QNameParser(env.GetNamespaceResolver()).WithAcceptEQName(true).WithErrorOnBadSyntax("XPST0003").WithErrorOnUnresolvedPrefix("XPST0081");
            parser.SetQNameParser(qp);
            return parser.ParseExtendedSequenceType(sequenceType, env);
        }

        public virtual void ProcessExtensionElementAttribute(NamespaceUri ns)
        {
            string ext = GetAttributeValue(ns, "extension-element-prefixes");
            if (ext != null)
            {

                int count = ext.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                extensionNamespaces = new NamespaceUri[count];
                count = 0;
                foreach (string s0 in ext.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string s = s0;
                    if (s == "#default")
                    {
                        s = "";
                    }

                    NamespaceUri uri = GetURIForPrefix(s, false);
                    if (uri == null)
                    {
                        extensionNamespaces = null;
                        CompileError("Namespace prefix " + s + " is undeclared", "XTSE1430");
                    }
                    else if (NamespaceUri.IsReserved(uri))
                    {
                        CompileError("Namespace " + uri + " is reserved: it cannot be used for extension instructions " + "(perhaps exclude-result-prefixes was intended).", "XTSE0085");
                        extensionNamespaces[count++] = uri;
                    }
                    else
                    {
                        extensionNamespaces[count++] = uri;
                    }
                }
            }
        }

        public virtual void ProcessExcludedNamespaces(NamespaceUri ns)
        {
            string ext = GetAttributeValue(ns, "exclude-result-prefixes");
            if (ext != null)
            {
                if (Whitespace.Trim(ext) == "#all")
                {
                    IList<NamespaceUri> excluded = new List<NamespaceUri>();
                    foreach (NamespaceBinding binding in AllNamespaces)
                    {
                        excluded.Add(binding.GetNamespaceUri());
                    }

                    excludedNamespaces = excluded.ToArray();
                }
                else
                {

                    int count = ext.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    excludedNamespaces = new NamespaceUri[count];
                    count = 0;
                    foreach (string s0 in ext.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string s = s0;
                        if (s == "#default")
                        {
                            s = "";
                        }
                        else if (s == "#all")
                        {
                            CompileError("In exclude-result-prefixes, cannot mix #all with other values", "XTSE0020");
                        }

                        NamespaceUri uri = GetURIForPrefix(s, true);
                        if (uri == null)
                        {
                            excludedNamespaces = null;
                            CompileError("Namespace prefix " + s + " is not declared", "XTSE0808");
                            break;
                        }

                        excludedNamespaces[count++] = uri;
                        if ((s.Length == 0) && uri.IsEmpty())
                        {
                            CompileError("Cannot exclude the #default namespace when no default namespace is declared", "XTSE0809");
                        }
                    }
                }
            }
        }

        protected internal virtual void ProcessVersionAttribute(NamespaceUri ns)
        {
            string v = Whitespace.Trim(GetAttributeValue(ns, "version"));
            if (v != null)
            {
                IConversionResult val = BigDecimalValue.MakeDecimalValue(v, true);
                if (val is ValidationFailure)
                {
                    version = 30;
                    CompileError("The version attribute must be a decimal literal", "XTSE0110");
                }
                else
                {

                    // Note this will normalize the decimal so that trailing spaces are not significant
                    version = (((DecimalValue)val).GetDecimalValue() * BigDecimal.Ten).IntValue();
                    if (version < 20 && version != 10)
                    {

                        // XSLT 2.0 says use backwards compatible mode. XSLT 3.0 says we can raise an error.
                        // Both allow a warning
                        IssueWarning("Unrecognized version " + val + ": treated as 1.0", DAXonErrorCode.SXWN9020);
                        version = 10;
                    }
                    else if (version > 20 && version < 30)
                    {
                        IssueWarning("Unrecognized version " + val + ": treated as 2.0", DAXonErrorCode.SXWN9020);
                        version = 20;
                    }
                }
            }
        }

        protected virtual int ValidateValidationAttribute(string value)
        {
            int code = Validation.GetCode(value);
            if (code == Validation.INVALID)
            {
                string prefix = this is LiteralResultElement ? "xsl:" : "";
                CompileError("Invalid value of " + prefix + "validation attribute: '" + value + "'", "XTSE0020");
                code = DefaultValidation;
            }

            if (!IsSchemaAware())
            {
                if (code == Validation.STRICT)
                {
                    CompileError("To perform validation, a schema-aware XSLT processor is needed", "XTSE1660");
                }

                code = Validation.STRIP;
            }

            return code;
        }

        public virtual bool IsExtensionAttributeAllowed(string attribute)
        {
            if (GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
            {
                return true;
            }
            else
            {
                IssueWarning("The option " + DisplayName + "/@" + attribute + " is ignored because it requires a Saxon-PE license", DAXonErrorCode.SXWN9021);
                return false;
            }
        }

        public virtual bool ForwardsCompatibleModeIsEnabled()
        {
            return EffectiveVersion > GetCompilation().GetCompilerInfo().XsltVersion;
        }

        public virtual bool XPath10ModeIsEnabled()
        {
            return EffectiveVersion < 20;
        }

        public virtual void ProcessDefaultCollationAttribute()
        {
            NamespaceUri ns = IsInXsltNamespace() ? NamespaceUri.NULL : NamespaceUri.XSLT;
            string v = GetAttributeValue(ns, "default-collation");
            if (v != null)
            {
                StringBuilder reasons = new StringBuilder();
                foreach (string uri0 in v.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string uri = uri0;
                    if (uri.Equals(NamespaceConstant.CODEPOINT_COLLATION_URI))
                    {
                        defaultCollationName = uri;
                        return;
                    }
                    else
                    {
                        URI collationURI;
                        try
                        {
                            collationURI = new URI(uri);
                            if (!collationURI.IsAbsolute())
                            {
                                URI @base = new URI(GetBaseURI());
                                collationURI = @base.Resolve(collationURI);
                                uri = collationURI.ToString();
                            }
                        }
                        catch (URISyntaxException)
                        {
                            CompileError("default collation '" + uri + "' is not a valid URI");
                            uri = NamespaceConstant.CODEPOINT_COLLATION_URI;
                        }

                        try
                        {
                            if (GetConfiguration().GetCollation(uri) != null)
                            {
                                defaultCollationName = uri;
                                return;
                            }
                            else
                            {
                                if (reasons.Length != 0)
                                {
                                    reasons.Append("; ");
                                }

                                reasons.Append("Collation ").Append(uri).Append(" is not recognized");
                            }
                        }
                        catch (XPathException e)
                        {
                            if (reasons.Length != 0)
                            {
                                reasons.Append("; ");
                            }

                            reasons.Append("Collation ").Append(uri).Append(" is not recognized (").Append(e.Message).Append(')'); // Ignore an unrecognized collation URI
                        }
                    } // if not recognized, try the next URI in order
                }

                string msg = "No recognized collation URI found in default-collation attribute";
                if (reasons.Length != 0)
                {
                    msg += ". ";
                    msg += reasons.ToString();
                }

                CompileErrorInAttribute(msg, "XTSE0125", new StructuredQName("", ns, "default-collation").ClarkName);
            }
        }

        public virtual string GetDefaultCollationName()
        {
            StyleElement e = this;
            while (true)
            {
                if (e.defaultCollationName != null)
                {
                    return e.defaultCollationName;
                }

                NodeInfo p = e.GetParent();
                if (!(p is StyleElement))
                {
                    break;
                }

                e = (StyleElement)p;
            }

            return GetConfiguration().GetDefaultCollationName();
        }

        protected virtual IStringCollator FindCollation(string name, string baseURI)
        {
            return GetConfiguration().GetCollation(name, baseURI);
        }

        public virtual void ProcessDefaultMode()
        {
            NamespaceUri ns = IsInXsltNamespace() ? NamespaceUri.NULL : NamespaceUri.XSLT;
            string v = GetAttributeValue(ns, "default-mode");
            if (v != null)
            {
                if (v.Equals("#unnamed"))
                {
                    defaultMode = Mode.UNNAMED_MODE_NAME;
                }
                else
                {
                    defaultMode = MakeQName(v, null, "default-mode");
                }
            }

            PrincipalStylesheetModule psm = compilation.GetPrincipalStylesheetModule();
            StructuredQName checkedName = defaultMode;
            if (psm != null && psm.IsDeclaredModes())
            {

                // It will be null on the xsl:package element itself
                psm.AddFixupAction(() =>
                {
                    if (psm.GetRuleManager().ObtainMode(checkedName, false) == null)
                    {
                        throw new XPathException("Mode " + checkedName.DisplayName + " is not declared in an xsl:mode declaration", "XTSE3085").WithLocation(this);
                    }
                });
            }
        }

        private bool DefinesExtensionElement(NamespaceUri uri)
        {
            if (extensionNamespaces == null)
            {
                return false;
            }

            foreach (NamespaceUri extensionNamespace in extensionNamespaces)
            {
                if (extensionNamespace.Equals(uri))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual bool IsExtensionNamespace(NamespaceUri uri)
        {
            NodeInfo anc = this;
            while (anc is StyleElement)
            {
                if (((StyleElement)anc).DefinesExtensionElement(uri))
                {
                    return true;
                }

                anc = anc.GetParent();
            }

            return false;
        }

        private bool DefinesExcludedNamespace(NamespaceUri uri)
        {
            if (excludedNamespaces == null)
            {
                return false;
            }

            foreach (NamespaceUri excludedNamespace in excludedNamespaces)
            {
                if (excludedNamespace.Equals(uri))
                {
                    return true;
                }
            }

            return false;
        }

        protected virtual bool IsExcludedNamespace(NamespaceUri uri)
        {
            if (uri.Equals(NamespaceUri.XSLT) || uri.Equals(NamespaceUri.XML))
            {
                return true;
            }

            if (IsExtensionNamespace(uri))
            {
                return true;
            }

            NodeInfo anc = this;
            while (anc is StyleElement)
            {
                if (((StyleElement)anc).DefinesExcludedNamespace(uri))
                {
                    return true;
                }

                anc = anc.GetParent();
            }

            return false;
        }

        public virtual void ProcessDefaultXPathNamespaceAttribute(NamespaceUri ns)
        {
            string v = GetAttributeValue(ns, "xpath-default-namespace");
            if (v != null)
            {
                defaultXPathNamespace = NamespaceUri.Of(v);
            }
        }

        public virtual void ProcessExpandTextAttribute(NamespaceUri ns)
        {
            string v = GetAttributeValue(ns, "expand-text");
            if (v != null)
            {
                expandText = ProcessBooleanAttribute("expand-text", v);
            }
            else
            {
                NodeInfo parent = GetParent();
                expandText = parent is StyleElement && ((StyleElement)parent).expandText;
            }
        }

        public virtual void ProcessDefaultValidationAttribute(NamespaceUri ns)
        {
            string v = GetAttributeValue(ns, "default-validation");
            if (v != null)
            {
                int val = Validation.GetCode(v);
                if (val == Validation.STRIP || val == Validation.PRESERVE)
                {
                    defaultValidation = val;
                }
                else if (val == Validation.STRICT || val == Validation.LAX)
                {
                    // XTSE1660: strict needs a schema-aware processor; a basic processor treats lax as strip.
                    if (val == Validation.STRICT && !IsSchemaAware())
                        CompileErrorInAttribute("To use default-validation=\"strict\", a schema-aware XSLT processor is needed", "XTSE1660", "default-validation");
                    else
                        defaultValidation = Validation.STRIP;
                }
                else
                {
                    CompileErrorInAttribute("@default-validation must be preserve|strip", "XTSE0020", "default-validation");
                }
            }
        }

        public virtual bool IsExpandingText()
        {
            return expandText;
        }

        public virtual ISchemaType GetSchemaType(string typeAtt)
        {
            try
            {
                NamespaceUri uri;
                string lname;
                if (typeAtt.StartsWith("Q{", StringComparison.Ordinal))
                {
                    StructuredQName q = MakeQName(typeAtt, "XTSE1520", "type");
                    uri = q.GetNamespaceUri();
                    lname = q.GetLocalPart();
                }
                else
                {
                    string[] parts = NameChecker.GetQNameParts(typeAtt);
                    lname = parts[1];
                    if (parts[0] == "")
                    {

                        // Name is unprefixed: use the default-xpath-namespace
                        uri = DefaultXPathNamespace;
                    }
                    else
                    {
                        uri = GetURIForPrefix(parts[0], false);
                        if (uri == null)
                        {
                            CompileError("Namespace prefix for type annotation is undeclared", "XTSE1520");
                            return null;
                        }
                    }
                }

                if (uri.Equals(NamespaceUri.SCHEMA))
                {
                    ISchemaType t = (ISchemaType)BuiltInType.GetSchemaTypeByLocalName(lname);
                    if (t == null)
                    {
                        CompileError("Unknown built-in type " + typeAtt, "XTSE1520");
                        return null;
                    }

                    return t;
                }


                // not a built-in type: look in the imported schemas
                if (!GetPrincipalStylesheetModule().IsImportedSchema(uri))
                {
                    CompileError("There is no imported schema for the namespace of type " + typeAtt, "XTSE1520");
                    return null;
                }

                StructuredQName qName = new StructuredQName("", uri, lname);
                ISchemaType stype = GetConfiguration().GetSchemaType(qName);
                if (stype == null)
                {
                    CompileError("There is no type named " + typeAtt + " in an imported schema", "XTSE1520");
                }

                return stype;
            }
            catch (QNameException err)
            {
                CompileError("Invalid type name. " + err.GetMessage(), "XTSE1520");
            }

            return null;
        }

        public virtual ISimpleType GetTypeAnnotation(ISchemaType schemaType)
        {
            return (ISimpleType)schemaType;
        }

        protected virtual Expression MapToSequence(Expression mapExpr)
        {
            try
            {
                return VendorFunctionSetHE.GetInstance().MakeFunction("map-as-sequence-of-maps", 1).MakeFunctionCall(mapExpr);
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }

        public virtual void Validate(ComponentDeclaration decl)
        {
        }

        public virtual void PostValidate()
        {
        }

        public virtual void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
        }

        public virtual Expression TypeCheck(string name, Expression exp)
        {
            if (exp == null)
            {
                return null;
            }

            Configuration config = GetConfiguration();
            if (config.GetBooleanProperty(Feature<bool>.STRICT_STREAMABILITY))
            {
                return exp;
            }

            try
            {
                exp = exp.TypeCheck(MakeExpressionVisitor(), config.MakeContextItemStaticInfo(Types.Type.ITEM_TYPE, true));
                exp = ExpressionTool.ResolveCallsToCurrentFunction(exp);

                //            if (explaining) {
                //                System.Console.Error.println("Static type: " +
                //                exp.display(10, getNamePool(), System.Console.Error);
                //                return injector.inject(exp, getStaticContext(), LocationKind.XPATH_IN_XSLT, new StructuredQName("", "", name));
                //            }
                return exp;
            }
            catch (XPathException err)
            {

                // we can't report a dynamic error such as divide by zero unless the expression
                // is actually executed.
                XPathException e2 = err;
                if (e2.IsReportableStatically())
                {
                    if (!(e2.GetLocator() is XPathParser.NestedLocation))
                    {
                        e2 = e2.WithLocation(new AttributeLocation(this, StructuredQName.FromClarkName(name)));
                    }

                    CompileError(e2);
                    return exp;
                }
                else
                {
                    ErrorExpression erexp = new ErrorExpression(new XmlProcessingException(e2));
                    ExpressionTool.CopyLocationInfo(exp, erexp);
                    return erexp;
                }
            }
        }

        protected virtual void AllocateLocalSlots(Expression exp)
        {
            SlotManager slotManager = ContainingSlotManager;
            if (slotManager == null)
            {
                throw new InvalidOperationException("Slot manager has not been allocated");
            }
            else
            {
                int firstSlot = slotManager.NumberOfVariables;
                int highWater = ExpressionTool.AllocateSlots(exp, firstSlot, slotManager);
                if (highWater > firstSlot)
                {
                    slotManager.NumberOfVariables = highWater; // This algorithm is not very efficient because it never reuses
                    // a slot when a variable goes out of scope. But at least it is safe.
                    // Note that range variables within XPath expressions need to maintain
                    // a slot until the instruction they are part of finishes, e.g. in
                    // xsl:for-each.
                }
            }
        }

        public virtual Patterns.Pattern TypeCheck(string name, Patterns.Pattern pattern)
        {
            if (pattern == null)
            {
                return null;
            }

            try
            {
                Types.ItemType cit = Types.Type.ITEM_TYPE;
                pattern = (Patterns.Pattern)pattern.TypeCheck(MakeExpressionVisitor(), GetConfiguration().MakeContextItemStaticInfo(cit, true));
                bool usesCurrent = false;
                foreach (Operand o in pattern.Operands())
                {
                    Expression filter = o.GetChildExpression();
                    if (ExpressionTool.CallsFunction(filter, Current.FN_CURRENT, false))
                    {
                        usesCurrent = true;
                        break;
                    }
                }

                if (usesCurrent)
                {
                    PatternThatSetsCurrent p2 = new PatternThatSetsCurrent(pattern);
                    pattern.BindCurrent((ILocalBinding)(p2.CurrentBinding));
                    pattern = p2;
                }

                return pattern;
            }
            catch (XPathException err)
            {

                // we can't report a dynamic error such as divide by zero unless the pattern
                // is actually executed. We don't have an error pattern available, so we
                // construct one
                if (err.IsReportableStatically())
                {
                    XPathException e2 = new XPathException("Error in " + name + " pattern", err);
                    e2.SetLocator(this);
                    e2.ErrorCodeQName = err.ErrorCodeQName;
                    throw e2;
                }
                else
                {
                    Patterns.Pattern p = new BasePatternWithPredicate(new NodeTestPattern(ErrorType.GetInstance()), new ErrorExpression(new XmlProcessingException(err)));
                    p.SetLocation(AllocateLocation());
                    return p;
                }
            }
        }

        public virtual void FixupReferences()
        {
            ProbeStylesheetDepth();
            foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(StyleElement))))
            {
                ((StyleElement)child).FixupReferences();
            }
        }

        // Every recursive descent of the stylesheet's own element tree (ProcessAllAttributes,
        // ValidateSubtree, FixupReferences, CompileSequenceConstructor) funnels through here, so a
        // pathologically deep stylesheet raises a clean, catchable compile error instead of
        // overflowing the uncatchable .NET stack at compile time. This build phase runs before the
        // expression optimizer, so it never reaches ExpressionVisitor's static-descent guard; the
        // adaptive stack probe (round AV) covers whatever depth the running thread's stack allows.
        internal void ProbeStylesheetDepth()
        {
            Controller.CheckActiveTimeout();   // the compile's deadline: one strided look per element of the stylesheet
            try
            {
                StackGuard.Probe();
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                throw e.Describe("Stylesheet is too deeply nested (insufficient stack on this thread)", "XTSE0010", this);
            }
        }

        public virtual void ValidateSubtree(ComponentDeclaration decl, bool excludeStylesheet)
        {
            if (IsActionCompleted(StyleElement.ACTION_VALIDATE))
            {
                return;
            }

            ProbeStylesheetDepth();
            SetActionCompleted(StyleElement.ACTION_VALIDATE);
            if (validationError != null)
            {
                if (reportingCircumstances == OnFailure.REPORT_ALWAYS)
                {
                    CompileError(validationError);
                }
                else if (reportingCircumstances == OnFailure.REPORT_UNLESS_FORWARDS_COMPATIBLE && !ForwardsCompatibleModeIsEnabled())
                {
                    CompileError(validationError);
                }
                else if (reportingCircumstances == OnFailure.REPORT_STATICALLY_UNLESS_FALLBACK_AVAILABLE)
                {
                    bool hasFallback = false;
                    foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(XSLFallback))))
                    {
                        hasFallback = true;
                        ((XSLFallback)child).ValidateSubtree(decl, false);
                    }

                    if (!hasFallback)
                    {
                        CompileError(validationError);
                    }
                }
                else if (reportingCircumstances == OnFailure.REPORT_DYNAMICALLY_UNLESS_FALLBACK_AVAILABLE)
                {
                    foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(XSLFallback))))
                    {
                        ((XSLFallback)child).ValidateSubtree(decl, false);
                    }
                }
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

                ValidateChildren(decl, excludeStylesheet);
                if (GetCompilation().ErrorCount == 0)
                {
                    PostValidate();
                }
            }
        }

        protected virtual void ValidateChildren(ComponentDeclaration decl, bool excludeStylesheet)
        {
            bool containsInstructions = MayContainSequenceConstructor();
            StyleElement lastChild = null;
            bool endsWithTextTemplate = false;
            foreach (NodeInfo child in Children())
            {
                if (child is StyleElement)
                {
                    if (!(excludeStylesheet && child is XSLStylesheet))
                    {
                        endsWithTextTemplate = false;
                        if (containsInstructions && !((StyleElement)child).IsInstruction() && !IsPermittedChild((StyleElement)child))
                        {
                            ((StyleElement)child).CompileError("An " + DisplayName + " element must not contain an " + child.DisplayName + " element", "XTSE0010");
                        }

                        ((StyleElement)child).ValidateSubtree(decl, excludeStylesheet);
                        lastChild = (StyleElement)child;
                    }
                }
                else
                {
                    endsWithTextTemplate = ExamineTextNode(child);
                }
            }

            if (lastChild is XSLLocalVariable && !(this is XSLStylesheet) && !endsWithTextTemplate)
            {
                lastChild.IssueWarning("A variable with no following sibling instructions has no effect", DAXonErrorCode.SXWN9001);
            }
        }

        private bool ExamineTextNode(NodeInfo node)
        {
            if (node is TextValueTemplateNode)
            {
                ((TextValueTemplateNode)node).Validate();
                return !(((TextValueTemplateNode)node).GetContentExpression() is Literal);
            }
            else
            {
                return false;
            }
        }

        protected virtual bool IsPermittedChild(StyleElement child)
        {
            return false;
        }

        public virtual PrincipalStylesheetModule GetPrincipalStylesheetModule()
        {
            return GetCompilation().GetPrincipalStylesheetModule();
        }

        protected virtual void CheckSortComesFirst(bool sortRequired)
        {
            bool sortFound = false;
            bool nonSortFound = false;
            foreach (NodeInfo child in Children())
            {
                if (child is XSLSort)
                {
                    if (nonSortFound)
                    {
                        ((XSLSort)child).CompileError("Within " + DisplayName + ", xsl:sort elements must come before other instructions", "XTSE0010");
                    }

                    sortFound = true;
                }
                else if (child.GetNodeKind() == Types.Type.TEXT)
                {

                    // with xml:space=preserve, white space nodes may still be there
                    if (!Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {
                        nonSortFound = true;
                    }
                }
                else
                {
                    nonSortFound = true;
                }
            }

            if (sortRequired && !sortFound)
            {
                CompileError(DisplayName + " must have at least one xsl:sort child", "XTSE0010");
            }
        }

        public virtual void CheckTopLevel(string errorCode, bool allowOverride)
        {
            NodeImpl parent = GetParent();
            if (parent.Fingerprint == StandardNames.XSL_OVERRIDE)
            {
                if (!allowOverride)
                {
                    CompileError("Element " + DisplayName + " is not allowed as a child of xsl:override");
                }
            }
            else if (!IsTopLevel())
            {
                CompileError("Element " + DisplayName + " must be top-level (a child of xsl:stylesheet, xsl:transform, or xsl:package)", errorCode);
            }
        }

        public virtual void CheckEmpty()
        {
            if (HasChildNodes())
            {
                CompileError("Element must be empty", "XTSE0260");
            }
        }

        public virtual void ReportAbsence(string attribute)
        {
            CompileError("Element must have an " + Err.Wrap(attribute, Err.ATTRIBUTE) + " attribute", "XTSE0010");
        }

        public virtual Expression Compile(Compilation compilation, ComponentDeclaration decl)
        {

            // no action: default for non-instruction elements
            return null;
        }

        // A loop over the ancestors rather than a call per level: it is asked from the deepest instruction.
        public virtual bool IsWithinDeclaredStreamableConstruct()
        {
            for (NodeInfo e = this; e is StyleElement s; e = s.GetParent())
            {
                bool? declared = s.DeclaredStreamability();
                if (declared.HasValue)
                {
                    return declared.Value;
                }
            }

            return false;
        }

        // Whether this element itself settles streamability; null leaves it to the parent.
        protected virtual bool? DeclaredStreamability()
        {
            if (IsInXsltNamespace())
            {
                string streamableAtt = GetAttributeValue("streamable");
                if (streamableAtt != null)
                {
                    return ProcessStreamableAtt(streamableAtt);
                }
            }

            return null;
        }

        protected virtual string GenerateId()
        {
            StringBuilder buff = new StringBuilder(16);
            GenerateId(buff);
            return buff.ToString();
        }

        public virtual void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
        }

        public virtual Expression CompileSequenceConstructor(Compilation compilation, ComponentDeclaration decl, bool includeParams)
        {

            // If there are any xsl:on-empty or xsl:on-non-empty children, then reorder the children so
            // that local variable declarations come first. This is necessary to ensure that the instructions
            // remain part of a single "block", since the containing block affects the semantics of
            // on-empty and on-non-empty. Moving variables to come first would probably be a safe strategy in all
            // cases, but there might be a performance disadvantage in some cases, and it's unnecessarily disruptive,
            // especially if there are calls on user extension functions having side-effects.
            // Note: we have already bound variable references to their declarations at this stage, so the reordering
            // does not change the scope of variables.
            // We also move any on-empty instructions to the end of the list, since this makes streaming easier.
            bool containsEmptyTest = false;
            foreach (NodeInfo child in Children())
            {
                int fp = child.Fingerprint;
                if (fp == StandardNames.XSL_ON_EMPTY || fp == StandardNames.XSL_ON_NON_EMPTY)
                {
                    containsEmptyTest = true;
                }
            }

            if (containsEmptyTest)
            {
                IList<NodeInfo> vars = new List<NodeInfo>();
                IList<NodeInfo> onEmpties = new List<NodeInfo>();
                IList<NodeInfo> others = new List<NodeInfo>();
                foreach (NodeInfo kid in Children())
                {
                    int fp = kid.Fingerprint;
                    if (fp == StandardNames.XSL_VARIABLE || fp == StandardNames.XSL_PARAM)
                    {
                        vars.Add(kid);
                    }
                    else if (fp == StandardNames.XSL_ON_EMPTY)
                    {
                        onEmpties.Add(kid);
                    }
                    else
                    {
                        others.Add(kid);
                    }
                }

                vars.AddRange(others);
                vars.AddRange(onEmpties);
                return CompileSequenceConstructor(compilation, decl, new NodeListIterator(vars), includeParams);
            }
            else
            {
                return CompileSequenceConstructor(compilation, decl, IterateAxis(AxisInfo.CHILD), includeParams);
            }
        }

        public virtual Expression CompileSequenceConstructor(Compilation compilation, ComponentDeclaration decl, ISequenceIterator iter, bool includeParams)
        {
            ProbeStylesheetDepth();
            ILocation locationId = AllocateLocation();
            IList<Expression> contents = new List<Expression>(10);
            bool containsSpecials = false;
            NodeInfo node;
            while ((node = (NodeInfo)iter.Next()) != null)
            {
                if (node.GetNodeKind() == Types.Type.TEXT)
                {
                    if (IsExpandingText())
                    {
                        CompileContentValueTemplate((TextImpl)node, contents);
                    }
                    else
                    {

                        // handle literal text nodes by generating an xsl:value-of instruction, unless expand-text is enabled
                        IAxisIterator lookahead = node.IterateAxis(AxisInfo.FOLLOWING_SIBLING);
                        NodeInfo sibling = lookahead.Next();
                        if (!(sibling is XSLLocalParam || sibling is XSLSort || sibling is XSLContextItem || sibling is XSLOnCompletion))
                        {

                            // The test for XSLParam and XSLSort is to eliminate whitespace nodes that have been retained
                            // because of xml:space="preserve"
                            Expression text = new ValueOf(new StringLiteral(node.UnicodeStringValue), false, false);
                            text.SetLocation(AllocateLocation());

                            //                        if (injector != null) {
                            //                            Expression tracer = injector.inject(text);
                            //                            text = tracer;
                            //                        }
                            contents.Add(text);
                        }
                    }
                }
                else if (node is XSLLocalVariable)
                {
                    XSLLocalVariable var = (XSLLocalVariable)node;
                    SourceBinding sourceBinding = var.GetSourceBinding();
                    var.CompileLocalVariable(compilation, decl);
                    Expression tail = CompileSequenceConstructor(compilation, decl, iter, includeParams);
                    if (tail == null || Literal.IsEmptySequence(tail))
                    {
                    }
                    else
                    {
                        LetExpression let = new LetExpression();
                        let.SetInstruction(true);
                        let.SetRequiredType(var.GetRequiredType());
                        let.SetVariableQName(sourceBinding.VariableQName);
                        let.Sequence = sourceBinding.GetSelectExpression();
                        let.SetAction(tail);
                        sourceBinding.FixupBinding(let);
                        locationId = ((StyleElement)node).AllocateLocation();
                        let.SetLocation(locationId);

                        //                        TraceExpression t = new TraceExpression(let);
                        //                        contents.add(t);
                        //                    } else {
                        contents.Add(let);

                        //                    }
                        if (var.ChangesRetainedStaticContext())
                        {
                            let.SetRetainedStaticContext(MakeRetainedStaticContext());
                        } //result.setLocationId(locationId);
                    }
                }
                else if (node is StyleElement)
                {
                    StyleElement snode = (StyleElement)node;
                    int fp = snode.Fingerprint;
                    if (fp == StandardNames.XSL_ON_EMPTY || fp == StandardNames.XSL_ON_NON_EMPTY)
                    {
                        containsSpecials = true;
                    }

                    Expression child;
                    if (snode.validationError != null && !(snode is AbsentExtensionElement))
                    {
                        if (snode.reportingCircumstances == OnFailure.REPORT_IF_INSTANTIATED)
                        {
                            child = new ErrorExpression(snode.validationError);
                        }
                        else
                        {
                            child = FallbackProcessing(compilation, decl, snode);
                        }
                    }
                    else
                    {
                        child = snode.Compile(compilation, decl);
                        if (child != null)
                        {
                            if (snode.ChangesRetainedStaticContext())
                            {
                                child.SetRetainedStaticContext(snode.MakeRetainedStaticContext());
                            }

                            SetInstructionLocation(snode, child);
                        }
                    }

                    if (child != null)
                    {
                        contents.Add(child);
                    }
                }
            }

            Expression block;
            if (containsSpecials)
            {
                block = new ConditionalBlock(contents);
            }
            else
            {
                block = Block.MakeBlock(contents);
            }

            if (block.GetLocation() == null)
            {
                block.SetLocation(locationId);
            }

            if (block.LocalRetainedStaticContext == null)
            {
                block.SetRetainedStaticContext(MakeRetainedStaticContext());
            }

            return block;
        }

        protected virtual void CompileContentValueTemplate(TextImpl node, IList<Expression> contents)
        {
            if (node is TextValueTemplateNode)
            {
                Expression exp = ((TextValueTemplateNode)node).GetContentExpression();
                if (GetConfiguration().GetBooleanProperty(Feature<bool>.STRICT_STREAMABILITY) && !(exp is Literal))
                {
                    exp = new SequenceInstr(exp);
                }

                contents.Add(exp);
            }
            else
            {
                contents.Add(new StringLiteral(node.UnicodeStringValue));
            }
        }

        protected static void SetInstructionLocation(StyleElement source, Expression child)
        {
            if (child.GetLocation() == null || child.GetLocation() == Loc.NONE)
            {
                child.SetLocation(source.SaveLocation());
            }
        }

        protected virtual Expression FallbackProcessing(Compilation exec, ComponentDeclaration decl, StyleElement instruction)
        {

            // process any xsl:fallback children; if there are none,
            // generate code to report the original failure reason
            Expression fallback = null;
            foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(XSLFallback))))
            {
                Expression b = ((XSLFallback)child).CompileSequenceConstructor(exec, decl, true);
                if (b == null)
                {
                    b = Literal.MakeEmptySequence();
                }

                if (fallback == null)
                {
                    fallback = b;
                }
                else
                {
                    fallback = Block.MakeBlock(fallback, b);
                    fallback.SetLocation(AllocateLocation());
                }
            }

            if (fallback != null)
            {
                return fallback;
            }
            else
            {
                return new ErrorExpression(instruction.validationError);
            }
        }

        protected virtual ILocation AllocateLocation()
        {
            if (savedLocation == null)
            {
                savedLocation = new Loc(this);
            }

            return savedLocation;
        }

        protected internal virtual SortKeyDefinitionList MakeSortKeys(Compilation compilation, ComponentDeclaration decl)
        {

            // handle sort keys if any
            int numberOfSortKeys = 0;
            foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(XSLSortOrMergeKey))))
            {
                ((XSLSortOrMergeKey)child).Compile(compilation, decl);
                if (child is XSLSort)
                {
                    if (numberOfSortKeys != 0 && ((XSLSort)child).Stable != null)
                    {
                        CompileError("stable attribute may appear only on the first xsl:sort element", "XTSE1017");
                    }
                }

                numberOfSortKeys++;
            }

            if (numberOfSortKeys > 0)
            {
                SortKeyDefinition[] keys = new SortKeyDefinition[numberOfSortKeys];
                int k = 0;
                foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(XSLSortOrMergeKey))))
                {
                    keys[k++] = (SortKeyDefinition)((XSLSortOrMergeKey)child).GetSortKeyDefinition().Simplify();
                }

                return new SortKeyDefinitionList(keys);
            }
            else
            {
                return null;
            }
        }

        protected virtual StructuredQName[] GetUsedAttributeSets(string use)
        {
            IList<StructuredQName> nameList = new List<StructuredQName>(4);
            foreach (string asetname in use.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                StructuredQName name = MakeQName(asetname, "XTSE0710", "use-attribute-sets");
                nameList.Add(name);
            }

            return nameList.ToArray();
        }

        public virtual Visibility InterpretVisibilityValue(string s, string flags)
        {
            foreach (Visibility v in (Visibility[])Enum.GetValues(typeof(Visibility)))
            {
                if (v.ToString().ToLowerInvariant().Equals(s) && (flags.Contains("h") || !s.Equals("hidden")) && (flags.Contains("a") || !s.Equals("absent")))
                {
                    return v;
                }
            }

            InvalidAttribute("visibility", "public|final|private|abstract" + (flags.Contains("h") ? "|hidden" : "") + (flags.Contains("a") ? "|absent" : ""));
            return Visibility.UNDEFINED;
        }

        public virtual WithParam[] GetWithParamInstructions(Expression parent, Compilation compilation, ComponentDeclaration decl, bool tunnel)
        {
            int count = 0;
            foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(XSLWithParam))))
            {
                XSLWithParam wp = (XSLWithParam)child;
                if (wp.GetSourceBinding().HasProperty(SourceBinding.BindingProperty.TUNNEL) == tunnel)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return WithParam.EMPTY_ARRAY;
            }

            WithParam[] array = new WithParam[count];
            count = 0;
            foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(XSLWithParam))))
            {
                XSLWithParam wp = (XSLWithParam)child;
                if (wp.GetSourceBinding().HasProperty(SourceBinding.BindingProperty.TUNNEL) == tunnel)
                {
                    WithParam p = wp.CompileWithParam(parent, compilation, decl);
                    if (wp.GetParent() is XSLNextIteration && wp.HasChildNodes())
                    {

                        // Type-check against the declared type of the xsl:param, unless this was done earlier
                        Values.SequenceType required = ((XSLNextIteration)wp.GetParent()).GetDeclaredParamType(wp.GetSourceBinding().VariableQName);
                        wp.CheckAgainstRequiredType(required);
                        p.SelectOperand.SetChildExpression(wp.sourceBinding.GetSelectExpression());
                    }

                    array[count++] = p;
                }
            }

            return array;
        }

        public virtual void CompileError(IXmlProcessingError error)
        {
            XmlProcessingIncident.MaybeSetHostLanguage(error, HostLanguage.XSLT);

            // Set the location of the error if there is no current location information,
            // or if the current location information is local to an XPath expression, unless we are
            // positioned on an xsl:function or xsl:template, in which case this would lose too much information
            if (error.GetLocation() == null || ((error.GetLocation() is Loc || error.GetLocation() is Expression) && !(this is IStylesheetComponent)))
            {
                XmlProcessingIncident.MaybeSetLocation(error, this);
            }

            GetCompilation().ReportError(error);
        }

        public virtual void CompileError(XPathException err)
        {
            if (err.GetLocator() == null)
            {
                err.SetLocation(this);
            }

            XmlProcessingIncident se = new XmlProcessingIncident(err.Message, err.ShowErrorCode(), err.GetLocator());
            se.SetHostLanguage(HostLanguage.XSLT);
            se.SetFailingExpression(err.GetFailingExpression());
            CompileError(se);
        }

        public virtual void CompileError(string message)
        {
            CompileError(message, "XTSE0010");
        }

        public virtual void CompileError(string message, StructuredQName errorCode)
        {
            XmlProcessingIncident error = new XmlProcessingIncident(message, errorCode.EQName, this);
            error.SetHostLanguage(HostLanguage.XSLT);
            CompileError(error);
        }

        public virtual void CompileError(string message, string errorCode)
        {
            CompileError(new XPathException(message, errorCode, this));
        }

        public virtual void CompileError(string message, string errorCode, ILocation loc)
        {
            CompileError(new XPathException(message, errorCode, loc));
        }

        public virtual void CompileErrorInAttribute(string message, string errorCode, string attributeName)
        {
            StructuredQName att = StructuredQName.FromClarkName(attributeName);
            ILocation location = new AttributeLocation(this, att);
            CompileError(new XPathException(message, errorCode, location));
        }

        public virtual void CompileErrorInAttribute(XPathException ex, string attributeName)
        {
            StructuredQName att = StructuredQName.FromClarkName(attributeName);
            CompileError(ex.WithLocation(new AttributeLocation(this, att)));
        }

        protected virtual void InvalidAttribute(string attributeName, string allowedValues)
        {
            CompileErrorInAttribute("Attribute " + DisplayName + "/@" + attributeName + " must be " + allowedValues, "XTSE0020", attributeName);
        }

        protected virtual bool RequireXslt40Attribute(string attributeName)
        {
            if (attributeName == null)
                throw new NullReferenceException();
            if (compilation.GetCompilerInfo().XsltVersion != 40)
            {
                if (ForwardsCompatibleModeIsEnabled())
                {
                    CompileWarning("Attribute " + DisplayName + "/@" + attributeName + " is ignored in forwards compatibility mode " + "(running an XSLT 3.0 processor against an XSLT 4.0 stylesheet)", "XTSE0090");
                    return false;
                }
                else
                {
                    CompileErrorInAttribute("Attribute " + DisplayName + "/@" + attributeName + " is allowed only if XSLT 4.0 is enabled", "XTSE0020", attributeName);
                }
            }

            return true;
        }

        protected virtual void RequireXslt40Element()
        {
            if (compilation.GetCompilerInfo().XsltVersion != 40)
            {
                CompileError("Element " + DisplayName + " is allowed only if XSLT 4.0 is enabled", "XTSE0010");
            }
        }

        protected virtual void UndeclaredNamespaceError(string prefix, string errorCode, string attributeName)
        {
            if (errorCode == null)
            {
                errorCode = "XTSE0280";
            }

            CompileErrorInAttribute("Undeclared namespace prefix " + Err.Wrap(prefix), errorCode, attributeName);
        }

        public virtual void CompileWarning(string message, StructuredQName errorCode)
        {
            GetCompilation().ReportWarning(message, errorCode.EQName, this);
        }

        public virtual void CompileWarning(string message, string errorCode)
        {
            GetCompilation().ReportWarning(message, errorCode, this);
        }

        public virtual void IssueWarning(string message, string errorCode, ILocation locator)
        {
            GetCompilation().ReportWarning(message, errorCode, locator == null ? this : locator);
        }

        public virtual void IssueWarning(string message, string errorCode)
        {
            GetCompilation().ReportWarning(message, errorCode, this);
        }

        public virtual bool IsTopLevel()
        {
            return GetParent() is XSLModuleRoot;
        }

        protected virtual bool IsConstructingComplexContent()
        {
            if (!IsInstruction())
            {
                return false;
            }

            NodeInfo parent = GetParent();
            while (true)
            {
                if (!(parent is StyleElement && ((StyleElement)parent).IsInstruction()))
                {
                    return false;
                }

                if (parent is XSLGeneralVariable)
                {
                    return ((XSLGeneralVariable)parent).GetAttributeValue("as") == null;
                }

                if (parent is XSLElement || parent is LiteralResultElement || parent is XSLDocument || parent is XSLCopy)
                {
                    return true;
                }

                parent = parent.GetParent();
            }
        }

        public virtual SourceBinding GetBindingInformation(StructuredQName name)
        {
            return null;
        }

        public virtual SourceBinding BindVariable(StructuredQName variableName, StructuredQName attributeName)
        {
            SourceBinding decl = BindLocalVariable(variableName, attributeName);
            if (decl != null)
            {
                return decl;
            }


            // Now check for a global variable
            // we rely on the search following the order of decreasing import precedence.
            SourceBinding binding = GetPrincipalStylesheetModule().GetGlobalVariableBinding(variableName);
            if (binding == null || Navigator.IsAncestorOrSelf(binding.SourceElement, this))
            {

                // test case variable-0118
                return null;
            }
            else
            {
                return binding;
            }
        }

        public virtual SourceBinding BindLocalVariable(StructuredQName variableName, StructuredQName attributeName)
        {
            NodeInfo curr = this;
            NodeInfo prev = this;
            SourceBinding @implicit = HasImplicitBinding(variableName, attributeName);
            if (@implicit != null)
            {
                return @implicit;
            }


            // first search for a local variable declaration
            if (!IsTopLevel())
            {
                while (curr is StyleElement && !((StyleElement)curr).SeesAvuncularVariables())
                {

                    // a local variable is not visible within a sibling xsl:fallback or xsl:catch element
                    curr = curr.GetParent();
                }

                IAxisIterator preceding = curr.IterateAxis(AxisInfo.PRECEDING_SIBLING);
                while (true)
                {
                    curr = preceding.Next();
                    while (curr == null)
                    {
                        curr = prev.GetParent();
                        if (curr is StyleElement)
                        {
                            @implicit = ((StyleElement)curr).HasImplicitBinding(variableName, null);
                            if (@implicit != null)
                            {
                                return @implicit;
                            }
                        }

                        while (curr is StyleElement && !((StyleElement)curr).SeesAvuncularVariables())
                        {

                            // a local variable is not visible within a sibling xsl:fallback or xsl:catch element
                            curr = curr.GetParent();
                        }

                        prev = curr;
                        if (curr.GetParent() is XSLModuleRoot)
                        {
                            break; // top level
                        }

                        preceding = curr.IterateAxis(AxisInfo.PRECEDING_SIBLING);
                        curr = preceding.Next();
                    }

                    if (curr.GetParent() is XSLModuleRoot)
                    {
                        break;
                    }

                    if (curr is XSLGeneralVariable)
                    {
                        SourceBinding sourceBinding = ((XSLGeneralVariable)curr).GetBindingInformation(variableName);
                        if (sourceBinding != null)
                        {
                            return sourceBinding;
                        }
                    }
                }
            }

            return null;
        }

        protected virtual bool SeesAvuncularVariables()
        {
            return true;
        }

        protected virtual SourceBinding HasImplicitBinding(StructuredQName variableName, StructuredQName attributeName)
        {
            return null;
        }

        public virtual StructuredQName GetObjectName()
        {
            return objectName;
        }

        public virtual void SetObjectName(StructuredQName qName)
        {
            objectName = qName;
        }

        public virtual IEnumerator<string> GetProperties()
        {
            IList<string> list = new List<string>(10);
            foreach (AttributeInfo att in Attributes())
            {
                list.Add(att.GetNodeName().GetStructuredQName().ClarkName);
            }

            return list.GetEnumerator();
        }

        public virtual bool IsActionCompleted(int action)
        {
            return (actionsCompleted & action) != 0;
        }

        public virtual void SetActionCompleted(int action)
        {
            actionsCompleted |= action;
        }
        public enum OnFailure
        {
            REPORT_ALWAYS,
            REPORT_UNLESS_FORWARDS_COMPATIBLE,
            REPORT_IF_INSTANTIATED,
            REPORT_STATICALLY_UNLESS_FALLBACK_AVAILABLE,
            REPORT_DYNAMICALLY_UNLESS_FALLBACK_AVAILABLE,
            IGNORED_INSTRUCTION
        }
    }
}