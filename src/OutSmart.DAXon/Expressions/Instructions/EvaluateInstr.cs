////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class EvaluateInstr : Expression
    {
        private Operand xpathOp;
        private readonly SequenceType requiredType;
        private Operand contextItemOp;
        private Operand baseUriOp;
        private Operand namespaceContextOp;
        private Operand schemaAwareOp;
        private Operand optionsOp;
        private HashSet<NamespaceUri> importedSchemaNamespaces = new HashSet<NamespaceUri>();
        private WithParam[] actualParams;
        private Operand dynamicParamsOp;
        private NamespaceUri defaultXPathNamespace = null;

        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_FOCUS | StaticProperty.DEPENDS_ON_XSLT_CONTEXT;

        public override int ImplementationMethod => ITERATE_METHOD;

        public Expression Xpath
        {
            get => xpathOp.GetChildExpression(); set
            {
                if (xpathOp == null)
                {
                    xpathOp = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
                }
                else
                {
                    xpathOp.SetChildExpression(value);
                }
            }
        }

        public Expression ContextItemExpr
        {
            get => contextItemOp == null ? null : contextItemOp.GetChildExpression(); set
            {
                if (contextItemOp == null)
                {
                    contextItemOp = new Operand(this, value, OperandRole.NAVIGATE);
                }
                else
                {
                    contextItemOp.SetChildExpression(value);
                }
            }
        }

        public Expression BaseUriExpr
        {
            get => baseUriOp == null ? null : baseUriOp.GetChildExpression(); set
            {
                if (baseUriOp == null)
                {
                    baseUriOp = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
                }
                else
                {
                    baseUriOp.SetChildExpression(value);
                }
            }
        }

        public Expression NamespaceContextExpr
        {
            get => namespaceContextOp == null ? null : namespaceContextOp.GetChildExpression(); set
            {
                if (namespaceContextOp == null)
                {
                    namespaceContextOp = new Operand(this, value, OperandRole.INSPECT);
                }
                else
                {
                    namespaceContextOp.SetChildExpression(value);
                }
            }
        }

        public Expression SchemaAwareExpr
        {
            get => schemaAwareOp == null ? null : schemaAwareOp.GetChildExpression(); set
            {
                if (schemaAwareOp == null)
                {
                    schemaAwareOp = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
                }
                else
                {
                    schemaAwareOp.SetChildExpression(value);
                }
            }
        }

        public WithParam[] ActualParams { get => actualParams; set => this.actualParams = value; }

        public Expression DynamicParams
        {
            get => dynamicParamsOp.GetChildExpression(); set
            {
                if (dynamicParamsOp == null)
                {
                    dynamicParamsOp = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
                }
                else
                {
                    dynamicParamsOp.SetChildExpression(value);
                }
            }
        }
        public EvaluateInstr(Expression xpath, SequenceType requiredType, Expression contextItemExpr, Expression baseUriExpr, Expression namespaceContextExpr, Expression schemaAwareExpr)
        {
            if (xpath != null)
            {
                xpathOp = new Operand(this, xpath, OperandRole.SINGLE_ATOMIC);
            }

            if (contextItemExpr != null)
            {
                contextItemOp = new Operand(this, contextItemExpr, OperandRole.NAVIGATE);
            }

            if (baseUriExpr != null)
            {
                baseUriOp = new Operand(this, baseUriExpr, OperandRole.SINGLE_ATOMIC);
            }

            if (namespaceContextExpr != null)
            {
                namespaceContextOp = new Operand(this, namespaceContextExpr, OperandRole.INSPECT);
            }

            if (schemaAwareExpr != null)
            {
                schemaAwareOp = new Operand(this, schemaAwareExpr, OperandRole.SINGLE_ATOMIC);
            }

            this.requiredType = requiredType;
        }

        public void SetOptionsExpression(Expression options)
        {
            optionsOp = new Operand(this, options, OperandRole.ABSORB);
        }

        public void SetDefaultXPathNamespace(NamespaceUri defaultXPathNamespace)
        {
            this.defaultXPathNamespace = defaultXPathNamespace;
        }

        public override bool IsInstruction()
        {
            return true;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            importedSchemaNamespaces = visitor.StaticContext.GetImportedSchemaNamespaces();
            TypeCheckChildren(visitor, contextInfo);
            WithParam.TypeCheck(ActualParams, visitor, contextInfo);
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            OptimizeChildren(visitor, contextItemType);
            return this;
        }

        public override ItemType GetItemType()
        {
            return requiredType.PrimaryType;
        }

        protected override int ComputeCardinality()
        {
            return requiredType.GetCardinality();
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            throw new NotSupportedException("Cannot do document projection when xsl:evaluate is used");
        }

        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> sub = new List<Operand>(8);
            if (xpathOp != null)
            {
                sub.Add(xpathOp);
            }

            if (contextItemOp != null)
            {
                sub.Add(contextItemOp);
            }

            if (baseUriOp != null)
            {
                sub.Add(baseUriOp);
            }

            if (namespaceContextOp != null)
            {
                sub.Add(namespaceContextOp);
            }

            if (schemaAwareOp != null)
            {
                sub.Add(schemaAwareOp);
            }

            if (dynamicParamsOp != null)
            {
                sub.Add(dynamicParamsOp);
            }

            if (optionsOp != null)
            {
                sub.Add(optionsOp);
            }

            WithParam.GatherOperands(this, ActualParams, sub);
            return sub;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            EvaluateInstr e2 = new EvaluateInstr(Xpath.Copy(rebindings), requiredType, ContextItemExpr.Copy(rebindings), BaseUriExpr == null ? null : BaseUriExpr.Copy(rebindings), NamespaceContextExpr == null ? null : NamespaceContextExpr.Copy(rebindings), SchemaAwareExpr == null ? null : SchemaAwareExpr.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, e2);
            e2.SetRetainedStaticContext(GetRetainedStaticContext());
            e2.importedSchemaNamespaces = importedSchemaNamespaces;
            e2.ActualParams = WithParam.Copy(e2, ActualParams, rebindings);
            if (optionsOp != null)
            {
                e2.SetOptionsExpression(optionsOp.GetChildExpression().Copy(rebindings));
            }

            if (dynamicParamsOp != null)
            {
                e2.DynamicParams = dynamicParamsOp.GetChildExpression().Copy(rebindings);
            }

            return e2;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("evaluate", this);
            if (!SequenceType.ANY_SEQUENCE.Equals(requiredType))
            {
                @out.EmitAttribute("as", requiredType.ToAlphaCode());
            }

            if (importedSchemaNamespaces != null && importedSchemaNamespaces.Count > 0)
            {
                StringBuilder buff = new StringBuilder(256);
                foreach (NamespaceUri s in importedSchemaNamespaces)
                {
                    buff.Append(s.IsEmpty() ? "##" : s);
                    buff.Append(' ');
                }

                buff.Length = buff.Length - 1;
                @out.EmitAttribute("schNS", buff.ToString());
            }

            if (defaultXPathNamespace != null)
            {
                @out.EmitAttribute("dxns", defaultXPathNamespace.ToString());
            }

            @out.SetChildRole("xpath");
            Xpath.Export(@out);
            if (ContextItemExpr != null)
            {
                @out.SetChildRole("cxt");
                ContextItemExpr.Export(@out);
            }

            if (BaseUriExpr != null)
            {
                @out.SetChildRole("baseUri");
                BaseUriExpr.Export(@out);
            }

            if (NamespaceContextExpr != null)
            {
                @out.SetChildRole("nsCxt");
                NamespaceContextExpr.Export(@out);
            }

            if (SchemaAwareExpr != null)
            {
                @out.SetChildRole("sa");
                SchemaAwareExpr.Export(@out);
            }

            if (optionsOp != null)
            {
                @out.SetChildRole("options");
                optionsOp.GetChildExpression().Export(@out);
            }

            WithParam.ExportParameters(actualParams, @out, false);
            if (dynamicParamsOp != null)
            {
                @out.SetChildRole("wp");
                DynamicParams.Export(@out);
            }

            @out.EndElement();
        }

        public bool IsActualParam(StructuredQName name)
        {
            foreach (WithParam wp in actualParams)
            {
                if (wp.VariableQName.Equals(name))
                {
                    return true;
                }
            }

            return false;
        }

        public override Elaborator GetElaborator()
        {
            return new EvaluateInstrElaborator();
        }

        private sealed class EvaluateInstrElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                EvaluateInstr instr = (EvaluateInstr)GetExpression();
                IStringEvaluator exprTextEval = instr.Xpath.MakeElaborator().ElaborateForString(false);
                IStringEvaluator baseUriEval = instr.BaseUriExpr == null ? null : instr.BaseUriExpr.MakeElaborator().ElaborateForString(false);
                IItemEvaluator contextItemEval = instr.ContextItemExpr.MakeElaborator().ElaborateForItem();
                IItemEvaluator namespaceContextEval = instr.NamespaceContextExpr == null ? null : instr.NamespaceContextExpr.MakeElaborator().ElaborateForItem();
                IStringEvaluator schemaAwareEval = instr.SchemaAwareExpr.MakeElaborator().ElaborateForString(false);
                IItemEvaluator dynamicParamsEval = instr.DynamicParams == null ? null : instr.DynamicParams.MakeElaborator().ElaborateForItem();
                IItemEvaluator optionsEval = instr.optionsOp == null ? null : instr.optionsOp.GetChildExpression().MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    Configuration config = context.GetConfiguration();
                    if (config.GetBooleanProperty(Feature<bool>.DISABLE_XSL_EVALUATE))
                    {
                        throw new XPathException("xsl:evaluate has been disabled", "XTDE3175");
                    }

                    string exprText = exprTextEval.Eval(context);
                    string baseUri = baseUriEval == null ? instr.StaticBaseURIString : Whitespace.Trim(baseUriEval.Eval(context));
                    IItem focus = contextItemEval.Eval(context);
                    NodeInfo namespaceContextBase = null;
                    if (namespaceContextEval != null)
                    {
                        namespaceContextBase = (NodeInfo)namespaceContextEval.Eval(context);
                    }

                    string schemaAwareAttr = Whitespace.Trim(schemaAwareEval.Eval(context));
                    bool isSchemaAware;
                    if (schemaAwareAttr == "yes" || schemaAwareAttr == "true" || schemaAwareAttr == "1")
                    {
                        isSchemaAware = true;
                    }
                    else if (schemaAwareAttr == "no" || schemaAwareAttr == "false" || schemaAwareAttr == "0")
                    {
                        isSchemaAware = false;
                    }
                    else
                    {
                        throw new XPathException("The schema-aware attribute of xsl:evaluate must be yes|no|true|false|0|1").WithErrorCode("XTDE0030").WithLocation(instr.GetLocation()).WithXPathContext(context);
                    }

                    Expression expr = null;
                    SlotManager slotMap = null;

                    // Create a cache key so the compiled expression can be reused
                    StringBuilder fsb = new StringBuilder(exprText.Length + (baseUri == null ? 4 : baseUri.Length) + 40);
                    fsb.Append(baseUri);
                    fsb.Append("##");
                    fsb.Append(schemaAwareAttr);
                    fsb.Append("##");
                    fsb.Append(exprText);
                    if (namespaceContextBase != null)
                    {
                        fsb.Append("##");
                        namespaceContextBase.GenerateId(fsb);
                    }

                    string cacheKey = fsb.ToString();
                    ICollection<XPathVariable> declaredVars = null;
                    Controller controller = context.GetController();
                    LFUCache<String, Object[]> cache;

                    lock (controller.syncLock)
                    {
                        cache = (LFUCache<String, Object[]>)controller.GetUserData(instr.GetLocation(), "xsl:evaluate");
                        if (cache == null)
                        {
                            cache = new LFUCache<string, object[]>(100);
                            controller.SetUserData(instr.GetLocation(), "xsl:evaluate", cache);
                        }
                        else
                        {
                            object[] o = cache[cacheKey];
                            if (o != null)
                            {
                                expr = (Expression)o[0];
                                slotMap = (SlotManager)o[1];
                                declaredVars = (ICollection<XPathVariable>)o[2];
                            }
                        }
                    }

                    MapItem dynamicParams = null;
                    if (dynamicParamsEval != null)
                    {
                        dynamicParams = (MapItem)dynamicParamsEval.Eval(context);
                    }

                    if (expr == null)
                    {

                        // Expression needs to be compiled. First create the static context...
                        int version = instr.GetRetainedStaticContext().GetPackageData().HostLanguageVersion;

                        //                    if (version == 30) {
                        //                        version = 31;
                        //                    }
                        MapItem options = (optionsEval == null ? new HashTrieMap() : (MapItem)optionsEval.Eval(context));
                        IndependentContext env = new IndependentContext(config);
                        env.SetWarningHandler((str, loc) =>
                        {
                            string message = "In dynamic expression {" + exprText + "}: " + str;
                            context.GetController().Warning(message, null, loc);
                        });
                        env.SetBaseURI(baseUri);
                        env.SetExecutable(context.GetController().GetExecutable());
                        env.SetXPathLanguageLevel(version == 40 ? 40 : config.GetConfigurationProperty(Feature<int>.XPATH_VERSION_FOR_XSLT));
                        env.SetDefaultCollationName(instr.GetRetainedStaticContext().DefaultCollationName);
                        if (namespaceContextEval != null)
                        {
                            env.SetNamespaces(namespaceContextBase);
                        }
                        else
                        {
                            env.SetNamespaceResolver(instr.GetRetainedStaticContext());
                            env.SetDefaultElementNamespace(instr.GetRetainedStaticContext().DefaultElementNamespace);
                        }


                        // Copy the function library list, except for XSLT-defined system functions and private user-written functions
                        FunctionLibraryList libraryList0 = ((StylesheetPackage)instr.GetRetainedStaticContext().GetPackageData()).GetFunctionLibrary();
                        FunctionLibraryList libraryList1 = new FunctionLibraryList();
                        foreach (IFunctionLibrary lib in libraryList0.LibraryList)
                        {
                            if (lib is BuiltInFunctionSet && ((BuiltInFunctionSet)lib).GetNamespace().Equals(NamespaceUri.FN))
                            {

                                // Exclude XSLT-defined functions
                                libraryList1.AddFunctionLibrary(config.GetXPathFunctionSet(version == 40 ? 40 : 31)); // see bug 6221
                            }
                            else if (lib is StylesheetFunctionLibrary || lib is ExecutableFunctionLibrary)
                            {
                                libraryList1.AddFunctionLibrary(new PublicStylesheetFunctionLibrary(lib));
                            }
                            else
                            {
                                libraryList1.AddFunctionLibrary(lib);
                            }
                        }

                        env.SetFunctionLibrary(libraryList1);
                        env.SetDecimalFormatManager(instr.GetRetainedStaticContext().GetDecimalFormatManager());

                        if (isSchemaAware)
                        {
                            IGroundedValue allowAny = options[StringValue.Bmp("allow-any-namespace")];
                            if (allowAny != null && allowAny.EffectiveBooleanValue())
                            {
                                env.SetImportedSchemaNamespaces(config.ImportedNamespaces);
                            }
                            else
                            {
                                env.SetImportedSchemaNamespaces(instr.importedSchemaNamespaces);
                            }
                        }

                        IGroundedValue defaultCollation = options[StringValue.Bmp("default-collation")];
                        if (defaultCollation != null)
                        {
                            env.SetDefaultCollationName(defaultCollation.Head().GetStringValue());
                        }

                        Dictionary<StructuredQName, int> locals = new Dictionary<StructuredQName, int>();
                        if (dynamicParams != null)
                        {
                            SequenceTool.Supply(dynamicParams.Keys(), (paramName) =>
                            {
                                if (!(paramName is QNameValue))
                                {
                                    IAtomicType primitiveItemType = ((AtomicValue)paramName).PrimitiveType;
                                    XPathException err = new XPathException("Parameter names supplied to xsl:evaluate must have type xs:QName, not " + primitiveItemType.DisplayName, "XTTE3165");
                                    err.SetIsTypeError(true);
                                    throw err;
                                }

                                XPathVariable var = env.DeclareVariable((QNameValue)paramName);
                                locals[((QNameValue)paramName).GetStructuredQName()] = var.LocalSlotNumber;
                            });
                        }

                        if (instr.ActualParams != null)
                        {
                            foreach (WithParam actualParam in instr.ActualParams)
                            {
                                StructuredQName name = actualParam.VariableQName;
                                if (!locals.ContainsKey(name))
                                {
                                    XPathVariable var = env.DeclareVariable(name);
                                    locals[name] = var.LocalSlotNumber;
                                }
                            }
                        }


                        // Now compile the expression
                        try
                        {
                            expr = ExpressionTool.Make(exprText, env, 0, Token.EOF, null);
                        }
                        catch (XPathException e)
                        {
                            throw new XPathException("Static error in XPath expression supplied to xsl:evaluate: " + e.Message + ". Expression: {" + exprText + "}").WithErrorCode("XTDE3160").WithLocation(instr.GetLocation());
                        }


                        // Type check, and allocate slots for variables
                        expr.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                        Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.EVALUATE_RESULT, exprText, 0);
                        ExpressionVisitor visitor = ExpressionVisitor.Make(env);
                        TypeChecker tc = config.GetTypeChecker(false);
                        expr = tc.StaticTypeCheck(expr, instr.requiredType, role, visitor);
                        expr = ExpressionTool.ResolveCallsToCurrentFunction(expr);
                        ContextItemStaticInfo cit;
                        if (instr.ContextItemExpr != null)
                        {
                            cit = config.MakeContextItemStaticInfo(instr.ContextItemExpr.GetItemType(), Cardinality.AllowsZero(instr.ContextItemExpr.GetCardinality()));
                        }
                        else
                        {
                            cit = ContextItemStaticInfo.ABSENT;
                        }

                        expr = expr.TypeCheck(visitor, cit).Optimize(visitor, cit);
                        slotMap = env.GetStackFrameMap();
                        ExpressionTool.AllocateSlots(expr, slotMap.NumberOfVariables, slotMap);

                        // Save the compiled expression in the cache for next time
                        if (cacheKey != null)
                        {
                            declaredVars = env.DeclaredVariables;
                            cache.Put(cacheKey, new object[] { expr, slotMap, declaredVars }); //System.Console.Error.println("Cache miss, size = " + cache.size());
                        }
                    }

                    XPathContextMajor c2 = context.NewContext();
                    if (focus == null)
                    {
                        c2.SetCurrentIterator(null);
                    }
                    else
                    {
                        ManualIterator mono = new ManualIterator(focus);
                        c2.SetCurrentIterator(mono);
                    }

                    c2.OpenStackFrame(slotMap);
                    if (instr.ActualParams != null)
                    {
                        for (int i = 0; i < instr.ActualParams.Length; i++)
                        {
                            StructuredQName variableQName = instr.ActualParams[i].VariableQName;
                            if (dynamicParams != null && dynamicParams[new QNameValue(variableQName, BuiltInAtomicType.QNAME)] != null)
                            {

                                // Don't evaluate xsl:with-param if there is a dynamic parameter of the same name
                                continue;
                            }

                            int slot = slotMap.VariableMap.IndexOf(variableQName);
                            c2.SetLocalVariable(slot, instr.ActualParams[i].GetSelectValue(context));
                        }
                    }

                    if (dynamicParams != null)
                    {
                        IAtomicIterator iter = dynamicParams.Keys();
                        QNameValue paramName;
                        while ((paramName = (QNameValue)iter.Next()) != null)
                        {
                            int slot = slotMap.VariableMap.IndexOf(paramName.GetStructuredQName());
                            if (slot >= 0)
                            {

                                // can be false if the with-params changes from one call to the next
                                c2.SetLocalVariable(slot, dynamicParams[paramName]);
                            }
                        }
                    }


                    // Check that all required variables are present
                    foreach (XPathVariable var in declaredVars)
                    {
                        StructuredQName name = var.GetVariableQName();
                        Func<Expression, bool> nameMatch = (e) => e is LocalVariableReference && ((LocalVariableReference)e).VariableName.Equals(name) && ((LocalVariableReference)e).GetBinding() is XPathVariable;
                        if (dynamicParams != null && dynamicParams[new QNameValue(name, BuiltInAtomicType.QNAME)] == null && !instr.IsActualParam(name) && ExpressionTool.Contains(expr, false, nameMatch))
                        {
                            throw new XPathException("No value has been supplied for variable " + name.DisplayName, "XPST0008");
                        }
                    }

                    try
                    {
                        return expr.Iterate(c2);
                    }
                    catch (XPathException err)
                    {
                        throw err.WithMessage("Dynamic error in expression {" + exprText + "} called using xsl:evaluate").WithLocation(instr.GetLocation());
                    }
                };
            }
        }
    }
}