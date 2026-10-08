////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    internal abstract class XSLLeafNodeConstructor : StyleElement
    {
        protected Expression select;

        protected abstract string ErrorCodeForSelectPlusContent { get; }
        protected virtual Expression PrepareAttributesNameAndSelect()
        {
            Expression name = null;
            string nameAtt = null;
            string selectAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("name"))
                {
                    nameAtt = Whitespace.Trim(value);
                    name = MakeAttributeValueTemplate(nameAtt, att);
                }
                else if (f.Equals("select"))
                {
                    selectAtt = value;
                    select = MakeExpression(selectAtt, att);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (nameAtt == null)
            {
                ReportAbsence("name");
            }

            return name;
        }

        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (select != null && HasChildNodes())
            {
                string errorCode = ErrorCodeForSelectPlusContent;
                CompileError("An " + DisplayName + " element with a select attribute must be empty", errorCode);
            }

            IAxisIterator kids = IterateAxis(AxisInfo.CHILD);
            NodeInfo first = kids.Next();
            if (select == null)
            {
                if (first == null)
                {

                    // there are no child nodes and no select attribute
                    //stringValue = "";
                    select = new StringLiteral(StringValue.EMPTY_STRING);
                    select.SetRetainedStaticContext(MakeRetainedStaticContext());
                }
                else
                {
                    if (kids.Next() == null && !IsExpandingText())
                    {

                        // there is exactly one child node
                        if (first.GetNodeKind() == Types.Type.TEXT)
                        {

                            // it is a text node: optimize for this case
                            select = new StringLiteral(first.UnicodeStringValue);
                            select.SetRetainedStaticContext(MakeRetainedStaticContext());
                        }
                    }
                }
            }
        }
        protected virtual void CompileContent(Compilation exec, ComponentDeclaration decl, SimpleNodeConstructor inst, Expression separator)
        {
            if (separator == null)
            {
                separator = new StringLiteral(StringValue.SINGLE_SPACE);
            }

            try
            {
                if (select == null)
                {
                    select = CompileSequenceConstructor(exec, decl, true);
                }

                select = MakeSimpleContentConstructor(select, separator, GetStaticContext());
                inst.Select = select;
            }
            catch (XPathException err)
            {
                CompileError(err);
            }
        }

        public static Expression MakeSimpleContentConstructor(Expression select, Expression separator, IStaticContext env)
        {
            RetainedStaticContext rsc = select.LocalRetainedStaticContext;
            if (rsc == null)
            {
                rsc = env.MakeRetainedStaticContext();
            }


            // Merge adjacent text nodes (also removes zero-length text nodes — spec phase 1 of
            // constructing simple content; was commented out as "streaming not used", but the
            // class is not about streaming: message-0304 kept empty text items in xsl:value-of).
            select = AdjacentTextNodeMerger.MakeAdjacentTextNodeMerger(select);

            // Atomize the result
            select = Atomizer.MakeAtomizer(select, null);

            // Convert each atomic value to a string
            select = new AtomicSequenceConverter(select, BuiltInAtomicType.STRING);
            select.SetRetainedStaticContext(rsc);
            ((AtomicSequenceConverter)select).AllocateConverterStatically(env.GetConfiguration(), false);

            // Join the resulting strings with a separator
            if (select.GetCardinality() != StaticProperty.EXACTLY_ONE)
            {
                select = SystemFunction.MakeCall("string-join", rsc, select, separator);
            }


            // All that's left for the instruction to do is to construct the right kind of node
            return select;
        }
    }
}