////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Numbering;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:number element in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLNumber : StyleElement
    {
        private int level;
        private Patterns.Pattern count;
        private Patterns.Pattern from;
        private Expression select;
        private Expression value;
        private Expression format;
        private Expression groupSize;
        private Expression groupSeparator;
        private Expression letterValue;
        private Expression lang;
        private Expression ordinal;
        private Expression startAt;
        private NumberFormatter formatter;
        public override bool IsInstruction()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string selectAtt = null;
            string valueAtt = null;
            string countAtt = null;
            string fromAtt = null;
            string levelAtt = null;
            string formatAtt = null;
            AttributeInfo gsizeAtt = null;
            AttributeInfo gsepAtt = null;
            string startAtAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string attValue = att.Value;
                switch (f)
                {
                    case "select":
                        selectAtt = attValue;
                        select = MakeExpression(selectAtt, att);
                        break;
                    case "value":
                        valueAtt = attValue;
                        this.value = MakeExpression(valueAtt, att);
                        break;
                    case "count":
                        countAtt = attValue;
                        break;
                    case "from":
                        fromAtt = attValue;
                        break;
                    case "level":
                        levelAtt = Whitespace.Trim(attValue);
                        break;
                    case "format":
                        formatAtt = attValue;
                        format = MakeAttributeValueTemplate(formatAtt, att);
                        break;
                    case "lang":
                        string langAtt = attValue;
                        lang = MakeAttributeValueTemplate(langAtt, att);
                        break;
                    case "letter-value":
                        string letterValueAtt = Whitespace.Trim(attValue);
                        letterValue = MakeAttributeValueTemplate(letterValueAtt, att);
                        break;
                    case "grouping-size":
                        gsizeAtt = att;
                        break;
                    case "grouping-separator":
                        gsepAtt = att;
                        break;
                    case "ordinal":
                        string ordinalAtt = attValue;
                        ordinal = MakeAttributeValueTemplate(ordinalAtt, att);
                        break;
                    case "start-at":
                        startAtAtt = attValue;
                        startAt = MakeAttributeValueTemplate(startAtAtt, att);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (valueAtt != null)
            {
                if (selectAtt != null)
                {
                    CompileError("The select attribute and value attribute must not both be present", "XTSE0975");
                }

                if (countAtt != null)
                {
                    CompileError("The count attribute and value attribute must not both be present", "XTSE0975");
                }

                if (fromAtt != null)
                {
                    CompileError("The from attribute and value attribute must not both be present", "XTSE0975");
                }

                if (levelAtt != null)
                {
                    CompileError("The level attribute and value attribute must not both be present", "XTSE0975");
                }
            }

            if (countAtt != null)
            {
                count = MakePattern(countAtt, "count");
            }

            if (fromAtt != null)
            {
                from = MakePattern(fromAtt, "from");
            }

            if (levelAtt == null)
            {
                level = NumberInstruction.SINGLE;
            }
            else if (levelAtt.Equals("single"))
            {
                level = NumberInstruction.SINGLE;
            }
            else if (levelAtt.Equals("multiple"))
            {
                level = NumberInstruction.MULTI;
            }
            else if (levelAtt.Equals("any"))
            {
                level = NumberInstruction.ANY;
            }
            else
            {
                InvalidAttribute("level", "single|any|multiple");
            }

            if (level == NumberInstruction.SINGLE && from == null && count == null)
            {
                level = NumberInstruction.SIMPLE;
            }

            if (formatAtt != null)
            {
                if (format is StringLiteral)
                {
                    formatter = new NumberFormatter();
                    formatter.Prepare(((StringLiteral)format).Stringify());
                } // else we'll need to allocate the formatter at run-time
            }
            else
            {
                formatter = new NumberFormatter();
                formatter.Prepare("1");
            }

            if (gsepAtt != null && gsizeAtt != null)
            {

                // the spec says that if only one is specified, it is ignored
                groupSize = MakeAttributeValueTemplate(gsizeAtt.Value, gsizeAtt);
                groupSeparator = MakeAttributeValueTemplate(gsepAtt.Value, gsepAtt);
            }

            if (startAtAtt != null)
            {
                if (startAtAtt.IndexOf('{') < 0 && !startAtAtt.MatchesRegex("-?[0-9]+(\\s+-?[0-9]+)*"))
                {
                    CompileErrorInAttribute("Invalid format for start-at attribute", "XTSE0020", "start-at");
                }
            }
            else
            {
                startAt = new StringLiteral("1");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            CheckEmpty();
            select = TypeCheck("select", select);
            value = TypeCheck("value", value);
            format = TypeCheck("format", format);
            groupSize = TypeCheck("group-size", groupSize);
            groupSeparator = TypeCheck("group-separator", groupSeparator);
            letterValue = TypeCheck("letter-value", letterValue);
            ordinal = TypeCheck("ordinal", ordinal);
            lang = TypeCheck("lang", lang);
            from = TypeCheck("from", from);
            count = TypeCheck("count", count);
            startAt = TypeCheck("start-at", startAt);
            string errorCode = "XTTE1000";
            if (value == null && select == null)
            {
                errorCode = "XTTE0990";
                ContextItemExpression implicitSelect = new ContextItemExpression();
                implicitSelect.SetLocation(AllocateLocation());
                implicitSelect.SetErrorCodeForUndefinedContext(errorCode, false);
                select = implicitSelect;
            }

            if (select != null)
            {
                try
                {
                    string errorCode1 = errorCode;
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:number/select", 0, errorCode1);
                    select = GetConfiguration().GetTypeChecker(false).StaticTypeCheck(select, SequenceType.SINGLE_NODE, role, MakeExpressionVisitor());
                }
                catch (XPathException err)
                {
                    CompileError(err);
                }
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            bool valueSpecified = value != null;
            if (value == null)
            {
                value = new NumberInstruction(select, level, count, from);
                value.SetLocation(AllocateLocation());
            }

            NumberSequenceFormatter numFormatter = new NumberSequenceFormatter(value, format, groupSize, groupSeparator, letterValue, ordinal, startAt, lang, formatter, XPath10ModeIsEnabled() && valueSpecified);
            numFormatter.SetLocation(AllocateLocation());
            ValueOf inst = new ValueOf(numFormatter, false, false);
            inst.SetLocation(AllocateLocation());
            inst.SetIsNumberingInstruction();
            return inst;
        }
    }
}
