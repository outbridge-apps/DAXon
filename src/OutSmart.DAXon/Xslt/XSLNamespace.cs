////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
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
    /// An xsl:namespace element in the stylesheet. (XSLT 2.0)
    /// </summary>
    internal sealed class XSLNamespace : XSLLeafNodeConstructor
    {
        Expression name;

        protected override string ErrorCodeForSelectPlusContent => "XTSE0910";
        public override void PrepareAttributes()
        {
            name = PrepareAttributesNameAndSelect();
        }

        public override void Validate(ComponentDeclaration decl)
        {
            name = TypeCheck("name", name);
            select = TypeCheck("select", select);
            int countChildren = 0;
            NodeInfo firstChild = null;
            foreach (NodeInfo child in Children())
            {
                if (child is XSLFallback)
                {
                    continue;
                }

                if (select != null)
                {
                    string errorCode = ErrorCodeForSelectPlusContent;
                    CompileError("An " + DisplayName + " element with a select attribute must be empty", errorCode);
                }

                countChildren++;
                if (firstChild == null)
                {
                    firstChild = child;
                }
                else
                {
                    break;
                }
            }

            if (select == null)
            {
                if (countChildren == 0)
                {

                    // there are no child nodes and no select attribute
                    select = new StringLiteral(StringValue.EMPTY_STRING);
                    select.SetRetainedStaticContext(MakeRetainedStaticContext());
                }
                else if (countChildren == 1)
                {

                    // there is exactly one child node
                    if (firstChild.GetNodeKind() == Types.Type.TEXT)
                    {

                        // it is a text node: optimize for this case
                        select = new StringLiteral(firstChild.UnicodeStringValue);
                        select.SetRetainedStaticContext(MakeRetainedStaticContext());
                    }
                }
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            NamespaceConstructor inst = new NamespaceConstructor(name);
            CompileContent(exec, decl, inst, new StringLiteral(StringValue.SINGLE_SPACE));
            return inst.WithLocation(SaveLocation());
        }
    }
}