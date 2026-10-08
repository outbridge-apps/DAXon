////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using System;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLForEachGroup : StyleElement
    {
        private Expression select = null;
        private Expression groupBy = null;
        private Expression groupAdjacent = null;
        private Expression splitWhen = null;
        private Patterns.Pattern starting = null;
        private Patterns.Pattern ending = null;
        private Expression collationName;
        private bool composite = false;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool IsPermittedChild(StyleElement child)
        {
            return child is XSLSort;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string groupByAtt = null;
            string groupAdjacentAtt = null;
            string startingAtt = null;
            string endingAtt = null;
            string splitWhenAtt = null;
            string collationAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "select":
                        select = MakeExpression(value, att);
                        break;
                    case "group-by":
                        groupByAtt = value;
                        groupBy = MakeExpression(groupByAtt, att);
                        break;
                    case "group-adjacent":
                        groupAdjacentAtt = value;
                        groupAdjacent = MakeExpression(groupAdjacentAtt, att);
                        break;
                    case "group-starting-with":
                        startingAtt = value;
                        break;
                    case "group-ending-with":
                        endingAtt = value;
                        break;
                    case "break-when":

                        // TODO: drop this, it was renamed split-when
                        if (RequireXslt40Attribute("break-when"))
                        {
                            splitWhenAtt = "function($group as item()*, $next as item()) as Q{http://www.w3.org/2001/XMLSchema}boolean " + "{ Q{http://www.w3.org/2005/xpath-functions}boolean(" + value + ") }";

                            splitWhen = MakeExpression(splitWhenAtt, att);
                        }

                        break;
                    case "split-when":

                        // 4.0 extension
                        if (RequireXslt40Attribute("split-when"))
                        {
                            splitWhenAtt = "function($group as item()*, $next as item()) as Q{http://www.w3.org/2001/XMLSchema}boolean " + "{ Q{http://www.w3.org/2005/xpath-functions}boolean(" + value + ") }";

                            splitWhen = MakeExpression(splitWhenAtt, att);
                        }

                        break;
                    case "collation":
                        collationAtt = Whitespace.Trim(value);
                        collationName = MakeAttributeValueTemplate(collationAtt, att);
                        break;
                    case "composite":
                        composite = ProcessBooleanAttribute("composite", value);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (select == null)
            {
                select = Literal.MakeEmptySequence();
                ReportAbsence("select");
            }

            int c = (groupByAtt == null ? 0 : 1) + (groupAdjacentAtt == null ? 0 : 1) + (startingAtt == null ? 0 : 1) + (endingAtt == null ? 0 : 1) + (splitWhenAtt == null ? 0 : 1);
            if (c != 1)
            {
                CompileError("Exactly one of the attributes group-by, group-adjacent, group-starting-with, " + "and group-ending-with must be specified", "XTSE1080"); //TODO: add break-when when it becomes mainstream
            }

            if (startingAtt != null)
            {
                starting = MakePattern(startingAtt, "group-starting-with");
            }

            if (endingAtt != null)
            {
                ending = MakePattern(endingAtt, "group-ending-with");
            }

            if (collationAtt != null)
            {
                if (groupBy == null && groupAdjacent == null)
                {
                    CompileError("A collation may be specified only if group-by or group-adjacent is specified", "XTSE1090");
                }
                else
                {
                    if (collationName is StringLiteral)
                    {
                        string collation = ((StringLiteral)collationName).Stringify();
                        URI collationURI;
                        try
                        {
                            collationURI = new URI(collation);
                            if (!collationURI.IsAbsolute())
                            {
                                URI @base = new URI(GetBaseURI());
                                collationURI = @base.Resolve(collationURI);
                                collationName = new StringLiteral(collationURI.ToString());
                            }
                        }
                        catch (URISyntaxException)
                        {
                            CompileError("Collation name '" + collationName + "' is not a valid URI", "XTDE1110");
                            collationName = new StringLiteral(NamespaceConstant.CODEPOINT_COLLATION_URI);
                        }
                    }
                }
            }
            else
            {
                string defaultCollation = GetDefaultCollationName();
                if (defaultCollation != null)
                {
                    collationName = new StringLiteral(defaultCollation);
                }
            }

            if (composite && (starting != null || ending != null))
            {
                CompileError("The composite attribute cannot be used with " + (starting == null ? "grouping-ending-with" : "group-starting-with"), "XTSE1090");
            }
        }

        //                case "array":
        //                    requireXslt40("array");
        //                    select = arrayToSequence(makeExpression(value, att));
        //                case "map":
        //                    requireXslt40("map");
        //                    select = mapToSequence(makeExpression(value, att));
        public override void Validate(ComponentDeclaration decl)
        {
            CheckSortComesFirst(false);
            TypeChecker tc = GetConfiguration().GetTypeChecker(false);
            select = TypeCheck("select", select);
            ExpressionVisitor visitor = MakeExpressionVisitor();
            if (groupBy != null)
            {
                groupBy = TypeCheck("group-by", groupBy);
                try
                {
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:for-each-group/group-by", 0);
                    groupBy = tc.StaticTypeCheck(groupBy, SequenceType.ATOMIC_SEQUENCE, role, visitor);
                }
                catch (XPathException err)
                {
                    CompileError(err);
                }
            }
            else if (groupAdjacent != null)
            {
                groupAdjacent = TypeCheck("group-adjacent", groupAdjacent);
                try
                {
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:for-each-group/group-adjacent", 0, "XTTE1100");
                    groupAdjacent = tc.StaticTypeCheck(groupAdjacent, composite ? SequenceType.ATOMIC_SEQUENCE : SequenceType.SINGLE_ATOMIC, role, visitor);
                }
                catch (XPathException err)
                {
                    CompileError(err);
                }
            }
            else if (splitWhen != null)
            {
                splitWhen = TypeCheck("break-when", splitWhen);
                try
                {
                    SpecificFunctionType breakWhenType = new SpecificFunctionType(new SequenceType[] { SequenceType.ANY_SEQUENCE, SequenceType.SINGLE_ITEM }, SequenceType.SINGLE_BOOLEAN);
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:for-each-group/break-when", 0, "XTTE1100");
                    splitWhen = tc.StaticTypeCheck(splitWhen, SequenceType.MakeSequenceType(breakWhenType, StaticProperty.EXACTLY_ONE), role, visitor);
                }
                catch (XPathException err)
                {
                    CompileError(err);
                }
            }

            starting = TypeCheck("group-starting-with", starting);
            ending = TypeCheck("group-ending-with", ending);
            if ((starting != null || ending != null) && visitor.StaticContext.GetXPathVersion() < 30)
            {
                try
                {
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:for-each-group/select", 0, "XTTE1120");
                    select = tc.StaticTypeCheck(select, SequenceType.NODE_SEQUENCE, role, visitor);
                }
                catch (XPathException err)
                {
                    string prefix = starting != null ? "With group-starting-with attribute: " : "With group-ending-with attribute: ";
                    CompileError(prefix + err.Message, err.ErrorCodeQName);
                }
            }

            if (!HasChildNodes())
            {
                IssueWarning("An empty xsl:for-each-group instruction has no effect", DAXonErrorCode.SXWN9009);
            }
        }

        public override Expression Compile(Compilation compilation, ComponentDeclaration decl)
        {
            IStringCollator collator = null;
            if (collationName is StringLiteral)
            {

                // if the collation name is constant, then we've already resolved it against the base URI
                string uri = ((StringLiteral)collationName).Stringify();
                try
                {
                    collator = FindCollation(uri, GetBaseURI());
                }
                catch (XPathException err)
                {
                    CompileError("Failed to load collation " + uri + ": " + err.Message, "XTDE1110");
                    collator = CodepointCollator.GetInstance(); // for recovery paths
                }

                if (collator == null)
                {
                    CompileError("The collation name '" + uri + "' has not been defined", "XTDE1110");
                    collator = CodepointCollator.GetInstance();
                }
            }

            byte algorithm = ForEachGroup.GROUP_BY;
            Expression key = null;
            if (groupBy != null)
            {
                algorithm = ForEachGroup.GROUP_BY;
                key = groupBy;
            }
            else if (groupAdjacent != null)
            {
                algorithm = ForEachGroup.GROUP_ADJACENT;
                key = groupAdjacent;
            }
            else if (starting != null)
            {
                algorithm = ForEachGroup.GROUP_STARTING;
                key = starting;
            }
            else if (ending != null)
            {
                algorithm = ForEachGroup.GROUP_ENDING;
                key = ending;
            }
            else if (splitWhen != null)
            {
                algorithm = ForEachGroup.GROUP_SPLIT_WHEN;
                key = splitWhen;
            }

            Expression action = CompileSequenceConstructor(compilation, decl, true);
            if (action == null)
            {

                // body of for-each is empty: it's a no-op.
                return Literal.MakeEmptySequence();
            }

            try
            {
                ForEachGroup instr = new ForEachGroup(select, action.Simplify(), algorithm, key, collator, collationName, MakeSortKeys(compilation, decl));
                instr.SetIsInFork(GetParent().Fingerprint == StandardNames.XSL_FORK);
                instr.SetComposite(composite);
                instr.SetLocation(SaveLocation());
                return instr;
            }
            catch (XPathException e)
            {
                CompileError(e);
                return null;
            }
        }
    }
}
