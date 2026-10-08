////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions.Instructions;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:copy elements in stylesheet. <br>
    /// </summary>
    internal sealed class XSLCopy : StyleElement
    {
        private string use; // value of use-attribute-sets attribute
        private StructuredQName[] attributeSets;
        private bool copyNamespaces = true;
        private bool inheritNamespaces = true;
        private int validationAction = Validation.PRESERVE;
        private ISchemaType schemaType;
        private Expression select;
        private bool selectSpecified;
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
            string copyNamespacesAtt = null;
            string validationAtt = null;
            string typeAtt = null;
            string inheritAtt = null;
            AttributeInfo selectAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "use-attribute-sets":
                        use = value;
                        break;
                    case "copy-namespaces":
                        copyNamespacesAtt = Whitespace.Trim(value);
                        break;
                    case "select":
                        selectAtt = att;
                        break;
                    case "type":
                        typeAtt = Whitespace.Trim(value);
                        break;
                    case "validation":
                        validationAtt = Whitespace.Trim(value);
                        break;
                    case "inherit-namespaces":
                        inheritAtt = Whitespace.Trim(value);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (copyNamespacesAtt != null)
            {
                copyNamespaces = ProcessBooleanAttribute("copy-namespaces", copyNamespacesAtt);
            }

            if (typeAtt != null && validationAtt != null)
            {
                CompileError("The type and validation attributes must not both be specified", "XTSE1505");
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
                schemaType = GetSchemaType(typeAtt);
                if (!IsSchemaAware())
                {
                    CompileError("The @type attribute is available only with a schema-aware XSLT processor", "XTSE1660");
                }

                validationAction = Validation.BY_TYPE;
            }

            if (inheritAtt != null)
            {
                inheritNamespaces = ProcessBooleanAttribute("inherit-namespaces", inheritAtt);
            }

            if (selectAtt != null)
            {
                select = MakeExpression(selectAtt.Value, selectAtt);
                selectSpecified = true;
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (use != null)
            {

                // get the names of referenced attribute sets
                attributeSets = GetUsedAttributeSets(use);
            }

            if (select == null)
            {
                select = new ContextItemExpression();
                select.SetLocation(AllocateLocation());
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            select = TypeCheck("select", select);
            try
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:copy/select", 0, "XTTE3180");
                select = GetConfiguration().GetTypeChecker(false).StaticTypeCheck(select, SequenceType.OPTIONAL_ITEM, role, MakeExpressionVisitor());
            }
            catch (XPathException err)
            {
                CompileError(err);
            }

            Expression content = CompileSequenceConstructor(exec, decl, true);
            if (attributeSets != null)
            {
                Expression use = UseAttributeSet.MakeUseAttributeSets(attributeSets, this);

                // The use-attribute-sets is ignored unless the context item is an element node. So we
                // wrap the UseAttributeSet instructions in a conditional to perform a run-time test
                Expression condition = new InstanceOfExpression(new ContextItemExpression(), SequenceType.MakeSequenceType(NodeKindTest.ELEMENT, StaticProperty.EXACTLY_ONE));
                Expression choice = Choose.MakeConditional(condition, use);
                if (content == null)
                {
                    content = choice;
                }
                else
                {
                    content = Block.MakeBlock(choice, content);
                    content.SetLocation(AllocateLocation());
                }
            }

            if (content == null)
            {
                content = Literal.MakeEmptySequence();
            }

            CopyInstr inst = new CopyInstr(copyNamespaces, inheritNamespaces, schemaType, validationAction);
            inst.SetLocation(SaveLocation());
            inst.SetContentExpression(content);
            if (selectSpecified)
            {
                return new ForEach(select, inst);
            }
            else
            {
                return inst;
            }
        }
    }
}