////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:perform-sort elements in stylesheet (XSLT 2.0). <br>
    /// </summary>
    internal sealed class XSLPerformSort : StyleElement
    {
        Expression select = null;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        protected override bool IsPermittedChild(StyleElement child)
        {
            return (child is XSLSort);
        }

        public override void PrepareAttributes()
        {
            string selectAtt = null;
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
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            CheckSortComesFirst(true);
            if (select != null)
            {

                // if there is a select attribute, check that there are no children other than xsl:sort and xsl:fallback
                foreach (NodeInfo child in Children())
                {
                    if (child is XSLSort || child is XSLFallback)
                    {
                    }
                    else if (child.GetNodeKind() == Types.Type.TEXT && !Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {

                        // with xml:space=preserve, white space nodes may still be there
                        CompileError("Within xsl:perform-sort, significant text must not appear if there is a select attribute", "XTSE1040");
                    }
                    else
                    {
                        ((StyleElement)child).CompileError("Within xsl:perform-sort, child instructions are not allowed if there is a select attribute", "XTSE1040");
                    }
                }
            }

            select = TypeCheck("select", select);
        }

        // no action
        public override Expression Compile(Compilation compilation, ComponentDeclaration decl)
        {
            SortKeyDefinitionList sortKeys = MakeSortKeys(compilation, decl);
            if (select != null)
            {
                return new SortExpression(select, sortKeys);
            }
            else
            {
                Expression body = CompileSequenceConstructor(compilation, decl, true);
                if (body == null)
                {
                    body = Literal.MakeEmptySequence();
                }

                try
                {
                    return new SortExpression(body.Simplify(), sortKeys);
                }
                catch (XPathException e)
                {
                    CompileError(e);
                    return null;
                }
            }
        }
    }
}