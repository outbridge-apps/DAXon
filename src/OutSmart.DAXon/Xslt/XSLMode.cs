////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLMode : StyleElement
    {
        private SimpleMode mode;
        private HashSet<Accumulator> accumulators;
        private bool prepared = false;
        private bool streamable = false;
        private bool traceMatching = false;
        public override bool IsDeclaration()
        {
            return true;
        }

        public override bool IsInstruction()
        {
            return false;
        }

        public override StructuredQName GetObjectName()
        {
            StructuredQName qn = base.GetObjectName();
            if (qn == null)
            {
                string nameAtt = Whitespace.Trim(GetAttributeValue(NamespaceUri.NULL, "name"));
                if (nameAtt == null)
                {
                    return Mode.UNNAMED_MODE_NAME;
                }

                qn = MakeQName(nameAtt, null, "name");
                SetObjectName(qn);
            }

            return qn;
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            StructuredQName name = GetObjectName();
            bool enclosing = HasChildNodes();
            SymbolicName sName = new SymbolicName(StandardNames.XSL_MODE, name);
            Dictionary<SymbolicName, Component> componentIndex = top.GetStylesheetPackage().ComponentIndex;

            // see if there is already a named mode with this precedence
            if (!name.Equals(Mode.UNNAMED_MODE_NAME))
            {
                Component other = componentIndex.GetOrDefault(sName);
                if (other != null && other.DeclaringPackage != top.GetStylesheetPackage())
                {
                    CompileError("Mode " + name.DisplayName + " conflicts with a public named mode in package " + other.DeclaringPackage.PackageName, "XTSE3050");
                }

                if (other != null && (((Mode)other.GetActor()).IsEnclosingMode() || HasChildNodes()))
                {
                    CompileError("The mode name " + name.DisplayName + " identifies an enclosing mode so its name must be unique ", "XTSE4025");
                }
            }

            mode = (SimpleMode)top.GetRuleManager().ObtainMode(name, true);
            mode.SetEnclosingMode(enclosing);
            if (name.Equals(Mode.UNNAMED_MODE_NAME))
            {
                top.GetRuleManager().SetUnnamedModeExplicit(true);
            }
            else if (mode.GetDeclaringComponent().DeclaringPackage != ContainingPackage)
            {
                CompileError("Mode name conflicts with a mode in a used package", "XTSE3050");
            }
            else
            {
                top.IndexMode(decl);
                Visibility declaredVisibility = DeclaredVisibility;
                Visibility actualVisibility = declaredVisibility == Visibility.UNDEFINED ? Visibility.PRIVATE : declaredVisibility;
                VisibilityProvenance provenance = declaredVisibility == Visibility.UNDEFINED ? VisibilityProvenance.DEFAULTED : VisibilityProvenance.EXPLICIT;
                mode.GetDeclaringComponent().SetVisibility(actualVisibility, provenance);
                top.IndexMode(decl);
            }
        }

        public override void PrepareAttributes()
        {
            string nameAtt = null;
            string visibilityAtt = null;
            string asAtt = null;
            if (prepared)
            {
                return;
            }

            prepared = true;
            Visibility visibility = Visibility.PRIVATE;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "streamable":
                        streamable = ProcessStreamableAtt(value);
                        break;
                    case "name":
                        nameAtt = Whitespace.Trim(value);
                        if (!nameAtt.Equals("#default"))
                        {
                            SetObjectName(MakeQName(nameAtt, null, "name"));
                        }

                        break;
                    case "use-accumulators":
                        accumulators = GetPrincipalStylesheetModule().GetStylesheetPackage().AccumulatorRegistry.GetUsedAccumulators(value, this);
                        break;
                    case "on-multiple-match":
                        {
                            switch (Whitespace.Trim(value))
                            {
                                case "fail":
                                case "use-last":
                                    break;
                                default:
                                    InvalidAttribute(f, "fail|use-last");
                                    break;
                            }

                            break;
                        }

                    case "on-no-match":
                        switch (Whitespace.Trim(value))
                        {
                            case "text-only-copy":
                            case "shallow-copy":
                            case "deep-copy":
                            case "shallow-skip":
                            case "deep-skip":
                            case "fail":
                                break;
                            case "shallow-copy-all":
                                RequireXslt40Attribute("on-no-match");
                                break;
                            default:
                                InvalidAttribute(f, "text-only-copy|shallow-copy|deep-copy|shallow-skip|deep-skip|fail");
                                break;
                        }

                        break;
                    case "warning-on-multiple-match":
                        {
                            ProcessBooleanAttribute("warning-on-multiple-match", value);
                            break;
                        }

                    case "warning-on-no-match":
                        {
                            ProcessBooleanAttribute("warning-on-no-match", value);
                            break;
                        }

                    case "typed":
                        {
                            CheckAttributeValue("typed", Whitespace.Trim(value), false, new string[] { "0", "1", "false", "lax", "no", "strict", "true", "unspecified", "yes" });
                            break;
                        }

                    case "visibility":
                        visibilityAtt = Whitespace.Trim(value);
                        visibility = InterpretVisibilityValue(visibilityAtt, "");
                        if (visibility == Visibility.ABSTRACT)
                        {
                            InvalidAttribute(f, "public|private|final");
                        }

                        mode.DeclaredVisibility = visibility;
                        break;
                    case "as":
                        if (RequireXslt40Attribute("as"))
                        {
                            asAtt = value;
                        }

                        break;
                    default:
                        if (attName.HasURI(NamespaceUri.SAXON))
                        {
                            IsExtensionAttributeAllowed(attName.DisplayName);
                            if (attName.GetLocalPart().Equals("trace"))
                            {
                                traceMatching = ProcessBooleanAttribute("saxon:trace", value);
                            }
                            else if (attName.GetLocalPart().Equals("as"))
                            {
                                asAtt = value;
                            }
                        }
                        else
                        {
                            CheckUnknownAttribute(attName);
                        }

                        break;
                }
            }

            if (nameAtt == null && visibilityAtt != null && mode.DeclaredVisibility != Visibility.PRIVATE)
            {
                CompileError("The unnamed mode must be private", "XTSE0020");
            }

            RuleManager manager = GetCompilation().GetPrincipalStylesheetModule().GetRuleManager();
            if (GetObjectName() == null)
            {
                mode = manager.UnnamedMode;
            }
            else
            {
                Mode m = manager.ObtainMode(GetObjectName(), true);
                if (m is SimpleMode)
                {
                    mode = (SimpleMode)m;
                }
                else
                {
                    CompileError("Mode name refers to an overridden mode");
                    mode = manager.UnnamedMode;
                }
            }

            mode.ObtainDeclaringComponent(this); // TODO: how does this work with multiple mode declarations?
            mode.SetModeTracing(traceMatching); // Saxon extension; ignore the complications of multiple xsl:mode declarations for now
            if (asAtt != null)
            {

                // Saxon extension; ignore the complications of multiple xsl:mode declarations for now
                SequenceType extraResultType;
                try
                {
                    extraResultType = MakeExtendedSequenceType(asAtt);
                }
                catch (XPathException e)
                {
                    CompileErrorInAttribute(e, "saxon:as");
                    extraResultType = SequenceType.ANY_SEQUENCE; // error recovery
                }

                mode.DefaultResultType = extraResultType;
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            CheckTopLevel("XTSE0010", false);
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string attValue = att.Value;
                if (f.Equals("streamable") || f.Equals("on-multiple-match") || f.Equals("on-no-match") || f.Equals("warning-on-multiple-match") || f.Equals("warning-on-no-match") || f.Equals("typed") || f.Equals("visibility"))
                {
                    string trimmed = Whitespace.Trim(attValue);
                    string normalizedAtt;
                    if ("true".Equals(trimmed) || "1".Equals(trimmed))
                    {
                        normalizedAtt = "yes";
                    }
                    else if ("false".Equals(trimmed) || "0".Equals(trimmed))
                    {
                        normalizedAtt = "no";
                    }
                    else
                    {
                        normalizedAtt = trimmed;
                    }

                    if (f.Equals("streamable") && !streamable)
                    {

                        // we've decided earlier to fall back to non-streaming
                        normalizedAtt = "no";
                    }

                    mode.ActivePart.SetExplicitProperty(f, normalizedAtt, decl.Precedence);
                    if (mode.IsMustBeTyped() && ContainingPackage.TargetEdition.MatchesRegex("JS\\d?"))
                    {
                        IssueWarning("In SaxonJS, all data is untyped", "XTTE3110");
                    }
                } /*Can be null after an error*/
                else if (f.Equals("use-accumulators") && accumulators != null)
                {
                    string[] names = new string[accumulators.Count];
                    int i = 0;
                    foreach (Accumulator acc in accumulators)
                    {
                        names[i++] = acc.AccumulatorName.EQName;
                    }

                    Array.Sort(names);
                    StringBuilder allNames = new StringBuilder();
                    bool first = true;
                    foreach (string name in names)
                    {
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            allNames.Append(' ');
                        }

                        allNames.Append(name);
                    }

                    mode.ActivePart.SetExplicitProperty(f, allNames.ToString(), decl.Precedence);
                }
            }

            if (GetCompilation().GetCompilerInfo().XsltVersion != 40)
            {
                CheckEmpty();
            }
            else
            {
                if (HasChildNodes())
                {
                    if (GetAttributeValue(NamespaceUri.NULL, "name") == null)
                    {
                        CompileError("A xsl:mode declaration with child xsl:template elements " + "must have a name attribute", "XTSE4005");
                    }

                    string v = GetAttributeValue(NamespaceUri.NULL, "default-mode");
                    if (v != null && !GetObjectName().Equals(MakeQName(v, null, "default-mode")))
                    {
                        CompileError("A xsl:mode declaration with child xsl:template elements must not have " + "a default-mode attribute that differs from the mode name", "XTSE4015");
                    }

                    foreach (NodeInfo n in Children())
                    {
                        if (n.GetNodeKind() == Types.Type.ELEMENT && !(n is XSLTemplate))
                        {
                            CompileError("The only children permitted for xsl:mode are xsl;template elements");
                        }

                        v = n.GetAttributeValue(NamespaceUri.NULL, "default-mode");
                        if (v != null && !GetObjectName().Equals(MakeQName(v, null, "default-mode")))
                        {
                            CompileError("An xsl:template declaration within an enclosing xsl:mode must not have " + "a default-mode attribute that differs from the mode name", "XTSE4015");
                        }
                    }
                }
            }

            CheckTopLevel("XTSE0010", false);
        }

        /*Can be null after an error*/
        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
            StylesheetPackage pack = GetPrincipalStylesheetModule().GetStylesheetPackage();
            Component c = pack.GetComponent(mode.GetSymbolicName());
            if (c == null)
            {
                throw new InvalidOperationException();
            }

            foreach (NodeInfo t in Children())
            {
                if (t is XSLTemplate)
                {
                    ComponentDeclaration templateDecl = new ComponentDeclaration(decl.Module, (XSLTemplate)t);
                    ((XSLTemplate)t).CompileDeclaration(compilation, templateDecl);
                }
            }
        }

        /*Can be null after an error*/
        public SimpleMode GetMode()
        {
            return mode;
        }
    }
}