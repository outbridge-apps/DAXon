////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Xslt
{
    internal abstract class AttributeValueTemplate
    {

        public static Expression Make(string avt, IStaticContext env)
        {
            IList<Expression> components = new List<Expression>(5);
            int i0, i1, i8, i9;
            int len = avt.Length;
            int last = 0;
            while (last < len)
            {
                i0 = avt.IndexOf('{', last);
                i1 = avt.IndexOf("{{", last, StringComparison.Ordinal);
                i8 = avt.IndexOf('}', last);
                i9 = avt.IndexOf("}}", last, StringComparison.Ordinal);
                if ((i0 < 0 || len < i0) && (i8 < 0 || len < i8))
                {

                    // found end of string
                    AddStringComponent(components, avt, last, len);
                    break;
                } // found a "}"
                else if (i8 >= 0 && (i0 < 0 || i8 < i0))
                {

                    // found a "}"
                    if (i8 != i9)
                    {

                        // a "}" that isn't a "}}"
                        throw new XPathException("Closing curly brace in attribute value template \"" + avt.Substring(0, len) + "\" must be doubled").WithErrorCode("XTSE0370").AsStaticError();
                    }

                    AddStringComponent(components, avt, last, i8 + 1);
                    last = i8 + 2;
                } // found a doubled "{{"
                else if (i1 >= 0 && i1 == i0)
                {

                    // found a doubled "{{"
                    AddStringComponent(components, avt, last, i1 + 1);
                    last = i1 + 2;
                } // found a single "{"
                else if (i0 >= 0)
                {

                    // found a single "{"
                    if (i0 > last)
                    {
                        AddStringComponent(components, avt, last, i0);
                    }

                    Expression exp;
                    XPathParser parser = env.GetConfiguration().NewExpressionParser("XP", false, env);

                    parser.SetAllowAbsentExpression(true);
                    exp = parser.Parse(avt, i0 + 1, Token.RCURLY, env);
                    exp.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                    exp = exp.Simplify();
                    last = parser.GetTokenizer().currentTokenStartOffset + 1;
                    if (env is ExpressionContext && ((ExpressionContext)env).GetStyleElement() is XSLAnalyzeString && IsIntegerOrIntegerPair(exp))
                    {
                        env.IssueWarning("Found {" + ShowIntegers(exp) + "} in regex attribute: perhaps {{" + ShowIntegers(exp) + "}} was intended? (The attribute is an AVT, so curly braces should be doubled)", DAXonErrorCode.SXWN9036, exp.GetLocation());
                    }

                    if (env.IsInBackwardsCompatibleMode())
                    {
                        components.Add(MakeFirstItem(exp, env));
                    }
                    else
                    {
                        components.Add(XSLLeafNodeConstructor.MakeSimpleContentConstructor(exp, new StringLiteral(StringValue.SINGLE_SPACE), env).Simplify());
                    }
                }
                else
                {
                    throw new InvalidOperationException("Internal error parsing AVT");
                }
            }

            Expression result;

            // is it empty?
            if (components.Count == 0)
            {
                result = new StringLiteral(StringValue.EMPTY_STRING);
            }
            else
            // is it a single component?
            if (components.Count == 1)
            {
                result = components[0].Simplify();
            }
            else

            // otherwise, return an expression that concatenates the components
            {
                Expression[] args = components.ToArray();
                Expression fn = SystemFunction.MakeCall("concat", new RetainedStaticContext(env), args);
                result = fn.Simplify();
            }

            result.SetLocation(env.GetContainingLocation());
            return result;
        }

        private static bool IsIntegerOrIntegerPair(Expression exp)
        {
            if (exp is Literal)
            {
                IGroundedValue val = ((Literal)exp).GroundedValue;
                if (val is IntegerValue)
                {
                    return true;
                }

                if (val.GetLength() == 2)
                {
                    return val.ItemAt(0) is IntegerValue && val.ItemAt(1) is IntegerValue;
                }
            }

            return false;
        }

        private static string ShowIntegers(Expression exp)
        {
            if (exp is Literal)
            {
                IGroundedValue val = ((Literal)exp).GroundedValue;
                if (val is IntegerValue)
                {
                    return val.ToString();
                }

                if (val.GetLength() == 2)
                {
                    if (val.ItemAt(0) is IntegerValue && val.ItemAt(1) is IntegerValue)
                    {
                        return val.ItemAt(0).ToString() + "," + val.ItemAt(1).ToString();
                    }
                }
            }

            return "";
        }

        private static void AddStringComponent(IList<Expression> components, string avt, int start, int end)
        {
            if (start < end)
            {
                components.Add(new StringLiteral(avt.Substring(start, end - start)));
            }
        }

        public static Expression MakeFirstItem(Expression exp, IStaticContext env)
        {
            if (Literal.IsEmptySequence(exp))
            {
                return exp;
            }

            TypeHierarchy th = env.GetConfiguration().GetTypeHierarchy();
            if (!exp.GetItemType().IsPlainType())
            {
                exp = Atomizer.MakeAtomizer(exp, null);
            }

            if (Cardinality.AllowsMany(exp.GetCardinality()))
            {
                exp = FirstItemExpression.MakeFirstItemExpression(exp);
            }

            if (!th.IsSubType(exp.GetItemType(), BuiltInAtomicType.STRING))
            {
                exp = new AtomicSequenceConverter(exp, BuiltInAtomicType.STRING);
                ((AtomicSequenceConverter)exp).AllocateConverterStatically(env.GetConfiguration(), false);
            }

            return exp;
        }
    }
}