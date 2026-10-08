////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Xslt
{
    public class XSLFunction : StyleElement, IStylesheetComponent
    {
        private bool doneAttributes = false;
        private string nameAtt = null;
        private string asAtt = null;
        private string extraAsAtt = null;
        private SequenceType resultType = SequenceType.ANY_SEQUENCE;
        private SlotManager stackFrameMap;
        private bool memoFunction = false;
        private string overrideExtensionFunctionAtt = null;
        private bool overrideExtensionFunction = true;
        private int numberOfParameters = -1; // -1 means not yet known
        private int numberOfOptionalParameters = -1; // -1 means not yet known
        private UserFunction compiledFunction;
        private Visibility visibility = Visibility.UNDEFINED;
        private FunctionStreamability streamability;
        private UserFunction.Determinism determinism = UserFunction.Determinism.PROACTIVE;
        private bool explaining;
        private bool updating = false;

        public virtual SequenceType ResultType
        {
            get
            {
                if (resultType == null)
                {

                    // may be handling a forwards reference - see hof-038
                    string asAtt = GetAttributeValue(NamespaceUri.NULL, "as");
                    if (asAtt != null)
                    {
                        try
                        {
                            resultType = MakeSequenceType(asAtt);
                        }
                        catch (XPathException)
                        {
                        }
                    }
                }

                return resultType == null ? SequenceType.ANY_SEQUENCE : resultType;
            }
        }

        public virtual int NumberOfParameters
        {
            get
            {
                if (numberOfParameters == -1)
                {
                    numberOfParameters = 0;
                    foreach (NodeInfo child in Children())
                    {
                        if (child is XSLLocalParam)
                        {
                            numberOfParameters++;
                        }
                        else
                        {
                            return numberOfParameters;
                        }
                    }
                }

                return numberOfParameters;
            }
        }

        public virtual int NumberOfOptionalParameters
        {
            get
            {
                if (numberOfOptionalParameters == -1)
                {
                    numberOfOptionalParameters = 0;
                    foreach (NodeInfo child in Children())
                    {
                        if (child is XSLLocalParam)
                        {
                            string requiredAtt = ((XSLLocalParam)child).GetAttributeValue("required");
                            if (requiredAtt != null && IsNo(Whitespace.Trim(requiredAtt)))
                            {
                                numberOfOptionalParameters++;
                            }
                        }
                        else
                        {
                            return numberOfOptionalParameters;
                        }
                    }
                }

                return numberOfOptionalParameters;
            }
        }

        public virtual SequenceType[] ArgumentTypes
        {
            get
            {
                SequenceType[] types = new SequenceType[NumberOfParameters];
                int count = 0;
                foreach (NodeInfo node in Children(new TypeIsInstancePredicate(typeof(XSLLocalParam))))
                {
                    types[count++] = ((XSLLocalParam)node).GetRequiredType();
                }

                return types;
            }
        }
        public UserFunction GetActor()
        {
            return compiledFunction;
        }

        public override bool IsDeclaration()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            if (doneAttributes)
            {
                return;
            }

            doneAttributes = true;
            IAttributeMap atts = Attributes();
            overrideExtensionFunctionAtt = null;
            string visibilityAtt = null;
            string cacheAtt = null;
            string newEachTimeAtt = null;
            string streamabilityAtt = null;
            foreach (AttributeInfo att in atts)
            {
                INodeName name = att.GetNodeName();
                NamespaceUri uri = name.GetNamespaceUri();
                string local = name.GetLocalPart();
                if (uri.IsEmpty())
                {
                    switch (local)
                    {
                        case "name":
                            nameAtt = Whitespace.Trim(att.Value);
                            StructuredQName functionName = MakeQName(nameAtt, null, "name");
                            if (functionName.HasURI(NamespaceUri.NULL))
                            {
                                functionName = new StructuredQName("saxon", NamespaceUri.SAXON, functionName.GetLocalPart());
                                CompileError("Function name must be in a namespace", "XTSE0740");
                            }

                            SetObjectName(functionName);
                            break;
                        case "as":
                            asAtt = att.Value;
                            break;
                        case "visibility":
                            visibilityAtt = Whitespace.Trim(att.Value);
                            break;
                        case "streamability":
                            streamabilityAtt = Whitespace.Trim(att.Value);
                            break;
                        case "override":
                            string overrideAtt = Whitespace.Trim(att.Value);
                            bool @override = ProcessBooleanAttribute("override", overrideAtt);
                            if (overrideExtensionFunctionAtt != null)
                            {
                                if (@override != overrideExtensionFunction)
                                {
                                    CompileError("Attributes override-extension-function and override are both used, but do not match", "XTSE0020");
                                }
                            }
                            else
                            {
                                overrideExtensionFunctionAtt = overrideAtt;
                                overrideExtensionFunction = @override;
                            }

                            IssueWarning("The xsl:function/@override attribute is deprecated; use override-extension-function", DAXonErrorCode.SXWN9014);
                            break;
                        case "override-extension-function":
                            string overrideExtAtt = Whitespace.Trim(att.Value);
                            bool overrideExt = ProcessBooleanAttribute("override-extension-function", overrideExtAtt);
                            if (overrideExtensionFunctionAtt != null)
                            {
                                if (overrideExt != overrideExtensionFunction)
                                {
                                    CompileError("Attributes override-extension-function and override are both used, but do not match", "XTSE0020");
                                }
                            }
                            else
                            {
                                overrideExtensionFunctionAtt = overrideExtAtt;
                                overrideExtensionFunction = overrideExt;
                            }

                            break;
                        case "cache":
                            cacheAtt = Whitespace.Trim(att.Value);
                            break;
                        case "new-each-time":
                            newEachTimeAtt = Whitespace.Trim(att.Value);
                            break;
                        default:
                            CheckUnknownAttribute(name);
                            break;
                    }
                }
                else if (uri.Equals(NamespaceUri.SAXON))
                {
                    if (IsExtensionAttributeAllowed(att.GetNodeName().DisplayName))
                    {
                        if (local.Equals("memo-function"))
                        {
                            IssueWarning("saxon:memo-function is deprecated: use cache='yes'", DAXonErrorCode.SXWN9014);
                            if (GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
                            {
                                memoFunction = ProcessBooleanAttribute("saxon:memo-function", att.Value);
                            }
                        }
                        else if (local.Equals("as"))
                        {
                            IsExtensionAttributeAllowed(name.DisplayName);
                            extraAsAtt = att.Value;
                        }
                        else if (local.Equals("explain") && IsYes(Whitespace.Trim(att.Value)))
                        {
                            explaining = true;
                        }
                    }
                }
                else if (uri.Equals(NamespaceUri.IXSL))
                {
                    if (IsExtensionAttributeAllowed(att.GetNodeName().DisplayName))
                    {
                        if (local.Equals("updating"))
                        {
                            if (GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XSLT))
                            {
                                updating = ProcessBooleanAttribute("ixsl:updating", att.Value);
                            }
                        }
                    }
                }
                else
                {
                    CheckUnknownAttribute(name);
                }
            }

            if (nameAtt == null)
            {
                ReportAbsence("name");
                nameAtt = "xsl:unnamed-function-" + GenerateId();
            }

            if (asAtt != null)
            {
                try
                {
                    resultType = MakeSequenceType(asAtt);
                }
                catch (XPathException e)
                {
                    CompileErrorInAttribute(e, "as");
                }
            }

            if (extraAsAtt != null)
            {
                SequenceType extraResultType = null;
                try
                {
                    extraResultType = MakeExtendedSequenceType(extraAsAtt);
                }
                catch (XPathException e)
                {
                    CompileErrorInAttribute(e, "saxon:as");
                    extraResultType = resultType;
                }

                if (asAtt != null)
                {
                    Affinity rel = GetConfiguration().GetTypeHierarchy().SequenceTypeRelationship(extraResultType, resultType);
                    if (rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMED_BY)
                    {
                        resultType = extraResultType;
                    }
                    else
                    {
                        CompileErrorInAttribute("When both are present, @saxon:as must be a subtype of @as", "SXER7TBA", "saxon:as");
                    }
                }
                else
                {
                    resultType = extraResultType;
                }
            }

            if (visibilityAtt == null)
            {
                visibility = Visibility.PRIVATE;
            }
            else
            {
                visibility = InterpretVisibilityValue(visibilityAtt, "");
            }

            if (streamabilityAtt == null)
            {
                streamability = FunctionStreamability.UNCLASSIFIED;
            }
            else
            {
                streamability = GetStreamabilityValue(streamabilityAtt);
                if (streamability.IsStreaming())
                {
                    bool streamable = ProcessStreamableAtt("yes");
                    if (!streamable)
                    {
                        streamability = FunctionStreamability.UNCLASSIFIED;
                    }
                }
            }

            if (newEachTimeAtt != null)
            {
                if (newEachTimeAtt == "maybe")
                {
                    determinism = UserFunction.Determinism.ELIDABLE;
                }
                else
                {
                    bool b = ProcessBooleanAttribute("new-each-time", newEachTimeAtt);
                    determinism = b ? UserFunction.Determinism.PROACTIVE : UserFunction.Determinism.DETERMINISTIC;
                }
            }

            bool cache = false;
            if (cacheAtt != null)
            {
                cache = ProcessBooleanAttribute("cache", cacheAtt);
            }

            if (determinism == UserFunction.Determinism.DETERMINISTIC || cache)
            {
                memoFunction = true;
            }
        }

        private FunctionStreamability GetStreamabilityValue(string s)
        {
            if (s.Contains(":"))
            {

                // QNames are allowed but not recognized by Saxon
                MakeQName(s, null, "streamability");
                return FunctionStreamability.UNCLASSIFIED;
            }

            try
            {
                return FunctionStreamabilityExtensions.Of(s);
            }
            catch (ArgumentException)
            {
                InvalidAttribute("streamability", "unclassified|absorbing|inspection|filter|shallow-descent|deep-descent|ascent");
                return FunctionStreamability.UNCLASSIFIED;
            }
        }

        public override StructuredQName GetObjectName()
        {
            StructuredQName qn = base.GetObjectName();
            if (qn == null)
            {
                nameAtt = Whitespace.Trim(GetAttributeValue(NamespaceUri.NULL, "name"));
                if (nameAtt == null)
                {
                    return new StructuredQName("saxon", NamespaceUri.SAXON, "badly-named-function" + GenerateId());
                }

                qn = MakeQName(nameAtt, null, "name");
                SetObjectName(qn);
            }

            return qn;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override bool MayContainParam()
        {
            return true;
        }

        protected override bool IsPermittedChild(StyleElement child)
        {
            return child is XSLLocalParam;
        }

        public override Visibility GetVisibility()
        {
            if (visibility == Visibility.UNDEFINED)
            {
                string vAtt = GetAttributeValue(NamespaceUri.NULL, "visibility");
                return vAtt == null ? Visibility.PRIVATE : InterpretVisibilityValue(Whitespace.Trim(vAtt), "");
            }

            return visibility;
        }

        public SymbolicName.F GetSymbolicName()
        {
            return new SymbolicName.F(GetObjectName(), NumberOfParameters);
        }

        public void CheckCompatibility(Component component)
        {
            if (compiledFunction == null)
            {
                GetCompiledFunction();
            }

            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            UserFunction other = (UserFunction)component.GetActor();
            if (!compiledFunction.GetSymbolicName().Equals(other.GetSymbolicName()))
            {

                // Can't happen
                CompileError("The overriding xsl:function " + nameAtt + " does not match the overridden function: " + "the function name/arity does not match", "XTSE3070");
            }

            if (!compiledFunction.DeclaredResultType.IsSameType(other.DeclaredResultType, th))
            {
                CompileError("The overriding xsl:function " + nameAtt + " does not match the overridden function: " + "the return type does not match", "XTSE3070");
            }

            if (!compiledFunction.DeclaredStreamability.Equals(other.DeclaredStreamability))
            {
                CompileError("The overriding xsl:function " + nameAtt + " does not match the overridden function: " + "the streamability category does not match", "XTSE3070");
            }

            if (!compiledFunction.GetDeterminism().Equals(other.GetDeterminism()))
            {
                CompileError("The overriding xsl:function " + nameAtt + " does not match the overridden function: " + "the new-each-time attribute does not match", "XTSE3070");
            }

            for (int i = 0; i < NumberOfParameters; i++)
            {
                if (!compiledFunction.GetArgumentType(i).IsSameType(other.GetArgumentType(i), th))
                {
                    CompileError("The overriding xsl:function " + nameAtt + " does not match the overridden function: " + "the type of the " + RoleDiagnostic.Ordinal(i + 1) + " argument does not match", "XTSE3070");
                }
            }
        }

        public virtual bool IsOverrideExtensionFunction()
        {
            if (overrideExtensionFunctionAtt == null)
            {

                // this is a forwards reference
                PrepareAttributes();
            }

            return overrideExtensionFunction;
        }

        public virtual bool IsUpdating()
        {
            return updating;
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {

            GetCompiledFunction();
            top.IndexFunction(decl);
        }

        public override void Validate(ComponentDeclaration decl)
        {
            stackFrameMap = GetConfiguration().MakeSlotManager();

            // check the element is at the top level of the stylesheet
            CheckTopLevel("XTSE0010", true);
            int arity = NumberOfParameters;
            if (arity == 0 && streamability != FunctionStreamability.UNCLASSIFIED)
            {
                CompileError("A function with no arguments must have streamability=unclassified", "XTSE3155");
            }

            int maxArity = NumberOfParameters;
            int minArity = maxArity - NumberOfOptionalParameters;
            if (minArity <= 1 && maxArity >= 1)
            {
                ISchemaType type = GetConfiguration().GetSchemaType(GetObjectName());
                if (type is IPlainType)
                {
                    CompileError("Stylesheet function clashes with constructor function for an imported atomic type", "XTSE0770");
                }
            }
        }

        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
            Expression exp = CompileSequenceConstructor(compilation, decl, false);
            if (exp == null)
            {
                exp = Literal.MakeEmptySequence();
            }
            else if (Literal.IsEmptySequence(exp))
            {
            }
            else
            {
                if (visibility == Visibility.ABSTRACT)
                {
                    CompileError("A function defined with visibility='abstract' must have no body");
                }

                exp = exp.Simplify();
            }

            UserFunction fn = GetCompiledFunction();
            fn.SetBody(exp);
            fn.SetStackFrameMap(stackFrameMap);
            BindParameterDefinitions(fn);
            fn.SetRetainedStaticContext(MakeRetainedStaticContext());
            fn.SetOverrideExtensionFunction(overrideExtensionFunction);
            if (compilation.GetCompilerInfo().CodeInjector != null)
            {
                compilation.GetCompilerInfo().CodeInjector.Process(fn);
            }

            Component overridden = OverriddenComponent;
            if (overridden != null)
            {
                CheckCompatibility(overridden);
            }
        }

        public void Optimize(ComponentDeclaration declaration)
        {
            Expression exp = compiledFunction.GetBody();
            ExpressionTool.ResetPropertiesWithinSubtree(exp);
            ExpressionVisitor visitor = MakeExpressionVisitor();
            Expression exp2 = exp.TypeCheck(visitor, ContextItemStaticInfo.ABSENT);
            if (streamability.IsStreaming())
            {
                visitor.SetOptimizeForStreaming(true);
            }

            exp2 = ExpressionTool.OptimizeComponentBody(exp2, GetCompilation(), visitor, ContextItemStaticInfo.ABSENT, true);
            SetInstructionLocation(this, exp2);
            compiledFunction.SetBody(exp2);

            // Assess the streamability of the function body
            Optimizer optimizer = visitor.GetConfiguration().ObtainOptimizer();
            if (streamability.IsStreaming())
            {
                optimizer.AssessFunctionStreamability(this, compiledFunction);
            }

            AllocateLocalSlots(exp2);
            if (exp2 != exp)
            {
                compiledFunction.SetBody(exp2);
            }

            OptimizerOptions options = GetCompilation().GetCompilerInfo().GetOptimizerOptions();
            if (options.IsSet(OptimizerOptions.TAIL_CALLS) && !streamability.IsStreaming())
            {
                int tailCalls = ExpressionTool.MarkTailFunctionCalls(exp2, GetObjectName(), NumberOfParameters);
                if (tailCalls != 0)
                {
                    compiledFunction.SetTailRecursive(tailCalls > 0, tailCalls > 1);
                    exp2 = compiledFunction.GetBody();
                    compiledFunction.SetBody(new TailCallLoop(compiledFunction, exp2));
                }
            }


            if (streamability.IsStreaming())
            {
                compiledFunction.PrepareForStreaming();
            }

            if (explaining)
            {
                exp2.Explain(GetConfiguration().Logger);
            }
        }

        public SlotManager GetSlotManager()
        {
            return stackFrameMap;
        }

        public virtual void SetParameterDefinitions(UserFunction fn)
        {
            UserFunctionParameter[] @params = new UserFunctionParameter[NumberOfParameters];
            int count = 0;
            int optional = 0;
            foreach (NodeInfo node in Children())
            {
                if (node is XSLLocalParam)
                {
                    UserFunctionParameter param = new UserFunctionParameter();
                    @params[count] = param;
                    param.SetRequiredType(((XSLLocalParam)node).GetRequiredType());
                    param.SetVariableQName(((XSLLocalParam)node).GetVariableQName());
                    param.SetSlotNumber(((XSLLocalParam)node).SlotNumber);
                    if (XSLLocalParam.IsNo(Whitespace.Trim(((XSLLocalParam)node).GetAttributeValue("required"))))
                    {
                        optional++;
                        param.SetRequired(false);
                    }

                    if (count == 0 && streamability != FunctionStreamability.UNCLASSIFIED)
                    {
                        param.FunctionStreamability = streamability;
                    }

                    count++;
                }
                else
                {
                    break;
                }
            }

            fn.SetParameterDefinitions(@params);
            fn.SetMinimumArity(count - optional);
        }

        private void BindParameterDefinitions(UserFunction fn)
        {
            UserFunctionParameter[] @params = fn.GetParameterDefinitions();
            int count = 0;
            foreach (NodeInfo node in Children(new TypeIsInstancePredicate(typeof(XSLLocalParam))))
            {
                UserFunctionParameter param = @params[count++];
                param.SetRequiredType(((XSLLocalParam)node).GetRequiredType());
                param.SetVariableQName(((XSLLocalParam)node).GetVariableQName());
                param.SetSlotNumber(((XSLLocalParam)node).SlotNumber);
                ((XSLLocalParam)node).GetSourceBinding().FixupBinding(param);
            }
        }

        public virtual UserFunction GetCompiledFunction()
        {
            if (compiledFunction == null)
            {
                PrepareAttributes();
                UserFunction fn = GetConfiguration().NewUserFunction(memoFunction, streamability);
                fn.SetPackageData(GetCompilation().GetPackageData());
                fn.SetFunctionName(GetObjectName());
                int maxArity = NumberOfParameters;
                int minArity = maxArity - NumberOfOptionalParameters;
                fn.SetArityRange(minArity, maxArity);
                SetParameterDefinitions(fn);
                fn.ResultType = ResultType;
                fn.SetLineNumber(GetLineNumber());
                fn.SetColumnNumber(GetColumnNumber());
                fn.SetSystemId(GetSystemId());
                fn.ObtainDeclaringComponent(this);
                fn.DeclaredVisibility = DeclaredVisibility;
                fn.DeclaredStreamability = streamability;
                fn.SetDeterminism(determinism);
                fn.SetIxslUpdating(updating);
                IList<Annotation> annotations = new List<Annotation>();
                if (memoFunction)
                {
                    annotations.Add(new Annotation(new StructuredQName("saxon", NamespaceUri.SAXON, "memo-function")));
                }

                fn.SetAnnotations(new AnnotationList(annotations));
                fn.SetOverrideExtensionFunction(overrideExtensionFunction);
                compiledFunction = fn;
            }

            return compiledFunction;
        }
        Actor IStylesheetComponent.GetActor() => GetActor();
        SymbolicName IStylesheetComponent.GetSymbolicName() => GetSymbolicName();
    }
}
