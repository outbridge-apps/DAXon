////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
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
    /// Handler for xsl:for-each elements in stylesheet. <br>
    /// </summary>
    internal sealed class XSLForEach : StyleElement
    {
        private Expression select = null;
        private bool containsTailCall = false;
        private Expression threads = null;
        private Expression separator = null;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool IsPermittedChild(StyleElement child)
        {
            return (child is XSLSort);
        }

        public override bool MarkTailCalls()
        {
            if (Cardinality.AllowsMany(select.GetCardinality()))
            {
                return false;
            }
            else
            {
                StyleElement last = LastChildInstruction;
                containsTailCall = last != null && last.MarkTailCalls();
                return containsTailCall;
            }
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("select"))
                {
                    select = MakeExpression(value, att);
                }
                else if (f.Equals("separator"))
                {
                    if (RequireXslt40Attribute("separator"))
                    {
                        separator = MakeAttributeValueTemplate(value, att);
                    }
                }
                else if (attName.GetLocalPart().Equals("threads") && attName.HasURI(NamespaceUri.SAXON))
                {
                    threads = MakeAttributeValueTemplate(Whitespace.Trim(value), att);
                    if (GetCompilation().GetCompilerInfo().IsCompileWithTracing())
                    {
                        IssueWarning("saxon:threads - no multithreading takes place when compiling with trace enabled", DAXonErrorCode.SXWN9012);
                        threads = new StringLiteral("0");
                    }
                    else if (!"EE".Equals(GetConfiguration().EditionCode))
                    {
                        IssueWarning("saxon:threads - ignored when not running Saxon-EE", DAXonErrorCode.SXWN9013);
                        threads = new StringLiteral("0");
                    }
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (select == null)
            {
                ReportAbsence("select");
                select = Literal.MakeEmptySequence();
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            CheckSortComesFirst(false);
            select = TypeCheck("select", select);
            if (separator != null)
            {
                separator = TypeCheck("separator", separator);
            }

            if (threads != null)
            {
                threads = TypeCheck("threads", threads);
            }

            if (!HasChildNodes())
            {
                IssueWarning("An empty xsl:for-each instruction has no effect", DAXonErrorCode.SXWN9009);
            }
        }

        public override Expression Compile(Compilation compilation, ComponentDeclaration decl)
        {
            SortKeyDefinitionList sortKeys = MakeSortKeys(compilation, decl);
            Expression sortedSequence = select;
            if (sortKeys != null)
            {
                sortedSequence = new SortExpression(select, sortKeys);
            }

            Expression block = CompileSequenceConstructor(compilation, decl, true);
            if (block == null)
            {

                // body of for-each is empty: it's a no-op.
                return Literal.MakeEmptySequence();
            }

            try
            {
                ForEach result = new ForEach(sortedSequence, block.Simplify(), containsTailCall, threads);
                result.SetInstruction(true);
                result.SetLocation(AllocateLocation());
                if (separator != null)
                {
                    result.SeparatorExpression = separator;
                }

                return result;
            }
            catch (XPathException err)
            {
                CompileError(err);
                return null;
            }
        }
    }
}