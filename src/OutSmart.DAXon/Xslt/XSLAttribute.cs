////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// xsl:attribute element in stylesheet. <br>
    /// </summary>
    internal sealed class XSLAttribute : XSLLeafNodeConstructor
    {
        private Expression attributeName;
        private Expression separator;
        private Expression @namespace = null;
        private int validationAction = Validation.PRESERVE;
        private ISimpleType schemaType;

        protected override string ErrorCodeForSelectPlusContent => "XTSE0840";
        public override void PrepareAttributes()
        {
            string nameAtt = null;
            string namespaceAtt = null;
            string selectAtt = null;
            string separatorAtt = null;
            string validationAtt = null;
            string typeAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                switch (f)
                {
                    case "name":
                        nameAtt = Whitespace.Trim(value);
                        attributeName = MakeAttributeValueTemplate(nameAtt, att);
                        break;
                    case "namespace":
                        namespaceAtt = Whitespace.Trim(value);
                        @namespace = MakeAttributeValueTemplate(namespaceAtt, att);
                        break;
                    case "select":
                        selectAtt = value;
                        select = MakeExpression(selectAtt, att);
                        break;
                    case "separator":
                        separatorAtt = value;
                        separator = MakeAttributeValueTemplate(separatorAtt, att);
                        break;
                    case "validation":
                        validationAtt = Whitespace.Trim(value);
                        break;
                    case "type":
                        typeAtt = Whitespace.Trim(value);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (nameAtt == null)
            {
                ReportAbsence("name");
                return;
            }

            if (attributeName is StringLiteral)
            {
                if (!NameChecker.IsQName(((StringLiteral)attributeName).GroundedValue.CodePoints()))
                {
                    InvalidAttributeName("Attribute name " + Err.Wrap(nameAtt) + " is not a valid QName");
                }

                if (nameAtt.Equals("xmlns"))
                {
                    if (@namespace == null)
                    {
                        InvalidAttributeName("Invalid attribute name: xmlns");
                    }
                }

                if (nameAtt.StartsWith("xmlns:", StringComparison.Ordinal))
                {
                    if (namespaceAtt == null)
                    {
                        InvalidAttributeName("Invalid attribute name: " + Err.Wrap(nameAtt));
                    }
                    else
                    {

                        // ignore the prefix "xmlns"
                        nameAtt = nameAtt.Substring(6);
                        attributeName = new StringLiteral(nameAtt);
                    }
                }
            }

            if (namespaceAtt != null)
            {
                if (@namespace is StringLiteral)
                {
                    if (!StandardURIChecker.GetInstance().IsValidURI(((StringLiteral)@namespace).Stringify()))
                    {
                        CompileError("The value of the namespace attribute must be a valid URI", "XTDE0865");
                    }
                }
            }

            if (separatorAtt == null)
            {
                if (selectAtt == null)
                {
                    separator = new StringLiteral(StringValue.EMPTY_STRING);
                }
                else
                {
                    separator = new StringLiteral(StringValue.SINGLE_SPACE);
                }
            }

            if (validationAtt != null)
            {
                validationAction = ValidateValidationAttribute(validationAtt);
            }
            else
            {
                validationAction = DefaultValidation;
            }

            if (typeAtt != null)
            {
                if (!IsSchemaAware())
                {
                    CompileError("The @type attribute is available only with a schema-aware XSLT processor", "XTSE1660");
                }
                else
                {
                    ISchemaType type = GetSchemaType(typeAtt);
                    if (type == null)
                    {
                        CompileError("Unknown attribute type " + typeAtt, "XTSE1520");
                    }
                    else
                    {
                        if (type.IsSimpleType())
                        {
                            schemaType = (ISimpleType)type;
                        }
                        else
                        {
                            CompileError("Type annotation for attributes must be a simple type", "XTSE1530");
                        }
                    }

                    validationAction = Validation.BY_TYPE;
                }
            }

            if (typeAtt != null && validationAtt != null)
            {
                CompileError("The validation and type attributes are mutually exclusive", "XTSE1505");
                validationAction = DefaultValidation;
                schemaType = null;
            }
        }

        private void InvalidAttributeName(string message)
        {
            CompileErrorInAttribute(message, "XTDE0850", "name");

            // prevent a duplicate error message...
            attributeName = new StringLiteral("saxon-error-attribute"); //        }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (schemaType != null)
            {
                if (schemaType.IsNamespaceSensitive())
                {
                    CompileErrorInAttribute("Validation at attribute level must not specify a " + "@namespace-sensitive type (xs:QName or xs:NOTATION)", "XTTE1545", "type");
                }
            }

            attributeName = TypeCheck("name", attributeName);
            @namespace = TypeCheck("namespace", @namespace);
            select = TypeCheck("select", select);
            separator = TypeCheck("separator", separator);

            //onEmpty = typeCheck("on-empty", onEmpty);
            base.Validate(decl);
        }

        // Covariant return in Java (Instruction extends Expression); net472 forbids covariant overrides,
        // and `public Instruction Compile` HID the virtual StyleElement.Compile — the style-tree compiler
        // dispatched to the base ("no action") and every xsl:attribute instruction compiled to NOTHING.
        public override Expression Compile(Compilation compilation, ComponentDeclaration decl)
        {

            // deal specially with the case where the attribute name is known statically
            if (attributeName is StringLiteral)
            {
                string qName = Whitespace.Trim(((StringLiteral)attributeName).Stringify());
                string[] parts;
                try
                {
                    parts = NameChecker.GetQNameParts(qName);
                }
                catch (QNameException e)
                {

                    // This can't happen, because of previous checks
                    return null;
                }

                if (@namespace == null)
                {
                    NamespaceUri nsuri = NamespaceUri.NULL;
                    if (!parts[0].Equals(""))
                    {
                        nsuri = GetURIForPrefix(parts[0], false);
                        if (nsuri == null)
                        {
                            UndeclaredNamespaceError(parts[0], "XTDE0860", "name");
                            return null;
                        }
                    }

                    INodeName attributeName = new FingerprintedQName(parts[0], nsuri, parts[1]);
                    attributeName.ObtainFingerprint(GetNamePool());
                    FixedAttribute instruction = new FixedAttribute(attributeName, validationAction, schemaType);
                    instruction.SetInstruction(true);
                    instruction.SetLocation(SaveLocation());
                    CompileContent(compilation, decl, instruction, separator);
                    return instruction;
                }
                else if (@namespace is StringLiteral && !NamespaceUri.XMLNS.Equals(NamespaceUri.Of(((StringLiteral)@namespace).Stringify())))
                {
                    // (the xmlns namespace is left to the instruction that computes the name: it is XTDE0865 when it runs)
                    UnicodeString nsuri = ((StringLiteral)@namespace).GetString();
                    if (nsuri.IsEmpty())
                    {
                        parts[0] = "";
                    }
                    else if (NamespaceUri.XML.Equals(NamespaceUri.Of(nsuri.ToString())))
                    {
                        // only the prefix xml names the XML namespace
                        parts[0] = "xml";
                    }
                    else if (parts[0].Equals(""))
                    {

                        // Need to choose an arbitrary prefix
                        // First see if the requested namespace is declared in the stylesheet
                        foreach (NamespaceBinding nb in AllNamespaces)
                        {
                            if (nb.GetNamespaceUri().ToUnicodeString().Equals(nsuri))
                            {
                                parts[0] = nb.GetPrefix();
                                break;
                            }
                        }


                        // Otherwise see if the URI is known to the namePool
                        if (parts[0].Equals(""))
                        {
                            string p = GetNamePool().SuggestPrefixForURI(NamespaceUri.Of(((StringLiteral)@namespace).Stringify()));
                            if (p != null)
                            {
                                parts[0] = p;
                            }
                        }


                        // Otherwise choose something arbitrary. This will get changed
                        // if it clashes with another attribute
                        if (parts[0].Equals(""))
                        {
                            parts[0] = "ns0";
                        }
                    }

                    INodeName nodeName = new FingerprintedQName(parts[0], NamespaceUri.Of(nsuri.ToString()), parts[1]);
                    nodeName.ObtainFingerprint(GetNamePool());
                    FixedAttribute fixedAtt = new FixedAttribute(nodeName, validationAction, schemaType);
                    fixedAtt.SetInstruction(true);
                    fixedAtt.SetLocation(SaveLocation());
                    CompileContent(compilation, decl, fixedAtt, separator);
                    return fixedAtt;
                }
            }

            ComputedAttribute inst = new ComputedAttribute(attributeName, @namespace, validationAction, schemaType, false);
            inst.SetInstruction(true);
            inst.SetLocation(SaveLocation());
            CompileContent(compilation, decl, inst, separator);
            return inst;
        }
    }
}
