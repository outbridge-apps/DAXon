////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Lib
{
    public class StandardDiagnostics
    {

        public int MAX_MESSAGE_LENGTH = 2000;
        public int MAX_MESSAGE_LINE_LENGTH = 100;
        public int MIN_MESSAGE_LINE_LENGTH = 10;
        public int TARGET_MESSAGE_LINE_LENGTH = 90;
        public StandardDiagnostics()
        {
        }

        public virtual string GetLocationMessageText(ILocation loc)
        {
            string locMessage = "";
            string systemId = null;
            NodeInfo node = null;
            string path;
            string nodeMessage = null;
            int lineNumber = -1;
            if (loc == null)
            {
                loc = Loc.NONE;
            }

            if (loc is XPathParser.NestedLocation)
            {
                loc = ((XPathParser.NestedLocation)loc).GetContainingLocation();
            }

            if (loc is AttributeLocation)
            {
                AttributeLocation saLoc = (AttributeLocation)loc;
                nodeMessage = "in " + (saLoc.ElementName == null ? "(unknown element)" : saLoc.ElementName.DisplayName); // runtime: GetElementName() can be null in this port (stubbed location plumbing); guard the sub-deref
                if (saLoc.AttributeName != null)
                {
                    nodeMessage += "/@" + saLoc.AttributeName;
                }

                nodeMessage += ' ';
            }
            else if (loc is DOMLocator)
            {
                nodeMessage = "at " + ((DOMLocator)loc).OriginatingNode.GetNodeName() + ' ';
            }
            else if (loc is NodeInfo)
            {
                node = (NodeInfo)loc;
                nodeMessage = "at " + node.DisplayName + ' ';
            }
            else if (loc is ValidationException && (node = ((ValidationException)loc).Node) != null)
            {
                nodeMessage = "at " + node.DisplayName + ' ';
            }
            else if (loc is ValidationException && loc.GetLineNumber() == -1 && (path = ((ValidationException)loc).GetPath()) != null)
            {
                nodeMessage = "at " + path + ' ';
            }
            else if (loc is Instruction)
            {
                string instructionName = GetInstructionName((Instruction)loc);
                if (instructionName != "")
                {
                    nodeMessage = "at " + instructionName + ' ';
                }

                systemId = loc.GetSystemId();
                lineNumber = loc.GetLineNumber();
            }
            else if (loc is Actor)
            {
                string kind = "procedure";
                if (loc is UserFunction)
                {
                    kind = "function";
                }
                else if (loc is NamedTemplate)
                {
                    kind = "template";
                }
                else if (loc is AttributeSet)
                {
                    kind = "attribute-set";
                }
                else if (loc is KeyDefinition)
                {
                    kind = "key";
                }
                else if (loc is GlobalParam)
                {
                    kind = "parameter";
                }
                else if (loc is GlobalVariable)
                {
                    kind = "variable";
                }
                else if (loc is Mode)
                {
                    kind = "mode";
                }

                systemId = loc.GetSystemId();
                lineNumber = loc.GetLineNumber();
                nodeMessage = "at " + kind + " ";
                StructuredQName name = ((Actor)loc).ComponentName;
                if (name != null)
                {
                    string n = name.ToString();
                    if (n.Equals("xsl:unnamed"))
                    {
                        n = "(unnamed)";
                    }

                    nodeMessage += n;
                    nodeMessage += " ";
                }
            }

            if (lineNumber == -1)
            {
                lineNumber = loc.GetLineNumber();
            }

            bool containsLineNumber = lineNumber > 0;
            if (node != null && !containsLineNumber)
            {
                nodeMessage = "at " + Navigator.GetPath(node) + ' ';
            }

            if (nodeMessage != null)
            {
                locMessage += nodeMessage;
            }

            if (containsLineNumber)
            {
                locMessage += "on line " + lineNumber + ' ';
                if (loc.GetColumnNumber() != -1)
                {
                    locMessage += "column " + loc.GetColumnNumber() + ' ';
                }
            }

            if (systemId != null && (systemId.Length == 0))
            {
                systemId = null;
            }

            if (systemId == null)
            {
                try
                {
                    systemId = loc.GetSystemId();
                }
                catch (Exception err)
                {
                    err.ToString(); // no action (can fail with NPE if the expression tree is corrupt)
                }
            }

            if (systemId != null && !(systemId.Length == 0))
            {
                locMessage += (containsLineNumber ? "of " : "in ") + AbbreviateLocationURI(systemId) + ':';
            }

            return locMessage;
        }

        public virtual string GetInstructionName(Instruction inst)
        {
            return GetInstructionNameDefault(inst);
        }

        public static string GetInstructionNameDefault(Instruction inst)
        {
            try
            {
                if (inst is FixedElement)
                {
                    StructuredQName qName = inst.GetObjectName();
                    return "element constructor <" + qName.DisplayName + '>';
                }
                else if (inst is FixedAttribute)
                {
                    StructuredQName qName = inst.GetObjectName();
                    return "attribute constructor " + qName.DisplayName + "=\"{...}\"";
                }

                int construct = inst.InstructionNameCode;
                if (construct < 0)
                {
                    return "";
                }

                if (construct < 1024 && construct != StandardNames.XSL_FUNCTION && construct != StandardNames.XSL_TEMPLATE)
                {

                    // it's a standard name
                    if (inst.GetPackageData().IsXSLT())
                    {
                        return StandardNames.GetDisplayName(construct);
                    }
                    else
                    {
                        string s = StandardNames.GetDisplayName(construct);
                        int colon = s.IndexOf(':');
                        if (colon > 0)
                        {
                            string local = s.Substring(colon + 1);
                            if (local.Equals("document"))
                            {
                                return "document node constructor";
                            }
                            else if (local.Equals("text") || s.Equals("value-of"))
                            {
                                return "text node constructor";
                            }
                            else if (local.Equals("element"))
                            {
                                return "computed element constructor";
                            }
                            else if (local.Equals("attribute"))
                            {
                                return "computed attribute constructor";
                            }
                            else if (local.Equals("variable"))
                            {
                                return "variable declaration";
                            }
                            else if (local.Equals("param"))
                            {
                                return "external variable declaration";
                            }
                            else if (local.Equals("comment"))
                            {
                                return "comment constructor";
                            }
                            else if (local.Equals("processing-instruction"))
                            {
                                return "processing-instruction constructor";
                            }
                            else if (local.Equals("namespace"))
                            {
                                return "namespace node constructor";
                            }
                        }

                        return s;
                    }
                }
                else
                {
                    return "";
                }
            }
            catch (Exception err)
            {
                return "";
            }
        }

        public virtual void LogStackTrace(IXPathContext context, Logger @out, int level)
        {
            if (level > 0)
            {
                int depth = 20;
                while (depth-- > 0 && context != null)
                {
                    IContextOriginator originator = context is XPathContextMajor ? ((XPathContextMajor)context).Origin : null;
                    Component component = context.GetCurrentComponent();
                    if (originator is Closure && ((Closure)originator).GetExpression() != null)
                    {
                        Expression expr = ((Closure)originator).GetExpression();
                        @out.Error("During lazy evaluation of " + expr.ToShortString() + " on line " + expr.GetLocation().GetLineNumber() + " of " + expr.GetLocation().GetSystemId());
                    }
                    else if (component != null)
                    {
                        if (component.GetActor() is Mode)
                        {
                            Rule rule = context.GetCurrentTemplateRule();
                            if (rule != null)
                            {
                                StringBuilder sb = new StringBuilder();
                                ILocation loc = rule.Pattern.GetLocation();
                                sb.Append("  In template rule with match=\"").Append(rule.Pattern.ToShortString()).Append("\" ");
                                if (loc != null && loc.GetLineNumber() != -1)
                                {
                                    sb.Append("on line ").Append(loc.GetLineNumber()).Append(' ');
                                }

                                if (loc != null && loc.GetSystemId() != null)
                                {
                                    sb.Append("of ").Append(AbbreviateLocationURI(loc.GetSystemId()));
                                }

                                @out.Error(sb.ToString());
                            }
                        }
                        else
                        {
                            @out.Error(GetLocationMessageText(component.GetActor()).ReplaceFirstRegex("^at ","In "));
                        }
                    }

                    try
                    {
                        context.GetStackFrame().GetStackFrameMap().ShowStackFrame(context, @out);
                    }
                    catch (Exception e)
                    {
                    }

                    context = context.MajorContext;
                    if (originator is Controller)
                    {
                        return;
                    }
                    else if (originator != null && !(originator is Closure))
                    {
                        @out.Error("     invoked by " + ShowOriginator(originator));
                    }

                    context = context.GetCaller();
                }
            }
        }

        protected virtual string ShowOriginator(IContextOriginator originator)
        {
            StringBuilder sb = new StringBuilder();
            if (originator == null)
            {
                sb.Append("unknown caller (null)");
            }
            else if (originator is Instruction)
            {
                sb.Append(GetInstructionName((Instruction)originator));
                if (originator is CallTemplate && ((CallTemplate)originator).UsesTailRecursion())
                {
                    sb.Append(" (tail calls omitted)");
                }

                if (originator is ApplyTemplates && ((ApplyTemplates)originator).UseTailRecursion())
                {
                    sb.Append(" (tail calls omitted)");
                }
            }
            else if (originator is UserFunctionCall)
            {
                sb.Append("function call");
            }
            else if (originator is Controller)
            {
                sb.Append("external application");
            }
            else if (originator is IBuiltInRuleSet)
            {
                sb.Append("built-in template rule (").Append(((IBuiltInRuleSet)originator).Name).Append(')');
            }
            else if (originator is KeyDefinition)
            {
                sb.Append("xsl:key definition");
            }
            else if (originator is GlobalParam)
            {
                sb.Append("global parameter ").Append(((GlobalParam)originator).GetVariableQName().DisplayName);
            }
            else if (originator is GlobalVariable)
            {
                sb.Append(((GlobalVariable)originator).Description);
            }
            else if (originator is MemoClosure)
            {
                Expression expr = ((MemoClosure)originator).GetExpression();
                if (expr == null)
                {
                    sb.Append("lazy evaluation of expression");
                }
                else
                {
                    sb.Append("lazy evaluation of ").Append(expr.ToShortString()).Append(" on line ").Append(expr.GetLocation().GetLineNumber());
                }
            }
            else if (originator is SingletonClosure)
            {
                Expression expr = ((SingletonClosure)originator).GetExpression();
                if (expr == null)
                {
                    sb.Append("lazy evaluation of singleton expression");
                }
                else
                {
                    sb.Append("lazy evaluation of ").Append(expr.ToShortString()).Append(" on line ").Append(expr.GetLocation().GetLineNumber());
                }
            }
            else
            {
                sb.Append("unknown caller (").Append(originator.GetType()).Append(')');
            }

            if (originator is ILocatable)
            {
                ILocation loc = ((ILocatable)originator).GetLocation();
                if (loc.GetLineNumber() != -1)
                {
                    sb.Append(" at ").Append(loc.GetSystemId() == null ? "line " : (loc.GetSystemId() + "#"));
                    sb.Append(loc.GetLineNumber());
                }
            }

            return sb.ToString();
        }

        protected virtual string FormatListOfOffendingNodes(ValidationFailure failure)
        {
            StringBuilder message = new StringBuilder();
            IList<NodeInfo> offendingNodes = failure.OffendingNodes;
            if (offendingNodes.Count > 0)
            {
                message.Append("\n  Nodes for which the assertion fails:");
                foreach (NodeInfo offender in offendingNodes)
                {
                    string nodeDesc = Types.Type.DisplayTypeName(offender);
                    if (offender.GetNodeKind() == Types.Type.TEXT)
                    {
                        nodeDesc += " " + Err.Wrap(offender.UnicodeStringValue, Err.VALUE);
                    }

                    if (offender.GetLineNumber() != -1)
                    {
                        nodeDesc += " on line " + offender.GetLineNumber();
                        if (offender.GetColumnNumber() != -1)
                        {
                            nodeDesc += " column " + offender.GetColumnNumber();
                        }

                        if (offender.GetSystemId() != null)
                        {
                            nodeDesc += " of " + offender.GetSystemId();
                        }
                    }
                    else
                    {
                        nodeDesc += " at " + Navigator.GetPath(offender);
                    }

                    message.Append("\n  * ").Append(nodeDesc);
                }
            }

            return message.ToString();
        }

        public virtual string AbbreviateLocationURI(string uri)
        {
            return AbbreviateLocationURIDefault(uri);
        }

        public static string AbbreviateLocationURIDefault(string uri)
        {
            if (uri == null)
            {
                return "*unknown*";
            }

            int slash = uri.LastIndexOf('/');
            if (slash >= 0 && slash < uri.Length - 1)
            {
                return uri.Substring(slash + 1);
            }
            else
            {
                return uri;
            }
        }
        public virtual string WordWrap(string message)
        {
            if (message.Length > MAX_MESSAGE_LENGTH)
            {
                message = message.Substring(0, MAX_MESSAGE_LENGTH);
            }

            int nl = message.IndexOf('\n');
            if (nl < 0)
            {
                nl = message.Length;
            }

            if (nl > MAX_MESSAGE_LINE_LENGTH)
            {
                int i = TARGET_MESSAGE_LINE_LENGTH;
                while (message[i] != ' ' && i > 0)
                {
                    i--;
                }

                if (i > MIN_MESSAGE_LINE_LENGTH)
                {
                    return message.Substring(0, i) + "\n  " + WordWrap(message.Substring(i + 1));
                }
                else
                {
                    return message;
                }
            }
            else if (nl < message.Length)
            {
                return message.Substring(0, nl) + '\n' + WordWrap(message.Substring(nl + 1));
            }
            else
            {
                return message;
            }
        }

        public virtual string ExpandSpecialCharacters(string @in, int threshold)
        {
            if (threshold >= UTF16CharacterSet.NONBMP_MAX)
            {
                return @in;
            }

            StringValue str = new StringValue(@in);
            StringBuilder fsb = new StringBuilder(str.Length32() * 2);
            IIntIterator iter = str.CodePoints();
            while (iter.MoveNext())
            {
                int ch = iter.Current;
                fsb.AppendCodePoint(ch);
                if (ch > threshold)
                {
                    fsb.Append("[x");
                    fsb.Append((ch).ToString("x", CultureInfo.InvariantCulture));
                    fsb.Append(']');
                }
            }

            return fsb.ToString();
        }
    }
}