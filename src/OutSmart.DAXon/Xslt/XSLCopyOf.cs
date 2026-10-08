////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:copy-of element in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLCopyOf : StyleElement
    {
        private Expression select;
        private bool copyNamespaces;
        private bool copyAccumulators;
        private int validation = Validation.PRESERVE;
        private ISchemaType schemaType;
        public override bool IsInstruction()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string selectAtt = null;
            string copyNamespacesAtt = null;
            string copyAccumulatorsAtt = null;
            string validationAtt = null;
            string typeAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                if (f.Equals("select"))
                {
                    selectAtt = value;
                    select = MakeExpression(selectAtt, att);
                }
                else if (f.Equals("copy-namespaces"))
                {
                    copyNamespacesAtt = Whitespace.Trim(value);
                }
                else if (f.Equals("copy-accumulators"))
                {
                    copyAccumulatorsAtt = Whitespace.Trim(value);
                }
                else if (f.Equals("validation"))
                {
                    validationAtt = Whitespace.Trim(value);
                }
                else if (f.Equals("type"))
                {
                    typeAtt = Whitespace.Trim(value);
                }
                else if (attName.GetLocalPart().Equals("read-once") && attName.HasURI(NamespaceUri.SAXON))
                {
                    CompileError("The saxon:read-once attribute is no longer available - use xsl:stream");
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (selectAtt == null)
            {
                ReportAbsence("select");
            }

            if (copyAccumulatorsAtt == null)
            {
                copyAccumulators = false;
            }
            else
            {
                copyAccumulators = ProcessBooleanAttribute("copy-accumulators", copyAccumulatorsAtt);
                if (copyAccumulators && IsConstructingComplexContent())
                {
                    IssueWarning("Copying accumulators is pointless when the copied element " + "is immediately attached to a new parent, since that action " + "will lose the accumulator values", DAXonErrorCode.SXWN9017);
                    copyAccumulators = false;
                }
            }

            if (copyNamespacesAtt == null)
            {
                copyNamespaces = true;
            }
            else
            {
                copyNamespaces = ProcessBooleanAttribute("copy-namespaces", copyNamespacesAtt);
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
                schemaType = GetSchemaType(typeAtt);
                if (!IsSchemaAware())
                {
                    CompileError("The @type attribute is available only with a schema-aware XSLT processor", "XTSE1660");
                }

                validation = Validation.BY_TYPE;
            }

            if (typeAtt != null && validationAtt != null)
            {
                CompileError("The @validation and @type attributes are mutually exclusive", "XTSE1505");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            CheckEmpty();
            select = TypeCheck("select", select);
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            CopyOf inst = new CopyOf(select, copyNamespaces, validation, schemaType, false);
            inst.SetCopyAccumulators(copyAccumulators);
            inst.SetLocation(SaveLocation());

            inst.SetSchemaAware(exec.IsSchemaAware());
            return inst;
        }
    }
}