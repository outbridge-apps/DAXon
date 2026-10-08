////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An instruction representing an xsl:comment element in the stylesheet.
    /// </summary>
    internal sealed class Comment : SimpleNodeConstructor
    {

        private static readonly UnicodeString TWO_HYPHENS = new Twine8(StringConstants.TWO_HYPHENS);

        public override int InstructionNameCode => StandardNames.XSL_COMMENT;
        public Comment()
        {
        }

        public override Types.ItemType GetItemType()
        {
            return NodeKindTest.COMMENT;
        }

        public override int GetCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Comment exp = new Comment();
            ExpressionTool.CopyLocationInfo(this, exp);
            exp.Select = Select.Copy(rebindings);
            return exp;
        }

        public override void LocalTypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {

            // Do early checking of content if known statically
            if (Select is Literal)
            {
                UnicodeString s = ((Literal)Select).GroundedValue.UnicodeStringValue;
                UnicodeString s2 = CheckContent(s, visitor.StaticContext.MakeEarlyEvaluationContext());
                if (!s2.Equals(s))
                {
                    Select = new StringLiteral(s2);
                }
            }
        }

        public override void ProcessValue(UnicodeString value, Outputter output, IXPathContext context)
        {
            UnicodeString comment = CheckContent(value, context);
            output.Comment(comment, GetLocation(), ReceiverOption.NONE);
        }

        public override UnicodeString CheckContent(UnicodeString comment, IXPathContext context)
        {
            if (IsXSLT())
            {
                return CheckContentXSLT(comment);
            }
            else
            {
                try
                {
                    return CheckContentXQuery(comment);
                }
                catch (XPathException err)
                {
                    throw err.WithXPathContext(context).WithLocation(GetLocation());
                }
            }
        }

        public static UnicodeString CheckContentXSLT(UnicodeString comment)
        {
            string message = IInvalidity(comment);
            if (message != null)
            {
                long hh;
                while ((hh = comment.IndexOf(TWO_HYPHENS, 0)) >= 0)
                {
                    comment = comment.Substring(0, hh + 1).Concat(StringConstants.SINGLE_SPACE).Concat(comment.Substring(hh + 1));
                }

                if (comment.CodePointAt(comment.Length() - 1) == '-')
                {
                    comment = comment.Concat(StringConstants.SINGLE_SPACE);
                }
            }

            return comment;
        }

        public static UnicodeString CheckContentXQuery(UnicodeString comment)
        {
            string message = IInvalidity(comment);
            if (message != null)
            {
                throw new XPathException(message, "XQDY0072");
            }

            return comment;
        }

        private static string IInvalidity(UnicodeString comment)
        {
            if (comment.IndexOf(TWO_HYPHENS, 0) >= 0)
            {
                return "Invalid characters (--) in comment";
            }

            if (comment.Length() > 0 && comment.CodePointAt(comment.Length() - 1) == '-')
            {
                return "Comment cannot end in '-'";
            }

            return null;
        }
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("comment", this);
            string flags = "";
            if (IsLocal())
            {
                flags += "l";
            }

            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            Select.Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new CommentElaborator();
        }

        private sealed class CommentElaborator : SimpleNodePushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                Comment expr = (Comment)GetExpression();
                ILocation loc = expr.GetLocation();
                IUnicodeStringEvaluator contentEval = expr.Select.MakeElaborator().ElaborateForUnicodeString(true);
                if (expr.IsXSLT())
                {
                    return (@out, context) =>
                    {
                        UnicodeString content = contentEval.Eval(context);
                        content = Comment.CheckContentXSLT(content);
                        @out.Comment(content, loc, ReceiverOption.NONE);
                        return null;
                    };
                }
                else
                {
                    return (@out, context) =>
                    {
                        UnicodeString content = contentEval.Eval(context);
                        Comment.CheckContentXQuery(content);
                        @out.Comment(content, loc, ReceiverOption.NONE);
                        return null;
                    };
                }
            }
        }
    }
}
