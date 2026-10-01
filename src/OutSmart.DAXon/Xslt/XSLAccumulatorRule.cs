////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:accumulator-rule elements in a stylesheet (XSLT 3.0).
    /// </summary>
    internal sealed class XSLAccumulatorRule : StyleElement
    {
        private Patterns.Pattern match;
        private bool postDescent;
        private Expression select;
        private bool capture;

        public Patterns.Pattern Match
        {
            get => match; set
            {
                this.match = value;
            }
        }
        public override void PrepareAttributes()
        {
            string matchAtt = null;
            string newValueAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                if (attName.GetNamespaceUri().IsEmpty())
                {
                    switch (f)
                    {
                        case "match":
                            matchAtt = value;
                            break;
                        case "select":
                            newValueAtt = value;
                            select = MakeExpression(newValueAtt, att);
                            break;
                        case "phase":
                            string phaseAtt = Whitespace.Trim(value);
                            if ("start".Equals(phaseAtt))
                            {
                                postDescent = false;
                            }
                            else if ("end".Equals(phaseAtt))
                            {
                                postDescent = true;
                            }
                            else
                            {
                                postDescent = true;
                                CompileError("phase must be 'start' or 'end'", "XTSE0020");
                            }

                            break;
                        case "capture":
                            RequireXslt40Attribute("capture");
                            capture = ProcessBooleanAttribute("capture", value);
                            break;
                        default:
                            CheckUnknownAttribute(attName);
                            break;
                    }
                }
                else if (attName.HasURI(NamespaceUri.SAXON))
                {
                    if (IsExtensionAttributeAllowed(attName.DisplayName))
                    {
                        if (attName.GetLocalPart().Equals("capture"))
                        {
                            capture = ProcessBooleanAttribute("saxon:capture", value);
                        }
                    }
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (matchAtt == null)
            {
                ReportAbsence("match");
                matchAtt = "non-existent-element";
            }

            match = MakePattern(matchAtt, "match");
            if (capture && !postDescent)
            {
                CompileErrorInAttribute("capture='yes' is not allowed on an accumulator rule with phase='start'", "XTSE3355", "capture");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            select = TypeCheck("select", select);
            match = TypeCheck("match", match);
            if (select != null && HasChildNodes())
            {
                CompileError("If the xsl:accumulator-rule element has a select attribute then it must have no children");
            }
        }

        public Expression GetNewValueExpression(Compilation compilation, ComponentDeclaration decl)
        {
            if (select == null)
            {
                select = CompileSequenceConstructor(compilation, decl, true);
            }

            return select;
        }

        public bool IsPostDescent()
        {
            return postDescent;
        }

        public bool IsCapture()
        {
            return capture;
        }

        protected override SourceBinding HasImplicitBinding(StructuredQName variableName, StructuredQName attributeName)
        {
            if (variableName.GetLocalPart().Equals("value") && variableName.HasURI(NamespaceUri.NULL) && (attributeName == null || (attributeName.GetLocalPart().Equals("select") && attributeName.HasURI(NamespaceUri.NULL))))
            {
                SourceBinding sb = new SourceBinding(this);
                sb.VariableQName = NamespaceUri.NULL.QName("value");
                sb.DeclaredType = ((XSLAccumulator)GetParent()).ResultType;
                sb.SetProperty(SourceBinding.BindingProperty.IMPLICITLY_DECLARED, true);
                return sb;
            }
            else
            {
                return null;
            }
        }
    }
}
