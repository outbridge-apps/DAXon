////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Types;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    public class StyleNodeFactory : INodeFactory
    {
        protected Configuration config;
        protected NamePool namePool;
        private readonly Compilation compilation;
        private bool topLevelModule;
        public StyleNodeFactory(Configuration config, Compilation compilation)
        {
            this.config = config;
            this.compilation = compilation;
            namePool = config.GetNamePool();
        }

        public virtual void SetTopLevelModule(bool topLevelModule)
        {
            this.topLevelModule = topLevelModule;
        }

        public virtual bool IsTopLevelModule()
        {
            return topLevelModule;
        }

        public virtual Compilation GetCompilation()
        {
            return compilation;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual ElementImpl MakeElementNode(NodeInfo parent, INodeName elemName, ISchemaType elemType, bool isNilled, IAttributeMap attlist, NamespaceMap namespaces, PipelineConfiguration pipe, ILocation location, int sequence)
        {
            int f = elemName.ObtainFingerprint(pipe.GetConfiguration().GetNamePool());
            bool toplevel = parent is XSLModuleRoot;
            string baseURI = location.GetSystemId();
            int lineNumber = location.GetLineNumber();
            int columnNumber = location.GetColumnNumber();
            int processorVersion = compilation.GetCompilerInfo().XsltVersion;
            if (parent is DataElement)
            {
                DataElement d = new DataElement();
                d.SetNamespaceMap(namespaces);
                d.Initialise(elemName, elemType, attlist, parent, sequence);
                d.SetLocation(baseURI, lineNumber, columnNumber);
                return d;
            }


            // Try first to make an XSLT element
            StyleElement e = MakeXSLElement(f, (NodeImpl)parent);
            if ((e is XSLStylesheet || e is XSLPackage) && parent.GetNodeKind() != Types.Type.DOCUMENT)
            {
                e = new AbsentExtensionElement();
                XmlProcessingIncident reason = new XmlProcessingIncident(elemName.DisplayName + " can only appear at the outermost level", "XTSE0010");
                e.SetValidationError(reason, StyleElement.OnFailure.REPORT_ALWAYS);
            }

            if (e != null)
            {

                // recognized as an XSLT element
                e.SetCompilation(compilation);
                e.SetNamespaceMap(namespaces);
                e.Initialise(elemName, elemType, attlist, parent, sequence);
                e.SetLocation(baseURI, lineNumber, columnNumber);
                e.ProcessExtensionElementAttribute(NamespaceUri.NULL);
                e.ProcessExcludedNamespaces(NamespaceUri.NULL);
                e.ProcessVersionAttribute(NamespaceUri.NULL);
                e.ProcessDefaultXPathNamespaceAttribute(NamespaceUri.NULL);
                e.ProcessExpandTextAttribute(NamespaceUri.NULL);
                e.ProcessDefaultValidationAttribute(NamespaceUri.NULL);
                if (toplevel && !e.IsDeclaration() && !(e is XSLExpose) && e.ForwardsCompatibleModeIsEnabled())
                {
                    DataElement d = new DataElement();
                    d.SetNamespaceMap(namespaces);
                    d.Initialise(elemName, elemType, attlist, parent, sequence);
                    d.SetLocation(baseURI, lineNumber, columnNumber);
                    return d;
                }

                if (parent is AbsentExtensionElement && ((AbsentExtensionElement)parent).ForwardsCompatibleModeIsEnabled() && ((AbsentExtensionElement)parent).IsInXsltNamespace() && !(e is XSLFallback))
                {

                    // Parent is an unknown XSLT element in forwards-compatibility mode; siblings of xsl:fallback are ignored
                    AbsentExtensionElement temp = new AbsentExtensionElement();
                    temp.Initialise(elemName, elemType, attlist, parent, sequence);
                    temp.SetLocation(baseURI, lineNumber, columnNumber);
                    temp.SetCompilation(compilation);
                    temp.SetIgnoreInstruction();
                    return temp;
                }

                return e;
            }

            NamespaceUri uri = elemName.GetNamespaceUri();
            if (toplevel && !uri.Equals(NamespaceUri.XSLT))
            {
                DataElement d = new DataElement();
                d.SetNamespaceMap(namespaces);
                d.Initialise(elemName, elemType, attlist, parent, sequence);
                d.SetLocation(baseURI, lineNumber, columnNumber);
                return d;
            } // not recognized as an XSLT element, not top-level
            else
            {

                // not recognized as an XSLT element, not top-level
                string localname = elemName.GetLocalPart();
                StyleElement temp = null;

                // Detect a mis-spelt XSLT element, or a 3.0 element used in a 2.0 stylesheet
                if (uri.Equals(NamespaceUri.XSLT))
                {
                    if (parent is XSLStylesheet)
                    {
                        if (((XSLStylesheet)parent).EffectiveVersion <= processorVersion)
                        {
                            temp = new AbsentExtensionElement();
                            temp.SetCompilation(compilation);
                            temp.SetValidationError(new XmlProcessingIncident("Unknown top-level XSLT declaration " + elemName.DisplayName, "XTSE0010", location.SaveLocation()), StyleElement.OnFailure.REPORT_UNLESS_FORWARDS_COMPATIBLE);
                        }
                    }
                    else
                    {
                        temp = new AbsentExtensionElement();
                        temp.Initialise(elemName, elemType, attlist, parent, sequence);
                        temp.SetLocation(baseURI, lineNumber, columnNumber);
                        temp.SetCompilation(compilation);
                        temp.ProcessStandardAttributes(NamespaceUri.NULL);
                        temp.SetValidationError(new XmlProcessingIncident("Unknown XSLT instruction " + elemName.DisplayName, "XTSE0010", location.SaveLocation()), temp.EffectiveVersion > processorVersion ? StyleElement.OnFailure.REPORT_STATICALLY_UNLESS_FALLBACK_AVAILABLE : StyleElement.OnFailure.REPORT_ALWAYS);
                    }
                }


                // Detect an unrecognized element in the Saxon namespace
                if (uri.Equals(NamespaceUri.SAXON))
                {
                    string message = elemName.DisplayName + " is not recognized as a Saxon instruction";
                    if (config.EditionCode.Equals("HE"))
                    {
                        message += ". Saxon extensions require Saxon-PE or higher";
                    }
                    else if (!config.IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
                    {
                        message += ". No Saxon-PE or -EE license was found";
                    }

                    XmlProcessingIncident err = new XmlProcessingIncident(message, DAXonErrorCode.SXWN9008, location.SaveLocation()).AsWarning();
                    pipe.GetErrorReporter().Report(err);
                }


                // We can't work out the final class of the node until we've examined its attributes
                // such as extension-element-prefixes.
                bool extensionElement = IsExtensionNamespace(uri, parent, namespaces, attlist);
                if (temp == null)
                {
                    if (extensionElement)
                    {
                        temp = new AbsentExtensionElement();
                    }
                    else
                    {
                        temp = new LiteralResultElement();
                    }
                }

                temp.SetNamespaceMap(namespaces);
                temp.SetCompilation(compilation);
                temp.Initialise(elemName, elemType, attlist, parent, sequence);
                temp.SetLocation(baseURI, lineNumber, columnNumber);
                temp.ProcessStandardAttributes(NamespaceUri.XSLT);
                XmlProcessingIncident reason;
                if (uri.Equals(NamespaceUri.XSLT))
                {
                }
                else if (extensionElement)
                {

                    // if we can't instantiate an extension element, we don't give up
                    // immediately, because there might be an xsl:fallback defined. We
                    // create a surrogate element called AbsentExtensionElement, and
                    // save the reason for failure just in case there is no xsl:fallback
                    if (NamespaceUri.IsReserved(uri))
                    {
                        reason = new XmlProcessingIncident("Cannot use a reserved namespace for extension instructions", "XTSE0800", location.SaveLocation());
                        temp.SetValidationError(reason, StyleElement.OnFailure.REPORT_ALWAYS);
                    }
                    else
                    {
                        reason = new XmlProcessingIncident("Unknown extension instruction " + Err.Wrap(elemName.DisplayName, Err.ELEMENT), "XTDE1450", location.SaveLocation());
                        temp.SetValidationError(reason, StyleElement.OnFailure.REPORT_DYNAMICALLY_UNLESS_FALLBACK_AVAILABLE);
                    }
                }

                return temp;
            }
        }

        private static bool IsExtensionNamespace(NamespaceUri uri, NodeInfo parent, NamespaceMap namespaces, IAttributeMap attlist)
        {
            string attValue = attlist.GetValue(NamespaceUri.XSLT, "extension-element-prefixes");
            if (attValue != null)
            {
                foreach (string s0 in attValue.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string s = s0;
                    if (s == "#default")
                    {
                        s = "";
                    }

                    NamespaceUri ns = namespaces.GetURIForPrefix(s, false);
                    if (uri.Equals(ns))
                    {
                        return true;
                    }
                }
            }

            return parent is StyleElement && ((StyleElement)parent).IsExtensionNamespace(uri);
        }

        protected virtual StyleElement MakeXSLElement(int f, NodeImpl parent)
        {
            switch (f)
            {
                case StandardNames.XSL_ACCEPT:
                    return new XSLAccept();
                case StandardNames.XSL_ACCUMULATOR:
                    return new XSLAccumulator();
                case StandardNames.XSL_ACCUMULATOR_RULE:
                    return new XSLAccumulatorRule();
                case StandardNames.XSL_ANALYZE_STRING:
                    return new XSLAnalyzeString();
                case StandardNames.XSL_APPLY_IMPORTS:
                    return new XSLApplyImports();
                case StandardNames.XSL_APPLY_TEMPLATES:
                    return new XSLApplyTemplates();
                case StandardNames.XSL_ASSERT:
                    return new XSLAssert();
                case StandardNames.XSL_ATTRIBUTE:
                    return new XSLAttribute();
                case StandardNames.XSL_ATTRIBUTE_SET:
                    return new XSLAttributeSet();
                case StandardNames.XSL_BREAK:
                    return new XSLBreak();
                case StandardNames.XSL_CALL_TEMPLATE:
                    return new XSLCallTemplate();
                case StandardNames.XSL_CATCH:
                    return new XSLCatch();
                case StandardNames.XSL_CONTEXT_ITEM:
                    return new XSLContextItem();
                case StandardNames.XSL_CHARACTER_MAP:
                    return new XSLCharacterMap();
                case StandardNames.XSL_CHOOSE:
                    return new XSLChoose();
                case StandardNames.XSL_COMMENT:
                    return new XSLComment();
                case StandardNames.XSL_COPY:
                    return new XSLCopy();
                case StandardNames.XSL_COPY_OF:
                    return new XSLCopyOf();
                case StandardNames.XSL_DECIMAL_FORMAT:
                    return new XSLDecimalFormat();
                case StandardNames.XSL_DOCUMENT:
                    return new XSLDocument();
                case StandardNames.XSL_ELEMENT:
                    return new XSLElement();
                case StandardNames.XSL_EVALUATE:
                    return new XSLEvaluate();
                case StandardNames.XSL_EXPOSE:
                    return new XSLExpose();
                case StandardNames.XSL_FALLBACK:
                    return new XSLFallback();
                case StandardNames.XSL_FOR_EACH:
                    return new XSLForEach();
                case StandardNames.XSL_FOR_EACH_GROUP:
                    return new XSLForEachGroup();
                case StandardNames.XSL_FORK:
                    return new XSLFork();
                case StandardNames.XSL_FUNCTION:
                    return new XSLFunction();
                case StandardNames.XSL_GLOBAL_CONTEXT_ITEM:
                    return new XSLGlobalContextItem();
                case StandardNames.XSL_IF:
                    return new XSLIf();
                case StandardNames.XSL_IMPORT:
                    return new XSLImport();
                case StandardNames.XSL_IMPORT_SCHEMA:
                    return new XSLImportSchema();
                case StandardNames.XSL_INCLUDE:
                    return new XSLInclude();
                case StandardNames.XSL_ITEM_TYPE:
                    return new XSLItemType();
                case StandardNames.XSL_ITERATE:
                    return new XSLIterate();
                case StandardNames.XSL_KEY:
                    return new XSLKey();
                case StandardNames.XSL_MAP:
                    return new XSLMap();
                case StandardNames.XSL_MAP_ENTRY:
                    return new XSLMapEntry();
                case StandardNames.XSL_MATCHING_SUBSTRING:
                    return new XSLMatchingSubstring();
                case StandardNames.XSL_MERGE:
                    return new XSLMerge();
                case StandardNames.XSL_MERGE_ACTION:
                    return new XSLMergeAction();
                case StandardNames.XSL_MERGE_KEY:
                    return new XSLMergeKey();
                case StandardNames.XSL_MERGE_SOURCE:
                    return new XSLMergeSource();
                case StandardNames.XSL_MESSAGE:
                    return new XSLMessage();
                case StandardNames.XSL_MODE:
                    return new XSLMode();
                case StandardNames.XSL_NEXT_ITERATION:
                    return new XSLNextIteration();
                case StandardNames.XSL_NEXT_MATCH:
                    return new XSLNextMatch();
                case StandardNames.XSL_NON_MATCHING_SUBSTRING:
                    return new XSLMatchingSubstring(); //sic
                case StandardNames.XSL_NUMBER:
                    return new XSLNumber();
                case StandardNames.XSL_NAMESPACE:
                    return new XSLNamespace();
                case StandardNames.XSL_NAMESPACE_ALIAS:
                    return new XSLNamespaceAlias();
                case StandardNames.XSL_ON_COMPLETION:
                    return new XSLOnCompletion();
                case StandardNames.XSL_ON_EMPTY:
                    return new XSLOnEmpty();
                case StandardNames.XSL_ON_NON_EMPTY:
                    return new XSLOnNonEmpty();
                case StandardNames.XSL_OTHERWISE:
                    return new XSLOtherwise();
                case StandardNames.XSL_OUTPUT:
                    return new XSLOutput();
                case StandardNames.XSL_OUTPUT_CHARACTER:
                    return new XSLOutputCharacter();
                case StandardNames.XSL_OVERRIDE:
                    return new XSLOverride();
                case StandardNames.XSL_PACKAGE:
                    return new XSLPackage();
                case StandardNames.XSL_PARAM:

                    return parent is XSLModuleRoot || parent is XSLOverride ? (StyleElement)new XSLGlobalParam() : (StyleElement)new XSLLocalParam();
                case StandardNames.XSL_PERFORM_SORT:
                    return new XSLPerformSort();
                case StandardNames.XSL_PRESERVE_SPACE:
                    return new XSLPreserveSpace();
                case StandardNames.XSL_PROCESSING_INSTRUCTION:
                    return new XSLProcessingInstruction();
                case StandardNames.XSL_RESULT_DOCUMENT:
                    compilation.SetCreatesSecondaryResultDocuments(true);
                    return new XSLResultDocument();
                case StandardNames.XSL_SEQUENCE:
                    return new XSLSequence();
                case StandardNames.XSL_SORT:
                    return new XSLSort();
                case StandardNames.XSL_SOURCE_DOCUMENT:
                    return new XSLSourceDocument();
                case StandardNames.XSL_STRIP_SPACE:
                    return new XSLPreserveSpace();
                case StandardNames.XSL_STYLESHEET:
                case StandardNames.XSL_TRANSFORM:

                    return topLevelModule ? (StyleElement)new XSLPackage() : (StyleElement)new XSLStylesheet();
                case StandardNames.XSL_TEMPLATE:
                    return new XSLTemplate();
                case StandardNames.XSL_TEXT:
                    return new XSLText();
                case StandardNames.XSL_TRY:
                    return new XSLTry();
                case StandardNames.XSL_USE_PACKAGE:
                    return new XSLUsePackage();
                case StandardNames.XSL_VALUE_OF:
                    return new XSLValueOf();
                case StandardNames.XSL_VARIABLE:

                    return parent is XSLModuleRoot || parent is XSLOverride ? (StyleElement)new XSLGlobalVariable() : (StyleElement)new XSLLocalVariable();
                case StandardNames.XSL_WITH_PARAM:
                    return new XSLWithParam();
                case StandardNames.XSL_WHEN:
                    return new XSLWhen();
                case StandardNames.XSL_WHERE_POPULATED:
                    return new XSLWherePopulated();
                default:
                    return null;
            }
        }

        //sic
        public virtual TextImpl MakeTextNode(NodeInfo parent, UnicodeString content)
        {
            if (parent is StyleElement && ((StyleElement)parent).IsExpandingText())
            {
                return new TextValueTemplateNode(content);
            }
            else
            {
                return new TextImpl(content);
            }
        }

        public virtual bool IsElementAvailable(NamespaceUri uri, string localName, bool instructionsOnly)
        {
            int fingerprint = namePool.GetFingerprint(uri, localName);
            if (uri.Equals(NamespaceUri.XSLT))
            {
                if (fingerprint == -1)
                {
                    return false; // all names are pre-registered
                }

                StyleElement e = MakeXSLElement(fingerprint, null);
                if (e != null)
                {
                    return !instructionsOnly || e.IsInstruction();
                }
            }

            return false;
        }

        //sic
        public virtual AccumulatorRegistry MakeAccumulatorManager()
        {
            return new AccumulatorRegistry();
        }

        public virtual PrincipalStylesheetModule NewPrincipalModule(XSLPackage node)
        {
            return new PrincipalStylesheetModule(node);
        }
    }
}