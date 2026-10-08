////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An xsl:copy-of element in the stylesheet.
    /// </summary>
    internal sealed class CopyOf : Instruction, IValidatingInstruction
    {
        private readonly Operand selectOp;
        private readonly bool copyNamespaces;
        private bool copyAccumulators;
        private readonly int validation;
        private readonly ISchemaType schemaType;
        private bool requireDocumentOrElement;
        private readonly bool rejectDuplicateAttributes;
        private readonly bool validating;
        private bool copyLineNumbers;
        private bool copyForUpdate;
        private bool isSchemaAware = true;
        private double invocations = 1;
        private double numberOfItems = 20;

        public Expression Select
        {
            get => selectOp.GetChildExpression(); set
            {
                selectOp.SetChildExpression(value);
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_COPY_OF;

        public override int ImplementationMethod => ITERATE_METHOD | PROCESS_METHOD | WATCH_METHOD;

        public override int Dependencies => Select.Dependencies;

        /* && visitor.isOptimizeForStreaming() */
        public override string StreamerName => "CopyOf";
        public CopyOf(Expression select, bool copyNamespaces, int validation, ISchemaType schemaType, bool rejectDuplicateAttributes)
        {
            selectOp = new Operand(this, select, OperandRole.SINGLE_ATOMIC);
            this.copyNamespaces = copyNamespaces;
            this.validation = validation;
            this.schemaType = schemaType;
            validating = schemaType != null || validation == Validation.STRICT || validation == Validation.LAX;
            this.rejectDuplicateAttributes = rejectDuplicateAttributes;
        }

        public override IEnumerable<Operand> Operands()
        {
            return selectOp;
        }

        public int GetValidationAction()
        {
            return validation;
        }

        public ISchemaType GetSchemaType()
        {
            return schemaType;
        }

        public void SetSchemaAware(bool schemaAware)
        {
            this.isSchemaAware = schemaAware;
        }

        public void SetCopyLineNumbers(bool copy)
        {
            copyLineNumbers = copy;
        }

        public override bool MayCreateNewNodes()
        {
            return !Select.GetItemType().IsPlainType();
        }

        public void SetRequireDocumentOrElement(bool requireDocumentOrElement)
        {
            this.requireDocumentOrElement = requireDocumentOrElement;
        }

        public bool IsDocumentOrElementRequired()
        {
            return requireDocumentOrElement;
        }

        public void SetCopyForUpdate(bool forUpdate)
        {
            copyForUpdate = forUpdate;
        }

        public void SetCopyAccumulators(bool copy)
        {
            copyAccumulators = copy;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            CopyOf c = new CopyOf(Select.Copy(rebindings), copyNamespaces, validation, schemaType, rejectDuplicateAttributes);
            ExpressionTool.CopyLocationInfo(this, c);
            c.SetCopyForUpdate(copyForUpdate);
            c.SetCopyLineNumbers(copyLineNumbers);
            c.isSchemaAware = isSchemaAware;
            c.SetCopyAccumulators(copyAccumulators);
            return c;
        }

        public override Types.ItemType GetItemType()
        {
            Types.ItemType @in = Select.GetItemType();
            if (!isSchemaAware)
            {
                return @in;
            }

            Configuration config = GetConfiguration();
            if (schemaType != null)
            {
                TypeHierarchy th = config.GetTypeHierarchy();
                Affinity e = th.Relationship(@in, NodeKindTest.ELEMENT);
                if (e == Affinity.SAME_TYPE || e == Affinity.SUBSUMED_BY)
                {
                    return new ContentTypeTest(Types.Type.ELEMENT, schemaType, config, false);
                }

                Affinity a = th.Relationship(@in, NodeKindTest.ATTRIBUTE);
                if (a == Affinity.SAME_TYPE || a == Affinity.SUBSUMED_BY)
                {
                    return new ContentTypeTest(Types.Type.ATTRIBUTE, schemaType, config, false);
                }
            }
            else
            {
                switch (validation)
                {
                    case Validation.PRESERVE:
                        return @in;
                    case Validation.STRIP:
                        {
                            TypeHierarchy th = config.GetTypeHierarchy();
                            Affinity e = th.Relationship(@in, NodeKindTest.ELEMENT);
                            if (e == Affinity.SAME_TYPE || e == Affinity.SUBSUMED_BY)
                            {
                                return new ContentTypeTest(Types.Type.ELEMENT, Untyped.INSTANCE, config, false);
                            }

                            Affinity a = th.Relationship(@in, NodeKindTest.ATTRIBUTE);
                            if (a == Affinity.SAME_TYPE || a == Affinity.SUBSUMED_BY)
                            {
                                return new ContentTypeTest(Types.Type.ATTRIBUTE, BuiltInAtomicType.UNTYPED_ATOMIC, config, false);
                            }

                            if (e != Affinity.DISJOINT || a != Affinity.DISJOINT)
                            {

                                // it might be an element or attribute
                                if (@in is NodeTest)
                                {
                                    return AnyNodeTest.GetInstance();
                                }
                                else
                                {
                                    return AnyItemType.GetInstance();
                                }
                            }
                            else
                            {

                                // it can't be an element or attribute, so stripping type annotations can't affect it
                                return @in;
                            }
                        }

                    case Validation.STRICT:
                    case Validation.LAX:
                        if (@in is NodeTest)
                        {
                            TypeHierarchy th = config.GetTypeHierarchy();
                            int fp = ((NodeTest)@in).Fingerprint;
                            if (fp != -1)
                            {
                                Affinity e = th.Relationship(@in, NodeKindTest.ELEMENT);
                                if (e == Affinity.SAME_TYPE || e == Affinity.SUBSUMED_BY)
                                {
                                    ISchemaDeclaration elem = config.GetElementDeclaration(fp);
                                    if (elem != null)
                                    {
                                        try
                                        {
                                            return new ContentTypeTest(Types.Type.ELEMENT, elem.GetType(), config, false);
                                        }
                                        catch (MissingComponentException)
                                        {
                                            return new ContentTypeTest(Types.Type.ELEMENT, AnyType.INSTANCE, config, false);
                                        }
                                    }
                                    else
                                    {

                                        // Although there is no element declaration now, there might be one at run-time
                                        return new ContentTypeTest(Types.Type.ELEMENT, AnyType.INSTANCE, config, false);
                                    }
                                }

                                Affinity a = th.Relationship(@in, NodeKindTest.ATTRIBUTE);
                                if (a == Affinity.SAME_TYPE || a == Affinity.SUBSUMED_BY)
                                {
                                    ISchemaDeclaration attr = config.GetAttributeDeclaration(fp);
                                    if (attr != null)
                                    {
                                        try
                                        {
                                            return new ContentTypeTest(Types.Type.ATTRIBUTE, attr.GetType(), config, false);
                                        }
                                        catch (MissingComponentException)
                                        {
                                            return new ContentTypeTest(Types.Type.ATTRIBUTE, AnySimpleType.INSTANCE, config, false);
                                        }
                                    }
                                    else
                                    {

                                        // Although there is no attribute declaration now, there might be one at run-time
                                        return new ContentTypeTest(Types.Type.ATTRIBUTE, AnySimpleType.INSTANCE, config, false);
                                    }
                                }
                            }
                            else
                            {
                                Affinity e = th.Relationship(@in, NodeKindTest.ELEMENT);
                                if (e == Affinity.SAME_TYPE || e == Affinity.SUBSUMED_BY)
                                {
                                    return NodeKindTest.ELEMENT;
                                }

                                Affinity a = th.Relationship(@in, NodeKindTest.ATTRIBUTE);
                                if (a == Affinity.SAME_TYPE || a == Affinity.SUBSUMED_BY)
                                {
                                    return NodeKindTest.ATTRIBUTE;
                                }
                            }

                            return AnyNodeTest.GetInstance();
                        }
                        else if (@in is IAtomicType)
                        {
                            return @in;
                        }
                        else
                        {
                            return AnyItemType.GetInstance();
                        }
                }
            }

            return Select.GetItemType();
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return Select.GetStaticUType(contextItemType);
        }

        public override int GetCardinality()
        {
            return Select.GetCardinality();
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            if (IsDocumentOrElementRequired())
            {

                // this implies the expression is actually an XQuery validate{} expression, hence the error messages
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.TYPE_OP, "validate", 0, "XQTY0030");
                Configuration config = visitor.GetConfiguration();
                Select = config.GetTypeChecker(false).StaticTypeCheck(Select, Values.SequenceType.SINGLE_NODE, role, visitor);
                TypeHierarchy th = config.GetTypeHierarchy();
                Types.ItemType t = Select.GetItemType();
                if (th.IsSubType(t, NodeKindTest.ATTRIBUTE))
                {
                    throw new XPathException("validate{} expression cannot be applied to an attribute", "XQTY0030");
                }

                if (th.IsSubType(t, NodeKindTest.TEXT))
                {
                    throw new XPathException("validate{} expression cannot be applied to a text node", "XQTY0030");
                }

                if (th.IsSubType(t, NodeKindTest.COMMENT))
                {
                    throw new XPathException("validate{} expression cannot be applied to a comment node", "XQTY0030");
                }

                if (th.IsSubType(t, NodeKindTest.PROCESSING_INSTRUCTION))
                {
                    throw new XPathException("validate{} expression cannot be applied to a processing instruction node", "XQTY0030");
                }

                if (th.IsSubType(t, NodeKindTest.NAMESPACE))
                {
                    throw new XPathException("validate{} expression cannot be applied to a namespace node", "XQTY0030");
                }
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            selectOp.Optimize(visitor, contextItemType);
            if (Literal.IsEmptySequence(Select))
            {
                return Select;
            }

            AdoptChildExpression(Select);
            if (Select.GetItemType().IsPlainType())
            {
                return Select;
            }

            if (Select is Block)
            {

                // change copy-of(a, b, c) to (copy-of(a), copy-of(b), copy-of(c)) - bug 5958
                Block b1 = (Block)Select;
                Expression[] splitCopy = new Expression[b1.Count];
                for (int i = 0; i < splitCopy.Length; i++)
                {
                    Expression exp = b1.GetOperanda()[i].GetChildExpression().Copy(new RebindingMap());
                    if (exp.GetItemType().IsPlainType())
                    {
                        splitCopy[i] = exp;
                    }
                    else
                    {
                        splitCopy[i] = new CopyOf(exp, copyNamespaces, validation, schemaType, rejectDuplicateAttributes);
                    }
                }

                return new Block(splitCopy);
            }

            return this;
        }

        /* && visitor.isOptimizeForStreaming() */
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("copyOf", this);
            if (validation != Validation.SKIP)
            {
                @out.EmitAttribute("validation", Validation.Describe(validation));
            }

            if (schemaType != null)
            {
                @out.EmitAttribute("type", schemaType.GetStructuredQName());
            }

            StringBuilder fsb = new StringBuilder(16);
            if (requireDocumentOrElement)
            {
                fsb.Append('p');
            }

            if (rejectDuplicateAttributes)
            {
                fsb.Append('a');
            }

            if (validating)
            {
                fsb.Append('v');
            }

            if (copyLineNumbers)
            {
                fsb.Append('l');
            }

            if (copyForUpdate)
            {
                fsb.Append('u');
            }

            if (isSchemaAware)
            {
                fsb.Append('s');
            }

            if (copyNamespaces)
            {
                fsb.Append('c');
            }

            if (copyAccumulators)
            {
                fsb.Append('m');
            }

            if (fsb.Length != 0)
            {
                @out.EmitAttribute("flags", fsb.ToString());
            }

            Select.Export(@out);
            @out.EndElement();
        }

        /* && visitor.isOptimizeForStreaming() */
        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet result = base.AddToPathMap(pathMap, pathMapNodeSet);
            result.SetReturnable(false);
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            Types.ItemType type = GetItemType();
            if (th.Relationship(type, NodeKindTest.ELEMENT) != Affinity.DISJOINT || th.Relationship(type, NodeKindTest.DOCUMENT) != Affinity.DISJOINT)
            {
                result.AddDescendants();
            }

            return new PathMap.PathMapNodeSet(pathMap.MakeNewRoot(this));
        }

        /* && visitor.isOptimizeForStreaming() */
        //
        //
        //
        //    }
        private void CopyOneNode(IXPathContext context, Outputter @out, NodeInfo item, int copyOptions)
        {
            Controller controller = context.GetController();
            bool copyBaseURI = @out.GetSystemId() == null;
            int kind = item.GetNodeKind();
            if (requireDocumentOrElement && !(kind == Types.Type.ELEMENT || kind == Types.Type.DOCUMENT))
            {
                throw new XPathException("Operand of validate expression must be a document or element node").WithXPathContext(context).WithErrorCode("XQTY0030");
            }

            Configuration config = controller.GetConfiguration();
            switch (kind)
            {
                case Types.Type.ELEMENT:
                    {
                        Outputter eval = @out;
                        if (validating)
                        {
                            ParseOptions options = new ParseOptions().WithSchemaValidationMode(validation);
                            ISchemaType type = schemaType;
                            if (type == null && (validation == Validation.STRICT || validation == Validation.LAX))
                            {

                                // Bug 3062
                                string xsitype = item.GetAttributeValue(NamespaceUri.SCHEMA_INSTANCE, "type");
                                if (xsitype != null)
                                {
                                    StructuredQName typeName;
                                    try
                                    {
                                        typeName = StructuredQName.FromLexicalQName(xsitype, true, false, item.AllNamespaces);
                                    }
                                    catch (XPathException e)
                                    {
                                        throw new XPathException("Invalid QName in xsi:type attribute of element being validated: " + xsitype + ". " + e.Message, "XTTE1510");
                                    }

                                    type = config.GetSchemaType(typeName);
                                    if (type == null)
                                    {
                                        throw new XPathException("Unknown xsi:type in element being validated: " + xsitype, "XTTE1510");
                                    }
                                }
                            }

                            options = options.WithTopLevelType(type).WithTopLevelElement(NameOfNode.MakeName(item).GetStructuredQName()).WithErrorReporter(context.GetErrorReporter());
                            config.PrepareValidationReporting(context, options);
                            IReceiver validator = config.GetElementValidator(@out, options, GetLocation());
                            eval = new ComplexContentOutputter(validator);
                        }

                        if (copyBaseURI)
                        {
                            eval.SetSystemId(ComputeNewBaseUri(item, StaticBaseURIString));
                        }

                        PipelineConfiguration pipe = @out.GetPipelineConfiguration();
                        if (copyLineNumbers)
                        {
                            LocationCopier copier = new LocationCopier(false, @out.GetSystemId());
                            pipe.CopyInformee = (NodeInfo node) => (object)copier.NotifyElementNode(node);
                        }

                        item.Copy(eval, copyOptions, GetLocation());

                        if (copyLineNumbers)
                        {
                            pipe.CopyInformee = null;
                        }

                        break;
                    }

                case Types.Type.ATTRIBUTE:
                    if (schemaType != null && schemaType.IsComplexType())
                    {
                        XPathException e = new XPathException("When copying an attribute with schema validation, the requested type must not be a complex type").WithLocation(GetLocation()).WithXPathContext(context).WithErrorCode("XTTE1535");
                        throw DynamicError(GetLocation(), e, context);
                    }

                    try
                    {
                        CopyAttribute(item, (ISimpleType)schemaType, validation, this, @out, context, rejectDuplicateAttributes);
                    }
                    catch (NoOpenStartTagException err)
                    {
                        XPathException e = new XPathException(err.Message).WithLocation(GetLocation()).WithXPathContext(context).WithErrorCode(err.ErrorCodeQName);
                        throw DynamicError(GetLocation(), e, context);
                    }

                    break;
                case Types.Type.TEXT:
                    @out.Characters(item.UnicodeStringValue, GetLocation(), ReceiverOption.NONE);
                    break;
                case Types.Type.PROCESSING_INSTRUCTION:
                    if (copyBaseURI)
                    {
                        @out.SetSystemId(item.GetBaseURI());
                    }

                    @out.ProcessingInstruction(item.DisplayName, item.UnicodeStringValue, GetLocation(), ReceiverOption.NONE);
                    break;
                case Types.Type.COMMENT:
                    @out.Comment(item.UnicodeStringValue, GetLocation(), ReceiverOption.NONE);
                    break;
                case Types.Type.NAMESPACE:
                    try
                    {
                        @out.Namespace(item.GetLocalPart(), NamespaceUri.Of(item.GetStringValue()), ReceiverOption.NONE);
                    }
                    catch (NoOpenStartTagException err)
                    {
                        XPathException e = new XPathException(err.Message).WithXPathContext(context).WithErrorCode(err.ErrorCodeQName);
                        throw DynamicError(GetLocation(), e, context);
                    }

                    break;
                case Types.Type.DOCUMENT:
                    {
                        ParseOptions options = new ParseOptions().WithSchemaValidationMode(validation).WithSpaceStrippingRule(NoElementsSpaceStrippingRule.GetInstance()).WithTopLevelType(schemaType).WithErrorReporter(context.GetErrorReporter());
                        config.PrepareValidationReporting(context, options);
                        IReceiver val = config.GetDocumentValidator(@out, item.GetBaseURI(), options, GetLocation());
                        if (copyBaseURI)
                        {
                            val.SetSystemId(item.GetBaseURI());
                        }

                        PipelineConfiguration savedPipe = null;
                        if (copyLineNumbers)
                        {
                            savedPipe = new PipelineConfiguration(val.GetPipelineConfiguration());
                            LocationCopier copier = new LocationCopier(true, item.GetBaseURI());
                            val.GetPipelineConfiguration().CopyInformee = (NodeInfo node) => (object)copier.NotifyElementNode(node);
                        }

                        item.Copy(val, copyOptions, GetLocation());
                        if (copyLineNumbers)
                        {
                            val.SetPipelineConfiguration(savedPipe);
                        }


                        //                        if (val != @out) {
                        //                            See bug 2403
                        //                            val.close(); // needed to flush out unresolved IDREF values when validating: test copy-5021
                        //                        }
                        break;
                    }

                default:
                    throw new ArgumentException("Unknown node kind " + item.GetNodeKind());
            }
        }

        /* && visitor.isOptimizeForStreaming() */
        //
        //
        //
        //    }
        public static string ComputeNewBaseUri(NodeInfo source, string staticBaseURI)
        {

            // These rules are the rules for xsl:copy-of instruction in XSLT. The same code is used to support the
            // validate{} expression in XQuery. XQuery says nothing about the base URI of a node that results
            // from a validate{} expression, so until it does, we might as well use the same logic.
            string newBaseUri;
            string xmlBase = source.GetAttributeValue(NamespaceUri.XML, "base");
            if (xmlBase != null)
            {
                try
                {
                    URI xmlBaseUri = new URI(xmlBase);
                    if (xmlBaseUri.IsAbsolute())
                    {
                        newBaseUri = xmlBase;
                    }
                    else if (staticBaseURI != null)
                    {
                        URI sbu = new URI(staticBaseURI);
                        URI abs = sbu.Resolve(xmlBaseUri);
                        newBaseUri = abs.ToString();
                    }
                    else
                    {
                        newBaseUri = source.GetBaseURI();
                    }
                }
                catch (URISyntaxException)
                {
                    newBaseUri = source.GetBaseURI();
                }
            }
            else
            {
                newBaseUri = source.GetBaseURI();
            }

            return newBaseUri;
        }

        /* && visitor.isOptimizeForStreaming() */
        //
        //
        //
        //    }
        public static void CopyAttribute(NodeInfo source, ISimpleType schemaType, int validation, Instruction instruction, Outputter output, IXPathContext context, bool rejectDuplicates)
        {
            int opt = rejectDuplicates ? ReceiverOption.REJECT_DUPLICATES : ReceiverOption.NONE;
            UnicodeString value = source.UnicodeStringValue;
            ISimpleType annotation = ValidateAttribute(source, schemaType, validation, context);
            try
            {
                output.Attribute(NameOfNode.MakeName(source), annotation, value.ToString(), instruction.GetLocation(), opt);
            }
            catch (XPathException e)
            {
                if (instruction.GetPackageData().GetHostLanguage() == HostLanguage.XQUERY && e.HasErrorCode("XTTE0950"))
                {
                    e.SetErrorCode("XQTY0086");
                }

                throw e.MaybeWithLocation(instruction.GetLocation()).MaybeWithContext(context);
            }
        }

        /* && visitor.isOptimizeForStreaming() */
        public static ISimpleType ValidateAttribute(NodeInfo source, ISimpleType schemaType, int validation, IXPathContext context)
        {
            UnicodeString value = source.UnicodeStringValue;
            ISimpleType annotation = BuiltInAtomicType.UNTYPED_ATOMIC;
            if (schemaType != null)
            {
                if (schemaType.IsNamespaceSensitive())
                {
                    XPathException nsErr = new XPathException("Cannot create a parentless attribute whose " + "type is namespace-sensitive (such as xs:QName)");
                    nsErr.SetErrorCode("XTTE1545");
                    throw nsErr;
                }

                ValidationFailure valErr = schemaType.ValidateContent(value, DummyNamespaceResolver.GetInstance(), context.GetConfiguration().GetConversionRules());
                if (valErr != null)
                {
                    valErr.SetMessage("Attribute being copied does not match the required type. " + valErr.GetMessage());
                    valErr.SetErrorCode("XTTE1510");
                    throw valErr.MakeException();
                }

                annotation = schemaType;
            }
            else if (validation == Validation.STRICT || validation == Validation.LAX)
            {
                try
                {
                    annotation = context.GetConfiguration().ValidateAttribute(NameOfNode.MakeName(source).GetStructuredQName(), value, validation);
                }
                catch (ValidationException e)
                {
                    XPathException err = XPathException.MakeXPathException(e);
                    err.ErrorCodeQName = e.ErrorCodeQName;
                    err.SetIsTypeError(true);
                    throw err;
                }
            }
            else if (validation == Validation.PRESERVE)
            {
                annotation = (ISimpleType)source.GetSchemaType();
                if (!annotation.Equals(BuiltInAtomicType.UNTYPED_ATOMIC) && annotation.IsNamespaceSensitive())
                {
                    XPathException err = new XPathException("Cannot preserve type annotation when copying an attribute with namespace-sensitive content");
                    err.SetErrorCode(context.GetController().GetExecutable().GetHostLanguage() == HostLanguage.XSLT ? "XTTE0950" : "XQTY0086");
                    err.SetIsTypeError(true);
                    throw err;
                }
            }

            return annotation;
        }

        /* && visitor.isOptimizeForStreaming() */
        private bool MustPush()
        {
            return schemaType != null || validation == Validation.LAX || validation == Validation.STRICT || copyForUpdate;
        }

        /* && visitor.isOptimizeForStreaming() */
        /*!copyNamespaces ||*/
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            IPullEvaluator pull = MakeElaborator().ElaborateForPull();
            return pull.Iterate(context); //        final Controller controller = context.getController();
            //        assert controller != null;
            //                return result;
            //        SequenceCollector @out = new SequenceCollector(pipe);
            //        try {
        }

        /* && visitor.isOptimizeForStreaming() */
        /*!copyNamespaces ||*/
        private ItemMappingIterator MakeVirtualCopy(ISequenceIterator input, Controller controller, bool isXSLT)
        {
            if (validation == Validation.PRESERVE)
            {

                // create a virtual copy of the underlying nodes
                IItemMappingFunction copier = ItemMapper.Of((item) =>
                {
                    if (item is NodeInfo)
                    {
                        if (((NodeInfo)item).GetTreeInfo().IsTyped())
                        {
                            if (!copyNamespaces && ((NodeInfo)item).GetNodeKind() == Types.Type.ELEMENT)
                            {

                                // A lot of extra work here just to check for error XTTE0950, but the conditions are rare
                                Sink sink = new Sink(controller.MakePipelineConfiguration());
                                ((NodeInfo)item).Copy(sink, CopyOptions.TYPE_ANNOTATIONS, GetLocation());
                            }

                            if (((NodeInfo)item).GetNodeKind() == Types.Type.ATTRIBUTE && ((ISimpleType)((NodeInfo)item).GetSchemaType()).IsNamespaceSensitive())
                            {
                                throw new XPathException("Cannot copy an attribute with namespace-sensitive content except as part of its containing element", "XTTE0950");
                            }
                        }

                        VirtualCopy vc = VirtualCopy.MakeVirtualCopy((NodeInfo)item);
                        vc.SetDropNamespaces(!copyNamespaces);
                        vc.GetTreeInfo().SetCopyAccumulators(copyAccumulators);
                        if (isXSLT && copyAccumulators)
                        {
                            vc.GetTreeInfo().SetCopyAccumulators(true);
                            AccumulatorManager am = ((XsltController)controller).GetAccumulatorManager();
                            am.SetApplicableAccumulators(vc.GetTreeInfo(), am.GetApplicableAccumulators(((NodeInfo)item).GetTreeInfo()));
                        }

                        if (((NodeInfo)item).GetNodeKind() == Types.Type.ELEMENT)
                        {
                            vc.SetSystemId(ComputeNewBaseUri((NodeInfo)item, StaticBaseURIString));
                        }

                        return (IItem)vc;
                    }
                    else
                    {
                        return item;
                    }
                });
                return new ItemMappingIterator(input, copier, true);
            }
            else if (validation == Validation.STRIP)
            {

                // create a virtual copy of the underlying nodes
                IItemMappingFunction copier = ItemMapper.Of((item) =>
                {
                    if (!(item is NodeInfo))
                    {
                        return item;
                    }

                    VirtualCopy vc = VirtualUntypedCopy.MakeVirtualUntypedTree((NodeInfo)item, (NodeInfo)item);
                    if (copyAccumulators)
                    {
                        vc.GetTreeInfo().SetCopyAccumulators(true);
                        AccumulatorManager am = ((XsltController)controller).GetAccumulatorManager();
                        am.SetApplicableAccumulators(vc.GetTreeInfo(), am.GetApplicableAccumulators(((NodeInfo)item).GetTreeInfo()));
                    }

                    vc.SetDropNamespaces(!copyNamespaces);
                    if (((NodeInfo)item).GetNodeKind() == Types.Type.ELEMENT)
                    {
                        vc.SetSystemId(ComputeNewBaseUri((NodeInfo)item, StaticBaseURIString));
                    }

                    return (IItem)vc;
                });
                return new ItemMappingIterator(input, copier, true);
            }
            else
            {
                return null;
            }
        }

        /* && visitor.isOptimizeForStreaming() */
        /*!copyNamespaces ||*/
        public override Elaborator GetElaborator()
        {
            return new CopyOfElaborator();
        }

        /* && visitor.isOptimizeForStreaming() */
        /*!copyNamespaces ||*/
        private sealed class CopyOfElaborator : PushElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                CopyOf expr = (CopyOf)GetExpression();
                bool isXSLT = expr.GetRetainedStaticContext().GetPackageData().IsXSLT();
                if (expr.schemaType == null && !expr.copyForUpdate && (expr.validation == Validation.PRESERVE || expr.validation == Validation.STRIP))
                {
                    IPullEvaluator select = expr.Select.MakeElaborator().ElaborateForPull();
                    return (context) =>
                    {
                        Controller controller = context.GetController();
                        return expr.MakeVirtualCopy(select.Iterate(context), controller, isXSLT);
                    };
                }
                else
                {
                    HostLanguage host = expr.GetPackageData().GetHostLanguage();
                    IPushEvaluator push = ElaborateForPush();
                    return (context) =>
                    {
                        Controller controller = context.GetController();
                        PipelineConfiguration pipe = controller.MakePipelineConfiguration();
                        pipe.XPathContext = context;
                        SequenceCollector @out = new SequenceCollector(pipe, (int)(expr.numberOfItems / expr.invocations));
                        if (expr.copyForUpdate)
                        {
                            @out.SetTreeModel(TreeModel.LINKED_TREE);
                        }

                        pipe.SetHostLanguage(host);
                        try
                        {
                            ITailCall tc = push.ProcessLeavingTail(new ComplexContentOutputter(@out), context);
                            Expression.DispatchTailCall(tc);
                        }
                        catch (XPathException err)
                        {
                            err.MaybeSetLocation(expr.GetLocation());
                            err.MaybeSetContext(context);
                            throw;
                        }

                        IGroundedValue result = @out.Sequence;
                        expr.invocations++;
                        expr.numberOfItems += result.GetLength();
                        return result.Iterate();
                    };
                }
            }

            public override IItemEvaluator ElaborateForItem()
            {
                if (((CopyOf)GetExpression()).copyForUpdate)
                {
                    IPushEvaluator pushEval = ElaborateForPush();
                    return (context) =>
                    {
                        Controller controller = context.GetController();
                        SequenceCollector seq = controller.AllocateSequenceOutputter(1);
                        seq.SetTreeModel(TreeModel.LINKED_TREE);
                        ITailCall tc = pushEval.ProcessLeavingTail(new ComplexContentOutputter(seq), context);
                        Expression.DispatchTailCall(tc);
                        seq.Close();
                        return seq.FirstItem;
                    };
                }
                else
                {
                    return base.ElaborateForItem();
                }
            }

            public override IPushEvaluator ElaborateForPush()
            {
                CopyOf expr = (CopyOf)GetExpression();
                if (expr.copyAccumulators)
                {
                    if (expr.MustPush())
                    {

                        // This typically happens with the combination copy-accumulators=yes, validation=strict
                        // Test case accumulators-070
                        // We have to create a physical copy because of the validation requirement, but this makes
                        // it difficult to copy the accumulator values.
                        IPullEvaluator selectPull = expr.Select.MakeElaborator().ElaborateForPull();
                        return (output, context) =>
                        {
                            SequenceTool.Supply(selectPull.Iterate(context), (item) =>
                            {
                                if (item is NodeInfo)
                                {
                                    TinyBuilder builder = new TinyBuilder(output.GetPipelineConfiguration());
                                    ComplexContentOutputter cco = new ComplexContentOutputter(builder);
                                    cco.Open();
                                    expr.CopyOneNode(context, cco, (NodeInfo)item, CopyOptions.ALL_NAMESPACES);
                                    cco.Close();
                                    TinyNodeImpl copy = (TinyNodeImpl)builder.CurrentRoot;
                                    copy.Tree.CopiedFrom = (NodeInfo)item;
                                    output.Append(copy);
                                }
                                else
                                {
                                    output.Append(item);
                                }
                            });
                            return null;
                        };
                    }
                    else
                    {

                        // Use the iterate() method to create a virtual copy.
                        IPullEvaluator pull = ElaborateForPull();
                        return (output, context) =>
                        {
                            SequenceTool.Supply(pull.Iterate(context), (IItemConsumer<IItem>)output.Append);
                            return null;
                        };
                    }
                }
                else
                {
                    int copyOptions = (expr.validation == Validation.SKIP ? 0 : CopyOptions.TYPE_ANNOTATIONS) | (expr.copyNamespaces ? CopyOptions.ALL_NAMESPACES : 0) | (expr.copyForUpdate ? CopyOptions.FOR_UPDATE : 0);
                    IPullEvaluator selectPull = expr.Select.MakeElaborator().ElaborateForPull();
                    return (output, context) =>
                    {
                        SequenceTool.Supply(selectPull.Iterate(context), (item) =>
                        {
                            if (item is NodeInfo)
                            {
                                expr.CopyOneNode(context, output, (NodeInfo)item, copyOptions);
                            }
                            else
                            {
                                output.Append(item, expr.GetLocation(), ReceiverOption.ALL_NAMESPACES);
                            }
                        });
                        return null;
                    };
                }
            }
        }
    }
}