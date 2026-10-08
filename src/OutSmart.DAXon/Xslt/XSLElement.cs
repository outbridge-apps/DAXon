////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:element element in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLElement : StyleElement
    {
        private Expression elementName;
        private Expression @namespace = null;
        private string use;
        private StructuredQName[] attributeSets = null;
        private int validation;
        private ISchemaType schemaType = null;
        private bool inheritNamespaces = true;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string nameAtt = null;
            string namespaceAtt = null;
            string validationAtt = null;
            string typeAtt = null;
            string inheritAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                switch (f)
                {
                    case "name":
                        nameAtt = Whitespace.Trim(value);
                        elementName = MakeAttributeValueTemplate(nameAtt, att);
                        break;
                    case "namespace":
                        namespaceAtt = value;
                        @namespace = MakeAttributeValueTemplate(namespaceAtt, att);
                        break;
                    case "validation":
                        validationAtt = Whitespace.Trim(value);
                        break;
                    case "type":
                        typeAtt = Whitespace.Trim(value);
                        break;
                    case "inherit-namespaces":
                        inheritAtt = Whitespace.Trim(value);
                        break;
                    case "use-attribute-sets":
                        use = value;
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (nameAtt == null)
            {
                ReportAbsence("name");
            }
            else
            {
                if (elementName is StringLiteral)
                {
                    if (!NameChecker.IsQName(((StringLiteral)elementName).GroundedValue.CodePoints()))
                    {
                        CompileError("Element name " + Err.Wrap(((StringLiteral)elementName).Stringify()) + " is not a valid QName", "XTDE0820");

                        // to prevent duplicate error messages:
                        elementName = new StringLiteral("saxon-error-element");
                    }
                }
            }

            if (namespaceAtt != null)
            {
                if (@namespace is StringLiteral)
                {
                    if (!StandardURIChecker.GetInstance().IsValidURI(((StringLiteral)@namespace).Stringify()))
                    {
                        CompileError("The value of the namespace attribute must be a valid URI", "XTDE0835");
                    }
                }
            }

            if (validationAtt != null)
            {
                validation = ValidateValidationAttribute(validationAtt);
            }
            else
            {
                validation = DefaultValidation;
            }

            if (typeAtt != null)
            {
                if (!IsSchemaAware())
                {
                    CompileError("The @type attribute is available only with a schema-aware XSLT processor", "XTSE1660");
                }

                schemaType = GetSchemaType(typeAtt);
                validation = Validation.BY_TYPE;
            }

            if (typeAtt != null && validationAtt != null)
            {
                CompileError("The @validation and @type attributes are mutually exclusive", "XTSE1505");
            }

            if (inheritAtt != null)
            {
                inheritNamespaces = ProcessBooleanAttribute("inherit-namespaces", inheritAtt);
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (use != null)
            {

                // get the names of referenced attribute sets
                attributeSets = GetUsedAttributeSets(use);
            }

            elementName = TypeCheck("name", elementName);
            @namespace = TypeCheck("namespace", @namespace);
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {

            // deal specially with the case where the element name is known statically
            if (elementName is StringLiteral)
            {
                string qName = ((StringLiteral)elementName).Stringify();
                string[] parts;
                try
                {
                    parts = NameChecker.GetQNameParts(qName);
                }
                catch (QNameException)
                {
                    CompileErrorInAttribute("Invalid element name: " + qName, "XTDE0820", "name");
                    return null;
                }

                NamespaceUri nsuri = null;
                if (@namespace is StringLiteral)
                {
                    nsuri = NamespaceUri.Of(((StringLiteral)@namespace).Stringify());
                    if (nsuri.IsEmpty())
                    {
                        parts[0] = "";
                    }
                }
                else if (@namespace == null)
                {
                    nsuri = GetURIForPrefix(parts[0], true);
                    if (nsuri == null)
                    {
                        UndeclaredNamespaceError(parts[0], "XTDE0830", "name");
                    }
                }

                // A name in the xmlns namespace, one in the xml namespace under another prefix and the prefix xmlns have
                // rules of their own (XTDE0835, a prefix to replace), which the computed form applies when the
                // instruction runs; a FixedElement of such a name failed in the namespace map or declared xmlns.
                bool reserved = nsuri != null && (nsuri.Equals(NamespaceUri.XMLNS) || parts[0].Equals("xmlns") || (nsuri.Equals(NamespaceUri.XML) && !parts[0].Equals("xml")));
                if (nsuri != null && !reserved)
                {

                    // Local name and namespace are both known statically: generate a FixedElement instruction
                    FingerprintedQName qn = new FingerprintedQName(parts[0], nsuri, parts[1]);
                    qn.ObtainFingerprint(GetNamePool());
                    FixedElement FixedElementInst = new FixedElement(qn, NamespaceMap.EmptyMap(), inheritNamespaces, true, schemaType, validation);
                    FixedElementInst.SetLocation(AllocateLocation());
                    return CompileContentExpression(exec, decl, FixedElementInst);
                }
            }

            ComputedElement inst = new ComputedElement(elementName, @namespace, schemaType, validation, inheritNamespaces, false);
            inst.SetLocation(AllocateLocation());
            return CompileContentExpression(exec, decl, inst);
        }

        private Expression CompileContentExpression(Compilation exec, ComponentDeclaration decl, ElementCreator inst)
        {
            Expression content = CompileSequenceConstructor(exec, decl, true);
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
            return inst;
        }
    }
}