////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using System;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class LiteralResultElement : StyleElement
    {
        private static readonly IntHashSet STANDARD_ATTRIBUTES = IntHashSet.Of(StandardNames.XSL_USE_ATTRIBUTE_SETS, StandardNames.XSL_DEFAULT_COLLATION, StandardNames.XSL_DEFAULT_MODE, StandardNames.XSL_DEFAULT_VALIDATION, StandardNames.XSL_EXTENSION_ELEMENT_PREFIXES, StandardNames.XSL_EXCLUDE_RESULT_PREFIXES, StandardNames.XSL_EXPAND_TEXT, StandardNames.XSL_VERSION, StandardNames.XSL_XPATH_DEFAULT_NAMESPACE, StandardNames.XSL_TYPE, StandardNames.XSL_USE_WHEN, StandardNames.XSL_VALIDATION);
        private INodeName resultNodeName;
        private INodeName[] attributeNames;
        private Expression[] attributeValues;
        private int numberOfAttributes;
        private bool toplevel;
        private NamespaceMap retainedNamespaces = NamespaceMap.EmptyMap();
        private StructuredQName[] attributeSets;
        private ISchemaType schemaType;
        private int validation = Validation.STRIP;
        private bool inheritNamespaces = true;
        public LiteralResultElement()
        {
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override bool IsInstruction()
        {
            return true;
        }

        public override bool IsInXsltNamespace()
        {
            return false;
        }

        public override void ProcessStandardAttributes(NamespaceUri @namespace)
        {
            int processorVersion = GetCompilation().GetCompilerInfo().XsltVersion;
            if (GetParent() is DocumentImpl && processorVersion >= 40)
            {
                if (GetAttributeValue(@namespace, "version") == null)
                {
                    version = processorVersion;
                }
                else
                {
                    ProcessVersionAttribute(@namespace);
                }

                if (version >= 40 && GetAttributeValue(@namespace, "expand-text") == null)
                {
                    expandText = true;
                }
                else
                {
                    ProcessExpandTextAttribute(@namespace);
                }

                ProcessExtensionElementAttribute(@namespace);
                ProcessExcludedNamespaces(@namespace);
                ProcessDefaultXPathNamespaceAttribute(@namespace);
                ProcessDefaultValidationAttribute(@namespace);
            }
            else
            {
                base.ProcessStandardAttributes(@namespace);
            }
        }

        /// <summary>
        /// Process the attribute list
        /// </summary>
        public override void PrepareAttributes()
        {

            // Process the values of all attributes. At this stage we deal with attribute
            // values (especially AVTs), but we do not apply namespace aliasing to the
            // attribute names.
            IAttributeMap atts = Attributes();
            int num = atts.Size();
            if (num == 0)
            {
                numberOfAttributes = 0;
            }
            else
            {
                attributeNames = new INodeName[num];
                attributeValues = new Expression[num];
                numberOfAttributes = 0;
                foreach (AttributeInfo att in atts)
                {
                    INodeName name = att.GetNodeName();
                    int fp = name.Fingerprint;
                    NamespaceUri attURI = name.GetNamespaceUri();
                    if (attURI.Equals(NamespaceUri.XSLT))
                    {
                        if (!STANDARD_ATTRIBUTES.Contains(fp))
                        {

                            // Standard attributes have already been dealt with
                            if (fp == StandardNames.XSL_INHERIT_NAMESPACES)
                            {
                                inheritNamespaces = ProcessBooleanAttribute("xsl:inherit-namespaces", att.Value);
                            }
                            else if (!ForwardsCompatibleModeIsEnabled())
                            {
                                CompileError("Unknown XSLT attribute " + Err.Wrap(name.DisplayName, Err.ATTRIBUTE), "XTSE0805");
                            }
                        }
                    }
                    else
                    {
                        attributeNames[numberOfAttributes] = name;
                        Expression exp = MakeAttributeValueTemplate(att.Value, att);
                        attributeValues[numberOfAttributes] = exp;
                        numberOfAttributes++;
                    }
                }


                // now shorten the arrays if necessary. This is necessary if there are [xsl:]-prefixed
                // attributes that weren't copied into the arrays.
                if (numberOfAttributes < attributeNames.Length)
                {
                    Array.Resize(ref attributeNames, numberOfAttributes);
                    Array.Resize(ref attributeValues, numberOfAttributes);
                }
            }

            resultNodeName = GetNodeName();
        }

        /// <summary>
        /// Validate that this node is OK
        /// </summary>
        public override void Validate(ComponentDeclaration decl)
        {
            toplevel = (GetParent() is XSLStylesheet);
            resultNodeName = GetNodeName();
            if (toplevel)
            {

                // A top-level element can never be a "real" literal result element,
                // but this class gets used for unknown elements found at the top level
                if (GetNamespaceUri().IsEmpty())
                {
                    CompileError("Top level elements must have a non-null namespace URI", "XTSE0130"); // Now gets caught earlier - such elements are built as DataElement instances
                }
            }
            else
            {

                // Build the list of output namespace nodes. Note we no longer optimize this list.
                // See comments in the 9.1 source code for some history of this decision.
                retainedNamespaces = AllNamespaces;

                // Spec bug 5857: if there is no other binding for the default @namespace, add an undeclaration
                //                namespaceCodes.add(NamespaceBinding.DEFAULT_UNDECLARATION);
                //            }
                // apply any aliases required to create the list of output namespaces
                PrincipalStylesheetModule sheet = GetPrincipalStylesheetModule();
                if (sheet.HasNamespaceAliases())
                {
                    NamespaceMap aliasedNamespaces = retainedNamespaces;
                    foreach (NamespaceBinding nb in retainedNamespaces)
                    {
                        NamespaceUri suri = nb.GetNamespaceUri();
                        NamespaceBinding ncode = sheet.GetNamespaceAlias(suri);
                        if (ncode != null && !ncode.GetNamespaceUri().Equals(suri))
                        {

                            // apply the namespace alias.
                            aliasedNamespaces = aliasedNamespaces.Remove(nb.GetPrefix());
                            if (!ncode.GetNamespaceUri().IsEmpty())
                            {
                                aliasedNamespaces = aliasedNamespaces.Put(ncode.GetPrefix(), ncode.GetNamespaceUri());
                            }
                        }
                    }

                    retainedNamespaces = aliasedNamespaces;

                    // determine if there is an alias for the namespace of the element name
                    NamespaceUri elementURI = GetNamespaceUri();
                    NamespaceBinding elementAlias = sheet.GetNamespaceAlias(elementURI);
                    if (elementAlias != null && !elementAlias.GetNamespaceUri().Equals(elementURI))
                    {
                        resultNodeName = new FingerprintedQName(elementAlias.GetPrefix(), elementAlias.GetNamespaceUri(), GetLocalPart());
                    }
                }


                // deal with special attributes
                string useAttSets = GetAttributeValue(NamespaceUri.XSLT, "use-attribute-sets");
                if (useAttSets != null)
                {
                    attributeSets = GetUsedAttributeSets(useAttSets);
                }

                validation = DefaultValidation;
                string type = GetAttributeValue(NamespaceUri.XSLT, "type");
                if (type != null)
                {
                    if (!IsSchemaAware())
                    {
                        CompileError("The xsl:type attribute is available only with a schema-aware XSLT processor", "XTSE1660");
                    }

                    schemaType = GetSchemaType(type);
                    validation = Validation.BY_TYPE;
                }

                string validate = GetAttributeValue(NamespaceUri.XSLT, "validation");
                if (validate != null)
                {
                    validation = ValidateValidationAttribute(validate);
                    if (schemaType != null)
                    {
                        CompileError("The attributes xsl:type and xsl:validation are mutually exclusive", "XTSE1505");
                    }
                }


                // establish the names to be used for all the output attributes;
                // also type-check the AVT expressions
                if (numberOfAttributes > 0)
                {
                    bool changed = false;
                    for (int i = 0; i < numberOfAttributes; i++)
                    {
                        INodeName anameCode = attributeNames[i];
                        INodeName alias = anameCode;
                        NamespaceUri attURI = anameCode.GetNamespaceUri();
                        if (!attURI.IsEmpty())
                        {

                            // attribute has a namespace prefix
                            NamespaceBinding newBinding = sheet.GetNamespaceAlias(attURI);
                            if (newBinding != null && !newBinding.GetNamespaceUri().Equals(attURI))
                            {
                                alias = new FingerprintedQName(newBinding.GetPrefix(), newBinding.GetNamespaceUri(), anameCode.GetLocalPart());
                                changed = true;
                            }
                        }

                        attributeNames[i] = alias;
                        attributeValues[i] = TypeCheck(alias.DisplayName, attributeValues[i]);
                    }

                    if (changed && numberOfAttributes > 1)
                    {

                        // spec bug 30400. Check that the attribute names are still distinct
                        IntSet names = new IntHashSet(numberOfAttributes);
                        for (int i = 0; i < numberOfAttributes; i++)
                        {
                            int fp = attributeNames[i].ObtainFingerprint(GetNamePool());
                            bool absent = names.Add(fp);
                            if (!absent)
                            {
                                CompileError("As a result of namespace aliasing, two attributes have the same expanded name", "XTSE0813");
                            }
                        }
                    }
                }


                // remove any namespaces that are on the exclude-result-prefixes list.
                // The namespace is excluded even if it is the namespace of the element or an attribute,
                // though in that case namespace fixup will reinstate it.
                NamespaceMap afterExclusions = retainedNamespaces;
                foreach (NamespaceBinding nb in retainedNamespaces)
                {
                    NamespaceUri uri = nb.GetNamespaceUri();
                    if (IsExcludedNamespace(uri) && !sheet.IsAliasResultNamespace(uri))
                    {
                        afterExclusions = afterExclusions.Remove(nb.GetPrefix());
                    }
                }

                retainedNamespaces = afterExclusions;
            }
        }

        /// <summary>
        /// Validate that this node is OK
        /// </summary>
        protected override void ValidateChildren(ComponentDeclaration decl, bool excludeStylesheet)
        {
            if (!toplevel)
            {
                base.ValidateChildren(decl, excludeStylesheet);
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {

            // top level elements in the stylesheet are ignored
            if (toplevel)
            {
                return null;
            }

            FixedElement inst = new FixedElement(resultNodeName, retainedNamespaces, inheritNamespaces, true, schemaType, validation);
            inst.SetLocation(AllocateLocation());
            Expression content = CompileSequenceConstructor(exec, decl, true);
            if (numberOfAttributes > 0)
            {
                for (int i = attributeNames.Length - 1; i >= 0; i--)
                {
                    FixedAttribute att = new FixedAttribute(attributeNames[i], Validation.STRIP, null);
                    att.SetRetainedStaticContext(MakeRetainedStaticContext());
                    att.Select = attributeValues[i];
                    att.SetLocation(AllocateLocation());
                    Expression exp = att;

                    //                    TraceExpression trace = new TraceExpression(exp);
                    //                    exp = trace;
                    //                }
                    if (content == null)
                    {
                        content = exp;
                    }
                    else
                    {
                        content = Block.MakeBlock(exp, content);
                        content.SetLocation(AllocateLocation());
                    }
                }
            }

            if (attributeSets != null)
            {
                Expression use = UseAttributeSet.MakeUseAttributeSets(attributeSets, this);
                if (content == null)
                {
                    content = use;
                }
                else
                {
                    content = Block.MakeBlock(use, content);
                    content.SetLocation(AllocateLocation());
                }
            }

            if (content == null)
            {
                content = Literal.MakeEmptySequence();
            }

            inst.SetContentExpression(content);
            inst.SetRetainedStaticContext(MakeRetainedStaticContext());
            inst.SetLocation(AllocateLocation());
            return inst;
        }

        public DocumentImpl MakeStylesheet(bool topLevel)
        {

            // the implementation grafts the LRE node onto a containing xsl:template and
            // xsl:stylesheet
            int processorVersion = GetCompilation().GetCompilerInfo().XsltVersion;
            StyleNodeFactory nodeFactory = GetCompilation().GetStyleNodeFactory(topLevel);
            if (!IsInScopeNamespace(NamespaceUri.XSLT) && processorVersion < 40)
            {
                string message;
                if (GetLocalPart().Equals("stylesheet") || GetLocalPart().Equals("transform"))
                {
                    message = "Namespace for stylesheet element should be " + NamespaceConstant.XSLT;
                }
                else
                {
                    message = "The supplied file does not appear to be a stylesheet";
                }

                XPathException err = new XPathException(message).WithLocation(AllocateLocation()).WithErrorCode("XTSE0150").AsStaticError();

                CompileError(err);
                throw err;
            }


            // check there is an xsl:version attribute (it's mandatory until 4.0), and copy
            // it to the new xsl:stylesheet element
            string version = GetAttributeValue(NamespaceUri.XSLT, "version");
            if (version == null && processorVersion < 40)
            {
                XPathException err = new XPathException("Simplified stylesheet: xsl:version attribute is missing").WithErrorCode("XTSE0150").AsStaticError().WithLocation(AllocateLocation());

                CompileError(err);
                throw err;
            }

            try
            {
                DocumentImpl oldRoot = (DocumentImpl)Root;
                LinkedTreeBuilder builder = new LinkedTreeBuilder(GetConfiguration().MakePipelineConfiguration(), Durability.LASTING);
                builder.SetNodeFactory(nodeFactory);
                builder.SetSystemId(this.GetSystemId());
                builder.Open();
                builder.StartDocument(ReceiverOption.NONE);
                ILocation loc = Loc.NONE;
                NamespaceMap map = AllNamespaces.Put("xsl", NamespaceUri.XSLT);
                IAttributeMap atts = EmptyAttributeMap.GetInstance();
                atts = atts.Put(new AttributeInfo(new NoNamespaceName("version"), BuiltInAtomicType.UNTYPED_ATOMIC, version == null ? "4.0" : version, loc, ReceiverOption.NONE));
                if (processorVersion >= 40 && GetAttributeValue(NamespaceUri.XSLT, "expand-text") == null)
                {
                    atts = atts.Put(new AttributeInfo(new NoNamespaceName("expand-text"), BuiltInAtomicType.UNTYPED_ATOMIC, "yes", loc, ReceiverOption.NONE));
                }

                int st = StandardNames.XSL_STYLESHEET;
                builder.StartElement(new CodedName(st, "xsl", GetNamePool()), Untyped.INSTANCE, atts, map, loc, ReceiverOption.NONE);
                atts = EmptyAttributeMap.GetInstance();
                atts = atts.Put(new AttributeInfo(new NoNamespaceName("match"), BuiltInAtomicType.UNTYPED_ATOMIC, "/", loc, ReceiverOption.NONE));
                int te = StandardNames.XSL_TEMPLATE;
                builder.StartElement(new CodedName(te, "xsl", GetNamePool()), Untyped.INSTANCE, atts, map, loc, ReceiverOption.NONE);
                builder.GraftElement(this);
                builder.EndElement();
                builder.EndElement();
                builder.EndDocument();
                builder.Close();
                DocumentImpl newRoot = (DocumentImpl)builder.CurrentRoot;
                newRoot.GraftLocationMap(oldRoot);
                return newRoot;
            }
            catch (XPathException err)
            {
                throw err.WithLocation(AllocateLocation());
            }
        }

        public override StructuredQName GetObjectName()
        {
            return new StructuredQName(GetPrefix(), GetNamespaceUri(), GetLocalPart());
        }
    }
}
