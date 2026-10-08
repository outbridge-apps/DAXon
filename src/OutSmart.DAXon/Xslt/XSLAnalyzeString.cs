////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:analyze-string elements in the stylesheet. New at XSLT 2.0<BR>
    /// </summary>
    internal sealed class XSLAnalyzeString : StyleElement
    {
        private Expression select;
        private Expression regex;
        private Expression flags;
        private XSLMatchingSubstring matching;
        private XSLMatchingSubstring nonMatching;
        private IRegularExpression pattern;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainFallback()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string selectAtt = null;
            string regexAtt = null;
            string flagsAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "regex":
                        regexAtt = value;
                        regex = MakeAttributeValueTemplate(regexAtt, att);
                        break;
                    case "select":
                        selectAtt = value;
                        select = MakeExpression(selectAtt, att);
                        break;
                    case "flags":
                        flagsAtt = value; // not trimmed, see bugzilla 4315
                        flags = MakeAttributeValueTemplate(flagsAtt, att);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (selectAtt == null)
            {
                ReportAbsence("select");
                select = MakeExpression(".", null); // for error recovery
            }

            if (regexAtt == null)
            {
                ReportAbsence("regex");
                regex = MakeAttributeValueTemplate("xxx", null); // for error recovery
            }

            if (flagsAtt == null)
            {
                flagsAtt = "";
                flags = MakeAttributeValueTemplate("", null);
            }

            if (regex is StringLiteral && flags is StringLiteral)
            {
                try
                {
                    UnicodeString regex = ((StringLiteral)this.regex).GetString();
                    string flagstr = ((StringLiteral)flags).Stringify();
                    IList<string> warnings = new List<string>();
                    pattern = GetConfiguration().CompileRegularExpression(regex, flagstr, EffectiveVersion >= 30 ? "XP30" : "XP20", warnings);
                    foreach (string w in warnings)
                    {
                        IssueWarning(w, DAXonErrorCode.SXWN9022);
                    }
                }
                catch (XPathException err)
                {
                    if (err.HasErrorCode("FORX0001"))
                    {
                        InvalidFlags("Error in regular expression flags: " + err.Message);
                    }
                    else
                    {
                        InvalidRegex("Error in regular expression: " + err.Message);
                    }
                }
            }
        }

        private void InvalidRegex(string message)
        {
            CompileErrorInAttribute(message, "XTDE1140", "regex");

            // prevent it being reported more than once
            SetDummyRegex();
        }

        private void InvalidFlags(string message)
        {
            CompileErrorInAttribute(message, "XTDE1145", "flags");

            // prevent it being reported more than once
            SetDummyRegex();
        }

        private void SetDummyRegex()
        {
            try
            {
                pattern = GetConfiguration().CompileRegularExpression(BMPString.Of("x"), "", "XP20", null);
            }
            catch (XPathException err)
            {
                throw new InvalidOperationException();
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {

            bool foundFallback = false;
            foreach (NodeInfo curr in Children())
            {
                if (curr is XSLFallback)
                {
                    foundFallback = true;
                }
                else if (curr is XSLMatchingSubstring)
                {
                    bool b = curr.GetLocalPart().Equals("matching-substring");
                    if (b)
                    {
                        if (matching != null || nonMatching != null || foundFallback)
                        {
                            CompileError("xsl:matching-substring element must come first", "XTSE0010");
                        }

                        matching = (XSLMatchingSubstring)curr;
                    }
                    else
                    {
                        if (nonMatching != null || foundFallback)
                        {
                            CompileError("xsl:non-matching-substring cannot appear here", "XTSE0010");
                        }

                        nonMatching = (XSLMatchingSubstring)curr;
                    }
                }
                else
                {
                    CompileError("Only xsl:matching-substring and xsl:non-matching-substring are allowed here", "XTSE0010");
                }
            }

            if (matching == null && nonMatching == null)
            {
                CompileError("At least one xsl:matching-substring or xsl:non-matching-substring element must be present", "XTSE1130");
            }

            select = TypeCheck("select", select);
            regex = TypeCheck("regex", regex);
            flags = TypeCheck("flags", flags);
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            Expression matchingBlock = null;
            if (matching != null)
            {
                matchingBlock = matching.GetSelectExpression();
                if (matchingBlock == null)
                {
                    matchingBlock = matching.CompileSequenceConstructor(exec, decl, false);
                }
            }

            Expression nonMatchingBlock = null;
            if (nonMatching != null)
            {
                nonMatchingBlock = nonMatching.GetSelectExpression();
                if (nonMatchingBlock == null)
                {
                    nonMatchingBlock = nonMatching.CompileSequenceConstructor(exec, decl, false);
                }
            }

            try
            {
                return new AnalyzeString(select, regex, flags, matchingBlock == null ? null : matchingBlock.Simplify(), nonMatchingBlock == null ? null : nonMatchingBlock.Simplify(), pattern).WithLocation(SaveLocation());
            }
            catch (XPathException e)
            {
                CompileError(e);
                return null;
            }
        }
    }
}