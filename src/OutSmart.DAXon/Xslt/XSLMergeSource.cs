////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLMergeSource : StyleElement
    {
        private Expression forEachItem;
        private Expression forEachSource;
        private Expression select;
        private bool sortBeforeMerge;
        private int mergeKeyCount;
        private string sourceName;
        private int validationAction = Validation.STRIP;
        private ISchemaType schemaType;
        private bool streamable;
        private HashSet<Accumulator> accumulators = new HashSet<Accumulator>();

        public Expression Select => select;

        public string SourceName => sourceName;
        public override bool IsInstruction()
        {
            return false;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return false;
        }

        public bool IsSortBeforeMerge()
        {
            return sortBeforeMerge;
        }

        public MergeInstr.MergeSource MakeMergeSource(MergeInstr mi, Expression select)
        {
            MergeInstr.MergeSource ms = new MergeInstr.MergeSource(mi);
            if (forEachItem != null)
            {
                ms.InitForEachItem(mi, forEachItem);
            }

            if (forEachSource != null)
            {
                ms.InitForEachStream(mi, forEachSource);
            }

            if (select != null)
            {
                this.select = select;
                ms.InitRowSelect(mi, select);
            }

            ms.baseURI = GetBaseURI();
            ms.sourceName = sourceName;
            ms.validation = validationAction;
            ms.schemaType = schemaType;
            ms.SetStreamable(streamable);
            ms.accumulators = accumulators;
            return ms;
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            return null;
        }

        public override void PrepareAttributes()
        {
            string selectAtt = null;
            string forEachItemAtt = null;
            string forEachSourceAtt = null;
            string validationAtt = null;
            string typeAtt = null;
            string streamableAtt = null;
            string useAccumulatorsAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "for-each-item":
                        forEachItemAtt = value;
                        forEachItem = MakeExpression(forEachItemAtt, att);
                        break;
                    case "for-each-source":
                        forEachSourceAtt = value;
                        forEachSource = MakeExpression(forEachSourceAtt, att);
                        break;
                    case "select":
                        selectAtt = value;
                        select = MakeExpression(selectAtt, att);
                        break;
                    case "sort-before-merge":
                        sortBeforeMerge = ProcessBooleanAttribute("sort-before-merge", value);
                        break;
                    case "name":
                        string nameAtt = Whitespace.Trim(value);
                        if (NameChecker.IsValidNCName(nameAtt))
                        {
                            sourceName = nameAtt;
                        }
                        else
                        {
                            CompileError("xsl:merge-source/@name (" + nameAtt + ") is not a valid NCName", "XTSE0020");
                        }

                        break;
                    case "validation":
                        validationAtt = Whitespace.Trim(value);
                        break;
                    case "type":
                        typeAtt = Whitespace.Trim(value);
                        break;
                    case "streamable":
                        streamableAtt = value;
                        break;
                    case "use-accumulators":
                        useAccumulatorsAtt = Whitespace.Trim(value);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (sourceName == null)
            {
                try
                {
                    sourceName = "merge-source " + (Count.CountFn(IterateAxis(AxisInfo.PRECEDING_SIBLING, NodeKindTest.ELEMENT)) + 1);
                }
                catch (XPathException)
                {
                    sourceName = "merge-source " + GetHashCode();
                }
            }

            if (forEachItemAtt != null)
            {
                if (forEachSourceAtt != null)
                {
                    CompileError("The for-each-item and for-each-source attributes must not both be present", "XTSE3195");
                }
            }

            if (selectAtt == null)
            {
                ReportAbsence("select");
            }

            if (validationAtt == null)
            {
                validationAction = DefaultValidation;
            }
            else
            {
                validationAction = ValidateValidationAttribute(validationAtt);
            }

            if (typeAtt != null)
            {
                if (!IsSchemaAware())
                {
                    CompileError("The @type attribute is available only with a schema-aware XSLT processor", "XTSE1660");
                }

                schemaType = GetSchemaType(typeAtt);
                validationAction = Validation.BY_TYPE;
            }

            if (typeAtt != null && validationAtt != null)
            {
                CompileError("The @validation and @type attributes are mutually exclusive", "XTSE1505");
            }

            if ((typeAtt != null || validationAtt != null) && forEachSourceAtt == null)
            {
                CompileError("The @type and @validation attributes can be used only when @for-each-stream is specified", "XTSE0020");
            }

            if (streamableAtt != null)
            {
                streamable = ProcessStreamableAtt(streamableAtt);
                if (streamable && forEachSource == null)
                {
                    CompileError("Streaming on xsl:merge-source is possible only when @for-each-source is used", "XTSE3195");
                }
            }
            else if (forEachSource != null)
            {
                streamable = false;
            }

            if (useAccumulatorsAtt == null)
            {
                useAccumulatorsAtt = "";
            }

            AccumulatorRegistry registry = GetPrincipalStylesheetModule().GetStylesheetPackage().AccumulatorRegistry;
            accumulators = registry.GetUsedAccumulators(useAccumulatorsAtt, this);
        }

        public override void Validate(ComponentDeclaration decl)
        {
            forEachItem = TypeCheck("for-each-item", forEachItem);
            forEachSource = TypeCheck("for-each-source", forEachSource);
            select = TypeCheck("select", select);
            foreach (NodeInfo child in Children())
            {
                if (child is XSLMergeKey)
                {
                    mergeKeyCount++;
                }
                else if (child.GetNodeKind() == Types.Type.TEXT)
                {

                    // with xml:space=preserve, white space nodes may still be there
                    if (!Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {
                        CompileError("No character data is allowed within xsl:merge-source", "XTSE0010");
                    }
                }
                else if (child is StyleElement)
                {
                    ((StyleElement)child).CompileError("No children other than xsl:merge-key are allowed within xsl:merge-source", "XTSE0010");
                }
            }

            if (mergeKeyCount == 0)
            {
                CompileError("xsl:merge-source must have exactly at least one xsl:merge-key child element", "XTSE0010");
            }
        }
    }
}
