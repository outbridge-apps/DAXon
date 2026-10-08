////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Text;
namespace OutSmart.DAXon.Tracing
{
    internal abstract class AbstractTraceListener : StandardDiagnostics, ITraceListener
    {
        protected int indent = 0;
        protected int detail = TraceLevel.NORMAL;
        protected Logger @out = new StandardLogger();
        private readonly Stack<object> stack = new Stack<object>();

        /// <summary>
        /// Called at start of a transformation
        /// </summary>
        protected virtual string OpeningAttributes => "";

        /// <summary>
        /// Called at start of a transformation
        /// </summary>
        public void Open(Controller controller)
        {
            @out.Info(Spaces(indent++) + "<trace " + "saxon-version=\"" + Core.Version.ProductVersion + "\" " + OpeningAttributes + '>');
        }

        /// <summary>
        /// Called at end of a transformation
        /// </summary>
        public void Close()
        {
            @out.Info(Spaces(indent--) + "</trace>");
        }

        // Abort-path release: the Logger is externally owned, nothing to free.
        public virtual void Dispose()
        {
        }

        public void Enter(ITraceable info, Dictionary<string, object> properties, IXPathContext context)
        {
            if (IsApplicable(info))
            {
                stack.Push(info);
                ILocation loc = GetLocation(info);
                string file = AbbreviateLocationURI(loc.GetSystemId());
                string elementTag = Tag(info);
                StringBuilder msg = new StringBuilder(AbstractTraceListener.Spaces(indent) + '<' + elementTag);
                if (info is Expression && !((Expression)info).IsInstruction() && !properties.ContainsKey("expr"))
                {
                    properties["expr"] = ((Expression)info).ToShortString();
                }

                foreach (KeyValuePair<string, object> entry in properties)
                {
                    object val = entry.Value;
                    if (val is StructuredQName)
                    {
                        val = ((StructuredQName)val).DisplayName;
                    }
                    else if (val is StringValue)
                    {
                        val = ((StringValue)val).UnicodeStringValue;
                    }

                    if (val != null)
                    {
                        msg.Append(' ').Append(entry.Key).Append('=').Append(Escape(val.ToString()));
                    }
                }

                msg.Append(" line=\"").Append(loc.GetLineNumber()).Append('"');
                int col = loc.GetColumnNumber();
                if (col >= 0)
                {
                    msg.Append(" column=\"").Append(loc.GetColumnNumber()).Append('"');
                }

                msg.Append(" module=").Append(Escape(file));
                msg.Append('>');
                @out.Info(msg.ToString());
                indent++;
            }
        }

        public static ILocation GetLocation(ITraceable info)
        {
            ILocation rawLocation = info.GetLocation();
            if (rawLocation is XPathParser.NestedLocation)
            {
                ILocation container = ((XPathParser.NestedLocation)rawLocation).GetContainingLocation();
                if (container is AttributeLocation)
                {
                    return container;
                }
            }

            return rawLocation;
        }

        public virtual string Escape(string @in)
        {
            if (@in == null)
            {
                return "\"\"";
            }

            char quot = @in.Contains("\"") ? '\'' : '"';
            string collapsed = Whitespace.CollapseWhitespace(@in);
            StringBuilder sb = new StringBuilder(collapsed.Length + 10).Append(quot);
            for (int i = 0; i < collapsed.Length; i++)
            {
                char c = collapsed[i];
                if (c == '<')
                {
                    sb.Append("&lt;");
                }
                else if (c == '>')
                {
                    sb.Append("&gt;");
                }
                else if (c == '&')
                {
                    sb.Append("&amp;");
                }
                else if (c == quot)
                {
                    sb.Append(quot == '"' ? "&quot;" : "&apos;");
                }
                else if (c == '\n')
                {
                    sb.Append("&#xA;");
                }
                else if (c == '\r')
                {
                    sb.Append("&#xD;");
                }
                else if (c == '\t')
                {
                    sb.Append("&#x9;");
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.Append(quot).ToString();
        }

        public void Leave(ITraceable info)
        {
            if (IsApplicable(info))
            {
                stack.Pop();
                indent--;
                @out.Info(AbstractTraceListener.Spaces(indent) + "</" + Tag(info) + '>');
            }
        }

        protected virtual bool IsApplicable(ITraceable info)
        {
            return Level(info) <= detail;
        }

        protected virtual string Tag(ITraceable info)
        {
            return "expr";
        }

        protected virtual int Level(ITraceable info)
        {
            if (info is ITraceableComponent || info is ApplyTemplates || info is CallTemplate)
            {
                return 1;
            }

            if (info is Expression && (((Expression)info).IsInstruction() || ((Expression)info).IsCallOn(typeof(TransformFn))))
            {
                return 2;
            }

            return 3;
        }

        /// <summary>
        /// Called when an item becomes the context item
        /// </summary>
        public void StartCurrentItem(IItem item)
        {
            if (item is NodeInfo && detail > 0)
            {
                stack.Push(item);
                NodeInfo curr = (NodeInfo)item;
                @out.Info(AbstractTraceListener.Spaces(indent) + "<source node=\"" + Navigator.GetPath(curr) + "\" line=\"" + curr.GetLineNumber() + "\" file=\"" + AbbreviateLocationURI(curr.GetSystemId()) + "\">");
            }

            indent++;
        }

        public void EndCurrentItem(IItem item)
        {
            indent--;
            if (item is NodeInfo && detail > 0)
            {
                NodeInfo curr = (NodeInfo)item;
                @out.Info(AbstractTraceListener.Spaces(indent) + "</source><!-- " + Navigator.GetPath(curr) + " -->");
                stack.Pop();
            }
        }

        protected static string Spaces(int n)
        {
            // The Java static self-appending StringBuffer is synchronized; the ported
            // StringBuilder was not (torn state under concurrent tracers) and pinned its
            // high-water length for the life of the process. Substring allocated anyway.
            return new string(' ', Math.Max(n, 0));
        }

        public void SetOutputDestination(Logger stream)
        {
            @out = stream;
        }

        public object Checkpoint()
        {
            return stack.Count;
        }

        public void Recover(object checkpoint, XPathException error)
        {
            @out.Info(AbstractTraceListener.Spaces(indent) + "<error code='" + error.ErrorCodeQName.GetLocalPart() + "'>" + error.Message + "</error>");
            while (stack.Count > (int)checkpoint)
            {
                int size = stack.Count;
                object o = stack.Peek();
                if (o is ITraceable)
                {
                    Leave((ITraceable)o);
                }
                else if (o is IItem)
                {
                    EndCurrentItem((IItem)o);
                }

                if (stack.Count == size)
                {
                    stack.Pop();
                }
            }

            @out.Info(AbstractTraceListener.Spaces(indent) + "<catch/>");
        }

        public void EndRuleSearch(object rule, Mode mode, IItem item)
        {
        }

        // do nothing
        /// <summary>
        /// Method called when a search for a template rule is about to start
        /// </summary>
        public void StartRuleSearch()
        {
        }
    }
}
