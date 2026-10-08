////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using static OutSmart.DAXon.Xslt.SourceBinding.BindingProperty;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Helper class for xsl:variable and xsl:param elements. <br>
    /// </summary>
    public class SourceBinding
    {
        private readonly StyleElement sourceElement;
        private StructuredQName name;
        private Expression select = null;
        private Values.SequenceType declaredType = null;
        private Values.SequenceType inferredType = null;
        protected SlotManager slotManager = null; // used only for global variable declarations
        private Visibility visibility;
        private IGroundedValue constantValue = null;

        private readonly HashSet<BindingProperty> properties = new HashSet<BindingProperty>();
        // List of VariableReference objects that reference this XSLVariableDeclaration
        private readonly IList<IBindingReference> references = new List<IBindingReference>(10);

        public virtual StyleElement SourceElement => sourceElement;

        public virtual StructuredQName VariableQName
        {
            get
            {
                if (name == null)
                {
                    ProcessVariableName(sourceElement.GetAttributeValue(NamespaceUri.NULL, "name"));
                }

                return name;
            }
            set
            {
                this.name = value;
            }
        }

        public virtual IList<IBindingReference> References => references;

        public virtual Values.SequenceType DeclaredType
        {
            get
            {
                if (declaredType == null)
                {

                    // may be handling a forwards reference - see hof-038
                    string asAtt = sourceElement.GetAttributeValue(NamespaceUri.NULL, "as");
                    if (asAtt == null)
                    {
                        return null;
                    }
                    else
                    {
                        try
                        {
                            declaredType = sourceElement.MakeSequenceType(asAtt);
                        }
                        catch (XPathException)
                        {
                        }
                    }
                }

                return declaredType;
            }
            set
            {
                this.declaredType = value;
            }
        }

        public virtual IGroundedValue ConstantValue
        {
            get
            {
                if (constantValue == null)
                {
                    Values.SequenceType type = GetInferredType(true);
                    TypeHierarchy th = sourceElement.GetConfiguration().GetTypeHierarchy();
                    if (!HasProperty(BindingProperty.ASSIGNABLE) && !HasProperty(BindingProperty.PARAM) && !(visibility == Visibility.PUBLIC || visibility == Visibility.ABSTRACT))
                    {
                        if (select is Literal)
                        {

                            // we can't rely on the constant value because it hasn't yet been type-checked,
                            // which could change it (eg by numeric promotion). Rather than attempt all the type-checking
                            // now, we do a quick check. See test bug64
                            Affinity relation = th.Relationship(select.GetItemType(), type.PrimaryType);
                            if (relation == Affinity.SAME_TYPE || relation == Affinity.SUBSUMED_BY)
                            {
                                constantValue = ((Literal)select).GroundedValue;
                            }
                        }
                    }
                }

                return constantValue;
            }
        }
        public SourceBinding(StyleElement sourceElement)
        {
            this.sourceElement = sourceElement;
        }

        public virtual void PrepareAttributes(HashSet<BindingProperty> permittedAttributes)
        {
            IAttributeMap atts = sourceElement.Attributes();
            AttributeInfo selectAtt = null;
            string asAtt = null;
            string extraAsAtt = null;
            string requiredAtt = null;
            string tunnelAtt = null;
            string assignableAtt = null;
            string staticAtt = null;
            string visibilityAtt = null;
            foreach (AttributeInfo att in atts)
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                if (f.Equals("name"))
                {
                    if (name == null || name.Equals(ErrorName()))
                    {
                        ProcessVariableName(att.Value);
                    }
                }
                else if (f.Equals("select"))
                {
                    if (permittedAttributes.Contains(SELECT))
                    {
                        selectAtt = att;
                    }
                    else
                    {
                        sourceElement.CompileErrorInAttribute("The select attribute is not permitted on a function parameter", "XTSE0760", "select");
                    }
                }
                else if (f.Equals("as") && permittedAttributes.Contains(BindingProperty.AS))
                {
                    asAtt = att.Value;
                }
                else if (f.Equals("required") && permittedAttributes.Contains(BindingProperty.REQUIRED))
                {
                    requiredAtt = Whitespace.Trim(att.Value);
                }
                else if (f.Equals("tunnel"))
                {
                    tunnelAtt = Whitespace.Trim(att.Value);
                }
                else if (f.Equals("static") && permittedAttributes.Contains(BindingProperty.STATIC))
                {
                    staticAtt = Whitespace.Trim(att.Value);
                }
                else if (f.Equals("visibility") && permittedAttributes.Contains(BindingProperty.VISIBILITY))
                {
                    visibilityAtt = Whitespace.Trim(att.Value);
                }
                else if (NamespaceUri.SAXON.Equals(attName.GetNamespaceUri()))
                {
                    if (sourceElement.IsExtensionAttributeAllowed(attName.DisplayName))
                    {
                        if (attName.GetLocalPart().Equals("assignable") && permittedAttributes.Contains(BindingProperty.ASSIGNABLE))
                        {
                            assignableAtt = Whitespace.Trim(att.Value);
                        }
                        else if (attName.GetLocalPart().Equals("as"))
                        {
                            extraAsAtt = att.Value;
                        }
                        else
                        {
                            sourceElement.CheckUnknownAttribute(att.GetNodeName());
                        }
                    }
                }
                else
                {
                    sourceElement.CheckUnknownAttribute(att.GetNodeName());
                }
            }

            if (name == null)
            {
                sourceElement.ReportAbsence("name");
                name = ErrorName();
            }

            if (selectAtt != null)
            {
                select = sourceElement.MakeExpression(selectAtt.Value, selectAtt);
            }

            if (requiredAtt != null)
            {
                bool required = sourceElement.ProcessBooleanAttribute("required", requiredAtt);
                SetProperty(BindingProperty.REQUIRED, required);
                if (required && select != null)
                {
                    sourceElement.CompileError("xsl:param: cannot supply a default value when required='yes'");
                }
            }

            if (tunnelAtt != null)
            {
                bool tunnel = sourceElement.ProcessBooleanAttribute("tunnel", tunnelAtt);
                if (tunnel && !permittedAttributes.Contains(BindingProperty.TUNNEL))
                {
                    sourceElement.CompileErrorInAttribute("The only permitted value of the 'tunnel' attribute is 'no'", "XTSE0020", "tunnel");
                }

                SetProperty(BindingProperty.TUNNEL, tunnel);
            }

            if (assignableAtt != null)
            {
                bool assignable = sourceElement.ProcessBooleanAttribute("saxon:assignable", assignableAtt);
                SetProperty(BindingProperty.ASSIGNABLE, assignable);
            }

            if (staticAtt != null)
            {
                bool statick = sourceElement.ProcessBooleanAttribute("static", staticAtt);
                SetProperty(BindingProperty.STATIC, statick);
                if (statick)
                {
                    SetProperty(BindingProperty.DISALLOWS_CONTENT, true);
                }

                if (statick && !HasProperty(BindingProperty.GLOBAL))
                {
                    sourceElement.CompileErrorInAttribute("Only global declarations can be static", "XTSE0020", "static");
                }
            }

            declaredType = CombineTypeDeclarations(asAtt, extraAsAtt);
            if (visibilityAtt != null)
            {
                if (HasProperty(BindingProperty.PARAM))
                {
                    sourceElement.CompileErrorInAttribute("The visibility attribute is not allowed on xsl:param", "XTSE0020", "visibility");
                }
                else
                {
                    visibility = sourceElement.InterpretVisibilityValue(visibilityAtt, "");
                }

                if (!HasProperty(BindingProperty.GLOBAL))
                {
                    sourceElement.CompileErrorInAttribute("The visibility attribute is allowed only on global declarations", "XTSE0020", "visibility");
                }
            }

            if (HasProperty(BindingProperty.STATIC) && visibility != Visibility.PRIVATE && visibilityAtt != null)
            {
                sourceElement.CompileErrorInAttribute("A static variable or parameter must be private", "XTSE0020", "static");
            }
        }

        public virtual void PrepareTemplateSignatureAttributes()
        {
            IAttributeMap atts = sourceElement.Attributes();
            string asAtt = null;
            string extraAsAtt = null;
            foreach (AttributeInfo att in atts)
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                if (f.Equals("name"))
                {
                    if (name == null || name.Equals(ErrorName()))
                    {
                        ProcessVariableName(att.Value);
                    }
                }
                else if (f.Equals("as"))
                {
                    asAtt = att.Value;
                }
                else if (f.Equals("required"))
                {
                    string requiredAtt = Whitespace.Trim(att.Value);
                    bool required = sourceElement.ProcessBooleanAttribute("required", requiredAtt);
                    SetProperty(BindingProperty.REQUIRED, required);
                }
                else if (f.Equals("tunnel"))
                {
                    string tunnelAtt = Whitespace.Trim(att.Value);
                    bool tunnel = sourceElement.ProcessBooleanAttribute("tunnel", tunnelAtt);
                    SetProperty(BindingProperty.TUNNEL, tunnel);
                }
                else if (NamespaceUri.SAXON.Equals(attName.GetNamespaceUri()))
                {
                    if (attName.GetLocalPart().Equals("as"))
                    {
                        extraAsAtt = att.Value;
                    }
                }
            }

            if (name == null)
            {
                sourceElement.ReportAbsence("name");
                name = ErrorName();
            }

            declaredType = CombineTypeDeclarations(asAtt, extraAsAtt);
        }

        private Values.SequenceType CombineTypeDeclarations(string asAtt, string extraAsAtt)
        {
            Values.SequenceType declaredType = null;
            if (asAtt != null)
            {
                try
                {
                    declaredType = sourceElement.MakeSequenceType(asAtt);
                }
                catch (XPathException e)
                {
                    sourceElement.CompileErrorInAttribute(e.Message, e.ShowErrorCode(), "as");
                }
            }

            if (extraAsAtt != null)
            {
                Values.SequenceType extraResultType = null;
                try
                {
                    extraResultType = sourceElement.MakeExtendedSequenceType(extraAsAtt);
                }
                catch (XPathException e)
                {
                    sourceElement.CompileErrorInAttribute(e.Message, e.ShowErrorCode(), "saxon:as");
                    extraResultType = Values.SequenceType.ANY_SEQUENCE;
                }

                if (asAtt != null)
                {
                    Affinity rel = sourceElement.GetConfiguration().GetTypeHierarchy().SequenceTypeRelationship(extraResultType, declaredType);
                    if (rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMED_BY)
                    {
                        declaredType = extraResultType;
                    }
                    else
                    {
                        sourceElement.CompileErrorInAttribute("When both are present, @saxon:as must be a subtype of @as", "SXER7TBA", "as");
                    }
                }
                else
                {
                    declaredType = extraResultType;
                }
            }

            return declaredType;
        }

        private void ProcessVariableName(string nameAttribute)
        {
            if (nameAttribute != null)
            {
                if (nameAttribute.StartsWith("$", StringComparison.Ordinal))
                {
                    sourceElement.CompileErrorInAttribute("Invalid variable name (no '$' sign needed)", "XTSE0020", "name");
                    nameAttribute = nameAttribute.Substring(1);
                }

                name = sourceElement.MakeQName(nameAttribute, null, "name");
            }
        }

        private StructuredQName ErrorName()
        {
            return new StructuredQName("saxon", NamespaceUri.SAXON, "error-variable-name");
        }

        public virtual void Validate()
        {
            if (select != null && sourceElement.HasChildNodes())
            {
                sourceElement.CompileError("An " + sourceElement.DisplayName + " element with a select attribute must be empty", "XTSE0620");
            }

            if (HasProperty(BindingProperty.DISALLOWS_CONTENT) && sourceElement.HasChildNodes())
            {
                if (IsStatic())
                {
                    sourceElement.CompileError("A static variable or parameter must have no content", "XTSE0010");
                }
                else
                {
                    sourceElement.CompileError("Within xsl:function, an xsl:param element must have no content", "XTSE0620");
                }
            }

            if (visibility == Visibility.ABSTRACT && (select != null || sourceElement.HasChildNodes()))
            {
                sourceElement.CompileError("An abstract variable must have no select attribute and no content", "XTSE0620");
            }
        }

        public virtual void PostValidate()
        {
            CheckAgainstRequiredType(declaredType);
            if (select == null && !HasProperty(BindingProperty.DISALLOWS_CONTENT) && visibility != Visibility.ABSTRACT)
            {
                IAxisIterator kids = sourceElement.IterateAxis(AxisInfo.CHILD);
                NodeInfo first = kids.Next();
                if (first == null)
                {
                    if (declaredType == null)
                    {
                        select = new StringLiteral(StringValue.EMPTY_STRING);
                        select.SetRetainedStaticContext(sourceElement.MakeRetainedStaticContext());
                    }
                    else
                    {
                        if (sourceElement is XSLLocalParam || sourceElement is XSLGlobalParam)
                        {
                            if (!HasProperty(BindingProperty.REQUIRED))
                            {
                                if (Cardinality.AllowsZero(declaredType.GetCardinality()))
                                {
                                    select = Literal.MakeEmptySequence();
                                    select.SetRetainedStaticContext(sourceElement.MakeRetainedStaticContext());
                                }
                                else
                                {

                                    // The implicit default value () is not valid for the required type, so
                                    // it is treated as if there is no default
                                    SetProperty(BindingProperty.IMPLICITLY_REQUIRED, true);
                                }
                            }
                        }
                        else
                        {
                            if (Cardinality.AllowsZero(declaredType.GetCardinality()))
                            {
                                select = Literal.MakeEmptySequence();
                                select.SetRetainedStaticContext(sourceElement.MakeRetainedStaticContext());
                            }
                            else
                            {
                                sourceElement.CompileError("The implicit value () is not valid for the declared type", "XTTE0570");
                            }
                        }
                    }
                }
            }

            select = sourceElement.TypeCheck("select", select);
        }

        public virtual bool IsStatic()
        {
            return HasProperty(BindingProperty.STATIC);
        }

        public virtual void CheckAgainstRequiredType(Values.SequenceType required)
        {
            if (visibility != Visibility.ABSTRACT)
            {
                try
                {
                    if (required != null)
                    {

                        // check that the expression is consistent with the required type
                        if (select != null)
                        {
                            int category = RoleDiagnostic.VARIABLE;
                            string errorCode = "XTTE0570";
                            if (sourceElement is XSLLocalParam)
                            {
                                category = RoleDiagnostic.PARAM;
                                errorCode = "XTTE0600";
                            }
                            else if (sourceElement is XSLWithParam || sourceElement is XSLGlobalParam)
                            {
                                category = RoleDiagnostic.PARAM;
                                errorCode = "XTTE0590";
                            }

                            int selectedCategory = category;
                            string selectedErrorCode = errorCode;
                            Func<RoleDiagnostic> role = () => new RoleDiagnostic(selectedCategory, name.DisplayName, 0, selectedErrorCode);
                            select = sourceElement.GetConfiguration().GetTypeChecker(false).StaticTypeCheck(select, required, role, sourceElement.MakeExpressionVisitor());
                        }
                        else
                        {
                        }
                    }
                }
                catch (XPathException err)
                {
                    err.SetLocator(sourceElement); // because the expression wasn't yet linked into the module
                    sourceElement.CompileError(err);
                    select = new ErrorExpression(new XmlProcessingException(err));
                }
            }
        }

        public virtual void SetProperty(BindingProperty prop, bool flag)
        {
            if (flag)
            {
                properties.Add(prop);
            }
            else
            {
                properties.Remove(prop);
            }
        }

        public virtual bool HasProperty(BindingProperty prop)
        {
            return properties.Contains(prop);
        }

        public virtual SlotManager GetSlotManager()
        {
            return slotManager;
        }

        public virtual void HandleSequenceConstructor(Compilation compilation, ComponentDeclaration decl)
        {

            // handle the "temporary tree" case by creating a IDocument sub-instruction
            // to construct and return a document node.
            if (sourceElement.HasChildNodes())
            {
                if (declaredType == null)
                {
                    Expression b = sourceElement.CompileSequenceConstructor(compilation, decl, true);
                    if (b == null)
                    {
                        b = Literal.MakeEmptySequence();
                    }

                    bool textonly = UType.TEXT.Subsumes(b.GetItemType().GetUType());
                    UnicodeString constant = null; // bug 3748
                    if (textonly && b is ValueOf && ((ValueOf)b).Select is StringLiteral)
                    {
                        constant = ((StringLiteral)((ValueOf)b).Select).GetString();
                    }

                    DocumentInstr doc = new DocumentInstr(textonly, constant);
                    doc.SetContentExpression(b);
                    doc.SetRetainedStaticContext(sourceElement.MakeRetainedStaticContext());
                    select = doc;
                }
                else
                {
                    select = sourceElement.CompileSequenceConstructor(compilation, decl, true);
                    if (select == null)
                    {
                        select = Literal.MakeEmptySequence();
                    }

                    try
                    {
                        Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, name.DisplayName, 0, "XTTE0570");
                        select = select.Simplify();
                        select = sourceElement.GetConfiguration().GetTypeChecker(false).StaticTypeCheck(select, declaredType, role, sourceElement.MakeExpressionVisitor());
                    }
                    catch (XPathException err)
                    {
                        err.SetLocator(sourceElement);
                        IXmlProcessingError error = new XmlProcessingException(err);
                        sourceElement.CompileError(error);
                        select = new ErrorExpression(error);
                    }
                }
            }
        }

        public virtual Expression GetSelectExpression()
        {
            return select;
        }

        public virtual Values.SequenceType GetInferredType(bool useContentRules)
        {
            if (inferredType != null)
            {
                return inferredType;
            }

            Visibility visibility = sourceElement.GetVisibility();
            if (HasProperty(BindingProperty.PARAM) || HasProperty(BindingProperty.ASSIGNABLE) || !(visibility == Visibility.PRIVATE || visibility == Visibility.FINAL))
            {
                Values.SequenceType declared = DeclaredType;
                return inferredType = declared == null ? Values.SequenceType.ANY_SEQUENCE : declared;
            }

            if (select != null)
            {
                TypeHierarchy th = sourceElement.GetConfiguration().GetTypeHierarchy();
                if (Literal.IsEmptySequence(select))
                {

                    // returning Types.EMPTY gives problems with static type checking
                    return inferredType = declaredType == null ? Values.SequenceType.ANY_SEQUENCE : declaredType;
                }

                Types.ItemType actual = select.GetItemType();
                int card = select.GetCardinality();
                if (declaredType != null)
                {
                    if (!th.IsSubType(actual, declaredType.PrimaryType))
                    {
                        actual = declaredType.PrimaryType;
                    }

                    if (!Cardinality.Subsumes(declaredType.GetCardinality(), card))
                    {
                        card = declaredType.GetCardinality();
                    }
                }

                inferredType = Values.SequenceType.MakeSequenceType(actual, card);
                return inferredType;
            }

            if (useContentRules)
            {
                if (sourceElement.HasChildNodes())
                {
                    if (declaredType == null)
                    {
                        return Values.SequenceType.MakeSequenceType(NodeKindTest.DOCUMENT, StaticProperty.EXACTLY_ONE);
                    }
                    else
                    {
                        return declaredType;
                    }
                }
                else
                {
                    if (declaredType == null)
                    {

                        // no select attribute or content: value is an empty string
                        return Values.SequenceType.SINGLE_STRING;
                    }
                    else
                    {
                        return declaredType;
                    }
                }
            }

            return declaredType;
        }

        public virtual void RegisterReference(IBindingReference @ref)
        {
            references.Add(@ref);
        }

        public virtual void FixupReferences(GlobalVariable compiledGlobalVariable)
        {
            Values.SequenceType type = GetInferredType(true);

            //IGroundedValue constantValue = null;
            int properties = 0;
            if (!HasProperty(BindingProperty.ASSIGNABLE) && !HasProperty(BindingProperty.PARAM) && !(visibility == Visibility.PUBLIC || visibility == Visibility.ABSTRACT))
            {
                /*if (select instanceof Literal) {
                // we can't rely on the constant value because it hasn't yet been type-checked,
                // which could change it (eg by numeric promotion). Rather than attempt all the type-checking
                // now, we do a quick check. See test bug64
                int relation = th.relationship(select.getItemType(), type.getPrimaryType());
                if (relation == TypeHierarchy.SAME_TYPE || relation == TypeHierarchy.SUBSUMED_BY) {
                    constantValue = ((Literal) select).getValue();
                }
            } */
                if (select != null)
                {
                    properties = select.GetSpecialProperties();
                }
            }

            foreach (IBindingReference reference in references)
            {
                if (compiledGlobalVariable != null)
                {
                    reference.Fixup(compiledGlobalVariable);
                }

                reference.SetStaticType(type, ConstantValue, properties);
            }
        }

        public virtual void FixupBinding(IBinding binding)
        {
            foreach (IBindingReference reference in references)
            {
                reference.Fixup(binding);
            }
        }
        //private int properties;
        public enum BindingProperty
        {
            PRIVATE,
            GLOBAL,
            PARAM,
            TUNNEL,
            REQUIRED,
            IMPLICITLY_REQUIRED,
            ASSIGNABLE,
            SELECT,
            AS,
            DISALLOWS_CONTENT,
            STATIC,
            VISIBILITY,
            IMPLICITLY_DECLARED
        }
    }
}